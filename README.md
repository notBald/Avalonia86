# Avalonia 86

**Avalonia 86** is a configuration manager for the [86Box emulator](https://github.com/86Box/86Box) and [PCBox emulator](https://github.com/PCBox/PCBox).

![Desktop](/images/UI-white_and_dark.png?raw=true)

## What's New

### v1.5.0 - Linux Platform Upgrade

- **Avalonia 12.0 on Linux**: Upgraded to Avalonia 12.0 (.NET 10.0) for improved Linux support
- **X11/XWayland Support**: Runs natively on X11 and via XWayland on Wayland compositors
- **Multi-Target Framework**: 
  - Windows/macOS: .NET 6.0 (Avalonia 11.3.x) for maximum compatibility
  - Linux: .NET 10.0 (Avalonia 12.0) with latest platform improvements
- **AppImage Distribution**: Easy installation on Linux with AppImage format

## Features

- Create/Delete Virtual Machines
- Sort them into categories
- Display machine information and images
- A tray icon so that the Manager window doesn't get in your way
- Supports both 86Box and PCBox executables

## Localization

Avalonia 86 supports the following languages:

| Language | Status |
|----------|--------|
| English | Complete |
| 简体中文 (Simplified Chinese) | Complete |
| 繁體中文 (Traditional Chinese) | Complete |

The UI language is automatically selected based on your system settings. To add a new language, edit `Avalonia86/Localization/L.cs`.

## System requirements

System requirements are the same as for 86Box / PCBox. Additionally, the following is required:

- [86Box 2.0](https://github.com/86Box/86Box/releases) or later (earlier builds are untested)

### Self-contained builds

Starting from this version, official release builds are **self-contained** — the .NET runtime is bundled inside the application. You do **not** need to install .NET separately.

| Platform | Target Framework | Avalonia Version | Notes |
|----------|-----------------|------------------|-------|
| **Windows** | .NET 6.0 | 11.3.x | Compatible with Windows 7+ |
| **Linux** | .NET 10.0 | 12.0 | X11 native, Wayland via XWayland |
| **macOS** | .NET 6.0 | 11.3.x | Compatible with macOS 10.15+ |

### Linux Display Server Support

Avalonia 86 uses the X11 backend on Linux. On Wayland compositors, it runs through **XWayland** automatically.

| Display Server | Support | Notes |
|---------------|---------|-------|
| **X11** | ✅ Native | Full support |
| **XWayland** | ✅ Automatic | X11 apps run transparently on Wayland via XWayland |

> **Note**: Avalonia does not yet have a native Wayland backend (planned for ~12.1). If you experience issues on Wayland, the application is running through XWayland which is included by default on most Wayland compositors.

## How to use

1. Download the desired build [here](https://github.com/notBald/Avalonia86/releases).
2. Run `Avalonia86.exe`.
3. Go to Settings, choose the folder where `86Box.exe` or `PCBox.exe` is located (along with the roms folder) and a folder where your virtual machines will be located (for configs, nvr folders, etc.).
4. Start creating new virtual machines and enjoy.

## Using on Windows

![Install .Net](/images/win_1.png?raw=true)

You may have to install .net 9.0. In that case, you will get a message like the one above.

### If .Net 9.0 fails on Windows 10

[KB5058379](https://support.microsoft.com/en-us/topic/may-13-2025-kb5058379-os-builds-19044-5854-and-19045-5854-0a30e9ee-5038-45dd-a5d7-70a8813a5e39) is required for .Net 9.0 to function properly. If you're using Windows 10 without this update you can either the Windows 7 release of Avalonia 86 or to follow [this guide](https://www.reddit.com/r/WindowsLTSC/comments/1klhp4e/comment/mst7tjf/) to install the update.

## Using on Linux

### AppImage (Recommended)

Newer builds are AppImages, same as 86Box. Just remember to set the AppImage executable before running.

```bash
# Make the AppImage executable
chmod +x Avalonia-86-for-Linux-x64.AppImage

# Run
./Avalonia-86-for-Linux-x64.AppImage
```

### Display Server Compatibility

The application uses the X11 backend and works on both X11 and Wayland (via XWayland) desktops. No additional configuration is needed — XWayland is included by default on most Wayland compositors (Fedora, Ubuntu 22.04+, etc.).

For older builds, see the [Linux Guide](Linux.md).

## How to build

1. Clone the repo
2. Open `Avalonia86.sln` solution file in Visual Studio 2022
3. Make your changes
4. Choose the `Release` or `Debug` configuration
5. Build the solution

### Multi-Target Framework

The project supports multiple target frameworks:

- **net6.0**: For Windows and macOS compatibility (Avalonia 11.3.x)
- **net10.0**: For Linux with latest Avalonia 12.0 platform improvements (Avalonia 12.0)

### Building self-contained releases

```sh
# Windows x64 (net6.0)
dotnet publish Avalonia86 -r win-x64 -f net6.0 -c Release --self-contained true

# Linux x64 (net10.0)
dotnet publish Avalonia86 -r linux-x64 -f net10.0 -c Release --self-contained true

# macOS ARM64 (net6.0)
dotnet publish Avalonia86 -r osx-arm64 -f net6.0 -c Release --self-contained true
```

Or use `build.sh` to build all platforms at once.

### Building AppImage

```sh
# Build Linux release first
dotnet publish Avalonia86 -r linux-x64 -f net10.0 -c Release --self-contained true -o dist/linux-x64

# Create AppImage (requires appimagetool)
chmod +x /path/to/appimagetool-x86_64.AppImage
/path/to/appimagetool-x86_64.AppImage --no-appstream dist/Avalonia86-x64.AppDir pub/Avalonia-86-for-Linux-x64.AppImage
```

## License

It's released under the MIT license, so it can be freely distributed with 86Box. See the `LICENSE` file for license information and `AUTHORS` for a complete list of contributors and authors.
