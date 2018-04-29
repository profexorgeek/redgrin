# README #

RedGrin is a multiplayer networking library that wraps Lidgren. It implements a Server/Client pattern where the server 
is the master record of game state, common in games like Unreal Tournament.

### Primary concepts: ###

* The NetworkManager handles all communication between clients. It must be instantiated and initialized with a configuration file to run properly.
* The main game screen should implement INetworkArena. The implementation of the interface should handle adding and removing objects from the game. 
* Requests to add, remove and update objects should be made to the Network manager and it will properly route them based on NetworkRole
* All entities that share state across the network should implement INetworkEntity. This handles getting and setting the network state of networked entities.
* All connected games get a unique ID that makes it easy to determine what entities are owned by which clients
* All entities have a unique ID that makes it easy to keep the correct entities in sync across the network
* Lightweight transfer objects can be registered in the config and automatically transferred across the network.

For a basic implementation see:
https://bitbucket.org/profexorgeek/redgrindemo


### How do I get set up? ###

This has no major requirements outside of Lidgren. Clone and build! An example implementation that uses the FlatRedBall game engine can be found here:
https://bitbucket.org/profexorgeek/redgrindemo

### Who do I talk to? ###

https://twitter.com/profexorgeek