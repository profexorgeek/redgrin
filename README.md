# README #

RedGrin is an opinionated wrapper for Lidgren that attempts to simplify multiplayer networking for small indie games. Redgrin was written specifically for the [FlatRedBall](http://flatredball.com) game engine but should be compatible with any C# game engine including MonoGame and Unity.

## What does Redgrin do that Lidgren doesn't?

Redgrin defines an architecture and communication pattern that abstracts away many of the complex details of networking. Redgrin takes care of:

* Choosing the right message type
* Serializing and deserializing transfer messages
* Giving entities unique identifiers across all clients
* Keeping track of an instance role (Client or Server)
* Allowing role-based logic
* Performing dead reckoning
* Client-server architecture

Redgrin does _not_ do partitioning, input prediction, physics interpolation, cheating detection or other logic that tends to be game-specific. The interfaces provide methods and arguments allowing developers to determine how their game should handle entity ownership, latency, and other multiplayer details.

## How does Redgrin Work?

At a high level, Redgrin works like this:

1. Define transfer classes for each entity that describes its state so it can be synced across the network. Defining transfer types up front allows automatic serialization of transfer objects into network messages.
1. Create a `NetworkConfiguration` instance that defines app name, port numbers, and lists the transfer class types.
1. Each entity that needs to sync across the network should implement the `INetworkEntity` interface.
1. Your game level/screen/arena should implement the `INetworkArena` interface.
1. Call `NetworkManager.Self.Update()` in your game loop to run the networking processes.
1. Call `NetworkManager.Self.Initialize` with your `NetworkConfiguration` object and an optional logger to initialize the network.
1. Call `NetworkManager.Self.Start` to start the network in a role (Client or Server).
1. Call `Connect` with an IP address to connect to a server.
1. The `NetworkManager` has methods to create, delete and update entities. It will automatically call methods on `INetworkEntity` objects and the `INetworkArena` to manage the synchronization of objects across the network.

## How do I learn more?

These whitepapers/articles have been helpful for me:

* [Client-server game architecture](https://gabrielgambetta.com/client-server-game-architecture.html)
* [What every programmer needs to know about game networking](https://gafferongames.com/post/what_every_programmer_needs_to_know_about_game_networking/)
* [Latency compensation methods](https://developer.valvesoftware.com/wiki/Latency_Compensating_Methods_in_Client/Server_In-game_Protocol_Design_and_Optimization)

### Who do I talk to? ###

https://twitter.com/profexorgeek
