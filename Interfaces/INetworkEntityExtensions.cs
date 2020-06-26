using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedGrin.Interfaces
{

    /// <summary>
    /// Utility methods that extend INetworkEntity implementations
    /// </summary>
    public static class INetworkEntityExtensions
    {
        static readonly int ClientIdBitOffset = (64 - 8);

        /// <summary>
        /// Uses bit shifting to pack the client ID and entity ID
        /// into a single ulong. This allows clients to generate
        /// an identifier that will be unique on the network using
        /// their local byte identifier
        /// </summary>
        /// <param name="clientId">The client identifier creating the entity</param>
        /// <param name="entityId">The entity's local identifier on the client</param>
        public static ulong PackUniqueId(byte clientId, uint entityId)
        {
            ulong clientIdShifted = (ulong)clientId << ClientIdBitOffset;
            return clientIdShifted | (ulong)entityId;
        }

        /// <summary>
        /// Unpacks the client ID from a packed UniqueId
        /// </summary>
        /// <param name="packedId">A UniqueId on an INetworkEntity</param>
        /// <returns>The client ID that created the entity</returns>
        public static byte UnpackClientId(ulong packedId)
        {
            return (byte)(packedId >> ClientIdBitOffset);
        }

        /// <summary>
        /// Unpacks an entity's local ID from a packed UniqueId
        /// </summary>
        /// <param name="packedId">A UniqueId on an INetworkEntity</param>
        /// <returns>The local entity identifier</returns>
        public static uint UnpackEntityId(ulong packedId)
        {
            return (uint)(packedId & uint.MaxValue);
        }

        /// <summary>
        /// Sets the Unique Id on the provided entity by packing the
        /// client ID and entity Id values.
        /// </summary>
        /// <param name="entity">The INetworkEntity to affect</param>
        /// <param name="clientId">The client identifier creating the entity</param>
        /// <param name="entityId">The entity's local identifier on the client</param>
        public static void SetEntityId(this INetworkEntity entity, byte clientId, uint entityId)
        {
            entity.Id = PackUniqueId(clientId, entityId);
        }

        /// <summary>
        /// Gets the owning client's identifier from an entity's UniqueId
        /// </summary>
        /// <param name="entity">The INetworkEntity to use</param>
        /// <returns>The client ID that created the entity</returns>
        public static byte GetClientId(this INetworkEntity entity)
        {
            return UnpackClientId(entity.Id);
        }

        /// <summary>
        /// Checks whether an entity is owned by this client on the
        /// network by unpacking the client ID from the entity and
        /// comparing it to the network's client ID
        /// </summary>
        /// <param name="entity">The entity to check</param>
        /// <returns>True if entity's owner ID matches this Client ID</returns>
        public static bool IsOwned(this INetworkEntity entity)
        {
            var id = UnpackClientId(entity.Id);
            return id == NetworkManager.Self.ClientId;
        }
    }
}
