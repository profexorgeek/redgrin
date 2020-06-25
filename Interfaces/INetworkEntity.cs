using RedGrin.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedGrin.Interfaces
{

    /// <summary>
    /// An entity that is updated across a network.
    /// 
    /// Entities have an owner, an ID and some object
    /// that describes the state of an entity. The 
    /// network manager will call GetState to 
    /// 
    /// </summary>
    public interface INetworkEntity
    {
        /// <summary>
        /// This value encodes both the client ID that owns this object
        /// and the objects ID to form a unique identifier across the
        /// entire network
        /// </summary>
        ulong UniqueId { get; set; }

        /// <summary>
        /// Gets a transferrable object from the entity.
        /// 
        /// Called by the NetworkManager when broadcasting this Entity's
        /// current state across the network.
        /// </summary>
        /// <returns>A generic object describing an entity state</returns>
        object GetState();

        /// <summary>
        /// Applies a state from the network to the local entity. Usually called when
        /// an update is received from the network for this EntityId
        /// </summary>
        /// <param name="entityState">A generic object describing an entity state </param>
        /// <param name="stateTime">The time this state was received, compare to current network time to understand elapsed time</param>
        /// <param name="isReckoningState">Whether or not this is a dead reckoning update</param>
        void UpdateFromState(object entityState, double stateTime);
    }

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
        /// Sets the UniqueId on the provided entity by packing the
        /// client ID and entity Id values.
        /// </summary>
        /// <param name="entity">The INetworkEntity to affect</param>
        /// <param name="clientId">The client identifier creating the entity</param>
        /// <param name="entityId">The entity's local identifier on the client</param>
        public static void SetUniqueId(this INetworkEntity entity, byte clientId, uint entityId)
        {
            entity.UniqueId = PackUniqueId(clientId, entityId);
        }

        /// <summary>
        /// Gets the owning client's identifier from an entity's UniqueId
        /// </summary>
        /// <param name="entity">The INetworkEntity to use</param>
        /// <returns>The client ID that created the entity</returns>
        public static byte GetClientId(this INetworkEntity entity)
        {
            return UnpackClientId(entity.UniqueId);
        }
    }
}
