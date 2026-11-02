using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace BudsDesk {
    public sealed class Earbuds {
        public string Name; public ulong Address; public bool Connected;
        public override string ToString() { return Name + (Connected ? " · 已连接音频" : " · 已配对"); }
    }
    internal static class Native {
        [StructLayout(LayoutKind.Sequential)] internal struct Time { public ushort Year,Month,DayOfWeek,Day,Hour,Minute,Second,Milliseconds; }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] internal struct Device {
            public uint Size; public ulong Address; public uint Class;
            public int Connected,Remembered,Authenticated; public Time Seen,Used;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=248)] public string Name;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct Search {
            public uint Size; public int Authenticated,Remembered,Unknown,Connected,Inquiry;
            public byte Timeout; public IntPtr Radio;
        }
        [StructLayout(LayoutKind.Explicit,Size=30)] internal struct Address {
            [FieldOffset(0)] public ushort Family;
            [FieldOffset(2)] public ulong BluetoothAddress;
            [FieldOffset(10)] public Guid Service;
            [FieldOffset(26)] public uint Port;
        }
        [DllImport("bthprops.cpl",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern IntPtr BluetoothFindFirstDevice(ref Search s,ref Device d);
        [DllImport("bthprops.cpl",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool BluetoothFindNextDevice(IntPtr h,ref Device d);
        [DllImport("bthprops.cpl")] internal static extern bool BluetoothFindDeviceClose(IntPtr h);
        [DllImport("Ws2_32.dll")] internal static extern int WSAStartup(ushort version,IntPtr data);
        [DllImport("Ws2_32.dll")] internal static extern IntPtr socket(int af,int type,int protocol);
        [DllImport("Ws2_32.dll")] internal static extern int connect(IntPtr socket,ref Address address,int size);
        [DllImport("Ws2_32.dll")] internal static extern int send(IntPtr socket,byte[] bytes,int length,int flags);
        [DllImport("Ws2_32.dll")] internal static extern int recv(IntPtr socket,byte[] bytes,int length,int flags);
        [DllImport("Ws2_32.dll")] internal static extern int setsockopt(IntPtr socket,int level,int option,ref int value,int length);
        [DllImport("Ws2_32.dll")] internal static extern int closesocket(IntPtr socket);
        [DllImport("Ws2_32.dll")] internal static extern int WSAGetLastError();
        internal static Exception Error(string operation) {
            int code=WSAGetLastError(); return new InvalidOperationException(operation+"："+new Win32Exception(code).Message+" ("+code+")");
        }
    }
    public static class BluetoothDiscovery {
        public static List<Earbuds> Paired() {
            var list=new List<Earbuds>();
            var search=new Native.Search { Size=(uint)Marshal.SizeOf(typeof(Native.Search)),Authenticated=1,Remembered=1,Connected=1 };
            var d=new Native.Device { Size=(uint)Marshal.SizeOf(typeof(Native.Device)) };
            IntPtr h=Native.BluetoothFindFirstDevice(ref search,ref d);
            if(h==IntPtr.Zero) {
                int error=Marshal.GetLastWin32Error();
                if(error!=259 && error!=0) throw new Win32Exception(error,"无法读取蓝牙设备，请检查蓝牙是否开启");
                return list;
            }
            try {
                do {
                    string name=d.Name ?? "";
                    if(IsTarget(name)) list.Add(new Earbuds { Name=name,Address=d.Address,Connected=d.Connected!=0 });
                    d.Size=(uint)Marshal.SizeOf(typeof(Native.Device));
                } while(Native.BluetoothFindNextDevice(h,ref d));
            } finally { Native.BluetoothFindDeviceClose(h); }
            return list;
        }
        public static bool IsTarget(string name) {
            return !String.IsNullOrEmpty(name) && name.IndexOf("Xiaomi Buds 5 Pro",StringComparison.OrdinalIgnoreCase)>=0 && name.IndexOf("Redmi",StringComparison.OrdinalIgnoreCase)<0;
        }
    }
    public sealed class EarbudConnection : IDisposable {
        static readonly IntPtr Invalid=new IntPtr(-1);
        IntPtr handle=Invalid;
        readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        Parser parser=new Parser(); byte version=3;
        public int Mode { get; private set; }
        public bool Ready { get; private set; }
        public Action<string> Trace;
        static EarbudConnection() {
            IntPtr data=Marshal.AllocHGlobal(512);
            try { int e=Native.WSAStartup(0x0202,data); if(e!=0) throw new Win32Exception(e); }
            finally { Marshal.FreeHGlobal(data); }
        }
        public EarbudConnection() { Mode=-1; }
        void Log(string text) { if(Trace!=null) Trace(text); }
        public async Task ConnectAsync(Earbuds device) {
            await gate.WaitAsync();
            try {
                DisposeSocket(); parser=new Parser();
                handle=Native.socket(32,1,3); if(handle==Invalid) throw Native.Error("创建蓝牙连接失败");
                IntPtr owned=handle;
                var address=new Native.Address { Family=32,BluetoothAddress=device.Address,Service=new Guid("00001101-0000-1000-8000-00805f9b34fb"),Port=0 };
                var task=Task.Run(()=> { var a=address; int rc=Native.connect(owned,ref a,30); if(rc!=0) throw Native.Error("连接控制通道失败"); });
                if(await Task.WhenAny(task,Task.Delay(12000))!=task) {
                    DisposeSocket();
                    task.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
                    throw new TimeoutException("连接超时，请确认耳机已连接电脑，并关闭手机端耳机设置页后重试");
                }
                await task;
                int timeout=1200;
                if(Native.setsockopt(handle,65535,4102,ref timeout,4)!=0 || Native.setsockopt(handle,65535,4101,ref timeout,4)!=0)
                    throw Native.Error("设置通信超时失败");
                await Task.Run(()=> {
                    Send(Protocol.Frame(version,0x000a,0x0300));
                    Wait(0x000a,0x8300,4000);
                    Mode=ReadMode(); Ready=true;
                });
                Log("握手成功 · 模式："+Protocol.ModeName(Mode));
            } catch { DisposeSocket(); throw; }
            finally { gate.Release(); }
        }
        void Send(byte[] bytes) {
            Log("TX "+BitConverter.ToString(bytes));
            int offset=0;
            while(offset<bytes.Length) {
                byte[] part=new byte[bytes.Length-offset]; Buffer.BlockCopy(bytes,offset,part,0,part.Length);
                int n=Native.send(handle,part,part.Length,0);
                if(n<=0) throw Native.Error("发送失败"); offset+=n;
            }
        }
        Packet Wait(ushort vendor,ushort command,int milliseconds) {
            var clock=System.Diagnostics.Stopwatch.StartNew();
            byte[] bytes=new byte[2048];
            while(clock.ElapsedMilliseconds<milliseconds) {
                int n=Native.recv(handle,bytes,bytes.Length,0);
                if(n==0) throw new InvalidOperationException("耳机已断开控制连接");
                if(n<0) { int e=Native.WSAGetLastError(); if(e==10060) continue; throw new Win32Exception(e); }
                foreach(var p in parser.Feed(bytes,n)) {
                    Log("RX "+p.Vendor.ToString("X4")+"/"+p.Command.ToString("X4")+" "+BitConverter.ToString(p.Payload));
                    if(p.Vendor==vendor && p.Command==command) { version=p.Version; return p; }
                    if(p.Vendor==vendor && (p.Command==(command|0x80))) throw new InvalidOperationException("耳机拒绝请求："+BitConverter.ToString(p.Payload));
                }
            }
            throw new TimeoutException("耳机未回应，请重新连接后重试");
        }
        int ReadMode() {
            Send(Protocol.Frame(version,0x001d,0x1003));
            var p=Wait(0x001d,0x1103,4000);
            if(p.Payload.Length!=5) throw new InvalidOperationException("模式响应格式与已知协议不一致");
            return p.Payload[0];
        }
        public async Task<int> RefreshAsync() {
            await gate.WaitAsync();
            try { if(!Ready) throw new InvalidOperationException("请先连接耳机"); Mode=await Task.Run(()=>ReadMode()); return Mode; }
            catch { DisposeSocket(); throw; } finally { gate.Release(); }
        }
        public async Task<int> SetAsync(int mode) {
            byte[] frame=Protocol.SetMode(version,mode);
            await gate.WaitAsync();
            try {
                if(!Ready) throw new InvalidOperationException("请先连接耳机");
                Mode=await Task.Run(()=> {
                    Send(frame); Wait(0x001d,0x1104,4000);
                    int actual=ReadMode();
                    if(actual!=mode) throw new InvalidOperationException("切换尚未确认，耳机回报："+Protocol.ModeName(actual));
                    return actual;
                }); return Mode;
            } catch { DisposeSocket(); throw; } finally { gate.Release(); }
        }
        void DisposeSocket() {
            Ready=false; Mode=-1; IntPtr old=Interlocked.Exchange(ref handle,Invalid);
            if(old!=Invalid) Native.closesocket(old);
        }
        public void Dispose() { DisposeSocket(); }
    }
}
