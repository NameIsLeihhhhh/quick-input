/* DSH Quick Bridge · background service worker
 *
 * 职责：轮询 QuickInput 的本地队列 → 找到 DSH 标签页 → 让 content script 注入 → 回报结果。
 * 关键点：标签页可以在后台，不激活、不抢焦点。
 */

const BRIDGE = 'http://127.0.0.1:47321';
const DSH_URLS = ['http://127.0.0.1:3080/*', 'http://localhost:3080/*'];
const DSH_HOME = 'http://127.0.0.1:3080/';
const POLL_MS = 700;
const CONTENT_VERSION = 4;
const MAX_ATTEMPTS = 3;

let loopTimer = null;
let busy = false;

function sleep(ms) {
  return new Promise(r => setTimeout(r, ms));
}

async function setState(ok, msg) {
  try {
    await chrome.storage.local.set({
      state: { ok, msg, at: Date.now() }
    });
  } catch (e) { /* ignore */ }
}

/* ── 找标签页 ───────────────────────────────────────────── */

function waitTabComplete(tabId, timeoutMs) {
  return new Promise(resolve => {
    let done = false;
    const finish = ok => {
      if (done) return;
      done = true;
      chrome.tabs.onUpdated.removeListener(onUpd);
      clearTimeout(timer);
      resolve(ok);
    };
    const onUpd = (id, info) => {
      if (id === tabId && info.status === 'complete') finish(true);
    };
    const timer = setTimeout(() => finish(false), timeoutMs);
    chrome.tabs.onUpdated.addListener(onUpd);
    chrome.tabs.get(tabId).then(t => {
      if (t && t.status === 'complete') finish(true);
    }).catch(() => finish(false));
  });
}

async function getDshTab() {
  let tabs = [];
  try {
    tabs = await chrome.tabs.query({ url: DSH_URLS });
  } catch (e) {
    tabs = [];
  }

  if (tabs.length) {
    // 优先拿一个没被丢弃的
    let tab = tabs.find(t => !t.discarded) || tabs[0];
    if (tab.discarded) {
      try {
        await chrome.tabs.reload(tab.id);
        await waitTabComplete(tab.id, 20000);
      } catch (e) { /* ignore */ }
    }
    return tab;
  }

  // 一个都没开：偷偷开一个后台标签页，不弹到前台
  try {
    const tab = await chrome.tabs.create({ url: DSH_HOME, active: false });
    await waitTabComplete(tab.id, 20000);
    await sleep(600);
    return await chrome.tabs.get(tab.id);
  } catch (e) {
    return null;
  }
}

/* ── 确保 content script 在位 ───────────────────────────── */

async function ensureContentScript(tabId) {
  try {
    const r = await chrome.tabs.sendMessage(tabId, { type: 'PING' });
    if (r && r.ok && r.v === CONTENT_VERSION) return true;
  } catch (e) { /* 没注入，或旧脚本已失效 */ }

  // 版本不对（或压根没有），直接注入最新的一份。
  // content.js 内部用认领标记让旧的那份闭嘴，所以不需要刷新页面。
  try {
    await chrome.scripting.executeScript({ target: { tabId }, files: ['content.js'] });
    await sleep(350);
    const r = await chrome.tabs.sendMessage(tabId, { type: 'PING' });
    return !!(r && r.ok && r.v === CONTENT_VERSION);
  } catch (e) {
    return false;
  }
}

/* ── 投递 ───────────────────────────────────────────────── */

async function deliver(item, attempt) {
  const tab = await getDshTab();
  if (!tab) return { ok: false, retry: true, msg: '没有可用的 DSH 标签页' };

  const alive = await ensureContentScript(tab.id);
  if (!alive) return { ok: false, retry: true, msg: '内容脚本没加载上' };

  // 第一次给足 30 秒，重试时缩短，免得一次失败把人卡住太久
  const wait = attempt > 1 ? 15000 : 30000;

  let reply = null;
  try {
    reply = await Promise.race([
      chrome.tabs.sendMessage(tab.id, {
        type: 'INJECT',
        text: item.text,
        mode: item.mode || 'new',
        final: attempt >= MAX_ATTEMPTS
      }),
      sleep(wait).then(() => ({ ok: false, retry: true, msg: '页面 ' + (wait / 1000) + ' 秒没回应' }))
    ]);
  } catch (e) {
    return { ok: false, retry: true, msg: '消息送不到页面：' + (e && e.message ? e.message : e) };
  }

  if (!reply) return { ok: false, retry: true, msg: '页面没有返回结果' };
  return {
    ok: !!reply.ok,
    // 内容脚本明确说不能重试（比如文字已留在输入框里），就不重试
    retry: reply.retry !== false,
    code: reply.code || '',
    msg: reply.msg || (reply.ok ? 'ok' : '失败')
  };
}

// 只在「重试是安全的」时候重试：
//   填不进去 / 没找到输入框 / 超时 → 页面上没留下痕迹，重试不会发两遍
//   已填入但发送失败（文字还在框里）→ 重试会重复填，直接放弃并把现状告诉人
async function deliverWithRetry(item) {
  let last = null;

  for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
    last = await deliver(item, attempt);

    if (last.ok) return { ok: true, msg: last.msg, attempts: attempt };
    if (!last.retry) return { ok: false, msg: last.msg, attempts: attempt, gaveUp: true };
    if (attempt < MAX_ATTEMPTS) await sleep(attempt === 1 ? 1200 : 2500);
  }

  return { ok: false, msg: last.msg, attempts: MAX_ATTEMPTS };
}

/* ── 轮询 ───────────────────────────────────────────────── */

async function pollOnce() {
  let res;
  try {
    res = await fetch(BRIDGE + '/poll?v=4', { cache: 'no-store' });
  } catch (e) {
    await setState(false, '本地桥没连上，QuickInput 在运行吗？');
    return;
  }

  if (res.status === 204) {
    await setState(true, '已连接，等待内容');
    return;
  }
  if (!res.ok) {
    await setState(false, '本地桥返回 HTTP ' + res.status);
    return;
  }

  let item;
  try {
    item = await res.json();
  } catch (e) {
    await setState(false, '本地桥返回的不是 JSON');
    return;
  }
  if (!item || !item.id || typeof item.text !== 'string') {
    await setState(false, '本地桥返回的内容不完整');
    return;
  }

  await setState(true, '正在投递…（' + item.text.length + ' 字）');

  let r;
  try {
    r = await deliverWithRetry(item);
  } catch (e) {
    r = { ok: false, msg: String((e && e.message) || e), attempts: 1 };
  }

  try {
    let ack = BRIDGE + '/ack?id=' + item.id + '&ok=' + (r.ok ? '1' : '0');
    if (!r.ok) ack += '&err=' + encodeURIComponent(r.msg || '');
    await fetch(ack, { cache: 'no-store' });
  } catch (e) { /* ignore */ }

  if (r.ok) {
    await setState(true, '已送达 DSH' + (r.attempts > 1 ? '（重试 ' + (r.attempts - 1) + ' 次后成功）' : ''));
  } else {
    await setState(false, '投递失败：' + r.msg +
      (r.attempts > 1 ? '（试了 ' + r.attempts + ' 次）' : '') +
      (r.gaveUp ? ' — 已停止重试' : ''));
  }
}

async function tick() {
  if (busy) return;
  busy = true;
  try {
    await pollOnce();
  } catch (e) {
    await setState(false, '轮询出错：' + String((e && e.message) || e));
  }
  busy = false;
}

function ensureLoop() {
  if (loopTimer) return;
  loopTimer = setInterval(tick, POLL_MS);
  tick();
}

/* ── 生命周期 ───────────────────────────────────────────── */

chrome.runtime.onInstalled.addListener(() => {
  ensureLoop();
  chrome.alarms.create('dsh-quick-watchdog', { periodInMinutes: 1 });
});

chrome.runtime.onStartup.addListener(() => {
  ensureLoop();
  chrome.alarms.create('dsh-quick-watchdog', { periodInMinutes: 1 });
});

chrome.alarms.onAlarm.addListener(a => {
  if (a.name === 'dsh-quick-watchdog') {
    // 若 service worker 被回收过，这里负责把循环拉回来
    if (!loopTimer) loopTimer = null;
    ensureLoop();
  }
});

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (msg && msg.type === 'TEST') {
    (async () => {
      const item = { id: 0, text: msg.text || '测试：来自扩展面板', mode: 'new' };
      const r = await deliverWithRetry(item);
      await setState(r.ok,
        r.ok ? '测试送达成功' + (r.attempts > 1 ? '（重试 ' + (r.attempts - 1) + ' 次）' : '')
             : '测试失败：' + r.msg);
      sendResponse(r);
    })();
    return true;
  }
  if (msg && msg.type === 'STATE') {
    chrome.storage.local.get('state').then(v => sendResponse(v.state || null));
    return true;
  }
});

ensureLoop();
