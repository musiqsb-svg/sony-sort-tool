using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle(SonySortTool.Product.Name)]
[assembly: AssemblyProduct(SonySortTool.Product.Name)]
[assembly: AssemblyDescription("소니 카메라 영상·사진을 촬영일별 폴더로 정리 (Sony 정식 프로그램이 아닌 개인 제작)")]
[assembly: AssemblyCompany(SonySortTool.Product.Publisher)]
[assembly: AssemblyCopyright("Copyright © 2026 이중필름")]
[assembly: AssemblyVersion(SonySortTool.Product.Version + ".0.0")]
[assembly: AssemblyFileVersion(SonySortTool.Product.Version + ".0.0")]
[assembly: AssemblyInformationalVersion(SonySortTool.Product.Version)]

namespace SonySortTool
{
    static class Program
    {
        public static string Title { get { return Product.Name + " " + Product.Version; } }

        // 폴더 경로를 인자로 받으면 바로 미리보기 (exe 아이콘에 폴더를 끌어다 놓거나 우클릭 메뉴에서 실행)
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ShowError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowError(e.ExceptionObject as Exception);
            Application.Run(new MainForm(args.Length > 0 ? args[0] : null));
        }

        // 오류 내용은 기록 폴더에 남겨 문의할 때 보낼 수 있게 함
        static void ShowError(Exception ex)
        {
            string saved = null;
            try {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Product.Id, "logs");
                Directory.CreateDirectory(dir);
                saved = Path.Combine(dir, "error_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                File.WriteAllText(saved, Title + "\r\n" + Environment.OSVersion + "\r\n\r\n" + ex);
            } catch (Exception) { saved = null; }
            MessageBox.Show("예상하지 못한 오류가 발생했습니다.\n\n" + (ex != null ? ex.Message : "") +
                            (saved != null ? "\n\n자세한 내용을 저장했습니다. 문의할 때 이 파일을 함께 보내 주세요.\n" + saved : ""),
                            Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
