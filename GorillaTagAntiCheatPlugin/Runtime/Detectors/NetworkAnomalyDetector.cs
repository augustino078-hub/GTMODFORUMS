using UnityEngine;

namespace GorillaTagAntiCheat
{
    public sealed class NetworkAnomalyDetector : INetworkAnomalyDetector
    {
        public string Name { get { return "NetworkAnomaly"; } }
        public bool Enabled { get; set; }
        public ViolationCategory Category { get { return ViolationCategory.Network; } }

        public NetworkAnomalyDetector()
        {
            Enabled = true;
        }

        public void Analyze(PlayerAnalysisContext context, DetectionResultSink results)
        {
            if (!Enabled || !context.Settings.EnableNetworkDetectors)
                return;

            if (context.History.TimeSinceFirstSeen(context.Now) < context.Settings.JoinGracePeriodSeconds)
                return;

            float oneSecondAgo = context.Now - 1f;
            int rpcPerSecond = context.History.CountNetworkEvents(NetworkEventKind.Rpc, oneSecondAgo);
            if (rpcPerSecond > context.Settings.RpcPerSecondLimit)
            {
                results.AddViolation(context.PlayerId, "RPCSpam", Category, 10f, context.Now, "rpcPerSecond=" + rpcPerSecond);
            }

            if (context.History.HasLatencySpike(context.Now - context.Settings.LagExploitWindowSeconds, context.Settings.LagSpikeMs) &&
                context.History.HasRecentTagEvent(context.Now - context.Settings.LagExploitWindowSeconds))
            {
                results.AddViolation(context.PlayerId, "LagExploit", Category, 15f, context.Now, "latency spike followed by action");
            }

            AnalyzeReplicationConsistency(context, results);
        }

        private void AnalyzeReplicationConsistency(PlayerAnalysisContext context, DetectionResultSink results)
        {
            PlayerTickSnapshot current;
            PlayerTickSnapshot previous;
            if (!context.History.TryGetLatestSnapshot(out current) || !context.History.TryGetPreviousSnapshot(out previous))
                return;

            if (previous.PhotonTimestamp > 0 && current.PhotonTimestamp > 0 && current.PhotonTimestamp < previous.PhotonTimestamp)
            {
                results.AddViolation(context.PlayerId, "PhotonTimestampRollback", Category, 8f, current.LocalTime, "timestamp moved backwards");
            }

            float distance = Vector3.Distance(current.Position, previous.Position);
            float toleratedJump = context.Settings.ReplicationJumpDistance + current.RoundTripTimeMs * context.Settings.PingSpeedAllowancePerMs;
            if (distance > toleratedJump)
            {
                results.AddViolation(context.PlayerId, "InconsistentReplication", Category, 8f, current.LocalTime, "jump=" + distance.ToString("0.00"));
            }
        }
    }
}
