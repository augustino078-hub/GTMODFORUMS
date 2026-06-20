# Gorilla Tag Anti-Cheat Plugin Module

Client-side behavioral anomaly detector for Unity/Gorilla Tag mod frameworks. It observes local Photon-replicated state, physics hints, tag observations, and network event metadata, then highlights suspicious players using weighted, decay-based scores.

This is not a ban tool. It does not enforce server authority, kick, ban, or claim that any single signal proves cheating. Unknown Photon custom properties are only weak context and never trigger flags by themselves.

## Drop-in usage

1. Copy `GorillaTagAntiCheatPlugin/Runtime` and `GorillaTagAntiCheat.Runtime.asmdef` into a Unity mod project.
2. Add `AntiCheatManager` to a persistent GameObject created by your mod loader/plugin.
3. Implement `IPhotonStateSource` for your framework and call `SetStateSource(...)`.
4. Forward observable collisions, tag events, and network events to:
   - `RecordCollision(...)`
   - `RecordTagEvent(...)`
   - `RecordNetworkEvent(...)`
5. Enable `AntiCheatDebugOverlay` in development builds to inspect scores, categories, and replay timelines.

## Design goals

- Tick-based analysis with a per-player ring buffer.
- Modular detector interfaces for movement, tag integrity, network anomalies, and Photon metadata.
- Low-GC update path using reusable buffers and bounded histories.
- Per-lobby adjustable thresholds through `AntiCheatSettings`.
- False-positive protection: ping normalization, join grace period, interpolation tolerance, decay, and multi-signal confirmation.

## BepInEx/Gorilla Tag mod DLL

`GorillaTagAntiCheatPlugin` now includes a BepInEx plugin entrypoint:

- `GorillaTagAntiCheatPlugin : BaseUnityPlugin`
- `GorillaTagPhotonStateSource : IPhotonStateSource`

Drop the built `GorillaTagAntiCheat.Runtime.dll` into your Gorilla Tag BepInEx plugins folder. The mod creates a persistent `AntiCheatManager`, attaches the debug overlay, and uses reflection-based Gorilla Tag/Photon observation so it can run without bundling Photon or game assemblies.
