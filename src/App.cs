using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace BudsDesk {
    public sealed class Desk {
        readonly Window window;
        readonly EarbudConnection link=new EarbudConnection();
        readonly List<string> log=new List<string>();
        readonly DispatcherTimer poll=new DispatcherTimer();
        Forms.NotifyIcon tray; Forms.ToolStripMenuItem[] modes;
        bool busy,exit; int failures; IntPtr hwnd;
        readonly bool preview;
        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
        T Find<T>(string name) where T:class { return window.FindName(name) as T; }
        public Desk(bool previewOnly) {
            preview=previewOnly;
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml"))
                window=(Window)XamlReader.Load(stream);
            link.Trace=WriteLog;
            Find<Button>("Scan").Click+=async(s,e)=>await Scan();
            Find<Button>("Connect").Click+=async(s,e)=>await Connect();
            Find<Button>("Anc").Click+=async(s,e)=>await SetMode(1);
            Find<Button>("Off").Click+=async(s,e)=>await SetMode(0);
            Find<Button>("Ambient").Click+=async(s,e)=>await SetMode(2);
            Find<Button>("BluetoothSettings").Click+=(s,e)=>{
                try { Process.Start(new ProcessStartInfo("ms-settings:bluetooth") {UseShellExecute=true}); }
                catch(Exception ex) { Status(ex.Message); }
            };
            Find<Button>("Diagnostics").Click+=(s,e)=>ExportLog();
            Find<Button>("About").Click+=(s,e)=>MessageBox.Show(window,
                "BudsDesk v0.1.0\n渝水龙出品\nCopyright © 2026 渝水龙\n\n"+
                "GitHub: zihangliao-cyber/BudsDesk\nMIT 开源许可证\n\n"+
                "协议研究参考：ncr/omarchy-headphones。\n本项目为独立开发的耳机伴侣，与小米无隶属关系。",
                "关于 BudsDesk · 渝水龙",MessageBoxButton.OK,MessageBoxImage.Information);
            window.Loaded+=async(s,e)=>{ if(!preview) await Scan(); };
            window.SourceInitialized+=(s,e)=>{ if(!preview) SetupDesktop(); };
            window.StateChanged+=(s,e)=>{ if(window.WindowState==WindowState.Minimized && tray!=null) window.Hide(); };
            window.Closing+=(s,e)=>{
                if(!exit && tray!=null) { e.Cancel=true; window.Hide(); return; }
                poll.Stop(); link.Dispose();
                if(hwnd!=IntPtr.Zero) { UnregisterHotKey(hwnd,1); UnregisterHotKey(hwnd,2); }
                if(tray!=null) { tray.Visible=false; tray.Dispose(); }
            };
            poll.Interval=TimeSpan.FromSeconds(4);
            poll.Tick+=async(s,e)=>{
                if(busy || !link.Ready) return;
                busy=true;
                try { await link.RefreshAsync(); failures=0; Update(); }
                catch(Exception ex) { failures++; Status("连接已断开："+ex.Message); Update(); }
                finally { busy=false; Update(); }
            };
            Update();
        }
        void SetupDesktop() {
            try {
                tray=new Forms.NotifyIcon { Text="BudsDesk · 小米耳机",Icon=System.Drawing.SystemIcons.Application,Visible=true };
                var menu=new Forms.ContextMenuStrip();
                menu.Items.Add("打开 BudsDesk",null,(s,e)=>Show());
                menu.Items.Add(new Forms.ToolStripSeparator());
                modes=new Forms.ToolStripMenuItem[3];
                for(int i=0;i<3;i++) { int mode=i;
                    modes[i]=new Forms.ToolStripMenuItem(Protocol.ModeName(i));
                    modes[i].Click+=async(s,e)=>await SetMode(mode); menu.Items.Add(modes[i]);
                }
                menu.Items.Add(new Forms.ToolStripSeparator());
                menu.Items.Add("退出",null,(s,e)=>{ exit=true; window.Close(); });
                tray.ContextMenuStrip=menu;
                tray.MouseClick+=(s,e)=>{ if(e.Button==Forms.MouseButtons.Left) Show(); };
                hwnd=new WindowInteropHelper(window).Handle;
                HwndSource.FromHwnd(hwnd).AddHook(Hook);
                bool n=RegisterHotKey(hwnd,1,0x4003,0x4e);
                bool t=RegisterHotKey(hwnd,2,0x4003,0x54);
                if(!n || !t) Status("部分全局快捷键已被其他软件占用，仍可使用按钮和托盘。");
            } catch(Exception ex) { Status("托盘初始化失败："+ex.Message); }
            Update();
        }
        IntPtr Hook(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled) {
            if(message==0x312) {
                handled=true;
                if(w.ToInt32()==1) { var task=SetMode(link.Mode>=0 && link.Mode<=2 ? (link.Mode+1)%3 : 1); }
                if(w.ToInt32()==2) { var task=SetMode(2); }
            }
            return IntPtr.Zero;
        }
        void WriteLog(string text) {
            lock(log) { log.Add(DateTime.Now.ToString("HH:mm:ss.fff")+" "+text); if(log.Count>1000)log.RemoveAt(0); }
        }
        void Status(string message) { Find<TextBlock>("Status").Text=message; WriteLog(message); }
        async Task Scan() {
            if(busy)return; busy=true; Update();
            try {
                var devices=await Task.Run(()=>BluetoothDiscovery.Paired());
                Find<ComboBox>("Devices").ItemsSource=devices;
                if(devices.Count>0)Find<ComboBox>("Devices").SelectedIndex=0;
                Find<TextBlock>("Hint").Text=devices.Count>0 ? "已找到配对耳机，点击连接以读取当前模式。" : "未找到耳机。请先在系统中配对 Xiaomi Buds 5 Pro，再点刷新。";
                Status(devices.Count>0 ? "找到 "+devices.Count+" 副匹配耳机。" : "尚未发现已配对的小米 Buds 5 Pro。");
            } catch(Exception ex) { Status("读取设备失败："+ex.Message); }
            finally {busy=false;Update();}
        }
        async Task Connect() {
            if(busy)return;
            if(link.Ready) { link.Dispose(); poll.Stop(); Status("已断开控制连接，系统音频连接保持不变。"); Update(); return; }
            Earbuds selected=Find<ComboBox>("Devices").SelectedItem as Earbuds;
            if(selected==null) { Status("请先在系统中配对耳机，再刷新列表。"); return; }
            busy=true; Status("正在连接耳机并读取监听模式…"); Update();
            try {
                await link.ConnectAsync(selected); failures=0; poll.Start();
                Status("控制通道已连接，当前为"+Protocol.ModeName(link.Mode)+"模式。");
            } catch(Exception ex) { Status(ex.Message); }
            finally {busy=false;Update();}
        }
        async Task SetMode(int mode) {
            if(busy || !link.Ready) { if(!link.Ready)Status("请先连接耳机。"); return; }
            busy=true;Status("正在切换至"+Protocol.ModeName(mode)+"，等待耳机确认…");Update();
            try { await link.SetAsync(mode); Status("已由耳机确认："+Protocol.ModeName(link.Mode)+"模式。"); }
            catch(Exception ex) { Status("切换失败："+ex.Message); }
            finally {busy=false;Update();}
        }
        void Update() {
            bool ready=link.Ready;
            Find<Button>("Scan").IsEnabled=!busy && !ready;
            Find<Button>("Connect").IsEnabled=!busy;
            Find<Button>("Connect").Content=busy ? "请稍候" : ready ? "断开" : "连接";
            Find<ComboBox>("Devices").IsEnabled=!busy && !ready;
            var names=new[]{"Off","Anc","Ambient"};
            for(int i=0;i<3;i++) {
                var button=Find<Button>(names[i]); button.IsEnabled=ready && !busy;
                button.Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(ready && link.Mode==i ? "#7461E8" : "#F2F3F8"));
                button.Foreground=new SolidColorBrush(ready && link.Mode==i ? Colors.White : (Color)ColorConverter.ConvertFromString("#50596A"));
                if(modes!=null) {modes[i].Enabled=ready && !busy; modes[i].Checked=ready && link.Mode==i;}
            }
            Find<TextBlock>("Badge").Text=ready ? "●  控制已连接" : "●  尚未连接";
            Find<TextBlock>("Badge").Foreground=new SolidColorBrush(ready ? Color.FromRgb(57,150,119) : Color.FromRgb(142,148,162));
            Find<TextBlock>("HeroMode").Text=ready ? "正在使用 · "+Protocol.ModeName(link.Mode) : "等待连接";
            Find<TextBlock>("ModeState").Text=busy ? "正在同步…" : ready ? "已同步耳机状态" : "连接后可调节";
            if(tray!=null)tray.Text=ready ? "BudsDesk · "+Protocol.ModeName(link.Mode) : "BudsDesk · 未连接";
        }
        void ExportLog() {
            var dialog=new Microsoft.Win32.SaveFileDialog { FileName="BudsDesk-diagnostics.txt",Filter="文本日志|*.txt" };
            if(dialog.ShowDialog(window)!=true)return;
            try {
                string text;lock(log)text=String.Join(Environment.NewLine,log);
                File.WriteAllText(dialog.FileName,"BudsDesk 0.1\r\n"+Environment.OSVersion+"\r\n"+text,Encoding.UTF8);
                Status("诊断日志已导出。");
            } catch(Exception ex) { Status("导出失败："+ex.Message); }
        }
        public void Show() { window.Show();window.WindowState=WindowState.Normal;window.Activate(); }
        public void Render(string path) {
            Find<TextBlock>("Status").Text="界面预览 · 未连接真实耳机，未发送蓝牙指令。";
            window.Width=1040;window.Height=900;
            var content=(FrameworkElement)window.Content; window.Content=null;
            var host=new Border { Background=window.Background,Child=content,Width=1040,Height=900,Resources=window.Resources };
            System.Windows.Documents.TextElement.SetFontFamily(host,window.FontFamily);
            host.Measure(new Size(1040,900));host.Arrange(new Rect(0,0,1040,900));host.UpdateLayout();
            var bitmap=new RenderTargetBitmap(1040,900,96,96,PixelFormats.Pbgra32);bitmap.Render(host);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var file=File.Create(path))encoder.Save(file);
        }
    }
    public static class Program {
        static void Report(string path,string text) {File.WriteAllText(path,text,Encoding.UTF8);}
        [STAThread] public static int Main(string[] args) {
            try {
                if(args.Length>0 && args[0]=="--self-test") {
                    string result=SelfTests.Run(); Report(args.Length>1 ? args[1] : "self-test.txt",result);return 0;
                }
                if(args.Length>0 && args[0]=="--probe") {
                    Probe(args.Length>1 ? args[1] : "probe.txt").GetAwaiter().GetResult();return 0;
                }
                if(args.Length>0 && args[0]=="--render") {
                    var app=new Application(); new Desk(true).Render(args.Length>1 ? args[1] : "preview.png"); return 0;
                }
                bool first; using(var mutex=new Mutex(true,"Local\\BudsDesk-Justin-01",out first)) {
                    if(!first) { MessageBox.Show("BudsDesk 已在运行，请点击任务栏托盘中的图标。","BudsDesk");return 0; }
                    var app=new Application { ShutdownMode=ShutdownMode.OnMainWindowClose };
                    var desk=new Desk(false);desk.Show();app.Run();
                }
                return 0;
            } catch(Exception ex) {
                if(args.Length>1)Report(args[1],ex.ToString());
                else MessageBox.Show(ex.Message,"BudsDesk 启动失败");return 1;
            }
        }
        static async Task Probe(string path) {
            var result=new StringBuilder("BudsDesk read-only probe\r\n");
            result.AppendLine("OS: "+Environment.OSVersion);
            result.AppendLine("Device struct size: "+Marshal.SizeOf(typeof(Native.Device)));
            var devices=BluetoothDiscovery.Paired();result.AppendLine("Matching paired devices: "+devices.Count);
            foreach(var d in devices)result.AppendLine(d.ToString());
            if(devices.Count>0)using(var c=new EarbudConnection()) {
                c.Trace=t=>result.AppendLine(t);
                try {await c.ConnectAsync(devices[0]);result.AppendLine("Mode: "+Protocol.ModeName(c.Mode));}
                catch(Exception ex){result.AppendLine("Connection result: "+ex.Message);}
            }
            Report(path,result.ToString());
        }
    }
}
