using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace MultiSych.Services.Implementations
{
    public static class PowerStatusHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Win32SystemPowerStatus
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll", EntryPoint = "GetSystemPowerStatus", SetLastError = true)]
        private static extern bool GetSystemPowerStatus(out Win32SystemPowerStatus lpSystemPowerStatus);

        public static bool IsOnBattery()
        {
            if (OperatingSystem.IsWindows())
            {
                if (GetSystemPowerStatus(out var status))
                {
                    return status.ACLineStatus == 0; // 0 means offline (on battery)
                }
                return false;
            }
            else if (OperatingSystem.IsLinux())
            {
                try
                {
                    if (Directory.Exists("/sys/class/power_supply"))
                    {
                        var dirs = Directory.GetDirectories("/sys/class/power_supply");
                        foreach (var dir in dirs)
                        {
                            var name = Path.GetFileName(dir);
                            if (name.StartsWith("AC") || name.Contains("ADP") || name.Contains("mains"))
                            {
                                var onlinePath = Path.Combine(dir, "online");
                                if (File.Exists(onlinePath))
                                {
                                    var content = File.ReadAllText(onlinePath).Trim();
                                    if (content == "0") return true; // Offline -> On battery
                                    if (content == "1") return false; // Online -> Plugged in
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Linux pil güç durumu okunamadı");
                }
                return false;
            }
            else if (OperatingSystem.IsMacOS())
            {
                try
                {
                    var psi = new ProcessStartInfo("pmset", "-g batt")
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                        return output.Contains("Battery Power") || output.Contains("drawing from 'Battery Power'");
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "macOS pil güç durumu okunamadı");
                }
            }
            return false;
        }

        public static int GetBatteryPercent()
        {
            if (OperatingSystem.IsWindows())
            {
                if (GetSystemPowerStatus(out var status))
                {
                    return status.BatteryLifePercent == 255 ? 100 : status.BatteryLifePercent;
                }
                return 100;
            }
            else if (OperatingSystem.IsLinux())
            {
                try
                {
                    if (Directory.Exists("/sys/class/power_supply"))
                    {
                        var dirs = Directory.GetDirectories("/sys/class/power_supply");
                        foreach (var dir in dirs)
                        {
                            var name = Path.GetFileName(dir);
                            if (name.Contains("BAT") || name.Contains("battery"))
                            {
                                var capacityPath = Path.Combine(dir, "capacity");
                                if (File.Exists(capacityPath))
                                {
                                    var content = File.ReadAllText(capacityPath).Trim();
                                    if (int.TryParse(content, out var pct)) return pct;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Linux pil yüzdesi okunamadı");
                }
                return 100;
            }
            else if (OperatingSystem.IsMacOS())
            {
                try
                {
                    var psi = new ProcessStartInfo("pmset", "-g batt")
                    {
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();
                        var parts = output.Split('\t');
                        if (parts.Length > 1)
                        {
                            var pctPart = parts[1].Split(';')[0].Replace("%", "").Trim();
                            if (int.TryParse(pctPart, out var pct)) return pct;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "macOS pil yüzdesi okunamadı");
                }
            }
            return 100;
        }
    }
}
