using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlatRedNetwork
{
    /// <summary>
    /// A game arena that deals with networked objects.
    /// </summary>
    public interface INetworkArena
    {
        /// <summary>
        /// Called by the NetworkManager when CreateEntity message is received.
        /// </summary>
        /// <param name="entity">The entity to create</param>
        INetworkEntity RequestCreateEntity(long ownerId, long entityId, object entityData);

        /// <summary>
        /// Called by the NetworkManager when DestroyEntity message is received.
        /// </summary>
        /// <param name="entity">The entity to destroy</param>
        void RequestDestroyEntity(INetworkEntity entity);
    }
}
