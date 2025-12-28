using System.Runtime.InteropServices;

namespace Outlook;

internal static class ComRuntime
{
    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(
        ref Guid rclsid,
        IntPtr reserved,
        [MarshalAs(UnmanagedType.Interface)] out object ppunk);

    [DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void CLSIDFromProgID(string lpszProgID, out Guid pclsid);

    public static bool TryGetActiveObject(string progId, out object? comObject)
    {
        comObject = null;
        try
        {
            CLSIDFromProgID(progId, out var clsid);
            GetActiveObject(ref clsid, IntPtr.Zero, out var obj);
            comObject = obj;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
