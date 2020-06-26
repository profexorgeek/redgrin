using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedGrin.Interfaces
{
    /// <summary>
    /// A game arena that deals with networked objects.
    /// In FlatRedBall the multi-player game Screen should
    /// implement this.
    /// </summary>
    public interface INetworkArena
    {
        /// <summary>
        /// Called by the network manager when a message is received to create an entity.
        /// After the game arena creates the visual representation of the entity, it
        /// will be stored by the NetworkManager, independent of the NetworkArena's tracking.
        /// </summary>
        /// <param name="uniqueId">The unique ID of the entity on the network</param>
        /// <param name="entityData">The initial state of the created entity</param>
        /// <returns>The created INetworkEntity, which will be tracked by the NetworkManager</returns>
        INetworkEntity HandleCreateEntity(ulong uniqueId, object entityData);

        /// <summary>
        /// Called by the network manager when a message is received to destroy an entity.
        /// This should destroy the visual representation of the entity owned by the Screen.
        /// After this method is called, the NetworkManager will destroy its copy.
        /// </summary>
        /// <param name="entity">The entity to destroy.</param>
        void HandleDestroyEntity(INetworkEntity entity);

        /// <summary>
        /// Called by the network manager when a generic message is received. Generic
        /// messages are not attached to a specific entity and may contain game state
        /// or other meta data that the INetworkArena should handle.
        /// </summary>
        /// <param name="message">The message payload.</param>
        /// <param name="messageTime">The time the message was sent.</param>
        void HandleGenericMessage(ulong messageId, object message, double messageTime);
    }
}
