using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GorillaTagAntiCheat
{
    public sealed class AntiCheatDebugOverlay : MonoBehaviour
    {
        private readonly List<PlayerTickSnapshot> replayScratch = new List<PlayerTickSnapshot>(256);
        private readonly StringBuilder builder = new StringBuilder(2048);
        private AntiCheatManager manager;
        private Vector2 scroll;

        public bool Visible = true;
        public int Width = 420;
        public int Height = 520;
        public float ReplaySeconds = 6f;

        public void Attach(AntiCheatManager antiCheatManager)
        {
            manager = antiCheatManager;
        }

        private void Awake()
        {
            if (manager == null)
                manager = FindObjectOfType<AntiCheatManager>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
                Visible = !Visible;
        }

        private void OnGUI()
        {
            if (!Visible || manager == null)
                return;

            GUILayout.BeginArea(new Rect(16f, 16f, Width, Height), GUI.skin.box);
            GUILayout.Label("Gorilla Tag Anti-Cheat Debug");
            GUILayout.Label("F8 toggles overlay. Client-side signals only; no ban action.");
            scroll = GUILayout.BeginScrollView(scroll);

            foreach (KeyValuePair<string, PlayerObservationHistory> pair in manager.PlayerHistories)
                DrawPlayer(pair.Key, pair.Value);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawPlayer(string playerId, PlayerObservationHistory history)
        {
            SuspicionScoreSnapshot score;
            manager.TryGetSuspicionSnapshot(playerId, out score);

            builder.Length = 0;
            builder.Append("Player: ").Append(playerId).AppendLine();
            builder.Append("Ticks: ").Append(history.SnapshotCount).Append("  Collisions: ").Append(history.CollisionCount).Append("  Tags: ").Append(history.TagEventCount).AppendLine();

            if (score != null)
            {
                builder.Append("Score: ").Append(score.FinalScore.ToString("0.0"));
                builder.Append("  M:").Append(score.MovementScore.ToString("0.0"));
                builder.Append(" T:").Append(score.TagScore.ToString("0.0"));
                builder.Append(" N:").Append(score.NetworkScore.ToString("0.0"));
                builder.Append(" Meta:").Append(score.MetadataScore.ToString("0.0"));
                builder.Append(score.IsFlagged ? "  FLAGGED" : string.Empty).AppendLine();

                ViolationRecord[] violations = score.RecentViolations;
                int start = Mathf.Max(0, violations.Length - 4);
                for (int i = start; i < violations.Length; i++)
                {
                    builder.Append(" - ").Append(violations[i].ViolationType);
                    builder.Append(" +").Append(violations[i].Weight.ToString("0.0"));
                    builder.Append(" [").Append(violations[i].Category).Append("] ");
                    builder.Append(violations[i].Evidence).AppendLine();
                }
            }
            else
            {
                builder.Append("Score: 0.0").AppendLine();
            }

            DrawTimeline(playerId, builder);
            GUILayout.Label(builder.ToString(), GUI.skin.textArea);
        }

        private void DrawTimeline(string playerId, StringBuilder text)
        {
            int count = manager.CopyReplay(playerId, ReplaySeconds, replayScratch);
            if (count == 0)
                return;

            text.Append("Timeline: ");
            int stride = Mathf.Max(1, count / 24);
            for (int i = 0; i < count; i += stride)
            {
                PlayerTickSnapshot tick = replayScratch[i];
                char marker = tick.IsTagged ? 'T' : '.';
                if (!tick.IsGrounded && !tick.IsTouchingClimbSurface)
                    marker = '^';
                text.Append(marker);
            }

            text.AppendLine();
        }
    }
}
