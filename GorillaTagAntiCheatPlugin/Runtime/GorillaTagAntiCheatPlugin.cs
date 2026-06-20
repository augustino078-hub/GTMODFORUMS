using BepInEx;
using UnityEngine;

namespace GorillaTagAntiCheat
{
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    public sealed class GorillaTagAntiCheatPlugin : BaseUnityPlugin
    {
        private AntiCheatManager manager;
        private AntiCheatDebugOverlay overlay;
        private GorillaTagPhotonStateSource stateSource;

        private void Awake()
        {
            GameObject root = new GameObject("GorillaTagAntiCheat");
            DontDestroyOnLoad(root);

            manager = root.AddComponent<AntiCheatManager>();
            overlay = root.AddComponent<AntiCheatDebugOverlay>();
            stateSource = new GorillaTagPhotonStateSource();

            manager.SetStateSource(stateSource);
            overlay.Attach(manager);
            manager.PlayerFlagged += OnPlayerFlagged;

            Logger.LogInfo(PluginInfo.Name + " loaded. This mod only highlights suspicious behavior; it does not ban or kick players.");
        }

        private void OnDestroy()
        {
            if (manager != null)
                manager.PlayerFlagged -= OnPlayerFlagged;
        }

        private void OnPlayerFlagged(SuspicionScoreSnapshot snapshot)
        {
            Logger.LogWarning("Suspicious player highlighted: " + snapshot.PlayerId + " score=" + snapshot.FinalScore.ToString("0.0"));
        }
    }
}
