using System;
using System.IO;
using System.Runtime.InteropServices;

namespace OllamaManager.Services;

internal static class MacOSHelper
{
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
    private static extern nint ObjcGetClass(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern nint ObjcSel(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint ObjcSend(nint receiver, nint sel);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint ObjcSendPtr(nint receiver, nint sel, nint arg);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void ObjcSendVoid(nint receiver, nint sel, nint arg);

    internal static void SetDockIcon()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.icns");
        if (!File.Exists(iconPath)) return;

        try
        {
            var pathPtr = Marshal.StringToCoTaskMemUTF8(iconPath);
            try
            {
                var nsStr  = ObjcSendPtr(ObjcGetClass("NSString"), ObjcSel("stringWithUTF8String:"), pathPtr);
                var alloc  = ObjcSend(ObjcGetClass("NSImage"), ObjcSel("alloc"));
                var img    = ObjcSendPtr(alloc, ObjcSel("initWithContentsOfFile:"), nsStr);
                if (img == 0) return;
                var nsApp  = ObjcSend(ObjcGetClass("NSApplication"), ObjcSel("sharedApplication"));
                ObjcSendVoid(nsApp, ObjcSel("setApplicationIconImage:"), img);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pathPtr);
            }
        }
        catch { }
    }
}
