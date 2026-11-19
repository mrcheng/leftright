# Left Right matchmaking audit — 19 November 2026

Tested the deployed game at https://mrcheng.github.io/leftright/, revision `8a82343729ee1a7f2aeaf25892aee33c1b270ebc`. This audit changed no production code and deployed no fixes.

**Coverage:** 36 scenario runs, including both host/guest layout directions, the four supported join routes and capacities 2–4.

**Outcome:** five reproducible UI/flow defects and one intermittent connection/presence failure. The exact reported “1/4 after choosing a two-player public room” was not reproduced verbatim. A related wrong-capacity display, **0/4 for an advertised two-player room**, was reproduced before the first host snapshot.

**Environment and limits**

- Real Unity Authentication, Lobby and Relay/WSS, through the published menus. No direct-IP substitute.
- Independent Chromium browsers, with desktop, Android-sized guest and iPhone-sized/user-agent host layouts. Phone emulation is still Chromium; **physical iPhone/Safari was not tested**.
- The initial matrix used software WebGL. One failure occurred during overlapping emulator loads; its trigger remains unproven. Later tests used Intel/Direct3D hardware rendering.
- A debugger pause stopped the host's `rafUpdateLoop` for about nine seconds. This validates a controlled main-loop stall, not actual iPhone app switching. An earlier Page Lifecycle freeze did not stop joining and is not counted as demonstrated suspension.

**Normal join coverage**

| Capacity | Desktop host: main public list | Desktop host: JOIN public list | Desktop host: public code | Desktop host: private code |
|---|---|---|---|---|
| 2 | First run failed intermittently; see M6 and control runs | Passed | Passed | Passed |
| 3 | Passed | Passed | Passed | Passed |
| 4 | Passed | Passed | Passed | Passed |

Phone-layout hosts also passed all twelve capacity/route combinations. Two fresh hardware-rendered controls of desktop-host/two-player/main-list joining both passed. Per-run details are enumerated in the JSON companion. Unless labelled “fill N human players,” capacity-3/4 cases used two humans and verified starting with remaining slots assigned to AI. Separate tests filled **3/3 and 4/4 actual humans**, confirmed the count on every client, and started everyone successfully.

Also tested: lowercase/space-padded codes; mobile and desktop Enter; empty, invalid and expired codes; full-room admission; joining after a round starts; Back during a delayed lookup; and a stalled host.

**M1 — P1: Back does not cancel a pending public join. Reproduced.**

Steps: host a public room; on the JOIN screen click its public Join button; delay that room's real Lobby GET; press Escape/Back to Main; release the response.

Expected: remain on Main without joining. Actual: the guest moves back into the online lobby and joins; both sides show two humans. Screenshots: [before response](matchmaking-2026-11-19/cancel-before.png), [after response](matchmaking-2026-11-19/cancel-after.png).

Cause: [PublicDrillLobby.JoinAsync](../../Assets/Rebuild/Scripts/PublicDrillLobby.cs) accepts either Main or Online after its await. Back changes Online to Main, so the guard still passes. There is no join-attempt generation invalidated by Back.

Fix direction: invalidate a separate join-attempt token on Back/navigation; check the token and intended screen before starting Relay. Include delayed-response and overlapping-join regression checks.

**M2 — P2: A pending guest displays the wrong capacity and a premature waiting lobby. Reproduced.**

Joining an advertised two-player room while the host was paused displayed “Connecting to your friend's game…”, **0 / 4 PLAYERS**, and **WAITING FOR HOST**. This persisted during the stall. Resuming the host recovered both sides to 2/2. [Screenshot](matchmaking-2026-11-19/pending-wrong-capacity.png).

Cause: [OnlineMatch.Capacity](../../Assets/Rebuild/Scripts/OnlineMatch.cs) defaults to 4. Joining sets Active before confirmation; only ReceiveState replaces Capacity/HumanCount. [StartMenu](../../Assets/Rebuild/Scripts/StartMenu.cs) renders those values for an active client. This explains an incorrect four-player denominator during connection, but does not prove the user's exact 1/4 incident had this cause.

Fix direction: distinguish Allocating, Connecting and Confirmed lobby. Use selected-room capacity only as pending metadata, or omit counts until confirmed. Do not present a joined waiting lobby before the first valid host state.

**M3 — P2: Join failures return to Main without displaying their reason. Reproduced.**

A late join produced internal status “Match already started.” A full-room attempt produced “Connection lost. Reconnect or host another game.” Both returned to menu page 0, where the status is not drawn. [Late-join screen](matchmaking-2026-11-19/late-join-error-hidden.png).

Cause: OnlineMatch.Disconnected/Leave retain Status and call menu.Open(); StartMenu displays that status on online pages, not Main. The intermittent failure in M6 ends the same way.

Fix direction: retain JOIN with a visible retryable error, or show a persistent failure banner on Main. Cover full, started, expired, unavailable-host and timeout cases.

**M4 — P3: Desktop Enter does not submit a code. Reproduced.**

Typing a valid private code and pressing Enter in desktop Chrome did nothing. Clicking JOIN afterward connected. Mobile native-form Enter worked, including lowercase codes with surrounding spaces. [Desktop screenshot](matchmaking-2026-11-19/enter-not-submitted.png).

Cause: StartMenu.Update handles Enter on Main, LocalGame and Couch, but not the desktop online code field. The native HTML form has an onsubmit handler.

Fix direction: submit the desktop field on Enter through the normal validation/busy guard, without double handling the key.

**M5 — P3: Some expected failures are mislabelled as connectivity failures. Reproduced.**

- A third guest was correctly refused from a full two-player room; host count remained two. The guest received generic “Connection lost” rather than a full-room explanation, then M3 hid it.
- Invalid input `000000` produced “Could not reach online services. Check your connection and try again,” while other service operations worked. [Screenshot](matchmaking-2026-11-19/invalid-code-message.png).
- Empty input correctly requested a code. An expired code belonging to this audit correctly produced “Code not found. Ask your friend for a fresh code.”

Cause: [UnityRelayConnection.FriendlyError](../../Assets/Rebuild/Scripts/RelayConnection.cs) maps selected reasons and otherwise uses connectivity wording. Disconnected similarly falls back when no recognized reason arrives.

Fix direction: distinguish invalid, missing/expired, full, already-started and genuine connection failures; keep their messages visible.

**M6 — P1 investigation: Host and guest disagree about whether joining succeeded. Intermittent; trigger unproven.**

In the first desktop-host/main-public-list/two-player run, the host showed **2/2** and enabled START MATCH. The guest reached Connecting, then returned to Main with Connection lost before receiving a confirmed lobby state. The host still showed 2/2 / room full in the failure capture roughly 30 seconds after first counting the guest. [Host](matchmaking-2026-11-19/ghost-host.png), [guest](matchmaking-2026-11-19/disconnected-guest.png).

This happened during overlapping software-rendered test loads. It is not a proven ordinary-load transport defect or an iPhone diagnosis. Other routes and phone-layout list joins passed; both fresh hardware-rendered control retries passed. This remains an intermittent investigation, not a stable reproduction.

Source findings: HumanCount comes from NGO callbacks. CanStart checks only host + at least two counted humans. There is no application-level acknowledgement that a guest received its initial state. The exact transport event behind this disconnect was not established.

Investigation direction: record per-attempt IDs and host/guest state transitions without credentials; consider a lobby-ready acknowledgement, reconciliation/expiry of unfinished slots, and visible retry handling. Repeat short stalls and forced disconnects on a physical iPhone and desktop Chrome over separate networks.

**Expected behavior and next work**

- Full rooms and running rounds rejected additional guests. Three- and four-human rooms started successfully.
- Starting is host-controlled. WAITING FOR HOST is normal after a confirmed join; filling a room does not automatically start it.
- The verified short host stall recovered when resumed. Longer real mobile-browser suspension remains unverified. Unity documents that [background web content may be throttled despite Run in Background](https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/webgl/develop/performance); this is a test consideration, not a diagnosis of the reported incident.
- Prioritize M1 cancellation, M3 visible errors, and M2 truthful connection state; investigate M6 readiness/presence; then address Enter and error wording.

Selected evidence is alongside this report; per-run summaries are in `matchmaking-audit-2026-11-19.json`. Full timelines and additional screenshots remain locally in gitignored `Logs/OnlineAudit/`. Reproduction runners are `Temp/AuditOnline.cjs`, `Temp/AuditOnlineEdges.cjs`, `Temp/AuditCancelRace.cjs` and `Temp/AuditFinalControls.cjs`.
