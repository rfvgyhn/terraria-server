using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TerrariaServerModded;

public static class Native
{
    private const string AppDir = "terraria-server";
    
    [SupportedOSPlatform("linux")]
    public static class Linux
    {
        [DllImport("libc", EntryPoint = "getuid")]
        private static extern uint GetUid();
        
        public static string FindDefaultSocketDir()
        {
            var path = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? $"/run/user/${GetUid()}";
            return Path.Combine(path, AppDir);
        }
    }
}