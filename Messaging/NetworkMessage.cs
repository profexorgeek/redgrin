using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RedGrin.Messaging
{
    /// <summary>
    /// A message capable of transferring almost any type of data across the network.
    /// </summary>
    internal class NetworkMessage
    {
        const BindingFlags DefaultBinding = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;

        // not encoded, derived from the remote connection
        public long SenderId { get; set; }

        // Header properties encoded in every message
        public double MessageSentTime { get; set; }
        public NetworkMessageType MessageType { get; set; }
        public long UniqueId { get; set; }
        public int PayloadTypeId { get; set; }

        // The actual message data payload
        public object Payload { get; set; }

        /// <summary>
        /// Basic constructor, does nothing special
        /// </summary>
        public NetworkMessage() { }

        /// <summary>
        /// Shortcut constructor, immediately decodes an incoming message
        /// </summary>
        /// <param name="msg">Incoming message to immediately decode.</param>
        public NetworkMessage(NetIncomingMessage msg)
        {
            this.Decode(msg);
        }

        /// <summary>
        /// Populates message properties from an incoming lidgren message
        /// Uses reflection to create and populate a payload object
        /// </summary>
        /// <param name="msg">The message to parse.</param>
        public void Decode(NetIncomingMessage msg)
        {
            SenderId = msg.SenderConnection.RemoteUniqueIdentifier;

            // read "header" properties
            MessageSentTime = msg.ReadDouble();
            MessageType = (NetworkMessageType)msg.ReadByte();
            UniqueId = msg.ReadInt64();
            PayloadTypeId = msg.ReadInt32();

            // Destroy messages have no payload type, only an EntityId
            if(MessageType != NetworkMessageType.Destroy) {
                try
                {
                    Type payloadType = NetworkManager.EntityStateTypes[PayloadTypeId];
                    Payload = Activator.CreateInstance(payloadType);

                    // Lidgren methods automatically read correct type for field types via reflection
                    msg.ReadAllFields(Payload, DefaultBinding);
                    msg.ReadAllProperties(Payload, DefaultBinding);
                }
                catch(NetException ex)
                {
                    throw new RedGrinException("Error reading entity state from network message.", ex);
                }
                catch(SystemException ex)
                {
                    throw new RedGrinException("Error instantiating type.", ex);
                }
            }
        }

        /// <summary>
        /// Serializes this message object into the provided outgoing lidgren message.
        /// Requires server time to mark when this message was prepared for sending.
        /// Uses reflection to serialize fields and properties from the payload object.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="serverTime"></param>
        public void Encode(NetOutgoingMessage msg, double serverTime)
        {
            MessageSentTime = serverTime;

            // write "header" properties
            msg.Write(MessageSentTime);
            msg.Write((byte)MessageType);
            msg.Write(UniqueId);
            msg.Write(PayloadTypeId);

            try
            {
                // Lidgren methods automatically write correct type for field types via reflection
                msg.WriteAllFields(Payload, DefaultBinding);
                msg.WriteAllProperties(Payload, DefaultBinding);
            }
            catch(NetException ex)
            {
                throw new RedGrinException("Error writing entity state to network message.", ex);
            }
            catch(SystemException ex)
            {
                throw new RedGrinException("Unknown message error.", ex);
            }
        }

        /// <summary>
        /// Automatically checks if payload is valid instance of the provided type and returns it.
        /// Throws an exception if the payload is not the correct type.
        /// </summary>
        /// <typeparam name="T">The type the payload is expected to be</typeparam>
        /// <returns>The payload as the provided type</returns>
        public T GetPayloadAsType<T>()
        {
            if(!(Payload is T))
            {
                string msg = string.Format("Could not cast {0} as {1}", Payload.GetType(), typeof(T));
                throw new RedGrinException(msg);
            }

            return (T)Payload;
        }
        
    }
}
