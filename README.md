# nerve-tcp-assignment
Encrypted TCP Client/Server (C# / .NET)

A TCP client and server written in C# for the Nerve socket programming assignment.

Part A: The client sends a string such as SetA-Two. The server looks it up in a collection and replies with the current time, repeated n times at 1-second intervals (where n is the value found). If the set or key is not found, the server replies EMPTY.
Part B: every message in both directions is encrypted and decrypted.
The server handles multiple clients at the same time.
All inputs are configurable.
Requirements
.NET SDK 10 (or .NET 8). If you use .NET 8, change net10.0 to net8.0 in NerveTcp.csproj.
No extra packages. Encryption uses the built-in System.Security.Cryptography.
Project structure
File	Purpose
Program.cs	Entry point. Chooses server or client mode and applies command-line overrides.
AppConfig.cs	Loads settings from config.json.
SecureChannel.cs	Key derivation, AES-GCM encryption/decryption, and message framing.
TimeServer.cs	Accepts connections, looks up requests, sends timestamps.
TimeClient.cs	Connects, sends the request, prints each reply.
config.json	Host, port, passphrase, interval, and the lookup collection.
How to run

Open two terminals in the project folder.

Terminal 1 (server):

dotnet run -- server

Terminal 2 (client):

dotnet run -- client SetA-Two

Running the client with no argument starts an interactive prompt where you can type several requests. Press Enter on a blank line to quit:

dotnet run -- client
Example

Request SetA-Two (value 2) gives two messages, one second apart:

16-11-2026 21:53:13
16-11-2026 21:53:14
Configuration

Edit config.json:

Setting	Meaning
host, port	Address the server listens on and the client connects to
passphrase	Shared secret used to derive the encryption key (must match on both sides)
salt	Salt for key derivation (must match on both sides)
interval	Seconds between messages
collection	The sets and values the server looks up

Command-line options override the file:

--config <path>   --host <ip>   --port <number>   --passphrase <text>   --interval <seconds>

Example: dotnet run -- server --port 6000 --interval 2

How it works
Part A: lookup logic
The server splits the request at the first -: SetA-Two, which becomes set SetA and key Two.
It checks that SetA exists in the collection and retrieves its subset [{"One":1, "Two":2}].
It looks for Two in the subset and gets the value 2.
It sends the current time (dd-MM-yyyy HH:mm:ss) 2 times, waiting an interval of seconds between messages.
If the set or key is missing, it sends EMPTY.
The server then closes the connection, which tells the client the messages have ended.
Part B: encryption
A 256-bit key is derived from the shared passphrase with PBKDF2 (SHA-256, 200,000 iterations), so the key itself is never sent over the network.
Each message is encrypted with AES-256-GCM using a fresh random nonce. GCM also detects tampering, so a wrong key or modified data fails to decrypt instead of producing garbage.
Both the client and the server encrypt what they send and decrypt what they receive.
Message framing

TCP is a byte stream and does not keep message boundaries. Each message is sent as:

[4-byte length][12-byte nonce][16-byte tag][ciphertext]

The receiver reads the length first, then reads exactly that many bytes.

Multiple clients

The server accepts connections in a loop and handles each client in its own async task, so a client that is waiting between messages never blocks another client.

Test cases
Start the server first (dotnet run -- server).

#	Command	Expected result
1	client SetA-Two	2 timestamps, 1 second apart
2	client SetA-One	1 timestamp
3	client SetC-Six	6 timestamps
4 client SetA-Nope	EMPTY
5	client SetZ-One	EMPTY
6	client hello	EMPTY 
7	client SetA-Two --passphrase wrong	Client prints nothing; server logs a decryption failure

Screenshots of these runs are in the screenshots folder.

Notes:
The passphrase in config.json is a demo value. In a real deployment, it would be supplied through an environment variable or a secrets store, not committed to the repository.
Each connection handles one request. The interactive client opens a new connection for each string you type.
