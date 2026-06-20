using System.Collections.Generic;

namespace GorillaTagAntiCheat
{
    public interface IPhotonStateSource
    {
        void CollectPlayerSnapshots(List<PlayerTickSnapshot> destination);
    }

    public struct PlayerAnalysisContext
    {
        public string PlayerId;
        public PlayerObservationHistory History;
        public IReadOnlyDictionary<string, PlayerObservationHistory> AllPlayers;
        public SuspicionScoreManager Scores;
        public AntiCheatSettings Settings;
        public float Now;
        public float DeltaTime;
    }

    public sealed class DetectionResultSink
    {
        private readonly SuspicionScoreManager scores;

        public DetectionResultSink(SuspicionScoreManager scores)
        {
            this.scores = scores;
        }

        public void AddViolation(string playerId, string violationType, ViolationCategory category, float weight, float localTime, string evidence)
        {
            scores.AddViolation(playerId, violationType, category, weight, localTime, evidence);
        }

        public void AddContextBonus(string playerId, string contextType, float weight, float localTime, string evidence)
        {
            scores.AddContextBonus(playerId, contextType, weight, localTime, evidence);
        }
    }

    public interface IAntiCheatDetector
    {
        string Name { get; }
        bool Enabled { get; set; }
        ViolationCategory Category { get; }
        void Analyze(PlayerAnalysisContext context, DetectionResultSink results);
    }

    public interface IMovementDetector : IAntiCheatDetector
    {
    }

    public interface ITagIntegrityDetector : IAntiCheatDetector
    {
    }

    public interface INetworkAnomalyDetector : IAntiCheatDetector
    {
    }

    public interface IMetadataIntegrityDetector : IAntiCheatDetector
    {
    }
}
