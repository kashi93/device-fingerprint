using System.Runtime.InteropServices;

namespace DeviceFingerprint.Fingerprint;

public class Device
{
    public required string Id { get; set; }

    public OSPlatform Platform { get; set; }

    public override string ToString()
    {
        return $"Platform: {Platform}, ID: {Id}";
    }
}
