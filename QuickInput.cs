using System; using System.Collections.Generic; using System.Drawing; using System.Drawing.Drawing2D; using System.IO; using System.Net; using System.Net.Sockets; using System.Runtime.InteropServices; using System.Text; using System.Threading; using System.Windows.Forms;  namespace QuickInput {
// ───────────────────────── 原生 API ─────────────────────────
internal static class Native
{

public const int WM_HOTKEY = 0x0312;

public const uint MOD_ALT = 0x0001;

public const uint MOD_CONTROL = 0x0002;

public const uint MOD_SHIFT = 0x0004;

public const uint MOD_WIN = 0x0008;

 public const uint INPUT_KEYBOARD = 1;

public const uint KEYEVENTF_KEYUP = 0x0002;

 public const ushort VK_CONTROL = 0x11;

public const ushort VK_MENU = 0x12;

public const ushort VK_RETURN = 0x0D;

public const ushort VK_V = 0x56;

 [DllImport("user32.dll", SetLastError = true)]

public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

 [DllImport("user32.dll", SetLastError = true)]

public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

 [DllImport("user32.dll")]

public static extern IntPtr GetForegroundWindow();

 [DllImport("user32.dll", SetLastError = true)]

public static extern bool SetForegroundWindow(IntPtr hWnd);

 [DllImport("user32.dll")]

public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);

 [DllImport("kernel32.dll")]

public static extern uint GetCurrentThreadId();

 [DllImport("user32.dll")]

public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

 [DllImport("user32.dll")]

public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

 [DllImport("user32.dll", CharSet = CharSet.Unicode)]

public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

 [DllImport("user32.dll")]

public static extern bool SetProcessDPIAware();

 [DllImport("user32.dll")]

public static extern uint GetDpiForSystem();

 [DllImport("user32.dll")]

public static extern bool SetCursorPos(int X, int Y);

 [DllImport("user32.dll")]

public static extern bool GetCursorPos(out POINT p);

 [DllImport("user32.dll")]

public static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

 [StructLayout(LayoutKind.Sequential)]

public struct POINT

{


public int X;


public int Y;

}

 // 在指定屏幕坐标点一下左键，点完把鼠标放回原处

public static void ClickAt(int x, int y)

{


POINT old;


GetCursorPos(out old);


SetCursorPos(x, y);


Thread.Sleep(40);


mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);


Thread.Sleep(30);


mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);


Thread.Sleep(40);


SetCursorPos(old.X, old.Y);

}

 [DllImport("user32.dll")]

public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,


int X, int Y, int cx, int cy, uint uFlags);

 [DllImport("user32.dll")]

public static extern bool BringWindowToTop(IntPtr hWnd);

 [DllImport("user32.dll")]

public static extern IntPtr SetActiveWindow(IntPtr hWnd);

 [DllImport("user32.dll")]

public static extern IntPtr SetFocus(IntPtr hWnd);

 public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

public const uint SWP_NOSIZE = 0x0001;

public const uint SWP_NOMOVE = 0x0002;

public const uint SWP_NOACTIVATE = 0x0010;

 [StructLayout(LayoutKind.Sequential)]

public struct MOUSEINPUT

{


public int dx;


public int dy;


public uint mouseData;


public uint dwFlags;


public uint time;


public IntPtr dwExtraInfo;

}

 [StructLayout(LayoutKind.Sequential)]

public struct KEYBDINPUT

{


public ushort wVk;


public ushort wScan;


public uint dwFlags;


public uint time;


public IntPtr dwExtraInfo;

}

 [StructLayout(LayoutKind.Sequential)]

public struct HARDWAREINPUT

{


public uint uMsg;


public ushort wParamL;


public ushort wParamH;

}

 [StructLayout(LayoutKind.Explicit)]

public struct InputUnion

{


[FieldOffset(0)] public MOUSEINPUT mi;


[FieldOffset(0)] public KEYBDINPUT ki;


[FieldOffset(0)] public HARDWAREINPUT hi;

}

 [StructLayout(LayoutKind.Sequential)]

public struct INPUT

{


public uint type;


public InputUnion u;

}

 [DllImport("user32.dll", SetLastError = true)]

public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

 public static void SendKey(ushort vk)

{


INPUT[] ins = new INPUT[2];


ins[0].type = INPUT_KEYBOARD;


ins[0].u.ki.wVk = vk;


ins[1].type = INPUT_KEYBOARD;


ins[1].u.ki.wVk = vk;


ins[1].u.ki.dwFlags = KEYEVENTF_KEYUP;


SendInput(2, ins, Marshal.SizeOf(typeof(INPUT)));

}

 public static void SendCtrlV()

{


INPUT[] ins = new INPUT[4];


ins[0].type = INPUT_KEYBOARD;


ins[0].u.ki.wVk = VK_CONTROL;


ins[1].type = INPUT_KEYBOARD;


ins[1].u.ki.wVk = VK_V;


ins[2].type = INPUT_KEYBOARD;


ins[2].u.ki.wVk = VK_V;


ins[2].u.ki.dwFlags = KEYEVENTF_KEYUP;


ins[3].type = INPUT_KEYBOARD;


ins[3].u.ki.wVk = VK_CONTROL;


ins[3].u.ki.dwFlags = KEYEVENTF_KEYUP;


SendInput(4, ins, Marshal.SizeOf(typeof(INPUT)));

}

 // Windows 的前台锁：后台进程不能随便抢焦点。

// 只做必要动作：附加到前台线程 → 设置前台。不再模拟 Alt、不再反复置顶，

// 那些会干扰用户正在按的键，表现为“光标乱飘”。

public static void ForceForeground(IntPtr hwnd)

{


if (hwnd == IntPtr.Zero) return;


if (GetForegroundWindow() == hwnd) return;


 IntPtr fg = GetForegroundWindow();


uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);


uint cur = GetCurrentThreadId();


bool attached = false;


 if (fgThread != 0 && fgThread != cur)


{



AttachThreadInput(cur, fgThread, true);



attached = true;


}


 SetForegroundWindow(hwnd);


BringWindowToTop(hwnd);


 if (attached) AttachThreadInput(cur, fgThread, false);

}

 // 等某个窗口真正成为前台（替代固定 Sleep）

public static bool WaitForeground(IntPtr hwnd, int timeoutMs)

{


int waited = 0;


while (waited < timeoutMs)


{



if (GetForegroundWindow() == hwnd) return true;



Thread.Sleep(25);



waited += 25;


}


return GetForegroundWindow() == hwnd;

}
}
 // ───────────────────── Everything 文件搜索 ─────────────────────
// 不自己扫硬盘：通过官方 SDK 把查询交给正在运行的 Everything，
// 用它的内存索引回答。速度、排序、搜索语法都和直接开 Everything 一致。
internal class FileHit
{

public string FullPath = "";

public string Name = "";

public bool IsFolder;
}
 internal static class Everything
{

private const string Dll = "Everything64.dll";

 [DllImport(Dll, CharSet = CharSet.Unicode)]

private static extern void Everything_SetSearchW(string s);

[DllImport(Dll)]

private static extern bool Everything_QueryW(bool wait);

[DllImport(Dll)]

private static extern uint Everything_GetNumResults();

[DllImport(Dll)]

private static extern uint Everything_GetLastError();

[DllImport(Dll)]

private static extern void Everything_SetMax(uint n);

[DllImport(Dll)]

private static extern void Everything_Reset();

[DllImport(Dll)]

private static extern bool Everything_IsFileResult(uint i);

[DllImport(Dll, CharSet = CharSet.Unicode)]

private static extern void Everything_GetResultFullPathNameW(uint i, StringBuilder buf, uint n);

 private static readonly object _gate = new object();

 public static bool Available = false;

public static string LastError = "";

// 查询被降级时的说明，给底部状态栏用

public static string LastNote = "";

 // 启动时试跑一次：既验证 DLL 能加载，也验证 Everything 的 IPC 通

public static void Probe()

{


List<FileHit> probe = Query("", 1);


InputForm.Log("everything probe: available=" + Available +





  "  err=" + LastError + "  got=" + probe.Count);

}

 public static List<FileHit> Query(string text, int max)

{


List<FileHit> list = new List<FileHit>();


if (max <= 0) max = 10;


if (max > 60) max = 60;


 // content: 会让 Everything 当场全盘读文件内容，几十秒到几十分钟；SDK 这个调用不能取消，


// 在主线程上就等于整个浮层卡死（2026-10-22 实测踩过）。这里直接降级成文件名搜索并明说。


LastNote = "";


if (text != null && text.IndexOf("content:", StringComparison.OrdinalIgnoreCase) >= 0)


{



text = System.Text.RegularExpressions.Regex.Replace(text, "content:\\S*", " ").Trim();



LastNote = "正文搜索会把 Everything 拖死，已忽略 content:";



if (text.Length == 0) { LastError = ""; return list; }


}


 lock (_gate)


{



try



{




Everything_Reset();




Everything_SetMax((uint)max);




Everything_SetSearchW(text == null ? "" : text);




 bool qok = Everything_QueryW(true);

 if (!qok)
 {
  // Everything 偶尔会 IPC 忙（它自己在扫盘/重建索引时）。重试一次通常就好，
  // 以前这里直接放弃，界面上就是「一条结果都没有」，看着像搜索坏了。
  System.Threading.Thread.Sleep(150);
  Everything_Reset();
  Everything_SetMax((uint)max);
  Everything_SetSearchW(text == null ? "" : text);
  qok = Everything_QueryW(true);
 }

 if (!qok)




{





LastError = "Everything 没响应 err=" + Everything_GetLastError();





Available = false;





return list;




}




 uint n = Everything_GetNumResults();




StringBuilder sb = new StringBuilder(1024);




 for (uint i = 0; i < n; i++)




{





sb.Length = 0;





Everything_GetResultFullPathNameW(i, sb, (uint)sb.Capacity);





string full = sb.ToString();





if (full.Length == 0) continue;





 FileHit h = new FileHit();





h.FullPath = full;





try { h.Name = Path.GetFileName(full); }





catch { h.Name = ""; }





if (h.Name == null || h.Name.Length == 0) h.Name = full;





h.IsFolder = !Everything_IsFileResult(i);





list.Add(h);




}




 LastError = "";




Available = true;



}



catch (Exception ex)



{




LastError = ex.Message;




Available = false;



}


}


return list;

}
}
 // ───────────────────── 本地桥（给浏览器扩展取件） ─────────────────────
// QuickInput 自己开一个回环端口，扩展轮询它取内容。
// 只有 127.0.0.1 能连，局域网和外网都碰不到。
internal class QAtt
{
 public string Name = "";
 public string Mime = "application/octet-stream";
 public byte[] Data = new byte[0];
}

internal class BridgeItem
{

public int Id;

public string Text = "";

public string Mode = "new";

public DateTime Created = DateTime.Now;

public DateTime ServedAt = DateTime.MinValue;

public List<QAtt> Files = new List<QAtt>();
}
 internal static class Bridge
{

public const int Port = 47321;

 private static readonly object _lock = new object();

private static readonly List<BridgeItem> _queue = new List<BridgeItem>();

private static int _nextId = 1;

 public static DateTime LastPoll = DateTime.MinValue;

public static string LastClientVersion = "";

public static string LastError = "";

public static int Delivered = 0;

public static bool Started = false;

 public static int Pending

{


get { lock (_lock) { return _queue.Count; } }

}

 // 扩展 8 秒内来取过件就算在线

public static bool Connected

{


get { return (DateTime.Now - LastPoll).TotalSeconds < 8; }

}

 public static int Push(string text, string mode) { return Push(text, mode, null); }

 public static int Push(string text, string mode, List<QAtt> files)

{


if (text == null) text = "";


lock (_lock)


{



BridgeItem it = new BridgeItem();



it.Id = _nextId++;



it.Text = text;



it.Mode = (mode == "current") ? "current" : "new";



it.Files = (files == null) ? new List<QAtt>() : files;



_queue.Add(it);



return _queue.Count;


}

}

 public static void Start()

{


if (Started) return;


Started = true;


Thread t = new Thread(Loop);


t.IsBackground = true;


t.Name = "bridge";


t.Start();

}

 // --- attachments: QI -> bridge -> extension ---
 private static readonly List<QAtt> _pending = new List<QAtt>();

 public static void AddPending(List<QAtt> list)
 {
  if (list == null) return;
  lock (_lock) { _pending.AddRange(list); }
 }

 private static List<QAtt> TakePending()
 {
  lock (_lock)
  {
   List<QAtt> copy = new List<QAtt>(_pending);
   _pending.Clear();
   return copy;
  }
 }

 public static List<QAtt> ParseAttach(string body)
 {
  List<QAtt> list = new List<QAtt>();
  if (string.IsNullOrEmpty(body)) return list;
  string[] lines = body.Split('\n');
  for (int i = 0; i < lines.Length; i++)
  {
   string ln = lines[i].Trim();
   if (ln.Length == 0 || !ln.StartsWith("file=")) continue;
   string[] parts = ln.Substring(5).Split('|');
   if (parts.Length != 3) continue;
   try
   {
    QAtt a = new QAtt();
    a.Name = Uri.UnescapeDataString(parts[0]);
    a.Mime = parts[1].Length > 0 ? parts[1] : "application/octet-stream";
    a.Data = Convert.FromBase64String(parts[2]);
    if (a.Name.Length == 0) a.Name = "file";
    list.Add(a);
   }
   catch { }
  }
  return list;
 }

 public static string AttsJson(BridgeItem it)
 {
  StringBuilder sb = new StringBuilder("[");
  for (int i = 0; i < it.Files.Count; i++)
  {
   if (i > 0) sb.Append(",");
   QAtt a = it.Files[i];
   sb.Append("{\"name\":").Append(J(a.Name))
     .Append(",\"mime\":").Append(J(a.Mime))
     .Append(",\"b64\":\"").Append(Convert.ToBase64String(a.Data)).Append("\"}");
  }
  return sb.Append("]").ToString();
 }

 private static void Loop()

{


TcpListener srv;


try


{



srv = new TcpListener(IPAddress.Loopback, Port);



srv.Start();



InputForm.Log("bridge listening on 127.0.0.1:" + Port);


}


catch (Exception ex)


{



InputForm.Log("bridge start FAILED: " + ex.Message);



return;


}


 while (true)


{



try



{




TcpClient c = srv.AcceptTcpClient();




Thread th = new Thread(delegate() { Handle(c); });




th.IsBackground = true;




th.Start();



}



catch (Exception ex)



{




InputForm.Log("bridge accept error: " + ex.Message);




Thread.Sleep(300);



}


}

}

 private static void Handle(TcpClient client)

{


NetworkStream ns = null;


try


{



client.ReceiveTimeout = 6000;



client.SendTimeout = 6000;



ns = client.GetStream();



 byte[] buf = new byte[8192];



MemoryStream ms = new MemoryStream();



int headerEnd = -1;



 while (ms.Length < 256 * 1024)



{




int n = ns.Read(buf, 0, buf.Length);




if (n <= 0) break;




ms.Write(buf, 0, n);




headerEnd = FindHeaderEnd(ms);




if (headerEnd >= 0) break;



}



 if (headerEnd < 0)



{




Send(ns, 400, "{\"ok\":false,\"error\":\"bad request\"}");




return;



}



 byte[] all = ms.ToArray();



string headText = Encoding.ASCII.GetString(all, 0, headerEnd);



string[] lines = headText.Split(new string[] { "\r\n" }, StringSplitOptions.None);



string[] reqLine = lines[0].Split(' ');



string method = reqLine.Length > 0 ? reqLine[0].ToUpperInvariant() : "GET";



string path = reqLine.Length > 1 ? reqLine[1] : "/";



 int contentLength = 0;



for (int i = 1; i < lines.Length; i++)



{




int c = lines[i].IndexOf(':');




if (c <= 0) continue;




if (lines[i].Substring(0, c).Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase))




{





int tmp;





if (int.TryParse(lines[i].Substring(c + 1).Trim(), out tmp)) contentLength = tmp;




}



}



 int bodyStart = headerEnd + 4;



int have = all.Length - bodyStart;



if (have < 0) have = 0;



 byte[] body = new byte[contentLength > 0 ? contentLength : 0];



int copied = Math.Min(have, contentLength);



if (copied > 0) Buffer.BlockCopy(all, bodyStart, body, 0, copied);



while (copied < contentLength)



{




int n = ns.Read(body, copied, contentLength - copied);




if (n <= 0) break;




copied += n;



}



string bodyText = Encoding.UTF8.GetString(body, 0, copied);



 int q = path.IndexOf('?');



string route = q >= 0 ? path.Substring(0, q) : path;



string query = q >= 0 ? path.Substring(q + 1) : "";



 if (method == "OPTIONS")



{




Send(ns, 204, "");




return;



}



 if (route == "/health")



{




Send(ns, 200, HealthJson());



}



else if (route == "/poll")



{




LastPoll = DateTime.Now;




string ver = QueryStr(query, "v");




if (ver.Length > 0) LastClientVersion = ver;




BridgeItem it = Take();




if (it == null)




{





Send(ns, 204, "");




}




else




{





StringBuilder sb = new StringBuilder();





sb.Append("{\"id\":").Append(it.Id)





  .Append(",\"text\":").Append(J(it.Text))





  .Append(",\"mode\":\"").Append(it.Mode).Append("\"")





  .Append(",\"pending\":").Append(Pending)



  .Append(",\"atts\":").Append(AttsJson(it))





  .Append("}");





Send(ns, 200, sb.ToString());




}



}



else if (route == "/search")



{




// 自检接口：直接问「这句话会被解析成什么网址」，不用碰界面




string nd = QueryStr(query, "q");




if (nd.Length == 0 || InputForm.Cfg == null)




{





StringBuilder al = new StringBuilder();





al.Append("{\"ok\":true,\"aliases\":[");





if (InputForm.Cfg != null)





{






bool first = true;






foreach (KeyValuePair<string, string> kv in InputForm.Cfg.SearchUrl)






{







if (!first) al.Append(',');







first = false;







al.Append(J(kv.Key));






}





}





al.Append("]}");





Send(ns, 200, al.ToString());





return;




}




 string u, lb;




if (InputForm.Cfg.TryResolveSearch(nd, out u, out lb))





Send(ns, 200, "{\"ok\":true,\"label\":" + J(lb) + ",\"url\":" + J(u) + "}");




else




{





string pk;





string peek = InputForm.Cfg.TryPeekAlias(nd, out pk) ? pk : "";





Send(ns, 200, "{\"ok\":false,\"input\":" + J(nd) + ",\"peek\":" + J(peek) + "}");




}



}



else if (route == "/open")



{




// 自检：走和 DoSend 完全相同的打开逻辑。只接受能解析成搜索的输入，




// 不能当通用的「随便开个网址」接口用。




string nd2 = QueryStr(query, "q");




string u2, lb2;




if (InputForm.Cfg != null && InputForm.Cfg.TryResolveSearch(nd2, out u2, out lb2))




{





bool ok2 = InputForm.OpenUrl(u2, "selftest");





Send(ns, 200, "{\"ok\":" + (ok2 ? "true" : "false") + ",\"url\":" + J(u2) + "}");




}




else




{





Send(ns, 200, "{\"ok\":false,\"err\":\"not a search\"}");




}



}



else if (route == "/files")



{




// 自检：直接验证 Everything 查询链路，不经过界面




string fq = QueryStr(query, "q");




int fn = QueryInt(query, "n");




if (fn <= 0) fn = 10;




 List<FileHit> hits = Everything.Query(fq, fn);




StringBuilder fb = new StringBuilder();




fb.Append("{\"ok\":").Append(Everything.Available ? "true" : "false");




fb.Append(",\"err\":").Append(J(Everything.LastError));




fb.Append(",\"n\":").Append(hits.Count).Append(",\"items\":[");




for (int i = 0; i < hits.Count; i++)




{





if (i > 0) fb.Append(',');





fb.Append("{\"name\":").Append(J(hits[i].Name))





  .Append(",\"path\":").Append(J(hits[i].FullPath))





  .Append(",\"dir\":").Append(hits[i].IsFolder ? "true" : "false")





  .Append('}');




}




fb.Append("]}");




Send(ns, 200, fb.ToString());



}



else if (route == "/debugfiles")



{




// 不带 q= 就只把浮层叫出来，不改内容 —— 用来验「恢复上次结果」




bool hasQ = query != null && query.IndexOf("q=", StringComparison.Ordinal) >= 0;




string dq = hasQ ? QueryStr(query, "q") : null;




// 可选 sel=N：把选中项直接推到第 N 条，自检滚动用




bool hasSel = query != null && query.IndexOf("sel=", StringComparison.Ordinal) >= 0;




int dsel = hasSel ? QueryInt(query, "sel") : -1;




string dr = InputForm.Instance == null





? "no instance"





: InputForm.Instance.DebugFileSearch(dq, dsel);




Send(ns, 200, "{\"ok\":true,\"r\":" + J(dr) + "}");



}



else if (route == "/hide")



{




// 走和「点浮层外面」完全相同的那条路径，自检用




if (InputForm.Instance != null) InputForm.Instance.DebugHide();




Send(ns, 200, "{\"ok\":true}");



}



else if (route == "/ack")



{




Ack(QueryInt(query, "id"), QueryStr(query, "ok") == "1", QueryStr(query, "err"));




Send(ns, 200, "{\"ok\":true}");



}



else if (route == "/attach")



 {



  AddPending(ParseAttach(bodyText));



  Send(ns, 200, "{\"ok\":true}");



 }

 else if (route == "/push")



{




int p = Push(bodyText, QueryStr(query, "mode"), TakePending());




Send(ns, 200, "{\"ok\":true,\"pending\":" + p + "}");



}



else



{




Send(ns, 404, "{\"ok\":false,\"error\":\"not found\"}");



}


}


catch (Exception ex)


{



InputForm.Log("bridge handle error: " + ex.Message);


}


finally


{



try { client.Close(); }



catch { }


}

}

 private static BridgeItem Take()

{


lock (_lock)


{



DateTime now = DateTime.Now;



for (int i = 0; i < _queue.Count; i++)



{




BridgeItem it = _queue[i];




 // 超过 10 分钟还没人取走，丢掉，免得越积越多




if ((now - it.Created).TotalMinutes > 10)




{





_queue.RemoveAt(i);





i--;





LastError = "有一条超过 10 分钟没取走，已丢弃";





continue;




}




 // 刚被取走还没回执的，先跳过，避免重复投递




if (it.ServedAt != DateTime.MinValue && (now - it.ServedAt).TotalSeconds < 10) continue;




 it.ServedAt = now;




return it;



}



return null;


}

}

 private static void Ack(int id, bool ok, string err)

{


lock (_lock)


{



for (int i = 0; i < _queue.Count; i++)



{




if (_queue[i].Id != id) continue;




_queue.RemoveAt(i);




break;



}


}


if (ok) Delivered++;


else LastError = string.IsNullOrEmpty(err) ? "投递失败" : err;

}

 private static string HealthJson()

{


StringBuilder sb = new StringBuilder();


sb.Append("{\"ok\":true,\"pending\":").Append(Pending)


  .Append(",\"connected\":").Append(Connected ? "true" : "false")


  .Append(",\"client\":").Append(J(LastClientVersion))


  .Append(",\"delivered\":").Append(Delivered)


  .Append(",\"lastError\":").Append(J(LastError))


  .Append("}");


return sb.ToString();

}

 private static int FindHeaderEnd(MemoryStream ms)

{


byte[] b = ms.GetBuffer();


int len = (int)ms.Length;


for (int i = 0; i + 3 < len; i++)


{



if (b[i] == 13 && b[i + 1] == 10 && b[i + 2] == 13 && b[i + 3] == 10) return i;


}


return -1;

}

 private static void Send(NetworkStream ns, int code, string body)

{


if (body == null) body = "";


byte[] b = Encoding.UTF8.GetBytes(body);


string reason = code == 200 ? "OK"





  : code == 204 ? "No Content"





  : code == 400 ? "Bad Request"





  : code == 404 ? "Not Found" : "OK";


int len = (code == 204) ? 0 : b.Length;


 StringBuilder h = new StringBuilder();


h.Append("HTTP/1.1 ").Append(code).Append(' ').Append(reason).Append("\r\n");


h.Append("Content-Type: application/json; charset=utf-8\r\n");


h.Append("Access-Control-Allow-Origin: *\r\n");


h.Append("Access-Control-Allow-Headers: Content-Type\r\n");


h.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");


h.Append("Cache-Control: no-store\r\n");


h.Append("Content-Length: ").Append(len).Append("\r\n");


h.Append("Connection: close\r\n\r\n");


 byte[] hb = Encoding.ASCII.GetBytes(h.ToString());


ns.Write(hb, 0, hb.Length);


if (len > 0) ns.Write(b, 0, b.Length);


ns.Flush();

}

 private static string QueryStr(string query, string key)

{


if (string.IsNullOrEmpty(query)) return "";


string[] parts = query.Split('&');


for (int i = 0; i < parts.Length; i++)


{



int eq = parts[i].IndexOf('=');



string k = eq >= 0 ? parts[i].Substring(0, eq) : parts[i];



if (!k.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;



string v = eq >= 0 ? parts[i].Substring(eq + 1) : "";



try { return Uri.UnescapeDataString(v.Replace("+", " ")); }



catch { return v; }


}


return "";

}

 private static int QueryInt(string query, string key)

{


int n;


int.TryParse(QueryStr(query, key), out n);


return n;

}

 private static string J(string s)

{


if (s == null) return "\"\"";


StringBuilder sb = new StringBuilder();


sb.Append('"');


for (int i = 0; i < s.Length; i++)


{



char c = s[i];



if (c == '"') sb.Append("\\\"");



else if (c == '\\') sb.Append("\\\\");



else if (c == '\n') sb.Append("\\n");



else if (c == '\r') sb.Append("\\r");



else if (c == '\t') sb.Append("\\t");



else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));



else sb.Append(c);


}


sb.Append('"');


return sb.ToString();

}
}
 // ───────────────────────── 配置 ─────────────────────────
internal class Settings
{

public uint Mods = Native.MOD_ALT;

public uint Vk = 0x46;


  // F

public string HotkeyText = "Alt+F";

public int FocusDelay = 180;

// 还焦点后等目标窗口就绪

public int PasteDelay = 130;

// 粘贴后到回车的间隔

public bool NewSession = true;
  // 发送前先点“新会话”

public int NewSessionX = 784;
   // “新会话”按钮的屏幕坐标（物理像素）

public int NewSessionY = 550;

public int NewSessionWait = 700;
// 点完之后等新对话加载

public string ConfigPath = "";

// new
// = 送进 DSH 的新会话（默认）

// current = 追加到你正在看的那个会话

// clipboard = 老办法：粘贴到当前窗口

public string Mode = "new";

 // [search] 段：别名 → 网址模板（{q} 会被替换成搜索词）

public readonly Dictionary<string, string> SearchUrl =


new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

public readonly Dictionary<string, string> SearchLabel =


new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

 // 写法：hh = 小红书 | https://...keyword={q}

// 名称可以省，省了就显示别名本身。

private void AddSearch(string alias, string val)

{


if (alias.Length == 0 || val.Length == 0) return;


 string label = "";


string url = val;


int bar = val.IndexOf('|');


if (bar > 0)


{



label = val.Substring(0, bar).Trim();



url = val.Substring(bar + 1).Trim();


}


 if (url.IndexOf("{q}") < 0) return;   // 没有占位符，模板没用


 SearchUrl[alias] = url;


SearchLabel[alias] = label.Length > 0 ? label : alias;

}

 // 认出搜索指令，两种写法都支持：

//   ① hh 800库
// —— 空格分隔，最明确

//   ② hh800库
// —— 不用空格，取「最长能匹配的别名」当前缀

// ② 的代价：任何以别名开头的文字都会被当搜索，要发原话就在行首加 \

public bool TryResolveSearch(string text, out string url, out string label)

{


url = null;


label = null;


if (text == null) return false;


 string t = text.Trim();


if (t.Length == 0) return false;


 // ① 先按空格切


int sp = t.IndexOf(' ');


if (sp > 0)


{



string a1 = t.Substring(0, sp).Trim();



string q1 = t.Substring(sp + 1).Trim();



if (a1.Length > 0 && q1.Length > 0 && SearchUrl.ContainsKey(a1))




return BuildSearchUrl(a1, q1, out url, out label);


}


 // ② 没有空格（或空格前不是别名）：找最长前缀别名


string best = null;


foreach (KeyValuePair<string, string> kv in SearchUrl)


{



if (t.Length <= kv.Key.Length) continue;




  // 后面没东西了，是别名本身



if (!t.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase)) continue;



if (best == null || kv.Key.Length > best.Length) best = kv.Key;


}


if (best == null) return false;


 string q2 = t.Substring(best.Length).Trim();


if (q2.Length == 0) return false;


 return BuildSearchUrl(best, q2, out url, out label);

}

 private bool BuildSearchUrl(string alias, string q, out string url, out string label)

{


url = null;


label = null;


 string tpl;


if (!SearchUrl.TryGetValue(alias, out tpl)) return false;


 try


{



url = tpl.Replace("{q}", Uri.EscapeDataString(q));


}


catch { return false; }


 string lb;


label = SearchLabel.TryGetValue(alias, out lb) ? lb : alias;


return true;

}

 // 只打了别名、还没打关键词时也要能立刻给反馈，

// 否则用户会觉得「我明明打了 hh，它半天没反应」。

public bool TryPeekAlias(string text, out string label)

{


label = null;


if (text == null) return false;


 string t = text.Trim();


if (t.Length == 0) return false;


if (t.IndexOf(' ') >= 0) return false;   // 已经带关键词，交给 TryResolveSearch


 string lb;


if (!SearchLabel.TryGetValue(t, out lb)) return false;


 label = lb;


return true;

}

 public static Settings Load()

{


Settings s = new Settings();


string dir = Path.GetDirectoryName(Application.ExecutablePath);


s.ConfigPath = Path.Combine(dir, "qui.ini");


 if (File.Exists(s.ConfigPath))


{



string[] lines = File.ReadAllLines(s.ConfigPath, Encoding.UTF8);



string section = "";



for (int i = 0; i < lines.Length; i++)



{




string line = lines[i].Trim();




if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;




 // 分段：[search] 下面全是搜索别名




if (line.Length >= 2 && line[0] == '[' && line[line.Length - 1] == ']')




{





section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();





continue;




}




 int eq = line.IndexOf('=');




if (eq <= 0) continue;




string key = line.Substring(0, eq).Trim().ToLowerInvariant();




string val = line.Substring(eq + 1).Trim();




 if (section == "search")




{





s.AddSearch(key, val);





continue;




}




 if (key == "hotkey") s.HotkeyText = val;




else if (key == "focusdelay") { int n; if (int.TryParse(val, out n) && n >= 0 && n < 3000) s.FocusDelay = n; }




else if (key == "pastedelay") { int n; if (int.TryParse(val, out n) && n >= 0 && n < 3000) s.PasteDelay = n; }




else if (key == "newsession") s.NewSession = (val == "1" || val.ToLowerInvariant() == "true");




else if (key == "newsessionx") { int n; if (int.TryParse(val, out n) && n >= 0) s.NewSessionX = n; }




else if (key == "newsessiony") { int n; if (int.TryParse(val, out n) && n >= 0) s.NewSessionY = n; }




else if (key == "newsessionwait") { int n; if (int.TryParse(val, out n) && n >= 0 && n < 5000) s.NewSessionWait = n; }




else if (key == "mode")




{





string mv = val.ToLowerInvariant();





if (mv == "new" || mv == "current" || mv == "clipboard") s.Mode = mv;




}



}


}


else


{



try



{




string[] demo = new string[] {





"; QuickInput 配置",





"; Hotkey 支持写法：Alt+Q  Ctrl+Alt+J  Win+Space  Shift+Alt+K  F9",





"Hotkey=Alt+Q",





"; 把焦点还给目标窗口后，等多久再粘贴（毫秒）",





"FocusDelay=180",





"; 粘贴完等多久再回车（毫秒）",





"PasteDelay=130",





"; 发送前先点 DSH 的「新会话」：1 开启，0 关闭",





"NewSession=1",





"; 「新会话」按钮的屏幕坐标（物理像素）。屏幕是 200% 缩放时，",





"; 这里填截图坐标的 2 倍。位置不对就改这两个数。",





"NewSessionX=784",





"NewSessionY=550",





"; 点完「新会话」等多久让新对话加载出来（毫秒）",





"NewSessionWait=700",





"",





"; 搜索别名：Alt+Q → 输入「别名 空格 关键词」→ 回车直接开浏览器",





"; 格式：别名 = 显示名 | 网址模板（{q} 会被替换成搜索词）",





"; 想强行把某段字发进 DSH（不当搜索），行首加一个反斜杠",





"[search]",





"hh    = 小红书 | https://www.xiaohongshu.com/search_result?keyword={q}",





"zh    = 知乎   | https://www.zhihu.com/search?type=content&q={q}",





"bili  = B站    | https://search.bilibili.com/all?keyword={q}",





"wb    = 微博   | https://s.weibo.com/weibo?q={q}",





"db    = 豆瓣   | https://www.douban.com/search?q={q}",





"tb    = 淘宝   | https://s.taobao.com/search?q={q}",





"bd    = 百度   | https://www.baidu.com/s?wd={q}",





"gh    = GitHub | https://github.com/search?q={q}"




};




File.WriteAllLines(s.ConfigPath, demo, new UTF8Encoding(false));



}



catch { }


}


 uint m, v;


if (ParseHotkey(s.HotkeyText, out m, out v))


{



s.Mods = m;



s.Vk = v;


}


else


{



s.HotkeyText = "Alt+F";



s.Mods = Native.MOD_ALT;



s.Vk = 0x46;


}


return s;

}

 private static bool ParseHotkey(string text, out uint mods, out uint vk)

{


mods = 0;


vk = 0;


if (text == null) return false;


string[] parts = text.Split('+');


for (int i = 0; i < parts.Length; i++)


{



string p = parts[i].Trim().ToUpperInvariant();



if (p.Length == 0) continue;



if (p == "ALT") mods |= Native.MOD_ALT;



else if (p == "CTRL" || p == "CONTROL") mods |= Native.MOD_CONTROL;



else if (p == "SHIFT") mods |= Native.MOD_SHIFT;



else if (p == "WIN" || p == "WINDOWS") mods |= Native.MOD_WIN;



else if (p.Length == 1 && p[0] >= 'A' && p[0] <= 'Z') vk = (uint)p[0];



else if (p.Length == 1 && p[0] >= '0' && p[0] <= '9') vk = (uint)p[0];



else if (p == "SPACE") vk = 0x20;



else if (p.Length >= 2 && p[0] == 'F')



{




int n;




if (int.TryParse(p.Substring(1), out n) && n >= 1 && n <= 24) vk = (uint)(0x70 + n - 1);



}



else return false;


}


return vk != 0;

}
}
 // ───────────────────────── 输入窗 ─────────────────────────
internal class InputForm : Form
{

private const int HOTKEY_ID = 0x5121;
        // 网页版 DSH 地址：桥没连上时用它自动把页面打开（换端口改这一行）
        private const string DshUrl = "http://127.0.0.1:3080";

 private static readonly string LogPath =


Path.Combine(Path.GetTempPath(), "quickinput.log");

 internal static void Log(string msg)

{


try


{



File.AppendAllText(LogPath,




DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + "\r\n",




new UTF8Encoding(false));


}


catch { }

}

 public static Settings Cfg;

public bool HotkeyOk = false;

 private TextBox tb;

private bool placeholder = true;

private readonly string phText = "输入内容，回车发送…";

private string hintText = "Enter 发送   Shift+Enter 换行   Esc 取消";

 private IntPtr targetWindow = IntPtr.Zero;

private string targetTitle = "";

 private static Color C_BG = Color.FromArgb(22, 26, 38);

private static Color C_LINE = Color.FromArgb(74, 90, 152);

private static Color C_TXT = Color.FromArgb(240, 242, 255);

private static Color C_DIM = Color.FromArgb(124, 134, 168);

private static Color C_ACCENT = Color.FromArgb(124, 92, 255);

 // 屏幕是 200% 缩放时，1 个逻辑单位 = 2 个物理像素。

// 所有尺寸都按这个比例放大，否则窗口会小一半。

private float _scale = 1f;

 // 字体缓存：每帧 new Font 既慢又漏 GDI 句柄

private Font _fBadge;

private Font _fHint;

private Dictionary<string, Font> _glyphFonts = new Dictionary<string, Font>();

private Font _fRow;

private Font _fSmall;

 // ── 文件搜索（Everything）状态 ──

private const int BASE_H = 80;
 // 输入行高度（逻辑像素）

private const int ROW_H = 30;
  // 每条结果的高度

private const int MAX_ROWS = 4;
// 最多显示几条（少一点，占屏更轻）

 private readonly List<FileHit> _hits = new List<FileHit>();

private int _sel = 0;

private int _top = 0;
   // 结果多于 MAX_ROWS 时，列表从第几条开始画

private int _lastMs = 0;

private string _query = "";

 // 结果行里两段文字的实测行高（只量一次，用来做垂直居中）

private float _nameLineH = -1f;

private float _pathLineH = -1f;

private float _hintLineH = -1f;

 // 「点了浮层外面就收起」：显示期间盯前台窗口，不是自己就收起

private DateTime _shownAt = DateTime.MinValue;

private System.Windows.Forms.Timer _watchTimer;

private System.Windows.Forms.Timer _debounce;

 // ── 悬停看全名 ──

// 行宽是固定的，长文件名必然被截断；前面几十个字一样的文件（同一篇笔记的多个版本、

// 「2026-07-07_完整会话记录——」这种）光看行内根本分不出来。所以悬停时在浮层

// 最下面加一块详情：完整文件名 + 完整路径 + 类型/大小/修改时间。

//

// 关键取舍：详情必须画在浮层自己身上，不能另开一个小窗口。

// 因为「点浮层外面就收起」是靠「前台窗口还是不是我」判断的（见 _watchTimer），

// 第二个窗口一 Show 就会把前台抢走 → 浮层立刻判定失焦 → 自己收起。

// 2026-10-22 实测试过：加 WS_EX_NOACTIVATE + ShowWithoutActivation 也拦不住，

// 日志里是 ShowInput 之后 306ms 就 HideOnFocusLost。所以这里不碰窗口，只长高。

private System.Windows.Forms.Timer _hoverTimer;

private int _hoverRow = -1;

 private int _tipRow = -1;

 // 正在显示详情的是哪一条，-1 = 没显示

private string _tipName = "";

private string _tipPath = "";

private string _tipMeta = "";

private int _tipH;



// 详情区高度（物理像素），0 = 不占位

private int _tipHFixed;

   // 固定高度：同一批结果里锁死，不随鼠标移动跳

        private int _tipNameLines = 2;     // 文件名占几行：按当前结果集定（1~3）
        private int _tipPathLines = 1;
// 路径占几行：按当前结果集里最长的那个定（1~4）

private ContextMenuStrip _ctx;
// 右键菜单（懒创建，复用）

 private const TextFormatFlags TipFmt =


TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak


| TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

 // 路径 / 类型那两行只占一行，靠省略号收尾

private const TextFormatFlags TipOne =


TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine


| TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

 // ── 文件类型图标：Material 风格「彩色文件形 + 白色类型字」──

// 不打包 PNG/SVG，直接用 GDI+ 按 24×24 网格画同一套几何：

// 屏幕 200% 缩放时是矢量放大，不会像位图那样糊；程序仍然只有单文件。

private sealed class FileKind

{


public Color Fill;    // 文件形主色


public Color Fold;    // 右上折角（主色提亮，营造纸张层次）


public string Glyph;  // 白色小字（W / X / PDF…），空串则不画

}

 private static readonly Dictionary<string, FileKind> _kindMap = BuildKindMap();

 private static readonly FileKind _kindDefault = MakeKind(0x6E, 0x7D, 0x91, 0x93, 0xA2, 0xB6, "");

 private static FileKind MakeKind(int r, int gg, int b, int fr, int fg, int fb, string glyph)

{


FileKind k = new FileKind();


k.Fill = Color.FromArgb(r, gg, b);


k.Fold = Color.FromArgb(fr, fg, fb);


k.Glyph = glyph;


return k;

}

 private static Dictionary<string, FileKind> BuildKindMap()

{


Dictionary<string, FileKind> m =



new Dictionary<string, FileKind>(StringComparer.OrdinalIgnoreCase);


 AddKind(m, MakeKind(0x7A, 0x4B, 0xD0, 0x9C, 0x74, 0xEE, "MD"),



"md", "markdown", "mdx");


AddKind(m, MakeKind(0x6E, 0x7D, 0x91, 0x93, 0xA2, 0xB6, "TXT"),



"txt", "log", "ini", "cfg", "rst");


AddKind(m, MakeKind(0x2B, 0x57, 0x9A, 0x4A, 0x7E, 0xC4, "W"),



"doc", "docx", "rtf", "odt", "pages");


AddKind(m, MakeKind(0x1D, 0x6F, 0x42, 0x3D, 0x9C, 0x69, "X"),



"xls", "xlsx", "csv", "ods", "numbers");


AddKind(m, MakeKind(0xB7, 0x47, 0x2A, 0xD9, 0x69, 0x4D, "P"),



"ppt", "pptx", "key", "odp");


AddKind(m, MakeKind(0xD9, 0x30, 0x25, 0xF2, 0x6A, 0x60, "PDF"),



"pdf");


AddKind(m, MakeKind(0x26, 0xA6, 0x9A, 0x4F, 0xBF, 0xB4, ""),



"png", "jpg", "jpeg", "gif", "webp", "bmp", "svg", "ico", "heic", "tif", "tiff");


AddKind(m, MakeKind(0xA6, 0x76, 0x3F, 0xC0, 0x8C, 0x52, ""),



"zip", "7z", "rar", "gz", "tar", "bz2", "xz", "iso");


AddKind(m, MakeKind(0xE0, 0x5B, 0x57, 0xF0, 0x7C, 0x79, ""),



"mp4", "mkv", "avi", "mov", "wmv", "flv", "webm", "m4v", "mpg");


AddKind(m, MakeKind(0xD8, 0x74, 0x2A, 0xE9, 0x96, 0x4E, ""),



"mp3", "flac", "wav", "m4a", "aac", "ogg", "wma");


AddKind(m, MakeKind(0x5E, 0x7A, 0x94, 0x7E, 0x99, 0xB2, ""),



"exe", "msi", "lnk", "bat", "cmd", "ps1", "dll");


AddKind(m, MakeKind(0x4A, 0x90, 0xD9, 0x6F, 0xAE, 0xE8, ""),



"js", "mjs", "cjs", "ts", "jsx", "tsx", "py", "json", "html", "htm",



"css", "scss", "less", "yml", "yaml", "xml", "sql", "cs", "java",



"go", "rs", "sh", "vue", "svelte", "toml", "h", "cpp", "c");


AddKind(m, MakeKind(0x3C, 0x88, 0xC8, 0x5F, 0xA6, 0xDE, ""),



"psd", "ai", "sketch", "fig", "xd", "indd", "afdesign");


 return m;

}

 private static void AddKind(Dictionary<string, FileKind> m, FileKind k, params string[] exts)

{


for (int i = 0; i < exts.Length; i++) m[exts[i]] = k;

}

 private static FileKind KindOf(string name, bool isFolder)

{


if (isFolder) return null;   // null = 文件夹，走另一个形状


int dot = name.LastIndexOf('.');


if (dot < 0 || dot == name.Length - 1) return _kindDefault;


string e = name.Substring(dot + 1);


FileKind k;


return _kindMap.TryGetValue(e, out k) ? k : _kindDefault;

}

 // 自检用：外部可以拿到窗体，绕开键盘直接驱动界面

public static InputForm Instance;

 // 把一段文字塞进输入框，跑一次文件搜索，返回结果条数和窗口高度。

// 只给 /debugfiles 自检接口用；正常使用时没人调它。

public string DebugFileSearch(string text, int sel)

{


string result = "err";


try


{



this.Invoke((MethodInvoker)delegate



{




if (!Visible) ShowInput();




// text == null = 只把浮层叫出来、不动内容（测「恢复上次结果」用）




if (text != null)




{





placeholder = false;





tb.ForeColor = C_TXT;





// 真实使用里文件搜索必然以 / 开头；这里补齐，让自检口走的是同一条路径





tb.Text = text.StartsWith("/") ? text : "/" + text;





// 赋值会触发文本框的防抖计时器；这里要的是「立刻定住状态」，





// 否则 120ms 后它自己又跑一次搜索，把 sel / top 重置回 0。





if (_debounce != null) _debounce.Stop();





RunFileSearch();





// 搜索已经挪到后台线程了；这里要的是「结果出来之后再读状态」，





// 所以转一段消息泵等它回来（最多 3s），否则自检永远读到 hits=0。





for (int _i = 0; _i < 300 && _searching; _i++)





{






Application.DoEvents();






Thread.Sleep(10);





}




}




if (sel >= 0 && _hits.Count > 0)




{





_sel = sel;





EnsureVisible();





Refresh();




}




result = "visible=" + Visible + "  query=" + _query + "  hits=" + _hits.Count +






 "  height=" + Height + "  ms=" + _lastMs +






 "  sel=" + _sel + "  top=" + _top;



});


}


catch (Exception ex)


{



result = "err " + ex.Message;


}


return result;

}

 private int S(int v) { return (int)Math.Round(v * _scale); }

private float SF(float v) { return v * _scale; }

 // 字体一律按「像素」建，不用「磅」：200% 缩放下 8.5pt = 22.67px，

// 带小数的字高让笔画落在半个像素上，怎么调都糊。整数像素才压在网格上。

private static bool HasFamily(string name)

{


try { using (FontFamily f = new FontFamily(name)) return f.Name.Length > 0; }


catch { return false; }

}

 private static Font MakeFont(string family, float px, FontStyle style)

{


if (HasFamily(family)) return new Font(family, px, style, GraphicsUnit.Pixel);


return new Font("Microsoft YaHei UI", px, style, GraphicsUnit.Pixel);

}

 public InputForm()

{


try { _scale = Native.GetDpiForSystem() / 96f; }


catch { _scale = 1f; }


if (_scale < 0.5f || _scale > 4f) _scale = 1f;


 // 字体换成 Noto Sans SC：这台机器装了它，Medium 字重在小字号下笔画更实，


// 深色底上不发飘（微软雅黑 Regular 偏细，糊）。没装会自动退回雅黑。


_fBadge = MakeFont("Noto Sans SC", S(13), FontStyle.Bold);


_fHint = MakeFont("Noto Sans SC", S(12), FontStyle.Regular);


_glyphFonts = new Dictionary<string, Font>();


// 用户从 7 个方案里挑的是 F：19px 加粗。深色底上「锐利」靠笔画够实，


// 不是靠字号；只放大不加粗看着还是发飘。


_fRow = MakeFont("Noto Sans SC", S(19), FontStyle.Bold);


_fSmall = MakeFont("Noto Sans SC", S(13), FontStyle.Regular);


 FormBorderStyle = FormBorderStyle.None;


ShowInTaskbar = false;


TopMost = true;


StartPosition = FormStartPosition.Manual;


AutoScaleMode = AutoScaleMode.None;


Size = new Size(S(560), S(80));


BackColor = C_BG;


DoubleBuffered = true;


KeyPreview = true;


Padding = new Padding(0);


 tb = new TextBox();


tb.BorderStyle = BorderStyle.None;


tb.BackColor = C_BG;


tb.ForeColor = C_DIM;


// 注意：字号用 pt，DPI-aware 进程里系统已经按 DPI 换算过，


// 这里绝不能再乘 _scale，否则字体被放大两次、文字被裁掉看不见。


tb.Font = MakeFont("Noto Sans SC Medium", S(17), FontStyle.Regular);


tb.Multiline = true;


tb.ScrollBars = ScrollBars.None;


tb.AcceptsReturn = true;


tb.Location = new Point(S(54), S(18));


tb.Size = new Size(S(470), S(30));


tb.Text = phText;


Controls.Add(tb);


  AllowDrop = true;
 DragEnter += delegate(object s1, DragEventArgs de)
 {
  if (de.Data.GetDataPresent(DataFormats.FileDrop)) de.Effect = DragDropEffects.Copy;
  else de.Effect = DragDropEffects.None;
 };
 DragDrop += delegate(object s2, DragEventArgs de)
 {
  try { AddFiles((string[])de.Data.GetData(DataFormats.FileDrop)); }
  catch (Exception ex) { Log("drop error: " + ex.Message); }
 };
 tb.AllowDrop = true;
 tb.DragEnter += delegate(object s3, DragEventArgs de)
 {
  if (de.Data.GetDataPresent(DataFormats.FileDrop)) de.Effect = DragDropEffects.Copy;
  else de.Effect = DragDropEffects.None;
 };
 tb.DragDrop += delegate(object s4, DragEventArgs de)
 {
  try { AddFiles((string[])de.Data.GetData(DataFormats.FileDrop)); }
  catch (Exception ex) { Log("drop error: " + ex.Message); }
 };
 tb.KeyDown += delegate(object s5, KeyEventArgs ke)
 {
  if (ke.Control && ke.KeyCode == Keys.V)
  {
   if (PasteImage()) { ke.SuppressKeyPress = true; ke.Handled = true; }
  }
 };

 tb.GotFocus += delegate


{



if (placeholder)



{




placeholder = false;




tb.Text = "";




tb.ForeColor = C_TXT;



}


};


tb.LostFocus += delegate


{



if (tb.Text.Trim().Length == 0)



{




placeholder = true;




tb.Text = phText;




tb.ForeColor = C_DIM;



}


};


// 正常路径由 ProcessCmdKey 先接住，这里只做兜底（并留下日志）


tb.KeyDown += delegate(object sender, KeyEventArgs e)


{



if (e.KeyCode == Keys.Enter && e.Shift) return;



if (TryHandleOverlayKey(e.KeyCode, e.Shift, "tb.KeyDown"))



{




e.Handled = true;




e.SuppressKeyPress = true;



}


};


 // 兜底：万一焦点落在窗体而不是文本框上，回车/ Esc 也要能用


this.KeyDown += delegate(object sender, KeyEventArgs e)


{



if (e.Handled) return;



if (TryHandleOverlayKey(e.KeyCode, e.Shift, "Form.KeyDown"))




e.SuppressKeyPress = true;


};


 UpdateRegion();


 // 搜索防抖：打字过程中不要每次按键都查一遍


_debounce = new System.Windows.Forms.Timer();


_debounce.Interval = 120;


_debounce.Tick += delegate


{



_debounce.Stop();



RunFileSearch();


};


 _hoverTimer = new System.Windows.Forms.Timer();


_hoverTimer.Interval = 200;
   // 停住 200ms 才弹，鼠标路过不闪


_hoverTimer.Tick += delegate


{



_hoverTimer.Stop();



ShowTipForHover();


};


 // 显示期间每 150ms 看一眼「前台窗口还是不是我」。


// 为什么不用 Deactivate 事件：这个浮层是用 AttachThreadInput +


// SetForegroundWindow 硬抢到前台的，WinForms 那套 Activated/Deactivate


// 记账不一定被触发（实测点了浏览器外面它压根没响）。看前台窗口是唯一靠得住的。


_watchTimer = new System.Windows.Forms.Timer();


_watchTimer.Interval = 150;


_watchTimer.Tick += delegate


{



if (!Visible) { _watchTimer.Stop(); return; }



if ((DateTime.Now - _shownAt).TotalMilliseconds < 250) return;



IntPtr fg = Native.GetForegroundWindow();



if (fg == Handle) return;



Log("HideOnFocusLost  fg=" + fg + "  me=" + Handle);



_watchTimer.Stop();



CancelAndHide();


};


 // 打字时实时刷新：是搜索、还是发送，边打边能看到


// 用 Refresh（= Invalidate + Update）强制立刻重绘，


// 只 Invalidate 的话要等消息队列空闲，打字快时肉眼能看出来滞后。


tb.TextChanged += delegate


{



if (!Visible) return;



// 自愈：占位符标志可能因为没经过 GotFocus 而残留成 true。
// 只要框里的内容已经不是占位符本身，就当作真的输入，并把颜色改回正常。
if (placeholder && tb.Text != phText)
{
 placeholder = false;
 tb.ForeColor = C_TXT;
 Log("placeholder flag cleared by typing");
}



 if (IsFileMode())



{




_debounce.Stop();




_debounce.Start();



}



else if (_hits.Count > 0)



{




ClearHits();



}



 Refresh();


};


 // 强制创建句柄，触发 OnHandleCreated 里的热键注册


IntPtr h = Handle;


 Instance = this;

}

 protected override void OnHandleCreated(EventArgs e)

{


base.OnHandleCreated(e);


RegisterHotkeyNow();

}

 private void RegisterHotkeyNow()

{


// 窗口句柄可能在显示时被重建，热键必须绑在当前句柄上，否则收不到 WM_HOTKEY


try { Native.UnregisterHotKey(Handle, HOTKEY_ID); }


catch { }


HotkeyOk = Native.RegisterHotKey(Handle, HOTKEY_ID, Cfg.Mods, Cfg.Vk);

}

 // 所有键盘入口都收敛到这里：谁先收到都只产生一个结果，不会重复发送。

private bool TryHandleOverlayKey(Keys keyCode, bool shift, string from)

{


if (!Visible) return false;


 if (keyCode == Keys.Escape)


{



Log("KEY ESC   via " + from);



CancelAndHide();



return true;


}


if (keyCode == Keys.Enter && !shift)


{



Log("KEY ENTER via " + from);



DoSend();



return true;


}


return false;

}

 // ── 文件搜索：状态与动作 ──────────────────────────────

 // 中文输入法里 / 很容易打成全角（／ U+FF0F）或分数斜杠（⁄ U+2044），
 // 那样 IsFileMode 判不出来，用户看到的就是「搜索没反应」。
 private static string StripFilePrefix(string raw)
 {
  string t = (raw == null) ? "" : raw.Trim();
  if (t.Length > 0 && (t[0] == '/' || t[0] == '\uFF0F' || t[0] == '\u2044')) t = t.Substring(1);
  return t.Trim();
 }

 private bool IsFileMode()

{


// 别用 placeholder 这个标志判断：它是「占位符还显示着」的状态位，
// 一旦因为焦点没经过 GotFocus 而没被清掉，这里就会永远返回 false ——
// 表现就是打 / 不进文件模式、字还是灰的（2026-10-22 实测到的那个 bug）。


string t = tb.Text;


if (t == null) return false;


if (t.Trim().Length == 0) return false;
if (t.Trim() == phText.Trim()) return false;
string ts = t.TrimStart();
if (ts.Length == 0) return false;
char c0 = ts[0];
return c0 == '/' || c0 == '\uFF0F' || c0 == '\u2044';

}

 // 搜索放到后台线程跑：Everything_QueryW 是阻塞且不可取消的，

// 一旦它慢（比如误触发正文扫描），在 UI 线程上就是整个浮层冻死 —— 没有报错，看着就像「坏了」。

private int _searchGen = 0;

private bool _searching = false;

private string _searchNote = "";

 private void RunFileSearch()

{


string t = StripFilePrefix(tb.Text);


if (t.StartsWith("/")) t = t.Substring(1);


_query = t.Trim();


_hoverRow = -1;


HideTip();


 if (_query.Length == 0)


{



_searchGen++;


 // 作废还在路上的结果



_searching = false;



_searchNote = "";



ClearHits();



Refresh();



return;


}


 int gen = ++_searchGen;

 InputForm.Log("search start q=[" + _query + "]  gen=" + gen);


string q = _query;


_searching = true;


_searchNote = "";


Refresh();




// 先把「搜索中…」画出来，界面立刻有反应


 ThreadPool.QueueUserWorkItem(delegate


{



List<FileHit> hits;



int ms = 0;



try



{




long t0 = DateTime.Now.Ticks;




hits = Everything.Query(q, 19);




ms = (int)((DateTime.Now.Ticks - t0) / 10000L);



}



catch { hits = new List<FileHit>(); }



 List<FileHit> hh = hits;



int mm = ms;



try { BeginInvoke((MethodInvoker)delegate { ApplyFileHits(gen, q, hh, mm); }); }



catch { }


});

}

 private void ApplyFileHits(int gen, string q, List<FileHit> hits, int ms)

{


// 期间用户又改了输入、或已经离开文件模式 → 这批结果作废


if (gen != _searchGen || !IsFileMode() || _query != q)
 {
  InputForm.Log("search DROP q=[" + q + "]  gen=" + gen + "/" + _searchGen + "  fileMode=" + IsFileMode() + "  cur=[" + _query + "]");
  return;
 }

 InputForm.Log("search q=[" + q + "]  hits=" + hits.Count + "  ms=" + ms + "  err=" + Everything.LastError);


 _searching = false;


 _searchNote = Everything.LastNote;

 if (hits.Count == 0 && Everything.LastError.Length > 0) _searchNote = "Everything 没响应（" + Everything.LastError + "）";


_lastMs = ms;


_hoverRow = -1;



// 结果换了，旧的详情条内容就作废了


HideTip();


 _hits.Clear();


_hits.AddRange(hits);


_sel = 0;


_top = 0;


ApplyHeight();


Refresh();

}

 // 同步版：给需要「立刻拿到结果」的地方用（自检口、启动探针）。

private void SearchNow()

{


string t = StripFilePrefix(tb.Text);


if (t.StartsWith("/")) t = t.Substring(1);


_query = t.Trim();


_searchGen++;



 // 作废在途的异步结果，免得它晚点把状态覆盖回去


_searching = false;


 if (_query.Length == 0)


{



_searchNote = "";



ClearHits();



Refresh();



return;


}


 long t0 = DateTime.Now.Ticks;


List<FileHit> hits = Everything.Query(_query, 19);


_lastMs = (int)((DateTime.Now.Ticks - t0) / 10000L);


_searchNote = Everything.LastNote;


 _hits.Clear();


_hits.AddRange(hits);


_sel = 0;


_top = 0;


ApplyHeight();


Refresh();

}

 // ── 排版对比：把同一批结果按不同字体/字号/渲染方式离屏画成图片 ──

// 不建窗口、不注册热键、不等 Everything，跑完就退出。

// 用途：调字体的时候一次出一组图给用户挑，别反复弹浮层打扰他。

private sealed class Variant

{


public string Key;


public string Family;


public int NamePx;


public int PathPx;


public FontStyle Style;


public bool Gdi;


public bool Gray;

}

 internal static void RenderSheet(string outDir)

{


Variant[] vs = new Variant[]


{



new Variant { Key = "A", Family = "Noto Sans SC Medium", NamePx = 15, PathPx = 11, Style = FontStyle.Regular, Gdi = true },



new Variant { Key = "B", Family = "Noto Sans SC Medium", NamePx = 17, PathPx = 12, Style = FontStyle.Regular, Gdi = true },



new Variant { Key = "C", Family = "Noto Sans SC Medium", NamePx = 19, PathPx = 13, Style = FontStyle.Regular, Gdi = true },



new Variant { Key = "D", Family = "Noto Sans SC Medium", NamePx = 17, PathPx = 12, Style = FontStyle.Regular, Gdi = false, Gray = true },



new Variant { Key = "E", Family = "Noto Sans SC", NamePx = 17, PathPx = 12, Style = FontStyle.Bold, Gdi = true },



new Variant { Key = "F", Family = "Noto Sans SC", NamePx = 19, PathPx = 13, Style = FontStyle.Bold, Gdi = true },



new Variant { Key = "G", Family = "Microsoft YaHei UI", NamePx = 17, PathPx = 12, Style = FontStyle.Bold, Gdi = true }


};


 FileHit[] hits = new FileHit[]


{



new FileHit { Name = "2026-07-07_完整会话记录——原型与锚点.md",






  FullPath = @"D:\知识库\concepts-历史\2026-07-07_完整会话记录——原型与锚点.md" },



new FileHit { Name = "季度报告数据底表.xlsx",






  FullPath = @"D:\工作\2026\底表\季度报告数据底表.xlsx" },



new FileHit { Name = "缺失内核的男人——关系被动",






  FullPath = @"D:\知识库\认知、成长、关系", IsFolder = true }


};


 foreach (Variant v in vs)


{



InputForm f = new InputForm();



f._scale = 2f;



f.Width = 1120;



f._query = "锚点";



f._useGdi = v.Gdi;



f._fRow = MakeFont(v.Family, f.S(v.NamePx), v.Style);



f._fSmall = MakeFont(v.Family, f.S(v.PathPx), FontStyle.Regular);



f._fHint = MakeFont(v.Family, f.S(11), FontStyle.Regular);



f._nameLineH = -1f;



f._pathLineH = -1f;



f._hintLineH = -1f;



 Bitmap panel = new Bitmap(1120, 240);



using (Graphics g = Graphics.FromImage(panel))



{




g.SmoothingMode = SmoothingMode.AntiAlias;




g.TextRenderingHint = v.Gray





? System.Drawing.Text.TextRenderingHint.AntiAliasGridFit





: System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;




g.Clear(Color.FromArgb(6, 8, 12));




 using (GraphicsPath p = Round(new Rectangle(0, 0, 1119, 239), 16))




using (SolidBrush bg = new SolidBrush(C_BG))





g.FillPath(bg, p);




 for (int i = 0; i < hits.Length; i++)




{





int y = 14 + i * 60;





if (i == 0)






using (SolidBrush sel = new SolidBrush(Color.FromArgb(19, 42, 69)))







g.FillRectangle(sel, 16, y, 1088, 58);





f.DrawHitRow(g, hits[i], 32, y, 60);




}




 f.DrawText(g, "文件搜索 · 19 条 · 31ms · 第 2 / 19 条", f._fHint,





Color.FromArgb(124, 170, 255), 108, 200);



}



 Bitmap outb = new Bitmap(1120, 376);



using (Graphics g = Graphics.FromImage(outb))



{




g.Clear(Color.FromArgb(6, 8, 12));




g.DrawImage(panel, 0, 0);




g.InterpolationMode = InterpolationMode.NearestNeighbor;




g.PixelOffsetMode = PixelOffsetMode.Half;




// 2 倍放大第一行的文字，用来判断锐利度




g.DrawImage(panel, new Rectangle(0, 252, 1120, 116),





new Rectangle(88, 14, 560, 58), GraphicsUnit.Pixel);



}



 string path = Path.Combine(outDir, "font-" + v.Key + ".png");



outb.Save(path, System.Drawing.Imaging.ImageFormat.Png);



outb.Dispose();



panel.Dispose();



Console.WriteLine("saved " + path);


}

}

 // 托盘/窗体图标：用 exe 里内嵌的 QuickInput.ico（9 个尺寸，Windows 按需挑）

internal static Icon LoadAppIcon(int size)

{


try


{



System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();



using (Stream s = asm.GetManifestResourceStream("QuickInput.ico"))



{




if (s != null)




{





using (Icon full = new Icon(s, new Size(size, size)))






return (Icon)full.Clone();




}



}


}


catch { }


return SystemIcons.Application;

}

 // ── 离屏出图：把浮层的各个状态画成 PNG（给文档/演示用）──

// 直接调真正的 OnPaint，所以图就是真界面，但全程不显示任何窗口。

private static FileHit Hit(string name, string dir)

{


FileHit h = new FileHit();


h.Name = name;


h.FullPath = dir + "\\" + name;


return h;

}

 private static FileHit Dir(string name, string dir)

{


FileHit h = new FileHit();


h.Name = name;


h.FullPath = dir + "\\" + name;


h.IsFolder = true;


return h;

}

 private static List<FileHit> QuarterHits()

{


List<FileHit> l = new List<FileHit>();


l.Add(Hit("2026 二季度经营分析报告.docx", @"D:\工作\2026\经营分析"));


l.Add(Hit("季度报告数据底表.xlsx", @"D:\工作\2026\经营分析\底表"));


l.Add(Hit("Q1 季度报告（终稿）.pdf", @"D:\工作\2026\归档"));


l.Add(Dir("报告归档", @"D:\工作\2026"));


return l;

}

 private static List<FileHit> ManyHits()

{


List<FileHit> l = new List<FileHit>();


l.Add(Hit("2026 二季度经营分析报告.docx", @"D:\工作\2026\经营分析"));


l.Add(Hit("季度报告数据底表.xlsx", @"D:\工作\2026\经营分析\底表"));


l.Add(Hit("Q1 季度报告（终稿）.pdf", @"D:\工作\2026\归档"));


l.Add(Hit("2026-07-07_完整会话记录——原型与锚点.md", @"D:\知识库\concepts-历史"));


l.Add(Hit("报告模板（中文）.dotx", @"D:\工作\_模板"));


l.Add(Hit("季度复盘会议纪要.md", @"D:\工作\2026\会议"));


l.Add(Hit("年度报告提纲.txt", @"D:\工作\2026\草稿"));


l.Add(Hit("report-2026-q3.pdf", @"D:\工作\2026\外发"));


l.Add(Hit("2026-07-07_完整会话记录——原型与锚点与第二轮修订与最终确认版.md", @"D:\知识库\归档项目\2026\第二阶段\原始素材\会议记录\未整理"));


l.Add(Dir("报告归档", @"D:\工作\2026"));


l.Add(Hit("经营分析看板.png", @"D:\工作\2026\图表"));


l.Add(Hit("财务口径说明.docx", @"D:\工作\_制度"));
            l.Add(Hit("2026-07-07_完整会话记录——原型与锚点与第二轮修订与最终确认版与对外发布说明的补充材料.md", @"D:\知识库\归档项目\2026\第二阶段"));


return l;

}

 private static void ShotOne(string outDir, string file, string text, bool ph,








List<FileHit> hits, int sel, int top, string query, int tipRow)

{


InputForm f = new InputForm();


f._scale = 2f;


f.Width = f.S(560);


f.placeholder = ph;


f.tb.Text = ph ? f.phText : text;


f.tb.ForeColor = ph ? C_DIM : C_TXT;


f._query = query;


f._hits.Clear();


if (hits != null) f._hits.AddRange(hits);


f._sel = sel;


f._top = top;


f._nameLineH = -1f; f._pathLineH = -1f; f._hintLineH = -1f;


// 让底部显示「扩展已连接」，不然文档里会写成「未连接」


Bridge.LastPoll = DateTime.Now;


 // 悬停详情：出图时直接按「正在悬停某一条」的状态排版


if (tipRow >= 0 && tipRow < f._hits.Count)


{



FileHit th = f._hits[tipRow];



f._tipRow = tipRow;



f._tipName = th.Name;



f._tipPath = th.FullPath;



f._tipMeta = f.MetaFor(th);



using (Graphics gg = f.CreateGraphics())
                {
                    f.CalcTipPathLines(gg);
                    f._tipH = f.TipHeight(gg);
                }


}


 int h = f.S(BASE_H) + (f._hits.Count > 0 ? f.S(Math.Min(f._hits.Count, MAX_ROWS) * ROW_H + 30) : 0)




+ f._tipH;


f.Height = h;


 Bitmap bmp = new Bitmap(f.Width, h);


using (Graphics g = Graphics.FromImage(bmp))


{



g.Clear(Color.FromArgb(11, 15, 20));



g.SmoothingMode = SmoothingMode.AntiAlias;



g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;



f.OnPaint(new PaintEventArgs(g, new Rectangle(0, 0, f.Width, h)));



 // 输入框里的字由 TextBox 控件自己画，OnPaint 里没有，这里补上



Rectangle tr = new Rectangle(f.S(54), f.S(18), f.S(470), f.S(30));



TextRenderer.DrawText(g, f.tb.Text, f.tb.Font, tr,




ph ? C_DIM : C_TXT,




TextFormatFlags.Left | TextFormatFlags.VerticalCenter




| TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);


}


 string path = Path.Combine(outDir, file + ".png");


bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);


bmp.Dispose();


Console.WriteLine("saved " + path);

}

 internal static void ShotStates(string outDir)

{


ShotOne(outDir, "01-idle", "", true, null, 0, 0, "", -1);


ShotOne(outDir, "02-dsh", "把上面三条整理成提纲", false, null, 0, 0, "", -1);


ShotOne(outDir, "03-site", "hh 800库", false, null, 0, 0, "", -1);


ShotOne(outDir, "04-peek", "hh", false, null, 0, 0, "", -1);


ShotOne(outDir, "05-files", "/季度报告", false, QuarterHits(), 0, 0, "季度报告", -1);


ShotOne(outDir, "06-scroll", "/报告", false, ManyHits(), 6, 3, "报告", -1);


ShotOne(outDir, "07-nohit", "/季度报告", false, new List<FileHit>(), 0, 0, "季度报告", -1);


ShotOne(outDir, "08-hover", "/报告", false, ManyHits(), 6, 3, "报告", 3);


// 同一个详情区、只换了内容（短文件名 + 短路径）—— 高度必须和 08 完全一样


ShotOne(outDir, "09-hover-short", "/报告", false, ManyHits(), 6, 3, "报告", 6);


// 深路径：验证绝对路径确实能折成两行、而不是被省略掉


ShotOne(outDir, "10-hover-longpath", "/报告", false, ManyHits(), 8, 8, "报告", 8);
            // 超长文件名：验证「开头 + … + 结尾」，结尾版本号不能被砍掉
            ShotOne(outDir, "11-hover-longname", "/报告", false, ManyHits(), 12, 12, "报告", 12);

}

 // 自检用：等价于点一下浮层外面

public void DebugHide()

{


try { this.Invoke((MethodInvoker)delegate { CancelAndHide(); }); }


catch { }

}

 private void ClearHits()

{


_hits.Clear();


_sel = 0;


_top = 0;


ApplyHeight();

}

 private const int ATT_H = 34;

 private readonly List<QAtt> _atts = new List<QAtt>();

 private readonly List<Rectangle> _chipRects = new List<Rectangle>();

 private void AddFiles(string[] paths)
 {
  if (paths == null) return;
  for (int i = 0; i < paths.Length && _atts.Count < 4; i++)
  {
   try
   {
    FileInfo fi = new FileInfo(paths[i]);
    if (!fi.Exists) continue;
    if (fi.Length > 12L * 1024 * 1024) { hintText = "单个文件上限 12 MB"; continue; }
    QAtt a = new QAtt();
    a.Name = fi.Name;
    a.Mime = MimeFor(fi.Extension);
    a.Data = File.ReadAllBytes(fi.FullName);
    _atts.Add(a);
    Log("attach + " + fi.Name + "  " + fi.Length + "B");
   }
   catch (Exception ex) { Log("attach error: " + ex.Message); }
  }
  ApplyHeight();
  Invalidate();
 }

 private bool PasteImage()
 {
  try
  {
   if (Clipboard.ContainsImage())
   {
    Image img = Clipboard.GetImage();
    if (img == null) return false;
    using (MemoryStream ms = new MemoryStream())
    {
     img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
     QAtt a = new QAtt();
     a.Name = "clipboard-" + DateTime.Now.ToString("HHmmss") + ".png";
     a.Mime = "image/png";
     a.Data = ms.ToArray();
     _atts.Add(a);
    }
    img.Dispose();
    ApplyHeight();
    Invalidate();
    Log("attach + clipboard png");
    return true;
   }
   if (Clipboard.ContainsFileDropList())
   {
    System.Collections.Specialized.StringCollection sc = Clipboard.GetFileDropList();
    string[] arr = new string[sc.Count];
    sc.CopyTo(arr, 0);
    AddFiles(arr);
    return true;
   }
  }
  catch (Exception ex) { Log("paste error: " + ex.Message); }
  return false;
 }

 private static string MimeFor(string ext)
 {
  ext = (ext == null) ? "" : ext.ToLowerInvariant();
  if (ext == ".png") return "image/png";
  if (ext == ".jpg" || ext == ".jpeg") return "image/jpeg";
  if (ext == ".gif") return "image/gif";
  if (ext == ".webp") return "image/webp";
  if (ext == ".pdf") return "application/pdf";
  if (ext == ".txt" || ext == ".md" || ext == ".csv") return "text/plain";
  if (ext == ".json") return "application/json";
  return "application/octet-stream";
 }

 private void DrawAtts(Graphics g, int footY)
 {
  _chipRects.Clear();
  if (_atts.Count == 0) return;
  int h = S(22);
  int y = footY - S(6) - h;
  if (y < S(50)) y = S(50);
  int x = S(54);
  int maxRight = Width - S(20);
  for (int i = 0; i < _atts.Count; i++)
  {
   string nm = _atts[i].Name;
   if (nm == null) nm = "";
   if (nm.Length > 26) nm = nm.Substring(0, 23) + "...";
   string label = nm + "  x";
   Size sz = TextRenderer.MeasureText(g, label, _fHint, new Size(0, 0),
     TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
   int w = sz.Width + S(18);
   if (x + w > maxRight) break;
   Rectangle r = new Rectangle(x, y, w, h);
   _chipRects.Add(r);
   using (SolidBrush b = new SolidBrush(Color.FromArgb(28, 36, 48)))
    g.FillRectangle(b, r);
   using (Pen p = new Pen(Color.FromArgb(64, 80, 104)))
    g.DrawRectangle(p, r);
   Rectangle tr = new Rectangle(r.X + S(9), r.Y, r.Width - S(12), r.Height);
   TextRenderer.DrawText(g, label, _fHint, tr, Color.FromArgb(198, 210, 228),
     TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix
     | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
   x += w + S(6);
  }
 }

 private void ApplyHeight()

{


int rows = Math.Min(_hits.Count, MAX_ROWS);


int h = S(BASE_H) + (rows > 0 ? S(rows * ROW_H + 30) : 0) + (_atts.Count > 0 ? S(ATT_H) : 0) + _tipH;


if (Height != h) Height = h;


 // 悬停长高后别把底边顶出屏幕外（贴底时往上让一点）


if (_tipH > 0)


{



try



{




Rectangle wa = Screen.FromControl(this).WorkingArea;




if (Top + Height > wa.Bottom) Top = Math.Max(wa.Top, wa.Bottom - Height);



}



catch { }


}


 UpdateRegion();

}

 // 选中项在全部结果里自由移动（以前只在「看得见的 4 条」里绕圈，

// 所以第 5 条往后永远选不到），再由 EnsureVisible 把窗口跟过去。

private void MoveSel(int d)

{


if (_hits.Count == 0) return;


_hoverRow = -1;


HideTip();



 // 键盘/滚轮一动，鼠标停的那条就不是他看的那条了


_sel += d;


if (_sel < 0) _sel = _hits.Count - 1;


if (_sel >= _hits.Count) _sel = 0;


EnsureVisible();


Refresh();

}

 private void EnsureVisible()

{


int n = _hits.Count;


if (n <= 0) { _top = 0; _sel = 0; return; }


if (_sel < 0) _sel = 0;


if (_sel > n - 1) _sel = n - 1;


if (_sel < _top) _top = _sel;


else if (_sel >= _top + MAX_ROWS) _top = _sel - MAX_ROWS + 1;


int maxTop = n - MAX_ROWS;


if (maxTop < 0) maxTop = 0;


if (_top > maxTop) _top = maxTop;


if (_top < 0) _top = 0;

}

 // 鼠标滚轮：一格一条，走同一套 MoveSel，保证两端行为一致

private void WheelSel(int delta)

{


if (!Visible || !IsFileMode() || _hits.Count <= 1) return;


MoveSel(delta > 0 ? -1 : 1);

}

 // 把鼠标的 y 坐标换算成结果下标；落在列表外返回 -1

private int RowIndexAt(int y)

{


if (_hits.Count == 0) return -1;


int first = S(BASE_H) - S(6) + S(4);


int rowH = S(ROW_H);


if (y < first) return -1;


int slot = (y - first) / rowH;


if (slot < 0 || slot >= MAX_ROWS) return -1;


int idx = _top + slot;


return idx < _hits.Count ? idx : -1;

}

 // ── 悬停：把被截断的那一行完整交代清楚 ──

protected override void OnMouseMove(MouseEventArgs e)

{


base.OnMouseMove(e);


int idx = (Visible && IsFileMode()) ? RowIndexAt(e.Y) : -1;


if (idx == _hoverRow) return;


_hoverRow = idx;


HideTip();







  // 换了行就先撤掉旧的，别停在别人的信息上


if (_hoverTimer != null)


{



_hoverTimer.Stop();



if (idx >= 0) _hoverTimer.Start();


}

}

 protected override void OnMouseLeave(EventArgs e)

{


base.OnMouseLeave(e);


_hoverRow = -1;


if (_hoverTimer != null) _hoverTimer.Stop();


HideTip();

}

 private void HideTip()

{


if (_tipRow < 0 && _tipH == 0) return;


_tipRow = -1;


_tipH = 0;


_tipName = ""; _tipPath = ""; _tipMeta = "";


ApplyHeight();


if (Visible) Refresh();

}

 // ── 右键菜单：对「光标下那个文件」做事 ──

// 菜单是 ToolStripDropDown，它一显示就会把前台抢走，而「点浮层外面就收起」

// 正是靠前台窗口判断的 —— 所以菜单开着的时候先把那个检查停掉，关掉再恢复。

protected override void OnMouseUp(MouseEventArgs e)

{


base.OnMouseUp(e);


if (!Visible || !IsFileMode() || e.Button != MouseButtons.Right) return;


 int idx = RowIndexAt(e.Y);


if (idx < 0) return;


 if (_sel != idx) { _sel = idx; EnsureVisible(); }


_hoverRow = -1;


if (_hoverTimer != null) _hoverTimer.Stop();


HideTip();


Refresh();


 if (_ctx == null) _ctx = BuildRowMenu();


_ctx.Tag = idx;


 if (_watchTimer != null) _watchTimer.Stop();


_ctx.Show(this, PointToClient(Cursor.Position));

}

 private FileHit MenuHit()

{


if (_ctx == null || _ctx.Tag == null) return null;


int i = (int)_ctx.Tag;


return (i >= 0 && i < _hits.Count) ? _hits[i] : null;

}

 private void MenuAct(int what)

{


FileHit h = MenuHit();


if (h == null) return;


try


{



switch (what)



{




case 1:







 // 打开





_sel = (int)_ctx.Tag;





OpenSelected(false);





break;




case 2:







 // 在文件夹中显示（资源管理器里选中这个文件）





_sel = (int)_ctx.Tag;





OpenSelected(true);





break;




case 3:







 // 打开所在文件夹





System.Diagnostics.Process.Start("explorer.exe",






"\"" + Path.GetDirectoryName(h.FullPath) + "\"");





Log("ctx open folder  " + h.FullPath);





CancelAndHide();





break;




case 4:







 // 复制完整路径





Clipboard.SetText(h.FullPath);





Log("ctx copy path  " + h.FullPath);





break;




case 5:







 // 复制文件名





Clipboard.SetText(h.Name);





Log("ctx copy name  " + h.Name);





break;



}


}


catch (Exception ex) { Log("ctx menu error  " + what + "  " + ex.Message); }

}

 private ToolStripMenuItem MenuItem(string text, int what)

{


ToolStripMenuItem it = new ToolStripMenuItem(text);


it.ForeColor = Color.FromArgb(228, 236, 250);


it.Padding = new Padding(S(10), S(4), S(10), S(4));


int code = what;


it.Click += delegate { MenuAct(code); };


return it;

}

 private ContextMenuStrip BuildRowMenu()

{


ContextMenuStrip m = new ContextMenuStrip();


m.Font = _fSmall;


m.BackColor = Color.FromArgb(26, 31, 45);


m.ForeColor = Color.FromArgb(228, 236, 250);


m.ShowImageMargin = false;


m.ShowCheckMargin = false;


m.Padding = new Padding(0, S(3), 0, S(3));


m.Renderer = new DarkMenuRenderer();


 m.Items.Add(MenuItem("打开", 1));


m.Items.Add(MenuItem("在文件夹中显示", 2));


m.Items.Add(MenuItem("打开所在文件夹", 3));


m.Items.Add(new ToolStripSeparator());


m.Items.Add(MenuItem("复制完整路径", 4));


m.Items.Add(MenuItem("复制文件名", 5));


 m.Closed += delegate


{



// 菜单收起来之后，把「失焦就收」和输入焦点都还回去



try { if (Visible) { tb.Focus(); } } catch { }



if (_watchTimer != null && Visible) { _shownAt = DateTime.Now; _watchTimer.Start(); }


};


return m;

}

 // 菜单配色跟浮层一套；系统默认的浅色在深底上会很刺眼

private sealed class DarkMenuRenderer : ToolStripSystemRenderer

{


protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)


{



using (SolidBrush b = new SolidBrush(Color.FromArgb(26, 31, 45)))




e.Graphics.FillRectangle(b, e.AffectedBounds);


}


 protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }


 protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)


{



if (!e.Item.Selected) return;



using (SolidBrush b = new SolidBrush(Color.FromArgb(56, 72, 116)))




e.Graphics.FillRectangle(b, new Rectangle(0, 0, e.Item.Width, e.Item.Height));


}


 protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)


{



e.TextColor = e.Item.Selected ? Color.White : Color.FromArgb(226, 234, 248);



base.OnRenderItemText(e);


}


 protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)


{



using (Pen p = new Pen(Color.FromArgb(48, 58, 84)))



{




int y = e.Item.Height / 2;




e.Graphics.DrawLine(p, 2, y, e.Item.Width - 4, y);



}


}


 protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)


{



using (Pen p = new Pen(Color.FromArgb(74, 90, 152)))




e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);


}

}

 private void ShowTipForHover()

{


if (!Visible || !IsFileMode()) return;


if (_hoverRow < 0 || _hoverRow >= _hits.Count) return;


if (_tipRow == _hoverRow) return;

 // 已经是这条了，别重排


 FileHit h = _hits[_hoverRow];


_tipName = h.Name;


_tipPath = h.FullPath;


_tipMeta = MetaFor(h);


_tipRow = _hoverRow;


 using (Graphics g = CreateGraphics())


{



CalcTipPathLines(g);

 // 这批结果的路径最长要几行，先定下来



_tipH = TipHeight(g);


}


ApplyHeight();


Refresh();

}

 // 详情区高度是固定的：两行文件名 + 两行绝对路径 + 一行类型。

// 不跟着内容长短变高变矮 —— 悬浮时一会长一会短，扫一眼像在抖。

// 两行路径是给「完整显示」用的：Windows 路径里没有空格，GDI 的自动换行不认它，

// 所以路径是自己按 \ 断行的（见 WrapPathLines）。

// 注意这里返回的是「物理像素」，外层不要再套 S()。

private int TipHeight(Graphics g)

{


if (_tipHFixed > 0) return _tipHFixed;


 if (_nameLineH < 0) _nameLineH = Measure(g, "国Ag", _fRow).Height;


if (_pathLineH < 0) _pathLineH = Measure(g, "国Ag", _fSmall).Height;


 int pl = _tipPathLines < 1 ? 1 : _tipPathLines;


int nl = _tipNameLines < 1 ? 1 : _tipNameLines;
            _tipHFixed = S(11) + (int)Math.Round(_nameLineH * nl) + S(4)




   + (int)Math.Round(_pathLineH * pl) + S(4) + (int)_pathLineH + S(10);


return _tipHFixed;

}

 // 路径要几行：在 1~4 之间，按当前这批结果里最长的那个路径定。

// 同一批结果里恒定 —— 鼠标在行间移动不会变高度；换了搜索词重新算，

// 这样深目录能完整折行，浅目录也不会留一大块白。

private void CalcTipPathLines(Graphics g)

{


int inner = Width - S(36);


if (inner < S(120)) inner = S(120);


const int cap = 4;


 int need = 1;


List<string> tmp = new List<string>(cap + 1);


for (int i = 0; i < _hits.Count; i++)


{



WrapPathLines(g, _hits[i].FullPath, inner, cap, tmp);



if (tmp.Count > need) need = tmp.Count;



if (need >= cap) { need = cap; break; }


}


 // 文件名要几行（1~3）：同样按这批结果里最长的那个定
            if (_nameLineH < 0) _nameLineH = Measure(g, "国Ag", _fRow).Height;
            int nameNeed = 1;
            for (int i = 0; i < _hits.Count; i++)
            {
                int hh = TextRenderer.MeasureText(g, _hits[i].Name, _fRow, new Size(inner, 0), TipFmt).Height;
                int lines = (int)Math.Round(hh / Math.Max(1.0, (double)_nameLineH));
                if (lines > nameNeed) nameNeed = lines;
                if (nameNeed >= 3) { nameNeed = 3; break; }
            }
            _tipNameLines = nameNeed;

            _tipPathLines = need;


_tipHFixed = 0;



  // 行数变了，高度重算

}

 // 文件名最多铺两行；超了就二分找能放下两行的最长前缀

private string FitTipName(Graphics g, int inner)

{


string s = _tipName;


if (s.Length == 0) return s;


 int maxH = (int)Math.Round(_nameLineH * (_tipNameLines < 1 ? 1 : _tipNameLines));


if (TextRenderer.MeasureText(g, s, _fRow, new Size(inner, 0), TipFmt).Height <= maxH) return s;


 int tail = Math.Min(14, Math.Max(4, s.Length / 5));
            string tailStr = s.Substring(s.Length - tail);
            int lo = 1, hi = s.Length, best = 1;


while (lo <= hi)


{



int mid = (lo + hi) / 2;



string cand = s.Substring(0, mid) + "…" + tailStr;



if (TextRenderer.MeasureText(g, cand, _fRow, new Size(inner, 0), TipFmt).Height <= maxH)



{




best = mid;




lo = mid + 1;



}



else hi = mid - 1;


}


return s.Substring(0, best) + "…" + tailStr;

}

 // 绝对路径按 \ 断成最多 maxLines 行：能在目录分隔处换就在那儿换，

// 整段路径还不够放时，末行加省略号。

private void WrapPathLines(Graphics g, string s, int maxW, int maxLines, List<string> outLines)

{


outLines.Clear();


if (string.IsNullOrEmpty(s)) { outLines.Add(""); return; }


 int pos = 0;


while (pos < s.Length && outLines.Count < maxLines)


{



int lo = 1, hi = s.Length - pos, best = 1;



while (lo <= hi)



{




int mid = (lo + hi) / 2;




if (Measure(g, s.Substring(pos, mid), _fSmall).Width <= maxW) { best = mid; lo = mid + 1; }




else hi = mid - 1;



}



 int take = best;



if (pos + take < s.Length)



{




int slash = s.LastIndexOf('\\', pos + take - 1, take);




if (slash > pos) take = slash - pos + 1;



}



outLines.Add(s.Substring(pos, take));



pos += take;


}


 if (pos < s.Length && outLines.Count > 0)


{



int li = outLines.Count - 1;



if (!outLines[li].EndsWith("…")) outLines[li] = FitText(g, outLines[li] + "…", _fSmall, maxW);


}

}

 private void DrawTip(Graphics g)

{


int top = Height - _tipH;



// _tipH 已经是物理像素


int padX = S(18);


int inner = Width - S(36);


if (inner < S(120)) inner = S(120);


 using (Pen p = new Pen(Color.FromArgb(40, 50, 72)))



g.DrawLine(p, S(16), top, Width - S(16), top);


 // 面板高度是固定的，内容按实际行数整体居中：


// 短名字不会离路径隔一大截，长名字正好把两行占满。


string name = FitTipName(g, inner);


int nameH = TextRenderer.MeasureText(g, name, _fRow, new Size(inner, 0), TipFmt).Height;


int lineH = (int)_pathLineH;


 List<string> pl = new List<string>(3);


WrapPathLines(g, _tipPath, inner, _tipPathLines < 1 ? 1 : _tipPathLines, pl);


 int used = nameH + S(4) + lineH * pl.Count + S(4) + lineH;


int areaH = _tipH - S(11) - S(10);


int y = top + S(11) + Math.Max(0, (areaH - used) / 2);


 TextRenderer.DrawText(g, name, _fRow,



new Rectangle(padX, y, inner, nameH),



Color.FromArgb(238, 244, 255), TipFmt);


y += nameH + S(4);


 for (int i = 0; i < pl.Count; i++)


{



TextRenderer.DrawText(g, pl[i], _fSmall,




new Rectangle(padX, y, inner, lineH),




Color.FromArgb(152, 172, 205), TipOne);



y += lineH;


}


y += S(4);


 TextRenderer.DrawText(g, _tipMeta, _fSmall,



new Rectangle(padX, y, inner, lineH),



Color.FromArgb(122, 138, 172), TipOne | TextFormatFlags.EndEllipsis);

}

 // 「文件类型」这一栏：光有扩展名不好认，给中文名 + 原扩展名

private string MetaFor(FileHit h)

{


string ext = "";


try { ext = Path.GetExtension(h.FullPath); }


catch { ext = ""; }


 string label = h.IsFolder ? "文件夹" : KindLabel(ext);


string tail = "";


try


{



if (h.IsFolder)



{




DirectoryInfo di = new DirectoryInfo(h.FullPath);




if (di.Exists) tail = "  ·  " + di.LastWriteTime.ToString("yyyy-MM-dd HH:mm");



}



else



{




FileInfo fi = new FileInfo(h.FullPath);




if (fi.Exists)





tail = "  ·  " + FormatSize(fi.Length) + "  ·  " + fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm");



}


}


catch { }


 if (!h.IsFolder && ext.Length > 0) return label + "（" + ext.ToLower() + "）" + tail;


return label + tail;

}

 private static string KindLabel(string ext)

{


string e = ext == null ? "" : ext.TrimStart('.').ToLower();


switch (e)


{



case "md": case "markdown": return "Markdown 笔记";



case "txt": return "纯文本";



case "doc": case "docx": return "Word 文档";



case "xls": case "xlsx": case "csv": return "表格";



case "ppt": case "pptx": return "幻灯片";



case "pdf": return "PDF 文档";



case "png": case "jpg": case "jpeg": case "gif": case "webp": case "bmp": case "svg": return "图片";



case "zip": case "rar": case "7z": case "gz": case "tar": return "压缩包";



case "mp4": case "mov": case "avi": case "mkv": case "wmv": return "视频";



case "mp3": case "wav": case "flac": case "m4a": case "aac": return "音频";



case "exe": case "msi": case "bat": case "cmd": return "程序";



case "psd": case "ai": case "sketch": case "fig": return "设计稿";



case "js": case "mjs": case "ts": case "py": case "cs": case "java":



case "json": case "html": case "css": case "ps1": return "代码";



default: return "文件";


}

}

 private static string FormatSize(long n)

{


if (n < 1024) return n + " B";


double k = n / 1024.0;


if (k < 1024) return k.ToString("0.#") + " KB";


double m = k / 1024.0;


if (m < 1024) return m.ToString("0.#") + " MB";


return (m / 1024.0).ToString("0.##") + " GB";

}

 protected override void OnMouseWheel(MouseEventArgs e)

{


base.OnMouseWheel(e);


WheelSel(e.Delta);

}

 protected override void OnMouseDown(MouseEventArgs e)

{


base.OnMouseDown(e);


_hoverRow = -1;


HideTip();


if (e.Button == MouseButtons.Left)

 {

  for (int i = 0; i < _chipRects.Count; i++)

  {

   if (_chipRects[i].Contains(e.X, e.Y))

   {

    Log("attach - " + _atts[i].Name);

    _atts.RemoveAt(i);

    _chipRects.Clear();

    ApplyHeight();

    Invalidate();

    if (tb != null) tb.Focus();

    return;

   }

  }

 }

 if (!Visible || !IsFileMode() || e.Button != MouseButtons.Left) return;


int idx = RowIndexAt(e.Y);


if (idx < 0) return;


if (idx == _sel)


{



// 再点一次已选中的那条 = 打开（和列表类软件的直觉一致）



OpenSelected((Control.ModifierKeys & Keys.Control) == Keys.Control);



return;


}


_sel = idx;


EnsureVisible();


Refresh();

}

 protected override void OnMouseDoubleClick(MouseEventArgs e)

{


base.OnMouseDoubleClick(e);


if (!Visible || !IsFileMode() || e.Button != MouseButtons.Left) return;


int idx = RowIndexAt(e.Y);


if (idx < 0) return;


_sel = idx;


EnsureVisible();


OpenSelected((Control.ModifierKeys & Keys.Control) == Keys.Control);

}

 private void OpenSelected(bool reveal)

{


if (_sel < 0 || _sel >= _hits.Count) return;


FileHit h = _hits[_sel];


 Log((reveal ? "DoSend -> reveal  " : "DoSend -> open file  ") + h.FullPath);


 Hide();


ResetInput();


_hits.Clear();


_sel = 0;


_top = 0;


ApplyHeight();


 if (reveal)


{



try



{




System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + h.FullPath + "\"");



}



catch (Exception ex) { Log("reveal FAILED: " + ex.Message); }


}


else


{



OpenUrl(h.FullPath, "file");


}

}

 // 从查询里挑出真正该高亮的那一段：跳过 ext: / path: / dm: 这类筛选器，

// 剩下的词里取最长的（等长取靠后的）。没有内容词就返回空。

private static string HighlightToken(string q)

{


if (string.IsNullOrEmpty(q)) return "";


string best = "";


string[] parts = q.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);


for (int i = 0; i < parts.Length; i++)


{



string p = parts[i];



if (p.IndexOf(':') >= 0) continue;



if (p.Length >= best.Length) best = p;


}


return best;

}

 // ── 文字渲染 ──

// 默认走 GDI（TextRenderer）：它做网格对齐，竖笔吸附到整像素，

// 这是资源管理器/开始菜单那种锐利感；GDI+ 的 DrawString 不做对齐，

// 笔画落在半像素上，深色底上就是「糊 + 脏」。

// _useGdi=false 只在离屏对比（--render）里用，正常使用永远为 true。

private const TextFormatFlags F_PLAIN =


TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

 private bool _useGdi = true;

 private Size Measure(Graphics g, string s, Font f)

{


if (string.IsNullOrEmpty(s)) return new Size(0, 0);


if (_useGdi) return TextRenderer.MeasureText(g, s, f, new Size(12000, 2000), F_PLAIN);


SizeF sz = g.MeasureString(s, f);


return new Size((int)Math.Ceiling(sz.Width), (int)Math.Ceiling(sz.Height));

}

 private void DrawText(Graphics g, string s, Font f, Color c, float x, float y)

{


if (string.IsNullOrEmpty(s)) return;


if (_useGdi)


{



TextRenderer.DrawText(g, s, f,




new Point((int)Math.Round(x), (int)Math.Round(y)), c, F_PLAIN);


}


else


{



using (SolidBrush b = new SolidBrush(c))




g.DrawString(s, f, b, x, y);


}

}

 // 按宽度截断并加省略号（二分测量，避免长名字反复量）

private string FitText(Graphics g, string s, Font f, float maxW)

{


if (s == null || s.Length == 0) return "";


if (maxW <= 0) return "…";


if (Measure(g, s, f).Width <= maxW) return s;


 int lo = 1, hi = s.Length - 1, best = 0;


while (lo <= hi)


{



int mid = (lo + hi) / 2;



string t = s.Substring(0, mid) + "…";



if (Measure(g, t, f).Width <= maxW) { best = mid; lo = mid + 1; }



else hi = mid - 1;


}


return best > 0 ? s.Substring(0, best) + "…" : "…";

}

 private void DrawHitRow(Graphics g, FileHit h, int x, int y, int rowH)

{


int box = S(18);


int iy = y + (rowH - box) / 2;


DrawTypeIcon(g, h, x, iy, box);


 // 行内文字垂直居中（以前是死贴顶部，跟居中的图标对不上，


// 一屏四条扫下来就是「歪歪扭扭」的观感）


if (_nameLineH < 0) _nameLineH = Measure(g, "国Ag", _fRow).Height;


if (_pathLineH < 0) _pathLineH = Measure(g, "国Ag", _fSmall).Height;


float ny = y + (rowH - _nameLineH) / 2f;


float py = ny + (_nameLineH - _pathLineH) / 2f;


 float nl = x + S(28);


float right = Width - S(18);


 // 路径固定占右边一列，名字永远不许越界 —— 两列各守各的边界，


// 扫的时候是一条直线，不会因为名字长短而左右乱窜。


// 184 这个宽度是称过的：文件名是主信息，多让 50px 出来能多显示三四个汉字；


// 路径本来就会被中间省略，留够「盘符 + 末级目录」就够认路了。


float pathW = S(184);


float pathLeft = right - pathW;


float maxNameW = pathLeft - S(22) - nl;


if (maxNameW < S(90)) maxNameW = S(90);


 // 文件名：先按可用宽度截断，再在截断后的串上做高亮，


// 否则长文件名会一直画到右侧路径上去，两个叠在一起看不清。


string name = FitText(g, h.Name, _fRow, maxNameW);


 // Everything 语法（ext:pdf 报告）里整串不会出现在文件名上，


// 所以只拿「内容词」去高亮，否则一用语法高亮就全灭。


string hl = HighlightToken(_query);


int pos = -1;


if (hl.Length > 0 && hl.Length <= name.Length)



pos = name.IndexOf(hl, StringComparison.OrdinalIgnoreCase);


 Color cName = Color.FromArgb(230, 238, 248);


Color cHit = Color.FromArgb(130, 200, 255);


 float cx = nl;


if (pos >= 0)


{



string pre = name.Substring(0, pos);



string hit = name.Substring(pos, hl.Length);



string post = name.Substring(pos + hl.Length);



 if (pre.Length > 0)



{




DrawText(g, pre, _fRow, cName, cx, ny);




cx += Measure(g, pre, _fRow).Width;



}



DrawText(g, hit, _fRow, cHit, cx, ny);



cx += Measure(g, hit, _fRow).Width;



if (post.Length > 0) DrawText(g, post, _fRow, cName, cx, ny);


}


else


{



DrawText(g, name, _fRow, cName, cx, ny);


}


 // 右侧灰字：所在目录


string parent = "";


int cut = h.FullPath.LastIndexOf('\\');


if (cut > 0) parent = h.FullPath.Substring(0, cut);


 if (parent.Length > 0 && pathW > S(40))


{



Rectangle rp = new Rectangle((int)pathLeft, (int)py, (int)pathW, (int)_pathLineH + S(2));



if (_useGdi)



{




// PathEllipsis 是 GDI 自带的「中间省略」，跟资源管理器一个行为




TextRenderer.DrawText(g, parent, _fSmall, rp, Color.FromArgb(102, 116, 136),





TextFormatFlags.Right | TextFormatFlags.PathEllipsis | TextFormatFlags.NoPrefix





| TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);



}



else



{




using (SolidBrush pb = new SolidBrush(Color.FromArgb(102, 116, 136)))




using (StringFormat sf2 = new StringFormat())




{





sf2.Alignment = StringAlignment.Far;





sf2.LineAlignment = StringAlignment.Near;





sf2.Trimming = StringTrimming.EllipsisPath;





sf2.FormatFlags |= StringFormatFlags.NoWrap;





g.DrawString(parent, _fSmall, pb, rp, sf2);




}



}


}

}

 // ── 类型图标的绘制 ──

// 24×24 单位网格，按 box 等比放大；所有尺寸都是矢量，任意 DPI 都清楚。

private void DrawTypeIcon(Graphics g, FileHit h, int x, int y, int box)

{


float u = box / 24f;


if (h.IsFolder) DrawFolderIcon(g, x, y, box, u);


else DrawDocIcon(g, x, y, box, u, KindOf(h.Name, false));

}

 private void DrawDocIcon(Graphics g, int x, int y, int box, float u, FileKind k)

{


using (GraphicsPath p = new GraphicsPath())


{



// 轮廓：(6,2) → (14,2)【折角起点】→ (20,8)【折角终点】→ 右下 → 左下 → 左上



float x0 = x + 4 * u, x1 = x + 20 * u;



float y0 = y + 2 * u, y1 = y + 22 * u;



float r = 2 * u;



float fx = x + 14 * u, fy = y + 8 * u;



 p.AddLine(x0 + r, y0, fx, y0);



p.AddLine(fx, y0, x1, fy);



p.AddLine(x1, fy, x1, y1 - r);



p.AddArc(x1 - 2 * r, y1 - 2 * r, 2 * r, 2 * r, 0, 90);



p.AddLine(x1 - r, y1, x0 + r, y1);



p.AddArc(x0, y1 - 2 * r, 2 * r, 2 * r, 90, 90);



p.AddLine(x0, y1 - r, x0, y0 + r);



p.AddArc(x0, y0, 2 * r, 2 * r, 180, 90);



p.CloseFigure();



 using (SolidBrush b = new SolidBrush(k.Fill))




g.FillPath(b, p);


}


 // 折角：提亮的三角，制造「纸角翻起来」的层次


PointF[] fold = new PointF[3];


fold[0] = new PointF(x + 14 * u, y + 2 * u);


fold[1] = new PointF(x + 20 * u, y + 8 * u);


fold[2] = new PointF(x + 14 * u, y + 8 * u);


using (SolidBrush fb = new SolidBrush(k.Fold))



g.FillPolygon(fb, fold);


 if (k.Glyph.Length > 0) DrawGlyph(g, k.Glyph, x, y, u, box);

}

 private void DrawFolderIcon(Graphics g, int x, int y, int box, float u)

{


float r = 2 * u;


float L = x + 2 * u, R = x + 22 * u;


float T = y + 4 * u, B = y + 21 * u;


float bodyTop = y + 6 * u;


 // 后层（含标签口），稍暗


using (GraphicsPath p = new GraphicsPath())


{



p.AddArc(L, bodyTop, 2 * r, 2 * r, 180, 90);

   // (2,6) → (4,4)



p.AddLine(L + 2 * u, T, x + 9 * u, T);



 // 顶边



p.AddLine(x + 9 * u, T, x + 11 * u, bodyTop);

  // 标签口斜坡



p.AddLine(x + 11 * u, bodyTop, R - 2 * u, bodyTop);



p.AddArc(R - 2 * r, bodyTop, 2 * r, 2 * r, 270, 90);    // (20,6) → (22,8)



p.AddLine(R, y + 8 * u, R, B - 2 * u);



p.AddArc(R - 2 * r, B - 2 * r, 2 * r, 2 * r, 0, 90);



p.AddLine(R - 2 * u, B, L + 2 * u, B);



p.AddArc(L, B - 2 * r, 2 * r, 2 * r, 90, 90);



p.CloseFigure();



 using (SolidBrush b = new SolidBrush(Color.FromArgb(0xD8, 0xA4, 0x32)))




g.FillPath(b, p);


}


 // 前面板：提亮，靠上下两层双色把「文件夹」这个形状讲清楚


using (GraphicsPath p = new GraphicsPath())


{



float ft = y + 8 * u;



p.AddLine(L + r, ft, R - r, ft);



p.AddArc(R - 2 * r, ft, 2 * r, 2 * r, 270, 90);



p.AddLine(R, ft + r, R, B - r);



p.AddArc(R - 2 * r, B - 2 * r, 2 * r, 2 * r, 0, 90);



p.AddLine(R - r, B, L + r, B);



p.AddArc(L, B - 2 * r, 2 * r, 2 * r, 90, 90);



p.AddLine(L, B - r, L, ft + r);



p.AddArc(L, ft, 2 * r, 2 * r, 180, 90);



p.CloseFigure();



 using (SolidBrush b = new SolidBrush(Color.FromArgb(0xF0, 0xC2, 0x58)))




g.FillPath(b, p);


}

}

 private void DrawGlyph(Graphics g, string glyph, int x, int y, float u, int box)

{


Font f = GlyphFont(g, glyph, box);


Rectangle rc = new Rectangle((int)(x + 1 * u), (int)(y + 8 * u), (int)(21 * u), (int)(14 * u));


TextRenderer.DrawText(g, glyph, f, rc, Color.White,



TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter



| TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

}

 // 图标里的类型字：字号由图标实际大小反推（宽、高都得装得下），

// 每个字只算一次，之后走缓存 —— 否则 M 会顶满整张纸、PDF 会被裁掉。

private Font GlyphFont(Graphics g, string glyph, int box)

{


Font cached;


if (_glyphFonts.TryGetValue(glyph, out cached)) return cached;


 float maxW = box * 0.62f;


float maxH = box * 0.40f;


float px = box * 0.34f;


 for (int i = 0; i < 4; i++)


{



float next = px;



using (Font probe = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel))



{




Size sz = Measure(g, glyph, probe);




if (sz.Width < 1 || sz.Height < 1) break;




next = Math.Min(px * maxW / sz.Width, px * maxH / sz.Height);



}



if (next < 3f) next = 3f;



if (Math.Abs(next - px) < 0.5f) { px = next; break; }



px = next;


}


 Font made = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel);


_glyphFonts[glyph] = made;


return made;

}

 // ProcessCmdKey 比 KeyDown 更早拿到按键，

// 能绕开多行 TextBox 对回车的原生处理和输入法的候选框占用。

protected override bool ProcessCmdKey(ref Message msg, Keys keyData)

{


Keys k = keyData & Keys.KeyCode;


bool shift = (keyData & Keys.Shift) == Keys.Shift;


bool ctrl = (keyData & Keys.Control) == Keys.Control;


 // 文件搜索模式：方向键和回车归我们管


if (Visible && IsFileMode())


{



if (k == Keys.Down) { MoveSel(1); return true; }



if (k == Keys.Up) { MoveSel(-1); return true; }



if (k == Keys.Enter)



{




if (_hits.Count > 0) OpenSelected(ctrl);




return true;   // 没有结果时也不许落到「发送」去



}


}


 if (TryHandleOverlayKey(k, shift, "ProcessCmdKey")) return true;


return base.ProcessCmdKey(ref msg, keyData);

}

 protected override void WndProc(ref Message m)

{


if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)


{



Log("HOTKEY  visible=" + Visible + "  " + Width + "x" + Height + "  at " + Left + "," + Top);



Toggle();



return;


}


 // 诊断用：确认按键究竟有没有进到窗口


if (Visible && (m.Msg == 0x0100 || m.Msg == 0x0104))


{



int vk = m.WParam.ToInt32();



if (vk == 0x0D || vk == 0x1B)




Log("WM_KEY " + (vk == 0x0D ? "ENTER" : "ESC") + "  msg=0x" + m.Msg.ToString("X"));


}


 base.WndProc(ref m);

}

 protected override void OnResize(EventArgs e)

{


base.OnResize(e);


UpdateRegion();

}

 private void UpdateRegion()

{


if (Width <= 0 || Height <= 0) return;


using (GraphicsPath p = Round(new Rectangle(0, 0, Width, Height), S(16)))


{



Region old = Region;



Region = new Region(p);



if (old != null) old.Dispose();


}

}

 private static GraphicsPath Round(Rectangle r, int radius)

{


GraphicsPath p = new GraphicsPath();


int d = radius * 2;


if (d > r.Width) d = r.Width;


if (d > r.Height) d = r.Height;


p.AddArc(r.X, r.Y, d, d, 180, 90);


p.AddArc(r.Right - d, r.Y, d, d, 270, 90);


p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);


p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);


p.CloseFigure();


return p;

}

 protected override void OnPaint(PaintEventArgs e)

{


Graphics g = e.Graphics;


g.SmoothingMode = SmoothingMode.AntiAlias;


// GDI+ 默认是灰度抗锯齿，和系统其它文字（ClearType）比会发虚。


// 200% 缩放下这个差异特别明显，看起来像「低分辨率」。


g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;


 Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);


using (GraphicsPath path = Round(r, S(16)))


{



using (SolidBrush bg = new SolidBrush(C_BG))




g.FillPath(bg, path);



using (Pen pen = new Pen(C_LINE, SF(1.1f)))




g.DrawPath(pen, path);


}


 // 左侧标记


using (SolidBrush b = new SolidBrush(C_ACCENT))



g.FillEllipse(b, S(18), S(20), S(22), S(22));


TextRenderer.DrawText(g, "Q", _fBadge, new Rectangle(S(18), S(20), S(22), S(22)),



Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter



| TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);


 // 输入行下方：文件搜索结果


int rows = Math.Min(_hits.Count, MAX_ROWS);


if (rows > 0)


{



int listTop = S(BASE_H) - S(6);



int rowH = S(ROW_H);



 using (Pen pen = new Pen(Color.FromArgb(38, 48, 62), SF(1f)))




g.DrawLine(pen, S(14), listTop, Width - S(14), listTop);



 // 从 _top 开始取窗口，选中项随滚动跑出视野也看得见



int last = Math.Min(_hits.Count, _top + MAX_ROWS);



for (int i = _top; i < last; i++)



{




int slot = i - _top;




int y = listTop + S(4) + slot * rowH;




if (i == _sel)




{





using (SolidBrush sel = new SolidBrush(Color.FromArgb(19, 42, 69)))






g.FillRectangle(sel, S(8), y, Width - S(16), rowH - S(2));




}




DrawHitRow(g, _hits[i], S(16), y, rowH);



}



 // 结果多于窗口高度时，右侧给一条细滚动条



if (_hits.Count > MAX_ROWS) DrawListScrollbar(g, listTop + S(6), rows * rowH - S(10));


}


 // 底部左侧：投递去向 / 桥的连接状态


string left = "";


Color leftColor = C_DIM;


 string sUrl = null, sLabel = null;


string peekLabel = null;


bool fileMode = IsFileMode();


bool isSearch = !fileMode && !placeholder && Cfg.TryResolveSearch(tb.Text, out sUrl, out sLabel);


bool isPeek = !fileMode && !isSearch && !placeholder && Cfg.TryPeekAlias(tb.Text, out peekLabel);


 if (fileMode)


{



if (_searching)




left = "文件搜索 · 搜索中…";



else if (_query.Length == 0)




left = "文件搜索 · 输入关键词";



else if (_hits.Count == 0)




left = _searchNote.Length > 0





 ? "文件搜索 · " + _searchNote





 : "文件搜索 · 「" + _query + "」没有匹配（只匹配文件名，不搜正文）";



else



{




left = "文件搜索 · " + _hits.Count + " 条" + (_lastMs > 0 ? " · " + _lastMs + "ms" : "");




// 结果多到一屏放不下时，明确告诉他在第几条 / 一共几条




if (_hits.Count > MAX_ROWS) left += " · 第 " + (_sel + 1) + " / " + _hits.Count + " 条";



}



 leftColor = Color.FromArgb(124, 170, 255);


}


else if (isSearch)


{



left = "→ " + sLabel + " 搜索 · 回车直接打开浏览器";



leftColor = Color.FromArgb(124, 92, 255);


}


else if (isPeek)


{



left = "→ " + peekLabel + " · 空格后输入关键词";



leftColor = Color.FromArgb(124, 92, 255);


}


else if (Cfg.Mode == "new" || Cfg.Mode == "current")


{



if (Bridge.Connected)



{




left = "→ DSH " + (Cfg.Mode == "new" ? "新会话" : "当前会话") + " · 扩展已连接";




leftColor = Color.FromArgb(96, 178, 120);



}



else



{




left = "⚠ 扩展未连接，内容先排队";




if (Bridge.Pending > 0) left += "（积压 " + Bridge.Pending + " 条）";




leftColor = Color.FromArgb(198, 148, 74);



}


}


else if (targetTitle.Length > 0)


{



string t = targetTitle;



if (t.Length > 34) t = t.Substring(0, 34) + "…";



left = "→ " + t;


}


 // 底部那一行：左状态、右按键提示，统一按实测行高贴底


// （悬停详情占了最下面一块时，这一行要跟着往上让，否则会被压住）


if (_hintLineH < 0) _hintLineH = Measure(g, "国Ag", _fHint).Height;


int footY = Height - _tipH - S(9) - (int)_hintLineH;



  // 底栏状态：把「扩展没连上 / 还有东西在排队」直接写在脸上。
 // 以前这两种情况看起来和正常一模一样，按完回车全靠猜。
 if (!IsFileMode())
 {
  if (!Bridge.Connected)
  {
   left = (left.Length > 0 ? left + "   " : "") + "· 扩展未连接，内容会排队";
   leftColor = Color.FromArgb(216, 140, 64);
  }
  else if (Bridge.Pending > 0)
  {
   left = (left.Length > 0 ? left + "   " : "") + "· 待投递 " + Bridge.Pending;
   leftColor = Color.FromArgb(216, 140, 64);
  }
 }

 DrawAtts(g, footY);


 if (left.Length > 0)



DrawText(g, left, _fHint, leftColor, S(54), footY);


 // 底部右侧：操作提示（右对齐到框内）


// 文件模式一条都没搜到时，↑↓/Enter 这些提示没有意义 —— 让位给左边的说明，


// 否则「只匹配文件名」这句会被右边那串按键提示压住看不见。


bool noHits = fileMode && !_searching && _query.Length > 0 && _hits.Count == 0;


string hint = IsFileMode()



? (noHits ? "Esc 关闭" : "↑↓ 选择    Enter 打开    Ctrl+Enter 定位    Esc 关闭")



: hintText;


Rectangle rh = new Rectangle(S(54), footY, Width - S(54) - S(20), (int)_hintLineH + S(2));


TextRenderer.DrawText(g, hint, _fHint, rh, Color.FromArgb(108, 118, 140),



TextFormatFlags.Right | TextFormatFlags.NoPrefix



| TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.SingleLine);


 // 最下面一块：悬停详情（画在同一块画布上，不开第二个窗口）


if (_tipRow >= 0 && _tipH > 0) DrawTip(g);

}

 // 列表右侧那条细滚动条：长的滑块 = 在总结果里的位置，短的就说明后面还有很多

private void DrawListScrollbar(Graphics g, int top, int trackH)

{


int total = _hits.Count;


if (total <= MAX_ROWS || trackH <= 0) return;


 int sbW = S(3);


int sbX = Width - S(7);


 using (SolidBrush b = new SolidBrush(Color.FromArgb(26, 36, 48)))



g.FillRectangle(b, sbX, top, sbW, trackH);


 int th = (int)((float)trackH * MAX_ROWS / total);


int minTh = S(10);


if (th < minTh) th = minTh;


if (th > trackH) th = trackH;


 int maxTop = total - MAX_ROWS;


int ty = top;


if (maxTop > 0)


{



int span = trackH - th;



if (span < 0) span = 0;



ty = top + (int)((float)span * _top / maxTop);


}


 using (SolidBrush b = new SolidBrush(Color.FromArgb(80, 106, 136)))



g.FillRectangle(b, sbX, ty, sbW, th);

}

 public void Toggle()

{


if (Visible)


{



CancelAndHide();



return;


}


ShowInput();

}

 private void ShowInput()

{


// 记录“按热键时正在用的那个窗口”——这就是内容要去的地方


targetWindow = Native.GetForegroundWindow();


StringBuilder sb = new StringBuilder(512);


if (targetWindow != IntPtr.Zero) Native.GetWindowText(targetWindow, sb, sb.Capacity);


targetTitle = sb.ToString();


Log("ShowInput  target=" + targetWindow + "  title=" + targetTitle);



 Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;


Left = wa.Left + (wa.Width - Width) / 2;


// 输入行本身落在屏幕正中；结果列表从它下面往下长，不会把输入行顶动


Top = wa.Top + (wa.Height - S(BASE_H)) / 2;


 Show();


_shownAt = DateTime.Now;


Native.ForceForeground(Handle);


Activate();


 // 确保输入框真的拿到焦点——“按回车没反应”就是因为它没焦点


if (!tb.Focused)


{



ActiveControl = tb;



tb.Select();


}


// Select() 有时不够，直接给原生句柄设焦点


Native.SetFocus(tb.Handle);


 // 不清 _hits / _query：收起时不丢状态，再呼出就是刚才那一屏


// （含选中位置）。只有「执行」或「手动清空」才真的丢。


EnsureVisible();


ApplyHeight();


 tb.SelectAll();


Invalidate();

}

 // 收起但不丢内容：再按 Alt+Q 还是刚才那一屏，连选中位置都在。

// （「执行」和手动清空才走 ResetInput）

private void CancelAndHide()

{


if (_watchTimer != null) _watchTimer.Stop();


Hide();

}

 // 浮层一露头就开始盯前台窗口，一收起就停

protected override void OnVisibleChanged(EventArgs e)

{


base.OnVisibleChanged(e);


if (!Visible)


{



// 详情条是独立窗口，主浮层收起时必须一起收，否则会孤零零挂在桌面上



_hoverRow = -1;



if (_hoverTimer != null) _hoverTimer.Stop();



HideTip();


}


if (_watchTimer == null) return;


if (Visible) _watchTimer.Start();


else _watchTimer.Stop();

}

 private void DoSend()

{


if (IsFileMode()) return;   // 文件搜索模式下回车归「打开文件」，不该走发送


 string text = tb.Text;


if (text == null) text = "";


 // 不靠 placeholder 标志判断：焦点没按正常路径进来时那个标志可能是旧的。


// 只认「框里的内容不是占位符、也不是空」。


if (text.Trim().Length == 0 || (placeholder && text.Trim() == phText.Trim()))


{



Log("DoSend SKIP  placeholder=" + placeholder + "  len=" + text.Length + "  text=[" + text + "]");



return;


}


 Log("DoSend  placeholder=" + placeholder + "  len=" + text.Length);


 // 搜索指令优先：别名 + 空格 + 关键词 → 拼网址，交给浏览器打开。


// 想强行把这段字发进 DSH，在最前面加一个反斜杠 \ 即可。


string body = text.Trim();


bool forceSend = false;


if (body.StartsWith("\\"))


{



body = body.Substring(1).Trim();



forceSend = true;


}


 if (!forceSend)


{



string sUrl, sLabel;



if (Cfg.TryResolveSearch(body, out sUrl, out sLabel))



{




Log("DoSend -> search  " + sLabel + "  " + sUrl);




Hide();




ResetInput();




OpenUrl(sUrl, "search");




return;



}


}


 if (forceSend) text = body;


 // 走扩展通道：内容交给本地桥，由浏览器扩展塞进 DSH。


// 不抢焦点、不切窗口、不动剪贴板 —— 眼睛看着的窗口纹丝不动。


if (Cfg.Mode == "new" || Cfg.Mode == "current")


{



int pending = Bridge.Push(text, Cfg.Mode, new List<QAtt>(_atts));
                // 网页版 DSH 没开（扩展没连上）时先把页面打开：内容已经在桥里排着，
                // 页面一起来扩展就会来取走 —— 不用自己去找标签页。
                if (!Bridge.Connected) { Log("dsh closed -> opening " + DshUrl); OpenUrl(DshUrl, "open-dsh"); }



Log("DoSend -> bridge  mode=" + Cfg.Mode + "  len=" + text.Length + "  pending=" + pending);



Hide();



ResetInput();



return;


}


 IntPtr target = targetWindow;


int pasteDelay = Cfg.PasteDelay;


int focusWait = Math.Max(400, Cfg.FocusDelay * 3);


 Hide();


 // ① 焦点还回去，并等它真的拿到焦点（不是睡固定时间）


Native.ForceForeground(target);


bool got = Native.WaitForeground(target, focusWait);


if (!got)


{



Native.ForceForeground(target);



got = Native.WaitForeground(target, focusWait);


}


 if (!got)


{



// 抢不到焦点就不发——否则内容会打进别的窗口



Log("DoSend FAILED: cannot focus target");



hintText = "⚠ 没抢到目标窗口焦点，再按一次 Ctrl+Enter 重试，或 Esc 取消";



Show();



Invalidate();



return;


}


Log("DoSend focus ok");


 // ①.5 先新建一个对话（可选）——点 DSH 的「新会话」按钮


if (Cfg.NewSession)


{



Native.ClickAt(Cfg.NewSessionX, Cfg.NewSessionY);



Thread.Sleep(Cfg.NewSessionWait);


}


 // ② 内容搬运


if (!SetClipboard(text))


{



hintText = "⚠ 写入剪贴板失败，请重试";



Show();



Invalidate();



return;


}


Thread.Sleep(40);


Native.SendCtrlV();


 // ③ 发送


Thread.Sleep(pasteDelay);


Native.SendKey(Native.VK_RETURN);


Log("DoSend completed, text_len=" + text.Length);


 ResetInput();

}

 private void ResetInput()

{


hintText = "Enter 发送   Shift+Enter 换行   Esc 取消";


placeholder = true;


ClearHits();



if (_atts.Count > 0) { _atts.Clear(); _chipRects.Clear(); ApplyHeight(); }


tb.Text = phText;


tb.ForeColor = C_DIM;

}

 // 交给系统默认浏览器打开。DoSend 和自检接口共用这一条路径，

// 免得「测的是一套、用的是另一套」。

internal static bool OpenUrl(string url, string tag)

{


if (string.IsNullOrEmpty(url)) return false;


try


{



System.Diagnostics.Process.Start(url);



Log("open " + tag + "  " + url);



return true;


}


catch (Exception ex)


{



Log("open " + tag + " FAILED: " + ex.Message);



return false;


}

}

 private static bool SetClipboard(string text)

{


for (int i = 0; i < 6; i++)


{



try



{




Clipboard.SetText(text);




return true;



}



catch



{




Thread.Sleep(35);



}


}


return false;

}

 protected override void Dispose(bool disposing)

{


if (disposing)


{



try { Native.UnregisterHotKey(Handle, HOTKEY_ID); }



catch { }



if (_fBadge != null) { _fBadge.Dispose(); _fBadge = null; }



if (_fHint != null) { _fHint.Dispose(); _fHint = null; }



foreach (Font gf in _glyphFonts.Values)



{




if (gf != null) gf.Dispose();



}



_glyphFonts.Clear();



if (_fRow != null) { _fRow.Dispose(); _fRow = null; }



if (_fSmall != null) { _fSmall.Dispose(); _fSmall = null; }



if (_debounce != null) { _debounce.Stop(); _debounce.Dispose(); _debounce = null; }



if (_watchTimer != null) { _watchTimer.Stop(); _watchTimer.Dispose(); _watchTimer = null; }



if (_hoverTimer != null) { _hoverTimer.Stop(); _hoverTimer.Dispose(); _hoverTimer = null; }



if (_ctx != null) { _ctx.Dispose(); _ctx = null; }


}


base.Dispose(disposing);

}
}
 // ───────────────────────── 托盘 ─────────────────────────
internal class TrayApp : ApplicationContext
{

private InputForm form;

private NotifyIcon tray;

private Icon trayIcon;

 public TrayApp()

{


form = new InputForm();


 // 托盘图标：内嵌的多尺寸 ico，按系统托盘的实际尺寸取最合适的那一档


trayIcon = InputForm.LoadAppIcon(SystemInformation.SmallIconSize.Width);


form.Icon = InputForm.LoadAppIcon(32);


 tray = new NotifyIcon();


tray.Icon = trayIcon;


tray.Text = "QuickInput  (" + InputForm.Cfg.HotkeyText + ")";


tray.Visible = true;


 ContextMenuStrip menu = new ContextMenuStrip();


menu.Items.Add("打开输入框  (" + InputForm.Cfg.HotkeyText + ")", null, delegate { form.Toggle(); });


menu.Items.Add("打开配置文件", null, delegate


{



try { System.Diagnostics.Process.Start("notepad.exe", InputForm.Cfg.ConfigPath); }



catch { }


});


menu.Items.Add(new ToolStripSeparator());


menu.Items.Add("退出", null, delegate { ExitThread(); });


tray.ContextMenuStrip = menu;


tray.DoubleClick += delegate { form.Toggle(); };


 if (!form.HotkeyOk)


{



MessageBox.Show(




"全局热键 " + InputForm.Cfg.HotkeyText + " 注册失败。\n\n" +




"通常是它已被别的程序占用（比如 Quicker 还在跑）。\n" +




"可以右键托盘图标 → 打开配置文件，把 Hotkey 改成别的组合，然后重启本程序。",




"QuickInput",




MessageBoxButtons.OK,




MessageBoxIcon.Warning);


}

}

 protected override void ExitThreadCore()

{


if (tray != null)


{



tray.Visible = false;



tray.Dispose();


}


if (trayIcon != null) { trayIcon.Dispose(); trayIcon = null; }


if (form != null) form.Dispose();


base.ExitThreadCore();

}
}
 // ───────────────────────── 入口 ─────────────────────────
internal static class Program
{

[STAThread]

private static void Main(string[] args)

{


// 离屏渲染排版对比图：不建窗口、不注册热键、不查单实例（放在最前面，


// 这样即使 QuickInput 正在运行也能跑，不会弹「已经在运行」的框打扰用户）


if (args != null && args.Length >= 2 && args[0] == "--render")


{



try { Native.SetProcessDPIAware(); } catch { }



InputForm.RenderSheet(args[1]);



return;


}


 // 离屏把浮层各状态出成图片（文档/演示用），同样不建窗口


if (args != null && args.Length >= 2 && args[0] == "--shot")


{



try { Native.SetProcessDPIAware(); } catch { }



InputForm.Cfg = Settings.Load();



InputForm.ShotStates(args[1]);



return;


}


  bool createdNew;


using (Mutex mutex = new Mutex(true, "QuickInput_SingleInstance_9F2A", out createdNew))


{



if (!createdNew)



{




MessageBox.Show("QuickInput 已经在运行了，看任务栏右下角的托盘图标。",





"QuickInput", MessageBoxButtons.OK, MessageBoxIcon.Information);




return;



}



 try { Native.SetProcessDPIAware(); }



catch { }



 InputForm.Cfg = Settings.Load();



 // 给浏览器扩展取件用的本地端口（只监听 127.0.0.1）



Bridge.Start();



 // 预热 Everything：第一次 IPC 握手慢，先跑一次，别让用户等



try { Everything.Probe(); }



catch (Exception ex) { InputForm.Log("everything probe threw: " + ex.Message); }



 Application.EnableVisualStyles();



Application.SetCompatibleTextRenderingDefault(false);



Application.Run(new TrayApp());


}

}
} }
