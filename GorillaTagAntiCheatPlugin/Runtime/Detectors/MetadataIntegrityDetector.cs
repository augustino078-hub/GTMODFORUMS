using System.Collections.Generic;

namespace GorillaTagAntiCheat
{
    public sealed class MetadataIntegrityDetector : IMetadataIntegrityDetector
    {
        public string Name { get { return "MetadataIntegrity"; } }
        public bool Enabled { get; set; }
        public ViolationCategory Category { get { return ViolationCategory.Metadata; } }

        public MetadataIntegrityDetector()
        {
            Enabled = true;
        }

        public void Analyze(PlayerAnalysisContext context, DetectionResultSink results)
        {
            if (!Enabled || !context.Settings.EnableMetadataDetectors)
                return;

            PlayerTickSnapshot current;
            if (!context.History.TryGetLatestSnapshot(out current))
                return;

            IReadOnlyDictionary<string, object> properties = current.PhotonCustomProperties;
            if (properties == null || properties.Count == 0)
                return;

            int unknownMetadataScore = 0;
            foreach (KeyValuePair<string, object> property in properties)
            {
                if (!context.Settings.AllowedPhotonPropertyKeys.Contains(property.Key))
                    unknownMetadataScore++;
            }

            if (unknownMetadataScore <= context.Settings.MaxUnknownPhotonKeysBeforeContext)
                return;

            float movementScore = context.Scores.GetCategoryScore(context.PlayerId, ViolationCategory.Movement);
            if (movementScore > context.Settings.MetadataMovementContextThreshold)
            {
                results.AddContextBonus(context.PlayerId, "UnknownPhotonData", 2f, current.LocalTime, "unknownKeys=" + unknownMetadataScore);
            }
        }
    }
}
