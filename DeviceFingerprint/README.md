# DeviceFingerprint

A small .NET console sample that retrieves a stable, OS-level identifier for the current machine — useful as a building block for device fingerprinting.

## How it works

`FingerPrinter.GetRawDeviceId()` detects the current OS and reads a platform-specific machine identifier:

| Platform | Source |
|----------|--------|
| Windows  | `MachineGuid` from `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography` |
| macOS    | `IOPlatformUUID` via `ioreg -rd1 -c IOPlatformExpertDevice` |
| Linux    | `/etc/machine-id`, falling back to `/var/lib/dbus/machine-id` |

The result is wrapped in a `Device` record containing the normalized ID and the detected `OSPlatform`. If the ID can't be read (permissions, missing file, etc.) an empty string is returned instead of throwing; an unsupported OS throws `PlatformNotSupportedException`.

## Requirements

- .NET 10 SDK

## Running

```bash
dotnet run
```

Example output:

```
Device Fingerprint — Platform: OSX, ID: 8B3F2C1A-....
```

## Publishing self-contained builds

```bash
dotnet publish -c Release -r win-x64 --self-contained true
dotnet publish -c Release -r linux-x64 --self-contained true
dotnet publish -c Release -r osx-x64 --self-contained true
dotnet publish -c Release -r osx-arm64 --self-contained true
```

## Project layout

- `Program.cs` — entry point, prints the device fingerprint.
- `Fingerprint/FingerPrinter.cs` — OS detection and platform-specific ID lookup.
- `Fingerprint/Device.cs` — model representing the resolved device ID and platform.
