using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Collections.Generic;
using System.Management;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeyCraft {
 static class SmoothTiming {
  [DllImport("winmm.dll")]static extern uint timeBeginPeriod(uint period);
  [DllImport("winmm.dll")]static extern uint timeEndPeriod(uint period);
  public static bool Begin(){return timeBeginPeriod(1)==0;}
  public static void End(){timeEndPeriod(1);}
 }
 class BufferedFlow : FlowLayoutPanel{public BufferedFlow(){DoubleBuffered=true;}}
 class BufferedGrid : TableLayoutPanel{public BufferedGrid(){DoubleBuffered=true;}}
 class BufferedPanel : Panel{public BufferedPanel(){DoubleBuffered=true;}}
 class GlassCard : Panel {
  public GlassCard(){DoubleBuffered=true;BackColor=Color.Transparent;Padding=new Padding(14);}
  public static GraphicsPath Round(RectangleF rect,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(rect.X,rect.Y,d,d,180,90);p.AddArc(rect.Right-d,rect.Y,d,d,270,90);p.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);p.AddArc(rect.X,rect.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new RectangleF(1,1,Width-3,Height-3);using(var p=Round(r,24))using(var b=new LinearGradientBrush(r,Color.FromArgb(248,255,255,255),Color.FromArgb(220,238,244,252),90f))using(var pen=new Pen(Color.FromArgb(245,255,255,255),1.5f)){e.Graphics.FillPath(b,p);e.Graphics.DrawPath(pen,p);}base.OnPaint(e);}
 }
 class SoftButton : Control, IButtonControl {
  float hover,target;bool pressed,isDefault; readonly Timer animation=new Timer();public bool Primary;
  public DialogResult DialogResult{get;set;}
  public void NotifyDefault(bool value){isDefault=value;Invalidate();}
  public void PerformClick(){if(Enabled && Visible)OnClick(EventArgs.Empty);}
  public SoftButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor|ControlStyles.ResizeRedraw|ControlStyles.Selectable|ControlStyles.StandardClick,true);BackColor=Color.Transparent;AccessibleRole=AccessibleRole.PushButton;TabStop=true;Cursor=Cursors.Hand;Size=new Size(152,38);animation.Interval=15;animation.Tick+=(s,e)=>{hover+=(target-hover)*.22f;if(Math.Abs(target-hover)<.01f){hover=target;animation.Stop();}Invalidate();};}
  protected override bool IsInputKey(Keys key){return key==Keys.Space || key==Keys.Enter || base.IsInputKey(key);}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space || e.KeyCode==Keys.Enter){pressed=true;e.Handled=true;Invalidate();}base.OnKeyDown(e);}
  protected override void OnKeyUp(KeyEventArgs e){if(pressed && (e.KeyCode==Keys.Space || e.KeyCode==Keys.Enter)){pressed=false;e.Handled=true;PerformClick();Invalidate();}base.OnKeyUp(e);}
  protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){Focus();pressed=true;Invalidate();}base.OnMouseDown(e);}
  protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
  protected override void OnLostFocus(EventArgs e){pressed=false;Invalidate();base.OnLostFocus(e);}
  protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
  public void PreviewState(int state){animation.Stop();hover=state==0?0:1;pressed=state==2;Enabled=state!=3;Invalidate();}
  protected override void OnMouseEnter(EventArgs e){target=1;animation.Start();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){target=0;animation.Start();base.OnMouseLeave(e);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new RectangleF(1,1,Width-3,Height-3);Color fill=Primary?Color.FromArgb(0,(int)(110+hover*15),235):Color.FromArgb((int)(232-hover*10),(int)(237-hover*8),245);if(pressed)fill=Primary?Color.FromArgb(0,90,205):Color.FromArgb(211,220,234);if(!Enabled)fill=Color.FromArgb(225,229,236);using(var path=GlassCard.Round(r,12))using(var brush=new SolidBrush(fill))e.Graphics.FillPath(brush,path);if((Focused && ShowFocusCues)||isDefault)using(var path=GlassCard.Round(new RectangleF(3,3,Width-7,Height-7),10))using(var pen=new Pen(Primary?Color.White:Color.FromArgb(0,110,235),1))e.Graphics.DrawPath(pen,path);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,!Enabled?Color.Gray:Primary?Color.White:Color.FromArgb(38,48,66),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
  protected override void Dispose(bool disposing){if(disposing)animation.Dispose();base.Dispose(disposing);}
 }
 class DetectionBadge : Control {
  public bool Detected;float phase;readonly Timer pulse=new Timer();
  readonly Stopwatch phaseClock=Stopwatch.StartNew();
  public DetectionBadge(){SetStyle(ControlStyles.SupportsTransparentBackColor,true);DoubleBuffered=true;Size=new Size(350,34);BackColor=Color.Transparent;pulse.Interval=15;pulse.Tick+=(s,e)=>{phase=(float)phaseClock.Elapsed.TotalSeconds*1.25f;Invalidate(new Rectangle(0,0,32,34));};}
  protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);pulse.Enabled=Visible;}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color color=Detected?Color.FromArgb(39,177,102):Color.FromArgb(153,164,180);if(Detected)using(var glow=new SolidBrush(Color.FromArgb((int)(24+12*Math.Sin(phase)),color)))e.Graphics.FillEllipse(glow,0,3,28,28);using(var fill=new SolidBrush(color))e.Graphics.FillEllipse(fill,8,11,12,12);TextRenderer.DrawText(e.Graphics,Detected?"Apple keyboard detected":"No Apple keyboard detected",Font,new Rectangle(34,0,Width-34,Height),Color.FromArgb(61,74,93),TextFormatFlags.VerticalCenter);}
  protected override void Dispose(bool disposing){if(disposing)pulse.Dispose();base.Dispose(disposing);}
 }
 class KeyboardArt : Control {
  Image art;Bitmap scaled;Size cachedSize;
  public KeyboardArt(){SetStyle(ControlStyles.SupportsTransparentBackColor,true);DoubleBuffered=true;BackColor=Color.Transparent;string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","keyboard.png");if(File.Exists(path))using(var original=Image.FromFile(path))art=new Bitmap(original);}
  protected override void OnPaint(PaintEventArgs e){if(art!=null && Height>16 && Width>20){if(scaled==null || cachedSize!=ClientSize){if(scaled!=null)scaled.Dispose();cachedSize=ClientSize;int width=(int)Math.Min(Width-20,(Height-16)*512f/135),height=(int)(width*135f/512);scaled=new Bitmap(Math.Max(1,width),Math.Max(1,height));using(var g=Graphics.FromImage(scaled)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(art,new Rectangle(0,0,scaled.Width,scaled.Height),new RectangleF(0,188,512,135),GraphicsUnit.Pixel);}}e.Graphics.DrawImageUnscaled(scaled,(Width-scaled.Width)/2,(Height-scaled.Height)/2);}base.OnPaint(e);}
  protected override void Dispose(bool disposing){if(disposing){if(art!=null)art.Dispose();if(scaled!=null)scaled.Dispose();}base.Dispose(disposing);}
 }
 class MainWindow : Form {
  readonly Engine engine=new Engine();readonly NotifyIcon tray=new NotifyIcon();readonly Timer timer=new Timer();
  readonly int[] keys={162,163,164,165,91,92};readonly string[] names={"Left Control","Right Control","Left Option","Right Option","Left Command","Right Command"};readonly ComboBox[] choices=new ComboBox[6];
  CheckBox media,fnFallback,keepNumLockOff;DetectionBadge badge;Label activity,fnStatus,message;SoftButton pause,eject,numLockToggle;bool ready,manualPaused,loading=true,exiting,ejecting,brightnessBusy,numLockError,smoothTiming;DateTime lastEject=DateTime.MinValue;Icon keyboardIcon;readonly Stopwatch updateClock=Stopwatch.StartNew();long lastDevices,lastNumLock;
  readonly string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KeyCraft");
  public MainWindow(){
   Text="KeyCraft";ClientSize=new Size(1020,Math.Min(980,Screen.PrimaryScreen.WorkingArea.Height-60));MinimumSize=new Size(900,700);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=true;Font=new Font("Segoe UI",10);DoubleBuffered=true;BackColor=Color.FromArgb(228,236,249);StartPosition=FormStartPosition.CenterScreen;
   var frame=new BufferedPanel{BackColor=Color.Transparent};Controls.Add(frame);
   Action placeFrame=()=>{int width=Math.Min(1020,ClientSize.Width),height=Math.Min(980,ClientSize.Height);frame.Bounds=new Rectangle((ClientSize.Width-width)/2,(ClientSize.Height-height)/2,width,height);};ClientSizeChanged+=(s,e)=>placeFrame();placeFrame();
   var viewport=new BufferedPanel{Dock=DockStyle.Fill,AutoScroll=false,BackColor=Color.Transparent};frame.Controls.Add(viewport);
   var root=new BufferedGrid{Dock=DockStyle.Fill,AutoSize=false,ColumnCount=1,RowCount=5,Padding=new Padding(12),BackColor=Color.Transparent};root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,350));root.RowStyles.Add(new RowStyle(SizeType.Absolute,144));root.RowStyles.Add(new RowStyle(SizeType.Absolute,36));viewport.Controls.Add(root);
   var header=new BufferedGrid{Dock=DockStyle.Fill,ColumnCount=2,BackColor=Color.Transparent,Margin=Padding.Empty};header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));
   header.Controls.Add(new Label{Text="KeyCraft",Font=new Font("Segoe UI",24,FontStyle.Bold),AutoSize=true},0,0);badge=new DetectionBadge{Anchor=AnchorStyles.Left,Font=new Font("Segoe UI",10),Width=248};root.Controls.Add(header);
   var hero=new GlassCard{Dock=DockStyle.Fill,Padding=new Padding(12),Margin=new Padding(0,0,0,10)};
   hero.Controls.Add(new KeyboardArt{Dock=DockStyle.Fill});
   var heroTitle=Label("Your keyboard. Your way.",18,true);heroTitle.AutoSize=false;heroTitle.Dock=DockStyle.Top;heroTitle.Height=32;heroTitle.TextAlign=ContentAlignment.MiddleCenter;heroTitle.Padding=new Padding(12,0,12,0);heroTitle.Margin=Padding.Empty;heroTitle.AutoEllipsis=true;hero.Controls.Add(heroTitle);
   var modelRow=new BufferedGrid{Dock=DockStyle.Bottom,Height=34,ColumnCount=2,BackColor=Color.Transparent};modelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70));modelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
   var model=Label("Magic Keyboard with Numeric Keypad  ·  Lightning / A1843",10,false);model.Dock=DockStyle.Fill;model.AutoSize=false;model.TextAlign=ContentAlignment.MiddleLeft;modelRow.Controls.Add(model,0,0);badge.Anchor=AnchorStyles.Right;modelRow.Controls.Add(badge,1,0);hero.Controls.Add(modelRow);root.Controls.Add(hero);
   var columns=new BufferedGrid{Dock=DockStyle.Fill,ColumnCount=2,BackColor=Color.Transparent,Margin=new Padding(0)};columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));root.Controls.Add(columns);
   var mapping=new GlassCard{Dock=DockStyle.Fill,Margin=new Padding(0,0,10,0)};columns.Controls.Add(mapping,0,0);
   var mappingGrid=new BufferedGrid{Dock=DockStyle.Fill,ColumnCount=2,RowCount=9,BackColor=Color.Transparent};mappingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,49));mappingGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,51));mapping.Controls.Add(mappingGrid);
   var mappingTitle=Label("Modifier keys",17,true);mappingGrid.Controls.Add(mappingTitle,0,0);mappingGrid.SetColumnSpan(mappingTitle,2);
   for(int i=0;i<6;i++){mappingGrid.Controls.Add(Label(names[i],10,false),0,i+1);choices[i]=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Top,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(241,245,251),Margin=new Padding(0,2,0,4)};choices[i].Items.AddRange(names);choices[i].SelectedIndex=i;mappingGrid.Controls.Add(choices[i],1,i+1);}
   var presets=new BufferedFlow{AutoSize=true,Dock=DockStyle.Fill,BackColor=Color.Transparent};presets.Controls.Add(Button("Mac-style",()=>Preset(true),false,120));presets.Controls.Add(Button("Default",()=>Preset(false),false,110));mappingGrid.Controls.Add(presets,0,7);mappingGrid.SetColumnSpan(presets,2);
   var automatic=Label("Changes save and apply automatically.",9,false);mappingGrid.Controls.Add(automatic,0,8);mappingGrid.SetColumnSpan(automatic,2);
   var actions=new GlassCard{Dock=DockStyle.Fill,Margin=new Padding(10,0,0,0)};columns.Controls.Add(actions,1,0);
   var actionFlow=new BufferedFlow{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.Transparent};actions.Controls.Add(actionFlow);
   actionFlow.Controls.Add(Label("Function row & devices",17,true));
   media=new CheckBox{Text="Utility keys by default · hold Fn for F1–F12",AutoSize=true,Checked=true,Margin=new Padding(0,4,0,4)};actionFlow.Controls.Add(media);
   fnFallback=new CheckBox{Text="Use Right Control as an additional Fn key",AutoSize=true,Checked=true,Margin=new Padding(0,0,0,4)};actionFlow.Controls.Add(fnFallback);
   actionFlow.Controls.Add(Label("F1 / F2   Brightness down / up\nF3   Task View     F4   Search\nF5   File Explorer     F6   Show desktop\nF7–F9   Previous / Play / Next\nF10–F12   Mute / Volume down / Volume up",10,false));
   fnStatus=Label("Fn input: waiting for keyboard",9,false);fnStatus.Margin=new Padding(0,4,0,4);actionFlow.Controls.Add(fnStatus);
   eject=Button("Eject USB drives",EjectUsb,true,180);actionFlow.Controls.Add(eject);
   actionFlow.Controls.Add(Label("F13 fallback ejects USB storage. Busy drives stay connected.\nNative Apple Fn / Eject need hardware verification.",9,false));
   var bottom=new GlassCard{Dock=DockStyle.Fill,Margin=new Padding(0,10,0,0),Padding=new Padding(12)};root.Controls.Add(bottom);
   var bottomGrid=new BufferedGrid{Dock=DockStyle.Fill,ColumnCount=3,RowCount=3,BackColor=Color.Transparent};bottomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,60));bottomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));bottomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));bottomGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,44));bottomGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,28));bottomGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100));bottom.Controls.Add(bottomGrid);
   activity=Label("Checking keyboard…",11,true);bottomGrid.Controls.Add(activity,0,0);pause=Button("Pause mappings",Toggle,false,170);bottomGrid.Controls.Add(pause,1,0);
   message=Label("",9,false);message.AutoSize=false;message.Dock=DockStyle.Fill;message.AutoEllipsis=true;bottomGrid.Controls.Add(message,0,1);
   numLockToggle=Button("Num Lock",()=>{bool target=!Engine.NumLockOn;if(!Engine.SetNumLock(target))Notify("Windows could not change Num Lock.");else Notify("Num Lock switched "+(target?"on.":"off."));},false,170);bottomGrid.Controls.Add(numLockToggle,2,0);
   keepNumLockOff=new CheckBox{Text="Keep Num Lock off · saved",AutoSize=true,Margin=new Padding(0,3,0,0)};bottomGrid.Controls.Add(keepNumLockOff,1,1);bottomGrid.SetColumnSpan(keepNumLockOff,2);
   var test=new TextBox{Dock=DockStyle.Fill,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,2,0,0)};bottomGrid.Controls.Add(test,0,2);bottomGrid.SetColumnSpan(test,3);
   root.Controls.Add(Label("Keypad = types equals. Clear erases the current line; use the app's Num Lock toggle.\nCtrl + Alt + Pause stops mappings. Live mappings affect every keyboard while an Apple keyboard is connected.",9,false));
   LoadSettings();Apply();loading=false;
   var menu=new ContextMenuStrip();menu.Items.Add("Open KeyCraft",null,(s,e)=>{Show();WindowState=FormWindowState.Normal;Activate();});menu.Items.Add("Pause / resume",null,(s,e)=>Toggle());menu.Items.Add("Eject USB drives",null,(s,e)=>EjectUsb());menu.Items.Add("Exit",null,(s,e)=>{exiting=true;Close();});
   string iconPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","keyboard.ico");if(File.Exists(iconPath)){keyboardIcon=new Icon(iconPath);Icon=keyboardIcon;}tray.Icon=keyboardIcon??SystemIcons.Application;tray.Text="KeyCraft";tray.ContextMenuStrip=menu;tray.Visible=true;tray.DoubleClick+=(s,e)=>{Show();Activate();};
   engine.Emergency=()=>Queue(()=>{manualPaused=true;UpdateActivation();});engine.UtilityAction=action=>Queue(()=>Utility(action));
   try{engine.Start();ready=true;}catch(Exception ex){Notify("Keyboard hook unavailable: "+ex.Message);}
   foreach(var choice in choices)choice.SelectedIndexChanged+=(s,e)=>Save();media.CheckedChanged+=(s,e)=>Save();fnFallback.CheckedChanged+=(s,e)=>Save();keepNumLockOff.CheckedChanged+=(s,e)=>{numLockError=false;Save();ApplyNumLockPolicy();};
   Shown+=(s,e)=>{RefreshInput();UpdateActivation();ApplyNumLockPolicy();};VisibleChanged+=(s,e)=>{if(Visible && !smoothTiming)smoothTiming=SmoothTiming.Begin();else if(!Visible && smoothTiming){SmoothTiming.End();smoothTiming=false;}timer.Interval=Visible?15:250;};timer.Interval=15;timer.Tick+=(s,e)=>{long now=updateClock.ElapsedMilliseconds;if(now-lastDevices>=2000){lastDevices=now;RefreshInput();}UpdateActivation();if(now-lastNumLock>=500){lastNumLock=now;ApplyNumLockPolicy();}};timer.Start();
  }
  static Label Label(string text,int size,bool bold){return new Label{Text=text,AutoSize=true,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=bold?Color.FromArgb(30,42,61):Color.FromArgb(85,100,122),Margin=new Padding(0,2,0,4),BackColor=Color.Transparent};}
  SoftButton Button(string text,Action action,bool primary,int width){var b=new SoftButton{Text=text,Primary=primary,Width=width,Margin=new Padding(0,4,8,4)};b.Click+=(s,e)=>action();return b;}
  void Queue(Action action){if(!IsDisposed && IsHandleCreated)BeginInvoke((Action)(()=>{if(!IsDisposed)action();}));}
  void RefreshInput(){try{AppleInput.Refresh(Handle);}catch(Exception ex){AppleInput.Present=false;Notify("Keyboard detection unavailable: "+ex.Message);}}
  static void SetText(Control control,string text){if(control.Text!=text)control.Text=text;}
  void UpdateActivation(){engine.SetActivation(ready,AppleInput.Present,manualPaused);bool active=engine.Enabled;if(badge.Detected!=AppleInput.Present){badge.Detected=AppleInput.Present;badge.Invalidate();}SetText(activity,!ready?"Input hook unavailable":!AppleInput.Present?"Waiting for an Apple keyboard":manualPaused?"Mappings paused":"Active · settings apply automatically");SetText(pause,manualPaused?"Resume mappings":"Pause mappings");pause.Enabled=ready && AppleInput.Present;eject.Enabled=active && !ejecting;SetText(fnStatus,engine.FnActive?"Fn held · standard function keys":"Fn released · "+AppleInput.HidCollections+" Apple HID collections");string trayText=active?"KeyCraft · active":AppleInput.Present?"KeyCraft · paused":"KeyCraft · waiting for Apple keyboard";if(tray.Text!=trayText)tray.Text=trayText;}
  void Apply(){var mappings=new Dictionary<int,int>();for(int i=0;i<6;i++)mappings[keys[i]]=keys[choices[i].SelectedIndex];engine.Configure(mappings,media.Checked,fnFallback.Checked);}
  void ApplyNumLockPolicy(){if(engine.Enabled && keepNumLockOff.Checked && Engine.NumLockOn && !Engine.SetNumLock(false) && !numLockError){numLockError=true;Notify("Windows could not turn Num Lock off.");}numLockToggle.Text="Num Lock: "+(Engine.NumLockOn?"ON":"OFF");numLockToggle.Enabled=engine.Enabled && !keepNumLockOff.Checked;}
  void Save(){if(loading)return;Apply();manualPaused=false;UpdateActivation();try{Directory.CreateDirectory(folder);var lines=new List<string>();foreach(var choice in choices)lines.Add(choice.SelectedIndex.ToString());lines.Add(media.Checked.ToString());lines.Add("v2");lines.Add(fnFallback.Checked.ToString());lines.Add(keepNumLockOff.Checked.ToString());File.WriteAllLines(Path.Combine(folder,"profile.txt"),lines.ToArray());Notify("Settings saved. Num Lock "+(Engine.NumLockOn?"on.":"off."));}catch(Exception ex){Notify("Could not save settings: "+ex.Message);}}
  void LoadSettings(){try{var lines=File.ReadAllLines(Path.Combine(folder,"profile.txt"));if(lines.Length<7)return;var values=new int[6];for(int i=0;i<6;i++)if(!int.TryParse(lines[i],out values[i])||values[i]<0||values[i]>5)return;for(int i=0;i<6;i++)choices[i].SelectedIndex=values[i];bool value;if(lines.Length>=8 && lines[7]=="v2" && bool.TryParse(lines[6],out value))media.Checked=value;if(lines.Length>=9 && bool.TryParse(lines[8],out value))fnFallback.Checked=value;if(lines.Length>=10 && bool.TryParse(lines[9],out value))keepNumLockOff.Checked=value;}catch(IOException){}catch(UnauthorizedAccessException){}}
  void Preset(bool mac){loading=true;for(int i=0;i<6;i++)choices[i].SelectedIndex=mac?new[]{4,5,2,3,0,1}[i]:i;loading=false;Save();}
  void Toggle(){manualPaused=!manualPaused;UpdateActivation();}
  void Notify(string text){message.Text=text;}
  async void EjectUsb(){if(!engine.Enabled || ejecting || (DateTime.UtcNow-lastEject).TotalSeconds<2)return;lastEject=DateTime.UtcNow;ejecting=true;UpdateActivation();Notify("Requesting safe USB removal…");string result;try{result=await Task.Run(()=>UsbStorage.EjectAll());}catch(Exception ex){result="USB ejection failed: "+ex.Message;}if(IsDisposed)return;ejecting=false;UpdateActivation();Notify(result);tray.BalloonTipTitle="KeyCraft · USB storage";tray.BalloonTipText=result;tray.ShowBalloonTip(6000);}
  async void Utility(int action){if(!engine.Enabled)return;switch(action){case 0:case 1:if(brightnessBusy)return;brightnessBusy=true;try{string result=await Task.Run(()=>MonitorBrightness.Change(action==0?-10:10));if(!IsDisposed)Notify(result);}catch(Exception ex){if(!IsDisposed)Notify("Brightness unavailable: "+ex.Message);}finally{brightnessBusy=false;}break;case 2:Engine.Shortcut(91,9);break;case 3:Engine.Shortcut(91,83);break;case 4:Engine.Shortcut(91,69);break;case 5:Engine.Shortcut(91,68);break;case 6:EjectUsb();break;}}
  public void PreviewButtons(int state){PreviewButtons(this,state);}
  static void PreviewButtons(Control parent,int state){foreach(Control child in parent.Controls){var button=child as SoftButton;if(button!=null)button.PreviewState(state);PreviewButtons(child,state);}}
  protected override void WndProc(ref Message m){if(m.Msg==0xff){try{AppleInput.Process(m.LParam,()=>{if(engine.Enabled)Queue(EjectUsb);});}catch(Exception ex){Notify("Apple HID input error: "+ex.Message);}}base.WndProc(ref m);}
  protected override void OnPaintBackground(PaintEventArgs e){using(var gradient=new LinearGradientBrush(ClientRectangle,Color.FromArgb(235,241,255),Color.FromArgb(211,229,242),35f))e.Graphics.FillRectangle(gradient,ClientRectangle);}
  protected override void OnFormClosing(FormClosingEventArgs e){if(!exiting && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}base.OnFormClosing(e);}
  protected override void Dispose(bool disposing){if(disposing){timer.Dispose();if(smoothTiming){SmoothTiming.End();smoothTiming=false;}engine.Dispose();tray.Dispose();if(keyboardIcon!=null)keyboardIcon.Dispose();AppleInput.Dispose();}base.Dispose(disposing);}
 }
}
