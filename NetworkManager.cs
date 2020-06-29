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
using RedGrin.Interfaces;

namespace RedGrin
{

    public class NetworkManager
    {
        public event NetworkEvent Connected;
        public event NetworkEvent Disconnected;
        public event NetworkEvent ClientConnected;

        Dictionary<byte, NetConnection> clientIdConnectionMap = new Dictionary<byte, NetConnection>();

        static NetworkManager self;

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
        /// The Lidgren NetPeer instance used to transmit and receive messages
        /// </summary>
        private NetPeer network;

        /// <summary>
        /// Master list of networked entities
        /// </summary>
        private List<INetworkEntity> entities = new List<INetworkEntity>();

        /// <summary>
        /// The logger instance that will be used to log messages.
        /// </summary>
        private ILogger log;

        /// <summary>
        /// A counter used to get new IDs for entities
        /// </summary>
        private uint entityIdCounter = 0;

        /// <summary>
        /// A counter used to get new IDs for clients:
        /// 0 - reserved for no ID set status
        /// 1 - reserved for server
        /// </summary>
        private byte clientIdCounter = 2;


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
                if (network == null)
                {
                    throw new RedGrinException("Attempted to get NetworkId before Network was initialized.");
                }
                return network.UniqueIdentifier;
            }
        }

        /// <summary>
        /// This is a short identifier that is used to uniquely identify
        /// clients on the network. Redgrin supports up to 255 clients,
        /// including the server itself. The zero value is reserved to indicate
        /// that the ID has not been properly set
        /// </summary>
        public byte ClientId { get; set; } = 0;

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
                    if (network != null && network.Connections != null && network.Connections.Count > 0)
                    {
                        netTime = network.Connections[0].GetRemoteTime(NetTime.Now);
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
        public INetworkArena GameArena { get; set; }

        public bool IsConnected
        {
            get
            {
                if(Role == NetworkRole.Server)
                {
                    return true;
                }
                else
                {
                    if(network.ConnectionsCount > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }



        private NetworkManager()
        {
            // intentionally empty, just marked private for singleton intent
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
            this.log = log ?? new NullLogger();
            EntityStateTypes = config.EntityStateTypes;
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

            entities = new List<INetworkEntity>();


            switch (Role)
            {
                case NetworkRole.Client:
                    network = new NetClient(config);
                    log.Info("Starting client.");
                    break;
                case NetworkRole.Server:
                    // server is always client 1
                    ClientId = 1;
                    config.Port = Configuration.ApplicationPort;
                    network = new NetServer(config);
                    log.Info("Starting server on port:" + Configuration.ApplicationPort);
                    break;
            }
            network.Start();
        }

        /// <summary>
        /// Connect to an endpoint
        /// </summary>
        /// <param name="ipaddress">The IP address to connect to</param>
        public void Connect(string address)
        {
            ServerAddress = address;
            log.Debug("Connecting to: " + ServerAddress + ":" + Configuration.ApplicationPort);
            if (Role == NetworkRole.Client)
            {
                if (ServerAddress == null)
                {
                    string errorMessage = "Bad server address.";
                    log.Error(errorMessage);
                    throw new RedGrinException(errorMessage);
                }

                try
                {
                    network.Connect(address, Configuration.ApplicationPort);
                }
                catch (Exception ex)
                {
                    throw new RedGrinException(ex.Message);
                }


                // TODO: set specific server connection variable here?
            }
            else
            {
                string errorMessage = "Cannot connect while running as Server.";
                log.Error(errorMessage);
                throw new RedGrinException(errorMessage);
            }
        }

        /// <summary>
        /// Closes connections
        /// </summary>
        public void Disconnect()
        {
            log.Debug("Disconnecting...");
            network.Shutdown("Disconnecting.");
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
            if (network == null || network.Status != NetPeerStatus.Running)
            {
                return;
            }

            NetIncomingMessage msg;
            while ((msg = network.ReadMessage()) != null)
            {
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.VerboseDebugMessage:
                    case NetIncomingMessageType.DebugMessage:
                        log.Debug(msg.ReadString());
                        break;
                    case NetIncomingMessageType.WarningMessage:
                        log.Warning(msg.ReadString());
                        break;
                    case NetIncomingMessageType.ErrorMessage:
                        log.Error(msg.ReadString());
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        ProcessStatusChangedMessage(msg);
                        break;
                    case NetIncomingMessageType.Data:
                        ProcessDataMessage(msg);
                        break;
                }
                network.Recycle(msg);
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

            for (int i = 0; i < entities.Count; i++)
            {
                // server sends reckoning message for everything but clients only send
                // reckoning for owned entities
                if (Role == NetworkRole.Server ||
                    (Role == NetworkRole.Client && entities[i].IsOwned()))
                {
                    SendDataMessage(entities[i], NetworkMessageType.Reckoning);
                    reckonCount++;
                }
            }

            log.Debug("Sent reckoning messages: " + reckonCount);
        }



        /// <summary>
        /// Broadcasts a message to all clients to create the provided entity.
        /// Note that action probably shouldn't happen
        /// for clients until the server bounces the message back
        /// </summary>
        /// <param name="initialState">The entity state.</param>
        public ulong RequestCreateEntity(object initialState)
        {
            var id = GetUniqueEntityId();
            if (Role == NetworkRole.Server)
            {
                CreateEntity(id, initialState, ServerTime);
            }
            else
            {
                SendDataMessage(id, initialState, NetworkMessageType.Create);
            }
            return id;
        }

        /// <summary>
        /// Broadcasts a message to destroy the provided entity.
        /// Note that action probably shouldn't happen
        /// for clients until the server bounces the message back
        /// </summary>
        /// <param name="entity">The entity to destroy.</param>
        public void RequestDestroyEntity(INetworkEntity entity)
        {
            if (Role == NetworkRole.Server)
            {
                DestroyEntity(entity.Id);
            }
            else
            {
                SendDataMessage(entity, NetworkMessageType.Destroy);
            }
        }

        /// <summary>
        /// Broadcasts a message to all clients to update the provided entity.
        /// </summary>
        /// <param name="entity">The entity to update.</param>
        public void RequestUpdateEntity(INetworkEntity entity)
        {
            if (Role == NetworkRole.Server)
            {
                UpdateEntity(entity.Id, entity.GetState(), true, ServerTime);
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
            var id = GetUniqueEntityId();
            if (Role == NetworkRole.Server)
            {
                
                ApplyGenericMessage(id, genericMessage, ServerTime);
            }
            else
            {
                SendDataMessage(id, genericMessage, NetworkMessageType.Generic);
            }
        }





        /// <summary>
        /// Handles an incoming data message
        /// </summary>
        /// <param name="message">The incoming message</param>
        private void ProcessDataMessage(NetIncomingMessage message)
        {
            NetworkMessage netMsg = new NetworkMessage(message);
            switch (netMsg.MessageType)
            {
                case NetworkMessageType.Generic:
                    ApplyGenericMessage(netMsg.Id, netMsg.Payload, netMsg.MessageSentTime);
                    break;
                case NetworkMessageType.Create:
                    CreateEntity(netMsg.Id, netMsg.Payload, netMsg.MessageSentTime);
                    break;
                case NetworkMessageType.Destroy:
                    DestroyEntity(netMsg.Id);
                    break;
                case NetworkMessageType.Update:
                case NetworkMessageType.Reckoning:
                    bool isReckoning = netMsg.MessageType == NetworkMessageType.Reckoning;
                    UpdateEntity(netMsg.Id, netMsg.Payload, isReckoning, netMsg.MessageSentTime);
                    break;
                default:
                    throw new RedGrinException("Message type not implemented: " + netMsg.MessageType.ToString());
            }
        }

        /// <summary>
        /// Called when a Create message has arrived, gets an entity from the game screen.
        /// </summary>
        /// <param name="ownerId">The NetworkId of the peer that controls the new entity.</param>
        /// <param name="entityId">The unique identifier for the entity.</param>
        /// <param name="payload">The object that will be used to apply the entity's starting state.</param>
        /// <param name="time">The time the message was sent, used for projecting the state to current time.</param>
        private void CreateEntity(ulong id, object payload, double time)
        {
            // check entity with ID already exists
            INetworkEntity targetEntity = entities.Where(e => e.Id == id).SingleOrDefault();

            if (targetEntity == null)
            {
                targetEntity = GameArena?.HandleCreateEntity(id, payload);

                // force correct unique ID
                targetEntity.Id = id;
            }
            targetEntity.UpdateFromState(payload, time, false);
            entities.Add(targetEntity);

            BroadcastIfServer(id, payload, NetworkMessageType.Create);
        }

        /// <summary>
        /// Called when a Destroy message has arrived, destroys an entity.
        /// </summary>
        /// <param name="entityId">The unique ID of the entity to be destroyed</param>
        private void DestroyEntity(ulong id)
        {
            INetworkEntity target = entities.Where(e => e.Id == id).SingleOrDefault();
            if (target != null)
            {
                log.Debug($"Destroying entity {id}");
                BroadcastIfServer(target.Id, null, NetworkMessageType.Destroy);
                entities.Remove(target);
                GameArena?.HandleDestroyEntity(target);
            }
            else
            {
                log.Debug($"Couldn't find entity marked for destruction: {id}");
            }
        }

        /// <summary>
        /// Called when an Update message has arrived, applies the new state to the entity.
        /// </summary>
        /// <param name="entityId">The unique identifier for the entity.</param>
        /// <param name="payload">The object that will be used to apply the entity's starting state.</param>
        /// <param name="isReckoning">True if this is a reckoning update.</param>
        /// <param name="time">The time the message was sent, used for projecting the state to current time.</param>
        private void UpdateEntity(ulong id, object payload, bool isReckoning, double time)
        {
            INetworkEntity targetEntity = entities.Where(e => e.Id == id).SingleOrDefault();

            if (targetEntity != null)
            {
                targetEntity.UpdateFromState(payload, time, isReckoning);
                BroadcastIfServer(id, payload, isReckoning ? NetworkMessageType.Reckoning : NetworkMessageType.Update);
            }
            else
            {
                // ignore if null, entity creation message may not have arrived yet
                log.Debug("Couldn't find entity to update: " + id);
            }
        }

        /// <summary>
        /// Called when a generic message has arrived. Notifies game arena of message
        /// </summary>
        /// <param name="ownerId">The original broadcaster of the message</param>
        /// <param name="payload">The message object</param>
        /// <param name="time">The time the message was sent, used for projecting the state to current time.</param>
        private void ApplyGenericMessage(ulong id, object payload, double time)
        {
            log?.Debug($"Received generic message from client {INetworkEntityExtensions.UnpackClientId(id)}");

            if (payload == null)
            {
                var msg = "Bad or missing payload for generic message.";
                log?.Error(msg);
                throw new RedGrinException(msg);
            }

            GameArena?.HandleGenericMessage(id, payload, time);

            BroadcastIfServer(id, payload, NetworkMessageType.Generic);
        }

        /// <summary>
        /// When the server receives a message from a client, it needs to notify other clients. This is called in
        /// all of the Create, Destroy and Update methods but only performs actual logic if running as server.
        /// </summary>
        /// <param name="entityId">The ID of the affected entity.</param>
        /// <param name="ownerId">The owner of the affected entity</param>
        /// <param name="payload">The payload from the original message.</param>
        /// <param name="action">Type type of message, determining the action to be taken.</param>
        private void BroadcastIfServer(ulong id, object payload, NetworkMessageType action)
        {
            if (Role == NetworkRole.Server)
            {
                SendDataMessage(id, payload, action);
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

                    // get ClientId provided by the server if we're a client
                    if (Role == NetworkRole.Client)
                    {
                        var serverConnection = network.GetConnection(message.SenderEndPoint);
                        var remoteHail = serverConnection.RemoteHailMessage;
                        if (remoteHail != null)
                        {
                            ClientId = remoteHail.ReadByte();
                        }

                        // add the server to our client Id map, its ID is always 1
                        clientIdConnectionMap.Add(1, serverConnection);
                        log.Info($"Connected to: {message.SenderEndPoint} with Client ID {ClientId}");
                        Connected?.Invoke(message.SenderConnection.RemoteUniqueIdentifier);
                    }
                    // send all game objects to new peer
                    if (Role == NetworkRole.Server)
                    {
                        var id = clientIdConnectionMap.Where(kvp => kvp.Value == message.SenderConnection).FirstOrDefault();
                        log.Info($"New client connected from: {message.SenderEndPoint} with id {id}");
                        SendCreateAllEntities(message.SenderConnection);
                        ClientConnected?.Invoke(message.SenderConnection.RemoteUniqueIdentifier);
                    }

                    RefreshConnectionCollection();
                    break;
                case NetConnectionStatus.Disconnected:
                    log.Info("Disconnected.");
                    // raise event
                    Disconnected?.Invoke(message.SenderConnection.RemoteUniqueIdentifier);
                    RefreshConnectionCollection();
                    if (Role == NetworkRole.Server)
                    {
                        DestroyUnownedEntities();
                    }
                    break;
                case NetConnectionStatus.RespondedAwaitingApproval:
                    SendApprovalMessage(message.SenderConnection);
                    break;
            }
        }

        /// <summary>
        /// Called when connect or disconnect messages are received, refreshes and
        /// sanity checks the mapping of ClientId to NetConnection
        /// </summary>
        private void RefreshConnectionCollection()
        {
            // first remove any disconnected clients
            var keys = clientIdConnectionMap.Keys.ToList();
            for (var i = keys.Count - 1; i > -1; i--)
            {
                var k = keys[i];
                if (!network.Connections.Contains(clientIdConnectionMap[k]))
                {
                    clientIdConnectionMap.Remove(k);
                }
            }

            // sanity check: the network shouldn't have connections with no ClientId entry
            for (var i = 0; i < network.Connections.Count; i++)
            {
                if (!clientIdConnectionMap.ContainsValue(network.Connections[i]))
                {
                    // TODO: force disconnect of unknown connection?
                    throw new RedGrinException("Lidgren network contains a connection with no ClientId. This shouldn't happen!");
                }
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
            // TODO: roll these up into a special message containing all data?
            for (var i = 0; i < entities.Count; i++)
            {
                SendDataMessage(entities[i], NetworkMessageType.Create, recipient: recipient);
            }
        }

        /// <summary>
        /// Sends a Destroy message for all entities that no longer have an owner
        /// </summary>
        private void DestroyUnownedEntities()
        {
            for (int i = entities.Count - 1; i > -1; i--)
            {
                INetworkEntity entity = entities[i];
                var id = entity.GetClientId();

                // if we don't own the entity and its ClientId isn't in the map
                // the client must have disconnected and the entity should be destroyed
                if (id != ClientId && !clientIdConnectionMap.ContainsKey(entity.GetClientId()))
                {
                    SendDataMessage(entity, NetworkMessageType.Destroy);
                    if (Role == NetworkRole.Server)
                    {
                        DestroyEntity(entity.Id);
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
            object payload = entity.GetState();
            SendDataMessage(entity.Id, payload, action, recipient);
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
        private void SendDataMessage(ulong id, object payload, NetworkMessageType action, NetConnection recipient = null)
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
            message.Id = id;
            message.PayloadTypeId = payloadTypeId;
            message.MessageType = action;
            message.Payload = payload;

            NetOutgoingMessage outgoingMessage = network.CreateMessage();
            message.Encode(outgoingMessage, ServerTime);
            switch (Role)
            {
                case NetworkRole.Server:
                    if (recipient == null)
                    {
                        ((NetServer)network).SendToAll(outgoingMessage, method);
                    }
                    else
                    {
                        ((NetServer)network).SendMessage(outgoingMessage, recipient, method);
                    }
                    break;
                case NetworkRole.Client:
                    var server = ((NetClient)network).ServerConnection;
                    network.SendMessage(outgoingMessage, server, method);
                    break;
                default:
                    throw new RedGrinException("Attempted to send message as an unsupported role: " + Role.ToString());
            }
        }

        private void SendApprovalMessage(NetConnection connection)
        {
            if (clientIdCounter >= 255)
            {
                throw new RedGrinException("Too many clients have connected. Connection count maxed out!");
            }
            var id = clientIdCounter++;
            NetOutgoingMessage hailMessage = network.CreateMessage();
            hailMessage.Write(id);
            connection.Approve(hailMessage);

            // add the connection to the dictionary
            clientIdConnectionMap.Add(id, connection);
        }

        /// <summary>
        /// Gets a unique ID for assignment to an entity.
        /// </summary>
        /// <returns>Long integer</returns>
        private ulong GetUniqueEntityId()
        {
            return INetworkEntityExtensions.PackUniqueId(ClientId, entityIdCounter++);
        }

        public bool IsOwnedId(ulong id)
        {
            return INetworkEntityExtensions.UnpackClientId(id) == ClientId;
        }
    }
}




