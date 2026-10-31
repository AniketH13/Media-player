# Frame Media Player

A fast, lightweight Windows desktop media player built with .NET 10 Windows Forms and native Windows Media Foundation / DirectX media pipeline.

> **No external player dependencies**: Completely independent of VLC player or LibVLC. Built natively for Windows.

---

## 🚀 How to Run

### Method 1: Using the Run File (Quickest)
Double-click `run.bat` in this folder:
```cmd
run.bat
```
You can also drag-and-drop any video or audio file directly onto `run.bat` to launch and play it immediately.

### Method 2: Direct Executable
- **Lightweight build**: [publish/MediaPlayer.exe](publish/MediaPlayer.exe)
- **Self-contained build**: [publish-selfcontained/MediaPlayer.exe](publish-selfcontained/MediaPlayer.exe)

### Method 3: Dotnet CLI
```powershell
dotnet run
```

---

## 📦 Building & Publishing

To re-compile and publish:

- **Self-Contained (Recommended for installer)**:
  ```powershell
  dotnet publish -c Release -r win-x64 --self-contained true -o .\publish-selfcontained
  ```

- **Framework-Dependent (Ultra lightweight, ~200 KB)**:
  ```powershell
  dotnet publish -c Release -r win-x64 --self-contained false -o .\publish
  ```

---

## 💿 Installation Setup (.iss file)

An Inno Setup script is included: [`MediaPlayerSetup.iss`](MediaPlayerSetup.iss).

To create a professional Windows Installer (`.exe`):
1. Install [Inno Setup](https://jrsoftware.org/isinfo.php) (if not already installed).
2. Right-click [`MediaPlayerSetup.iss`](MediaPlayerSetup.iss) and select **Compile**, or open it in Inno Setup and press **F9** (Run).
3. The generated installer will be saved in `installer_output/FrameMediaPlayerSetup_v1.0.0.exe`.

### Installer Features:
- Desktop shortcut & Start Menu entry
- Clean Windows Add/Remove Programs integration with uninstaller
- Optional media file associations (`.mp4`, `.mkv`, `.mp3`, `.wav`, `.avi`, `.wmv`)
- 64-bit Windows support

---

## ✨ Player Features
- **High-Definition Video**: Hardware-accelerated decoding for 1080p, 2K, and 4K videos at 60fps.
- **Multiple Aspect Ratios**: Switch instantly between **Auto (Fit)**, **16:9 (Widescreen)**, **4:3 (Classic TV)**, **21:9 (Cinema Ultrawide)**, **Fill (Stretch)**, **Zoom (Crop)**, and **Original (1:1)** using the toolbar button or **`A`** key.
- **Video Context Menu**: Right-click anywhere on the video to pick an aspect ratio, take a screenshot, or toggle fullscreen.
- **Volume Booster**: Audio amplified up to **150%** with visual boost indicator.
- **Video Screenshots (`Ctrl + S`)**: Capture the active video frame cleanly (with letterbox cropping), automatically copy it to the clipboard, and save to PNG/JPG/BMP.
- **Full Screen Mode**: Dedicated Full Screen button, `F` / `F11` key, and double-click video to toggle.
- **Clear Timeline UI**: High-contrast, easily visible elapsed and total time (hours, minutes, seconds).
- **Video & Audio playback**: Supports MP4, MKV, AVI, WMV, MP3, WAV, AAC, FLAC, and more.
- **Playlist management**: Drag and drop files, double-click to play, auto-advances to the next track.
- **Controls**: Play/Pause logo button, Next/Previous, Seek bar, 150% Volume slider, Mute toggle, Aspect Ratio button, Screenshot camera button, Fullscreen toggle.
- **Keyboard shortcuts**:
  - `Space`: Play / Pause
  - `A`: Cycle Aspect Ratio (Auto, 16:9, 4:3, 21:9, Fill, Zoom, 1:1)
  - `Ctrl + S`: Take Screenshot of current video frame
  - `Left Arrow`: Rewind 10 seconds
  - `Right Arrow`: Fast forward 10 seconds
  - `Up Arrow`: Volume up (+5%)
  - `Down Arrow`: Volume down (-5%)
  - `F` or `F11` or `Alt + Enter`: Toggle Full Screen
  - `Esc`: Exit Full Screen
  - `M`: Mute / Unmute
  - `Ctrl + O`: Open media files
