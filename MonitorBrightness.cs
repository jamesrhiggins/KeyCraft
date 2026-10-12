using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
namespace KeyCraft {
 static class MonitorBrightness {
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct Physical {public IntPtr handle;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string description;}
  delegate bool MonitorCallback(IntPtr monitor,IntPtr dc,IntPtr rectangle,IntPtr data);
  [DllImport("user32.dll")]static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorCallback callback,IntPtr data);
  [DllImport("dxva2.dll",SetLastError=true)]static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor,out uint count);
  [DllImport("dxva2.dll",SetLastError=true)]static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor,uint count,[Out] Physical[] devices);
  [DllImport("dxva2.dll")]static extern bool DestroyPhysicalMonitors(uint count,Physical[] devices);
  [DllImport("dxva2.dll",SetLastError=true)]static extern bool GetMonitorBrightness(IntPtr monitor,out uint minimum,out uint current,out uint maximum);
  [DllImport("dxva2.dll",SetLastError=true)]static extern bool SetMonitorBrightness(IntPtr monitor,uint value);
  [DllImport("dxva2.dll",SetLastError=true)]static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr monitor,byte code,out int type,out uint current,out uint maximum);
  [DllImport("dxva2.dll",SetLastError=true)]static extern bool SetVCPFeature(IntPtr monitor,byte code,uint value);
  public static uint Adjust(uint current,uint minimum,uint maximum,int percent){if(maximum<=minimum || percent==0)return current;long step=Math.Max(1,(long)(maximum-minimum)*Math.Abs(percent)/100);return (uint)Math.Max(minimum,Math.Min(maximum,(long)current+(percent<0?-step:step)));}
  public static string Diagnose(){return External(0,false);}
  public static string VerifyHardware(){return External(0,true);}
  static string External(int change,bool write){var results=new List<string>();
   MonitorCallback callback=(monitor,dc,rect,data)=>{
    uint count;if(!GetNumberOfPhysicalMonitorsFromHMONITOR(monitor,out count)||count==0)return true;
    var devices=new Physical[count];if(!GetPhysicalMonitorsFromHMONITOR(monitor,count,devices))return true;
    try{foreach(var device in devices){uint min,current,max;bool standard=GetMonitorBrightness(device.handle,out min,out current,out max);if(!standard){int type;min=0;if(!GetVCPFeatureAndVCPFeatureReply(device.handle,0x10,out type,out current,out max)){results.Add(device.description+": DDC/CI brightness unavailable (Windows "+Marshal.GetLastWin32Error()+")");continue;}}
     if(max<=min){results.Add(device.description+": invalid brightness range");continue;}
     if(!write){results.Add(device.description+": DDC/CI supported, brightness "+Math.Round((current-min)*100d/(max-min))+"%");continue;}
     uint target=Adjust(current,min,max,change);bool ok=standard?SetMonitorBrightness(device.handle,target):SetVCPFeature(device.handle,0x10,target);
     results.Add(device.description+(ok?": brightness "+Math.Round((target-min)*100d/(max-min))+"%":": could not set brightness (Windows "+Marshal.GetLastWin32Error()+")"));
    }}finally{DestroyPhysicalMonitors(count,devices);}return true;
   };
   if(!EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,callback,IntPtr.Zero))results.Add("Windows monitor enumeration failed.");
   return results.Count==0?"No external monitor brightness controls found.":string.Join(Environment.NewLine,results.ToArray());
  }
  public static string Change(int change){
   // Built-in panels use WMI; external displays generally use DDC/CI instead.
   try{using(var values=new ManagementObjectSearcher("root\\WMI","SELECT * FROM WmiMonitorBrightness"))foreach(ManagementObject value in values.Get())using(value){int level=Math.Max(0,Math.Min(100,Convert.ToInt32(value["CurrentBrightness"])+change));string name=Convert.ToString(value["InstanceName"]);using(var methods=new ManagementObjectSearcher("root\\WMI","SELECT * FROM WmiMonitorBrightnessMethods"))foreach(ManagementObject method in methods.Get())using(method)if(Convert.ToString(method["InstanceName"])==name){method.InvokeMethod("WmiSetBrightness",new object[]{(uint)1,(byte)level});return "Display brightness: "+level+"%";}}}catch(ManagementException){}
   return External(change,true);
  }
  public static void Test(){if(Adjust(95,0,100,10)!=100 || Adjust(3,0,100,-10)!=0 || Adjust(50,10,90,10)!=58 || Adjust(5,5,5,10)!=5)throw new Exception("Brightness range handling");Console.WriteLine("PASS: brightness clamps and nonstandard ranges.");}
 }
}
