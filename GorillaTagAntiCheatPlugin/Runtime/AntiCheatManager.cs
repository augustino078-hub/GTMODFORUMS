using System;
using System.Collections.Generic;
using UnityEngine;

namespace GorillaTagAntiCheat
{
    public sealed class AntiCheatManager : MonoBehaviour
    {
        private readonly Dictionary<string, PlayerObservationHistory> playerHistories = new Dictionary<string, PlayerObservationHistory>(32);
        private readonly List<PlayerTickSnapshot> snapshotScratch = new List<PlayerTickSnapshot>(32);
        private readonly List<IAntiCheatDetector> detectors = new List<IAntiCheatDetector>(8);
        private readonly List<SuspicionScoreSnapshot> flaggedScratch = new List<SuspicionScoreSnapshot>(16);
        private readonly HashSet<string> raisedFlags = new HashSet<string>();
        private DetectionResultSink resultSink;
        private float accumulator;
        private IPhotonStateSource stateSource;

        [SerializeField]
        private AntiCheatSettings settings = new AntiCheatSettings();

        public event Action<SuspicionScoreSnapshot> PlayerFlagged;

        public AntiCheatSettings Settings
        {
            get { return settings; }
        }

        public SuspicionScoreManager Scores { get; private set; }

        public IReadOnlyDictionary<string, PlayerObservationHistory> PlayerHistories
        {
            get { return playerHistories; }
        }

        private void Awake()
        {
            Initialize(settings);
        }

        public void Initialize(AntiCheatSettings initialSettings)
        {
            settings = initialSettings ?? new AntiCheatSettings();
            Scores = new SuspicionScoreManager(settings);
            resultSink = new DetectionResultSink(Scores);
            detectors.Clear();
            detectors.Add(new MovementPhysicsDetector());
            detectors.Add(new TagIntegrityDetector());
            detectors.Add(new NetworkAnomalyDetector());
            detectors.Add(new MetadataIntegrityDetector());
        }

        public void SetSettings(AntiCheatSettings nextSettings)
        {
            settings = nextSettings ?? new AntiCheatSettings();
            if (Scores == null)
                Scores = new SuspicionScoreManager(settings);
            else
                Scores.SetSettings(settings);
        }

        public void SetStateSource(IPhotonStateSource source)
        {
            stateSource = source;
        }

        public void RegisterDetector(IAntiCheatDetector detector)
        {
            if (detector == null)
                return;

            for (int i = 0; i < detectors.Count; i++)
            {
                if (detectors[i].Name == detector.Name)
                {
                    detectors[i] = detector;
                    return;
                }
            }

            detectors.Add(detector);
        }

        public bool SetDetectorEnabled(string detectorName, bool enabled)
        {
            for (int i = 0; i < detectors.Count; i++)
            {
                if (detectors[i].Name == detectorName)
                {
                    detectors[i].Enabled = enabled;
                    return true;
                }
            }

            return false;
        }

        public void RecordCollision(CollisionObservation collision)
        {
            if (string.IsNullOrEmpty(collision.SourcePlayerId))
                return;

            GetOrCreateHistory(collision.SourcePlayerId).AddCollision(collision);
            if (!string.IsNullOrEmpty(collision.OtherPlayerId))
            {
                CollisionObservation mirrored = collision;
                mirrored.SourcePlayerId = collision.OtherPlayerId;
                mirrored.OtherPlayerId = collision.SourcePlayerId;
                GetOrCreateHistory(mirrored.SourcePlayerId).AddCollision(mirrored);
            }
        }

        public void RecordTagEvent(TagEventObservation tagEvent)
        {
            if (!string.IsNullOrEmpty(tagEvent.SourcePlayerId))
                GetOrCreateHistory(tagEvent.SourcePlayerId).AddTagEvent(tagEvent);

            if (!string.IsNullOrEmpty(tagEvent.TargetPlayerId))
                GetOrCreateHistory(tagEvent.TargetPlayerId).AddTagEvent(tagEvent);
        }

        public void RecordNetworkEvent(NetworkEventObservation networkEvent)
        {
            if (string.IsNullOrEmpty(networkEvent.PlayerId))
                return;

            GetOrCreateHistory(networkEvent.PlayerId).AddNetworkEvent(networkEvent);
        }

        public bool TryGetSuspicionSnapshot(string playerId, out SuspicionScoreSnapshot snapshot)
        {
            if (Scores == null)
            {
                snapshot = null;
                return false;
            }

            return Scores.TryGetSnapshot(playerId, out snapshot);
        }

        public int CopyReplay(string playerId, float seconds, List<PlayerTickSnapshot> destination)
        {
            PlayerObservationHistory history;
            if (!playerHistories.TryGetValue(playerId, out history))
            {
                destination.Clear();
                return 0;
            }

            return history.CopyRecentSnapshots(Time.time - Mathf.Max(0f, seconds), destination);
        }

        private void Update()
        {
            if (Scores == null || resultSink == null)
                Initialize(settings);

            accumulator += Time.deltaTime;
            float interval = settings.TickInterval;
            while (accumulator >= interval)
            {
                Tick(interval);
                accumulator -= interval;
            }
        }

        private void Tick(float deltaTime)
        {
            float now = Time.time;
            CollectSnapshots(now);
            Scores.Decay(deltaTime);

            foreach (KeyValuePair<string, PlayerObservationHistory> pair in playerHistories)
            {
                PlayerAnalysisContext context = new PlayerAnalysisContext
                {
                    PlayerId = pair.Key,
                    History = pair.Value,
                    AllPlayers = playerHistories,
                    Scores = Scores,
                    Settings = settings,
                    Now = now,
                    DeltaTime = deltaTime
                };

                for (int i = 0; i < detectors.Count; i++)
                {
                    if (detectors[i].Enabled)
                        detectors[i].Analyze(context, resultSink);
                }
            }

            RaiseNewFlags();
        }

        private void CollectSnapshots(float now)
        {
            if (stateSource == null)
                return;

            snapshotScratch.Clear();
            stateSource.CollectPlayerSnapshots(snapshotScratch);
            for (int i = 0; i < snapshotScratch.Count; i++)
            {
                PlayerTickSnapshot snapshot = snapshotScratch[i];
                if (string.IsNullOrEmpty(snapshot.PlayerId))
                    continue;

                if (snapshot.LocalTime <= 0f)
                    snapshot.LocalTime = now;

                GetOrCreateHistory(snapshot.PlayerId).AddSnapshot(snapshot);
            }
        }

        private void RaiseNewFlags()
        {
            Scores.CopyFlaggedPlayers(flaggedScratch);
            for (int i = 0; i < flaggedScratch.Count; i++)
            {
                SuspicionScoreSnapshot snapshot = flaggedScratch[i];
                if (raisedFlags.Contains(snapshot.PlayerId))
                    continue;

                raisedFlags.Add(snapshot.PlayerId);
                if (PlayerFlagged != null)
                    PlayerFlagged(snapshot);
            }
        }

        private PlayerObservationHistory GetOrCreateHistory(string playerId)
        {
            PlayerObservationHistory history;
            if (!playerHistories.TryGetValue(playerId, out history))
            {
                history = new PlayerObservationHistory(playerId, settings.TickCapacity);
                playerHistories.Add(playerId, history);
            }

            return history;
        }
    }
}
