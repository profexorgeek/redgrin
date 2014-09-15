using FlatRedNetwork.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlatRedNetwork
{
    /// <summary>
    /// An entity that is updated across a network
    /// </summary>
    public interface INetworkEntity
    {
        /// <summary>
        /// The network ID for the client that controls this entity
        /// </summary>
        long OwnerId { get; set; }

        /// <summary>
        /// The unique ID for this entity. This will be set automatically by the NetworkManager and
        /// should not be changed manually at runtime.
        /// </summary>
        long EntityId { get; set; }

        /// <summary>
        /// Gets a transferrable object from the INetworkEntity
        /// </summary>
        /// <returns>An DTO describing an entity state</returns>
        object GetState();

        /// <summary>
        /// Applies a state from the network to the local entity
        /// </summary>
        /// <param name="entityState">A DTO describing an entity state </param>
        void UpdateState(object entityState, double stateTime, bool isReckoningState = false);
    }
}
