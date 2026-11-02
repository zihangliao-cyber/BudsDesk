using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace BudsDesk {
    public sealed class Earbuds {
        public string Name; public ulong Address; public bool Connected;
        public string MacHex { get { return Address.ToString("X12"); } }
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
        [StructLayout(LayoutKind.Sequential)] internal struct DevPropKey {
            public Guid fmtid;
            public uint pid;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct DeviceInterfaceData {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }
        [StructLayout(LayoutKind.Explicit,Size=20)] internal struct BthLeUuid {
            [FieldOffset(0)] public byte IsShortUuid;
            [FieldOffset(4)] public ushort ShortUuid;
            [FieldOffset(4)] public Guid LongUuid;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct GattCharacteristic {
            public ushort ServiceHandle;
            public BthLeUuid CharacteristicUuid;
            public ushort AttributeHandle;
            public ushort CharacteristicValueHandle;
            public byte IsBroadcastable;
            public byte IsReadable;
            public byte IsWritable;
            public byte IsWritableWithoutResponse;
            public byte IsSignedWritable;
            public byte IsNotifiable;
            public byte IsIndicatable;
            public byte HasExtendedProperties;
        }
        [StructLayout(LayoutKind.Sequential)] internal struct GattDescriptor {
            public ushort ServiceHandle;
            public ushort CharacteristicHandle;
            public int DescriptorType;
            public BthLeUuid DescriptorUuid;
            public ushort AttributeHandle;
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

        [DllImport("CfgMgr32.dll",CharSet=CharSet.Unicode)] internal static extern int CM_Get_Device_ID_List_SizeW(out uint pulLen,string pszFilter,uint ulFlags);
        [DllImport("CfgMgr32.dll",CharSet=CharSet.Unicode)] internal static extern int CM_Get_Device_ID_ListW(string pszFilter,char[] Buffer,uint BufferLen,uint ulFlags);
        [DllImport("CfgMgr32.dll",CharSet=CharSet.Unicode)] internal static extern int CM_Locate_DevNodeW(out uint pdnDevInst,string pDeviceID,uint ulFlags);
        [DllImport("CfgMgr32.dll",CharSet=CharSet.Unicode)] internal static extern int CM_Get_DevNode_PropertyW(uint dnDevInst,ref DevPropKey PropertyKey,out uint PropertyType,byte[] PropertyBuffer,ref uint PropertyBufferSize,uint ulFlags);

        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern IntPtr SetupDiGetClassDevsW(ref Guid ClassGuid,IntPtr Enumerator,IntPtr hwndParent,uint Flags);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern bool SetupDiEnumDeviceInterfaces(IntPtr DeviceInfoSet,IntPtr DeviceInfoData,ref Guid InterfaceClassGuid,uint MemberIndex,ref DeviceInterfaceData DeviceInterfaceData);
        [DllImport("setupapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr DeviceInfoSet,ref DeviceInterfaceData DeviceInterfaceData,IntPtr DeviceInterfaceDetailData,uint DeviceInterfaceDetailDataSize,out uint RequiredSize,IntPtr DeviceInfoData);
        [DllImport("setupapi.dll",SetLastError=true)] internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern IntPtr CreateFileW(string lpFileName,uint dwDesiredAccess,uint dwShareMode,IntPtr lpSecurityAttributes,uint dwCreationDisposition,uint dwFlagsAndAttributes,IntPtr hTemplateFile);
        [DllImport("kernel32.dll",SetLastError=true)] internal static extern bool CloseHandle(IntPtr hObject);

        [DllImport("BluetoothApis.dll",SetLastError=true)] internal static extern int BluetoothGATTGetCharacteristics(IntPtr hDevice,IntPtr Service,ushort CharacteristicsBufferCount,IntPtr CharacteristicsBuffer,out ushort CharacteristicsBufferActual,uint Flags);
        [DllImport("BluetoothApis.dll",SetLastError=true)] internal static extern int BluetoothGATTGetDescriptors(IntPtr hDevice,ref GattCharacteristic Characteristic,ushort DescriptorsBufferCount,IntPtr DescriptorsBuffer,out ushort DescriptorsBufferActual,uint Flags);
        [DllImport("BluetoothApis.dll",SetLastError=true)] internal static extern int BluetoothGATTGetDescriptorValue(IntPtr hDevice,ref GattDescriptor Descriptor,uint DescriptorValueDataSize,IntPtr DescriptorValue,out ushort DescriptorValueSizeRequired,uint Flags);
        [DllImport("BluetoothApis.dll",SetLastError=true)] internal static extern int BluetoothGATTGetCharacteristicValue(IntPtr hDevice,ref GattCharacteristic Characteristic,uint CharacteristicValueDataSize,IntPtr CharacteristicValue,out ushort CharacteristicValueSizeRequired,uint Flags);

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

    public static class WindowsBatteryReader {
        static readonly Native.DevPropKey PkeyBattery = new Native.DevPropKey { fmtid = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), pid = 2 };
        static readonly Native.DevPropKey PkeyIsConnected = new Native.DevPropKey { fmtid = new Guid("83DA6326-97A6-4088-9453-A1923F573B29"), pid = 15 };
        static readonly Guid GuidBatteryService = new Guid("0000180f-0000-1000-8000-00805f9b34fb");

        public static BatterySnapshot Read(Earbuds device, EarbudConnection link, Action<string> trace) {
            bool controlConnected = link != null && link.Ready;
            var snapshot = new BatterySnapshot { IsConnected = controlConnected || (device != null && device.Connected) };
            if (device == null || device.Address == 0) return snapshot;

            string macHex = device.Address.ToString("X12");
            try { ReadCfgMgr(macHex, snapshot, trace); }
            catch (Exception ex) { if (trace != null) trace("CfgMgr battery read error: " + ex.Message); }

            try { ReadGattBatteryService(macHex, snapshot, trace); }
            catch (Exception ex) { if (trace != null) trace("GATT 180F battery read error: " + ex.Message); }

            if (link != null && link.Ready && link.RcspBattery != null && link.RcspBattery.IsRcspLive) {
                var rb = link.RcspBattery;
                snapshot.IsConnected = true;
                snapshot.IsRcspLive = true;
                snapshot.LeftPercent = rb.LeftPercent;
                snapshot.RightPercent = rb.RightPercent;
                snapshot.CasePercent = rb.CasePercent;
                snapshot.LeftCharging = rb.LeftCharging;
                snapshot.RightCharging = rb.RightCharging;
                snapshot.CaseCharging = rb.CaseCharging;
                snapshot.FirmwareVersion = rb.FirmwareVersion;
                snapshot.VidPidText = rb.VidPidText;
            }

            return snapshot;
        }

        static void ReadCfgMgr(string macHex, BatterySnapshot snapshot, Action<string> trace) {
            uint len;
            if (Native.CM_Get_Device_ID_List_SizeW(out len, null, 0) != 0 || len == 0) return;
            char[] buf = new char[len];
            if (Native.CM_Get_Device_ID_ListW(null, buf, len, 0) != 0) return;

            bool classicConnected = false, leConnected = false;
            int? classicBat = null, leBat = null, hfpBat = null;

            string all = new string(buf);
            string[] ids = all.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string id in ids) {
                if (id.IndexOf(macHex, StringComparison.OrdinalIgnoreCase) < 0) continue;
                uint devInst;
                if (Native.CM_Locate_DevNodeW(out devInst, id, 1) != 0) continue;

                if (id.StartsWith("BTHENUM\\Dev_", StringComparison.OrdinalIgnoreCase)
                    || id.StartsWith("BTHLE\\Dev_", StringComparison.OrdinalIgnoreCase)) {
                    var connKey = PkeyIsConnected;
                    uint connType; uint connSize = 4; byte[] connBuf = new byte[4];
                    if (Native.CM_Get_DevNode_PropertyW(devInst, ref connKey, out connType, connBuf, ref connSize, 0) == 0 && connSize >= 1) {
                        bool nodeConnected = connBuf[0] != 0;
                        if (nodeConnected) snapshot.IsConnected = true;
                        if (id.StartsWith("BTHENUM\\Dev_", StringComparison.OrdinalIgnoreCase)) classicConnected = nodeConnected;
                        if (id.StartsWith("BTHLE\\Dev_", StringComparison.OrdinalIgnoreCase)) leConnected = nodeConnected;
                        if (trace != null) trace("PnP [" + id + "] IsConnected=" + nodeConnected);
                    }
                }

                var batKey = PkeyBattery;
                uint propType; uint propSize = 8; byte[] propBuf = new byte[8];
                if (Native.CM_Get_DevNode_PropertyW(devInst, ref batKey, out propType, propBuf, ref propSize, 0) == 0 && propSize >= 1) {
                    int val = propBuf[0];
                    if (trace != null) trace("PnP [" + id + "] DEVPKEY_Bluetooth_Battery=" + val + " (type=" + propType + ")");
                    if (id.StartsWith("BTHLE\\Dev_", StringComparison.OrdinalIgnoreCase)) {
                        if (BatterySnapshot.IsValidPercent(val)) leBat = val;
                    } else if (id.StartsWith("BTHENUM\\Dev_", StringComparison.OrdinalIgnoreCase)) {
                        if (BatterySnapshot.IsValidPercent(val)) classicBat = val;
                    } else if (id.IndexOf("{0000111e-0000-1000-8000-00805f9b34fb}", StringComparison.OrdinalIgnoreCase) >= 0) {
                        snapshot.HfpRawValue = val;
                        if (BatterySnapshot.IsValidPercent(val)) hfpBat = val;
                    }
                }
            }

            // Prefer active transport's battery so stale BLE cache doesn't override live Classic BT HFP battery
            if (classicConnected && (classicBat.HasValue || (hfpBat.HasValue && hfpBat.Value > 10))) {
                snapshot.WindowsPercent = classicBat ?? hfpBat;
            } else if (leConnected && leBat.HasValue) {
                snapshot.WindowsPercent = leBat;
            } else {
                snapshot.WindowsPercent = classicBat ?? (hfpBat.HasValue && hfpBat.Value > 10 ? hfpBat : null) ?? leBat;
            }
        }

        static void ReadGattBatteryService(string macHex, BatterySnapshot snapshot, Action<string> trace) {
            Guid serviceGuid = GuidBatteryService;
            IntPtr hInfo = Native.SetupDiGetClassDevsW(ref serviceGuid, IntPtr.Zero, IntPtr.Zero, 0x02 | 0x10);
            if (hInfo == IntPtr.Zero || hInfo == new IntPtr(-1)) return;

            try {
                uint memberIndex = 0;
                while (true) {
                    var ifData = new Native.DeviceInterfaceData { cbSize = Marshal.SizeOf(typeof(Native.DeviceInterfaceData)) };
                    if (!Native.SetupDiEnumDeviceInterfaces(hInfo, IntPtr.Zero, ref serviceGuid, memberIndex, ref ifData))
                        break;
                    memberIndex++;

                    uint reqSize;
                    Native.SetupDiGetDeviceInterfaceDetailW(hInfo, ref ifData, IntPtr.Zero, 0, out reqSize, IntPtr.Zero);
                    if (reqSize == 0) continue;

                    IntPtr detailPtr = Marshal.AllocHGlobal((int)reqSize);
                    string devicePath = null;
                    try {
                        Marshal.WriteInt32(detailPtr, IntPtr.Size == 8 ? 8 : 6);
                        if (Native.SetupDiGetDeviceInterfaceDetailW(hInfo, ref ifData, detailPtr, reqSize, out reqSize, IntPtr.Zero))
                            devicePath = Marshal.PtrToStringUni(IntPtr.Add(detailPtr, 4));
                    } finally {
                        Marshal.FreeHGlobal(detailPtr);
                    }

                    if (String.IsNullOrEmpty(devicePath) || devicePath.IndexOf(macHex, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    InspectGattInterface(devicePath, snapshot, trace);
                }
            } finally {
                Native.SetupDiDestroyDeviceInfoList(hInfo);
            }
        }

        static void InspectGattInterface(string devicePath, BatterySnapshot snapshot, Action<string> trace) {
            IntPtr hDev = Native.CreateFileW(devicePath, 0x80000000u, 3u, IntPtr.Zero, 3u, 0u, IntPtr.Zero);
            if (hDev == IntPtr.Zero || hDev == new IntPtr(-1)) return;

            try {
                ushort charCount;
                Native.BluetoothGATTGetCharacteristics(hDev, IntPtr.Zero, 0, IntPtr.Zero, out charCount, 0);
                if (charCount == 0) return;

                int charSize = Marshal.SizeOf(typeof(Native.GattCharacteristic));
                IntPtr charBuf = Marshal.AllocHGlobal(charSize * charCount);
                try {
                    ushort actualChars;
                    int hr = Native.BluetoothGATTGetCharacteristics(hDev, IntPtr.Zero, charCount, charBuf, out actualChars, 0);
                    if (hr != 0) return;

                    for (int i = 0; i < actualChars; i++) {
                        var ch = (Native.GattCharacteristic)Marshal.PtrToStructure(IntPtr.Add(charBuf, i * charSize), typeof(Native.GattCharacteristic));
                        if (ch.CharacteristicUuid.IsShortUuid == 0 || ch.CharacteristicUuid.ShortUuid != 0x2A19)
                            continue;

                        ushort sideDescription = ReadPresentationDescription(hDev, ref ch, trace);
                        if (sideDescription == 0) {
                            // Fallback by service handle order if BLE link is offline and 0x2904 cannot be read live
                            if (devicePath.IndexOf("&0051#", StringComparison.OrdinalIgnoreCase) >= 0)
                                sideDescription = GattPresentationFormat.DescriptionLeft;
                            else if (devicePath.IndexOf("&0056#", StringComparison.OrdinalIgnoreCase) >= 0)
                                sideDescription = GattPresentationFormat.DescriptionRight;
                        }
                        int? livePct = ReadCharacteristicPercent(hDev, ref ch, 0x04u, trace);
                        int? cachedPct = ReadCharacteristicPercent(hDev, ref ch, 0x00u, trace);

                        if (trace != null) {
                            string sideLabel = sideDescription == GattPresentationFormat.DescriptionLeft ? "Left(0x010D)"
                                : sideDescription == GattPresentationFormat.DescriptionRight ? "Right(0x010E)"
                                : "0x" + sideDescription.ToString("X4");
                            trace("GATT 180F/2A19 [Service=0x" + ch.ServiceHandle.ToString("X4") + " " + sideLabel + "] Live(0x04)="
                                + (livePct.HasValue ? livePct.Value + "%" : "N/A")
                                + " Cache(0x00)=" + (cachedPct.HasValue ? cachedPct.Value + "%" : "N/A"));
                        }

                        if (sideDescription == GattPresentationFormat.DescriptionLeft) {
                            snapshot.CachedLeftPercent = cachedPct;
                            if (livePct.HasValue) {
                                snapshot.LeftPercent = livePct;
                                snapshot.IsLiveGatt = true;
                                snapshot.IsConnected = true;
                            }
                        } else if (sideDescription == GattPresentationFormat.DescriptionRight) {
                            snapshot.CachedRightPercent = cachedPct;
                            if (livePct.HasValue) {
                                snapshot.RightPercent = livePct;
                                snapshot.IsLiveGatt = true;
                                snapshot.IsConnected = true;
                            }
                        }
                    }
                } finally {
                    Marshal.FreeHGlobal(charBuf);
                }
            } finally {
                Native.CloseHandle(hDev);
            }
        }

        static ushort ReadPresentationDescription(IntPtr hDev, ref Native.GattCharacteristic ch, Action<string> trace) {
            ushort descCount;
            Native.BluetoothGATTGetDescriptors(hDev, ref ch, 0, IntPtr.Zero, out descCount, 0);
            if (descCount == 0) return 0;

            int descSize = Marshal.SizeOf(typeof(Native.GattDescriptor));
            IntPtr descBuf = Marshal.AllocHGlobal(descSize * descCount);
            try {
                ushort actualDescs;
                if (Native.BluetoothGATTGetDescriptors(hDev, ref ch, descCount, descBuf, out actualDescs, 0) != 0)
                    return 0;

                for (int d = 0; d < actualDescs; d++) {
                    var desc = (Native.GattDescriptor)Marshal.PtrToStructure(IntPtr.Add(descBuf, d * descSize), typeof(Native.GattDescriptor));
                    if (desc.DescriptorType != 4 && (desc.DescriptorUuid.IsShortUuid == 0 || desc.DescriptorUuid.ShortUuid != 0x2904))
                        continue;

                    ushort reqVal;
                    Native.BluetoothGATTGetDescriptorValue(hDev, ref desc, 0, IntPtr.Zero, out reqVal, 0);
                    int alloc = reqVal > 64 ? reqVal : 128;
                    IntPtr valBuf = Marshal.AllocHGlobal(alloc);
                    try {
                        for (int z = 0; z < alloc; z++) Marshal.WriteByte(valBuf, z, 0);
                        int hr = Native.BluetoothGATTGetDescriptorValue(hDev, ref desc, (uint)alloc, valBuf, out reqVal, 0);
                        if (hr == 0 && reqVal > 0) {
                            int copyLen = Math.Min((int)reqVal, alloc);
                            byte[] raw = new byte[copyLen];
                            Marshal.Copy(valBuf, raw, 0, copyLen);
                            ushort description, unit; byte ns;
                            if (GattPresentationFormat.TryParseDescriptorValue(raw, out description, out unit, out ns))
                                return description;
                        }
                    } finally {
                        Marshal.FreeHGlobal(valBuf);
                    }
                }
            } finally {
                Marshal.FreeHGlobal(descBuf);
            }
            return 0;
        }

        static int? ReadCharacteristicPercent(IntPtr hDev, ref Native.GattCharacteristic ch, uint flags, Action<string> trace) {
            ushort reqVal = 0;
            int alloc = 64;
            IntPtr valBuf = Marshal.AllocHGlobal(alloc);
            try {
                for (int z = 0; z < alloc; z++) Marshal.WriteByte(valBuf, z, 0);
                int hr = Native.BluetoothGATTGetCharacteristicValue(hDev, ref ch, (uint)alloc, valBuf, out reqVal, flags);
                if (hr != 0 || reqVal < 5) return null;
                int copyLen = Math.Min((int)reqVal, alloc);
                byte[] raw = new byte[copyLen];
                Marshal.Copy(valBuf, raw, 0, copyLen);
                int pct;
                if (GattPresentationFormat.TryParseCharacteristicLevel(raw, out pct))
                    return pct;
                return null;
            } finally {
                Marshal.FreeHGlobal(valBuf);
            }
        }
    }

    public sealed class EarbudConnection : IDisposable {
        static readonly IntPtr Invalid=new IntPtr(-1);
        IntPtr handle=Invalid;
        IntPtr rcspHandle=Invalid;
        readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        Parser parser=new Parser(); byte version=3;
        RcspParser rcspParser=new RcspParser(); byte rcspSeq=0;
        public int Mode { get; private set; }
        public ModeState LastModeState { get; private set; }
        public AdaptiveState Adaptive { get; private set; }
        public BatterySnapshot RcspBattery { get; private set; }
        public bool Ready { get; private set; }
        public Action<string> Trace;
        static EarbudConnection() {
            IntPtr data=Marshal.AllocHGlobal(512);
            try { int e=Native.WSAStartup(0x0202,data); if(e!=0) throw new Win32Exception(e); }
            finally { Marshal.FreeHGlobal(data); }
        }
        public EarbudConnection() {
            Mode=-1;
            Adaptive=new AdaptiveState();
            RcspBattery=new BatterySnapshot();
        }
        void Log(string text) { if(Trace!=null) Trace(text); }
        byte NextRcspSeq() { byte s=rcspSeq; rcspSeq=(byte)((rcspSeq+1)&0xFF); return s; }

        public async Task ConnectAsync(Earbuds device) {
            await gate.WaitAsync();
            try {
                DisposeSocket(); parser=new Parser(); rcspParser=new RcspParser(); rcspSeq=0;
                handle=Native.socket(32,1,3); if(handle==Invalid) throw Native.Error("创建蓝牙连接失败");
                IntPtr owned=handle;
                var address=new Native.Address { Family=32,BluetoothAddress=device.Address,Service=new Guid("00001101-0000-1000-8000-00805f9b34fb"),Port=0 };
                var task=Task.Run(()=> { var a=address; int rc=Native.connect(owned,ref a,30); if(rc!=0) throw Native.Error("连接控制通道失败"); });
                if(await Task.WhenAny(task,Task.Delay(12000))!=task) {
                    DisposeSocket();
                    var cont=task.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
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
                    TryConnectRcsp(device);
                });
                Log("握手成功 · 模式："+Protocol.ModeName(Mode)+" ("+(LastModeState!=null ? LastModeState.RawHex : "")+")");
            } catch { DisposeSocket(); throw; }
            finally { gate.Release(); }
        }

        void TryConnectRcsp(Earbuds device) {
            try {
                rcspHandle=Native.socket(32,1,3);
                if(rcspHandle==Invalid) return;
                var addr=new Native.Address {
                    Family=32,
                    BluetoothAddress=device.Address,
                    Service=new Guid("0000fd2d-0000-1000-8000-00805f9b34fb"),
                    Port=0
                };
                if(Native.connect(rcspHandle,ref addr,30)!=0) {
                    Log("RCSP (0000fd2d) 连接未建立: WSAError="+Native.WSAGetLastError());
                    CloseRcspSocket();
                    return;
                }
                int rto=500;
                Native.setsockopt(rcspHandle,65535,4102,ref rto,4);
                Native.setsockopt(rcspHandle,65535,4101,ref rto,4);
                Log("RCSP (0000fd2d) 已连接，开始双向认证…");

                byte[] challenge=new byte[16];
                for(int i=0;i<16;i++) challenge[i]=(byte)(0x20+i);
                byte[] authPayload=new byte[17];
                authPayload[0]=0x01;
                Buffer.BlockCopy(challenge,0,authPayload,1,16);
                SendRcsp(Rcsp.Encode(Rcsp.TypePhoneRequest,Rcsp.OpAuthChallenge,NextRcspSeq(),authPayload));
                PumpRcsp(1200,challenge);

                // Query Device Info (Battery L/R/Case + Firmware + VID/PID)
                SendRcsp(Rcsp.BuildGetTargetInfo(NextRcspSeq()));
                PumpRcsp(600,null);

                // Query Adaptive ANC (0x25)
                SendRcsp(Rcsp.BuildGetConfig(NextRcspSeq(),Rcsp.ConfigAdaptiveAnc));
                PumpRcsp(600,null);
            } catch(Exception ex) {
                Log("RCSP 初始化异常: "+ex.Message);
                CloseRcspSocket();
            }
        }

        void SendRcsp(byte[] frame) {
            if(rcspHandle==Invalid) return;
            Log("RCSP TX "+BitConverter.ToString(frame));
            Native.send(rcspHandle,frame,frame.Length,0);
        }

        bool PumpRcsp(int maxWaitMs, byte[] pendingChallenge) {
            if(rcspHandle==Invalid) return false;
            var clock=System.Diagnostics.Stopwatch.StartNew();
            byte[] buf=new byte[1024];
            bool gotAny=false;
            while(clock.ElapsedMilliseconds<maxWaitMs) {
                int n=Native.recv(rcspHandle,buf,buf.Length,0);
                if(n<=0) break;
                gotAny=true;
                foreach(var pkt in rcspParser.Feed(buf,n)) {
                    Log("RCSP RX "+pkt.Type.ToString("X2")+"/"+pkt.Opcode.ToString("X2")+" seq="+pkt.Seq+" "+BitConverter.ToString(pkt.Payload));
                    HandleRcspPacket(pkt,pendingChallenge);
                }
            }
            return gotAny;
        }

        void HandleRcspPacket(RcspPacket pkt, byte[] pendingChallenge) {
            if(pkt.Opcode==Rcsp.OpAuthChallenge && !pkt.IsRequest) {
                if(pendingChallenge!=null && pkt.Payload.Length>=17) {
                    byte[] expected=Rcsp.ComputeChallengeResponse(pendingChallenge);
                    bool match=true;
                    for(int i=0;i<16;i++) if(pkt.Payload[1+i]!=expected[i]) { match=false; break; }
                    if(match) SendRcsp(Rcsp.Encode(Rcsp.TypePhoneRequest,Rcsp.OpAuthConfirm,NextRcspSeq(),0x01,0x00));
                }
            } else if(pkt.Opcode==Rcsp.OpAuthChallenge && pkt.IsRequest && pkt.Payload.Length>=17) {
                byte[] devChal=new byte[16];
                Buffer.BlockCopy(pkt.Payload,1,devChal,0,16);
                byte[] resp=Rcsp.ComputeChallengeResponse(devChal);
                byte[] rpay=new byte[17];
                rpay[0]=0x01;
                Buffer.BlockCopy(resp,0,rpay,1,16);
                SendRcsp(Rcsp.Encode(Rcsp.TypeResponse,Rcsp.OpAuthChallenge,pkt.Seq,rpay));
            } else if(pkt.Opcode==Rcsp.OpAuthConfirm && pkt.IsRequest) {
                SendRcsp(Rcsp.Encode(Rcsp.TypeResponse,Rcsp.OpAuthConfirm,pkt.Seq,0x01));
            } else if(pkt.Opcode==Rcsp.OpReportStatus && pkt.IsRequest) {
                ApplyRcspDeviceOrStatus(pkt.RawFrame);
                SendRcsp(Rcsp.Encode(Rcsp.TypeResponse,Rcsp.OpReportStatus,pkt.Seq));
            } else if(pkt.Opcode==Rcsp.OpGetDeviceInfo && !pkt.IsRequest) {
                ApplyRcspDeviceOrStatus(pkt.RawFrame);
            } else if(pkt.Opcode==Rcsp.OpNotifyConfig && pkt.IsRequest) {
                bool adaptiveEnabled;
                if(Rcsp.TryParseAdaptiveConfig(pkt.Payload,out adaptiveEnabled)) {
                    Adaptive.ProtocolConfirmed=true;
                    Adaptive.Enabled=adaptiveEnabled;
                    Log("RCSP 通知自适应降噪状态: "+(adaptiveEnabled ? "开启" : "关闭"));
                }
                SendRcsp(Rcsp.Encode(Rcsp.TypeResponse,Rcsp.OpNotifyConfig,pkt.Seq));
            } else if(pkt.Opcode==Rcsp.OpGetConfig && !pkt.IsRequest && pkt.Status==0) {
                bool adaptiveEnabled;
                if(Rcsp.TryParseAdaptiveConfig(pkt.Payload,out adaptiveEnabled)) {
                    Adaptive.ProtocolConfirmed=true;
                    Adaptive.Enabled=adaptiveEnabled;
                    Log("RCSP 读取自适应降噪状态: "+(adaptiveEnabled ? "开启" : "关闭"));
                }
            }
        }

        void ApplyRcspDeviceOrStatus(byte[] rawFrame) {
            int? l,r,c; bool lc,rc,cc; string fw,vp;
            if(Rcsp.TryParseDeviceInfoOrStatus(rawFrame,out l,out r,out c,out lc,out rc,out cc,out fw,out vp)) {
                RcspBattery.IsConnected=true;
                RcspBattery.IsRcspLive=true;
                RcspBattery.LeftPercent=l;
                RcspBattery.RightPercent=r;
                RcspBattery.CasePercent=c;
                RcspBattery.LeftCharging=lc;
                RcspBattery.RightCharging=rc;
                RcspBattery.CaseCharging=cc;
                if(!String.IsNullOrEmpty(fw)) RcspBattery.FirmwareVersion=fw;
                if(!String.IsNullOrEmpty(vp)) RcspBattery.VidPidText=vp;
                Log("RCSP 电量同步: L="+BatterySnapshot.FormatSlot(l,lc)+" R="+BatterySnapshot.FormatSlot(r,rc)+" 盒="+BatterySnapshot.FormatSlot(c,cc));
            }
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
            ModeState state;
            if(!ModeState.TryParse(p.Payload,out state)) throw new InvalidOperationException("模式响应格式与已知协议不一致");
            LastModeState=state;
            Adaptive.LastModeHex=state.RawHex;
            return state.Mode;
        }
        public async Task<int> RefreshAsync() {
            await gate.WaitAsync();
            try {
                if(!Ready) throw new InvalidOperationException("请先连接耳机");
                Mode=await Task.Run(()=> {
                    int m=ReadMode();
                    if(rcspHandle!=Invalid) {
                        SendRcsp(Rcsp.BuildGetTargetInfo(NextRcspSeq()));
                        SendRcsp(Rcsp.BuildGetConfig(NextRcspSeq(),Rcsp.ConfigAdaptiveAnc));
                        PumpRcsp(400,null);
                    }
                    return m;
                });
                return Mode;
            }
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
        public async Task<bool> SetAdaptiveAsync(bool enable) {
            await gate.WaitAsync();
            try {
                if(!Ready || rcspHandle==Invalid || !Adaptive.ProtocolConfirmed)
                    throw new InvalidOperationException("RCSP 自适应控制通道尚未就绪，请重新连接耳机");
                return await Task.Run(()=> {
                    byte setSeq=NextRcspSeq();
                    SendRcsp(Rcsp.BuildSetAdaptiveAnc(setSeq,enable));
                    PumpRcsp(600,null);

                    // Explicit GET_CONFIG (0xF3, 0x25) readback verification
                    Adaptive.Enabled=null;
                    SendRcsp(Rcsp.BuildGetConfig(NextRcspSeq(),Rcsp.ConfigAdaptiveAnc));
                    PumpRcsp(600,null);
                    if(!Adaptive.Enabled.HasValue || Adaptive.Enabled.Value!=enable)
                        throw new InvalidOperationException("自适应降噪切换未获耳机回读确认");
                    return Adaptive.Enabled.Value;
                });
            } finally { gate.Release(); }
        }
        void CloseRcspSocket() {
            IntPtr oldRcsp=Interlocked.Exchange(ref rcspHandle,Invalid);
            if(oldRcsp!=Invalid) Native.closesocket(oldRcsp);
        }
        void DisposeSocket() {
            Ready=false; Mode=-1; LastModeState=null;
            Adaptive=new AdaptiveState();
            RcspBattery=new BatterySnapshot();
            CloseRcspSocket();
            IntPtr old=Interlocked.Exchange(ref handle,Invalid);
            if(old!=Invalid) Native.closesocket(old);
        }
        public void Dispose() { DisposeSocket(); }
    }
}
