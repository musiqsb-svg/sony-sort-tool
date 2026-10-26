// 설치·제거 프로그램 — 같은 소스를 두 번 컴파일함 (build.ps1)
//   /define:INSTALLER → dist\Setup.exe     = 배포 파일 'SONY 타입 정리기 설치.exe' (프로그램·제거 프로그램을 안에 담음)
//   그 밖            → dist\Uninstall.exe = 배포 파일 'SONY 타입 정리기 제거.exe', 설치 폴더의 Uninstall.exe ([설정 › 앱]에서 실행)
//
//  설치: 관리자 권한 없이 지금 사용자에게만
//    %LOCALAPPDATA%\Programs\SonySortTool\  SonySortTool.exe · Uninstall.exe · LICENSE.txt
//    시작 메뉴 'SONY 타입 정리기' · 바탕화면 아이콘(선택) · 폴더 우클릭 'SONY 타입 정리기로 정리'(선택) · [설정 › 앱]의 제거 항목
//    이전 버전 위에 그대로 덮어씀 (설정·정리 기록 유지). 1.4까지의 Inno Setup 설치본도 같은 자리에서 이어받고 옛 제거 파일(unins000.*)은 지움
//    옛 이름('소니정리툴')의 바로가기는 이 프로그램을 가리키는 것만 지우고 새 이름으로 만듦
//  제거: 위 항목과 설정·기록(%LOCALAPPDATA%\SonySortTool)을 지움. 정리한 영상·사진 폴더는 건드리지 않음
//
//  명령줄: /silent  창 없이   ·  /nodesktop /nomenu  설치 옵션 끄기
//          /sandbox=<폴더>  테스트용: 설치 위치·바로가기·설정 폴더를 그 폴더 안으로, 레지스트리는 HKCU\Software\SonySortToolSandbox 아래로
//  끝 코드: 0 성공 · 1 실패 · 2 프로그램이 실행 중 · 3 (제거) 설치되어 있지 않음
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

#if INSTALLER
[assembly: AssemblyTitle(SonySortTool.Product.Name + " 설치")]
[assembly: AssemblyDescription(SonySortTool.Product.Name + " 설치 프로그램")]
#else
[assembly: AssemblyTitle(SonySortTool.Product.Name + " 제거")]
[assembly: AssemblyDescription(SonySortTool.Product.Name + " 제거 프로그램")]
#endif
[assembly: AssemblyProduct(SonySortTool.Product.Name)]
[assembly: AssemblyCompany(SonySortTool.Product.Publisher)]
[assembly: AssemblyCopyright("Copyright © 2026 이중필름")]
[assembly: AssemblyVersion(SonySortTool.Product.Version + ".0.0")]
[assembly: AssemblyFileVersion(SonySortTool.Product.Version + ".0.0")]
[assembly: AssemblyInformationalVersion(SonySortTool.Product.Version)]

namespace SonySortTool
{
    static class SetupMain
    {
#if INSTALLER
        public static readonly bool Installer = true;
#else
        public static readonly bool Installer = false;
#endif

        [STAThread]
        static int Main(string[] args)
        {
            var place = Place.Find(Value(args, "/sandbox="));
            Setup.CleanTemp();
            if (Has(args, "/silent")) {
                try {
                    return Installer ? Setup.SilentInstall(place, !Has(args, "/nodesktop") && place.DefaultDesktop, !Has(args, "/nomenu") && place.DefaultMenu)
                                     : Setup.SilentUninstall(place);
                } finally { Setup.DeleteMovedSelf(); }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new SetupForm(place)); }
            finally { Setup.DeleteMovedSelf(); }
            return 0;
        }

        static bool Has(string[] args, string name) { return args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase)); }

        static string Value(string[] args, string prefix)
        {
            var a = args.FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            return a == null ? null : Path.GetFullPath(a.Substring(prefix.Length).Trim('"'));
        }
    }

    // 설치에 쓰는 위치. sandbox면 모두 그 폴더 안과 별도 레지스트리 키 (실제 설치본을 건드리지 않는 테스트용)
    class Place
    {
        public string App, Data, StartMenu, Desktop, Reg, Sandbox;
        public string[] OldLinks;                // 옛 이름의 바로가기: 시작 메뉴 · 바탕화면 · 1.1의 '제거' 항목
        public string InstalledVersion;          // 설치된 버전. 설치되어 있지 않으면 null
        public bool DefaultDesktop = true, DefaultMenu = true;   // 설치 옵션의 처음 값 (업데이트면 지금 상태 그대로)

        public string Exe          { get { return Path.Combine(App, Product.Exe); } }
        public string Uninstaller  { get { return Path.Combine(App, "Uninstall.exe"); } }
        public string UninstallKey { get { return Reg + @"\Microsoft\Windows\CurrentVersion\Uninstall\" + Product.UninstallKey; } }
        public string MenuKey      { get { return Reg + @"\Classes\Directory\shell\" + Product.Id; } }
        public string MenuBackKey  { get { return Reg + @"\Classes\Directory\Background\shell\" + Product.Id; } }
        public string Args         { get { return Sandbox == null ? "" : " \"/sandbox=" + Sandbox + "\""; } }
        public bool Installed      { get { return InstalledVersion != null || File.Exists(Exe); } }

        public static Place Find(string sandbox)
        {
            var p = new Place { Sandbox = sandbox };
            string local    = sandbox != null ? Path.Combine(sandbox, "LocalAppData") : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programs = sandbox != null ? Path.Combine(sandbox, "StartMenu") : Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            string desktop  = sandbox != null ? Path.Combine(sandbox, "Desktop") : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            p.Reg = sandbox != null ? @"Software\" + Product.Id + "Sandbox" : "Software";
            p.StartMenu = Path.Combine(programs, Product.Name + ".lnk");
            p.Desktop = Path.Combine(desktop, Product.Name + ".lnk");
            p.OldLinks = new[] { Path.Combine(programs, Product.OldName + ".lnk"), Path.Combine(desktop, Product.OldName + ".lnk"),
                                 Path.Combine(programs, Product.OldName + " 제거.lnk") };
            p.Data = Path.Combine(local, Product.Id);
            p.App = Path.Combine(local, "Programs", Product.Id);
            try {
                using (var k = Registry.CurrentUser.OpenSubKey(p.UninstallKey)) {
                    if (k != null) {
                        p.InstalledVersion = k.GetValue("DisplayVersion") as string ?? "";
                        var at = k.GetValue("InstallLocation") as string;    // 1.4까지는 설치할 때 위치를 고를 수 있었음
                        if (!string.IsNullOrWhiteSpace(at)) p.App = at.Trim().TrimEnd('\\');
                    }
                }
            } catch (Exception) { }
            if (p.Installed) {
                p.DefaultDesktop = File.Exists(p.Desktop) || File.Exists(p.OldLinks[1]);   // 옛 이름의 바탕화면 아이콘도 있던 것으로 봄
                try { using (var k = Registry.CurrentUser.OpenSubKey(p.MenuKey)) p.DefaultMenu = k != null; } catch (Exception) { }
            }
            return p;
        }
    }

    static class Setup
    {
        static readonly string[] Files = { Product.Exe, "Uninstall.exe", "LICENSE.txt" };
        static readonly Regex InnoFile = new Regex(@"^unins\d{3}\.(exe|dat|msg)$", RegexOptions.IgnoreCase);   // Inno Setup의 제거 파일
        static string movedSelf;    // 설치 폴더에서 비켜난 제거 프로그램 (끝나면 지움)

        // ── 설치 ──────────────────────────────────────────────────
        public static void Install(Place p, bool desktop, bool menu)
        {
            Directory.CreateDirectory(p.App);
            Extract("payload.app", p.Exe);
            Extract("payload.uninstall", p.Uninstaller);
            Extract("payload.license", Path.Combine(p.App, "LICENSE.txt"));
            foreach (var f in InnoFiles(p)) Delete(f);

            DeleteOldLinks(p);
            Link(p.StartMenu, p.Exe);
            if (desktop) Link(p.Desktop, p.Exe); else Delete(p.Desktop);

            if (menu) WriteMenu(p); else DeleteMenu(p);
            WriteUninstallEntry(p);
            p.InstalledVersion = Product.Version;
            Notify();
        }

        public static int SilentInstall(Place p, bool desktop, bool menu)
        {
            if (Running(p)) return 2;
            try { Install(p, desktop, menu); return 0; }
            catch (Exception) { return 1; }
        }

        static void Extract(string resource, string path)
        {
            string tmp = path + ".new";
            try {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)) {
                    if (s == null) throw new InvalidOperationException("설치 파일 안의 '" + resource + "'을(를) 찾지 못했습니다. 설치 파일을 다시 받아 주세요.");
                    using (var f = File.Create(tmp)) s.CopyTo(f);
                }
                if (File.Exists(path)) File.Copy(tmp, path, true);
                else File.Move(tmp, path);
            } finally { Delete(tmp); }
        }

        static void WriteMenu(Place p)
        {
            string icon = "\"" + p.Exe + "\",0";
            using (var k = Registry.CurrentUser.CreateSubKey(p.MenuKey)) {
                k.SetValue("MUIVerb", Product.MenuText);
                k.SetValue("Icon", icon);
                k.SetValue("MultiSelectModel", "Single");
                using (var c = k.CreateSubKey("command")) c.SetValue("", "\"" + p.Exe + "\" \"%1\"");
            }
            using (var k = Registry.CurrentUser.CreateSubKey(p.MenuBackKey)) {
                k.SetValue("MUIVerb", Product.MenuText);
                k.SetValue("Icon", icon);
                using (var c = k.CreateSubKey("command")) c.SetValue("", "\"" + p.Exe + "\" \"%V\"");
            }
        }

        static void DeleteMenu(Place p) { DeleteKey(p.MenuKey); DeleteKey(p.MenuBackKey); }

        // [설정 › 앱]에 보이는 항목. Inno Setup이 남긴 값까지 지우고 새로 씀
        static void WriteUninstallEntry(Place p)
        {
            DeleteKey(p.UninstallKey);
            using (var k = Registry.CurrentUser.CreateSubKey(p.UninstallKey)) {
                k.SetValue("DisplayName", Product.Name);
                k.SetValue("DisplayVersion", Product.Version);
                k.SetValue("Publisher", Product.Publisher);
                k.SetValue("DisplayIcon", p.Exe);
                k.SetValue("InstallLocation", p.App + "\\");
                k.SetValue("UninstallString", "\"" + p.Uninstaller + "\"" + p.Args);
                k.SetValue("QuietUninstallString", "\"" + p.Uninstaller + "\" /silent" + p.Args);
                k.SetValue("URLInfoAbout", Product.Home);
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("EstimatedSize", (int)(Files.Select(f => Path.Combine(p.App, f)).Where(File.Exists).Sum(f => new FileInfo(f).Length) / 1024), RegistryValueKind.DWord);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        // ── 제거 ──────────────────────────────────────────────────
        // 지우지 못한 항목을 돌려줌 (모두 지웠으면 빈 목록)
        public static List<string> Uninstall(Place p)
        {
            var left = new List<string>();
            DeleteOldLinks(p);
            MoveSelfAway(p);
            foreach (var f in Files.Select(n => Path.Combine(p.App, n)).Concat(InnoFiles(p)))
                if (!DeleteRetry(f)) left.Add(f);
            try { if (Directory.Exists(p.App) && !Directory.EnumerateFileSystemEntries(p.App).Any()) Directory.Delete(p.App); } catch (Exception) { }

            Delete(p.StartMenu);
            Delete(p.Desktop);
            DeleteMenu(p);
            try { if (Directory.Exists(p.Data)) Directory.Delete(p.Data, true); } catch (Exception) { left.Add(p.Data); }
            // 프로그램이 남았으면 [설정 › 앱] 항목도 남겨 다시 지울 수 있게
            if (!File.Exists(p.Exe)) { DeleteKey(p.UninstallKey); p.InstalledVersion = null; }
            Notify();
            return left;
        }

        public static int SilentUninstall(Place p)
        {
            if (!p.Installed) return 3;
            if (Running(p)) return 2;
            try { return Uninstall(p).Count == 0 ? 0 : 1; }
            catch (Exception) { return 1; }
        }

        // 설치 폴더의 Uninstall.exe로 실행 중이면 자기 파일을 임시 폴더로 옮겨 설치 폴더를 비움
        // (실행 중인 파일은 지울 수 없지만 같은 드라이브 안에서 옮길 수는 있음)
        static void MoveSelfAway(Place p)
        {
            string me = Application.ExecutablePath;
            if (!(Path.GetDirectoryName(me) ?? "").Equals(p.App, StringComparison.OrdinalIgnoreCase)) return;
            string to = Path.Combine(Path.GetTempPath(), Product.Id + "-Uninstall-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".exe");
            try { File.Move(me, to); movedSelf = to; } catch (Exception) { }
        }

        // 프로그램을 닫은 뒤 비켜난 제거 프로그램을 지움 (실행 중에는 지울 수 없으므로 잠시 뒤 따로)
        public static void DeleteMovedSelf()
        {
            if (movedSelf == null) return;
            try {
                Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 >nul & del /f /q \"" + movedSelf + "\"") {
                    CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
            } catch (Exception) { }
        }

        // 전에 지우지 못하고 남은 임시 제거 프로그램 정리
        public static void CleanTemp()
        {
            try { foreach (var f in Directory.GetFiles(Path.GetTempPath(), Product.Id + "-Uninstall-*.exe")) Delete(f); } catch (Exception) { }
        }

        // ── 실행 중인 프로그램 ────────────────────────────────────
        // 설치 폴더의 프로그램이 실행 중인지 (실행 중인 exe는 쓰기용으로 열 수 없음)
        public static bool Running(Place p)
        {
            if (!File.Exists(p.Exe)) return false;
            try { using (File.Open(p.Exe, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) return false; }
            catch (IOException) { return true; }
            catch (Exception) { return false; }
        }

        // 열려 있는 창에 닫기를 요청 (정리하는 중이면 프로그램이 거절함). 닫혔으면 true
        public static bool CloseApp(Place p)
        {
            foreach (var proc in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Product.Exe))) {
                try {
                    if (!string.Equals(proc.MainModule.FileName, p.Exe, StringComparison.OrdinalIgnoreCase)) continue;
                    proc.CloseMainWindow();
                    proc.WaitForExit(6000);
                } catch (Exception) { }
            }
            return !Running(p);
        }

        // ── 도움 ──────────────────────────────────────────────────
        static IEnumerable<string> InnoFiles(Place p)
        {
            try { return Directory.Exists(p.App) ? Directory.GetFiles(p.App).Where(f => InnoFile.IsMatch(Path.GetFileName(f))).ToList() : new List<string>(); }
            catch (Exception) { return new List<string>(); }
        }

        static void Delete(string path)
        {
            try { if (File.Exists(path)) { File.SetAttributes(path, FileAttributes.Normal); File.Delete(path); } } catch (Exception) { }
        }

        // 백신·탐색기가 잠깐 잡고 있는 경우가 있어 몇 번 다시
        static bool DeleteRetry(string path)
        {
            for (int i = 0; i < 6; i++) {
                Delete(path);
                if (!File.Exists(path)) return true;
                Thread.Sleep(400);
            }
            return false;
        }

        static void DeleteKey(string key)
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(key, false); } catch (Exception) { }
        }

        [DllImport("shell32.dll")]
        static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

        // 탐색기에 바로가기·우클릭 메뉴가 바뀐 것을 알림
        static void Notify() { try { SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); } catch (Exception) { } }   // SHCNE_ASSOCCHANGED

        // 바로가기 (.lnk) 만들기: Windows 셸의 ShellLink를 직접 씀 (WScript가 막힌 PC에서도 됨)
        static void Link(string link, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(link));
            var sl = (IShellLinkW)new ShellLink();
            try {
                sl.SetPath(target);
                sl.SetWorkingDirectory(Path.GetDirectoryName(target));
                sl.SetDescription(Product.Summary);
                sl.SetIconLocation(target, 0);
                ((System.Runtime.InteropServices.ComTypes.IPersistFile)sl).Save(link, true);
            } finally { Marshal.ReleaseComObject(sl); }
        }

        // 옛 이름의 바로가기 중 이 프로그램(설치 폴더 안의 파일)을 가리키는 것만 지움 (사용자가 따로 만든 같은 이름은 그대로)
        static void DeleteOldLinks(Place p)
        {
            foreach (var link in p.OldLinks) {
                string target = LinkTarget(link);
                if (target != null && string.Equals(Path.GetDirectoryName(target), p.App, StringComparison.OrdinalIgnoreCase)) Delete(link);
            }
        }

        static string LinkTarget(string link)
        {
            if (!File.Exists(link)) return null;
            var sl = (IShellLinkW)new ShellLink();
            try {
                ((System.Runtime.InteropServices.ComTypes.IPersistFile)sl).Load(link, 0);   // STGM_READ
                var path = new StringBuilder(1024);
                sl.GetPath(path, path.Capacity, IntPtr.Zero, 0);
                return path.Length > 0 ? path.ToString() : null;
            } catch (Exception) { return null; }
            finally { Marshal.ReleaseComObject(sl); }
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        class ShellLink { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription(IntPtr pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory(IntPtr pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments(IntPtr pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation(IntPtr pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }
    }

    // ── 화면: 위쪽 그림 + 단계별 내용 (준비 → 진행 → 완료 / 오류) ──────────────
    class SetupForm : Form
    {
        readonly Place place;
        readonly Panel body;
        readonly Hero hero;
        readonly int pad = Theme.S(32);
        bool busy;
        bool desktop, menu;    // 설치 옵션 (화면을 다시 그려도 고른 값 유지)
        static bool Installer { get { return SetupMain.Installer; } }
        static int S(float v) { return Theme.S(v); }

        public SetupForm(Place place)
        {
            this.place = place;
            Text = Product.Name + (Installer ? " 설치" : " 제거");
            Font = Theme.Font(9.75f);
            BackColor = Theme.Surface;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(520), S(470));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            desktop = place.DefaultDesktop;
            menu = place.DefaultMenu;

            string caption;
            if (Installer)
                caption = place.InstalledVersion == null || place.InstalledVersion == Product.Version || place.InstalledVersion.Length == 0
                    ? "설치 · 버전 " + Product.Version : "업데이트 · " + place.InstalledVersion + " → " + Product.Version;
            else caption = "제거" + (string.IsNullOrEmpty(place.InstalledVersion) ? "" : " · 버전 " + place.InstalledVersion);
            hero = new Hero(Theme.Art("hero.jpg"), Theme.AppIcon(S(48))) { Dock = DockStyle.Top, Height = S(184), Title = Product.Name, Caption = caption };
            body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
            Controls.Add(body);
            Controls.Add(hero);
            FormClosing += (s, e) => { if (busy) e.Cancel = true; };

            if (Installer) ShowReady(null);
            else if (!place.Installed) ShowNotInstalled();
            else ShowReady(null);
        }

        int Width0 { get { return ClientSize.Width - pad * 2; } }

        Stack Begin()
        {
            body.SuspendLayout();
            foreach (Control c in body.Controls.Cast<Control>().ToList()) c.Dispose();
            body.Controls.Clear();
            AcceptButton = CancelButton = null;
            return new Stack(body, pad, Width0, S(24));
        }

        void End() { body.ResumeLayout(); }

        // ── 준비: 설명 · 옵션 · [설치하기] / [제거하기] ──
        Toggle desktopBox, menuBox;
        PillButton main;
        SlimProgress progress;

        void ShowReady(string warning)
        {
            var st = Begin();
            using (var lead = Theme.Font(10.5f, FontStyle.Bold)) {
                string title, detail;
                if (Installer) Describe(out title, out detail);
                else {
                    title = "이 PC에서 " + Product.Name + Product.Eul + " 제거합니다";
                    detail = "프로그램, 시작 메뉴·바탕화면 아이콘, 폴더 우클릭 메뉴와\n설정·정리 기록을 지웁니다.";
                }
                st.Text(title, lead, Theme.Ink, S(6));
                st.Text(detail, Font, Theme.Muted, S(14));
            }
            if (Installer) {
                desktopBox = new Toggle { Text = "바탕화면 아이콘", Font = Font, Checked = desktop };
                menuBox = new Toggle { Text = "폴더 우클릭 메뉴", Font = Font, Checked = menu };
                desktopBox.CheckedChanged += (s, e) => desktop = desktopBox.Checked;
                menuBox.CheckedChanged += (s, e) => menu = menuBox.Checked;
                new ToolTip().SetToolTip(menuBox, "폴더를 우클릭해 '" + Product.MenuText + "'로 바로 엽니다.\nWindows 11은 '더 많은 옵션 표시' 안에 있습니다.");
                int y = st.Y;
                st.Add(desktopBox, pad, desktopBox.PreferredSize.Width, S(28), 0);
                st.Y = y;
                st.Add(menuBox, pad + desktopBox.PreferredSize.Width + S(24), menuBox.PreferredSize.Width, S(28), S(16));
            } else {
                var keep = new Label { BackColor = Theme.Surface };
                keep.Paint += (s, e) => {
                    Theme.Badge(e.Graphics, new Rectangle(0, S(2), S(18), S(18)), '✓', Theme.OkLine);
                    Theme.Text(e.Graphics, "정리해 둔 영상·사진 폴더는 그대로 남습니다.", Font, new Rectangle(S(26), 0, Width0, S(22)), Theme.Ok, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
                };
                st.Add(keep, S(22), S(18));
            }
            if (warning != null) st.Text(warning, Font, Theme.Warn, S(10));

            if (Installer) {
                main = new PillButton { Text = InstallVerb(), Primary = true, Font = Theme.Font(10.5f, FontStyle.Bold) };
                st.Add(main, S(46), S(10));
            } else {
                var cancel = new PillButton { Text = "취소", DialogResult = DialogResult.Cancel };
                cancel.Click += (s, e) => Close();
                main = new PillButton { Text = "제거하기", Primary = true, Fill = Theme.Danger, Font = Theme.Font(10.5f, FontStyle.Bold) };
                int cw = S(120), y = st.Y;
                st.Add(cancel, pad, cw, S(46), 0);
                st.Y = y;
                st.Add(main, pad + cw + S(10), Width0 - cw - S(10), S(46), S(10));
                CancelButton = cancel;
            }
            main.Click += MainClick;
            AcceptButton = main;
            progress = st.Add(new SlimProgress { Visible = false }, S(4), 0);

            // 맨 아래: (설치) 정식 프로그램이 아니라는 안내 · 설치 위치. 알림 글이 길어 자리가 모자라면 생략
            int bottom = body.ClientSize.Height - S(32);
            var small = Theme.Font(8.75f);
            if (Installer && st.Y + S(8) < bottom - S(20)) {
                var notice = new Label { Text = "※ " + Product.Unofficial + " · 개인이 만든 비공식 무료 프로그램", ForeColor = Theme.Muted, BackColor = Theme.Surface,
                                         AutoEllipsis = true, UseMnemonic = false, Font = small, Bounds = new Rectangle(pad, bottom - S(20), Width0, S(20)) };
                body.Controls.Add(notice);
            }
            if (st.Y + S(8) < bottom) {
                var where = new Label { Text = (Installer ? "설치 위치  " : "설치된 곳  ") + place.App, ForeColor = Theme.Faint, BackColor = Theme.Surface,
                                        AutoEllipsis = true, UseMnemonic = false, Font = small, Bounds = new Rectangle(pad, bottom, Width0, S(20)) };
                body.Controls.Add(where);
            }
            End();
            main.Focus();
        }

        void Describe(out string title, out string detail)
        {
            string have = place.InstalledVersion;
            int cmp = Compare(have, Product.Version);
            // 한글은 글자 사이에서도 줄이 바뀌므로 줄은 직접 나눔
            if (have == null) {
                title = Product.Summary;
                detail = "관리자 권한 없이 지금 사용자 계정에 설치됩니다.\n필요 없으면 '" + Product.Name + " 제거'로 언제든 지울 수 있습니다.";
            } else if (cmp < 0) {
                title = "설치된 " + have + " 버전을 " + Product.Version + " 버전으로 업데이트합니다";
                detail = "설정과 정리 기록은 그대로 둡니다.";
            } else if (cmp == 0) {
                title = "이미 " + Product.Version + " 버전이 설치되어 있습니다";
                detail = "다시 설치하면 프로그램과 바로가기·메뉴를 새로 만듭니다.\n설정과 정리 기록은 그대로 둡니다.";
            } else {
                title = "더 최신 버전(" + have + ")이 설치되어 있습니다";
                detail = "이 버전(" + Product.Version + ")으로 바꾸려면 아래 버튼을 누르세요.\n설정과 정리 기록은 그대로 둡니다.";
            }
        }

        string InstallVerb()
        {
            int cmp = Compare(place.InstalledVersion, Product.Version);
            return place.InstalledVersion == null ? "설치하기" : cmp < 0 ? "업데이트하기" : cmp == 0 ? "다시 설치하기" : Product.Version + " 버전으로 설치하기";
        }

        // "1.3" < "1.5". 읽을 수 없는 버전은 오래된 것으로 봄
        static int Compare(string a, string b)
        {
            Version va, vb;
            if (a == null) return -1;
            if (!Version.TryParse(a, out va)) return -1;
            if (!Version.TryParse(b, out vb)) return 1;
            return va.CompareTo(vb);
        }

        async void MainClick(object sender, EventArgs e)
        {
            if (busy) return;
            busy = true;
            main.Enabled = false;
            if (desktopBox != null) desktopBox.Enabled = menuBox.Enabled = false;
            main.Text = Installer ? "설치하는 중…" : "제거하는 중…";
            progress.Visible = true;
            progress.Marquee = true;
            UseWaitCursor = true;

            string error = null, warning = null;
            List<string> left = null;
            var work = Task.Run(() => {
                if (Setup.Running(place) && !Setup.CloseApp(place)) {
                    warning = Product.Name + Product.Ga + " 파일을 옮기는 중이라 닫지 못했습니다. 정리가 끝난 뒤 다시 눌러 주세요.";
                    return;
                }
                try {
                    if (Installer) Setup.Install(place, desktop, menu);
                    else left = Setup.Uninstall(place);
                } catch (Exception ex) { error = ex.Message; }
            });
            await Task.WhenAll(work, Task.Delay(900));    // 너무 빨리 끝나도 진행을 볼 수 있게
            UseWaitCursor = false;
            busy = false;

            if (warning != null) ShowReady(warning);
            else if (error != null) ShowProblem(Installer ? "설치하지 못했습니다" : "제거하지 못했습니다",
                                               error + "\n\n백신이나 다른 프로그램이 막았을 수 있습니다. 잠시 뒤 다시 시도해 주세요.");
            else if (left != null && left.Count > 0) ShowProblem("일부를 지우지 못했습니다",
                                               string.Join("\n", left) + "\n\n다른 프로그램이 쓰고 있을 수 있습니다. 다시 시도하거나 직접 지워 주세요.");
            else ShowDone();
        }

        // ── 완료 ──
        void ShowDone()
        {
            var st = Begin();
            Title(st, '✓', Theme.OkLine, Installer ? "설치를 마쳤습니다" : "제거했습니다");
            if (Installer) {
                string where = "시작 메뉴" + (desktop ? "와 바탕화면" : "") + "의 '" + Product.Name + "'" + Product.Ro + " 열 수 있습니다.";
                if (menu) where += "\n폴더를 우클릭해 '" + Product.MenuText + "'를 눌러도 됩니다.\n(Windows 11은 우클릭 › '더 많은 옵션 표시' 안에 있습니다)";
                st.Text(where, Font, Theme.Muted, S(14));
                var tip = new Card { Fill = Theme.WarnSoft, Border = Color.FromArgb(253, 230, 138), Bar = Theme.WarnLine, Radius = S(10),
                                     Padding = new Padding(S(16), S(9), S(12), S(9)), Font = Font, MessageColor = Theme.Warn,
                                     Message = "SD카드 원본은 PC로 복사한 뒤, 복사한 폴더를 정리하세요.\n결과를 확인할 때까지 카드를 지우거나 포맷하지 마세요." };
                st.Add(tip, tip.HeightFor(Width0), S(18));
                var close = new PillButton { Text = "닫기" };
                close.Click += (s, e) => Close();
                var open = new PillButton { Text = Product.Name + " 시작하기", Primary = true, Font = Theme.Font(10.5f, FontStyle.Bold) };
                open.Click += (s, e) => { try { Process.Start(new ProcessStartInfo(place.Exe) { WorkingDirectory = place.App }); } catch (Exception) { } Close(); };
                Buttons(st, close, open);
                AcceptButton = open;
                CancelButton = close;
                End();
                open.Focus();
            } else {
                st.Text(Product.Name + Product.Gwa + " 바로가기·메뉴·설정을 지웠습니다.\n정리해 둔 영상·사진 폴더는 그대로 있습니다.\n다시 쓰려면 '" + Product.Name + " 설치'를 실행하세요.",
                        Font, Theme.Muted, S(22));
                var close = new PillButton { Text = "닫기", Primary = true, Font = Theme.Font(10.5f, FontStyle.Bold) };
                close.Click += (s, e) => Close();
                st.Add(close, S(46), 0);
                AcceptButton = CancelButton = close;
                End();
                close.Focus();
            }
        }

        void ShowNotInstalled()
        {
            var st = Begin();
            Title(st, 'i', Theme.Faint, "설치된 " + Product.Name + Product.Ga + " 없습니다");
            st.Text("이 PC에는 " + Product.Name + Product.Ga + " 설치되어 있지 않습니다.\n설치 없이 쓰던 " + Product.Exe + "는 그 파일을 지우면 됩니다.",
                    Font, Theme.Muted, S(22));
            var close = new PillButton { Text = "닫기", Primary = true, Font = Theme.Font(10.5f, FontStyle.Bold) };
            close.Click += (s, e) => Close();
            st.Add(close, S(46), 0);
            AcceptButton = CancelButton = close;
            End();
            close.Focus();
        }

        void ShowProblem(string title, string message)
        {
            var st = Begin();
            Title(st, '!', Theme.Danger, title);
            st.Text(message, Font, Theme.Body, S(18));
            var close = new PillButton { Text = "닫기" };
            close.Click += (s, e) => Close();
            var retry = new PillButton { Text = "다시 시도", Primary = true, Font = Theme.Font(10.5f, FontStyle.Bold) };
            retry.Click += (s, e) => ShowReady(null);
            Buttons(st, close, retry);
            AcceptButton = retry;
            CancelButton = close;
            End();
        }

        // 동그라미 표시 + 큰 제목 한 줄
        void Title(Stack st, char kind, Color color, string text)
        {
            var head = new Label { BackColor = Theme.Surface };
            head.Paint += (s, e) => {
                Theme.Badge(e.Graphics, new Rectangle(0, S(2), S(30), S(30)), kind, color);
                using (var f = Theme.Font(13.5f, FontStyle.Bold))
                    Theme.Text(e.Graphics, text, f, new Rectangle(S(42), 0, Width0 - S(42), S(34)), Theme.Ink, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
            };
            st.Add(head, S(34), S(10));
        }

        // [보조 버튼] [주 버튼] 한 줄
        void Buttons(Stack st, PillButton secondary, PillButton primary)
        {
            int cw = S(120), y = st.Y;
            st.Add(secondary, pad, cw, S(46), 0);
            st.Y = y;
            st.Add(primary, pad + cw + S(10), Width0 - cw - S(10), S(46), 0);
        }
    }
}
