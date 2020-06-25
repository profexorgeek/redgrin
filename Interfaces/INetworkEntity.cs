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
}
