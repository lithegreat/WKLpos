using System;
using System.IO;
using System.Runtime.InteropServices;

namespace WanKePos.WinUI.Helpers;

[ComImport]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
    void GetParent(out IShellItem ppsi);
    void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
    void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
    void Compare(IShellItem psi, uint hint, out int piOrder);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct COMDLG_FILTERSPEC
{
    [MarshalAs(UnmanagedType.LPWStr)]
    public string pszName;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string pszSpec;
}

[ComImport]
[Guid("84bccd23-5fde-4cdb-aea4-af64b83d78ab")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileSaveDialog
{
    [PreserveSig]
    int Show(IntPtr parent);

    void SetFileTypes(uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
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
    void AddPlace(IShellItem psi, int fdap);
    void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
    void Close(int hr);
    void SetClientGuid(ref Guid guid);
    void ClearClientData();
    void SetFilter(IntPtr pFilter);
    void SetSaveAsItem(IShellItem psi);
    void SetProperties(IntPtr pStore);
    void SetCollectedProperties(IntPtr pList, int fAppendDefault);
    void GetProperties(out IntPtr ppStore);
    void ApplyProperties(IShellItem psi, IntPtr pStore, IntPtr hwnd, IntPtr pSink);
}

[ComImport]
[Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
[ClassInterface(ClassInterfaceType.None)]
internal class FileSaveDialogRCW
{
}

public static class WindowsSaveFileDialog
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        ref Guid riid,
        out IShellItem ppv);

    /// <summary>
    /// 打开标准 Windows 另存为对话框，并准确定位到指定的默认目录
    /// </summary>
    public static string? Show(IntPtr hwnd, string initialDirectory, string suggestedFileName, string filterName, string filterExt)
    {
        try
        {
            if (!Directory.Exists(initialDirectory))
            {
                Directory.CreateDirectory(initialDirectory);
            }

            var dialog = (IFileSaveDialog)new FileSaveDialogRCW();

            // FOS_FORCEFILESYSTEM = 0x40, FOS_PATHMUSTEXIST = 0x800, FOS_OVERWRITEPROMPT = 0x2
            dialog.SetOptions(0x40 | 0x800 | 0x2);

            var filters = new COMDLG_FILTERSPEC[]
            {
                new COMDLG_FILTERSPEC { pszName = $"{filterName} (*.{filterExt})", pszSpec = $"*.{filterExt}" }
            };
            dialog.SetFileTypes((uint)filters.Length, filters);
            dialog.SetFileTypeIndex(1);
            dialog.SetDefaultExtension(filterExt);
            dialog.SetFileName(suggestedFileName);
            dialog.SetTitle("导出采购单 Excel");

            var shellItemGuid = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");
            if (SHCreateItemFromParsingName(initialDirectory, IntPtr.Zero, ref shellItemGuid, out var folderItem) == 0 && folderItem != null)
            {
                dialog.SetFolder(folderItem);
                dialog.SetDefaultFolder(folderItem);
            }

            var hr = dialog.Show(hwnd);
            if (hr == 0) // S_OK
            {
                dialog.GetResult(out var resultItem);
                if (resultItem != null)
                {
                    // SIGDN_FILESYSPATH = 0x80058000
                    resultItem.GetDisplayName(0x80058000, out var path);
                    return path;
                }
            }
            return null; // 用户取消
        }
        catch
        {
            // 异常兜底：直接返回默认目录下的文件完整路径
            return Path.Combine(initialDirectory, suggestedFileName);
        }
    }
}
