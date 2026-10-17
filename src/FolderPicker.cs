using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SonySortTool
{
    // 탐색기와 같은 모양의 폴더 선택 창 (.NET 기본 FolderBrowserDialog는 옛날 트리 모양이라 직접 호출)
    static class FolderPicker
    {
        public static string Pick(IWin32Window owner, string title)
        {
            try {
                var dlg = (IFileDialog)new FileOpenDialog();
                uint opts;
                dlg.GetOptions(out opts);
                dlg.SetOptions(opts | 0x20 | 0x40);                  // FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM
                dlg.SetTitle(title);
                if (dlg.Show(owner.Handle) != 0) return null;         // 취소
                IShellItem item;
                dlg.GetResult(out item);
                string path;
                item.GetDisplayName(0x80058000, out path);           // SIGDN_FILESYSPATH
                return path;
            } catch (COMException) {
                using (var fb = new FolderBrowserDialog { Description = title })
                    return fb.ShowDialog(owner) == DialogResult.OK ? fb.SelectedPath : null;
            }
        }

        [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        class FileOpenDialog { }

        // 필요한 메서드까지만 선언 (순서가 COM 인터페이스 순서와 같아야 함)
        [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IFileDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(IntPtr pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem
        {
            void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        }
    }
}
