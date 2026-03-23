using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TerrariaServerModded;

public static class Native
{
    [SupportedOSPlatform("linux")]
    public static class Linux
    {
        [DllImport("libc", EntryPoint = "getuid")]
        internal static extern uint GetUid();
    }
}