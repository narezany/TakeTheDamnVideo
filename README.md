# Take The Damn Video

A BepInEx plugin for **Gorilla Tag** that fixes the built-in LIV camera on Linux under Proton. 

It works by intercepting the high-level C# camera wrappers and replacing the native Windows Media Foundation (WMF) encoding pipeline (which fails under Proton) with a lightweight, pure C# MJPEG/AVI encoder.

## Features

- **Video Recording**: Fixes the crash and `Failed to start native muxer` / `ERROR` screen. Videos are saved directly to `<Gorilla Tag>/Damn videos/`.
- **Photo Capture**: Restores screenshot functionality. Photos are saved directly to `<Gorilla Tag>/Damn photos/`.
- **Proton Compatibility**: Eliminates dependencies on `lck_rs.dll` and WMF, avoiding Proton Access Violations and `NullReferenceException` crashes.

## Installation

### Prerequisities & Recommendations
For the best experience and compatibility, it is highly recommended to run the game using one of the following Proton versions:
- **Proton GE 10-32+** (recommended)
- **Proton Experimental**

### Steps
1. Make sure you have **BepInEx** installed in your Gorilla Tag folder.
2. Download the latest release of `TakeTheDamnVideo.dll`.
3. Copy `TakeTheDamnVideo.dll` into your `<Gorilla Tag>/BepInEx/plugins/` folder.
4. Launch the game and enjoy capturing your VR footage!

---

## Disclaimers
* This mod is not affiliated with, sponsored by, or associated with Another Axiom or Gorilla Tag.
