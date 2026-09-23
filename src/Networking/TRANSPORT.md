# MVZ-MP Steam session transport

The mod uses the game's Steamworks initialization and callback loop.
SteamNetworkingMessages is the primary peer data path on channel 27182. Each
application message is split into 64 KiB chunks, sent reliably, and reassembled
by sender and sequence. The sender must be a member of the current Steam lobby,
and a session request from a nonmember is refused. The lobby metadata declares
the selected transport so peers use the same path.

If SteamNetworkingMessages is unavailable before hosting, the host selects a
lobby chat fallback with 3,800-byte chunks. Native-capable guests can join a
chat-fallback lobby. The fallback sends each chunk to every lobby member and is
slower for snapshots and audio. A failure after joining ends the session rather
than switching transports under an active game state.

Limits: 4 MiB per application message, 8 MiB queued output, 8 MiB incomplete
or blocked incoming messages, 128 pending incoming messages, and four chunks
submitted per game update on lobby chat, or one native chunk per update.
Incomplete messages and missing sequence gaps
expire after 30 seconds. A failed send or native session failure ends the lobby
session so peers do not continue with silently missing zoo state.

The `reliable=false` option currently uses the reliable path because the
ordered 64 KiB chunk format is not suitable for Steam's unreliable datagram
size. Large snapshots can still queue behind other messages. Live two-account
testing must confirm native throughput and whether the bundled Steam runtime
supports this channel on both installations.
