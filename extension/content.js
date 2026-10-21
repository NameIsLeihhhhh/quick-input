/* DSH Quick Bridge · content script
 *
 * 在 DSH 页面里替用户完成三件事：新会话 → 填字 → 发送。
 * 页面可以在后台标签页里，不需要激活窗口，也不需要抢焦点。
 *
 * 选择器不是猜的，是从 DSH 前端源码里读出来的
 * （@deepseek-ai/dsh-client-ui-conversation / -sidebar）：
 *   [data-composer-input]    Lexical 的 contenteditable 宿主
 *   [data-composer-card]     输入卡片容器（发送按钮在里面）
 *   [data-composer-placeholder]  占位符（空的时候才显示）
 *   aria-label="新建会话"      侧边栏的新会话按钮
 *   Enter 快捷键按 event.code 匹配，不按 event.key
 *   粘贴走 event.clipboardData.getData("text/plain")
 */
(() => {
  'use strict';

  // 版本 + 认领标记：扩展更新后，页面里的旧内容脚本可能还没死。
  // 谁最后被注入谁生效，只有认领标记还是自己时才响应消息，避免两份同时处理。
  const VERSION = 4;
  const claim = Math.random().toString(36).slice(2);
  window.__dshQuickBridgeClaim = claim;
  window.__dshQuickBridgeVersion = VERSION;

  const mine = () => window.__dshQuickBridgeClaim === claim;

  const SEL_COMPOSER = '[data-composer-input]';
  const SEL_CARD = '[data-composer-card]';
  const SEL_PLACEHOLDER = '[data-composer-placeholder]';
  const SEL_NEW_SESSION = '[aria-label="新建会话"], [aria-label="New session"], [class*="newSession"]';

  const sleep = ms => new Promise(r => setTimeout(r, ms));

  // 后台标签页里 setTimeout 会被节流到约 1 秒一次，轮询式的等待全部会超时。
  // 这里用 MutationObserver 盯 DOM 变化，它不受定时器节流影响；
  // setTimeout 只作最后的兜底。
  function waitFor(test, timeoutMs) {
    return new Promise(resolve => {
      let settled = false;
      let obs = null;
      let timer = null;

      const finish = v => {
        if (settled) return;
        settled = true;
        try { if (obs) obs.disconnect(); } catch (e) { /* ignore */ }
        if (timer) clearTimeout(timer);
        resolve(v || null);
      };

      let first = null;
      try { first = test(); } catch (e) { /* ignore */ }
      if (first) return finish(first);

      obs = new MutationObserver(() => {
        let v = null;
        try { v = test(); } catch (e) { /* ignore */ }
        if (v) finish(v);
      });

      try {
        obs.observe(document.documentElement, {
          childList: true, subtree: true, attributes: true, characterData: true
        });
      } catch (e) { /* ignore */ }

      timer = setTimeout(() => {
        let v = null;
        try { v = test(); } catch (e) { /* ignore */ }
        finish(v);
      }, timeoutMs);
    });
  }

  /* ── 小工具 ─────────────────────────────────────────── */

  function isVisible(el) {
    if (!el || !el.getBoundingClientRect) return false;
    const r = el.getBoundingClientRect();
    if (r.width < 2 || r.height < 2) return false;
    let st;
    try { st = getComputedStyle(el); } catch (e) { return false; }
    if (!st) return false;
    if (st.visibility === 'hidden' || st.display === 'none') return false;
    return true;
  }

  function readBack(el) {
    if (!el) return '';
    if (typeof el.value === 'string') return el.value;
    return el.innerText || el.textContent || '';
  }

  function head(s, n) {
    return (s || '').slice(0, n || 8);
  }

  /* ── 页面内提示条 ───────────────────────────────────── */

  function toast(text, ms) {
    let el = document.getElementById('__qi_bridge_toast');
    if (!el) {
      el = document.createElement('div');
      el.id = '__qi_bridge_toast';
      el.style.cssText = [
        'position:fixed', 'right:18px', 'bottom:18px', 'z-index:2147483647',
        'max-width:380px', 'padding:10px 14px', 'border-radius:9px',
        'background:#12181f', 'color:#dbe6f2', 'font-size:12px', 'line-height:1.5',
        'border:1px solid #2c3a4c', 'box-shadow:0 10px 34px rgba(0,0,0,.55)',
        'font-family:-apple-system,"Segoe UI","Microsoft YaHei",sans-serif',
        'pointer-events:none', 'opacity:0', 'transition:opacity .22s',
        'white-space:pre-wrap'
      ].join(';');
      document.documentElement.appendChild(el);
    }
    el.textContent = text;
    el.style.opacity = '1';
    clearTimeout(el.__t);
    el.__t = setTimeout(() => { el.style.opacity = '0'; }, ms || 3200);
  }

  /* ── 找新会话按钮 ───────────────────────────────────── */

  function findNewSessionButton() {
    let el = document.querySelector(SEL_NEW_SESSION);
    // 被折叠的侧边栏里也有同名按钮，挑可见的
    if (el && !isVisible(el)) {
      const all = document.querySelectorAll(SEL_NEW_SESSION);
      for (let i = 0; i < all.length; i++) {
        if (isVisible(all[i])) { el = all[i]; break; }
      }
    }
    if (el && isVisible(el)) return el;

    // 兜底：按文字找
    const words = ['新会话', '新建会话', 'new session', 'new chat'];
    const list = document.querySelectorAll('button, a, [role="button"]');
    for (let i = 0; i < list.length && i < 4000; i++) {
      const t = (list[i].textContent || '').trim().toLowerCase();
      if (!t || t.length > 16) continue;
      for (let k = 0; k < words.length; k++) {
        if (t.indexOf(words[k]) >= 0 && isVisible(list[i])) return list[i];
      }
    }
    return null;
  }

  /* ── 找输入框 ───────────────────────────────────────── */

  function findComposer() {
    const list = document.querySelectorAll(SEL_COMPOSER);
    for (let i = 0; i < list.length; i++) {
      if (isVisible(list[i])) return list[i];
    }
    // 兜底：通用 contenteditable
    const gen = document.querySelectorAll('[contenteditable="true"]');
    for (let i = 0; i < gen.length; i++) {
      if (isVisible(gen[i])) return gen[i];
    }
    return null;
  }

  function composerEmpty(box) {
    return readBack(box).trim().length === 0;
  }

  /* ── 填字：三种策略，逐个试并验证 ───────────────────── */

  function fillViaPaste(box, text) {
    // DSH 的 DraftEditor 监听 paste 并读 clipboardData.getData("text/plain")
    try {
      const dt = new DataTransfer();
      dt.setData('text/plain', text);
      const ev = new ClipboardEvent('paste', {
        bubbles: true,
        cancelable: true,
        clipboardData: dt
      });
      box.dispatchEvent(ev);
      return true;
    } catch (e) {
      return false;
    }
  }

  function fillViaExecCommand(box, text) {
    try {
      box.focus();
      const sel = window.getSelection();
      const range = document.createRange();
      range.selectNodeContents(box);
      sel.removeAllRanges();
      sel.addRange(range);
      return document.execCommand('insertText', false, text);
    } catch (e) {
      return false;
    }
  }

  function fillViaTextContent(box, text) {
    try {
      box.textContent = text;
      box.dispatchEvent(new InputEvent('input', {
        bubbles: true,
        cancelable: false,
        data: text,
        inputType: 'insertText'
      }));
      return true;
    } catch (e) {
      return false;
    }
  }

  async function fillComposer(box, text) {
    const probe = head(text, 8);
    const plan = [fillViaPaste, fillViaExecCommand, fillViaTextContent];

    for (let i = 0; i < plan.length; i++) {
      try { plan[i](box, text); } catch (e) { /* 试下一种 */ }
      const ok = await waitFor(() => readBack(box).indexOf(probe) >= 0 ? true : null, 1800);
      if (ok) return { ok: true, how: plan[i].name };
    }
    return { ok: false, how: 'none' };
  }

  /* ── 发送 ───────────────────────────────────────────── */

  function findSendButton(box) {
    const card = box.closest(SEL_CARD) || document.querySelector(SEL_CARD) || document.body;
    const list = card.querySelectorAll('button[aria-label]');
    for (let i = 0; i < list.length; i++) {
      const b = list[i];
      const label = b.getAttribute('aria-label') || '';
      if (!/发送|send|submit/i.test(label)) continue;
      if (!isVisible(b) || b.disabled) continue;
      return b;
    }
    // 兜底：卡片里最后一个非禁用的按钮通常是发送
    const btns = card.querySelectorAll('button');
    for (let i = btns.length - 1; i >= 0; i--) {
      if (isVisible(btns[i]) && !btns[i].disabled) return btns[i];
    }
    return null;
  }

  function pressEnter(box) {
    // DSH 的快捷键按 event.code 匹配（见 fixed.send 绑定），所以 code 必须写对
    const init = {
      key: 'Enter',
      code: 'Enter',
      keyCode: 13,
      which: 13,
      bubbles: true,
      cancelable: true
    };
    box.dispatchEvent(new KeyboardEvent('keydown', init));
    box.dispatchEvent(new KeyboardEvent('keypress', init));
    box.dispatchEvent(new KeyboardEvent('keyup', init));
  }

  async function sendComposer(box, text) {
    const probe = head(text, 8);

    // 「已发出」的判定要两个条件同时成立：
    //   ① 输入框里已经没有这段文字（DSH 发送后会清空）
    //   ② 页面里能找得到这段文字（消息气泡出现了）
    // 只看①容易误判：万一文字是被别的逻辑清掉的，会以为发了。
    const sent = () => {
      if (readBack(box).indexOf(probe) >= 0) return null;
      const body = document.body.innerText || '';
      return body.indexOf(probe) >= 0 ? true : null;
    };

    // ① 回车（DSH 默认 Enter 发送）
    try { box.focus(); } catch (e) { /* ignore */ }
    pressEnter(box);
    if (await waitFor(sent, 2000)) return { ok: true, how: 'enter' };

    // ② 点发送按钮
    const btn = findSendButton(box);
    if (btn) {
      try { btn.click(); } catch (e) { /* ignore */ }
      if (await waitFor(sent, 2000)) return { ok: true, how: 'button' };
    }

    // 文字还卡在输入框里 —— 重试只会重复填一次，不能再试
    const kept = readBack(box).indexOf(probe) >= 0;
    return { ok: false, kept: kept, how: 'none' };
  }

  /* ── 主流程 ─────────────────────────────────────────── */

  async function inject(text, mode, final) {
    const t0 = Date.now();
    let note = '';

    // 1. 新会话
    if (mode === 'new') {
      const btn = findNewSessionButton();
      if (btn) {
        const before = location.href;
        const oldBox = findComposer();
        try { btn.click(); } catch (e) { /* ignore */ }
        // 等路由变化或输入框被换成新的（MutationObserver，不受后台节流影响）
        await waitFor(
          () => (location.href !== before || findComposer() !== oldBox) ? true : null,
          2500
        );
      } else {
        note = '（没找到「新建会话」按钮，发到当前会话）';
      }
    }

    // 2. 找输入框
    let box = findComposer();
    if (!box) box = await waitFor(() => findComposer(), 4000);
    if (!box) {
      if (final) toast('⚠ QuickInput：没找到 DSH 的输入框。\n可能是页面没加载完，或 DSH 界面变了。');
      return { ok: false, code: 'E_NO_COMPOSER', retry: true, msg: '找不到输入框' };
    }

    // 3. 填字
    const f = await fillComposer(box, text);
    if (!f.ok) {
      if (final) toast('⚠ QuickInput：内容填不进去（编辑器拒收）。\n把这条消息告诉我就能修。', 6000);
      return { ok: false, code: 'E_FILL', retry: true, msg: '填字失败' };
    }

    await sleep(180);

    // 4. 发送
    const s = await sendComposer(box, text);
    const sec = ((Date.now() - t0) / 1000).toFixed(1);

    if (s.ok) {
      toast('✔ 已送入 DSH 新会话' + note + '（' + sec + 's）');
      return { ok: true, msg: f.how + ' / ' + s.how };
    }

    if (s.kept) {
      // 文字还在框里：重试会重复填，只能把现状告诉人
      toast('⚠ QuickInput：文字已放进 DSH 输入框，但发送没成功。\n切过去按一下回车就行。', 6000);
      return {
        ok: false, code: 'E_SEND_KEPT', retry: false,
        msg: '已填入但发送失败（文字留在输入框里，不再重试）'
      };
    }

    if (final) toast('⚠ QuickInput：发送没成功，内容可能没进去。', 6000);
    return { ok: false, code: 'E_SEND', retry: true, msg: '发送没成功' };
  }

  /* ── 消息通道 ───────────────────────────────────────── */

  chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
    if (!mine()) return false;

    if (msg && msg.type === 'PING') {
      sendResponse({ ok: true, v: VERSION });
      return false;
    }
    if (msg && msg.type === 'INJECT') {
      inject(msg.text || '', msg.mode || 'new', msg.final !== false)
        .then(r => sendResponse(r))
        .catch(e => sendResponse({ ok: false, code: 'E_EXCEPTION', retry: true, msg: String(e) }));
      return true;
    }
    if (msg && msg.type === 'PROBE') {
      const box = findComposer();
      const nb = findNewSessionButton();
      const sb = box ? findSendButton(box) : null;
      sendResponse({
        ok: true,
        url: location.href,
        composer: box ? {
          tag: box.tagName,
          editable: !!box.isContentEditable,
          empty: composerEmpty(box),
          hasPlaceholder: !!document.querySelector(SEL_PLACEHOLDER)
        } : null,
        newSession: nb ? (nb.getAttribute('aria-label') || nb.textContent || '').trim().slice(0, 24) : null,
        sendButton: sb ? (sb.getAttribute('aria-label') || '(无标签)') : null
      });
      return false;
    }
    return false;
  });
})();
