# QuickInput 全链路自测
# 覆盖：本地桥 / 搜索别名解析 / 只打别名的提示 / 不该当搜索的输入 / 唤起浏览器 / 送进 DSH
# 用法：powershell -NoProfile -ExecutionPolicy Bypass -File .\selftest-all.ps1
# 副作用：会弹一个浏览器标签页；会在 DSH 里建一个自测会话（可删）

$ErrorActionPreference = 'Continue'
$base = 'http://127.0.0.1:47321'
$pass = 0
$fail = 0

function T($name, $cond, $detail) {
    if ($cond) {
        $script:pass++
        Write-Host ("  PASS  {0,-28} {1}" -f $name, $detail) -ForegroundColor Green
    } else {
        $script:fail++
        Write-Host ("  FAIL  {0,-28} {1}" -f $name, $detail) -ForegroundColor Red
    }
}

function Get2($path) {
    (Invoke-WebRequest -Uri ($base + $path) -UseBasicParsing -TimeoutSec 8).Content
}

Write-Host ''
Write-Host '=== 1. 本地桥 ===' -ForegroundColor Cyan
$h = $null
try { $h = Get2 '/health' | ConvertFrom-Json } catch { }
if ($null -eq $h) {
    Write-Host '  桥没起来。先确认 QuickInput.exe 在运行（右下角托盘有图标）。' -ForegroundColor Red
    exit 1
}
T '桥在线' ($h.ok -eq $true) ("pending=" + $h.pending)
T '浏览器扩展已连接' ($h.connected -eq $true) ("client=" + $h.client)
T '扩展版本是 5' ($h.client -eq '5') ("client=" + $h.client)
if ($h.client -ne '5') {
    Write-Host '  提示：扩展版本不是 5，去 chrome://extensions 点一次刷新图标（附件功能需要 v5）。' -ForegroundColor Yellow
}

Write-Host ''
Write-Host '=== 2. 搜索别名解析 ===' -ForegroundColor Cyan
$cases = @(
    @{ t = 'hh800库';    label = '小红书'; host = 'xiaohongshu.com' },
    @{ t = 'hh 800库';   label = '小红书'; host = 'xiaohongshu.com' },
    @{ t = 'bb罗翔';     label = 'B站';    host = 'bilibili.com' },
    @{ t = 'dd做饭';     label = '抖音';   host = 'douyin.com' },
    @{ t = 'ggdeepseek'; label = 'Google'; host = 'google.com' },
    @{ t = 'zh测试';     label = '知乎';   host = 'zhihu.com' },
    @{ t = 'wb测试';     label = '微博';   host = 'weibo.com' },
    @{ t = 'db测试';     label = '豆瓣';   host = 'douban.com' },
    @{ t = 'tb测试';     label = '淘宝';   host = 'taobao.com' },
    @{ t = 'bd测试';     label = '百度';   host = 'baidu.com' },
    @{ t = 'gh测试';     label = 'GitHub'; host = 'github.com' }
)
foreach ($c in $cases) {
    $r = Get2 ('/search?q=' + [uri]::EscapeDataString($c.t)) | ConvertFrom-Json
    $ok = ($r.ok -eq $true) -and ($r.label -eq $c.label) -and ($r.url -match [regex]::Escape($c.host))
    T $c.t $ok ("-> " + $r.label)
}

Write-Host ''
Write-Host '=== 3. 只打别名（应认出但不打开）===' -ForegroundColor Cyan
foreach ($p in @('hh', 'bb', 'dd', 'gg')) {
    $r = Get2 ('/search?q=' + $p) | ConvertFrom-Json
    T $p (($r.ok -eq $false) -and ($r.peek -ne '')) ("已认出「" + $r.peek + "」")
}

Write-Host ''
Write-Host '=== 4. 不该当搜索的输入 ===' -ForegroundColor Cyan
foreach ($p in @('h', '普通一句话', '把上面三条整理成提纲', 'ds 测试')) {
    $r = Get2 ('/search?q=' + [uri]::EscapeDataString($p)) | ConvertFrom-Json
    T $p (($r.ok -eq $false) -and ([string]::IsNullOrEmpty($r.peek))) '走 DSH'
}

Write-Host ''
Write-Host '=== 5. 唤起浏览器（会弹一个标签页）===' -ForegroundColor Cyan
$r = Get2 ('/open?q=' + [uri]::EscapeDataString('gg quickinput selftest')) | ConvertFrom-Json
T '唤起默认浏览器' ($r.ok -eq $true) $r.url

Write-Host ''
Write-Host '=== 6. 送进 DSH（会新建一个会话）===' -ForegroundColor Cyan
$before = (Get2 '/health' | ConvertFrom-Json).delivered
$text = 'QuickInput 自测 ' + (Get-Date -Format 'HH:mm:ss')
$body = [System.Text.Encoding]::UTF8.GetBytes($text)
Invoke-WebRequest -Uri ($base + '/push?mode=new') -Method Post -Body $body -ContentType 'text/plain; charset=utf-8' -UseBasicParsing | Out-Null
$delivered = $false
for ($i = 0; $i -lt 24; $i++) {
    Start-Sleep -Milliseconds 500
    $h2 = Get2 '/health' | ConvertFrom-Json
    if ($h2.pending -eq 0) {
        if ($h2.delivered -gt $before) { $delivered = $true }
        break
    }
}
$hf = Get2 '/health' | ConvertFrom-Json
T '投递到 DSH' $delivered ("delivered=" + $hf.delivered)
if ($hf.lastError -ne '') {
    Write-Host ("  最近一次错误：" + $hf.lastError) -ForegroundColor Yellow
}

Write-Host ''
Write-Host '=== 6.5 附件端到端（会再建一个会话，带一个附件）===' -ForegroundColor Cyan
$attDir = Join-Path $env:USERPROFILE '.dsh\attachments\v1\file-objects'
$cntBefore = if (Test-Path $attDir) { (Get-ChildItem $attDir -Recurse -File | Measure-Object).Count } else { -1 }
$probeName = 'selftest-' + (Get-Date -Format 'HHmmss') + '.txt'
# DSH 按内容哈希去重：载荷必须每次都不同，否则第二轮不新增对象，用例会误报。
$stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'
$b64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes('quickinput selftest attachment ' + $stamp))
$line = 'file=' + [uri]::EscapeDataString($probeName) + '|text/plain|' + $b64
$lb = [System.Text.Encoding]::UTF8.GetBytes($line)
Invoke-WebRequest -Uri ($base + '/attach') -Method Post -Body $lb -ContentType 'text/plain; charset=utf-8' -UseBasicParsing | Out-Null
$before2 = (Get2 '/health' | ConvertFrom-Json).delivered
$body2 = [System.Text.Encoding]::UTF8.GetBytes('QuickInput 附件自测 ' + (Get-Date -Format 'HH:mm:ss'))
Invoke-WebRequest -Uri ($base + '/push?mode=new') -Method Post -Body $body2 -ContentType 'text/plain; charset=utf-8' -UseBasicParsing | Out-Null
$sent2 = $false
for ($i = 0; $i -lt 24; $i++) {
    Start-Sleep -Milliseconds 500
    $hh = Get2 '/health' | ConvertFrom-Json
    if ($hh.pending -eq 0) { if ($hh.delivered -gt $before2) { $sent2 = $true }; break }
}
$cntAfter = if (Test-Path $attDir) { (Get-ChildItem $attDir -Recurse -File | Measure-Object).Count } else { -1 }
T '带附件的一条发出去了' $sent2 ("delivered=" + $sent2)
T '附件真的落进了 DSH' ($cntAfter -gt $cntBefore) ("file-objects " + $cntBefore + " -> " + $cntAfter)

Write-Host ''
Write-Host '=== 7. 文件搜索 / 列表滚动 ===' -ForegroundColor Cyan
$r = Get2 ('/debugfiles?q=' + [uri]::EscapeDataString('/a')) | ConvertFrom-Json
$m = [regex]::Match($r.r, 'hits=(\d+)\s+height=(\d+)')
$hits = if ($m.Success) { [int]$m.Groups[1].Value } else { 0 }
$winH = if ($m.Success) { [int]$m.Groups[2].Value } else { 0 }
T 'Everything 有命中' ($hits -gt 0) ("hits=$hits height=$winH")

$r2 = Get2 ('/debugfiles?q=' + [uri]::EscapeDataString('/a') + '&sel=8') | ConvertFrom-Json
$m2 = [regex]::Match($r2.r, 'hits=(\d+).*?sel=(\d+)\s+top=(\d+)')
$h2 = if ($m2.Success) { [int]$m2.Groups[1].Value } else { 0 }
$sel = if ($m2.Success) { [int]$m2.Groups[2].Value } else { -1 }
$top = if ($m2.Success) { [int]$m2.Groups[3].Value } else { -1 }
if ($h2 -ge 9) {
    T '选中项能到第 9 条' (($sel -eq 8) -and ($top -ge 5)) ("sel=$sel top=$top（共 $h2 条）")
} else {
    Write-Host ("  跳过：结果只有 $h2 条，凑不出第 9 条") -ForegroundColor Yellow
}

Write-Host ''
Write-Host '=== 8. 收起 / 再呼出保持上次结果 ===' -ForegroundColor Cyan
$r3 = Get2 ('/debugfiles?q=' + [uri]::EscapeDataString('/a') + '&sel=8') | ConvertFrom-Json
$m3 = [regex]::Match($r3.r, 'visible=(\w+)\s+query=\S*\s+hits=(\d+)\s+height=\d+\s+ms=\d+\s+sel=(\d+)\s+top=(\d+)')
$vis3 = if ($m3.Success) { $m3.Groups[1].Value } else { '' }
$hits3 = if ($m3.Success) { [int]$m3.Groups[2].Value } else { 0 }
$sel3 = if ($m3.Success) { [int]$m3.Groups[3].Value } else { -1 }
T '先摆出一个有选中项的结果' (($vis3 -eq 'True') -and ($hits3 -gt 0) -and ($sel3 -eq 8)) ("hits=$hits3 sel=$sel3")

Get2 '/hide' | Out-Null
Start-Sleep -Milliseconds 250
$r4 = Get2 '/debugfiles' | ConvertFrom-Json
$m4 = [regex]::Match($r4.r, 'visible=(\w+)\s+query=(\S*)\s+hits=(\d+)\s+height=\d+\s+ms=\d+\s+sel=(\d+)\s+top=(\d+)')
$vis4 = if ($m4.Success) { $m4.Groups[1].Value } else { '' }
$q4 = if ($m4.Success) { $m4.Groups[2].Value } else { '' }
$hits4 = if ($m4.Success) { [int]$m4.Groups[3].Value } else { -1 }
$sel4 = if ($m4.Success) { [int]$m4.Groups[4].Value } else { -1 }
$top4 = if ($m4.Success) { [int]$m4.Groups[5].Value } else { -1 }
T '收起后状态没丢' (($hits4 -eq $hits3) -and ($sel4 -eq 8) -and ($q4 -eq 'a')) ("query=$q4 hits=$hits4 sel=$sel4 top=$top4")
T '再呼出还是那一屏' (($vis4 -eq 'True') -and ($hits4 -eq $hits3) -and ($sel4 -eq 8) -and ($top4 -gt 0)) ($r4.r)

Write-Host ''
if ($fail -eq 0) {
    Write-Host ("全部通过：$pass 项") -ForegroundColor Green
} else {
    Write-Host ("通过 $pass 项，失败 $fail 项") -ForegroundColor Red
}
Write-Host ''
exit 0
