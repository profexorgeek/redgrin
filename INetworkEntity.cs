using RedGrin.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedGrin
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
        /// The network ID for the client that "owns" this entity.
        /// </summary>
        long OwnerId { get; set; }

        /// <summary>
        /// The unique ID for this entity across all network peers.
        /// This will be set automatically by the NetworkManager and 
        /// should not be changed manually at runtime.
        /// </summary>
        long EntityId { get; set; }

        /// <summary>
        /// Gets a transferrable object from the INetworkEntity.
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
        void UpdateState(object entityState, double stateTime, bool isReckoningState = false);
    }
}
