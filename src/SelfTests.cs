using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace BudsDesk {
    public static class SelfTests {
        static int count;
        static void Check(bool value,string label) {if(!value)throw new Exception("FAIL: "+label);count++;}
        public static string Run() {
            count=0;
            var get=Protocol.Frame(3,0x001d,0x1003);
            Check(BitConverter.ToString(get)=="FF-03-00-00-00-1D-10-03","wire GET");
            Check(BitConverter.ToString(Protocol.SetMode(3,1))=="FF-03-00-01-00-1D-10-04-01","wire ANC SET");
            foreach(int bad in new[]{-1,3,4,255}) {
                bool rejected=false;try {Protocol.SetMode(3,bad);}catch(ArgumentOutOfRangeException){rejected=true;}
                Check(rejected,"reject unknown mode "+bad);
            }
            var reply=Protocol.Frame(3,0x001d,0x1103,1,1,1,0,0);
            for(int split=1;split<reply.Length;split++) {
                var parser=new Parser();
                Check(parser.Feed(reply.Take(split).ToArray(),split).Count==0,"fragment first "+split);
                var packets=parser.Feed(reply.Skip(split).ToArray(),reply.Length-split);
                Check(packets.Count==1 && packets[0].Payload[0]==1 && packets[0].Command==0x1103,"fragment reassembly "+split);
            }
            var ack=Protocol.Frame(3,0x001d,0x1104);
            var combined=new byte[]{0,4,99}.Concat(ack).Concat(reply).ToArray();
            Check(new Parser().Feed(combined,combined.Length).Count==2,"garbage and coalesced frames");
            var invalid=Protocol.Frame(3,0x001d,0x1104);invalid[2]=1;
            combined=invalid.Concat(reply).ToArray();
            Check(new Parser().Feed(combined,combined.Length).Single().Command==0x1103,"invalid header recovery");
            Check(BluetoothDiscovery.IsTarget("Xiaomi Buds 5 Pro"),"target name");
            Check(!BluetoothDiscovery.IsTarget("Redmi Buds 5 Pro"),"exclude Redmi");
            Check(!BluetoothDiscovery.IsTarget("Xiaomi Buds 5"),"exclude other model");
            Check(Marshal.SizeOf(typeof(Native.Address))==30,"SOCKADDR_BTH size");
            Check(Marshal.OffsetOf(typeof(Native.Address),"BluetoothAddress").ToInt32()==2,"Bluetooth address alignment");
            Check(Marshal.SizeOf(typeof(Native.Device))==560,"BLUETOOTH_DEVICE_INFO layout");

            // ModeState 5-byte 0x1103 parsing
            ModeState ms;
            Check(ModeState.TryParse(new byte[]{1,1,1,0,0},out ms) && ms.Mode==1 && ms.AncDetail==1 && ms.RawHex=="01-01-01-00-00","ModeState ANC parse");
            Check(!ModeState.TryParse(new byte[]{1,1},out ms),"ModeState reject short payload");

            // RCSP SAFER+ authentication hardware vector verification (Xiaomi Buds 5 Pro)
            byte[] chal=new byte[]{0x10,0x11,0x12,0x13,0x14,0x15,0x16,0x17,0x18,0x19,0x1A,0x1B,0x1C,0x1D,0x1E,0x1F};
            Check(BitConverter.ToString(Rcsp.ComputeChallengeResponse(chal))=="3A-3A-64-AE-2D-0E-F3-0C-DD-A3-6A-C1-63-72-95-7C","RCSP SAFER+ challenge response");

            // RCSP Adaptive ANC (ConfigType 0x25) frame encoding & parsing
            Check(BitConverter.ToString(Rcsp.BuildGetConfig(5,Rcsp.ConfigAdaptiveAnc))=="FE-DC-BA-C4-F3-00-03-05-00-25-EF","RCSP GET_CONFIG 0x25");
            Check(BitConverter.ToString(Rcsp.BuildSetAdaptiveAnc(4,true))=="FE-DC-BA-C4-F2-00-05-04-03-00-25-01-EF","RCSP SET_CONFIG 0x25=01");
            Check(BitConverter.ToString(Rcsp.BuildSetAdaptiveAnc(7,false))=="FE-DC-BA-C4-F2-00-05-07-03-00-25-00-EF","RCSP SET_CONFIG 0x25=00");
            bool adp;
            Check(Rcsp.TryParseAdaptiveConfig(new byte[]{0x03,0x00,0x25,0x01},out adp) && adp,"RCSP parse Adaptive ON");
            Check(Rcsp.TryParseAdaptiveConfig(new byte[]{0x03,0x00,0x25,0x00},out adp) && !adp,"RCSP parse Adaptive OFF");
            Check(!Rcsp.TryParseAdaptiveConfig(new byte[]{0x04,0x00,0x0B,0x01,0x0B},out adp),"RCSP ignore non-0x25 config");

            // RCSP GET_DEVICE_INFO (0x02) & REPORT_STATUS (0x0E) battery + firmware + VID/PID parsing
            Check(BitConverter.ToString(Rcsp.BuildGetTargetInfo(2))=="FE-DC-BA-C4-02-00-05-02-FF-FF-FF-FF-EF","RCSP GET_DEVICE_INFO frame");
            int? rl,rr,rc; bool rlc,rrc,rcc; string fw,vp;
            byte[] statusFrame=new byte[]{0xFE,0xDC,0xBA,0xC7,0x0E,0x00,0x09,0x04,0x04,0x00,0x2B,0x2A,0xFF,0x02,0x04,0x01,0xEF};
            Check(Rcsp.TryParseDeviceInfoOrStatus(statusFrame,out rl,out rr,out rc,out rlc,out rrc,out rcc,out fw,out vp)
                && rl==43 && rr==42 && !rc.HasValue,"RCSP REPORT_STATUS 0x0E battery parse");

            byte[] devInfoFrame=new byte[]{
                0xFE,0xDC,0xBA,0x04,0x02,0x00,0x41,0x00,0x02,
                0x12,0x00,0x58,0x69,0x61,0x6F,0x6D,0x69,0x20,0x42,0x75,0x64,0x73,0x20,0x35,0x20,0x50,0x72,0x6F,
                0x05,0x01,0x15,0x32,0x15,0x32,
                0x05,0x03,0x27,0x17,0x50,0xAD,
                0x02,0x04,0x01,0x02,0x05,0x00,
                0x04,0x07,0x2A,(byte)(0x29|0x80),0x55,
                0x02,0x08,0x00,0x02,0x09,0x00,0x02,0x0A,0x01,0x02,0x0B,0x01,0x02,0x0D,0x01,0x02,0x0E,0x03,0x02,0x0F,0x01,
                0xEF
            };
            Check(Rcsp.TryParseDeviceInfoOrStatus(devInfoFrame,out rl,out rr,out rc,out rlc,out rrc,out rcc,out fw,out vp)
                && rl==42 && !rlc && rr==41 && rrc && rc==85 && !rcc && fw=="1.5.3.2" && vp=="2717:50AD","RCSP GET_DEVICE_INFO 0x02 full parse");

            // BLE GATT 0x2904 Presentation Format descriptor parser (Xiaomi Buds 5 Pro 0x0051 Left & 0x0056 Right)
            byte[] leftDesc=new byte[80];
            leftDesc[0]=4; leftDesc[4]=1; leftDesc[8]=0x04; leftDesc[9]=0x29;
            leftDesc[24]=0x04; leftDesc[26]=0xAD; leftDesc[27]=0x27; leftDesc[28]=0x01; leftDesc[29]=0x0D; leftDesc[30]=0x01;
            ushort descVal,unitVal; byte nsVal;
            Check(GattPresentationFormat.TryParseDescriptorValue(leftDesc,out descVal,out unitVal,out nsVal)
                && descVal==GattPresentationFormat.DescriptionLeft && unitVal==GattPresentationFormat.UnitPercentage && nsVal==1,"GATT 0x2904 Left 0x010D");
            byte[] rightDesc=(byte[])leftDesc.Clone();
            rightDesc[29]=0x0E; rightDesc[30]=0x01;
            Check(GattPresentationFormat.TryParseDescriptorValue(rightDesc,out descVal,out unitVal,out nsVal)
                && descVal==GattPresentationFormat.DescriptionRight,"GATT 0x2904 Right 0x010E");

            // BLE GATT 0x2A19 Characteristic Value parser
            int level;
            Check(GattPresentationFormat.TryParseCharacteristicLevel(new byte[]{1,0,0,0,100,0,0,0},out level) && level==100,"GATT 0x2A19 100%");
            Check(!GattPresentationFormat.TryParseCharacteristicLevel(new byte[]{1,0,0,0,255,0,0,0},out level),"GATT 0x2A19 reject 0xFF");

            // BatterySnapshot strict separation & honesty rules
            var winOnly=new BatterySnapshot { IsConnected=true,WindowsPercent=42 };
            Check(winOnly.FormatMainLine()=="L  —    R  —    盒  —","Do not disguise Windows overall battery as L/R");
            Check(winOnly.FormatOverallLine()=="Windows 报告的耳机电量：42%","Explicit Windows battery label");

            var liveLr=new BatterySnapshot { IsConnected=true,IsRcspLive=true,LeftPercent=42,RightPercent=41,WindowsPercent=42,FirmwareVersion="1.5.3.2" };
            Check(liveLr.FormatMainLine()=="L  42%    R  41%    盒  —","Live L/R with unknown case as dash");

            var disconnectedCache=new BatterySnapshot { IsConnected=false,CachedLeftPercent=100,CachedRightPercent=100,WindowsPercent=100 };
            Check(disconnectedCache.FormatMainLine()=="L  —    R  —    盒  —","Disconnected main line shows dash");
            Check(disconnectedCache.FormatOverallLine()=="Windows 报告的耳机电量：100% (缓存)","Disconnected overall label marks cache");

            // Windows native struct layouts for CfgMgr32 / SetupAPI / BluetoothApis
            Check(Marshal.SizeOf(typeof(Native.DevPropKey))==20,"DEVPROPKEY layout");
            Check(Marshal.SizeOf(typeof(Native.BthLeUuid))==20,"BTH_LE_UUID layout");
            Check(Marshal.SizeOf(typeof(Native.GattCharacteristic))==36,"BTH_LE_GATT_CHARACTERISTIC layout");
            Check(Marshal.SizeOf(typeof(Native.GattDescriptor))==32,"BTH_LE_GATT_DESCRIPTOR layout");

            return "PASS: "+count+" checks. Protocol framing, fragmentation, coalescing, invalid-header recovery, strict mode allowlist, target filtering, ModeState 1103 parsing, RCSP SAFER+ auth, RCSP 0x25 Adaptive ANC & 0x02/0x0E L/R/Case battery parsing, BLE GATT 0x2904/0x2A19 L/R battery parsing, strict battery separation, and Windows native layouts.\r\nHardware behavior is not established by these tests.";
        }
    }
}
