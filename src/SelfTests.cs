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
            return "PASS: "+count+" checks. Protocol framing, fragmentation, coalescing, invalid-header recovery, strict mode allowlist, target filtering and Windows native layouts.\r\nHardware behavior is not established by these tests.";
        }
    }
}
