# Changelog — Erenshor Campmaster

## Unreleased — audit hardening

### Fixed / hardened

- Hunt Camp automatic recognition now fails closed when player position is unknown
  or party authority is partial/COOP/unresolved.
- Player-held Puller activity stays `UNKNOWN` when the Sim-only pull lifecycle is
  unavailable instead of claiming `WAITING`.
- Relax no longer treats unknown combat as combat-clear and only resumes after
  combat when the player is back inside the Relax anchor radius.
- Relax local-party loss uses the existing grace even when tracking entries remain
  but local Sim avatars are unresolved.
- Hunt Camp now applies the same grace-and-end rule when party tracking entries
  remain but no locally resolved Sim is available, preventing an explicit camp
  from surviving a stale or remote-only roster.
- Valid Hunt Camp declarations deterministically replace Relax; invalid declarations
  leave Relax intact.
- Follow/control declaration is admitted from one fresh snapshot and consumed
  immediately, eliminating the stale queued-request window across zoning.
- Bounded event streams expose their oldest retained sequence so consumers can
  detect replay gaps.
- Party roster comparison ignores slot order/case to avoid duplicate social events.
- `/camp setup` and `/camp status` diagnostics now expose missing native setup facts
  more directly without changing them.
- Stale Phase-4 API/design documentation was corrected.

## 0.4.0 — Phase 4: explicit Relax social downtime

### Added

- Explicit `/relax here`, `/relax off`, and `/relax status`.
- Read-only `/camp setup` checklist showing actual Main Tank/Main Assist/Puller, Healing/Mana, Guard, Auto Pull, and the next native setup step.
- A deterministic Relax lifecycle separate from Hunt Camp: Active, combat
  suspension/resume, and clean termination on zone change, sustained anchor
  departure, party loss, or explicit off.
- Mutual exclusion between Hunt Camp and Relax; Hunt Camp auto-recognition is
  paused while Relax is active.
- Read-only API schema 3 with real `IsRelaxActive`, Relax state in the current
  snapshot, and a separate sequenced `GetRelaxEventsAfter(...)` stream.
- Deterministic Relax lifecycle tests.

### Still deliberately not implemented

- Automatic Relax from sitting, Guard, inactivity, tavern appearance, or any
  guessed location type. Relax is explicit intent.
- Native Guard/Stay invocation or any custom movement loop.
- Any combat, pull, heal, loot, equipment, quest, or travel automation.

## 0.3.0 — Phase 3: richer recovery/pull semantics

Read-only Phase 3 implementation plus build repair.

### Fixed

- Restored buildability of the uploaded Phase-3 work-in-progress by removing
  duplicated statements in `CampSessionTracker.cs` and implementing the
  missing `ReadPullerActivity(...)` path.
- Completed propagation of the new pull/recovery fields through observation,
  session snapshot, API, status, and deterministic tests.

### Added

- `Pulling` / `PULL INCOMING` from the assigned Sim Puller's verified native
  `CurrentPullPhase` lifecycle. The reflection read is cached and fails closed
  if the member is unavailable. Player-held Puller remains unknown rather than
  guessed.
- Current verified pull-target name from the Sim Puller's native `PullTarget`
  when readable.
- Exact read-only recomputation of the native per-healer mana gate from
  `SimPlayerGrouping.Heals`, `Stats.CurrentMana`,
  `Stats.GetCurrentMaxMana()`, and `ManaNeededForPull`.
- `Recovering` only when out of combat, Auto Pull is enabled, and that verified
  native healer-mana gate is currently blocked.
- Verified pull counter and `camp_pull_started` events on native pull-lifecycle
  rising edges.
- `camp_encounter_started` / `camp_encounter_completed` events.
- Deterministic repeated-enemy seed: `camp_repeated_enemy` fires once when the
  same exact verified pull-target name reaches the configurable threshold
  (default 3 pulls). It does not claim a kill or infer an enemy family.
- Deterministic rough-encounter seed: `camp_rough_encounter` is based only on
  verified combat duration crossing a configurable Campmaster threshold
  (default 45s), explicitly marked `CampmasterDerivedLongCombat`.
- `camp_recovery_started` / `camp_recovery_ended` events for the native mana
  gate.
- `camp_role_snapshot` events from actual native Manage Roles assignments,
  suitable for optional Deep Sims `role_comment` mapping without class guesses.
- API schema 2 additive fields and `verified.<key>` event payloads.
- Richer `/camp status` showing pull lifecycle/target, healer mana gate, verified
  pulls, and bounded Phase-3 seed counters.
- Seven Phase-3 deterministic regression cases (29 total).

### Still deliberately not implemented

- Any gameplay write/automation.
- Player-puller lifecycle inference when no equivalent native player pull-phase
  signal is available.
- Enemy-family inference, kill attribution, death/close-call claims, or rarity
  claims from the new social-seed events.


## 0.1.0 — Phase 1: Hunt Camp recognition and context

First implementation. Observation only; no native write path exists.

### Added

- Deterministic Hunt Camp session lifecycle (`Inactive / Active / Suspended /
  Breaking`) with `Waiting / Fighting / Unknown` activity. `Establishing` is
  reserved in the enum; the pre-recognition stability window is currently
  reported through `/camp status` ("Waiting on: …") rather than as a state.
- Automatic recognition from a fully verified native predicate: current party
  + every locally-resolvable party Sim guarding at one anchor + the player at
  that anchor + assigned Puller + `PullConstant` (Auto Pull) on + no raid,
  held for a stability window. Fails closed on any unknown signal.
- Commands `/camp status`, `/camp here`, `/camp clear`, `/camp auto on|off`,
  `/camp selftest`.
- Native readers for the Role Manager (`MainTank`, `MainAssist`,
  `DesignatedMA`, `Puller`, `CC`, `Heals` and their `PlayerIsX` flags), pull
  settings (`PullerRangeHigh/Low`, `MaxPullDist`, `ManaNeededForPull`), pull
  state (`PullConstant`, `isPulling`, `ForcePullTarget`) and the guard anchor
  (`SimPlayer.GuardSpot` / `GetGuardPos()`), with a tolerance derived from the
  live `SimPlayerGrouping.SpreadMagnitude`.
- Read-only `CampmasterApi` (schema 1): snapshot plus a sequenced bounded event
  stream, primitives only, absent key means unknown.
- COOP compatibility by reflection only; remote/unresolved members degrade
  authority and are excluded from the anchor.
- 22 deterministic lifecycle tests runnable standalone or via `/camp selftest`.
- `docs/CAMP_PHASE1_ASSEMBLY_FINDINGS.md` recording every native member used,
  its call graph, and the open questions.

### Deliberately not implemented

- `Pulling` / `Recovering` activity states. `SimPlayer.CurrentPullPhase`
  (`PullPhases`) was located and documented, but its transitions have not been
  observed live, so Phase 1 counts **completed encounters, not pulls**.
- Any "holding for mana" claim. The native check is a per-healer gate inside
  the pull loop with no exposed state field.
- Deep Sims integration, social memory, LLM calls, and any native command
  writes.
