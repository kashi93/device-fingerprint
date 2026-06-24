using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DeviceFingerprint.Fingerprint;

public static class FingerPrinter
{
    private const string MacOsIoregPath = "/usr/sbin/ioreg";

    public static Device GetRawDeviceId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new Device { Id = GetWindowsMachineGuid(), Platform = OSPlatform.Windows };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new Device { Id = GetLinuxMachineId(), Platform = OSPlatform.Linux };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new Device { Id = GetMacOsPlatformUuid(), Platform = OSPlatform.OSX };
        }

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    private static string GetWindowsMachineGuid()
    {
        try
        {
#pragma warning disable CA1416 // Validate platform compatibility
            string? value = Registry
                .GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null)
                ?.ToString();
#pragma warning restore CA1416 // Validate platform compatibility

            return Normalize(value);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return "";
        }
    }

    private static string Normalize(string? value)
    {
        return value?.Trim().ToUpperInvariant() ?? "";
    }

    private static string GetMacOsPlatformUuid()
    {
        string output;

        try
        {
            output = RunCommand(MacOsIoregPath, "-rd1 -c IOPlatformExpertDevice");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return "";
        }

        foreach (string line in output.Split('\n'))
        {
            if (line.Contains("IOPlatformUUID"))
            {
                string[] parts = line.Split('=');

                if (parts.Length >= 2)
                {
                    return Normalize(parts[1].Replace("\"", "").Trim());
                }
            }
        }

        return "";
    }

    private static string GetLinuxMachineId()
    {
        string[] paths = ["/etc/machine-id", "/var/lib/dbus/machine-id"];

        foreach (string path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    return Normalize(File.ReadAllText(path));
                }
            }
            catch (IOException)
            {
                // Try the next candidate path.
            }
        }

        return "";
    }

    private static string RunCommand(string fileName, string arguments)
    {
        using var process = new Process();

        process.StartInfo.FileName = fileName;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;

        process.Start();

        // Read both streams concurrently: reading stdout to completion first can
        // deadlock if the process fills the stderr buffer while waiting to be read.
        Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stdErrTask = process.StandardError.ReadToEndAsync();

        process.WaitForExit();
        Task.WaitAll(stdOutTask, stdErrTask);

        return stdOutTask.Result;
    }
}
