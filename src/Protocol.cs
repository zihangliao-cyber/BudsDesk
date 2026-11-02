using System;
using System.Collections.Generic;
using System.Text;

namespace BudsDesk {
    public sealed class Packet {
        public byte Version; public ushort Vendor, Command; public byte[] Payload;
    }

    public sealed class ModeState {
        public int Mode;
        public byte Flag1, AncDetail, AmbientDetail, Reserved4;
        public byte[] RawPayload;
        public string RawHex {
            get { return RawPayload == null ? "" : BitConverter.ToString(RawPayload); }
        }
        public static bool TryParse(byte[] payload, out ModeState state) {
            state = null;
            if (payload == null || payload.Length != 5) return false;
            state = new ModeState {
                Mode = payload[0],
                Flag1 = payload[1],
                AncDetail = payload[2],
                AmbientDetail = payload[3],
                Reserved4 = payload[4],
                RawPayload = (byte[])payload.Clone()
            };
            return true;
        }
    }

    public sealed class AdaptiveState {
        public bool ProtocolConfirmed;
        public bool? Enabled;
        public string LastModeHex;
        public string FormatStatus(bool connected) {
            if (!connected) return "连接后读取（RCSP 0xFD2D / 0x25）";
            if (ProtocolConfirmed && Enabled.HasValue)
                return Enabled.Value ? "已开启 · 耳机已确认 (0x25=01)" : "已关闭 · 耳机已确认 (0x25=00)";
            if (!String.IsNullOrEmpty(LastModeHex))
                return "当前仅 GAIA 通道 · 1103: " + LastModeHex;
            return "等待读取自适应状态…";
        }
    }

    public sealed class BatterySnapshot {
        public bool IsConnected;
        public bool IsRcspLive;
        public bool IsLiveGatt;
        public int? LeftPercent;
        public int? RightPercent;
        public int? CasePercent;
        public int? WindowsPercent;
        public int? CachedLeftPercent;
        public int? CachedRightPercent;
        public int? HfpRawValue;
        public bool LeftCharging, RightCharging, CaseCharging;
        public string FirmwareVersion;
        public string VidPidText;

        public static bool IsValidPercent(int value) {
            return value >= 0 && value <= 100;
        }

        public static string FormatSlot(int? percent, bool charging) {
            if (!percent.HasValue || !IsValidPercent(percent.Value)) return "—";
            return percent.Value + "%" + (charging ? "⚡" : "");
        }

        public string FormatMainLine() {
            int? left = IsConnected ? LeftPercent : null;
            int? right = IsConnected ? RightPercent : null;
            int? box = IsConnected ? CasePercent : null;
            return "L  " + FormatSlot(left, LeftCharging)
                + "    R  " + FormatSlot(right, RightCharging)
                + "    盒  " + FormatSlot(box, CaseCharging);
        }

        public string FormatOverallLine() {
            if (WindowsPercent.HasValue && IsValidPercent(WindowsPercent.Value)) {
                return IsConnected
                    ? "Windows 报告的耳机电量：" + WindowsPercent.Value + "%"
                    : "Windows 报告的耳机电量：" + WindowsPercent.Value + "% (缓存)";
            }
            return "Windows 报告的耳机电量：—";
        }

        public string FormatSubLine() {
            if (IsConnected) {
                if (IsRcspLive && (LeftPercent.HasValue || RightPercent.HasValue || CasePercent.HasValue)) {
                    string boxState = CasePercent.HasValue ? "充电盒已同步" : "充电盒未打开/未连接(0xFF)";
                    string fw = !String.IsNullOrEmpty(FirmwareVersion) ? " · 固件 " + FirmwareVersion : "";
                    return "来源：MiWear/RCSP (0xFD2D) 实时读取 · " + boxState + fw;
                }
                if (LeftPercent.HasValue || RightPercent.HasValue) {
                    string gattMode = IsLiveGatt ? "BLE GATT 180F 实时读取 (左0x010D/右0x010E)" : "BLE GATT 180F 驱动缓存 (左0x010D/右0x010E)";
                    string boxNote = CasePercent.HasValue ? "充电盒已读取" : "点击“连接”可建立 0xFD2D 读取实时电量";
                    return "来源：" + gattMode + " · " + boxNote;
                }
                if (WindowsPercent.HasValue) {
                    return "已读到系统整体电量（未伪装为左右耳） · 点击“连接”读取左右耳与充电盒电量";
                }
                return "已连接 · 点击“连接”读取耳机实时电量";
            }
            if (CachedLeftPercent.HasValue || CachedRightPercent.HasValue) {
                return "耳机未连接 · 上次 GATT 缓存 L " + FormatSlot(CachedLeftPercent, false)
                    + " / R " + FormatSlot(CachedRightPercent, false) + "（连接后刷新实时值）";
            }
            if (WindowsPercent.HasValue) {
                return "耳机未连接 · 仅存在上次系统缓存电量（连接后刷新）";
            }
            return "连接耳机后自动读取左右耳、充电盒与系统电量";
        }
    }

    public static class GattPresentationFormat {
        public const ushort UnitPercentage = 0x27AD;
        public const byte NameSpaceBluetoothSig = 0x01;
        public const ushort DescriptionLeft = 0x010D;
        public const ushort DescriptionRight = 0x010E;

        public static bool TryParseDescriptorValue(byte[] raw, out ushort description, out ushort unit, out byte nameSpace) {
            description = 0; unit = 0; nameSpace = 0;
            if (raw == null) return false;
            if (raw.Length == 7) {
                unit = (ushort)(raw[2] | (raw[3] << 8));
                nameSpace = raw[4];
                description = (ushort)(raw[5] | (raw[6] << 8));
                return true;
            }
            if (raw.Length >= 31) {
                int descType = BitConverter.ToInt32(raw, 0);
                if (descType == 4) {
                    unit = (ushort)(raw[26] | (raw[27] << 8));
                    nameSpace = raw[28];
                    description = (ushort)(raw[29] | (raw[30] << 8));
                    return true;
                }
            }
            return false;
        }

        public static bool TryParseCharacteristicLevel(byte[] raw, out int percent) {
            percent = -1;
            if (raw == null || raw.Length < 5) return false;
            uint dataSize = BitConverter.ToUInt32(raw, 0);
            if (dataSize < 1) return false;
            int value = raw[4];
            if (!BatterySnapshot.IsValidPercent(value)) return false;
            percent = value;
            return true;
        }
    }

    public sealed class RcspPacket {
        public byte Type, Opcode, Seq, Status;
        public byte[] Payload;
        public byte[] RawFrame;
        public bool IsRequest { get { return (Type & 0x80) != 0; } }
    }

    public sealed class RcspParser {
        readonly List<byte> pending = new List<byte>();
        public List<RcspPacket> Feed(byte[] bytes, int length) {
            if (length < 0 || length > bytes.Length) throw new ArgumentOutOfRangeException("length");
            for (int i = 0; i < length; i++) pending.Add(bytes[i]);
            var result = new List<RcspPacket>();
            while (pending.Count > 0) {
                if (pending[0] != 0xFE) { pending.RemoveAt(0); continue; }
                if (pending.Count < 8) break;
                if (pending[1] != 0xDC || pending[2] != 0xBA) { pending.RemoveAt(0); continue; }
                int payloadLen = (pending[5] << 8) | pending[6];
                if (payloadLen < 1 || payloadLen > 512) { pending.RemoveAt(0); continue; }
                int totalLen = payloadLen + 8;
                if (pending.Count < totalLen) break;
                if (pending[totalLen - 1] != 0xEF) { pending.RemoveAt(0); continue; }

                byte[] raw = pending.GetRange(0, totalLen).ToArray();
                pending.RemoveRange(0, totalLen);
                byte type = raw[3];
                byte opcode = raw[4];
                bool isReq = (type & 0x80) != 0;
                byte status = isReq ? (byte)0 : raw[7];
                int seqIdx = isReq ? 7 : 8;
                byte seq = seqIdx < totalLen - 1 ? raw[seqIdx] : (byte)0;
                int bodyStart = seqIdx + 1;
                int bodyLen = Math.Max(0, (totalLen - 1) - bodyStart);
                byte[] body = new byte[bodyLen];
                if (bodyLen > 0) Buffer.BlockCopy(raw, bodyStart, body, 0, bodyLen);

                result.Add(new RcspPacket {
                    Type = type,
                    Opcode = opcode,
                    Status = status,
                    Seq = seq,
                    Payload = body,
                    RawFrame = raw
                });
            }
            return result;
        }
    }

    public static class Rcsp {
        public const byte TypePhoneRequest = 0xC4;
        public const byte TypeResponse = 0x04;
        public const byte TypeEarbudsRequest = 0xC0;
        public const byte TypeEarbudsResponse = 0x07;
        public const byte TypeEarbudsNotify = 0xC7;

        public const byte OpGetDeviceInfo = 0x02;
        public const byte OpGetDeviceRunInfo = 0x09;
        public const byte OpReportStatus = 0x0E;
        public const byte OpAuthChallenge = 0x50;
        public const byte OpAuthConfirm = 0x51;
        public const byte OpSetConfig = 0xF2;
        public const byte OpGetConfig = 0xF3;
        public const byte OpNotifyConfig = 0xF4;

        public const byte ConfigAnc = 0x0B;
        public const byte ConfigAdaptiveAnc = 0x25;

        const int Pattern = 0x9999;
        const int Block = 16;
        static readonly byte[] AuthSeq = {
            0x11, 0x22, 0x33, 0x33, 0x22, 0x11, 0x11, 0x22,
            0x33, 0x33, 0x22, 0x11, 0x11, 0x22, 0x33, 0x33
        };
        static readonly int[][] Coefficients = new int[][] {
            new[]{2, 1, 1, 1, 4, 2, 1, 1, 2, 2, 4, 2, 4, 4, 16, 8},
            new[]{2, 1, 1, 1, 4, 2, 1, 1, 1, 1, 2, 1, 2, 2, 8, 4},
            new[]{1, 1, 4, 2, 2, 2, 4, 2, 16, 8, 4, 4, 2, 1, 1, 1},
            new[]{1, 1, 4, 2, 1, 1, 2, 1, 8, 4, 2, 2, 2, 1, 1, 1},
            new[]{16, 8, 2, 2, 4, 2, 4, 4, 1, 1, 4, 2, 1, 1, 2, 1},
            new[]{8, 4, 1, 1, 2, 1, 2, 2, 1, 1, 4, 2, 1, 1, 2, 1},
            new[]{2, 2, 4, 2, 4, 4, 16, 8, 2, 1, 1, 1, 4, 2, 1, 1},
            new[]{1, 1, 2, 1, 2, 2, 8, 4, 2, 1, 1, 1, 4, 2, 1, 1},
            new[]{4, 2, 4, 4, 16, 8, 2, 2, 1, 1, 2, 1, 1, 1, 4, 2},
            new[]{2, 1, 2, 2, 8, 4, 1, 1, 1, 1, 2, 1, 1, 1, 4, 2},
            new[]{4, 4, 16, 8, 1, 1, 2, 1, 4, 2, 1, 1, 4, 2, 2, 2},
            new[]{2, 2, 8, 4, 1, 1, 2, 1, 4, 2, 1, 1, 2, 1, 1, 1},
            new[]{1, 1, 2, 1, 1, 1, 4, 2, 4, 4, 16, 8, 2, 2, 4, 2},
            new[]{1, 1, 2, 1, 1, 1, 4, 2, 2, 2, 8, 4, 1, 1, 2, 1},
            new[]{4, 2, 1, 1, 2, 1, 1, 1, 4, 2, 2, 2, 16, 8, 4, 4},
            new[]{4, 2, 1, 1, 2, 1, 1, 1, 2, 1, 1, 1, 8, 4, 2, 2},
        };
        static readonly byte[] ExpTab = {
            0x01, 0x2D, 0xE2, 0x93, 0xBE, 0x45, 0x15, 0xAE, 0x78, 0x03, 0x87, 0xA4, 0xB8, 0x38, 0xCF, 0x3F,
            0x08, 0x67, 0x09, 0x94, 0xEB, 0x26, 0xA8, 0x6B, 0xBD, 0x18, 0x34, 0x1B, 0xBB, 0xBF, 0x72, 0xF7,
            0x40, 0x35, 0x48, 0x9C, 0x51, 0x2F, 0x3B, 0x55, 0xE3, 0xC0, 0x9F, 0xD8, 0xD3, 0xF3, 0x8D, 0xB1,
            0xFF, 0xA7, 0x3E, 0xDC, 0x86, 0x77, 0xD7, 0xA6, 0x11, 0xFB, 0xF4, 0xBA, 0x92, 0x91, 0x64, 0x83,
            0xF1, 0x33, 0xEF, 0xDA, 0x2C, 0xB5, 0xB2, 0x2B, 0x88, 0xD1, 0x99, 0xCB, 0x8C, 0x84, 0x1D, 0x14,
            0x81, 0x97, 0x71, 0xCA, 0x5F, 0xA3, 0x8B, 0x57, 0x3C, 0x82, 0xC4, 0x52, 0x5C, 0x1C, 0xE8, 0xA0,
            0x04, 0xB4, 0x85, 0x4A, 0xF6, 0x13, 0x54, 0xB6, 0xDF, 0x0C, 0x1A, 0x8E, 0xDE, 0xE0, 0x39, 0xFC,
            0x20, 0x9B, 0x24, 0x4E, 0xA9, 0x98, 0x9E, 0xAB, 0xF2, 0x60, 0xD0, 0x6C, 0xEA, 0xFA, 0xC7, 0xD9,
            0x00, 0xD4, 0x1F, 0x6E, 0x43, 0xBC, 0xEC, 0x53, 0x89, 0xFE, 0x7A, 0x5D, 0x49, 0xC9, 0x32, 0xC2,
            0xF9, 0x9A, 0xF8, 0x6D, 0x16, 0xDB, 0x59, 0x96, 0x44, 0xE9, 0xCD, 0xE6, 0x46, 0x42, 0x8F, 0x0A,
            0xC1, 0xCC, 0xB9, 0x65, 0xB0, 0xD2, 0xC6, 0xAC, 0x1E, 0x41, 0x62, 0x29, 0x2E, 0x0E, 0x74, 0x50,
            0x02, 0x5A, 0xC3, 0x25, 0x7B, 0x8A, 0x2A, 0x5B, 0xF0, 0x06, 0x0D, 0x47, 0x6F, 0x70, 0x9D, 0x7E,
            0x10, 0xCE, 0x12, 0x27, 0xD5, 0x4C, 0x4F, 0xD6, 0x79, 0x30, 0x68, 0x36, 0x75, 0x7D, 0xE4, 0xED,
            0x80, 0x6A, 0x90, 0x37, 0xA2, 0x5E, 0x76, 0xAA, 0xC5, 0x7F, 0x3D, 0xAF, 0xA5, 0xE5, 0x19, 0x61,
            0xFD, 0x4D, 0x7C, 0xB7, 0x0B, 0xEE, 0xAD, 0x4B, 0x22, 0xF5, 0xE7, 0x73, 0x23, 0x21, 0xC8, 0x05,
            0xE1, 0x66, 0xDD, 0xB3, 0x58, 0x69, 0x63, 0x56, 0x0F, 0xA1, 0x31, 0x95, 0x17, 0x07, 0x3A, 0x28
        };
        static readonly byte[] LogTab = {
            0x80, 0x00, 0xB0, 0x09, 0x60, 0xEF, 0xB9, 0xFD, 0x10, 0x12, 0x9F, 0xE4, 0x69, 0xBA, 0xAD, 0xF8,
            0xC0, 0x38, 0xC2, 0x65, 0x4F, 0x06, 0x94, 0xFC, 0x19, 0xDE, 0x6A, 0x1B, 0x5D, 0x4E, 0xA8, 0x82,
            0x70, 0xED, 0xE8, 0xEC, 0x72, 0xB3, 0x15, 0xC3, 0xFF, 0xAB, 0xB6, 0x47, 0x44, 0x01, 0xAC, 0x25,
            0xC9, 0xFA, 0x8E, 0x41, 0x1A, 0x21, 0xCB, 0xD3, 0x0D, 0x6E, 0xFE, 0x26, 0x58, 0xDA, 0x32, 0x0F,
            0x20, 0xA9, 0x9D, 0x84, 0x98, 0x05, 0x9C, 0xBB, 0x22, 0x8C, 0x63, 0xE7, 0xC5, 0xE1, 0x73, 0xC6,
            0xAF, 0x24, 0x5B, 0x87, 0x66, 0x27, 0xF7, 0x57, 0xF4, 0x96, 0xB1, 0xB7, 0x5C, 0x8B, 0xD5, 0x54,
            0x79, 0xDF, 0xAA, 0xF6, 0x3E, 0xA3, 0xF1, 0x11, 0xCA, 0xF5, 0xD1, 0x17, 0x7B, 0x93, 0x83, 0xBC,
            0xBD, 0x52, 0x1E, 0xEB, 0xAE, 0xCC, 0xD6, 0x35, 0x08, 0xC8, 0x8A, 0xB4, 0xE2, 0xCD, 0xBF, 0xD9,
            0xD0, 0x50, 0x59, 0x3F, 0x4D, 0x62, 0x34, 0x0A, 0x48, 0x88, 0xB5, 0x56, 0x4C, 0x2E, 0x6B, 0x9E,
            0xD2, 0x3D, 0x3C, 0x03, 0x13, 0xFB, 0x97, 0x51, 0x75, 0x4A, 0x91, 0x71, 0x23, 0xBE, 0x76, 0x2A,
            0x5F, 0xF9, 0xD4, 0x55, 0x0B, 0xDC, 0x37, 0x31, 0x16, 0x74, 0xD7, 0x77, 0xA7, 0xE6, 0x07, 0xDB,
            0xA4, 0x2F, 0x46, 0xF3, 0x61, 0x45, 0x67, 0xE3, 0x0C, 0xA2, 0x3B, 0x1C, 0x85, 0x18, 0x04, 0x1D,
            0x29, 0xA0, 0x8F, 0xB2, 0x5A, 0xD8, 0xA6, 0x7E, 0xEE, 0x8D, 0x53, 0x4B, 0xA1, 0x9A, 0xC1, 0x0E,
            0x7A, 0x49, 0xA5, 0x2C, 0x81, 0xC4, 0xC7, 0x36, 0x2B, 0x7F, 0x43, 0x95, 0x33, 0xF2, 0x6C, 0x68,
            0x6D, 0xF0, 0x02, 0x28, 0xCE, 0xDD, 0x9B, 0xEA, 0x5E, 0x99, 0x7C, 0x14, 0x86, 0xCF, 0xE5, 0x42,
            0xB8, 0x40, 0x78, 0x2D, 0x3A, 0xE9, 0x64, 0x1F, 0x92, 0x90, 0x7D, 0x39, 0x6F, 0xE0, 0x89, 0x30
        };
        static readonly byte[][] BiasMatrix = new byte[][] {
            new byte[]{0x46, 0x97, 0xB1, 0xBA, 0xA3, 0xB7, 0x10, 0x0A, 0xC5, 0x37, 0xB3, 0xC9, 0x5A, 0x28, 0xAC, 0x64},
            new byte[]{0xEC, 0xAB, 0xAA, 0xC6, 0x67, 0x95, 0x58, 0x0D, 0xF8, 0x9A, 0xF6, 0x6E, 0x66, 0xDC, 0x05, 0x3D},
            new byte[]{0x8A, 0xC3, 0xD8, 0x89, 0x6A, 0xE9, 0x36, 0x49, 0x43, 0xBF, 0xEB, 0xD4, 0x96, 0x9B, 0x68, 0xA0},
            new byte[]{0x5D, 0x57, 0x92, 0x1F, 0xD5, 0x71, 0x5C, 0xBB, 0x22, 0xC1, 0xBE, 0x7B, 0xBC, 0x99, 0x63, 0x94},
            new byte[]{0x2A, 0x61, 0xB8, 0x34, 0x32, 0x19, 0xFD, 0xFB, 0x17, 0x40, 0xE6, 0x51, 0x1D, 0x41, 0x44, 0x8F},
            new byte[]{0xDD, 0x04, 0x80, 0xDE, 0xE7, 0x31, 0xD6, 0x7F, 0x01, 0xA2, 0xF7, 0x39, 0xDA, 0x6F, 0x23, 0xCA},
            new byte[]{0x3A, 0xD0, 0x1C, 0xD1, 0x30, 0x3E, 0x12, 0xA1, 0xCD, 0x0F, 0xE0, 0xA8, 0xAF, 0x82, 0x59, 0x2C},
            new byte[]{0x7D, 0xAD, 0xB2, 0xEF, 0xC2, 0x87, 0xCE, 0x75, 0x06, 0x13, 0x02, 0x90, 0x4F, 0x2E, 0x72, 0x33},
            new byte[]{0xC0, 0x8D, 0xCF, 0xA9, 0x81, 0xE2, 0xC4, 0x27, 0x2F, 0x6C, 0x7A, 0x9F, 0x52, 0xE1, 0x15, 0x38},
            new byte[]{0xFC, 0x20, 0x42, 0xC7, 0x08, 0xE4, 0x09, 0x55, 0x5E, 0x8C, 0x14, 0x76, 0x60, 0xFF, 0xDF, 0xD7},
            new byte[]{0xFA, 0x0B, 0x21, 0x00, 0x1A, 0xF9, 0xA6, 0xB9, 0xE8, 0x9E, 0x62, 0x4C, 0xD9, 0x91, 0x50, 0xD2},
            new byte[]{0x18, 0xB4, 0x07, 0x84, 0xEA, 0x5B, 0xA4, 0xC8, 0x0E, 0xCB, 0x48, 0x69, 0x4B, 0x4E, 0x9C, 0x35},
            new byte[]{0x45, 0x4D, 0x54, 0xE5, 0x25, 0x3C, 0x0C, 0x4A, 0x8B, 0x3F, 0xCC, 0xA7, 0xDB, 0x6B, 0xAE, 0xF4},
            new byte[]{0x2D, 0xF3, 0x7C, 0x6D, 0x9D, 0xB5, 0x26, 0x74, 0xF2, 0x93, 0x53, 0xB0, 0xF0, 0x11, 0xED, 0x83},
            new byte[]{0xB6, 0x03, 0x16, 0x73, 0x3B, 0x1E, 0x8E, 0x70, 0xBD, 0x86, 0x1B, 0x47, 0x7E, 0x24, 0x56, 0xF1},
            new byte[]{0x88, 0x46, 0x97, 0xB1, 0xBA, 0xA3, 0xB7, 0x10, 0x0A, 0xC5, 0x37, 0xB3, 0xC9, 0x5A, 0x28, 0xAC},
        };

        public static byte[] ComputeChallengeResponse(byte[] challenge) {
            if (challenge == null || challenge.Length != 16) throw new ArgumentException("challenge must be 16 bytes");
            byte[] k = (byte[])challenge.Clone();
            k[15] ^= 6;
            var keys = new List<byte[]> { k };
            byte[] reg = new byte[17];
            byte xor = 0;
            for (int i = 0; i < 16; i++) { reg[i] = k[i]; xor ^= k[i]; }
            reg[16] = xor;

            for (int round = 1; round <= 16; round++) {
                for (int idx = 0; idx < 17; idx++) {
                    int v = reg[idx];
                    reg[idx] = (byte)(((v >> 5) | ((v << 3) & 0xFF)) & 0xFF);
                }
                byte[] keyI = new byte[16];
                for (int i = 0; i < 16; i++)
                    keyI[i] = (byte)((reg[(round + i) % 17] + BiasMatrix[round - 1][i]) & 0xFF);
                keys.Add(keyI);
            }

            byte[] ct = (byte[])AuthSeq.Clone();
            for (int round = 0; round < 8; round++) {
                if (round == 2) {
                    for (int i = 0; i < Block; i++) {
                        if (((Pattern >> i) & 1) != 0) ct[i] ^= AuthSeq[i];
                        else ct[i] = (byte)((ct[i] + AuthSeq[i]) & 0xFF);
                    }
                }
                for (int i = 0; i < Block; i++) {
                    if (((Pattern >> i) & 1) != 0) ct[i] ^= keys[round * 2][i];
                    else ct[i] = (byte)((ct[i] + keys[round * 2][i]) & 0xFF);
                }
                for (int i = 0; i < Block; i++) {
                    byte b = ct[i];
                    ct[i] = (((Pattern >> i) & 1) != 0) ? ExpTab[b] : LogTab[b];
                }
                for (int i = 0; i < Block; i++) {
                    if (((Pattern >> i) & 1) != 0) ct[i] = (byte)((keys[round * 2 + 1][i] + ct[i]) & 0xFF);
                    else ct[i] ^= keys[round * 2 + 1][i];
                }
                byte[] copy = (byte[])ct.Clone();
                for (int i = 0; i < Block; i++) {
                    int sum = 0;
                    for (int j = 0; j < Block; j++)
                        sum = (sum + Coefficients[i][j] * copy[j]) & 0xFF;
                    ct[i] = (byte)sum;
                }
            }
            for (int i = 0; i < Block; i++) {
                if (((Pattern >> i) & 1) != 0) ct[i] ^= keys[16][i];
                else ct[i] = (byte)((ct[i] + keys[16][i]) & 0xFF);
            }
            return ct;
        }

        public static byte[] Encode(byte type, byte opcode, byte seq, params byte[] payload) {
            bool isReq = (type & 0x80) != 0;
            int payloadLen = (payload != null ? payload.Length : 0) + (isReq ? 1 : 2);
            var list = new List<byte>(8 + payloadLen) {
                0xFE, 0xDC, 0xBA, type, opcode, (byte)(payloadLen >> 8), (byte)(payloadLen & 0xFF)
            };
            if (!isReq) list.Add(0x00);
            list.Add(seq);
            if (payload != null && payload.Length > 0) list.AddRange(payload);
            list.Add(0xEF);
            return list.ToArray();
        }

        public static byte[] BuildGetTargetInfo(byte seq) {
            return Encode(TypePhoneRequest, OpGetDeviceInfo, seq, 0xFF, 0xFF, 0xFF, 0xFF);
        }

        public static byte[] BuildGetConfig(byte seq, byte configType) {
            return Encode(TypePhoneRequest, OpGetConfig, seq, 0x00, configType);
        }

        public static byte[] BuildSetAdaptiveAnc(byte seq, bool enable) {
            return Encode(TypePhoneRequest, OpSetConfig, seq, 0x03, 0x00, ConfigAdaptiveAnc, (byte)(enable ? 0x01 : 0x00));
        }

        public static bool TryParseAdaptiveConfig(byte[] payload, out bool enabled) {
            enabled = false;
            if (payload == null || payload.Length < 4) return false;
            // Payload format for GET_CONFIG (0xF3) / NOTIFY_CONFIG (0xF4): [len=0x03] [0x00] [config=0x25] [0x00|0x01]
            int idx = 0;
            while (idx + 3 < payload.Length) {
                int len = payload[idx];
                if (len < 3 || idx + 1 + len > payload.Length) break;
                if (payload[idx + 1] == 0x00 && payload[idx + 2] == ConfigAdaptiveAnc) {
                    byte val = payload[idx + 3];
                    if (val == 0x00 || val == 0x01) {
                        enabled = val == 0x01;
                        return true;
                    }
                }
                idx += 1 + len;
            }
            return false;
        }

        public static bool TryParseBatteryPayload(
            byte[] frameOrPayload,
            out int? left, out int? right, out int? box,
            out bool leftCharging, out bool rightCharging, out bool boxCharging) {
            string fw, vp;
            return TryParseDeviceInfoOrStatus(frameOrPayload, out left, out right, out box, out leftCharging, out rightCharging, out boxCharging, out fw, out vp);
        }

        public static bool TryParseDeviceInfoOrStatus(
            byte[] frameOrPayload,
            out int? left, out int? right, out int? box,
            out bool leftCharging, out bool rightCharging, out bool boxCharging,
            out string firmware, out string vidPid) {
            left = null; right = null; box = null;
            leftCharging = false; rightCharging = false; boxCharging = false;
            firmware = null; vidPid = null;
            if (frameOrPayload == null || frameOrPayload.Length < 4) return false;

            byte opcode = 0x02;
            byte[] payload = frameOrPayload;
            if (frameOrPayload.Length >= 8
                && frameOrPayload[0] == 0xFE && frameOrPayload[1] == 0xDC && frameOrPayload[2] == 0xBA
                && frameOrPayload[frameOrPayload.Length - 1] == 0xEF) {
                var packets = new RcspParser().Feed(frameOrPayload, frameOrPayload.Length);
                if (packets.Count == 0) return false;
                opcode = packets[0].Opcode;
                payload = packets[0].Payload;
            }

            bool foundBattery = false;
            int idx = 0;
            while (idx + 1 < payload.Length) {
                int tlvLen = payload[idx];
                if (tlvLen <= 0 || idx + 1 + tlvLen > payload.Length) break;
                byte tag = payload[idx + 1];
                if (opcode == OpGetDeviceInfo && tag == 0x01 && tlvLen >= 5) {
                    byte b0 = payload[idx + 2], b1 = payload[idx + 3];
                    firmware = ((b0 >> 4) & 0xF) + "." + (b0 & 0xF) + "." + ((b1 >> 4) & 0xF) + "." + (b1 & 0xF);
                } else if (opcode == OpGetDeviceInfo && tag == 0x03 && tlvLen >= 5) {
                    ushort vid = (ushort)((payload[idx + 2] << 8) | payload[idx + 3]);
                    ushort pid = (ushort)((payload[idx + 4] << 8) | payload[idx + 5]);
                    vidPid = vid.ToString("X4") + ":" + pid.ToString("X4");
                } else if (((opcode == OpGetDeviceInfo && tag == 0x07) || (opcode == OpReportStatus && tag == 0x00)) && tlvLen >= 4) {
                    DecodeByte(payload[idx + 2], out left, out leftCharging);
                    DecodeByte(payload[idx + 3], out right, out rightCharging);
                    DecodeByte(payload[idx + 4], out box, out boxCharging);
                    foundBattery = left.HasValue || right.HasValue || box.HasValue;
                }
                idx += 1 + tlvLen;
            }
            return foundBattery;
        }

        static void DecodeByte(byte raw, out int? percent, out bool charging) {
            percent = null; charging = false;
            if (raw == 0xFF) return;
            int val = raw & 0x7F;
            if (!BatterySnapshot.IsValidPercent(val)) return;
            percent = val;
            charging = (raw & 0x80) != 0;
        }
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
            // This model's other values are unverified on GAIA 0x1004; 0x04 reportedly reboots it.
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
