using System.Collections.Generic;
using UnityEngine;

namespace GorillaTagAntiCheat
{
    [System.Serializable]
    public sealed class AntiCheatSettings
    {
        public int TickRate = 20;
        public int ReplaySeconds = 12;
        public float JoinGracePeriodSeconds = 5f;
        public float InterpolationToleranceSeconds = 0.12f;
        public float PingSpeedAllowancePerMs = 0.003f;

        public float MaxAllowedSpeed = 9.5f;
        public float TeleportDistanceThreshold = 4.5f;
        public float MaxAirStallSeconds = 1.25f;
        public float MinExpectedFallSpeed = 1.2f;
        public float MaxImpulseWithoutSurface = 45f;
        public LayerMask SolidWorldMask = 0;

        public float MaxTagRange = 1.35f;
        public float MaxArmReach = 1.75f;
        public float TagCollisionWindowSeconds = 0.18f;
        public float TagDesyncTolerance = 1.1f;
        public float LagSpikeMs = 220f;
        public float LagExploitWindowSeconds = 0.45f;

        public int RpcPerSecondLimit = 25;
        public float ReplicationJumpDistance = 5f;
        public int MaxUnknownPhotonKeysBeforeContext = 0;
        public float MetadataMovementContextThreshold = 12f;

        public float DecayPerSecond = 1.5f;
        public float FlagThreshold = 45f;
        public float HeavySingleSignalThreshold = 75f;
        public int RequiredSignalCategories = 2;

        public bool EnableMovementDetectors = true;
        public bool EnableTagIntegrityDetectors = true;
        public bool EnableNetworkDetectors = true;
        public bool EnableMetadataDetectors = true;

        public HashSet<string> AllowedPhotonPropertyKeys = new HashSet<string>
        {
            "cosmetics",
            "hat",
            "badge",
            "material",
            "color",
            "platform",
            "queue",
            "room",
            "voice",
            "isTagged",
            "gameMode",
            "version"
        };

        public float TickInterval
        {
            get { return 1f / Mathf.Max(1, TickRate); }
        }

        public int TickCapacity
        {
            get { return Mathf.Max(16, TickRate * Mathf.Max(ReplaySeconds, 1)); }
        }

        public float SpeedThresholdForPing(int roundTripTimeMs)
        {
            return MaxAllowedSpeed + Mathf.Max(0, roundTripTimeMs) * PingSpeedAllowancePerMs;
        }
    }
}
