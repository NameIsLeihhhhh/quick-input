# QuickInput

按一下 **Alt+J**，屏幕正中弹出一个输入框。打完字回车，三种去向：

| 你输入的 | 会发生什么 |
|---|---|
| 普通文字 | 送进一个网页应用的输入框（不切窗口、不抢焦点、不碰剪贴板） |
| `别名 关键词`（如 `hh 800库`） | 打开浏览器，在该网站搜索 |
| `/关键词`（如 `/季度报告`） | 用 [Everything](https://www.voidtools.com/) 搜本机文件，列表里选中打开 |

**一个 92 KB 的 exe + 一个浏览器扩展 + 一个 ini。** 不装运行时（用 Windows 自带的 .NET Framework 4.8）、不联网、托盘常驻、没有主窗口。

👉 **想看效果**：直接打开 [`docs/demo.html`](docs/demo.html)（纯前端演示动画，不用装任何东西）。

---

## 为什么做这个

系统自带的「Win+S」搜的是网页和设置，搜不到你的文件；Everything 快但每次都要切窗口；
各种启动器要么重、要么要装一堆东西、要么得登录账号。

这个的思路是：**按一个键，打字，回车，然后继续干你原来的事。** 全程不离开当前窗口。

## 三个去向分别怎么工作

```
                  Alt+J（注册进系统的全局热键）
                        │
      ┌─────────────────▼──────────────────────────────┐
      │  QuickInput.exe   托盘常驻，平时没有任何窗口        │
      │   · 浮层界面：自己画的（圆角 / 类型图标 / 结果列表） │
      │   · 本地桥：HTTP 服务，只监听 127.0.0.1:47321      │
      │   · 配置：同目录 qui.ini                          │
      └───┬──────────────────────┬─────────────────────┘
          │                      │
  ┌───────▼────────┐    ┌────────▼──────────────────────┐
  │ Everything64.dll│    │ Chrome 扩展 extension/         │
  │ 直连 Everything │    │ 每 700ms 来桥取件               │
  │ 的内存索引      │    │ → 塞进目标网页的输入框并回车      │
  └────────────────┘    └───────────────────────────────┘
```

**为什么要拆成 exe + 扩展两块**：exe 能拿到全局热键和你的按键，但它碰不到浏览器里的网页。
所以 exe 把内容放进本地队列，扩展定时来取、再写进页面。两边都在你自己机器上，内容不出本机。

## 快速开始

### 1. 编译

需要 .NET Framework 4.8（Win10/11 自带，不用装）。

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

产出 `QuickInput.exe`。双击运行，托盘出现紫色 Q 图标。

### 2. 配置

首次运行会自动生成 `qui.ini`（仓里给了一份 `qui.ini.example` 可以直接改名用）。

```ini
Hotkey=Alt+J
Mode=new

[search]
hh = 小红书 | https://www.xiaohongshu.com/search_result?keyword={q}
```

- **Hotkey**：`Alt+J` / `Ctrl+Alt+J` / `Win+Space` / `Shift+Alt+K` / `F9`。改完重启生效。
- **Mode**：`new` 新会话 / `current` 追加到当前会话 / `clipboard` 老办法，粘贴到当前窗口。
- **[search]**：一行一个站点，`别名 = 显示名 | 网址模板`，模板里的 `{q}` 会被替换成搜索词（自动 URL 编码，中文没问题）。

### 3. 文件搜索（可选）

需要本机装了 Everything（并在运行）。**`Everything64.dll` 不随本仓分发**——它是 voidtools 的 SDK，
受它自己的许可约束。请从你自己的 Everything 安装目录拷一份到 exe 旁边：

```powershell
copy "C:\Program Files\Everything\Everything64.dll" .
```

（64 位 Everything 才有 `Everything64.dll`；如果你装的是 32 位版，拷 `Everything32.dll` 并改一下源码里的 P/Invoke 名字。）

### 4. 浏览器扩展（可选，只有「送内容进网页」这条需要）

1. Chrome 打开 `chrome://extensions` → 打开右上角「开发者模式」
2. 「加载已解压的扩展程序」→ 选本仓的 `extension/` 目录
3. 确认桥在线：`curl http://127.0.0.1:47321/health`，`connected` 应该是 `true`

> 扩展当前是**针对某一个网页应用（DSH）的 DOM** 写的，选择器在 `content.js` 里。
> 想换成别的网页应用，改那几个选择器即可——桥那一层是通用的。

## 用法细节

| 按键 | 作用 |
|---|---|
| `Enter` | 执行（发送 / 搜索 / 打开文件） |
| `Ctrl+Enter` | 文件模式：在资源管理器里定位该文件 |
| `↑` `↓` | 文件模式：在结果里选 |
| `Shift+Enter` | 换行 |
| `Esc` | 收起 |

- **点浮层外面的任何地方，它自己收起**，不用先按 Esc。
- **收起不丢内容**：再按 Alt+J，还是刚才那一屏——输入的文字、结果、选中第几条、滚到哪儿，全都在。
- **结果多于一屏**时列表跟着选中项滚，右侧有细滚动条，底部写「第 N / 共 M 条」。
- **鼠标**：滚轮一格一条，点一下选中，再点一下打开。

## 自检

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\selftest-all.ps1
```

28 项，覆盖本地桥、11 个搜索别名的解析、别名提示、文件搜索命中与滚动、收起后状态不丢、投递。

只监听本机的调试接口（可以单独用）：

```powershell
curl http://127.0.0.1:47321/health            # 桥状态、扩展版本、最近一次错误
curl "http://127.0.0.1:47321/search?q=hh%20800%E5%BA%93"   # 这句话会被解析成什么网址
curl "http://127.0.0.1:47321/debugfiles?q=/a&sel=10"       # 把浮层驱动到某个状态
curl http://127.0.0.1:47321/hide              # 收起浮层
```

日志：`%TEMP%\quickinput.log`。

## 目录结构

```
.
├── src/QuickInput.cs       主程序源码（单文件，约 103 KB）
├── build.ps1               编译脚本（用 Windows 自带的 csc.exe）
├── QuickInput.ico          托盘 / exe 图标，9 个尺寸
├── qui.ini.example         配置样例
├── selftest-all.ps1        全链路自检
├── extension/              Chrome MV3 扩展（本地桥的取件端）
└── docs/demo.html          演示动画（纯前端，双击就能看）
```

## 已知边界（设计取舍，不是 bug）

1. **文件搜索只匹配文件名**，不搜正文。要搜正文得在 Everything 里开内容索引——那是它的设置。
2. **别名会「抢首词」**：因为支持省略空格（`hh800库`），任何以别名开头的文字都会被当搜索——`bb机` 会去 B站搜「机」。真要发原话，行首加 `\`。
3. **`clipboard` 模式会覆盖剪贴板**，且要求目标输入框已经有焦点。默认模式不用剪贴板。
4. **管理员权限的窗口**：`clipboard` 模式下受 UIPI 限制。默认模式不受影响。
5. **搜索交给系统默认浏览器**，不是指定某个浏览器。
6. **热键独占**：全局热键一旦注册就被本程序独占。选了某个软件也在用的组合，那个功能会被抢走。

## 第三方

- [Everything](https://www.voidtools.com/) 的 SDK（`Everything64.dll`）——**未包含在本仓**，请自行获取。
- 界面字体优先用 [Noto Sans SC](https://fonts.google.com/noto/specimen/Noto+Sans+SC)，没装则退回系统自带的微软雅黑。

## License

MIT，见 [LICENSE](LICENSE)。