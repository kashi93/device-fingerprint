using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DeviceFingerprint.Fingerprint;

public static class FingerPrinter
{
    private const string MacOsIoregPath = "/usr/sbin/ioreg";

    public static Device GetRawDeviceId()
    {
        OSPlatform platform = GetCurrentPlatform();

        string id;

        try
        {
            if (platform == OSPlatform.Windows)
            {
                id = GetWindowsMachineGuid();
            }
            else if (platform == OSPlatform.Linux)
            {
                id = GetLinuxMachineId();
            }
            else if (platform == OSPlatform.OSX)
            {
                id = GetMacOsPlatformUuid();
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported operating system.");
            }
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException
            or Win32Exception or InvalidOperationException)
        {
            // The native device id could not be read (e.g. denied registry access, sandboxed
            // ioreg, missing machine-id file). Fall back to an id derived from hardware signals
            // so devices that hit this path don't all collapse to the same identifier.
            id = GetHardwareFallbackId();
        }

        return new Device { Id = id, Platform = platform };
    }

    private static OSPlatform GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return OSPlatform.Windows;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return OSPlatform.Linux;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return OSPlatform.OSX;
        }

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    private static string GetHardwareFallbackId()
    {
        string[] macAddresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback
                && nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .Select(nic => nic.GetPhysicalAddress().ToString())
            .Where(mac => !string.IsNullOrEmpty(mac) && mac != "000000000000")
            .Distinct()
            .OrderBy(mac => mac, StringComparer.Ordinal)
            .ToArray();

        if (macAddresses.Length == 0)
        {
            throw new InvalidOperationException(
                "Unable to derive a fallback device id: no usable network adapter MAC addresses found.");
        }

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', macAddresses)));
        var guid = new Guid(hash[..16]);

        return Normalize(guid.ToString());
    }

    private static string GetWindowsMachineGuid()
    {
#pragma warning disable CA1416 // Validate platform compatibility
        string? value = Registry
            .GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null)
            ?.ToString();
#pragma warning restore CA1416 // Validate platform compatibility

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("MachineGuid not found in registry.");
        }

        return Normalize(value);
    }

    private static string Normalize(string? value)
    {
        return value?.Trim().ToUpperInvariant() ?? "";
    }

    private static string GetMacOsPlatformUuid()
    {
        string output = RunCommand(MacOsIoregPath, "-rd1 -c IOPlatformExpertDevice");

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

        throw new InvalidOperationException("IOPlatformUUID not found in ioreg output.");
    }

    private static string GetLinuxMachineId()
    {
        string[] paths = ["/etc/machine-id", "/var/lib/dbus/machine-id"];
        IOException? lastError = null;

        foreach (string path in paths)
        {
            try
            {
                if (File.Exists(path))
                {
                    string value = Normalize(File.ReadAllText(path));

                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }
            }
            catch (IOException ex)
            {
                lastError = ex;
            }
        }

        throw new IOException("Unable to determine Linux machine id from known paths.", lastError);
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
