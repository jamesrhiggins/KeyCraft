using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
namespace KeyCraft {
 static class AppleInput {
  [StructLayout(LayoutKind.Sequential)] struct Device {public IntPtr handle;public uint type;}
  [StructLayout(LayoutKind.Sequential)] struct Registration {public ushort page,usage;public uint flags;public IntPtr window;}
  [StructLayout(LayoutKind.Sequential)] struct Caps {
   public ushort usage,page,inputLength,outputLength,featureLength;
   [MarshalAs(UnmanagedType.ByValArray,SizeConst=17)]public ushort[] reserved;
   public ushort links,inputButtons,inputValues,inputData,outputButtons,outputValues,outputData,featureButtons,featureValues,featureData;
  }
  [DllImport("user32.dll")] static extern uint GetRawInputDeviceList([Out] Device[] list,ref uint count,uint size);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern uint GetRawInputDeviceInfo(IntPtr device,uint command,IntPtr data,ref uint size);
  [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterRawInputDevices(Registration[] list,uint count,uint size);
  [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr input,uint command,IntPtr data,ref uint size,uint headerSize);
  [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr preparsed,out Caps caps);
  [DllImport("hid.dll")] static extern int HidP_GetUsages(int type,ushort page,ushort link,[Out] ushort[] usages,ref uint length,IntPtr preparsed,byte[] report,uint reportLength);
  [DllImport("hid.dll")] static extern int HidP_GetButtonCaps(int type,IntPtr buttons,ref ushort length,IntPtr preparsed);
  [DllImport("hid.dll")] static extern int HidP_GetValueCaps(int type,IntPtr values,ref ushort length,IntPtr preparsed);
  [DllImport("hid.dll")] static extern int HidP_GetUsageValue(int type,ushort page,ushort link,ushort usage,out uint value,IntPtr preparsed,byte[] report,uint length);
  const int Success=0x110000;
  class Source {public IntPtr prep;public Caps caps;public bool fn,eject;}
  static readonly Dictionary<IntPtr,Source> sources=new Dictionary<IntPtr,Source>();
  public static volatile bool Present, FnDown; public static int HidCollections;
  public static string Refresh(IntPtr window) {
   uint count=0;uint element=(uint)Marshal.SizeOf(typeof(Device));GetRawInputDeviceList(null,ref count,element);
   var devices=new Device[count];uint found=GetRawInputDeviceList(devices,ref count,element);if(found==uint.MaxValue){Present=false;FnDown=false;return "Input detection unavailable";}
   bool present=false;var active=new HashSet<IntPtr>();var report=new StringBuilder();int hids=0;
   foreach(var d in devices) {
    uint chars=0;GetRawInputDeviceInfo(d.handle,0x20000007,IntPtr.Zero,ref chars);if(chars==0)continue;
    IntPtr nameBuffer=Marshal.AllocHGlobal((int)(chars+1)*2);string name;
    try {if(GetRawInputDeviceInfo(d.handle,0x20000007,nameBuffer,ref chars)==uint.MaxValue)continue;name=Marshal.PtrToStringUni(nameBuffer);}finally{Marshal.FreeHGlobal(nameBuffer);}
    bool apple=name.IndexOf("VID_05AC",StringComparison.OrdinalIgnoreCase)>=0 || name.IndexOf("VID&000205AC",StringComparison.OrdinalIgnoreCase)>=0;
    if(!apple)continue;
    if(d.type==1)present=true;
    report.AppendLine("Apple input type "+d.type+": "+name);
    if(d.type!=2 && !(d.type==1 && window==IntPtr.Zero))continue;
    active.Add(d.handle);hids++;
    Source source;
    if(!sources.TryGetValue(d.handle,out source)) {
     uint bytes=0;GetRawInputDeviceInfo(d.handle,0x20000005,IntPtr.Zero,ref bytes);if(bytes==0)continue;
     var prep=Marshal.AllocHGlobal((int)bytes);
     Caps caps;if(GetRawInputDeviceInfo(d.handle,0x20000005,prep,ref bytes)==uint.MaxValue || HidP_GetCaps(prep,out caps)!=Success){Marshal.FreeHGlobal(prep);continue;}
     source=new Source{prep=prep,caps=caps};sources[d.handle]=source;
    }
    report.AppendLine("  HID page="+source.caps.page.ToString("X4")+" usage="+source.caps.usage.ToString("X4")+" input bytes="+source.caps.inputLength);
    if(window==IntPtr.Zero && source.caps.inputValues>0){ushort length=source.caps.inputValues;var values=Marshal.AllocHGlobal(length*72);try{if(HidP_GetValueCaps(0,values,ref length,source.prep)==Success)for(int i=0;i<length;i++){IntPtr b=IntPtr.Add(values,i*72);report.AppendLine("    values page="+((ushort)Marshal.ReadInt16(b)).ToString("X4")+" report="+Marshal.ReadByte(b,2)+" usage="+((ushort)Marshal.ReadInt16(b,56)).ToString("X4")+" bits="+Marshal.ReadInt16(b,18)+" count="+Marshal.ReadInt16(b,20));}}finally{Marshal.FreeHGlobal(values);}}
    if(window==IntPtr.Zero && source.caps.inputButtons>0){ushort length=source.caps.inputButtons;var buttons=Marshal.AllocHGlobal(length*72);try{if(HidP_GetButtonCaps(0,buttons,ref length,source.prep)==Success)for(int i=0;i<length;i++){IntPtr b=IntPtr.Add(buttons,i*72);report.AppendLine("    buttons page="+((ushort)Marshal.ReadInt16(b)).ToString("X4")+" report="+Marshal.ReadByte(b,2)+" usage="+((ushort)Marshal.ReadInt16(b,56)).ToString("X4")+" range="+Marshal.ReadByte(b,12));}}finally{Marshal.FreeHGlobal(buttons);}}
    if(window!=IntPtr.Zero)RegisterRawInputDevices(new[]{new Registration{page=source.caps.page,usage=source.caps.usage,flags=0x100,window=window}},1,(uint)Marshal.SizeOf(typeof(Registration)));
   }
   var removed=new List<IntPtr>();foreach(var s in sources)if(!active.Contains(s.Key))removed.Add(s.Key);
   foreach(var key in removed){Marshal.FreeHGlobal(sources[key].prep);sources.Remove(key);}
   Present=present;HidCollections=hids;UpdateFn();return report.ToString();
  }
  static void UpdateFn(){FnDown=false;foreach(var s in sources.Values)FnDown|=s.fn;}
  static bool Usage(Source source,byte[] report,ushort page,ushort usage,out bool valid) {
   uint length=128;var usages=new ushort[length];int result=HidP_GetUsages(0,page,0,usages,ref length,source.prep,report,(uint)report.Length);
   valid=result==Success;if(!valid)return false;for(int i=0;i<length;i++)if(usages[i]==usage)return true;return false;
  }
  public static void Process(IntPtr raw,Action eject) {
   uint header=(uint)(8+2*IntPtr.Size),bytes=0;GetRawInputData(raw,0x10000003,IntPtr.Zero,ref bytes,header);if(bytes<header+8 || bytes>65536)return;
   IntPtr buffer=Marshal.AllocHGlobal((int)bytes);
   try {
    if(GetRawInputData(raw,0x10000003,buffer,ref bytes,header)==uint.MaxValue || Marshal.ReadInt32(buffer)!=2)return;
    IntPtr device=Marshal.ReadIntPtr(buffer,8);Source source;if(!sources.TryGetValue(device,out source))return;
    int reportSize=Marshal.ReadInt32(buffer,(int)header),count=Marshal.ReadInt32(buffer,(int)header+4);
    if(reportSize<=0 || count<0 || (long)reportSize*count>bytes-header-8)return;
    for(int n=0;n<count;n++) {
     var report=new byte[reportSize];Marshal.Copy(IntPtr.Add(buffer,(int)header+8+n*reportSize),report,0,reportSize);
     bool valid,pressed=false,fnValid=false;
     foreach(ushort page in new ushort[]{0xff00,0xff,0xff01,0xff7f}) {bool down=Usage(source,report,page,3,out valid);if(valid){fnValid=true;pressed|=down;}else{uint value;if(HidP_GetUsageValue(0,page,0,3,out value,source.prep,report,(uint)report.Length)==Success){fnValid=true;pressed|=value!=0;}}}
     if(fnValid)source.fn=pressed;
     bool ejectDown=Usage(source,report,12,0xb8,out valid);
     if(valid){if(ejectDown && !source.eject && eject!=null)eject();source.eject=ejectDown;}
    }
    UpdateFn();
   } finally {Marshal.FreeHGlobal(buffer);}
  }
  public static void Dispose(){foreach(var s in sources.Values)Marshal.FreeHGlobal(s.prep);sources.Clear();FnDown=false;}
 }
 static class UsbStorage {
  [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Locate_DevNodeW(out uint node,string id,uint flags);
  [DllImport("cfgmgr32.dll")]static extern uint CM_Get_Parent(out uint parent,uint node,uint flags);
  [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Get_Device_IDW(uint node,StringBuilder id,int length,uint flags);
  [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Request_Device_EjectW(uint node,out int veto,StringBuilder name,int length,uint flags);
  public class Drive {public string Name,Id;public uint Node;public bool Protected;public override string ToString(){return Name+(Protected?" (system/in-use disk: excluded)":"");}}
  public static List<Drive> Discover() {
   var protectedLetters=new HashSet<string>(StringComparer.OrdinalIgnoreCase);protectedLetters.Add(Environment.GetEnvironmentVariable("SystemDrive")??"C:");
   using(var pages=new ManagementObjectSearcher("SELECT Name FROM Win32_PageFileUsage"))foreach(ManagementObject page in pages.Get()){using(page){string path=Convert.ToString(page["Name"]);if(path.Length>=2)protectedLetters.Add(path.Substring(0,2));}}
   var result=new List<Drive>();
   using(var query=new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive"))foreach(ManagementObject disk in query.Get())using(disk) {
    string id=Convert.ToString(disk["PNPDeviceID"]);uint node;if(CM_Locate_DevNodeW(out node,id,0)!=0)continue;
    uint current=node,usb=0;
    for(int depth=0;depth<12;depth++) {var text=new StringBuilder(512);if(CM_Get_Device_IDW(current,text,text.Capacity,0)!=0)break;
     // Physical USB device only: never eject an ancestor hub or host controller.
     if(text.ToString().StartsWith("USB\\VID_",StringComparison.OrdinalIgnoreCase)){usb=current;break;}
     uint parent;if(CM_Get_Parent(out parent,current,0)!=0)break;current=parent;
    }
    if(usb==0)continue;
    bool protect=false;var letters=new List<string>();
    using(var partitions=disk.GetRelated("Win32_DiskPartition"))foreach(ManagementObject partition in partitions)using(partition) {
     protect|=Convert.ToBoolean(partition["BootPartition"]);
     using(var volumes=partition.GetRelated("Win32_LogicalDisk"))foreach(ManagementObject volume in volumes)using(volume){string letter=Convert.ToString(volume["DeviceID"]);letters.Add(letter);protect|=protectedLetters.Contains(letter);}
    }
    result.Add(new Drive{Name=Convert.ToString(disk["Model"])+" "+string.Join(", ",letters.ToArray()),Id=id,Node=usb,Protected=protect});
   }
   return result;
  }
  public static string EjectAll() {
   var drives=Discover();if(drives.Count==0)return "No USB storage drives are connected.";
   var result=new List<string>();var done=new HashSet<uint>();
   // A USB enclosure can expose multiple disks. Protect the whole enclosure if any disk is protected.
   var protectedNodes=new HashSet<uint>();foreach(var drive in drives)if(drive.Protected)protectedNodes.Add(drive.Node);
   foreach(var drive in drives){if(!done.Add(drive.Node))continue;if(protectedNodes.Contains(drive.Node)){result.Add("Skipped system/page-file drive: "+drive.Name);continue;}
    int veto;var reason=new StringBuilder(512);uint code=CM_Request_Device_EjectW(drive.Node,out veto,reason,reason.Capacity,0);
    result.Add(code==0?"Safely ejected: "+drive.Name:"Could not eject "+drive.Name+" (Windows code "+code+", veto "+veto+") "+reason);
   }
   return string.Join(Environment.NewLine,result.ToArray());
  }
 }
}
