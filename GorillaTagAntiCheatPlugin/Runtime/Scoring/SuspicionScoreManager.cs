using System.Collections.Generic;
using UnityEngine;

namespace GorillaTagAntiCheat
{
    public enum ViolationCategory
    {
        Movement,
        TagIntegrity,
        Network,
        Metadata
    }

    public struct ViolationRecord
    {
        public string PlayerId;
        public string ViolationType;
        public ViolationCategory Category;
        public float Weight;
        public float LocalTime;
        public string Evidence;
    }

    public sealed class SuspicionScoreSnapshot
    {
        public string PlayerId;
        public float MovementScore;
        public float TagScore;
        public float NetworkScore;
        public float MetadataScore;
        public float FinalScore;
        public int ActiveSignalCategories;
        public bool IsFlagged;
        public ViolationRecord[] RecentViolations;
    }

    internal sealed class PlayerSuspicionState
    {
        private readonly ViolationRecord[] recentViolations;
        private int violationCursor;
        private int violationCount;

        public PlayerSuspicionState(string playerId, int maxViolations)
        {
            PlayerId = playerId;
            recentViolations = new ViolationRecord[Mathf.Max(8, maxViolations)];
        }

        public string PlayerId { get; private set; }
        public float MovementScore;
        public float TagScore;
        public float NetworkScore;
        public float MetadataScore;

        public float FinalScore
        {
            get { return MovementScore + TagScore + NetworkScore + MetadataScore; }
        }

        public int ActiveSignalCategories
        {
            get
            {
                int count = 0;
                if (MovementScore > 0.01f)
                    count++;
                if (TagScore > 0.01f)
                    count++;
                if (NetworkScore > 0.01f)
                    count++;
                if (MetadataScore > 0.01f)
                    count++;
                return count;
            }
        }

        public void AddViolation(ViolationRecord record)
        {
            switch (record.Category)
            {
                case ViolationCategory.Movement:
                    MovementScore += record.Weight;
                    break;
                case ViolationCategory.TagIntegrity:
                    TagScore += record.Weight;
                    break;
                case ViolationCategory.Network:
                    NetworkScore += record.Weight;
                    break;
                case ViolationCategory.Metadata:
                    MetadataScore += record.Weight;
                    break;
            }

            recentViolations[violationCursor] = record;
            violationCursor = (violationCursor + 1) % recentViolations.Length;
            if (violationCount < recentViolations.Length)
                violationCount++;
        }

        public void Decay(float amount)
        {
            MovementScore = Mathf.Max(0f, MovementScore - amount);
            TagScore = Mathf.Max(0f, TagScore - amount);
            NetworkScore = Mathf.Max(0f, NetworkScore - amount);
            MetadataScore = Mathf.Max(0f, MetadataScore - amount);
        }

        public SuspicionScoreSnapshot ToSnapshot(AntiCheatSettings settings)
        {
            ViolationRecord[] records = new ViolationRecord[violationCount];
            for (int i = 0; i < violationCount; i++)
            {
                int index = violationCursor - violationCount + i;
                while (index < 0)
                    index += recentViolations.Length;

                records[i] = recentViolations[index];
            }

            return new SuspicionScoreSnapshot
            {
                PlayerId = PlayerId,
                MovementScore = MovementScore,
                TagScore = TagScore,
                NetworkScore = NetworkScore,
                MetadataScore = MetadataScore,
                FinalScore = FinalScore,
                ActiveSignalCategories = ActiveSignalCategories,
                IsFlagged = IsFlagged(settings),
                RecentViolations = records
            };
        }

        public bool IsFlagged(AntiCheatSettings settings)
        {
            if (FinalScore >= settings.HeavySingleSignalThreshold)
                return true;

            return FinalScore >= settings.FlagThreshold && ActiveSignalCategories >= settings.RequiredSignalCategories;
        }
    }

    public sealed class SuspicionScoreManager
    {
        private readonly Dictionary<string, PlayerSuspicionState> states = new Dictionary<string, PlayerSuspicionState>(32);
        private readonly List<string> playerScratch = new List<string>(32);
        private AntiCheatSettings settings;

        public SuspicionScoreManager(AntiCheatSettings settings)
        {
            this.settings = settings;
        }

        public void SetSettings(AntiCheatSettings nextSettings)
        {
            settings = nextSettings;
        }

        public void AddViolation(string playerId, string violationType, ViolationCategory category, float weight, float localTime, string evidence)
        {
            if (string.IsNullOrEmpty(playerId) || weight <= 0f)
                return;

            PlayerSuspicionState state = GetOrCreate(playerId);
            state.AddViolation(new ViolationRecord
            {
                PlayerId = playerId,
                ViolationType = violationType,
                Category = category,
                Weight = weight,
                LocalTime = localTime,
                Evidence = evidence
            });
        }

        public void AddContextBonus(string playerId, string contextType, float weight, float localTime, string evidence)
        {
            AddViolation(playerId, contextType, ViolationCategory.Metadata, weight, localTime, evidence);
        }

        public void Decay(float deltaTime)
        {
            float amount = settings.DecayPerSecond * Mathf.Max(0f, deltaTime);
            if (amount <= 0f)
                return;

            playerScratch.Clear();
            foreach (KeyValuePair<string, PlayerSuspicionState> pair in states)
                playerScratch.Add(pair.Key);

            for (int i = 0; i < playerScratch.Count; i++)
                states[playerScratch[i]].Decay(amount);
        }

        public float GetCategoryScore(string playerId, ViolationCategory category)
        {
            PlayerSuspicionState state;
            if (!states.TryGetValue(playerId, out state))
                return 0f;

            switch (category)
            {
                case ViolationCategory.Movement:
                    return state.MovementScore;
                case ViolationCategory.TagIntegrity:
                    return state.TagScore;
                case ViolationCategory.Network:
                    return state.NetworkScore;
                case ViolationCategory.Metadata:
                    return state.MetadataScore;
                default:
                    return 0f;
            }
        }

        public bool TryGetSnapshot(string playerId, out SuspicionScoreSnapshot snapshot)
        {
            PlayerSuspicionState state;
            if (!states.TryGetValue(playerId, out state))
            {
                snapshot = null;
                return false;
            }

            snapshot = state.ToSnapshot(settings);
            return true;
        }

        public void CopyFlaggedPlayers(List<SuspicionScoreSnapshot> destination)
        {
            destination.Clear();
            foreach (KeyValuePair<string, PlayerSuspicionState> pair in states)
            {
                if (pair.Value.IsFlagged(settings))
                    destination.Add(pair.Value.ToSnapshot(settings));
            }
        }

        private PlayerSuspicionState GetOrCreate(string playerId)
        {
            PlayerSuspicionState state;
            if (!states.TryGetValue(playerId, out state))
            {
                state = new PlayerSuspicionState(playerId, 32);
                states.Add(playerId, state);
            }

            return state;
        }
    }
}
