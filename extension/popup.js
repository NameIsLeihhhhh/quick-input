/* DSH Quick Bridge · popup 面板
 * 只做三件事：显示状态、发一条测试、探测页面结构。
 */

const DSH_URLS = ['http://127.0.0.1:3080/*', 'http://localhost:3080/*'];

const $ = id => document.getElementById(id);
const out = msg => { $('out').textContent = msg; };

function fmtTime(ts) {
  if (!ts) return '—';
  const d = new Date(ts);
  const p = n => String(n).padStart(2, '0');
  return p(d.getHours()) + ':' + p(d.getMinutes()) + ':' + p(d.getSeconds());
}

async function refresh() {
  const v = await chrome.storage.local.get('state');
  const st = v.state;

  if (st) {
    $('dot').className = 'dot ' + (st.ok ? 'ok' : 'bad');
    $('v-state').textContent = st.msg || (st.ok ? '正常' : '异常');
    $('v-time').textContent = fmtTime(st.at);
  } else {
    $('dot').className = 'dot';
    $('v-state').textContent = '还没开始（后台可能刚被唤醒）';
  }

  try {
    const tabs = await chrome.tabs.query({ url: DSH_URLS });
    $('v-tabs').textContent = tabs.length
      ? tabs.length + ' 个' + (tabs.some(t => t.discarded) ? '（有被回收的）' : '')
      : '没有（会自动开一个后台标签页）';
  } catch (e) {
    $('v-tabs').textContent = '查询失败';
  }
}

$('btn-test').addEventListener('click', async () => {
  const btn = $('btn-test');
  btn.disabled = true;
  out('正在投递…');

  try {
    const r = await chrome.runtime.sendMessage({
      type: 'TEST',
      text: '来自扩展面板的测试：' + new Date().toLocaleTimeString()
    });
    if (r && r.ok) {
      out('✔ 成功：' + (r.msg || 'ok'));
    } else {
      out('✘ 失败：' + ((r && r.msg) || '没有返回'));
    }
  } catch (e) {
    out('✘ 出错：' + (e && e.message ? e.message : e));
  }

  btn.disabled = false;
  refresh();
});

$('btn-probe').addEventListener('click', async () => {
  out('探测中…');
  try {
    const tabs = await chrome.tabs.query({ url: DSH_URLS });
    if (!tabs.length) {
      out('没有开着的 DSH 标签页，先打开 http://127.0.0.1:3080');
      return;
    }
    const r = await chrome.tabs.sendMessage(tabs[0].id, { type: 'PROBE' });
    const lines = [];

    if (r.composer) {
      lines.push('输入框：' + r.composer.tag +
        (r.composer.editable ? '（contenteditable）' : '') +
        (r.composer.empty ? ' · 空' : ' · 有内容') + ' ✔');
    } else {
      lines.push('输入框：没找到 ✘');
    }

    lines.push('新会话按钮：' + (r.newSession ? '「' + r.newSession + '」✔' : '没找到 ✘'));
    lines.push('发送按钮：' + (r.sendButton ? '「' + r.sendButton + '」✔' : '没找到（靠回车发）'));

    out(lines.join('\n'));
  } catch (e) {
    out('探测失败：' + (e && e.message ? e.message : e) +
        '（页面可能需要在装好扩展后刷新一次）');
  }
});

refresh();
setInterval(refresh, 1200);
