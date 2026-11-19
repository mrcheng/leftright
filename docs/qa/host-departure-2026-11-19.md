# Host departure verification - 2026-11-19

Verification performed against the local WebGL build before deployment.

The host must confirm leaving through Back/Escape, results Main Menu or settings Leave Drill. Stay preserves the current room and round. Leave & Kick stops advertising, rejects joins and starts, sends a reliable ended notice, then shuts down the session after a short transport flush. Guests return to the menu with a persistent notice. An old asynchronous close cannot end a subsequently created room.

## Results

- **23 editor checks passed** with `Tools/Rebuild/VerifyHostLeave.cs`: confirmation/cancellation, listing deletion, no recreation while closing, countdown/settings, persistent notice, offline behavior and rehosting race protection.
- **14 browser checks passed** with `Tools/Web/VerifyHostLeave.cjs`: two independent Chromium instances, desktop host and landscape mobile guest, using real Unity Authentication/Lobby/Relay against the new local WebGL build. Covered Stay, confirmed lobby exit, native overlay suppression, unload warning registration, successful cloud DELETE response, reliable guest notice, exit during a started round and abrupt host browser termination. No browser page errors.
- Screenshots inspected for desktop confirmation and mobile guest notice. Mobile testing used Chromium device emulation, not physical iOS Safari.

## Limits

Normal confirmed exit initiates cloud deletion immediately; the service request takes network time. Abrupt browser/device termination cannot guarantee a successful delete. Guests recover after transport disconnect detection; stale public listings rely on service expiry. Browser navigation warnings are native browser UI where supported; the synthetic test verifies handler registration, not every browser's native prompt behavior. Switching tabs/apps does not intentionally end the room.

This change addresses host departure only. The separate matchmaking audit findings remain tracked in `matchmaking-audit-2026-11-19.md`.
