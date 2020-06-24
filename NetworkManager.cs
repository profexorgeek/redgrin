using RedGrin.Logging;
using RedGrin.Messaging;
using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace RedGrin
{

    public class NetworkManager
    {
        #region Events

        public event NetworkEvent Connected;
        public event NetworkEvent Disconnected;

        #endregion

        public ObservableCollection<NetConnection> Connections
        { get; private set; } = new ObservableCollection<NetConnection>();

        static NetworkManager self;
        public static NetworkManager Self
        {
            get
            {
                if (self == null)
                {
                    self = new NetworkManager();
                }
                return self;
            }
        }

        /// <summary>
        /// The seconds the last update happened.
        /// </summary>
        private double lastUpdateTime;

        /// <summary>
        /// The elapsed time since the last Update cycle
        /// </summary>
        private double lastUpdateDelta;

        /// <summary>
        /// Seconds remaining until the next dead reckoning
        /// </summary>
        private double timeToDeadReckon;

        /// <summary>
        /// The role of this instance on the network
        /// Used to dictate client vs server behavior
        /// </summary>
        public NetworkRole Role { get; private set; } = NetworkRole.None;

        /// <summary>
        /// The IPEndpoint for the Server
        /// </summary>
        public string ServerAddress { get; private set; }

        /// <summary>
        /// The unique identifier for this instance on the network.
        /// </summary>
        public long NetworkId
        {
            get
            {
                if (mNetwork == null)
                {
                    throw new RedGrinException("Attempted to get NetworkId before Network was initialized.");
                }
                return mNetwork.UniqueIdentifier;
            }
        }

        /// <summary>
        /// The configuration, including application name and port,
        /// used to set up the network
        /// </summary>
        public NetworkConfiguration Configuration { get; private set; }

        /// <summary>
        /// A numerical list of state types used to transfer type as a very small package.
        /// Defined in the constructor from the network configuration
        /// </summary>
        internal static List<Type> EntityStateTypes { get; private set; }

        /// <summary>
        /// The current time for the server.
        /// Useful as a consistent timeline for projecting physics
        /// based on latency.
        /// </summary>
        public double ServerTime
        {
            get
            {
                double netTime;
                if (Role == NetworkRole.Server)
                {
                    netTime = NetTime.Now;
                }
                else
                {
                    if (mNetwork != null && mNetwork.Connections != null && mNetwork.Connections.Count > 0)
                    {
                        netTime = mNetwork.Connections[0].GetRemoteTime(NetTime.Now);
                    }
                    else
                    {
                        netTime = -1;
                    }
                }
                return netTime;
            }
        }

        /// <summary>
        /// The game arena that controls all client game objects
        /// </summary>
        public INetworkArena GameArena
        {
            get
            {
                return mGameArena;
            }
            set
            {
                mGameArena = value;
            }
        }



        /// <summary>
        /// The Lidgren NetPeer instance used to transmit and receive messages
        /// </summary>
        private NetPeer mNetwork;

        /// <summary>
        /// Master list of networked entities
        /// </summary>
        private List<INetworkEntity> mEntities = new List<INetworkEntity>();

        /// <summary>
        /// The logger instance that will be used to log messages.
        /// </summary>
        private ILogger mLog;

        /// <summary>
        /// A counter used to get new IDs for entities
        /// </summary>
        private long entityId;

        /// <summary>
        /// A reference to the game arena
        /// </summary>
        private INetworkArena mGameArena;


        NetworkManager()
        {

        }

        /// <summary>
        /// Instantiate the Network.
        /// WARNING: If no logger is provided, all messages will be swallowed.
        /// </summary>
        /// <param name="config">Configuration for networking</param>
        /// <param name="arena">The game arena</param>
        /// <param name="log">An ILogger to write messages to</param>
        public void Initialize(NetworkConfiguration config, ILogger log = null)
        {
            Configuration = config;
            // if no logger was provided, use NullLogger
            mLog = log ?? new NullLogger();
            EntityStateTypes = config.EntityStateTypes;
        }

        /// <summary>
        /// Reads any messages in the queue and updates Entities accordingly.
        /// Usually called in the game loop.
        /// Read messages are recycled at each iteration.
        /// </summary>
        public void Update()
        {
            lastUpdateDelta = ServerTime - lastUpdateTime;
            lastUpdateTime = ServerTime;

            // early out, don't update if network not running
            // allows generic update call in main gameloop before
            // network has initialized
            if (mNetwork == null || mNetwork.Status != NetPeerStatus.Running)
            {
                return;
            }

            NetIncomingMessage msg;
            while ((msg = mNetwork.ReadMessage()) != null)
            {
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.VerboseDebugMessage:
                    case NetIncomingMessageType.DebugMessage:
                        mLog.Debug(msg.ReadString());
                        break;
                    case NetIncomingMessageType.WarningMessage:
                        mLog.Warning(msg.ReadString());
                        break;
                    case NetIncomingMessageType.ErrorMessage:
                        mLog.Error(msg.ReadString());
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        ProcessStatusChangedMessage(msg);
                        break;
                    case NetIncomingMessageType.Data:
                        ProcessDataMessage(msg);
                        break;
                }
                mNetwork.Recycle(msg);
            }

            if (Configuration.DeadReckonSeconds > 0)
            {
                timeToDeadReckon -= lastUpdateDelta;
                if (timeToDeadReckon <= 0)
                {
                    DeadReckon();
                    timeToDeadReckon = Configuration.DeadReckonSeconds;
                }
            }


            foreach (var internalConnection in mNetwork.Connections)
            {
                if (this.Connections.Contains(internalConnection) == false)
                {
                    this.Connections.Add(internalConnection);
                }
            }
            for (int i = Connections.Count - 1; i > -1; i--)
            {
                if (mNetwork.Connections.Contains(Connections[i]) == false)
                {
                    Connections.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// The dead reckoning cycle.
        /// Broadcasts the state of all entities and forces updates.
        /// Clients should always accept overrides from the server 
        /// during dead reckoning to keep the game in sync.
        /// </summary>
        public void DeadReckon()
        {
            int reckonCount = 0;

            for (int i = 0; i < mEntities.Count; i++)
            {
                // server sends reckoning message for everything but clients only send
                // reckoning for owned entities
                if (Role == NetworkRole.Server ||
                    (Role == NetworkRole.Client && mEntities[i].OwnerId == NetworkId))
                {
                    SendDataMessage(mEntities[i], NetworkMessageType.Reckoning);
                    reckonCount++;
                }
            }

            mLog.Debug("Sent reckoning messages: " + reckonCount);
        }

        /// <summary>
        /// Broadcasts a message to all clients to create the provided entity.
        /// Note that this is broadcast only. No instantiation should happen
        /// at this point.
        /// </summary>
        /// <param name="entity">The entity to create.</param>
        public void RequestCreateEntity(object initialState)
        {
            if (Role == NetworkRole.Server)
            {
                CreateEntity(NetworkId, GetUniqueEntityId(), initialState, ServerTime);
            }
            else
            {
                SendDataMessage(-1, NetworkId, initialState, NetworkMessageType.Create);
            }
        }

        /// <summary>
        /// Broadcasts a message to all clients to destroy the provided entity.
        /// Note that this is broadcast only. No destruction should happen until
        /// a message is received.
        /// </summary>
        /// <param name="entity">The entity to destroy.</param>
        public void RequestDestroyEntity(INetworkEntity entity)
        {
            if (Role == NetworkRole.Server)
            {
                DestroyEntity(entity.EntityId);
            }
            else
            {
                SendDataMessage(entity, NetworkMessageType.Destroy);
            }
        }

        /// <summary>
        /// Broadcasts a message to all clients to update the provided entity.
        /// Note that this is broadcast only. No updating should happen until
        /// a message is received.
        /// </summary>
        /// <param name="entity">The entity to update.</param>
        public void RequestUpdateEntity(INetworkEntity entity)
        {
            if (Role == NetworkRole.Server)
            {
                UpdateEntity(entity.EntityId, entity.OwnerId, entity.GetState(), true, ServerTime);
            }
            else
            {
                SendDataMessage(entity, NetworkMessageType.Update);
            }
        }

        /// <summary>
        /// If instance is server, applies the generic message and then broadcasts it to
        /// clients. If instance is a client, sends the data message to the server.
        /// </summary>
        /// <param name="genericMessage">A message in the form of a registered state object not tied to a specific entity.</param>
        public void RequestGenericMessage(object genericMessage)
        {
            if (Role == NetworkRole.Server)
            {
                ApplyGenericMessage(NetworkId, genericMessage, ServerTime);
            }
            else
            {
                // NOTE: -1 as a magic constant isn't great but this enables us to send
                // non-entity messages without a major change in the current pattern
                SendDataMessage(-1, NetworkId, genericMessage, NetworkMessageType.Generic);
            }
        }

        /// <summary>
        /// Initializes the network according to the provided role
        /// </summary>
        /// <param name="role">The role to use</param>
        public void Start(NetworkRole role)
        {
            Role = role;

            var config = new NetPeerConfiguration(Configuration.ApplicationName);
            config.EnableMessageType(NetIncomingMessageType.WarningMessage);
            config.EnableMessageType(NetIncomingMessageType.VerboseDebugMessage);
            config.EnableMessageType(NetIncomingMessageType.DebugMessage);
            config.EnableMessageType(NetIncomingMessageType.ErrorMessage);
            config.EnableMessageType(NetIncomingMessageType.Error);
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);

#if DEBUG
            // Note: these only exist on Lidgren in DEBUG mode
            // Allows simulation of slow/problematic networks
            config.SimulatedLoss = Configuration.SimulatedLoss;
            config.SimulatedMinimumLatency = Configuration.SimulatedMinimumLatencySeconds;
            config.SimulatedRandomLatency = Configuration.SimulatedRandomLatencySeconds;
            config.SimulatedDuplicatesChance = Configuration.SimulatedDuplicateChance;
#endif

            mEntities = new List<INetworkEntity>();


            switch (Role)
            {
                case NetworkRole.Client:
                    mNetwork = new NetClient(config);
                    mLog.Info("Starting client.");
                    break;
                case NetworkRole.Server:
                    config.Port = Configuration.ApplicationPort;
                    mNetwork = new NetServer(config);
                    mLog.Info("Starting server on port:" + Configuration.ApplicationPort);
                    break;
            }
            mNetwork.Start();
        }

        /// <summary>
        /// Connect to an endpoint
        /// </summary>
        /// <param name="ipaddress">The IP address to connect to</param>
        public void Connect(string address)
        {
            ServerAddress = address;
            mLog.Debug("Connecting to: " + ServerAddress + ":" + Configuration.ApplicationPort);
            if (Role == NetworkRole.Client)
            {
                if (ServerAddress == null)
                {
                    string errorMessage = "Bad server address.";
                    mLog.Error(errorMessage);
                    throw new RedGrinException(errorMessage);
                }
                mNetwork.Connect(address, Configuration.ApplicationPort);
                // TODO: set specific server connection variable here?
            }
            else
            {
                string errorMessage = "Cannot connect while running as Server.";
                mLog.Error(errorMessage);
                throw new RedGrinException(errorMessage);
            }
        }

        /// <summary>
        /// Closes connections
        /// </summary>
        public void Disconnect()
        {
            mLog.Debug("Disconnecting...");
            mNetwork.Shutdown("Disconnecting.");
        }


        /// <summary>
        /// Handles an incoming data message
        /// </summary>
        /// <param name="message">The incoming message</param>
        private void ProcessDataMessage(NetIncomingMessage message)
        {
            NetworkMessage netMsg = new NetworkMessage(message);
            switch (netMsg.Action)
            {
                case NetworkMessageType.Generic:
                    ApplyGenericMessage(netMsg.OwnerId, netMsg.Payload, netMsg.MessageSentTime);
                    break;
                case NetworkMessageType.Create:
                    CreateEntity(netMsg.OwnerId, netMsg.EntityId, netMsg.Payload, netMsg.MessageSentTime);
                    break;
                case NetworkMessageType.Destroy:
                    DestroyEntity(netMsg.EntityId);
                    break;
                case NetworkMessageType.Update:
                case NetworkMessageType.Reckoning:
                    bool isReckoning = netMsg.Action == NetworkMessageType.Reckoning;
                    UpdateEntity(netMsg.EntityId, netMsg.OwnerId, netMsg.Payload, isReckoning, netMsg.MessageSentTime);
                    break;
                default:
                    throw new RedGrinException("Message type not implemented: " + netMsg.Action.ToString());
            }
        }

        /// <summary>
        /// Called when a Create message has arrived, gets an entity from the game screen.
        /// </summary>
        /// <param name="ownerId">The NetworkId of the peer that controls the new entity.</param>
        /// <param name="entityId">The unique identifier for the entity.</param>
        /// <param name="payload">The object that will be used to apply the entity's starting state.</param>
        /// <param name="time">The time the message was sent, used for projecting the state to current time.</param>
        private void CreateEntity(long ownerId, long entityId, object payload, double time)
        {
            // this is a brand new entity, get a new ID
            if (entityId == -1)
            {
                if (Role == NetworkRole.Server)
                {
                    entityId = GetUniqueEntityId();
                }
                else
                {
                    var msg = "Something went wrong, client received bad entity ID.";
                    mLog.Error(msg);
                    throw new RedGrinException(msg);
                }
            }

            // check entity with ID already exists
            INetworkEntity targetEntity = mEntities.Where(e => e.EntityId == entityId).SingleOrDefault();

            if (targetEntity == null)
            {
                targetEntity = GameArena?.RequestCreateEntity(ownerId, payload);
            }
            else
            {
                var msg = "Attempted to create entity for ID that already exists: " + entityId;
                mLog?.Error(msg);
                throw new RedGrinException(msg);
            }

            targetEntity.UpdateFromState(payload, time);
            targetEntity.OwnerId = ownerId;
            targetEntity.EntityId = entityId;
            mEntities.Add(targetEntity);

            BroadcastIfServer(entityId, ownerId, payload, NetworkMessageType.Create);
        }

        public void AddToEntities(INetworkEntity entityToAdd)
        {
#if DEBUG
            if (mEntities.Contains(entityToAdd))
            {
                throw new InvalidOperationException("This entity is already part of the list, it can't be added again.");
            }
#endif
            entityToAdd.EntityId = GetUniqueEntityId();
            mEntities.Add(entityToAdd);
        }

        /// <summary>
        /// Called when a Destroy message has arrived, destroys an entity.
        /// </summary>
        /// <param name="entityId">The unique ID of the entity to be destroyed</param>
        private void DestroyEntity(long entityId)
        {
            INetworkEntity target = mEntities.Where(e => e.EntityId == entityId).SingleOrDefault();
            if (target != null)
            {
                BroadcastIfServer(target.EntityId, target.OwnerId, null, NetworkMessageType.Destroy);
                mEntities.Remove(target);
                GameArena?.RequestDestroyEntity(target);
            }
            else
            {
                mLog.Debug("Couldn't find entity marked for destruction: " + entityId);
            }
        }

        /// <summary>
        /// Called when an Update message has arrived, applies the new state to the entity.
        /// </summary>
        /// <param name="entityId">The unique identifier for the entity.</param>
        /// <param name="payload">The object that will be used to apply the entity's starting state.</param>
        /// <param name="isReckoning">True if this is a reckoning update.</param>
        /// <param name="time">The time the message was sent, used for projecting the state to current time.</param>
        private void UpdateEntity(long entityId, long ownerId, object payload, bool isReckoning, double time)
        {
            INetworkEntity targetEntity = mEntities.Where(e => e.EntityId == entityId && e.OwnerId == ownerId).SingleOrDefault();

            mLog?.Debug($"Receiving update from {ownerId} to update entity {payload?.GetType()}");

            if (targetEntity != null)
            {
                if (targetEntity.OwnerId != this.NetworkId || isReckoning)
                {
                    targetEntity.UpdateFromState(payload, time);
                }

                BroadcastIfServer(entityId, targetEntity.OwnerId, payload,
                    isReckoning ?
                    NetworkMessageType.Reckoning :
                    NetworkMessageType.Update
                    );
            }
            else
            {
                // ignore if null, entity creation message may not have arrived yet
                mLog.Debug("Couldn't find entity to update: " + entityId);
            }
        }

        /// <summary>
        /// Called when a generic message has arrived. Notifies game arena of message
        /// </summary>
        /// <param name="ownerId">The original broadcaster of the message</param>
        /// <param name="payload">The message object</param>
        /// <param name="time">The time the message was sent, used for projecting the state to current time.</param>
        private void ApplyGenericMessage(long ownerId, object payload, double time)
        {
            mLog?.Info($"Received generic message from {ownerId}");

            if (payload == null)
            {
                var msg = "Bad or missing payload for generic message.";
                mLog?.Error(msg);
                throw new RedGrinException(msg);
            }

            GameArena?.HandleGenericMessage(payload, time);

            // NOTE: -1 as a magic constant isn't great but this enables us to send
            // non-entity messages without a major change in the current pattern
            BroadcastIfServer(-1, ownerId, payload, NetworkMessageType.Generic);
        }

        /// <summary>
        /// When the server receives a message from a client, it needs to notify other clients. This is called in
        /// all of the Create, Destroy and Update methods but only performs actual logic if running as server.
        /// </summary>
        /// <param name="entityId">The ID of the affected entity.</param>
        /// <param name="ownerId">The owner of the affected entity</param>
        /// <param name="payload">The payload from the original message.</param>
        /// <param name="action">Type type of message, determining the action to be taken.</param>
        private void BroadcastIfServer(long entityId, long ownerId, object payload, NetworkMessageType action)
        {
            if (Role == NetworkRole.Server)
            {
                SendDataMessage(entityId, ownerId, payload, action);
            }
        }

        /// <summary>
        /// Called by the Update method when status change messages are received.
        /// Connection, disconnection and approval requests, for example.
        /// </summary>
        /// <param name="message">The incoming message.</param>
        private void ProcessStatusChangedMessage(NetIncomingMessage message)
        {
            NetConnectionStatus newStatus = (NetConnectionStatus)message.ReadByte();

            switch (newStatus)
            {
                case NetConnectionStatus.Connected:
                    mLog.Info("Connected to: " + message.SenderEndPoint);

                    Connected?.Invoke(message.SenderConnection.RemoteUniqueIdentifier);

                    // send all game objects to new peer
                    if (Role == NetworkRole.Server)
                    {
                        SendCreateAllEntities(message.SenderConnection);
                    }
                    break;


                case NetConnectionStatus.Disconnected:
                    mLog.Info("Disconnected.");

                    // raise event
                    Disconnected?.Invoke(message.SenderConnection.RemoteUniqueIdentifier);

                    // destroy all game objects owned by disconnected peer
                    if (Role == NetworkRole.Server)
                    {
                        DestroyAllOwnedById(message.SenderConnection.RemoteUniqueIdentifier);
                    }

                    break;


                case NetConnectionStatus.RespondedAwaitingApproval:
                    // TODO: set max connections and deny if full
                    message.SenderConnection.Approve();
                    break;
            }

        }

        /// <summary>
        /// Sends a Create message for all entities in the local collection.
        /// This should generally only be called in Server mode when a new
        /// client connects
        /// </summary>
        /// <param name="recipient">An individual receipient.
        /// If not supplied, the message will be sent to all connections.</param>
        private void SendCreateAllEntities(NetConnection recipient = null)
        {
            foreach (INetworkEntity entity in mEntities)
            {
                SendDataMessage(entity, NetworkMessageType.Create, recipient: recipient);
            }
        }

        /// <summary>
        /// Sends a Destroy message for all entities owned by a specific NetworkId.
        /// Usually called in Server mode when a client disconnects to update other clients
        /// </summary>
        /// <param name="ownerId">The OwnerId of entities to destroy.</param>
        private void DestroyAllOwnedById(long ownerId)
        {
            for (int i = mEntities.Count - 1; i > -1; i--)
            {
                INetworkEntity entity = mEntities[i];

                if (entity.OwnerId == ownerId)
                {
                    SendDataMessage(entity, NetworkMessageType.Destroy);
                    if (Role == NetworkRole.Server)
                    {
                        DestroyEntity(entity.EntityId);
                    }
                }
            }
        }

        /// <summary>
        /// Uses the provided entity to compose a data message.
        /// ReliableSequenced method is suggested to balance performance with deliverability.
        /// </summary>
        /// <param name="entity">The entity to build a message from.</param>
        /// <param name="action">The type of message to send.</param>
        /// <param name="method">Delivery method.</param>
        /// <param name="recipient">The recipient connection. Will send to all if null.</param>
        private void SendDataMessage(INetworkEntity entity, NetworkMessageType action, NetConnection recipient = null)
        {
            // clients can't force a message for an entity they don't own
            if (Role != NetworkRole.Server && entity.OwnerId != NetworkId)
            {
                throw new RedGrinException("Cannot send an update for an entity that is not owned by this client!");
            }

            object payload = entity.GetState();
            SendDataMessage(entity.EntityId, entity.OwnerId, payload, action, recipient);
        }

        // from https://stackoverflow.com/questions/6803073/get-local-ip-address
        public string GetLocalIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return null;
        }

        /// <summary>
        /// Composes and sends a data message.
        /// </summary>
        /// <param name="entityId">Unique ID of the affected entity.</param>
        /// <param name="ownerId">The entity owner's NetworkId</param>
        /// <param name="payload">The state describing changes to the entity.</param>
        /// <param name="action">The type of message that determines the ultimate action taken.</param>
        /// <param name="method">The delivery method.</param>
        /// <param name="recipient">The recipient connection. Will send to all if null.</param>
        private void SendDataMessage(long entityId, long ownerId, object payload, NetworkMessageType action, NetConnection recipient = null)
        {
            int payloadTypeId = -1;
            Type type;
            NetDeliveryMethod method;

            switch (action)
            {
                // critical messages should be guaranteed delivery and in sequence
                case NetworkMessageType.Create:
                case NetworkMessageType.Destroy:
                case NetworkMessageType.Reckoning:
                    method = NetDeliveryMethod.ReliableOrdered;
                    break;
                // general updates can fail, reckoning cycle will correct missed messages
                case NetworkMessageType.Update:
                    method = NetDeliveryMethod.UnreliableSequenced;
                    break;
                default:
                    method = NetDeliveryMethod.UnreliableSequenced;
                    break;
            }

            if (payload != null)
            {
                try
                {
                    // get the type and find it's ID in the config enumeration
                    type = payload.GetType();
                    payloadTypeId = Configuration.EntityStateTypes.IndexOf(type);
                }
                catch (Exception ex)
                {
                    throw new RedGrinException("Failed to get entity state.", ex);
                }

                // TODO: Not a fan of the negative type ID meaning "missing" - better solution here?
                if (payloadTypeId == -1)
                {
                    throw new RedGrinException("Failed to find ID for type: " + type.ToString());
                }
            }

            NetworkMessage message = new NetworkMessage();
            message.SenderId = NetworkId;
            message.OwnerId = ownerId;
            message.EntityId = entityId;
            message.PayloadTypeId = payloadTypeId;
            message.Action = action;
            message.Payload = payload;

            NetOutgoingMessage outgoingMessage = mNetwork.CreateMessage();
            message.Encode(outgoingMessage, ServerTime);
            switch (Role)
            {
                case NetworkRole.Server:
                    if (recipient == null)
                    {
                        ((NetServer)mNetwork).SendToAll(outgoingMessage, method);
                    }
                    else
                    {
                        ((NetServer)mNetwork).SendMessage(outgoingMessage, recipient, method);
                    }
                    break;
                case NetworkRole.Client:
                    var server = ((NetClient)mNetwork).ServerConnection;
                    mNetwork.SendMessage(outgoingMessage, server, method);
                    break;
                default:
                    throw new RedGrinException("Attempted to send message as an unsupported role: " + Role.ToString());
            }
        }

        /// <summary>
        /// Gets a unique ID for assignment to an entity.
        /// </summary>
        /// <returns>Long integer</returns>
        private long GetUniqueEntityId()
        {
            // TODO: handle max long ID?
            long id = entityId;
            entityId++;
            return id;
        }
    }
}
