using System.Collections.Generic;
using UnityEngine;

namespace GorillaTagAntiCheat
{
    public sealed class PlayerObservationHistory
    {
        private readonly PlayerTickSnapshot[] snapshots;
        private readonly CollisionObservation[] collisions;
        private readonly TagEventObservation[] tagEvents;
        private readonly NetworkEventObservation[] networkEvents;
        private int snapshotCursor;
        private int collisionCursor;
        private int tagCursor;
        private int networkCursor;
        private int snapshotCount;
        private int collisionCount;
        private int tagCount;
        private int networkCount;

        public PlayerObservationHistory(string playerId, int capacity)
        {
            PlayerId = playerId;
            snapshots = new PlayerTickSnapshot[Mathf.Max(16, capacity)];
            collisions = new CollisionObservation[Mathf.Max(16, capacity)];
            tagEvents = new TagEventObservation[Mathf.Max(16, capacity)];
            networkEvents = new NetworkEventObservation[Mathf.Max(16, capacity)];
            FirstSeenTime = -1f;
        }

        public string PlayerId { get; private set; }
        public float FirstSeenTime { get; private set; }
        public int SnapshotCount { get { return snapshotCount; } }
        public int TagEventCount { get { return tagCount; } }
        public int CollisionCount { get { return collisionCount; } }

        public float TimeSinceFirstSeen(float now)
        {
            if (FirstSeenTime < 0f)
                return 0f;

            return Mathf.Max(0f, now - FirstSeenTime);
        }

        public void AddSnapshot(PlayerTickSnapshot snapshot)
        {
            if (FirstSeenTime < 0f)
                FirstSeenTime = snapshot.LocalTime;

            snapshots[snapshotCursor] = snapshot;
            snapshotCursor = (snapshotCursor + 1) % snapshots.Length;
            if (snapshotCount < snapshots.Length)
                snapshotCount++;
        }

        public void AddCollision(CollisionObservation collision)
        {
            collisions[collisionCursor] = collision;
            collisionCursor = (collisionCursor + 1) % collisions.Length;
            if (collisionCount < collisions.Length)
                collisionCount++;
        }

        public void AddTagEvent(TagEventObservation tagEvent)
        {
            tagEvents[tagCursor] = tagEvent;
            tagCursor = (tagCursor + 1) % tagEvents.Length;
            if (tagCount < tagEvents.Length)
                tagCount++;
        }

        public void AddNetworkEvent(NetworkEventObservation networkEvent)
        {
            networkEvents[networkCursor] = networkEvent;
            networkCursor = (networkCursor + 1) % networkEvents.Length;
            if (networkCount < networkEvents.Length)
                networkCount++;
        }

        public bool TryGetLatestSnapshot(out PlayerTickSnapshot snapshot)
        {
            return TryGetSnapshotFromNewest(0, out snapshot);
        }

        public bool TryGetPreviousSnapshot(out PlayerTickSnapshot snapshot)
        {
            return TryGetSnapshotFromNewest(1, out snapshot);
        }

        public bool TryGetSnapshotFromNewest(int indexFromNewest, out PlayerTickSnapshot snapshot)
        {
            if (indexFromNewest < 0 || indexFromNewest >= snapshotCount)
            {
                snapshot = default(PlayerTickSnapshot);
                return false;
            }

            int index = snapshotCursor - 1 - indexFromNewest;
            while (index < 0)
                index += snapshots.Length;

            snapshot = snapshots[index];
            return true;
        }

        public bool TryGetLastTagEvent(out TagEventObservation tagEvent)
        {
            if (tagCount == 0)
            {
                tagEvent = default(TagEventObservation);
                return false;
            }

            int index = tagCursor - 1;
            if (index < 0)
                index += tagEvents.Length;

            tagEvent = tagEvents[index];
            return true;
        }

        public int CopyRecentSnapshots(float sinceTime, List<PlayerTickSnapshot> destination)
        {
            destination.Clear();
            for (int i = snapshotCount - 1; i >= 0; i--)
            {
                PlayerTickSnapshot snapshot;
                if (!TryGetSnapshotFromNewest(i, out snapshot))
                    continue;

                if (snapshot.LocalTime >= sinceTime)
                    destination.Add(snapshot);
            }

            return destination.Count;
        }

        public bool HasCollisionWith(string otherPlayerId, float atTime, float windowSeconds)
        {
            for (int i = 0; i < collisionCount; i++)
            {
                int index = collisionCursor - 1 - i;
                while (index < 0)
                    index += collisions.Length;

                CollisionObservation collision = collisions[index];
                float delta = Mathf.Abs(collision.LocalTime - atTime);
                if (delta > windowSeconds)
                {
                    if (collision.LocalTime < atTime - windowSeconds)
                        break;

                    continue;
                }

                if (collision.OtherPlayerId == otherPlayerId && (collision.IsBodyOverlap || collision.IsHandOverlap))
                    return true;
            }

            return false;
        }

        public int CountNetworkEvents(NetworkEventKind kind, float sinceTime)
        {
            int count = 0;
            for (int i = 0; i < networkCount; i++)
            {
                int index = networkCursor - 1 - i;
                while (index < 0)
                    index += networkEvents.Length;

                NetworkEventObservation networkEvent = networkEvents[index];
                if (networkEvent.LocalTime < sinceTime)
                    break;

                if (networkEvent.Kind == kind)
                    count++;
            }

            return count;
        }

        public bool HasLatencySpike(float sinceTime, float spikeMs)
        {
            for (int i = 0; i < networkCount; i++)
            {
                int index = networkCursor - 1 - i;
                while (index < 0)
                    index += networkEvents.Length;

                NetworkEventObservation networkEvent = networkEvents[index];
                if (networkEvent.LocalTime < sinceTime)
                    break;

                if (networkEvent.RoundTripTimeMs >= spikeMs)
                    return true;
            }

            return false;
        }

        public bool HasRecentTagEvent(float sinceTime)
        {
            if (tagCount == 0)
                return false;

            int index = tagCursor - 1;
            if (index < 0)
                index += tagEvents.Length;

            return tagEvents[index].LocalTime >= sinceTime;
        }
    }
}
