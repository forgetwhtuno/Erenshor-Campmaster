# CAMP / RELAX DESIGN

**Project:** Erenshor Campmaster + Deep Sims integration  
**Status:** Phase 4 explicit Relax implemented in Campmaster 0.4.0; social-expression tuning remains in Deep Sims  
**Research snapshot:** 2026-08-10  
**Primary design rule:** **Do not replace Erenshor systems. Enrich and orchestrate Erenshor systems.**

> Public GitHub and public decompiled sources are reference material only. The local working tree and the installed `Assembly-CSharp.dll` are authoritative for implementation.

---

## 1. Executive recommendation

Create a **small standalone deterministic mod tentatively named `Erenshor Campmaster`** that owns:

- recognition of an intentional hunting camp;
- explicit Relax Here / social downtime intent;
- camp/relax session state and metadata;
- compact status and diagnostics;
- a read-only compatibility API/event stream for companion mods;
- optional, later convenience calls into **verified native** Guard/Stay behavior.

Keep **Deep Sims** as the owner of:

- social relevance/admission;
- chatter frequency and cooldowns;
- relationship-aware topic selection;
- verified social memory;
- templates vs LLM expression;
- grounding.

Do **not** put the entire feature inside Deep Sims. Hunting-camp state is useful even without an LLM and should not require AI. Do **not** create a separate Relax mod: Hunt Camp and Relax share the same party/zone/anchor lifecycle and should be two intents under one deterministic downtime system.

Recommended ownership:

```text
Erenshor native systems
    combat / role manager / puller / auto pull / mana hold / guard / follow
                         |
                         v
                 Erenshor Campmaster
          deterministic observation + intent
              /                 \
             v                   v
       tiny status UI        read-only API/events
                                 |
                                 v
                             Deep Sims
                   social relevance + memory
                                 |
                         templates / LLM
```

The target feeling is:

> “I am sitting at an MMO camp with a group of persistent players.”

The system must not become:

> “I enabled a bot that plays the game for me.”

---

## 2. Research findings that shape the design

### 2.1 Native Erenshor already provides the MMO party machinery

The current Official Erenshor Wiki documents these native Role Manager roles:

- **Main Tank**
- **Main Assist**
- **Healing/Mana**
- **Crowd Control**
- **Puller**

It also documents native Auto Pull settings:

- maximum target level above the puller;
- maximum target level below the puller;
- maximum pull distance;
- **Hold if under % group mana**.

The party window already provides:

- Attack;
- Assist MA;
- Follow;
- Pull Target;
- Auto-pull Toggle;
- Guard;
- Run Away;
- Manage Roles.

Native `/group` chat already recognizes orders including:

- attack / kill / fight;
- pull / grab;
- stop pulling / hold pulls / no pulls;
- wait / guard / stay;
- follow / come;
- run / flee / escape;
- careful / cautious;
- aggressive / burn;
- mana;
- where / loc.

Therefore Campmaster must **not** implement a replacement puller, healer, tank controller, target selector, mana policy, crowd-control policy, or combat loop.

Source: <https://erenshor.wiki.gg/wiki/Simulated_Players>

### 2.2 Native SimPlayers already have some memory/social continuity

The wiki says SimPlayers can remember the player's name, past adventures, items received, and grouping history, and that prior relationship/opinion affects responses.

Campmaster must not try to rewrite or replace the native memory model.

Deep Sims may add **verified camp-specific summaries** because that is a separate social layer, but those memories should be narrow, factual, and non-duplicative.

Source: <https://erenshor.wiki.gg/wiki/Simulated_Players>

### 2.3 Guilds already exist natively

Erenshor guilds contain SimPlayers and may include the player. Guildmates can be addressed through `/guild`, SimPlayers can initiate guild quests, and guild membership/rank are native game concepts.

Relax social seeds may refer to guilds **only when the relevant guild fact is actually known from game state**.

Source: <https://erenshor.wiki.gg/wiki/Guild>

### 2.4 Deep Sims already has a narrow “camp” feature

Current public Deep Sims 0.7.0 already has:

- `/dscamp`;
- automatic sitting/meditation detection;
- a short social-camp chatter cadence;
- a central `SocialBudget`;
- verified event conversations;
- templates/LLM/Off expression modes;
- relationship and outing memory.

That old “camp” is really **resting/sitting social chatter**, not an EQ-style hunting-camp model. The new work should **supersede or migrate** that concept instead of adding a second unrelated meaning for “camp.”

In particular, **sitting must not imply Relax** once Hunt Camp exists: the player may simply be meditating between pulls.

Public repo: <https://github.com/forgetwhtuno/DeepSim-erenshor>

### 2.5 Current Deep Sims `CombatRole` is not native Manage Roles state

Current public `SimContextReader` derives `CombatRole` from class, e.g. broad class-role text such as tank/DPS/healer. It does **not** currently prove which actor is assigned Main Tank, Main Assist, Puller, Healing/Mana, or Crowd Control in the native Role Manager.

This is the highest-priority local assembly research item before role-aware camp recognition is enabled.

---

## 3. Goals

### Primary goals

1. Make an intentional hunting location feel like an old-school MMO camp.
2. Recognize and describe native party behavior instead of replacing it.
3. Give Deep Sims deterministic, grounded camp/relax context.
4. Make recovery and quiet periods socially meaningful without producing constant chatter.
5. Preserve the player's responsibility for travel, roles, pull settings, combat, loot, gear, quests, and major decisions.
6. Degrade safely when native state cannot be verified.
7. Work usefully without Ollama or Deep Sims.

### Experience goals

A typical hunting loop should feel like:

```text
travel
-> choose/arrive at camp
-> native Guard/Stay
-> native roles already assigned
-> native Puller / Pull Target / Auto Pull
-> fight
-> native recovery / mana hold
-> next pull
-> occasional grounded chatter
-> occasional verified memory-worthy moment
```

A typical relaxation loop should feel like:

```text
player explicitly chooses Relax Here
-> party remains in place using native stay behavior when available
-> no urgency
-> silence is normal
-> occasional relationship/memory/world conversation
-> combat/travel interrupts or suspends relaxation
-> player leaves
```

---

## 4. Non-goals

Campmaster must not:

- choose quests;
- navigate arbitrary routes;
- travel the world unattended;
- pick farming targets independently of native Auto Pull;
- write a second puller AI;
- choose tanks/healers/CC behavior;
- cast heals or spells;
- manage cooldowns;
- sell items;
- equip items;
- distribute loot;
- decide need/greed;
- auto-resurrect/recover from wipes;
- automatically re-enable pulling after death/escape;
- make arbitrary inventory decisions;
- automate guild quests;
- simulate remote COOP players;
- claim rare loot without verified rarity metadata;
- turn silence into repeated “nothing is happening” messages.

Campmaster may observe native automation the player explicitly enabled. It must not expand that automation boundary.

---

## 5. Proposed product split

### 5.1 `Erenshor Campmaster` — standalone

Owns deterministic state.

Suggested initial source layout:

```text
Erenshor-Campmaster/
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
    CampStatusPanel.cs
    CoopCompatibility.cs
  docs/
    CAMP_RELAX_DESIGN.md
    CAMP_RELAX_GAME_HOOKS.md
    CAMP_RELAX_LOCAL_AI_HANDOFF.md
```

Later, only if justified:

```text
  src/
    NativeGroupCommandAdapter.cs
    FollowCompatibility.cs
    DuelCompatibility.cs
```

### 5.2 Deep Sims — optional consumer

Add a small compatibility layer, e.g.:

```text
src/CampmasterCompatibility.cs
src/DowntimeSocialContext.cs
```

Responsibilities:

- detect Campmaster at runtime;
- read verified snapshot/event data;
- map those facts into existing `SocialBudget` / event-conversation machinery;
- persist only appropriate verified camp memories;
- never call Campmaster to drive combat.

### 5.3 Existing mods stay separate

**Erenshor Follow**
- remains owner of player Follow/Lead travel;
- Campmaster should observe or ask its status through an optional read-only bridge if needed;
- Campmaster should not acquire a second movement controller.

**Party Tools**
- remains small utilities (`/ready`, `/roll`, `/rollparty`);
- Campmaster may copy its conservative state-reading patterns;
- no hard dependency is needed.

**Practice Duels**
- remains friendly-duel owner;
- it should later consume a stable Campmaster state API instead of probing Deep Sims private camp fields;
- a verified completed duel can remain a Relax memory seed through Deep Sims.

---

## 6. Top-level downtime model

Use two mutually exclusive intents:

```text
None
HuntCamp
Relax
```

Do not model `HuntCamp` and `Relax` as two independent booleans.

### Why

- A hunt camp can contain sitting, recovery, silence, and conversation.
- Sitting/recovery during a hunt must not silently become Relax.
- Relax is intentional social downtime, not merely “not currently fighting.”
- Mutual exclusion simplifies compatibility with duels/travel and keeps prompts grounded.

---

## 7. Hunt Camp state machine

Separate **session lifecycle** from **current activity**.

### 7.1 Session lifecycle

```text
Inactive
  |
  | strong automatic recognition OR explicit /camp here
  v
Establishing
  |
  | stable prerequisites
  v
Active
  | \
  |  \ prerequisites temporarily ambiguous
  |   v
  | Suspended
  |   |
  |   | prerequisites recover
  |   v
  | Active
  |
  | break condition
  v
Breaking
  |
  v
Inactive
```

### 7.2 Activity state while Active

```text
Waiting
Pulling       (only if a native pull lifecycle is verifiable)
Fighting
Recovering
Unknown
```

`PullIncoming` can be a display/social alias for `Pulling` only after a reliable native signal is found.

Do not force every frame into a guessed state. `Unknown` is valid.

### 7.3 Expected transitions

- `Waiting -> Pulling`: verified native pull begins.
- `Pulling -> Fighting`: party combat becomes authoritative.
- `Fighting -> Recovering`: encounter completes and combat clears.
- `Recovering -> Waiting`: native recovery condition clears / puller is again eligible.
- `Recovering -> Pulling`: native Auto Pull starts the next pull.
- Any state -> `Breaking`: zone change, party dissolves, explicit clear, retreat/escape, or a sufficiently large/stable anchor departure.
- `Active -> Suspended`: transient zoning/party object churn where authority is temporarily incomplete.

Version 1 may collapse `Waiting`, `Pulling`, and `Recovering` to fewer labels until hooks are proven.

---

## 8. Automatic Hunt Camp recognition

### 8.1 Recommendation

**Prefer automatic recognition, but only from strong native evidence.**

The ideal strong predicate is:

```text
current local party exists
AND native Guard/Stay state is verified and stable at a common anchor
AND a native Puller assignment is verified
AND native Auto Pull is verified enabled
AND party is not currently in explicit travel mode
```

After a short stability window (suggested starting point: 5–10 seconds), enter `HuntCamp`.

This directly recognizes the player-created MMO pattern without another mandatory start command.

### 8.2 Why not infer from Guard alone?

Guard is a tactical command. A party can Guard because the player wants them to:

- wait around a corner;
- stop moving briefly;
- prepare for a boss;
- avoid aggro;
- pause while the player checks inventory.

Guard alone does not prove “we are camping here to hunt.”

### 8.3 Why not infer from sitting?

Sitting means rest/meditation and is already used by current Deep Sims. It can happen:

- during camp recovery;
- after a fight while traveling;
- while AFK;
- while reading UI;
- during explicit Relax.

It is not a sufficient intent signal.

### 8.4 Manual fallback

Keep a non-redundant context command for manual-pull camps or builds where Auto Pull state is not safely readable:

```text
/camp here
/camp clear
/camp status
/camp auto on
/camp auto off
```

`/camp here` means:

> “Treat this location/party state as my hunting camp context.”

It must **not** itself attack, select targets, toggle Auto Pull, or configure roles in version 1.

`/camp clear` clears Campmaster context. It does **not** imply native `stop pulling`; the player already has native controls for that.

### 8.5 Recognition fail-closed rule

If exact Puller or Auto Pull state is unknown:

- report it as unknown;
- do not auto-activate based on a guessed field;
- allow explicit `/camp here` as the safe fallback.

---

## 9. Camp anchor

A camp should have a stable local anchor used for context and break detection.

Preferred source:

1. native central Guard/Stay anchor if one exists;
2. otherwise a deterministic aggregate of verified local Sim `GuardSpot` positions;
3. otherwise explicit player position captured by `/camp here`.

The anchor is contextual. Campmaster must not continuously teleport, warp, or force actors back to it.

Suggested break behavior:

- small puller movement away from camp: expected;
- combat motion around camp: expected;
- player and most party members leaving the anchor area for a sustained period: break or suspend;
- zone change: always end the current camp session.

Exact distances must be tuned from live tests, not invented as game semantics. Initial implementation may use config values solely for Campmaster classification and clearly label them as mod thresholds, not native mechanics.

---

## 10. Camp metadata

All fields should be nullable/unknown when not proven.

```text
session id
zone
camp start UTC
anchor
party composition
authority quality: FullLocal | PartialCoop | Unknown
native roles:
  Main Tank
  Main Assist
  Healing/Mana actor(s)
  Crowd Control actor(s)
  Puller
native pull state:
  Auto Pull enabled?
  current pull target?
  pull in progress?
native pull settings:
  max level above
  max level below
  max distance
  mana hold threshold
session metrics:
  completed encounters
  verified pulls (only when pull lifecycle hook exists)
  deaths
  close calls
  explicit retreat events
  observed loot count
  repeated enemy counts
  elapsed duration
```

Do not equate “encounters” with “pulls” until pull starts can be verified.

---

## 11. Recovery

Recovery is a core camp texture and should be **observed**, not automated.

Current verified recovery signal, in priority order:

1. exact native per-healer pull-readiness gate is blocked while Auto Pull is on;
2. future signals may be added only if separately verified.

The installed assembly established that this is **not a group average**.
`SimPlayer.CheckPullReadiness()` checks each Sim in `SimPlayerGrouping.Heals` and
blocks if `CurrentMana < GetCurrentMaxMana() * ManaNeededForPull`. Phase 3
recomputes that same condition read-only and fails closed if a relevant healer
cannot be resolved.

Campmaster still must not make anyone sit, heal anyone, or resume native pulling
if the native system stopped itself.

---

## 12. Relax Here

### 12.1 Relax must be explicit

Recommended commands:

```text
/relax here
/relax off
/relax status
```

Optional future UI: one **Relax Here** party-level action.

Do **not** automatically enter Relax from inactivity or Guard alone.

### 12.2 Relax lifecycle

```text
Inactive
  |
  | explicit Relax Here
  v
Active
  |
  | verified combat starts
  v
SuspendedForCombat
  |
  | combat clears AND party remains at relax anchor
  v
Active
  |
  | travel / zone / explicit off / party dissolves
  v
Inactive
```

### 12.3 Native Guard integration

Version 1 may make Relax a context-only state.

A later convenience phase may have `Relax Here` issue the exact **native** Guard/Stay operation once, but only after the local AI verifies the native command path.

Do not emulate Guard by writing a custom movement loop.

### 12.4 Social behavior

Relax changes **topic weighting and willingness**, not truth.

Relax may favor:

- verified shared outing memories;
- verified prior camp summaries;
- verified prior Practice Duels;
- current relationships;
- current/known other Sims;
- recent conversation topics;
- verified guild membership/activity;
- current zone;
- cached/grounded lore;
- personality-specific preferences;
- player interests already established in conversation.

Relax should suppress repetitive non-content such as:

- “nothing is happening”;
- “I’m waiting”;
- “not much going on”;
- “we’re standing around.”

### 12.5 Silence remains normal

Relax must continue using the existing central Deep Sims social budget.

**Do not automatically force `Lively`.**

The current user-selected social activity preset remains authoritative. Relax should mainly:

- increase the relevance score of memory/relationship seeds;
- allow richer 1–3 line threads when one is admitted;
- reduce urgency/tactical topic weights;
- keep combat gating;
- keep duplicate suppression;
- keep `NO_MESSAGE` / silence as a normal outcome.

---


### 12.6 Phase-4 implementation status

Campmaster 0.4.0 implements explicit Relax as context-only deterministic
downtime. `/relax here` captures the player anchor and party, combat suspends
Relax, combat clear resumes it when the party remains at the location, and
zone/party/departure/explicit-off conditions end it. Sitting is not an intent
signal. No native Guard/Stay write is issued in this phase.

The read-only compatibility API is schema 3 and exposes `IsRelaxActive`, Relax
mode in `GetCurrentSnapshot()`, plus a separate Relax event sequence so older
Hunt Camp consumers are not broken.

---

## 13. Deep Sims integration

### 13.1 Responsibility boundary

```text
Erenshor:
WHAT HAPPENED in gameplay

Campmaster:
WHAT DOWNTIME STATE ARE WE IN?
WHAT VERIFIED CAMP/RELAX FACTS EXIST?

Deep Sims:
IS THIS SOCIALLY RELEVANT NOW?

Template / LLM:
HOW WOULD THIS SIM SAY IT?
```

### 13.2 Read-only compatibility API

Prefer a small public Campmaster compatibility surface rather than Campmaster reaching into Deep Sims internals.

Conceptual API only — local AI must choose exact signatures after inspecting current code:

```text
CampmasterApi.GetCurrentSnapshot()
CampmasterApi.GetEventsAfter(sequence)
CampmasterApi.IsHuntCampActive
CampmasterApi.IsRelaxActive
```

The snapshot/event DTO should contain only primitive/enumerable data and immutable copies where practical.

Deep Sims should runtime-detect Campmaster so there is no hard dependency.

### 13.3 Why poll/read instead of only pushing events?

Camp and Relax include continuous context:

- duration;
- current activity;
- roles;
- mana hold;
- current party;
- session counters.

An event-only bridge cannot reliably build prompt context for a player question like:

> “How has this camp been going?”

A read-only snapshot plus a small event sequence solves both use cases.

### 13.4 Event schema

Conceptual deterministic event:

```text
schemaVersion
sequence
eventId
utc
source = "ErenshorCampmaster"
sessionId
mode = HuntCamp | Relax
type
zone
partyNames[]
verifiedFields{}
```

Current Hunt Camp event types (API schema 3 retains the sequenced Hunt Camp stream):

```text
camp_started
camp_ended
camp_suspended
camp_resumed
camp_party_changed
camp_pull_started
camp_encounter_started
camp_encounter_completed
camp_recovery_started
camp_recovery_ended
camp_repeated_enemy        // exact repeated verified pull-target name; never a kill claim
camp_rough_encounter       // Campmaster duration classification, explicitly derived
camp_role_snapshot         // actual native Manage Roles assignments only
```

Phase-3 events may carry flat `verified.<key>` values in the public dictionary
representation. Examples include `verified.target`,
`verified.combatDurationSeconds`, and `verified.puller`. Unknown facts stay
absent.

Phase 4 also exposes a separate Relax sequence with:

```text
relax_started
relax_suspended
relax_resumed
relax_ended
```

Keeping Relax on its own sequence preserves existing Hunt Camp consumers. Future
events such as `camp_retreat` or `camp_loot_observed` remain deferred until their
own native facts are verified. Avoid an event named `rare_item` until item rarity
can be verified from a stable game source.

### 13.5 Deep Sims social seed mapping

Campmaster event/context -> Deep Sims seed examples:

```text
camp_recovery_started -> camp_recovery / mana_hold
camp_rough_encounter  -> rough_encounter (duration-derived only)
camp_repeated_enemy   -> repeated_enemy (exact pull-target repetition only)
camp_role_snapshot    -> role_comment
camp_pull_started     -> pull_incoming, when socially relevant
completed camp memory -> camp_memory (future Deep Sims consumer policy)
```

Do not translate `camp_repeated_enemy` into a repeated-kill claim, and do not
translate `camp_rough_encounter` into deaths, wipes, or close calls that the
Campmaster payload did not verify.

Relax:

```text
shared outing memory  -> shared_memory
completed camp        -> past_camp
verified duel         -> past_duel
pair relationship     -> relationship
recent topic summary  -> recent_topic
known guild facts     -> guild
known current zone    -> current_zone
grounded reference    -> world_lore
```

Deep Sims decides if a seed deserves speech. Campmaster does not.

### 13.6 Prompt context

Only verified fields enter the “game fact” portion.

Example:

```text
VERIFIED DOWNTIME CONTEXT
mode=hunt_camp
state=recovering
zone=Azure
elapsed_minutes=27
puller=Phanty
main_tank=Baetil
native_auto_pull=true
native_mana_hold_threshold=45
completed_encounters=14
recent_completed_encounter=1 close call, no deaths
```

Unknown facts should be omitted or explicitly marked unknown. Never guess a role from class.

---

## 14. Integrating with current Deep Sims 0.7

### Reuse

Reuse:

- `SocialBudget`;
- `EventConversationDirector`;
- templates/LLM expression routing;
- existing verified `SessionTelemetry`;
- `MemoryStore`;
- relationship model;
- outing summaries;
- completed encounter snapshots;
- host-authority rules;
- duplicate suppression.

### Change

The current sitting-based `_campActive` concept should be renamed/migrated so it no longer conflicts with Hunt Camp.

Recommended compatibility behavior:

- **Campmaster absent:** preserve current `/dscamp` behavior for backwards compatibility.
- **Campmaster present:** disable automatic sitting=>social-camp inference. Hunt Camp/Relax context comes from Campmaster.
- Consider deprecating `/dscamp` in favor of `/relax` for explicit social downtime.
- Do not make sitting during Hunt Camp switch the mode to Relax.

### Do not double-count relationships

A completed 30-minute camp is already 30 minutes of shared outing time.

If Deep Sims stores a camp summary, it must **not** also add a second independent 30 minutes of familiarity progress.

Camp memory is narrative indexing, not extra relationship XP.

---

## 15. Camp memory policy

### 15.1 Ownership

Campmaster:
- session-local state only;
- emits a factual completed-session summary;
- no long-term social memory by default.

Deep Sims:
- decides whether to persist a camp memory for participating Deep Sims.

### 15.2 Meaningful-camp floor

Do not persist every brief stop.

A candidate completed camp can become a durable Deep Sims memory when one or more are true:

- at least ~5 minutes **and** multiple completed encounters;
- a death/close-call/retreat occurred;
- a verified notable loot event occurred;
- a verified duel/other meaningful social event occurred during Relax;
- the camp was unusually long.

Exact floors are mod policy, not Erenshor mechanics, and should be configurable only if needed.

### 15.3 Bounded storage

Prefer:

- recent event list;
- at most a small number of compact camp summaries per Sim;
- existing outing summaries remain the broad history.

Example safe summary:

```text
Camped in Azure for about 27 minutes with Phanty and Baetil.
Completed 14 encounters; one had a close call. No party deaths were recorded.
```

Unsafe additions unless separately verified:

```text
We farmed efficiently.
That was a rare drop.
Phanty saved the group.
Baetil was the best tank.
Everyone had fun.
```

Those are interpretation or unsupported causality.

---

## 16. UI and commands

### Version 1 UI

Use either:

- `/camp status` output; or
- one tiny temporary/compact status panel.

Suggested compact display:

```text
CAMP — Azure — 27m
RECOVERING
14 encounters
Puller: Phanty
Auto Pull: ON
Mana hold: 45%
```

Only render rows that are known.

Relax:

```text
RELAX — Azure — 8m
Social downtime
```

No permanent giant control center.

### Do not duplicate native controls

Do not add Campmaster buttons for:

- Main Tank;
- Main Assist;
- Puller;
- Auto Pull configuration;
- attack;
- follow;
- run away;
- heal/CC.

Those are already native.

A future status UI may show these values and tell the user where to change them natively.

---

## 17. Failure behavior

Fail closed.

If a hook/state is unavailable:

- `Puller: unknown`, not guessed;
- `Auto Pull: unknown`, not inferred from movement;
- omit mana threshold;
- collapse activity to `Unknown`/`Fighting` as appropriate;
- do not issue replacement AI.

On exceptions:

- leave native Erenshor untouched;
- disable only the affected Campmaster reader/feature;
- log one bounded diagnostic rather than spam;
- preserve direct player control.

On zone change:

- finalize/end current Camp/Relax session;
- clear object references;
- require new recognition or explicit intent in the new zone.

On party disappearance/transient load:

- use a short suspension/grace window before finalizing if needed;
- never hold destroyed Unity object references.

---

## 18. Co-op policy

Version 1 is **observation-first and host-conservative**.

Rules:

- local player and verified local Sim state may be used normally;
- remote humans must never be treated as `SimPlayer` automation targets;
- remote/network-owned Sims must not receive synthetic Campmaster movement commands;
- unresolved members may appear in metadata as `UNAVAILABLE`/partial authority;
- automatic camp recognition should fail closed if its required role/guard facts depend on unknown remote state;
- manual `/camp here` may establish local social context, but must not imply authoritative remote state;
- Deep Sims autonomous output remains host-authority only under its existing rules;
- do not invent a new COOP synchronization protocol.

Later native command orchestration should be disabled in COOP until host/client command ownership is verified.

---

## 19. Compatibility with Practice Duels

Current Practice Duels 0.3.x treats sitting/current Deep Sims camp state as a reason not to start or continue a duel.

That is too ambiguous for the new model.

Desired future rule:

- `Relax` active: challenge may either end Relax explicitly or be refused until Relax is exited; choose one deterministic UX.
- `HuntCamp` active: duel should normally be blocked while native pulling/camp activity is active.
- Campmaster exposes a stable read-only `IsDuelUnsafe/IsDowntimeActive` style fact, or Practice Duels checks explicit `HuntCamp/Relax` status.
- Do not make Practice Duels reflect into Deep Sims private `_director` state.

A verified completed duel remains a Deep Sims memory/social seed.

---

## 20. Compatibility with Erenshor Follow

Travel and camp are opposing intents.

Suggested read-only interaction:

```text
Follow/Lead active -> do not auto-recognize Hunt Camp
starting Follow/Lead -> end Relax; normally break Hunt Camp
verified travel arrival -> eligible for a later camp establishment
```

Campmaster does not stop Follow itself in v1.

Later, if Follow formalizes a public status/lifecycle API, consume it by runtime detection.

---

## 21. Test matrix

### Native-state observation

| Case | Expected |
|---|---|
| solo, no Sims | no auto Hunt Camp |
| party follows player | no Hunt Camp |
| party Guards, no Puller/Auto Pull | no auto Hunt Camp |
| Guard + verified Puller + Auto Pull | Hunt Camp candidate -> active after stability window |
| Auto Pull unknown | no automatic activation |
| `/camp here` with valid party | explicit Hunt Camp context |
| zone change | finalize/end camp |
| party membership reshuffle | update safely; no stale refs |

### Combat loop

| Case | Expected |
|---|---|
| native pull begins | Pulling only if hook verified |
| real party combat | Fighting |
| combat clears | Recovering/Waiting based verified state |
| mana hold below threshold | Recovery fact only if native formula verified |
| repeated verified pull-target names | seed after deterministic per-target pull threshold; never implies a kill |
| death | recorded factual danger event |
| Run Away | retreat only if native command/event observed |
| unrelated nearby NPC combat | must not count as camp encounter |

### Relax

| Case | Expected |
|---|---|
| ordinary inactivity | no Relax |
| Guard only | no Relax |
| `/relax here` | Relax active |
| player sits | remains current mode; sitting alone does not switch |
| combat starts | Relax suspended; ambient chatter stops |
| combat clears at anchor | Relax resumes |
| travel/zone | Relax ends |

### Deep Sims

| Case | Expected |
|---|---|
| Campmaster absent | Deep Sims works normally |
| Ollama down, Auto expression | templates/silence continue |
| Templates mode | no LLM request |
| Off mode | no autonomous chatter |
| Hunt Camp active | prompt receives verified context |
| Relax active | memory/relationship seed weights change |
| unsupported “rare drop” | no rare claim |
| unknown role | no role claim |
| player recently spoke | social budget still suppresses autonomous chatter |
| Lively preset | bounded by existing budget, not unlimited |
| completed camp | at most one bounded factual summary per participating Sim |
| outing relationship time | not double-counted by camp memory |

### Co-op

| Case | Expected |
|---|---|
| host + local Sims | read-only camp state works |
| remote human in party | not treated as Sim |
| remote-owned Sim unresolved | partial/unknown; no synthetic action |
| client without Campmaster | game continues normally |
| Deep Sims host authority | only host generates autonomous Deep Sim output |

---

## 22. Phased roadmap

### Phase 0 — local discovery / proof

Before feature code:

- inspect current local repo state;
- inspect current installed `Assembly-CSharp.dll`;
- identify exact native Role Manager storage;
- identify exact Auto Pull toggle/state;
- identify native pull settings;
- identify Guard/Stay central/per-Sim semantics;
- identify mana fields and trace the native pull-readiness mana formula;
- identify pull-start/pull-end state if exposed;
- document findings.

No gameplay changes.

### Phase 1 — Camp recognition and context only

Implement standalone Campmaster:

- current party reader;
- zone/anchor;
- native role/pull-state reader **only for verified fields**;
- automatic recognition only if strong predicate is proven;
- `/camp here`, `/camp clear`, `/camp status`;
- basic in-memory session;
- no Deep Sims dependency;
- no native command writes.

This is the best minimal first version.

### Phase 2 — session metrics + Deep Sims bridge

Add:

- read-only snapshot API;
- event sequence;
- completed encounter/session counters using verified sources;
- Deep Sims compatibility reader;
- Camp context in prompts;
- camp event seed mapping through existing social budget;
- no new social scheduler.

### Phase 3 — richer recovery/pull semantics — IMPLEMENTED (0.3.0)

Implemented after the installed-assembly verification recorded in
`CAMP_PHASE1_ASSEMBLY_FINDINGS.md`:

- **Pulling / Pull Incoming:** the assigned Sim Puller's verified
  `SimPlayer.CurrentPullPhase != NotPulling`; player-held Puller remains unknown
  because that Sim-only lifecycle field does not apply.
- **Native mana-hold state:** recomputes the exact `CheckPullReadiness`
  per-healer condition over `SimPlayerGrouping.Heals`; it is not a group
  average. `Recovering` is used only while out of combat, Auto Pull is on, and
  that verified gate is blocked.
- **Repeated-enemy seed:** counts distinct verified pull starts by exact native
  `PullTarget` name and emits one bounded seed at a configurable threshold. It
  does not infer a family and does not claim the target died.
- **Rough-encounter seed:** derives a bounded seed from verified combat duration
  crossing a configurable Campmaster threshold. The payload labels the result
  `CampmasterDerivedLongCombat`; it does not fabricate deaths/close calls.
- **Role-comment seed:** emits a `camp_role_snapshot` only from actual native
  Manage Roles assignments already read from `SimPlayerGrouping`; unknown roles
  remain absent rather than class-guessed.
- **Compact richer status:** shows PULL INCOMING / RECOVERING, current pull
  target when known, native healer mana gate, verified pulls, and seed counters.
- **Read-only API schema 2:** additive Phase-3 snapshot fields/events with
  `verified.<key>` payloads for optional consumers such as Deep Sims.

Still read-only. Campmaster does not itself generate social dialogue; Deep Sims
remains the owner of admission, templates/LLM expression, memory, and grounding.

### Phase 4 — Relax Here

Add explicit Relax:

- `/relax here|off|status`;
- shared downtime lifecycle;
- Deep Sims Relax seed weighting;
- migrate/disable old sitting=>camp inference when Campmaster is present;
- optional one-shot native Guard call only if exact native path is verified.

### Phase 5 — memory and polish

Add:

- bounded camp summaries;
- past-camp Relax seed;
- duel/camp/reunion social cross-links;
- optional Follow/Duel formal compatibility;
- UI polish;
- COOP verification.

### Phase 6 — optional convenience orchestration

Only if still desired:

- explicit player-triggered wrappers around native Guard/Stay;
- possibly status shortcuts to native role/pull UI;
- never a replacement combat/pull loop.

---

## 23. Version 1 acceptance definition

Version 1 is successful if all of the following are true:

1. Player can create a normal native Erenshor camp using native roles/Guard/Auto Pull.
2. Campmaster can reliably say “this looks like a Hunt Camp” without controlling the fight.
3. `/camp status` reports only verified facts.
4. Explicit `/camp here` works as a safe fallback.
5. Nothing changes about native combat when Campmaster is disabled/uninstalled.
6. Unknown internal fields do not become guesses.
7. No LLM is required.
8. No loot/inventory/travel automation is added.
9. The design leaves a clean read-only bridge for Deep Sims Phase 2.

That is enough to establish the architecture without overbuilding.
