# MVZ-MP Steam session transport

The session uses the game's Steamworks callback loop and Steam lobby chat.
Each application message is addressed to one Steam lobby member, split into
3,800-byte chunks, and reassembled by sender and sequence. Chat is reliable;
the public `reliable=false` option currently uses the same reliable path.
The transport rejects packets from nonmembers and packets addressed to
another member.

Limits: 4 MiB per application message, 8 MiB queued output, 8 MiB incomplete
or blocked incoming messages, 128 pending incoming messages, and four chat
packets submitted per game update. Incomplete messages and missing sequence
gaps expire after 30 seconds. A failed Steam chat send ends the session so a
peer does not continue with silently missing zoo state.

Every chunk is a lobby chat message, so all lobby members receive the wire
traffic even though only the intended recipient accepts it. Large snapshots
can take many frames and may hit Steam chat throttling. Keep routine state
updates and audio clips small. A later SteamNetworkingMessages transport can
use the same session API if live two-account testing shows chat throughput is
insufficient.
