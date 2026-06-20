using UnityEngine;

namespace GorillaTagAntiCheat
{
    public sealed class MovementPhysicsDetector : IMovementDetector
    {
        public string Name { get { return "MovementPhysics"; } }
        public bool Enabled { get; set; }
        public ViolationCategory Category { get { return ViolationCategory.Movement; } }

        public MovementPhysicsDetector()
        {
            Enabled = true;
        }

        public void Analyze(PlayerAnalysisContext context, DetectionResultSink results)
        {
            if (!Enabled || !context.Settings.EnableMovementDetectors)
                return;

            if (context.History.TimeSinceFirstSeen(context.Now) < context.Settings.JoinGracePeriodSeconds)
                return;

            PlayerTickSnapshot current;
            PlayerTickSnapshot previous;
            if (!context.History.TryGetLatestSnapshot(out current) || !context.History.TryGetPreviousSnapshot(out previous))
                return;

            float deltaTime = Mathf.Max(0.001f, current.LocalTime - previous.LocalTime);
            float toleratedDelta = deltaTime + context.Settings.InterpolationToleranceSeconds;
            Vector3 measuredVelocity = (current.Position - previous.Position) / deltaTime;
            float speed = measuredVelocity.magnitude;
            float speedThreshold = context.Settings.SpeedThresholdForPing(current.RoundTripTimeMs);

            if (speed > speedThreshold)
            {
                results.AddViolation(context.PlayerId, "SpeedHack", Category, 10f, current.LocalTime, "velocity=" + speed.ToString("0.00"));
            }

            float distance = Vector3.Distance(current.Position, previous.Position);
            float teleportThreshold = context.Settings.TeleportDistanceThreshold + speedThreshold * toleratedDelta;
            if (distance > teleportThreshold)
            {
                results.AddViolation(context.PlayerId, "Teleport", Category, 15f, current.LocalTime, "distance=" + distance.ToString("0.00"));
            }

            AnalyzeFlight(context, results, current, measuredVelocity);
            AnalyzeClimbImpulse(context, results, current, previous, measuredVelocity, deltaTime);
            AnalyzeNoclip(context, results, current);
        }

        private void AnalyzeFlight(PlayerAnalysisContext context, DetectionResultSink results, PlayerTickSnapshot current, Vector3 measuredVelocity)
        {
            if (current.IsGrounded || current.IsTouchingClimbSurface)
                return;

            float airborneTime = 0f;
            float lastTime = current.LocalTime;
            for (int i = 1; i < context.History.SnapshotCount; i++)
            {
                PlayerTickSnapshot older;
                if (!context.History.TryGetSnapshotFromNewest(i, out older))
                    break;

                if (older.IsGrounded || older.IsTouchingClimbSurface)
                    break;

                airborneTime += Mathf.Max(0f, lastTime - older.LocalTime);
                lastTime = older.LocalTime;
                if (airborneTime >= context.Settings.MaxAirStallSeconds)
                    break;
            }

            bool gravityViolation = measuredVelocity.y > -context.Settings.MinExpectedFallSpeed;
            if (airborneTime >= context.Settings.MaxAirStallSeconds && gravityViolation)
            {
                results.AddViolation(context.PlayerId, "AirStallFlight", Category, 8f, current.LocalTime, "airborne=" + airborneTime.ToString("0.00"));
            }
        }

        private void AnalyzeClimbImpulse(PlayerAnalysisContext context, DetectionResultSink results, PlayerTickSnapshot current, PlayerTickSnapshot previous, Vector3 measuredVelocity, float deltaTime)
        {
            Vector3 previousVelocity = previous.Velocity;
            PlayerTickSnapshot beforePrevious;
            if (context.History.TryGetSnapshotFromNewest(2, out beforePrevious))
            {
                float previousDelta = Mathf.Max(0.001f, previous.LocalTime - beforePrevious.LocalTime);
                previousVelocity = (previous.Position - beforePrevious.Position) / previousDelta;
            }

            float impulse = (measuredVelocity - previousVelocity).magnitude / Mathf.Max(0.001f, deltaTime);
            bool hasSurfaceContact = current.IsGrounded || current.IsTouchingClimbSurface;

            if (!hasSurfaceContact && impulse > context.Settings.MaxImpulseWithoutSurface)
            {
                results.AddViolation(context.PlayerId, "InvalidClimbImpulse", Category, 7f, current.LocalTime, "impulse=" + impulse.ToString("0.00"));
            }
        }

        private void AnalyzeNoclip(PlayerAnalysisContext context, DetectionResultSink results, PlayerTickSnapshot current)
        {
            if (!current.HasValidBodyBounds || context.Settings.SolidWorldMask.value == 0)
                return;

            Vector3 halfExtents = current.BodyBounds.extents;
            if (halfExtents.sqrMagnitude <= 0.0001f)
                return;

            bool penetratesSolid = Physics.CheckBox(current.BodyBounds.center, halfExtents, Quaternion.identity, context.Settings.SolidWorldMask, QueryTriggerInteraction.Ignore);
            if (penetratesSolid)
            {
                results.AddViolation(context.PlayerId, "NoclipInference", Category, 9f, current.LocalTime, "body bounds overlap solid world");
            }
        }
    }
}
