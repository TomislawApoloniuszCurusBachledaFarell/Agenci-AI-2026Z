using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace SteamAdvisor.McpServer.Services;

public record HardwareSpecs(
    [property: JsonPropertyName("cpu")] string Cpu,
    [property: JsonPropertyName("gpu")] string Gpu,
    [property: JsonPropertyName("ramGb")] int RamGb,
    [property: JsonPropertyName("storageAvailableGb")] long StorageAvailableGb,
    [property: JsonPropertyName("os")] string Os,
    [property: JsonPropertyName("detectedAtUtc")] DateTime DetectedAtUtc
);

public static class HardwareDetector
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    public static HardwareSpecs Detect()
    {
        var cpu = GetCpuName();
        var gpu = GetGpuName();
        var ramGb = GetTotalRamGb();
        var storageGb = GetAvailableStorageGb();
        var os = RuntimeInformation.OSDescription;

        return new HardwareSpecs(
            Cpu: cpu,
            Gpu: gpu,
            RamGb: ramGb,
            StorageAvailableGb: storageGb,
            Os: os,
            DetectedAtUtc: DateTime.UtcNow
        );
    }

    private static string GetCpuName()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var regVal = Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                    "ProcessorNameString",
                    null)?.ToString();

                if (!string.IsNullOrWhiteSpace(regVal))
                    return regVal.Trim();
            }
        }
        catch
        {
            
        }

        return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") 
               ?? $"Generic {Environment.ProcessorCount} Cores CPU";
    }

    private static string GetGpuName()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string? bestGpu = null;

                for (int i = 0; i < 10; i++)
                {
                    var keyPath = $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{{4d36e968-e325-11ce-bfc1-08002be10318}}\{i:D4}";
                    var desc = Registry.GetValue(keyPath, "DriverDesc", null)?.ToString();

                    if (!string.IsNullOrWhiteSpace(desc) && !desc.Contains("Basic", StringComparison.OrdinalIgnoreCase))
                    {
                        bestGpu = desc;
                        if (desc.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                            desc.Contains("Radeon RX", StringComparison.OrdinalIgnoreCase) ||
                            desc.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
                            desc.Contains("Arc", StringComparison.OrdinalIgnoreCase))
                        {
                            return desc;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(bestGpu))
                    return bestGpu;
            }
        }
        catch
        {
            
        }

        return "Zintegrowana karta graficzna";
    }

    private static int GetTotalRamGb()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    return (int)Math.Round((double)memStatus.ullTotalPhys / (1024 * 1024 * 1024));
                }
            }
        }
        catch
        {
           
        }

        return 16;
    }

    private static long GetAvailableStorageGb()
    {
        try
        {
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(systemDrive);
            if (drive.IsReady)
            {
                return drive.AvailableFreeSpace / (1024 * 1024 * 1024);
            }
        }
        catch
        {
          
        }

        return 100;
    }
}

