using System;
using DeviceFingerprint.Fingerprint;

namespace DeviceFingerprint
{
    internal class Program
    {
        static void Main()
        {
            try
            {
                var fingerprint = FingerPrinter.GetRawDeviceId();
                Console.WriteLine($"Device Fingerprint - {fingerprint}");
            }
            catch (Exception ex)
                when (ex is PlatformNotSupportedException or InvalidOperationException)
            {
                Console.Error.WriteLine($"Unable to determine device fingerprint: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }
}
