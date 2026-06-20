using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GorillaTagAntiCheat
{
    public sealed class GorillaTagPhotonStateSource : IPhotonStateSource
    {
        private readonly List<MonoBehaviour> rigScratch = new List<MonoBehaviour>(16);
        private readonly Dictionary<string, object> propertyScratch = new Dictionary<string, object>(16);
        private float nextRigRefreshTime;

        public void CollectPlayerSnapshots(List<PlayerTickSnapshot> destination)
        {
            destination.Clear();
            RefreshRigsIfNeeded();

            for (int i = rigScratch.Count - 1; i >= 0; i--)
            {
                MonoBehaviour rig = rigScratch[i];
                if (rig == null)
                {
                    rigScratch.RemoveAt(i);
                    continue;
                }

                string playerId = ResolvePlayerId(rig);
                if (string.IsNullOrEmpty(playerId))
                    playerId = rig.GetInstanceID().ToString();

                Transform rigTransform = rig.transform;
                Vector3 velocity = ReadVector3(rig, "velocity", "currentVelocity", "playerVelocity");
                Transform leftHand = ReadTransform(rig, "leftHandTransform", "leftHand", "leftHandTarget", "leftHandFollower");
                Transform rightHand = ReadTransform(rig, "rightHandTransform", "rightHand", "rightHandTarget", "rightHandFollower");
                Bounds bounds = ResolveBodyBounds(rig);

                destination.Add(new PlayerTickSnapshot
                {
                    PlayerId = playerId,
                    Position = rigTransform.position,
                    Velocity = velocity,
                    LeftHand = SnapshotHand(leftHand),
                    RightHand = SnapshotHand(rightHand),
                    LocalTime = Time.time,
                    PhotonTimestamp = ResolvePhotonTimestamp(),
                    RoundTripTimeMs = ResolvePing(),
                    IsTagged = ReadBool(rig, "isTagged", "tagged", "inTaggedMode"),
                    IsGrounded = ReadBool(rig, "isGrounded", "grounded", "onGround"),
                    IsTouchingClimbSurface = ReadBool(rig, "isTouchingClimbSurface", "touchingClimbSurface", "isClimbing"),
                    BodyBounds = bounds,
                    HasValidBodyBounds = bounds.extents.sqrMagnitude > 0.0001f,
                    PhotonCustomProperties = CopyCustomProperties(rig)
                });
            }
        }

        private void RefreshRigsIfNeeded()
        {
            if (Time.time < nextRigRefreshTime)
                return;

            nextRigRefreshTime = Time.time + 1f;
            rigScratch.Clear();
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                string typeName = behaviour.GetType().Name;
                if (typeName == "VRRig" || typeName == "GorillaPlayerNetworkedRig" || typeName.EndsWith("VRRig", StringComparison.Ordinal))
                    rigScratch.Add(behaviour);
            }
        }

        private string ResolvePlayerId(MonoBehaviour rig)
        {
            object owner = ReadMember(rig, "photonView.owner", "photonView.Owner", "creator", "owner", "player", "netPlayer");
            string userId = ReadString(owner, "UserId", "userId", "NickName", "nickName");
            if (!string.IsNullOrEmpty(userId))
                return userId;

            string rigId = ReadString(rig, "playerId", "userId", "nickName", "playerName");
            return rigId;
        }

        private int ResolvePing()
        {
            Type photonNetwork = FindType("Photon.Pun.PhotonNetwork");
            if (photonNetwork == null)
                return 0;

            object value = ReadStaticMember(photonNetwork, "GetPing", "NetworkingClient.LoadBalancingPeer.RoundTripTime");
            if (value is int)
                return (int)value;

            return 0;
        }

        private int ResolvePhotonTimestamp()
        {
            Type photonNetwork = FindType("Photon.Pun.PhotonNetwork");
            if (photonNetwork == null)
                return 0;

            object value = ReadStaticMember(photonNetwork, "ServerTimestamp");
            if (value is int)
                return (int)value;

            return 0;
        }

        private IReadOnlyDictionary<string, object> CopyCustomProperties(MonoBehaviour rig)
        {
            propertyScratch.Clear();
            object owner = ReadMember(rig, "photonView.Owner", "photonView.owner", "owner", "player", "netPlayer");
            object properties = ReadMember(owner, "CustomProperties", "customProperties");
            IDictionary dictionary = properties as IDictionary;
            if (dictionary == null)
                return new Dictionary<string, object>(0);

            foreach (DictionaryEntry entry in dictionary)
            {
                string key = entry.Key as string;
                if (!string.IsNullOrEmpty(key))
                    propertyScratch[key] = entry.Value;
            }

            return new Dictionary<string, object>(propertyScratch);
        }

        private Bounds ResolveBodyBounds(MonoBehaviour rig)
        {
            Collider collider = rig.GetComponentInChildren<Collider>();
            if (collider != null)
                return collider.bounds;

            return new Bounds(rig.transform.position, new Vector3(0.65f, 1.2f, 0.65f));
        }

        private HandTransformSnapshot SnapshotHand(Transform transform)
        {
            if (transform == null)
                return new HandTransformSnapshot(Vector3.zero, Quaternion.identity, false);

            return new HandTransformSnapshot(transform.position, transform.rotation, true);
        }

        private Transform ReadTransform(object target, params string[] names)
        {
            object value = ReadMember(target, names);
            Transform transform = value as Transform;
            if (transform != null)
                return transform;

            GameObject gameObject = value as GameObject;
            if (gameObject != null)
                return gameObject.transform;

            Component component = value as Component;
            if (component != null)
                return component.transform;

            return null;
        }

        private Vector3 ReadVector3(object target, params string[] names)
        {
            object value = ReadMember(target, names);
            if (value is Vector3)
                return (Vector3)value;

            return Vector3.zero;
        }

        private bool ReadBool(object target, params string[] names)
        {
            object value = ReadMember(target, names);
            if (value is bool)
                return (bool)value;

            return false;
        }

        private string ReadString(object target, params string[] names)
        {
            object value = ReadMember(target, names);
            string text = value as string;
            if (!string.IsNullOrEmpty(text))
                return text;

            return null;
        }

        private object ReadStaticMember(Type type, params string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                object value = ReadPath(null, type, paths[i]);
                if (value != null)
                    return value;
            }

            return null;
        }

        private object ReadMember(object target, params string[] paths)
        {
            if (target == null)
                return null;

            Type type = target.GetType();
            for (int i = 0; i < paths.Length; i++)
            {
                object value = ReadPath(target, type, paths[i]);
                if (value != null)
                    return value;
            }

            return null;
        }

        private object ReadPath(object target, Type rootType, string path)
        {
            string[] parts = path.Split('.');
            object current = target;
            Type currentType = rootType;
            for (int i = 0; i < parts.Length; i++)
            {
                object next = ReadSingleMember(current, currentType, parts[i]);
                if (next == null)
                    return null;

                current = next;
                currentType = current.GetType();
            }

            return current;
        }

        private object ReadSingleMember(object target, Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.GetIndexParameters().Length == 0)
                return property.GetValue(target, null);

            FieldInfo field = type.GetField(name, flags);
            if (field != null)
                return field.GetValue(target);

            MethodInfo method = type.GetMethod(name, flags, null, Type.EmptyTypes, null);
            if (method != null)
                return method.Invoke(target, null);

            return null;
        }

        private Type FindType(string fullName)
        {
            Type direct = Type.GetType(fullName);
            if (direct != null)
                return direct;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
