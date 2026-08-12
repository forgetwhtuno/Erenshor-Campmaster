# CAMP / RELAX LOCAL AI HANDOFF

You are implementing a new Erenshor system tentatively called **Erenshor Campmaster**.

This document is an implementation handoff, not permission to redesign the feature into a bot.

Read these first:

1. `docs/CAMP_RELAX_DESIGN.md`
2. `docs/CAMP_RELAX_GAME_HOOKS.md`
3. the **current local** source of:
   - Deep Sims
   - Erenshor Follow
   - Erenshor Party Tools
   - Practice Duels
4. the installed game's current `Assembly-CSharp.dll`

Public GitHub was research only. **Do not assume public `main` equals the local tree.**

> **Current implementation status (2026-08-10):** Campmaster **0.4.0 / Phase 4** is implemented. Hunt Camp observation, the bounded declaration control API, and explicit Relax are live. The older Phase 1/Phase 3 bootstrap instructions later in this handoff are retained as historical design context, not as the current implementation status.

---

## Mission

Build the smallest safe deterministic foundation for:

- recognizing an intentional old-school MMO hunting camp;
- tracking factual camp context;
- later exposing that context to Deep Sims;
- explicit Relax Here social downtime later.

The player should feel:

> “I am sitting at an MMO camp with a group of persistent players.”

Not:

> “I turned on an automation bot.”

---

## Non-negotiable architecture rule

**DO NOT REPLACE ERENSHOR SYSTEMS.**

Erenshor already owns:

- Main Tank;
- Main Assist;
- Healing/Mana;
- Crowd Control;
- Puller;
- Pull Target;
- Auto Pull;
- pull level/range settings;
- mana hold threshold;
- Guard/Stay;
- Follow;
- Attack;
- Run Away;
- SimPlayer combat AI.

Campmaster observes and coordinates context around these systems.

Do not write a second pulling/combat/healing/navigation AI.

---

## First action: inspect, do not edit

Before changing files:

1. Run `git status`.
2. Read repository-local `AGENTS.md` / contributor instructions.
3. Identify the current branch and recent relevant commits.
4. Inspect all existing Camp/Deep Sims/party state code.
5. Locate the actual installed `Assembly-CSharp.dll` used by the build script.
6. Decompile/inspect the exact current assembly.
7. Write down discovered members and semantics.
8. Compare those findings against `CAMP_RELAX_GAME_HOOKS.md`.

Do not “fix” unrelated code while doing discovery.

Do not reset, checkout, or overwrite unrelated agent/user changes.

---

## Mandatory native discovery

Before Camp auto-recognition, prove the following.

### Role Manager

Find the exact current storage and command paths for:

```text
Main Tank
Main Assist
Healing/Mana
Crowd Control
Puller
```

Do not derive these from class.

For every role, report:

- declaring class;
- exact field/property/method;
- value type;
- how the player is represented;
- how SimPlayers are represented;
- all important callers;
- zoning/party lifecycle.

### Auto Pull

Prove:

- exact Auto Pull enabled state;
- exact semantics of any `isPulling` member;
- current pull target if available;
- Pull Target entry point;
- Auto Pull entry point;
- Stop Pulling entry point;
- behavior on Guard, Follow, death, Run Away, zone, party change.

Do not use `isPulling` as `AutoPullEnabled` until its read/write call graph proves that meaning.

### Pull settings

Find exact current storage for:

```text
max target level above
max target level below
max pull distance
hold if under % group mana
```

Trace the native mana-threshold calculation. Do not reproduce it from the wiki.

### Guard/Stay

Trace native Shift+6 and `/group wait|guard|stay`.

Determine whether there is:

- central Guard state;
- per-Sim `GuardSpot`;
- common guard anchor;
- an existing public command method.

Do not implement a movement loop to imitate Guard.

### Mana

Historical Phase-1 task: find exact current/max mana members and trace the native pull-readiness mana calculation before generating any low-mana facts. Phase 3 established that the installed game checks each assigned Sim healer independently; it is not a group average.

---

## Historical Phase 1 implementation scope — completed

Implement **Camp recognition/context only**.

Recommended new standalone repo/module:

```text
src/
  CampmasterPlugin.cs
  CommandRouter.cs
  PartyStateReader.cs
  NativeGroupStateReader.cs
  CampRecognition.cs
  DowntimeStateMachine.cs
  CampSessionTracker.cs
  CampEventStream.cs
  CampmasterApi.cs
  CoopCompatibility.cs
```

A tiny status panel is optional. Chat status is sufficient.

### Required commands

```text
/camp status
/camp here
/camp clear
```

Optionally:

```text
/camp auto on
/camp auto off
```

### Semantics

`/camp here`
- declares hunting-camp context at the current location;
- captures a context anchor;
- does not toggle native Auto Pull;
- does not assign roles;
- does not attack;
- does not move Sims.

`/camp clear`
- clears Campmaster context;
- does not send native Stop Pulling.

`/camp status`
- prints only verified fields;
- unknown must remain unknown.

### Automatic recognition

Enable only if you can prove a strong predicate such as:

```text
verified party
AND verified stable Guard/Stay anchor
AND verified Puller
AND verified Auto Pull enabled
AND not explicit travel
```

If any required state is unverified, do not weaken the predicate just to make auto mode work. Use `/camp here` as the fallback.

### Session state

Start simple:

```text
Inactive
Establishing
Active
Suspended
Breaking
```

Activity may initially be only:

```text
Waiting
Fighting
Unknown
```

Add `Pulling` and `Recovering` only when their native signals are proven.

---

## Phase 1 hard non-goals

Do not implement:

- target selection;
- pulling;
- attack;
- healing;
- CC;
- auto loot;
- item distribution;
- selling;
- gearing;
- quest logic;
- arbitrary travel;
- auto retreat;
- auto-resurrect;
- auto restart after wipe;
- LLM calls;
- Deep Sims memory writes;
- COOP command synchronization.

---

## Phase 1 data requirements

Use immutable/snapshot data where practical.

Conceptual model:

```text
CampSnapshot
  SchemaVersion
  SessionId
  IsActive
  RecognitionSource     Auto | Explicit
  Zone
  StartedUtc
  Activity
  Anchor
  Party[]
  AuthorityQuality
  Roles
  PullState
  Counters
```

Every optional native field must support unknown.

Never use a default value that could be mistaken for a verified game fact.

Examples:

```text
Puller = null          // unknown
AutoPullEnabled = null // unknown
HoldManaPercent = null // unknown
```

not:

```text
Puller = "Paladin"
AutoPullEnabled = false
HoldManaPercent = 50
```

unless those values are actually read.

---

## Event stream

Add a tiny read-only event sequence now if it does not complicate Phase 1.

Conceptual:

```text
CampEvent
  Sequence
  EventId
  Utc
  SessionId
  Mode
  Type
  Zone
  VerifiedFields
```

Initial events can be limited to:

```text
camp_started
camp_ended
camp_suspended
camp_resumed
camp_party_changed
```

Do not emit pull/mana/retreat events until hooks are proven.

Provide a stable public read surface for future optional consumers.

Prefer:

```text
CampmasterApi.GetCurrentSnapshot()
CampmasterApi.GetEventsAfter(sequence)
```

Exact signatures are your implementation decision after reviewing the current target framework and existing mod compatibility style.

---

## Deep Sims integration — Phase 2, not Phase 1 unless trivial

When Campmaster Phase 1 is stable:

1. Add runtime detection in Deep Sims.
2. Do not make Deep Sims a hard dependency of Campmaster.
3. Poll/read Campmaster snapshot/events.
4. Feed events through the existing `SocialBudget` and `EventConversationDirector`.
5. Do not build a second chatter scheduler.
6. Add current verified camp context to the game-fact portion of prompts.
7. Unknown fields must not appear as claims.
8. Keep templates/LLM/Off behavior intact.

Potential verified social seeds:

```text
camp_recovery
mana_hold
rough_encounter
repeated_enemy
loot
long_camp
role_comment
camp_memory
```

Only create a seed when its underlying fact is proven.

---

## Current Deep Sims migration

Current public Deep Sims has a legacy sitting/meditation `/dscamp`.

When integrating Campmaster:

- preserve legacy behavior if Campmaster is absent;
- when Campmaster is present, do not let sitting alone become Hunt Camp or Relax;
- sitting during Hunt Camp is ordinary recovery;
- explicit Relax will later replace the semantic purpose of the old social-camp mode;
- keep current `SocialBudget`, templates, event director, memory, and grounding.

Also inspect local source: it may be newer than public 0.7.0.

---

## Phase 3 completion notes

Campmaster 0.3.0 now exposes the following additive events for an optional Deep
Sims compatibility reader:

```text
camp_pull_started
camp_encounter_started
camp_encounter_completed
camp_recovery_started
camp_recovery_ended
camp_repeated_enemy
camp_rough_encounter
camp_role_snapshot
```

Phase-3 event payloads use flat `verified.<key>` entries in API schema 2. Deep
Sims should continue to route any eventual expression through its existing
`SocialBudget` / `EventConversationDirector`; Campmaster must not grow its own
chatter scheduler.

`camp_repeated_enemy` is exact pull-target repetition, not kill history.
`camp_rough_encounter` is a duration-derived Campmaster classification, not a
native danger verdict. `camp_role_snapshot` contains actual Manage Roles facts,
never class-derived role guesses.

## Relax Here — later phase

After Camp context is stable, implement:

```text
/relax here
/relax off
/relax status
```

Relax must be explicit.

Do not infer it from:

- inactivity;
- sitting;
- Guard alone.

Relax changes **social context**, not game truth.

Combat suspends Relax chatter. Travel/zone ends it.

Only after native Guard command path is verified may `/relax here` optionally issue the one native Guard/Stay operation. Do not emulate Guard.

---

## Persistence

Campmaster active sessions are ephemeral.

Do not restore an active camp after:

- game restart;
- zone change;
- stale save/load state.

Long-term social memory belongs in Deep Sims.

When adding camp memory:

- store a compact verified completed-camp summary;
- keep it bounded;
- do not call loot “rare” unless rarity is verified;
- do not double-count shared outing minutes/familiarity.

---

## Practice Duels compatibility

Inspect current local Practice Duels.

Public 0.3.x currently treats sitting/legacy Deep Sims camp state as duel-incompatible and reflects into private Deep Sims state.

After Campmaster exists, prefer a stable Campmaster status API.

Do not patch Practice Duels in Phase 1 unless required to prevent a concrete regression.

If you do touch it:

- preserve all existing virtual-health safety boundaries;
- do not weaken outside-hostile cancellation;
- do not overwrite unrelated duel work;
- report exact changed files and why.

---

## Erenshor Follow compatibility

Follow remains owner of player travel.

Campmaster should not call or duplicate FollowController/LeaderController movement.

For automatic recognition, it is acceptable to disable camp recognition while verified Follow/Lead travel is active if a stable read-only status is available.

Do not create a hard dependency.

---

## COOP

Fail closed.

- Exclude remote humans from local Sim automation.
- Do not command remote-owned network Sims.
- Do not invent synchronization.
- If required state is remote/unresolved, mark authority partial/unknown.
- Keep writable Campmaster orchestration off in COOP until host authority is proven.

---

## Build discipline

Use the repository's current build/install mechanism.

The build must compile against the **installed current assemblies**, not copied assumptions.

After each small phase:

1. build;
2. run deterministic/unit tests if present;
3. run focused in-game acceptance tests;
4. inspect BepInEx logs;
5. report failures by root cause.

Do not stack several unverified Harmony patches before testing.

---

## Testing minimum for Phase 1

Prove:

1. no party -> no auto camp;
2. Follow state -> no auto camp;
3. Guard only -> no auto camp;
4. verified Guard + Puller + Auto Pull -> auto camp if all signals are authoritative;
5. unknown Auto Pull -> no auto camp;
6. `/camp here` works without changing native gameplay;
7. `/camp clear` does not change native pulling;
8. combat does not end a Hunt Camp;
9. zone change ends/finalizes it;
10. party churn does not retain stale Unity references;
11. mod disabled/uninstalled -> native Erenshor behavior unchanged;
12. COOP remote/unresolved state fails closed.

---

## Required report after each implementation phase

Return:

### Discovery

```text
Exact native classes/members found:
- ...
Semantics verified by:
- call graph:
- live test:
Unresolved:
- ...
```

### Changes

```text
Files changed:
- path — reason
```

### Tests

```text
Build:
Deterministic tests:
In-game tests:
Failures:
```

### Root causes

For every failure:

```text
symptom
-> actual root cause
-> evidence
-> minimal fix
```

Do not report only “fixed.”

### Preservation

Confirm:

```text
- unrelated user/agent changes preserved
- no forced reset/checkout
- no game DLLs committed
- no GitHub push performed
```

---

# Historical Phase 1 copy/paste implementation prompt — completed

You are continuing development of my Erenshor mod suite.

Historical bootstrap note: the original handoff's first task was **Phase 1 of Erenshor Campmaster: deterministic Hunt Camp recognition/context only**. That work is complete through Phase 3 in the current tree. Do not restart Phase 1 unless repairing a regression; continue from the current implementation and treat **Phase 4: explicit Relax Here** as the next feature phase.

Read `docs/CAMP_RELAX_DESIGN.md` and `docs/CAMP_RELAX_GAME_HOOKS.md` first.

Important constraints:

- Inspect the current local working tree before editing.
- Read any `AGENTS.md`.
- Run `git status` and preserve unrelated changes.
- Inspect the installed current `Assembly-CSharp.dll`; public GitHub is not authoritative for my local build.
- Verify every native hook before coding against it.
- Do not invent signatures or field semantics.
- Build against the current installed assemblies.
- Do not push to GitHub.
- Do not replace Erenshor combat, pulling, roles, mana handling, Guard, Follow, or target selection.

Research and prove the current native implementation of:

1. Main Tank / Main Assist / Healing-Mana / Crowd Control / Puller role storage.
2. Auto Pull enabled state. In particular, determine exactly what any `SimPlayerGrouping.isPulling` member means by tracing its reads/writes.
3. Pull Target / Auto Pull / Stop Pulling command paths.
4. Max pull level +/- settings, pull distance, and hold-if-under group mana threshold.
5. Native Guard/Stay command path and whether a stable common guard anchor can be read.
6. Exact current/max mana fields and native pull-readiness mana formula (now verified as a per-assigned-Sim-healer threshold, not a group average).
7. Party membership lifecycle and COOP/local ownership.

Then implement the smallest safe standalone Campmaster foundation.

Phase 1 behavior:

```text
/camp status
/camp here
/camp clear
optional: /camp auto on|off
```

`/camp here` declares context only. It must not toggle Auto Pull, assign roles, attack, move Sims, manage loot, heal, or travel.

Automatic recognition should activate only from strong verified evidence, preferably:

```text
verified current party
+ stable native Guard/Stay anchor
+ verified Puller
+ verified Auto Pull enabled
+ not active travel
```

If any required native signal cannot be proven, fail closed and rely on `/camp here`.

Track a small ephemeral session with zone, anchor, party, verified role/pull fields, start time, and safe counters. Use nullable/unknown fields instead of guesses.

Expose a small read-only compatibility surface for future Deep Sims integration if doing so is straightforward, but do not add LLM/social-memory work yet.

Combat is expected inside Hunt Camp and must not end the session. Zone change, explicit clear, party loss, or sustained departure should end it safely.

Do not implement Relax until Hunt Camp context is stable.

After changes:

- build using the repo's current build script against installed assemblies;
- run existing tests;
- add focused deterministic tests where practical;
- provide a manual in-game acceptance checklist;
- report exact files changed;
- report discovered classes/members and their semantics;
- report unresolved assembly questions;
- explain root causes for any failures;
- confirm unrelated changes were preserved;
- do not push.
