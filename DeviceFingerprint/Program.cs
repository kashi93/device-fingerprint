using System;
using DeviceFingerprint.Fingerprint;

namespace DeviceFingerprint
{
    internal class Program
    {
        static void Main()
        {
            var fingerprint = FingerPrinter.GetRawDeviceId();
            Console.WriteLine($"Device Fingerprint — {fingerprint}");
        }
    }
}
