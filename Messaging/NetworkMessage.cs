using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FlatRedNetwork.Messaging
{
    internal class NetworkMessage
    {
        public double MessageSentTime { get; set; }
        public long SenderId { get; set; }
        public long EntityId { get; set; }
        public long OwnerId { get; set; }
        public int PayloadTypeId { get; set; }
        public NetworkMessageType Action { get; set; }
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
        /// Attempts to populate properties from an incoming message.
        /// </summary>
        /// <param name="msg">The message to parse.</param>
        public void Decode(NetIncomingMessage msg)
        {
            SenderId = msg.SenderConnection.RemoteUniqueIdentifier;
            MessageSentTime = msg.ReadDouble();
            Action = (NetworkMessageType)msg.ReadByte();
            OwnerId = msg.ReadInt64();
            EntityId = msg.ReadInt64();
            PayloadTypeId = msg.ReadInt32();

            // Destroy messages have no payload type, only an EntityId
            if(Action != NetworkMessageType.Destroy) {
                try
                {
                    Type payloadType = NetworkManager.EntityStateTypes[PayloadTypeId];
                    Payload = Activator.CreateInstance(payloadType);
                    msg.ReadAllFields(Payload);
                    msg.ReadAllProperties(Payload);
                }
                catch(NetException ex)
                {
                    throw new FlatRedNetworkException("Error reading entity state from network message.", ex);
                }
                catch(SystemException ex)
                {
                    throw new FlatRedNetworkException("Error instantiating type.", ex);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="serverTime"></param>
        public void Encode(NetOutgoingMessage msg, double serverTime)
        {
            MessageSentTime = serverTime;

            msg.Write(serverTime);
            msg.Write((byte)Action);
            msg.Write(OwnerId);
            msg.Write(EntityId);
            msg.Write(PayloadTypeId);

            try
            {
                msg.WriteAllFields(Payload);
                msg.WriteAllProperties(Payload);
            }
            catch(NetException ex)
            {
                throw new FlatRedNetworkException("Error writing entity state to network message.", ex);
            }
            catch(SystemException ex)
            {
                throw new FlatRedNetworkException("Unknown message error.", ex);
            }
        }


        public T GetPayloadAsType<T>()
        {
            if(!(Payload is T))
            {
                string msg = string.Format("Could not cast {0} as {1}", Payload.GetType(), typeof(T));
                throw new FlatRedNetworkException(msg);
            }

            return (T)Payload;
        }
        
    }
}
