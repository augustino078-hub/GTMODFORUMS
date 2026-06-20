using System;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace GorillaTagAntiCheat
{
    public sealed class DesktopTelemetryWriter
    {
        private readonly StringBuilder builder = new StringBuilder(4096);
        private readonly string telemetryPath;
        private float nextScoreWriteTime;

        public DesktopTelemetryWriter()
        {
            string configPath = Paths.ConfigPath;
            string directory = Path.Combine(configPath, "GorillaTagAntiCheat");
            Directory.CreateDirectory(directory);
            telemetryPath = Path.Combine(directory, "detections.jsonl");
        }

        public string TelemetryPath
        {
            get { return telemetryPath; }
        }

        public void WriteScoreSnapshot(AntiCheatManager manager, float intervalSeconds)
        {
            if (manager == null || Time.time < nextScoreWriteTime)
                return;

            nextScoreWriteTime = Time.time + Mathf.Max(0.1f, intervalSeconds);
            builder.Length = 0;
            foreach (System.Collections.Generic.KeyValuePair<string, PlayerObservationHistory> pair in manager.PlayerHistories)
            {
                SuspicionScoreSnapshot score;
                manager.TryGetSuspicionSnapshot(pair.Key, out score);
                AppendScoreRecord(pair.Key, score, false);
            }

            FlushBuilder();
        }

        public void WriteFlag(SuspicionScoreSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            builder.Length = 0;
            AppendScoreRecord(snapshot.PlayerId, snapshot, true);
            FlushBuilder();
        }

        private void AppendScoreRecord(string playerId, SuspicionScoreSnapshot score, bool flagEvent)
        {
            float movement = score != null ? score.MovementScore : 0f;
            float tag = score != null ? score.TagScore : 0f;
            float network = score != null ? score.NetworkScore : 0f;
            float metadata = score != null ? score.MetadataScore : 0f;
            float final = score != null ? score.FinalScore : 0f;
            int categories = score != null ? score.ActiveSignalCategories : 0;
            bool flagged = score != null && score.IsFlagged;
            string violation = string.Empty;
            string evidence = string.Empty;

            if (score != null && score.RecentViolations != null && score.RecentViolations.Length > 0)
            {
                ViolationRecord record = score.RecentViolations[score.RecentViolations.Length - 1];
                violation = record.ViolationType;
                evidence = record.Evidence;
            }

            builder.Append('{');
            AppendJsonField("event", flagEvent ? "flag" : "score");
            builder.Append(',');
            AppendJsonField("playerId", playerId);
            builder.Append(',');
            AppendJsonNumber("time", Time.time);
            builder.Append(',');
            AppendJsonNumber("movement", movement);
            builder.Append(',');
            AppendJsonNumber("tag", tag);
            builder.Append(',');
            AppendJsonNumber("network", network);
            builder.Append(',');
            AppendJsonNumber("metadata", metadata);
            builder.Append(',');
            AppendJsonNumber("final", final);
            builder.Append(',');
            AppendJsonNumber("categories", categories);
            builder.Append(',');
            builder.Append("\"flagged\":").Append(flagged ? "true" : "false");
            builder.Append(',');
            AppendJsonField("violation", violation);
            builder.Append(',');
            AppendJsonField("evidence", evidence);
            builder.Append('}').AppendLine();
        }

        private void AppendJsonField(string key, string value)
        {
            builder.Append('"').Append(Escape(key)).Append("\":\"").Append(Escape(value)).Append('"');
        }

        private void AppendJsonNumber(string key, float value)
        {
            builder.Append('"').Append(Escape(key)).Append("\":").Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private void AppendJsonNumber(string key, int value)
        {
            builder.Append('"').Append(Escape(key)).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private void FlushBuilder()
        {
            if (builder.Length == 0)
                return;

            File.AppendAllText(telemetryPath, builder.ToString());
        }
    }
}
