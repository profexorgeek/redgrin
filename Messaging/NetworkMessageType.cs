using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RedGrin.Messaging
{
    /// <summary>
    /// The types of message the network is capable of sending.
    /// </summary>
    internal enum NetworkMessageType
    {
        /// <summary>
        /// Generic messages are not attached to a specific entity
        /// and are instead handed directly to the INetworkArena.
        /// </summary>
        Generic = 0,

        /// <summary>
        /// Create messages are broadcast when a new entity is created
        /// and result in a call to INetworkArena.RequestCreateEntity
        /// </summary>
        Create = 1,

        /// <summary>
        /// Update messages are broadcast when an entity state changes
        /// and result in a call to INetworkEntity.UpdateFromState
        /// </summary>
        Update = 2,

        /// <summary>
        /// Destroy messages are broadcast when an entity is destroyed
        /// and result in a call to INetworkArena.RequestDestroyEntity
        /// </summary>
        Destroy = 3,

        /// <summary>
        /// Reckoning messages are sent by the server to all clients
        /// to sync states across the network. During a reckoning, a
        /// message is generated for every INetworkEntity tracked by
        /// the NetworkManager.
        /// </summary>
        Reckoning = 4,
    }
}
