using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GorillaTagAntiCheat
{
    public sealed class InGameDetectionHud : MonoBehaviour
    {
        private struct HudRow
        {
            public string PlayerId;
            public float FinalScore;
            public float MovementScore;
            public float TagScore;
            public float NetworkScore;
            public float MetadataScore;
            public bool IsFlagged;
            public string Violation;
            public string Evidence;
        }

        private readonly List<HudRow> rows = new List<HudRow>(16);
        private readonly StringBuilder builder = new StringBuilder(2048);
        private AntiCheatManager manager;
        private GameObject hudRoot;
        private TextMesh textMesh;
        private TextMesh shadowMesh;
        private float nextRefreshTime;

        public bool Visible = true;
        public KeyCode ToggleKey = KeyCode.F6;
        public Vector3 LocalPosition = new Vector3(0.45f, -0.2f, 1.25f);
        public Vector3 LocalEulerAngles = new Vector3(8f, -18f, 0f);
        public float RefreshIntervalSeconds = 0.25f;
        public int MaxRows = 8;

        public void Attach(AntiCheatManager antiCheatManager)
        {
            manager = antiCheatManager;
        }

        private void Awake()
        {
            if (manager == null)
                manager = FindObjectOfType<AntiCheatManager>();

            BuildHud();
        }

        private void OnDestroy()
        {
            if (hudRoot != null)
                Destroy(hudRoot);
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                Visible = !Visible;

            if (hudRoot == null)
                BuildHud();

            if (hudRoot == null)
                return;

            hudRoot.SetActive(Visible);
            if (!Visible)
                return;

            AttachToCamera();
            if (Time.time >= nextRefreshTime)
            {
                nextRefreshTime = Time.time + Mathf.Max(0.05f, RefreshIntervalSeconds);
                RefreshText();
            }
        }

        private void BuildHud()
        {
            hudRoot = new GameObject("GorillaTagAntiCheatInGameHud");
            DontDestroyOnLoad(hudRoot);

            GameObject shadowObject = new GameObject("DetectionHudShadow");
            shadowObject.transform.SetParent(hudRoot.transform, false);
            shadowObject.transform.localPosition = new Vector3(0.012f, -0.012f, 0.01f);
            shadowMesh = shadowObject.AddComponent<TextMesh>();
            ConfigureTextMesh(shadowMesh, Color.black);

            GameObject textObject = new GameObject("DetectionHudText");
            textObject.transform.SetParent(hudRoot.transform, false);
            textMesh = textObject.AddComponent<TextMesh>();
            ConfigureTextMesh(textMesh, new Color(0.1f, 1f, 0.65f, 1f));

            RefreshText();
        }

        private void ConfigureTextMesh(TextMesh mesh, Color color)
        {
            mesh.anchor = TextAnchor.UpperLeft;
            mesh.alignment = TextAlignment.Left;
            mesh.characterSize = 0.035f;
            mesh.fontSize = 48;
            mesh.color = color;
            mesh.richText = false;
        }

        private void AttachToCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;

            Transform cameraTransform = camera.transform;
            if (hudRoot.transform.parent != cameraTransform)
                hudRoot.transform.SetParent(cameraTransform, false);

            hudRoot.transform.localPosition = LocalPosition;
            hudRoot.transform.localRotation = Quaternion.Euler(LocalEulerAngles);
        }

        private void RefreshText()
        {
            builder.Length = 0;
            builder.AppendLine("GORILLA TAG ANTI-CHEAT");
            builder.Append("F6 HUD  F8 debug  Players: ");
            builder.Append(manager != null ? manager.PlayerHistories.Count : 0).AppendLine();
            builder.AppendLine("Score     M    T    N  Meta  Player");

            BuildRows();
            if (rows.Count == 0)
            {
                builder.AppendLine("No replicated players observed yet.");
            }
            else
            {
                int count = Mathf.Min(MaxRows, rows.Count);
                for (int i = 0; i < count; i++)
                    AppendRow(rows[i]);
            }

            builder.AppendLine();
            builder.AppendLine("Client-side signals only. No ban/kick action.");
            string text = builder.ToString();
            if (textMesh != null)
                textMesh.text = text;
            if (shadowMesh != null)
                shadowMesh.text = text;
        }

        private void BuildRows()
        {
            rows.Clear();
            if (manager == null)
                return;

            foreach (KeyValuePair<string, PlayerObservationHistory> pair in manager.PlayerHistories)
            {
                SuspicionScoreSnapshot score;
                manager.TryGetSuspicionSnapshot(pair.Key, out score);
                HudRow row = new HudRow
                {
                    PlayerId = Shorten(pair.Key, 18),
                    FinalScore = score != null ? score.FinalScore : 0f,
                    MovementScore = score != null ? score.MovementScore : 0f,
                    TagScore = score != null ? score.TagScore : 0f,
                    NetworkScore = score != null ? score.NetworkScore : 0f,
                    MetadataScore = score != null ? score.MetadataScore : 0f,
                    IsFlagged = score != null && score.IsFlagged,
                    Violation = LastViolation(score),
                    Evidence = LastEvidence(score)
                };
                rows.Add(row);
            }

            rows.Sort(CompareRows);
        }

        private int CompareRows(HudRow left, HudRow right)
        {
            return right.FinalScore.CompareTo(left.FinalScore);
        }

        private void AppendRow(HudRow row)
        {
            builder.Append(row.IsFlagged ? "FLAG " : "     ");
            builder.Append(row.FinalScore.ToString("000.0")).Append("  ");
            builder.Append(row.MovementScore.ToString("00")).Append("  ");
            builder.Append(row.TagScore.ToString("00")).Append("  ");
            builder.Append(row.NetworkScore.ToString("00")).Append("  ");
            builder.Append(row.MetadataScore.ToString("00")).Append("  ");
            builder.Append(row.PlayerId).AppendLine();

            if (!string.IsNullOrEmpty(row.Violation))
            {
                builder.Append("  > ").Append(row.Violation);
                if (!string.IsNullOrEmpty(row.Evidence))
                    builder.Append("  ").Append(row.Evidence);
                builder.AppendLine();
            }
        }

        private string LastViolation(SuspicionScoreSnapshot score)
        {
            if (score == null || score.RecentViolations == null || score.RecentViolations.Length == 0)
                return string.Empty;

            return score.RecentViolations[score.RecentViolations.Length - 1].ViolationType;
        }

        private string LastEvidence(SuspicionScoreSnapshot score)
        {
            if (score == null || score.RecentViolations == null || score.RecentViolations.Length == 0)
                return string.Empty;

            return Shorten(score.RecentViolations[score.RecentViolations.Length - 1].Evidence, 30);
        }

        private string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;

            return value.Substring(0, maxLength - 1) + "…";
        }
    }
}
