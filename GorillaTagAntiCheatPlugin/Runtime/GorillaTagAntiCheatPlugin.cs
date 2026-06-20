using System;
using BepInEx;
using UnityEngine;

namespace GorillaTagAntiCheat
{
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    public sealed class GorillaTagAntiCheatPlugin : BaseUnityPlugin
    {
        private AntiCheatManager manager;
        private AntiCheatDebugOverlay overlay;
        private InGameDetectionHud inGameHud;
        private GorillaTagPhotonStateSource stateSource;
        private DesktopTelemetryWriter telemetryWriter;

        private void Awake()
        {
            Logger.LogInfo(PluginInfo.Name + " starting...");

            try
            {
                GameObject root = new GameObject("GorillaTagAntiCheat");
                DontDestroyOnLoad(root);

                manager = root.AddComponent<AntiCheatManager>();
                overlay = root.AddComponent<AntiCheatDebugOverlay>();
                inGameHud = root.AddComponent<InGameDetectionHud>();
                stateSource = new GorillaTagPhotonStateSource();
                telemetryWriter = new DesktopTelemetryWriter();

                manager.SetStateSource(stateSource);
                overlay.Attach(manager);
                inGameHud.Attach(manager);
                manager.PlayerFlagged += OnPlayerFlagged;

                Logger.LogInfo(PluginInfo.Name + " loaded. F6 toggles the in-game detection HUD; F8 toggles the debug overlay.");
                Logger.LogInfo("Desktop telemetry: " + telemetryWriter.TelemetryPath);
            }
            catch (Exception exception)
            {
                Logger.LogError(PluginInfo.Name + " failed to initialize: " + exception);
                throw;
            }
        }

        private void Update()
        {
            if (telemetryWriter != null)
                telemetryWriter.WriteScoreSnapshot(manager, 1f);
        }

        private void OnDestroy()
        {
            if (manager != null)
                manager.PlayerFlagged -= OnPlayerFlagged;
        }

        private void OnPlayerFlagged(SuspicionScoreSnapshot snapshot)
        {
            Logger.LogWarning("Suspicious player highlighted: " + snapshot.PlayerId + " score=" + snapshot.FinalScore.ToString("0.0"));
            if (telemetryWriter != null)
                telemetryWriter.WriteFlag(snapshot);
        }
    }
}
