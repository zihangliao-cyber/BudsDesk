# BudsDesk v0.2.0 · 渝水龙

**渝水龙出品 · Copyright © 2026 渝水龙**

![界面预览](docs/preview.png)

[v0.2.0 发布与下载](https://github.com/zihangliao-cyber/BudsDesk/releases/tag/v0.2.0) · [v0.1.0 历史版本](https://github.com/zihangliao-cyber/BudsDesk/releases/tag/v0.1.0)

小米 Buds 5 Pro 蓝牙版的 Windows 桌面控制程序。Windows 10/11 x64，使用系统 .NET Framework 4.8；无需安装 Python、Node 或额外蓝牙驱动。

## 使用

1. 在 Windows 蓝牙设置中配对并连接 Xiaomi Buds 5 Pro。
2. 双击 `dist/BudsDesk.exe`。
3. 选中耳机，点击“连接”。握手成功后：
   - **降噪 / 关闭 / 通透** 按钮可用（通过标准 SPP `00001101` Compact GAIA v3 协议控制并回读确认）。
   - **自适应降噪** 复选框在 `0000fd2d`（`miwear` / RCSP）双向认证与状态读取成功后自动启用，支持开启/关闭与 `0xF3 / 0x25` 状态回读确认。
   - **电量卡片** 实时显示左耳（`L`）、右耳（`R`）、充电盒（`盒`，盒盖关闭/未连接时显示 `—`），并在右上角独立标注 **Windows 报告的耳机电量**。
4. 关闭窗口会保留托盘；右键托盘可切模式或退出。

快捷键：`Ctrl+Alt+N` 循环模式；`Ctrl+Alt+T` 通透。快捷键与按钮都需要控制连接已建立。

## 实机验证结果（本机 `Xiaomi Buds 5 Pro`，`VID=0x2717, PID=0x50AD`，固件 `1.5.3.2`）

1. **监听模式切换（标准 SPP `00001101-0000-1000-8000-00805f9b34fb`，Compact GAIA v3）**：
   - 握手：`FF-03-00-00-00-0A-03-00` -> `000A/8300`（`00-03-03-01`）
   - 读取模式：`FF-03-00-00-00-1D-10-03` -> `001D/1103`（`01-01-01-00-00` 降噪）
   - 设置模式：`001D/1004`（仅允许 `00` 关闭、`01` 降噪、`02` 通透，严禁发送 `03`/`04`），收到 `1104` 后再次读取 `1103` 校验一致。
2. **自适应降噪读取、开关与回读确认（独立通道 `0000fd2d-0000-1000-8000-00805f9b34fb`，`miwear` / RCSP）**：
   - 经本机实测，这副 Xiaomi Buds 5 Pro 提供 `0000fd2d` 服务，且可与 `00001101` GAIA 通道同时工作、互不干扰（严禁向 `00001101` 发送 `FE DC BA` RCSP 帧）。
   - 完成 SAFER+ 双向挑战-响应认证（`Opcode 0x50 / 0x51`）后：
     - **读取自适应降噪**：发送 `GET_CONFIG (0xF3)` 查询 `ConfigType.ADAPTIVE_ANC (0x25)`：`FE-DC-BA-C4-F3-00-03-<seq>-00-25-EF`，耳机返回 `FE-DC-BA-04-F3-00-06-00-<seq>-03-00-25-<00|01>-EF`。
     - **切换自适应降噪**：发送 `SET_CONFIG (0xF2)`：`FE-DC-BA-C4-F2-00-05-<seq>-03-00-25-<00|01>-EF`，耳机返回 `NOTIFY_CONFIG (0xC7/0xF4)` 与 `SET_CONFIG RESPONSE (0x04/0xF2)`，随后再次发送 `GET_CONFIG (0xF3, 0x25)` 读回确认状态一致后才更新界面。
3. **电量显示（严格区分左耳、右耳、充电盒与 Windows 系统整体电量）**：
   - **RCSP 实时电量（`0000fd2d`）**：通过 `GET_DEVICE_INFO (0x02)` TLV `0x07` 与 `REPORT_STATUS (0x0E)` TLV `0x00` 读取 3 字节 `[left, right, case]`（低 7 位为 `0..100%`，最高位为充电标志，充电盒未打开/未连接时耳机回报 `0xFF`，界面如实显示 `盒 —`）。实机实测读出 `L 42%  R 41%  盒 —`。
   - **BLE GATT 独立电量（`0000180f-0000-1000-8000-00805f9b34fb`）**：通过 `BluetoothApis.dll` 读取两个 `180F` 服务（句柄 `0x0051` 与 `0x0056`）的 `0x2904` 描述符识别 `0x010D`（左耳）与 `0x010E`（右耳）及 `0x2A19` 电量特征；未建立实时连接时的系统缓存仅在副标题说明，不冒充实时值。
   - **Windows 报告的耳机电量（`CfgMgr32.dll`）**：通过原生 `CM_Get_DevNode_PropertyW` 读取 `DEVPROPKEY {104EA319-6EE2-4701-BD47-8DDBF425BBE5}, PID 2`（优先选取当前已连接 Classic BT / Hands-Free 节点的实时值 `42%`，区分未连接 BLE 节点的旧缓存 `100%`），独立显示为 `Windows 报告的耳机电量：42%`，绝不伪装成左右耳电量。
4. **63 项自检通过**：涵盖 GAIA 分包/合包/异常头恢复、模式白名单、RCSP SAFER+ 认证向量、RCSP `0x25` 自适应降噪与 `0x02`/`0x0E` 电量解析、GATT `0x2904`/`0x2A19` 解析、电量来源隔离规则及 Windows x64 原生结构布局检查。

## 开发

运行 `build.ps1` 编译（需要系统自带 `Framework64/v4.0.30319/csc.exe`）。源代码位于 `src`。

验证命令：

```powershell
.\dist\BudsDesk.exe --self-test tests\result.txt
.\dist\BudsDesk.exe --probe tests\probe.txt
.\dist\BudsDesk.exe --render tests\preview.png
```

`--probe` 只做配对设备枚举、标准 SPP（`00001101`）握手与模式读取、独立 `0000fd2d`（`miwear`）双向认证与只读信息查询，以及 PnP/GATT/RCSP 电量汇总，不修改耳机模式或配置。

协议依据：
- https://github.com/ncr/omarchy-headphones/blob/main/xiaomi-bridge
- https://github.com/ncr/omarchy-headphones/blob/main/PROTOCOL.md
- https://github.com/maniacx/BudsLink
- https://learn.microsoft.com/en-us/windows/win32/api/ws2bth/ns-ws2bth-sockaddr_bth
