using System.Collections.Generic;
using UnityEngine;

namespace GorillaTagAntiCheat
{
    public struct HandTransformSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public bool IsValid;

        public HandTransformSnapshot(Vector3 position, Quaternion rotation, bool isValid)
        {
            Position = position;
            Rotation = rotation;
            IsValid = isValid;
        }
    }

    public struct PlayerTickSnapshot
    {
        public string PlayerId;
        public Vector3 Position;
        public Vector3 Velocity;
        public HandTransformSnapshot LeftHand;
        public HandTransformSnapshot RightHand;
        public float LocalTime;
        public int PhotonTimestamp;
        public int RoundTripTimeMs;
        public bool IsTagged;
        public bool IsGrounded;
        public bool IsTouchingClimbSurface;
        public Bounds BodyBounds;
        public bool HasValidBodyBounds;
        public IReadOnlyDictionary<string, object> PhotonCustomProperties;
    }

    public enum NetworkEventKind
    {
        Rpc,
        PhotonEvent,
        StateReplication
    }

    public struct CollisionObservation
    {
        public string SourcePlayerId;
        public string OtherPlayerId;
        public Vector3 ContactPoint;
        public float LocalTime;
        public bool IsBodyOverlap;
        public bool IsHandOverlap;
    }

    public struct TagEventObservation
    {
        public string SourcePlayerId;
        public string TargetPlayerId;
        public Vector3 HandPosition;
        public Vector3 TargetHitboxPosition;
        public float LocalTime;
        public int PhotonTimestamp;
        public bool StateChanged;
    }

    public struct NetworkEventObservation
    {
        public string PlayerId;
        public NetworkEventKind Kind;
        public string EventName;
        public float LocalTime;
        public int SizeBytes;
        public int RoundTripTimeMs;
    }
}
