using System;
using System.Collections.Generic;

namespace BudsDesk {
    public sealed class Packet {
        public byte Version; public ushort Vendor, Command; public byte[] Payload;
    }
    // Independently implemented from the documented Compact GAIA wire format.
    public static class Protocol {
        public static byte[] Frame(byte version, ushort vendor, ushort command, params byte[] payload) {
            if (payload.Length > 255) throw new ArgumentException("Payload too long");
            byte[] b = new byte[8 + payload.Length];
            b[0]=255; b[1]=version; b[3]=(byte)payload.Length;
            b[4]=(byte)(vendor>>8); b[5]=(byte)vendor;
            b[6]=(byte)(command>>8); b[7]=(byte)command;
            Buffer.BlockCopy(payload,0,b,8,payload.Length); return b;
        }
        public static byte[] SetMode(byte version, int mode) {
            // This model's other values are unverified; 0x04 reportedly reboots it.
            if (mode < 0 || mode > 2) throw new ArgumentOutOfRangeException("mode");
            return Frame(version,0x001d,0x1004,(byte)mode);
        }
        public static string ModeName(int mode) {
            return mode==0 ? "关闭" : mode==1 ? "降噪" : mode==2 ? "通透" : "未知模式";
        }
    }
    public sealed class Parser {
        readonly List<byte> pending = new List<byte>();
        public List<Packet> Feed(byte[] bytes, int length) {
            if (length<0 || length>bytes.Length) throw new ArgumentOutOfRangeException("length");
            for(int j=0;j<length;j++) pending.Add(bytes[j]);
            var result = new List<Packet>();
            while(pending.Count>0) {
                if(pending[0]!=255) { pending.RemoveAt(0); continue; }
                if(pending.Count<8) break;
                if(pending[2]!=0 || pending[1]<1 || pending[1]>4) { pending.RemoveAt(0); continue; }
                int size=8+pending[3]; if(pending.Count<size) break;
                result.Add(new Packet { Version=pending[1],Vendor=(ushort)((pending[4]<<8)|pending[5]),
                    Command=(ushort)((pending[6]<<8)|pending[7]),Payload=pending.GetRange(8,size-8).ToArray() });
                pending.RemoveRange(0,size);
            }
            return result;
        }
    }
}
