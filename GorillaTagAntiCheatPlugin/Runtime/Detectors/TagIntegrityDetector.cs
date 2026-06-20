using UnityEngine;

namespace GorillaTagAntiCheat
{
    public sealed class TagIntegrityDetector : ITagIntegrityDetector
    {
        public string Name { get { return "TagIntegrity"; } }
        public bool Enabled { get; set; }
        public ViolationCategory Category { get { return ViolationCategory.TagIntegrity; } }

        public TagIntegrityDetector()
        {
            Enabled = true;
        }

        public void Analyze(PlayerAnalysisContext context, DetectionResultSink results)
        {
            if (!Enabled || !context.Settings.EnableTagIntegrityDetectors)
                return;

            if (context.History.TimeSinceFirstSeen(context.Now) < context.Settings.JoinGracePeriodSeconds)
                return;

            AnalyzeRecentTagEvent(context, results);
            AnalyzeSilentStateChange(context, results);
        }

        private void AnalyzeRecentTagEvent(PlayerAnalysisContext context, DetectionResultSink results)
        {
            TagEventObservation tagEvent;
            if (!context.History.TryGetLastTagEvent(out tagEvent))
                return;

            if (tagEvent.SourcePlayerId != context.PlayerId)
                return;

            if (context.Now - tagEvent.LocalTime > context.Settings.TickInterval * 1.5f)
                return;

            float handDistance = Vector3.Distance(tagEvent.HandPosition, tagEvent.TargetHitboxPosition);
            if (handDistance > context.Settings.MaxTagRange)
            {
                results.AddViolation(context.PlayerId, "TagAura", Category, 20f, tagEvent.LocalTime, "handDistance=" + handDistance.ToString("0.00"));
            }

            if (handDistance > context.Settings.MaxArmReach)
            {
                results.AddViolation(context.PlayerId, "ExtendedReach", Category, 12f, tagEvent.LocalTime, "reach=" + handDistance.ToString("0.00"));
            }

            if (!context.History.HasCollisionWith(tagEvent.TargetPlayerId, tagEvent.LocalTime, context.Settings.TagCollisionWindowSeconds))
            {
                results.AddViolation(context.PlayerId, "InvalidTag", Category, 25f, tagEvent.LocalTime, "no overlap history");
            }

            PlayerObservationHistory targetHistory;
            if (context.AllPlayers.TryGetValue(tagEvent.TargetPlayerId, out targetHistory))
                AnalyzeTargetDesync(context, results, tagEvent, targetHistory);

            if (context.History.HasLatencySpike(tagEvent.LocalTime - context.Settings.LagExploitWindowSeconds, context.Settings.LagSpikeMs))
            {
                results.AddViolation(context.PlayerId, "LagSwitchTag", Category, 15f, tagEvent.LocalTime, "latency spike before tag");
            }
        }

        private void AnalyzeTargetDesync(PlayerAnalysisContext context, DetectionResultSink results, TagEventObservation tagEvent, PlayerObservationHistory targetHistory)
        {
            PlayerTickSnapshot targetSnapshot;
            if (!targetHistory.TryGetLatestSnapshot(out targetSnapshot))
                return;

            float targetOffset = Vector3.Distance(tagEvent.TargetHitboxPosition, targetSnapshot.Position);
            float tolerance = context.Settings.TagDesyncTolerance + targetSnapshot.RoundTripTimeMs * context.Settings.PingSpeedAllowancePerMs;
            if (targetOffset > tolerance)
            {
                results.AddViolation(context.PlayerId, "DesyncTagAbuse", Category, 10f, tagEvent.LocalTime, "targetOffset=" + targetOffset.ToString("0.00"));
            }
        }

        private void AnalyzeSilentStateChange(PlayerAnalysisContext context, DetectionResultSink results)
        {
            PlayerTickSnapshot current;
            PlayerTickSnapshot previous;
            if (!context.History.TryGetLatestSnapshot(out current) || !context.History.TryGetPreviousSnapshot(out previous))
                return;

            if (current.IsTagged == previous.IsTagged)
                return;

            bool recentTagEvent = context.History.HasRecentTagEvent(current.LocalTime - context.Settings.TagCollisionWindowSeconds);
            if (!recentTagEvent)
            {
                results.AddViolation(context.PlayerId, "SilentTagStateChange", Category, 12f, current.LocalTime, "tag state changed without matching event");
            }
        }
    }
}
