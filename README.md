````md
# YoutubeDesktopByNiko

Minimalistic YouTube desktop client for Windows using WebView2 with:
- persistent profile
- extension support
- uBlock Origin
- SponsorBlock
- automatic playback restore
- saved last watched timestamp

Built with:
- WPF
- C#
- .NET 10.0
- WebView2

Made by Niko973.

---

# Features

## Persistent Browser Profile

The application stores a separate WebView2 profile inside:

```text
%LOCALAPPDATA%\YouTubeDesktopProfile
````

This preserves:

* cookies
* login sessions
* local storage
* browser settings
* extension data

---

# Extension Support

WebView2 browser extensions are enabled using:

```csharp
AreBrowserExtensionsEnabled = true
```

Supported extensions:

* uBlock Origin
* SponsorBlock

Extensions are loaded dynamically from:

```text
Extensions/uBlockOrigin
Extensions/SponsorBlock
```

---

# Automatic Video Restore

The app automatically:

* saves the current video URL
* saves playback timestamp
* restores the video on next launch

Playback position is injected into URL using:

```text
?t=123s
```

---

# Auto Save System

Application state is automatically saved every:

```text
3 seconds
```

Saved data:

* current video URL
* current playback time

State file:

```text
appstate.json
```

---

# Saved State Example

```json
{
  "url": "https://m.youtube.com/watch?v=VIDEO_ID",
  "time": 184.2
}
```

---

# Technologies

* C#
* WPF
* .NET 10.0
* WebView2
* DispatcherTimer
* JSON Serialization

---

# Core Components

## WebView2 Initialization

Creates a custom browser environment with:

* extension support
* persistent profile
* isolated storage

---

## Extension Loader

Dynamically loads unpacked Chromium extensions.

---

## Playback State System

Uses injected JavaScript:

```javascript
document.querySelector('video').currentTime
```

to retrieve current playback position.

---

## URL Timestamp Builder

Automatically appends:

```text
?t=SECONDSs
```

to restore playback.

---

# Project Structure

```text
Project/
│
├── Extensions/
│   ├── uBlockOrigin/
│   └── SponsorBlock/
│
├── appstate.json
├── YoutubePlayer.xaml
└── YoutubePlayer.xaml.cs
```

---

# Requirements

## Runtime

* Windows 10/11
* .NET 10.0 SDK / Runtime
* Microsoft Edge WebView2 Runtime

Download:

* WebView2 Runtime:
  [https://developer.microsoft.com/en-us/microsoft-edge/webview2/](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)

* .NET 10:
  [https://dotnet.microsoft.com/](https://dotnet.microsoft.com/)

---

# NuGet Package

Install:

```powershell
Install-Package Microsoft.Web.WebView2
```

---

# Build

## Visual Studio

Requirements:

* Visual Studio 2022+
* .NET Desktop Development workload
* .NET 10.0 SDK

---

# Main Functional Flow

```text
Application Start
    ↓
Initialize WebView2
    ↓
Load Extensions
    ↓
Restore Last Video
    ↓
Start AutoSave Timer
    ↓
Save Playback State Every 3 Seconds
```

---

# Playback Recovery Logic

The app:

1. detects active video
2. reads currentTime
3. stores playback data
4. removes old `t=` parameter
5. rebuilds URL with updated timestamp
6. restores playback after restart

---

# Error Handling

The application safely handles:

* missing extensions
* invalid state files
* unavailable video elements
* broken URLs
* script execution failures

---

# Notes

* Only YouTube watch pages are saved
* Playback time below 3 seconds is ignored
* Old `t=` parameters are automatically removed
* State saving is fully asynchronous

---

# Author

GitHub:
[https://github.com/Nlko973](https://github.com/Nlko973)

Telegram:
[https://t.me/niko_973](https://t.me/niko_973)

```
```
