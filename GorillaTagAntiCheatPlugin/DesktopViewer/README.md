# Gorilla Tag Anti-Cheat Desktop Viewer

Windows Forms desktop UI for watching the mod's live `detections.jsonl` telemetry.

## Usage

1. Install `GorillaTagAntiCheat_BepInExMod.dll` into `Gorilla Tag/BepInEx/plugins`.
2. Launch Gorilla Tag once so the mod creates:
   `BepInEx/config/GorillaTagAntiCheat/detections.jsonl`
3. Run `GorillaTagAntiCheatDesktopViewer.exe`.
4. If the file is not found automatically, click **Browse** and select `detections.jsonl`.

The viewer shows suspicion score, category scores, flagged state, latest violation, evidence, and a scrolling detection event log.
