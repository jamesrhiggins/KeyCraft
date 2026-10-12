using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;

namespace KeyCraft {
 static class Program {
  [STAThread] static void Main(string[] args) {
   if(args.Length>0 && args[0]=="--devices") {try{Console.WriteLine(AppleInput.Refresh(IntPtr.Zero));Console.WriteLine("Apple keyboard detected: "+AppleInput.Present);foreach(var drive in UsbStorage.Discover())Console.WriteLine("USB: "+drive);}finally{AppleInput.Dispose();}return;}
   if (args.Length > 0 && args[0] == "--self-test") { Engine.Test();MonitorBrightness.Test(); return; }
   if(args.Length>0 && args[0]=="--brightness-info"){Console.WriteLine(MonitorBrightness.Diagnose());return;}
   if(args.Length>0 && args[0]=="--brightness-verify"){Console.WriteLine(MonitorBrightness.VerifyHardware());return;}
   if (args.Length > 0 && args[0] == "--hook-test") { using(var engine=new Engine()) {engine.Start();} Console.WriteLine("PASS: Windows keyboard hook installed and removed."); return; }
   if(args.Length>0 && args[0]=="--latency-test"){Engine.LatencyTest();return;}
   if (args.Length > 0 && args[0] == "--numpad-on") { Console.WriteLine("Num Lock before: " + Engine.NumLockOn); if(!Engine.EnableNumpad()) throw new Exception("Windows rejected Num Lock input"); System.Threading.Thread.Sleep(150); Console.WriteLine("Num Lock after: " + Engine.NumLockOn); return; }
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   if(args.Length>0 && args[0]=="--ui-check"){using(var window=new MainWindow()){window.Show();window.WindowState=FormWindowState.Maximized;Application.DoEvents();var frame=window.Controls[0];int width=Math.Min(1020,window.ClientSize.Width),height=Math.Min(980,window.ClientSize.Height);if(frame.Width!=width || frame.Height!=height || frame.Left!=(window.ClientSize.Width-width)/2 || frame.Top!=(window.ClientSize.Height-height)/2)throw new Exception("Centered fixed frame failed");Console.WriteLine("PASS: maximized content stays "+frame.Width+" x "+frame.Height+" and centered in "+window.ClientSize.Width+" x "+window.ClientSize.Height+".");var intervals=new List<double>();var watch=Stopwatch.StartNew();double previous=0;using(var clock=new System.Windows.Forms.Timer()){clock.Interval=15;clock.Tick+=(s,e)=>{double now=watch.Elapsed.TotalMilliseconds;intervals.Add(now-previous);previous=now;if(now>=1500){clock.Stop();Application.ExitThread();}};clock.Start();Application.Run();}intervals.Sort();Console.WriteLine("UI timer cadence: median "+intervals[intervals.Count/2].ToString("F1")+" ms, 95th percentile "+intervals[(int)(intervals.Count*.95)].ToString("F1")+" ms.");window.Hide();}return;}
   if(args.Length>1 && args[0]=="--render"){using(var window=new MainWindow()){window.Show();Application.DoEvents();if(args.Length>2)window.PreviewButtons(int.Parse(args[2]));Application.DoEvents();using(var bitmap=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(args[1]);}window.Hide();}return;}
   using (var mutex = new System.Threading.Mutex(false, "Local\\KeyCraftUtility")) {
    if (!mutex.WaitOne(0, false)) { MessageBox.Show("KeyCraft is already running in the system tray."); return; }
    try { Application.Run(new MainWindow()); } finally { mutex.ReleaseMutex(); }
   }
  }
 }
 class Engine : IDisposable {
  public Dictionary<int,int> Map = new Dictionary<int,int>();
  readonly Dictionary<int,int> held = new Dictionary<int,int>();
  public volatile bool Enabled, Media;
  public volatile bool FnFallback=true;volatile bool fallbackHeld;
  readonly object stateLock=new object();Thread inputThread;uint inputThreadId;Exception startupError;Action latencyProbe;
  public Action<int> UtilityAction;
  public const int UtilityBase=0x30000;
  public bool FnActive {get{return AppleInput.FnDown || fallbackHeld;}}
  public void SetActivation(bool ready,bool present,bool paused){bool active=ready && present && !paused;if(Enabled && !active)Pause();Enabled=active;}
  public const int EqualsText=0x1003D,ClearLine=0x20000;
  public Action Emergency;
  IntPtr hook; HookProc callback;
  [StructLayout(LayoutKind.Sequential)]struct NativeMessage{public IntPtr window;public uint message;public UIntPtr wParam;public IntPtr lParam;public uint time;public int x,y;public uint privateValue;}
  delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] struct Key { public uint vk, scan, flags, time; public UIntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] struct KeyboardInput { public ushort vk, scan; public uint flags, time; public UIntPtr extra; }
  [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public KeyboardInput keyboard; [FieldOffset(0)] public MouseInput mouse; }
  [StructLayout(LayoutKind.Sequential)] struct MouseInput { public int x,y; public uint data, flags, time; public UIntPtr extra; }
  [StructLayout(LayoutKind.Sequential)] struct Input { public uint type; public InputUnion data; }
  [DllImport("user32.dll", SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id, HookProc fn, IntPtr module, uint thread);
  [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr h);
  [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
  [DllImport("kernel32.dll", CharSet=CharSet.Auto)] static extern IntPtr GetModuleHandle(string name);
  [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")]static extern bool PeekMessage(out NativeMessage message,IntPtr window,uint min,uint max,uint remove);
  [DllImport("user32.dll",SetLastError=true)]static extern bool PostThreadMessage(uint thread,uint message,UIntPtr wParam,IntPtr lParam);
  [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
  [DllImport("user32.dll")] static extern short GetKeyState(int key);
  public static bool NumLockOn { get {return (GetKeyState(144)&1)!=0;} }
  public static bool SetNumLock(bool on) {return NumLockOn==on || (Emit(144,false) && Emit(144,true));}
  public static bool EnableNumpad() {return SetNumLock(true);}
  [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint n, Input[] input, int size);
  public void Start() {
   if(inputThread!=null)throw new InvalidOperationException("Input thread already started");
   var started=new ManualResetEvent(false);
   inputThread=new Thread(()=>{try{inputThreadId=GetCurrentThreadId();NativeMessage message;PeekMessage(out message,IntPtr.Zero,0,0,0);callback=Handle;hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);if(hook==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();}catch(Exception ex){startupError=ex;}finally{started.Set();}
    if(startupError==null)try{Application.Run();}finally{Pause();if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}}
   });inputThread.IsBackground=true;inputThread.Name="KeyCraft input";inputThread.SetApartmentState(ApartmentState.STA);inputThread.Start();started.WaitOne();started.Dispose();if(startupError!=null)throw startupError;
  }
  public void Configure(Dictionary<int,int> mappings,bool media,bool fn){lock(stateLock){Pause();Map=mappings;Media=media;FnFallback=fn;}}
  public int Translate(int key) {
   int result;if(Map.TryGetValue(key,out result))return result;
   bool fn=AppleInput.FnDown || fallbackHeld;
   if(Media && !fn){if(key>=112 && key<=117)return UtilityBase+(key-112);switch(key){case 118:return 177;case 119:return 179;case 120:return 176;case 121:return 173;case 122:return 174;case 123:return 175;}}
   if(Media && fn){switch(key){case 177:return 118;case 179:return 119;case 176:return 120;case 173:return 121;case 174:return 122;case 175:return 123;}}
   return key;
  }
  public int TranslatePhysical(int vk, uint scan, uint flags) {
   if(vk==146 || (scan==0x59 && (flags&1)==0)) return EqualsText;
   // With Num Lock off, keypad 5 is VK_CLEAR too. Keep that key untouched.
   if(vk==144 || vk==254 || (vk==12 && scan!=0x4c)) return ClearLine;
   return Translate(vk);
  }
  static Input Stroke(int vk,bool up) {
   uint flags=up?2u:0u;
   if(vk==144 || vk==35 || vk==36 || vk==163 || vk==165 || vk==91 || vk==92 || (vk>=166 && vk<=183))flags|=1;
   return new Input{type=1,data=new InputUnion{keyboard=new KeyboardInput{vk=(ushort)vk,flags=flags}}};
  }
  static bool Emit(int vk, bool up) {
   if(vk==EqualsText) {var unicode=new Input{type=1,data=new InputUnion{keyboard=new KeyboardInput{scan=61,flags=4u|(up?2u:0u)}}};return SendInput(1,new[]{unicode},Marshal.SizeOf(typeof(Input)))==1;}
   if(vk==ClearLine){if(up)return true;if(GetAsyncKeyState(16)<0 || GetAsyncKeyState(17)<0 || GetAsyncKeyState(18)<0 || GetAsyncKeyState(91)<0 || GetAsyncKeyState(92)<0)return false;var inputs=new[]{Stroke(36,false),Stroke(36,true),Stroke(16,false),Stroke(35,false),Stroke(35,true),Stroke(16,true),Stroke(8,false),Stroke(8,true)};return SendInput((uint)inputs.Length,inputs,Marshal.SizeOf(typeof(Input)))==inputs.Length;}
   uint flags = up ? 2u : 0u;
   if(vk == 144 || vk == 163 || vk == 165 || vk == 91 || vk == 92 || (vk >= 166 && vk <= 183)) flags |= 1;
   return SendInput(1,new[]{new Input{type=1,data=new InputUnion{keyboard=new KeyboardInput{vk=(ushort)vk,flags=flags}}}},Marshal.SizeOf(typeof(Input))) == 1;
  }
  IntPtr Handle(int code, IntPtr w, IntPtr l) {
   if(code >= 0) {
    var key = (Key)Marshal.PtrToStructure(l,typeof(Key));
    if(latencyProbe!=null && key.extra.ToUInt64()==0x4b434c54){if((key.flags&0x80)==0)latencyProbe();return (IntPtr)1;}
    if((key.flags & 0x10) == 0) {
     // Digits and normal keypad operators never enter the remapping path.
     if(key.vk>=96 && key.vk<=111)return CallNextHookEx(hook,code,w,l);
     lock(stateLock){
     int vk=(int)key.vk; bool up=(key.flags & 0x80)!=0;
     if(vk==163 && FnFallback && Media && (Enabled || fallbackHeld)){fallbackHeld=!up;return (IntPtr)1;}
     if(!up && vk==19 && GetAsyncKeyState(17)<0 && GetAsyncKeyState(18)<0) { Pause(); if(Emergency!=null) Emergency(); return (IntPtr)1; }
     int target;
     int physical=(int)key.scan|(((int)key.flags&1)<<16)|(vk<<17);
     if(held.TryGetValue(physical,out target)) { if(target<UtilityBase && (target!=ClearLine || up))Emit(target,up); if(up) held.Remove(physical); return (IntPtr)1; }
     if(Enabled && !up) { target=vk==124?UtilityBase+6:TranslatePhysical(vk,key.scan,key.flags);if(target>=UtilityBase){held[physical]=target;if(UtilityAction!=null)UtilityAction(target-UtilityBase);return (IntPtr)1;} if(target!=vk && Emit(target,false)) { held[physical]=target; return (IntPtr)1; } }
     }
    }
   }
   return CallNextHookEx(hook,code,w,l);
  }
  public void Pause() {lock(stateLock){Enabled=false;fallbackHeld=false; foreach(var pair in held)if(pair.Value<UtilityBase) Emit(pair.Value,true); held.Clear();}}
  public static void Shortcut(params int[] keys){var list=new List<Input>();foreach(int key in keys)list.Add(Stroke(key,false));for(int i=keys.Length-1;i>=0;i--)list.Add(Stroke(keys[i],true));SendInput((uint)list.Count,list.ToArray(),Marshal.SizeOf(typeof(Input)));}
  public void Dispose() {Pause();if(inputThread!=null && inputThread.IsAlive){PostThreadMessage(inputThreadId,0x12,UIntPtr.Zero,IntPtr.Zero);if(Thread.CurrentThread!=inputThread)inputThread.Join(2000);}}
  public static void LatencyTest(){using(var received=new AutoResetEvent(false))using(var engine=new Engine()){long sent=0;var times=new List<double>();engine.latencyProbe=()=>{times.Add((Stopwatch.GetTimestamp()-Interlocked.Read(ref sent))*1000d/Stopwatch.Frequency);received.Set();};engine.Start();for(int i=0;i<30;i++){Interlocked.Exchange(ref sent,Stopwatch.GetTimestamp());var down=Stroke(96,false);down.data.keyboard.extra=new UIntPtr(0x4b434c54u);var up=Stroke(96,true);up.data.keyboard.extra=new UIntPtr(0x4b434c54u);if(SendInput(2,new[]{down,up},Marshal.SizeOf(typeof(Input)))!=2 || !received.WaitOne(1000))throw new Exception("Input stalled without UI message pumping");}times.Sort();Console.WriteLine("PASS: 30 hook events while caller never pumps UI messages. Median "+times[times.Count/2].ToString("F3")+" ms; max "+times[times.Count-1].ToString("F3")+" ms. Test events were consumed, not typed.");}}
  public static void Test() {
   var e=new Engine(); if(e.Translate(65)!=65) throw new Exception("Identity");
   e.Map[91]=162; e.Map[162]=91; if(e.Translate(91)!=162 || e.Translate(162)!=91) throw new Exception("Simultaneous swap");
   e.Media=true; int[] expected={177,179,176,173,174,175}; for(int i=0;i<6;i++) if(e.Translate(118+i)!=expected[i]) throw new Exception("Media");
   e.Map[118]=65; if(e.Translate(118)!=65) throw new Exception("Explicit mapping priority");
   e.Map.Remove(118);AppleInput.FnDown=true;if(e.Translate(118)!=118 || e.Translate(179)!=119)throw new Exception("Fn function row");AppleInput.FnDown=false;
   if(e.Translate(112)!=UtilityBase)throw new Exception("Default utility row");
   e.SetActivation(true,true,false);if(!e.Enabled)throw new Exception("Apple activation");e.SetActivation(true,false,false);if(e.Enabled)throw new Exception("Disconnected keyboard");e.SetActivation(true,true,true);if(e.Enabled)throw new Exception("Manual pause");e.SetActivation(false,true,false);if(e.Enabled)throw new Exception("Hook unavailable");
   Console.WriteLine("PASS: Fn row, utility defaults, Apple presence gating, pause, hook failure gating.");
   if(e.TranslatePhysical(146,0,0)!=EqualsText || e.TranslatePhysical(255,0x59,0)!=EqualsText)throw new Exception("Keypad equals");
   foreach(int clear in new[]{144,12,254})if(e.TranslatePhysical(clear,0,0)!=ClearLine)throw new Exception("Clear erases current line");
   if(e.TranslatePhysical(12,0x4c,0)!=12)throw new Exception("Keypad 5 must not toggle Num Lock");
   if(e.TranslatePhysical(36,0x47,1)!=36 || e.TranslatePhysical(97,0x4f,0)!=97)throw new Exception("Normal navigation and keypad preserved");
   Console.WriteLine("PASS: keypad equals variants, Clear variants, navigation and numpad unchanged.");
   Console.WriteLine("PASS: identity, modifier swaps, six media mappings, explicit mapping priority.");
  }
 }
}
