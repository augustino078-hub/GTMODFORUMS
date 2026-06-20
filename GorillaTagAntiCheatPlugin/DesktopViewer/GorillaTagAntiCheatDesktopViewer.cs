using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace GorillaTagAntiCheat.DesktopViewer
{
    internal sealed class PlayerRow
    {
        public string PlayerId = string.Empty;
        public float Final;
        public float Movement;
        public float Tag;
        public float Network;
        public float Metadata;
        public bool Flagged;
        public string Violation = string.Empty;
        public string Evidence = string.Empty;
        public DateTime Updated = DateTime.Now;
    }

    public sealed class MainForm : Form
    {
        private readonly Dictionary<string, PlayerRow> players = new Dictionary<string, PlayerRow>();
        private readonly Timer refreshTimer = new Timer();
        private readonly TextBox pathBox = new TextBox();
        private readonly Button browseButton = new Button();
        private readonly Button pauseButton = new Button();
        private readonly DataGridView grid = new DataGridView();
        private readonly TextBox eventLog = new TextBox();
        private readonly Label statusLabel = new Label();
        private long lastOffset;
        private bool paused;

        public MainForm()
        {
            Text = "Gorilla Tag Anti-Cheat Detection Viewer";
            Width = 980;
            Height = 640;
            MinimumSize = new Size(800, 500);
            BuildLayout();
            pathBox.Text = FindDefaultTelemetryPath();
            refreshTimer.Interval = 500;
            refreshTimer.Tick += delegate { RefreshTelemetry(); };
            refreshTimer.Start();
        }

        private void BuildLayout()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.RowCount = 4;
            layout.ColumnCount = 3;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            Controls.Add(layout);

            pathBox.Dock = DockStyle.Fill;
            browseButton.Text = "Browse";
            browseButton.Dock = DockStyle.Fill;
            browseButton.Click += delegate { BrowseTelemetryFile(); };
            pauseButton.Text = "Pause";
            pauseButton.Dock = DockStyle.Fill;
            pauseButton.Click += delegate { TogglePause(); };

            layout.Controls.Add(pathBox, 0, 0);
            layout.Controls.Add(browseButton, 1, 0);
            layout.Controls.Add(pauseButton, 2, 0);

            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.ReadOnly = true;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.Columns.Add("PlayerId", "Player");
            grid.Columns.Add("Final", "Score");
            grid.Columns.Add("Movement", "Move");
            grid.Columns.Add("Tag", "Tag");
            grid.Columns.Add("Network", "Net");
            grid.Columns.Add("Metadata", "Meta");
            grid.Columns.Add("Flagged", "Flagged");
            grid.Columns.Add("Violation", "Last Violation");
            grid.Columns.Add("Evidence", "Evidence");
            grid.Columns.Add("Updated", "Updated");
            layout.SetColumnSpan(grid, 3);
            layout.Controls.Add(grid, 0, 1);

            eventLog.Dock = DockStyle.Fill;
            eventLog.Multiline = true;
            eventLog.ReadOnly = true;
            eventLog.ScrollBars = ScrollBars.Vertical;
            layout.SetColumnSpan(eventLog, 3);
            layout.Controls.Add(eventLog, 0, 2);

            statusLabel.Dock = DockStyle.Fill;
            statusLabel.Text = "Waiting for telemetry...";
            layout.SetColumnSpan(statusLabel, 3);
            layout.Controls.Add(statusLabel, 0, 3);
        }

        private void BrowseTelemetryFile()
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Detection telemetry|detections.jsonl|JSON lines|*.jsonl|All files|*.*";
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                pathBox.Text = dialog.FileName;
                lastOffset = 0;
                players.Clear();
                grid.Rows.Clear();
                eventLog.Clear();
            }
        }

        private void TogglePause()
        {
            paused = !paused;
            pauseButton.Text = paused ? "Resume" : "Pause";
        }

        private void RefreshTelemetry()
        {
            if (paused)
                return;

            string path = pathBox.Text.Trim();
            if (path.Length == 0 || !File.Exists(path))
            {
                statusLabel.Text = "Telemetry file not found. Start Gorilla Tag with the mod, or browse to detections.jsonl.";
                return;
            }

            FileInfo info = new FileInfo(path);
            if (info.Length < lastOffset)
                lastOffset = 0;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                stream.Seek(lastOffset, SeekOrigin.Begin);
                using (StreamReader reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                        ReadLine(line);

                    lastOffset = stream.Position;
                }
            }

            RenderGrid();
            statusLabel.Text = "Watching " + path + " | players=" + players.Count + " | updated=" + DateTime.Now.ToLongTimeString();
        }

        private void ReadLine(string line)
        {
            string playerId = JsonString(line, "playerId");
            if (playerId.Length == 0)
                return;

            PlayerRow row;
            if (!players.TryGetValue(playerId, out row))
            {
                row = new PlayerRow();
                row.PlayerId = playerId;
                players[playerId] = row;
            }

            row.Final = JsonFloat(line, "final");
            row.Movement = JsonFloat(line, "movement");
            row.Tag = JsonFloat(line, "tag");
            row.Network = JsonFloat(line, "network");
            row.Metadata = JsonFloat(line, "metadata");
            row.Flagged = JsonBool(line, "flagged");
            row.Violation = JsonString(line, "violation");
            row.Evidence = JsonString(line, "evidence");
            row.Updated = DateTime.Now;

            if (row.Flagged || JsonString(line, "event") == "flag" || row.Violation.Length > 0)
                AppendEvent(row);
        }

        private void RenderGrid()
        {
            grid.Rows.Clear();
            foreach (PlayerRow row in players.Values)
            {
                int index = grid.Rows.Add(row.PlayerId, row.Final.ToString("0.0"), row.Movement.ToString("0.0"), row.Tag.ToString("0.0"), row.Network.ToString("0.0"), row.Metadata.ToString("0.0"), row.Flagged ? "YES" : "no", row.Violation, row.Evidence, row.Updated.ToLongTimeString());
                if (row.Flagged)
                    grid.Rows[index].DefaultCellStyle.BackColor = Color.MistyRose;
                else if (row.Final >= 25f)
                    grid.Rows[index].DefaultCellStyle.BackColor = Color.LemonChiffon;
            }
        }

        private void AppendEvent(PlayerRow row)
        {
            string line = DateTime.Now.ToLongTimeString() + "  " + row.PlayerId + "  score=" + row.Final.ToString("0.0") + "  " + row.Violation + "  " + row.Evidence;
            eventLog.AppendText(line + Environment.NewLine);
        }

        private static string JsonString(string json, string key)
        {
            Match match = Regex.Match(json, "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*\\\"((?:\\\\.|[^\\\"])*)\\\"");
            if (!match.Success)
                return string.Empty;

            return match.Groups[1].Value.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        private static float JsonFloat(string json, string key)
        {
            Match match = Regex.Match(json, "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?)");
            if (!match.Success)
                return 0f;

            float value;
            if (float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                return value;

            return 0f;
        }

        private static bool JsonBool(string json, string key)
        {
            Match match = Regex.Match(json, "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(true|false)");
            return match.Success && match.Groups[1].Value == "true";
        }

        private static string FindDefaultTelemetryPath()
        {
            string current = Path.Combine(Environment.CurrentDirectory, "BepInEx", "config", "GorillaTagAntiCheat", "detections.jsonl");
            if (File.Exists(current))
                return current;

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string steam = Path.Combine(programFiles, "Steam", "steamapps", "common", "Gorilla Tag", "BepInEx", "config", "GorillaTagAntiCheat", "detections.jsonl");
            if (File.Exists(steam))
                return steam;

            return current;
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
