# Network Tank Arena — Server-Authoritative Team Deathmatch

## Project summary

This Unity project extends the provided Network Tank Arena starter using **Netcode for GameObjects (NGO)**. The main networking focus is **consistent, server-authoritative team scores and match outcomes**, including for clients that connect after a match has started or finished.

The updated course starter already supplied networked movement, server-controlled firing, and projectiles. My additions include Red/Blue team assignment and materials, player health and timed respawning, elimination scoring, and a first-to-three match with a synchronized winner screen.

## How to run and test

1. Open the Unity project and the configured gameplay scene (`SampleScene`).
2. In **Multiplayer Play Mode**, run a Host and one Client. Use the on-screen **Start Host** and **Start Client (localhost)** controls. The session uses Unity Transport on localhost, port **7777**.
3. Verify that each player has a team and can move, aim, and shoot using the project's configured Input System actions.
4. Eliminate opponents to score. A tank starts with **100 HP**; the weapon's default damage is **25 per hit**. Eliminated tanks respawn after about **3 seconds** while the match is active.
5. The first team to reach **3 points** wins. Verify that both instances show the same score and winner and that gameplay stops after the match ends.
6. To test a late join, keep the Host running, disconnect the Client, then reconnect it. Its scoreboard should display the current or final match state rather than restarting at 0–0.

The provided session currently supports **two players**, one in each team. Internet matchmaking/Unity Relay was not implemented.

**Scene setup:** `LocalNetworkSession` requires the scene's input/camera references, two spawn transforms, a registered damage-target prefab and spawn marker, and a registered `NetworkMatchManager` prefab. The `NetworkManager` must also have the tank and projectile prefabs configured correctly.

## Networking decisions

- **Authority:** Clients submit movement and firing requests; the server simulates movement/projectiles, determines hits and damage, and decides eliminations, scores, respawns, and victory. Clients cannot directly award points.
- **Team identity:** The server uses the connection/spawn-slot mapping to assign Red or Blue. Each tank replicates its team ID; its colour material is chosen locally from that ID.
- **Health and respawn:** Health is a server-written `NetworkVariable<int>`. At 0 HP, the server blocks further gameplay actions and schedules a respawn. The same player NetworkObject is reused and teleported to its assigned spawn point, preserving ownership and team.
- **Match state:** One server-owned `NetworkMatchManager` replicates a `MatchState` containing `RedScore`, `BlueScore`, `Phase`, and `Winner`. The server accepts a point only for a confirmed enemy elimination while the match is playing; once a team reaches 3, scoring stops.
- **State vs. events:** Firing uses an RPC because it is a request for a one-time action. Health, team and match state use NetworkVariables because clients (including late joiners) need their current values, not a replay of past events.

## Networking problem and improvement

The first scoreboard implementation used **four separate NetworkVariables** for the two scores, match phase, and winner. A winning elimination required separate assignments, creating the possibility of other code observing an intermediate combination (for example, a winning score while the phase still says `Playing`). I did **not** observe an actual client scoreboard mismatch with this version.

I changed the scoreboard to use **one `NetworkVariable<MatchState>`**. The server copies the current state, updates the score and any winning outcome, then publishes **one complete snapshot**. A validation method checks the received snapshot's score/phase/winner combinations and logs `Valid: True` or an error. This makes match transitions easier to reason about and avoids publishing partial match outcomes.

**Trade-off:** A score change now serializes the whole match-state struct rather than only the changed scalar value. I did not measure bandwidth, and I do not claim this change improves latency or network cost. The snapshot is internally consistent, but health, projectile state and transforms are still replicated separately.

## Repeatable network tests

For each preset I started a fresh Host + Client session, performed the same elimination sequence (**Red → 1–0; Blue → 1–1; Red → 2–1; Red → 3–1**), and checked respawns and the final winner on both instances. I also separately tested Client reconnect, including after a match finished. Ratings below are **subjective severity scores**: 0 = no noticeable problem; 3 = severe.

| Simulator preset | Delay | Jitter | Loss | Correctness | Firing | Movement | Scoreboard |
| --- | ---: | ---: | ---: | --- | ---: | ---: | ---: |
| Clean Local Host | 0 ms | 0 ms | 0% | Pass | 0/3 | 0/3 | 0/3 |
| Moderate Latency | 100 ms | 0 ms | 0% | Pass | 2/3 | 2/3 | 1/3 |
| Latency Plus Jitter | 100 ms | 50 ms | 0% | Pass | 2/3 | 3/3 | 2/3 |
| Lossy Connection | 0 ms | 0 ms | 10% | Pass | 1/3 | 0/3 | 0/3 |
| Combined Impairment | 100 ms | 50 ms | 5% | Pass | 3/3 | 3/3 | 1/3 |

All presets used **Packet Loss Interval = 0**. The Host felt considerably more responsive than the remote Client under latency. Under jitter and combined impairment, the Client sometimes saw projectiles move inconsistently or disappear early, even though server-confirmed damage and final scores were correct. With combined impairment, the Client's death screen sometimes appeared roughly half a second after the server-side event.

**Before/after evaluation:** The original four-variable scoreboard passed the five gameplay correctness tests. After switching to the single snapshot, I repeated **Clean Local Host** and **Combined Impairment**. Host and Client logs both showed valid match-state progression: `0–0 Playing`, `1–0 Playing`, `1–1 Playing`, `2–1 Playing`, and `3–1 Finished, Winner Red`. No invalid match snapshots appeared. Disconnecting and reconnecting the Client after the win produced the correct final snapshot again.

## Troubleshooting and known limitations

- Initially, starting the Host failed configuration validation. I traced the failure to an unassigned `NetworkMatchManager` prefab reference in `LocalNetworkSession`, assigned the prefab in the Inspector, and verified the session could start.
- Under **Combined Impairment**, the Client logged two NGO **Deferred OnSpawn / NetworkTransformMessage** warnings: transform messages for NetworkObject IDs 13 and 17 expired after a 10-second wait for those objects to spawn. I separated these warnings from the match-state logs: scores and victory remained correct. Short-lived networked projectiles are a possible explanation, but the IDs were not traced to specific objects, so the root cause remains unconfirmed.
- Movement and projectile presentation are noticeably delayed or irregular under heavy impairment. This implementation prioritizes authoritative results rather than client-side prediction or projectile visual prediction.
- Team allocation and spawn positions are designed for two players. There is no Unity Relay/internet connection flow, and loss of the Host ends the session.
- Closely timed competing eliminations and delayed fire requests crossing a death/respawn boundary have not been covered by an automated stress test.

The measured result is **correct match outcomes in the tested conditions**, not elimination of all networking edge cases.
