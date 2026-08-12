# CAMP / RELAX GAME HOOKS

**Purpose:** Public-source research matrix and local `Assembly-CSharp.dll` inspection checklist.  
**Status:** Draft reference — all implementation hooks must be re-verified locally.  
**Research snapshot:** 2026-08-10.

## Confidence labels

- **KNOWN FROM CURRENT PUBLIC AUTHOR SOURCE** — directly referenced by the current public `forgetwhtuno` repositories.
- **KNOWN FROM OFFICIAL WIKI** — behavior is documented, but the backing class/member is not necessarily public.
- **KNOWN FROM PUBLIC THIRD-PARTY MOD SOURCE** — appears in public decompiled/mod source and may target a different game build.
- **LIKELY / INFERRED** — architecture suggests it exists, but do not code against a guessed member.
- **MUST VERIFY IN ASSEMBLY-CSHARP** — exact class/member/semantics must be confirmed locally before editing.

Never invent signatures.

---

## 1. Official native behavior

Source: <https://erenshor.wiki.gg/wiki/Simulated_Players>

| Native concept | Publicly documented behavior | Implementation status |
|---|---|---|
| Main Tank | One actor; tries to hold aggro via taunts/damage | behavior known; storage/hook MUST VERIFY |
| Main Assist | One actor; Sims focus its selected target | behavior known; storage/hook MUST VERIFY |
| Healing/Mana | healer focus; Duelist mana recovery when low | behavior known; storage/hook MUST VERIFY |
| Crowd Control | focuses stun/root/etc.; player CC assignment changes Sim behavior toward CC targets | behavior known; storage/hook MUST VERIFY |
| Puller | One actor; performs Pull Target / Auto Pull | behavior known; storage/hook MUST VERIFY |
| Pull Target | Puller pulls selected target; waits for mana threshold; ignores auto level limits | behavior known; exact method MUST VERIFY |
| Auto Pull | Puller automatically selects/pulls targets | behavior known; enabled-state member MUST VERIFY |
| Max pull level +/- | Relative to puller level | behavior known; fields MUST VERIFY |
| Max pull distance | Native pull search radius | behavior known; field MUST VERIFY |
| Hold if under % group mana | UI wording suggests group mana; installed assembly actually checks each Sim in `Heals` independently | field/formula VERIFIED locally; see Phase-1 findings section 5 |
| Guard | Party stays where they are | behavior known; command path MUST VERIFY |
| Follow | Party resumes following | behavior known; command path MUST VERIFY |
| Run Away | Party runs to zone border | behavior known; command path MUST VERIFY |
| `/group` order parser | keywords map to native group commands | behavior known; parser/method MUST VERIFY |

---

## 2. Current public `forgetwhtuno` source

### 2.1 Party identity and membership

**KNOWN FROM CURRENT PUBLIC AUTHOR SOURCE**

Current `Erenshor-PartyTools/src/PartyStateReader.cs` uses:

```text
GameData.GroupMembers
SimPlayerTracking
SimPlayerTracking.SimName
SimPlayerTracking.MyAvatar
SimPlayer.InGroup
GameData.SimPlayerGrouping
GameData.SimPlayerGrouping.IsSimInPlayerGroup(sim)
```

Observed interpretation:

- `GameData.GroupMembers` is used as the authoritative current party tracking list.
- A tracking entry may exist without a trustworthy local `SimPlayer` avatar, especially around COOP/zoning.
- local Sim membership is confirmed with both `sim.InGroup` and `IsSimInPlayerGroup`.

**Local questions**

1. Is `GameData.GroupMembers` still the best current party source?
2. Can it contain remote humans/network-owned Sims in the local build?
3. What is the exact lifecycle during invite, dismiss, zoning, death, and respawn?
4. Does a raid use a separate structure?
5. Is the array length fixed or dynamic?

---

### 2.2 Basic combat state

**KNOWN FROM CURRENT PUBLIC AUTHOR SOURCE**

Used in Party Tools, Follow, Duel, and/or Deep Sims:

```text
GameData.InCombat
SimPlayer.IsSimGroupInCombat()
Character.Alive
NPC.CurrentAggroTarget
Stats.CurrentHP
Stats.CurrentMaxHP
Stats.ReduceHP(...)
```

**Important limitation already established by Deep Sims**

`Stats.ReduceHP` identifies the victim but does not by itself prove the attacker. Current Deep Sims deliberately refuses to turn nearby NPC damage into an attributed party kill.

**Local questions**

1. Is there a better attacker/source event in the current assembly?
2. Can party combat be distinguished from unrelated nearby combat without log parsing?
3. Is there a native encounter/pull object that already owns the lifecycle?
4. Are `GameData.InCombat` and `IsSimGroupInCombat` authoritative during pull travel before first damage?

---

### 2.3 Guard / follow state

**KNOWN FROM CURRENT PUBLIC AUTHOR SOURCE**

Current Follow and Practice Duel source uses:

```text
SimPlayer.GuardSpot
SimPlayer.GetGuardPos()
SimPlayer.AssignGuardSpot(Vector3)
SimPlayer.FreeFollow()
NPC.HighPriorityNavUpdate(Vector3)
```

`ErenshorFollow.LeaderController` snapshots a Sim's existing Guard state and restores it after travel.

**Do not conclude** that `AssignGuardSpot` is the native party-button implementation. It is a usable public member in the current author source, but the local AI must inspect the actual Shift+6 / `/group wait|guard|stay` path.

**Local questions**

1. What method does the native Guard button call?
2. Is there a central party Guard boolean/state?
3. Does Guard simply call `AssignGuardSpot` for every Sim?
4. How is the guard position chosen?
5. What exactly clears Guard when Follow is selected?
6. What happens to the designated Puller while the rest of the party Guards?
7. Is Guard state preserved across combat?
8. Can a common camp anchor be read without writing any Sim state?

---

### 2.4 Player sitting / meditation

**KNOWN FROM CURRENT PUBLIC AUTHOR SOURCE**

Current Deep Sims and Practice Duels read:

```text
GameData.PlayerControl.Sitting
```

Current Deep Sims uses it for legacy automatic social-camp detection.

**Design implication**

Sitting is a reliable narrow fact (“the player is sitting”) but not a reliable intent classifier for Hunt Camp or Relax.

**Local questions**

1. Is `Sitting` set for all meditation/rest variants?
2. Does it remain true during UI states?
3. Does damage/combat automatically clear it?
4. Are Sim sitting/rest states separately readable?

---

### 2.5 Player targeting / click path

**KNOWN FROM CURRENT PUBLIC AUTHOR SOURCE**

Current Follow action menu patches:

```text
PlayerControl.LeftClick
Character.TargetMe
GameData.PlayerControl.CurrentTarget
```

This is useful only if Campmaster later gets a target-based/context UI. It is not needed for Phase 1 camp recognition.

---

### 2.6 Slash command interception

**KNOWN FROM CURRENT PUBLIC AUTHOR SOURCE**

Party Tools uses the narrow existing pattern around:

```text
TypeText.CheckCommands
```

Campmaster may reuse this pattern for `/camp` and `/relax` while passing all unrelated commands through untouched.

**Local questions**

1. Confirm the current overload/signature.
2. Check interaction order with Deep Sims, Follow, Duel, Party Tools, and other command patches.
3. Ensure commands do not double-fire when multiple mods patch the same method.

---

## 3. Deep Sims hooks and reusable structures

Public repo: <https://github.com/forgetwhtuno/DeepSim-erenshor>

### 3.1 `SocialBudget`

**KNOWN FROM CURRENT PUBLIC AUTHOR SOURCE**

Current 0.7 has one central deterministic social budget with:

- global cooldown;
- per-Sim cooldown;
- per-event-type cooldown;
- rolling 10-minute message budget;
- recent semantic/message duplication suppression;
- player-speech quiet window;
- conversation-thread ownership;
- combat gating;
- priority arbitration.

**Implementation rule**

Camp/Relax integration must feed this existing budget. Do not create a second autonomous-chat scheduler.

---

### 3.2 `SocialEventCandidate` / `EventConversationDirector`

**KNOWN FROM CURRENT PUBLIC AUTHOR SOURCE**

Current event candidates include:

```text
Type
ObservedUtc
InvolvedNames
EligibleSpeakerNames
VerifiedEntities
Trust
Importance
Novelty
CooldownCategory
VerifiedContext
BaseChance
```

This is a strong internal model for mapping verified Campmaster events to social opportunities.

**Do not make Campmaster reference this type directly.** It is internal Deep Sims implementation detail.

---

### 3.3 `SessionTelemetry`

**KNOWN FROM CURRENT PUBLIC AUTHOR SOURCE**

Current Deep Sims already tracks, conservatively:

- party outing time;
- zone history;
- attributed kills;
- loot;
- observed kill->loot proximity;
- deaths;
- close calls;
- current/recent completed encounters;
- encounter quiet finalization;
- party participants;
- shared party time.

This can supply social memory after a Campmaster session, but Campmaster should own its own lightweight session identity/counters so it remains useful standalone.

**Key safety rule already in current code**

Do not treat a damaged nearby NPC as a party kill without attribution.

---

### 3.4 `MemoryStore`

**KNOWN FROM CURRENT PUBLIC AUTHOR SOURCE**

Current Deep Sims supports:

- `MemoryEvent`;
- `ImportantMemories`;
- `OutingSummaries`;
- completed outing counters;
- total grouped minutes;
- Sim-to-Sim relationship records;
- verified Practice Duel count;
- conversation topic summaries.

**Camp memory recommendation**

Use bounded camp-specific event/summary data or a small new list. Do not increment shared time a second time if the same period is already outing time.

---

### 3.5 Current `CombatRole` limitation

**KNOWN FROM CURRENT PUBLIC AUTHOR SOURCE**

`SimContextReader` currently computes broad `CombatRole` from class through `DescribeClassRole(...)`.

It does **not** establish native Manage Roles assignments.

The current Deep Sims README explicitly identifies exact Manage Roles discovery as future correctness work.

**Must fix/extend separately before role-aware camp speech.**

---

### 3.6 Current guild reader

**KNOWN FROM CURRENT PUBLIC AUTHOR SOURCE, BUT REFLECTION-BASED**

`SimContextReader` currently probes:

```text
SimPlayerMngr
manager.Sims
tracking.GuildID / GuildId / Guild
GameData.GuildManager / GuildMngr
GuildManager
guildManager.Guilds
guild.GuildMembers
guild.GuildName / Name
```

Because this code intentionally tries alternative reflective member names, individual names are **not proof** of a stable assembly contract.

Local AI must verify current guild storage before expanding social claims.

---

## 4. Practice Duel integration pattern

Public repo: <https://github.com/forgetwhtuno/Erenshor-Duel>

### 4.1 Verified event bridge

**KNOWN FROM CURRENT PUBLIC AUTHOR SOURCE**

Practice Duels currently runtime-detects Deep Sims and reflects into:

```text
ErenshorDeepSims.DeepSimsPlugin
Instance
NotifyObservedGameEvent(...)
```

It emits challenge/decline/accept/start/completed/cancelled lifecycle facts, with completed duel events treated as meaningful verified memory.

This demonstrates a working optional-mod pattern, but it is coupled to a private/internal Deep Sims surface.

**Campmaster recommendation**

Prefer a small explicit public read-only API rather than deeper reflection into private fields.

### 4.2 Current camp check is legacy/fragile

Practice Duels currently:

- treats `GameData.PlayerControl.Sitting` as camp-active; and
- can reflect into Deep Sims `_director.DescribeCamp()`.

This must not become the long-term Campmaster contract.

**Local follow-up**

After Campmaster exists, replace this with an explicit stable Campmaster status check while retaining safe fallback behavior where appropriate.

---

## 5. Public third-party source clues

These are useful clues, **not current assembly contracts**.

### 5.1 Erenshor COOP decompiled source

Public source:
<https://thunderstore.io/c/erenshor/p/mizuki/Erenshor_COOP/source/>

The public decompiled mod has patched or referenced:

```text
SimPlayerGrouping.InviteToGroup
SimPlayerGrouping.DismissMember1
SimPlayerGrouping.DismissMember2
SimPlayerGrouping.DismissMember3
SimPlayerGrouping.DismissMember4
SimPlayerTracking.SpawnMeInGame
SimPlayerMngr.BringPlayerGroupToZone
Stats.ReduceHP
GameData.AddExperience
TypeText.CheckInput
```

A public snippet also references:

```text
GameData.SimPlayerGrouping.GroupTargets.Clear()
GameData.SimPlayerGrouping.isPulling = false
GameData.AttackingPlayer.Clear()
```

**Critical caution**

Do **not** assume `isPulling` means “Auto Pull toggle is enabled.” It could mean:

- a pull is currently in progress;
- the pull subsystem is active;
- a transient internal state;
- an older-build implementation.

The local AI must trace reads/writes/callers in the current assembly.

### 5.2 PartyMod decompiled source

Public source:
<https://thunderstore.io/c/erenshor/p/Recks/PartyMod/source/>

Useful only as additional evidence that `SimPlayerGrouping` and native party tracking are central. It may target a different build and should not define the new mod's contract.

---

## 6. Highest-priority Assembly-CSharp questions

### A. Native Role Manager — MUST ANSWER

1. What MonoBehaviour/class backs the Role Manager UI?
2. Where are Main Tank, Main Assist, Healing/Mana, Crowd Control, and Puller assignments stored?
3. Are assignments actor references, Sim indexes, party slots, names, or enums?
4. How is the **player** represented when assigned a role?
5. Which roles permit multiple actors?
6. Do assignments survive zone changes?
7. Are there public/internal getters already used by Sim AI?
8. What method does each Role Manager button/toggle call?
9. Is there one authoritative role object that Campmaster can read without reflection heuristics?

### B. Auto Pull — MUST ANSWER

1. Where is the **enabled toggle** stored?
2. What does `SimPlayerGrouping.isPulling` mean in the current build?
3. What are all read/write call sites for `isPulling`?
4. What method does Shift+5 call?
5. What method or branch does `/group stop pulling` change?
6. Is a pull-in-progress state separate from auto-pull-enabled state?
7. Is current pull target exposed?
8. What happens to auto-pull after death, Run Away, zoning, Guard, Follow, or party change?

### C. Pull settings — MUST ANSWER

Find exact storage/type/default for:

- max level above;
- max level below;
- max pull distance;
- hold-if-under group mana %.

Then answer:

1. Is pull distance world-space meters/Unity units?
2. Does manual Pull Target bypass only level limits or other filters too?
3. Which actor list is included in average group mana?
4. How are non-mana actors treated?
5. Does the player count?
6. Does dead/unavailable/remote party state count?
7. Is the threshold compared as integer percent or normalized float?
8. Is there an explicit “holding for mana” state or only a condition in the pull loop?

### D. Guard/Stay — MUST ANSWER

1. What exact method does Shift+6 invoke?
2. What exact method does `/group wait|guard|stay` invoke?
3. Does it set `GuardSpot` for all local Sims?
4. Is there a group-level Guard state?
5. Does the Puller retain/ignore Guard when pulling?
6. How is the return-to-camp position represented?
7. Does Follow clear the same state?
8. Can Campmaster read a stable common anchor without issuing any navigation call?

### E. Pull lifecycle — SHOULD ANSWER

1. Is there a method that begins a native pull?
2. Is there a method/event when the puller chooses a target?
3. Is there a target list (`GroupTargets`) whose semantics are safe to observe?
4. When does the game decide the pull reached camp?
5. Can a pull be distinguished from generic aggro/combat?
6. Can Campmaster count pulls without Harmony-patching high-frequency movement code?

If no reliable lifecycle exists, version 1 should count **completed encounters**, not pulls.

### F. Mana — MUST ANSWER before low-mana social facts

1. Exact `Stats` members for current/max mana.
2. Types and range.
3. Which classes have zero/unused mana values.
4. Native group-average formula.
5. Whether mana fields update safely while objects are loading/zoning.

### G. Retreat / Run Away — OPTIONAL

1. Exact command method.
2. Whether an explicit state/event can prove the party ran away.
3. Do not infer retreat merely from a zone change.

### H. COOP — MUST ANSWER before any writable orchestration

1. Which machine is authoritative for role/pull/group commands?
2. How are networked Sims distinguished from local native Sims?
3. Are Role Manager and Auto Pull states replicated?
4. Can host-issued native Guard affect remote-owned Sims safely?
5. If uncertain, keep Campmaster write paths disabled in COOP.

---

## 7. Suggested hook strategy order

Prefer the least invasive source of truth.

```text
1. stable direct read of authoritative native state
2. existing native lifecycle method postfix/prefix
3. stable visible/log event already produced by Erenshor
4. low-frequency deterministic polling
5. reflection fallback for compatibility
6. heuristic inference only when explicitly labeled as Campmaster-derived
```

Avoid:

- frame-by-frame scene scans if a manager roster exists;
- patching combat methods to drive gameplay;
- writing private fields;
- interpreting names/strings as roles when object identity exists;
- copying native AI logic into Campmaster.

---

## 8. Proposed Campmaster read model

All fields nullable when unknown.

```text
PartySnapshot
  Zone
  Members[]
  AuthorityQuality

RoleSnapshot
  MainTank
  MainAssist
  HealingMana[]
  CrowdControl[]
  Puller

NativePullSnapshot
  AutoPullEnabled?
  PullInProgress?
  CurrentPullTarget?
  MaxLevelAbove?
  MaxLevelBelow?
  MaxDistance?
  HoldManaPercent?
  HoldingForMana?

AnchorSnapshot
  GuardActive?
  Anchor?
  GuardedMembers[]
  StableSeconds
```

The first local implementation task is to fill these from verified native sources. The state machine comes afterward.

---

## 9. Public-source acceptance checklist

Before claiming a hook as “verified” in code comments/docs:

- [ ] local AI located the member in the installed `Assembly-CSharp.dll`;
- [ ] local AI found its read/write callers;
- [ ] semantics match the current wiki behavior;
- [ ] live test confirms at least one transition;
- [ ] zoning/party churn does not leave stale state;
- [ ] COOP behavior is understood or fails closed;
- [ ] no unrelated current agent changes were overwritten.

If any box is missing, retain the field as `unknown` or keep the feature out of the phase.
---

## Phase 3 hook status — implemented in 0.3.0

The following formerly deferred read paths are now implemented from the local
assembly findings, still observation-only:

| Phase-3 concept | Read basis | Fail-closed behavior |
|---|---|---|
| Pulling / Pull Incoming | assigned Sim Puller + `SimPlayer.CurrentPullPhase != NotPulling` | player-held/unreadable puller -> unknown |
| Current pull target | assigned Sim Puller's `SimPlayer.PullTarget` | missing/unreadable target -> omitted |
| Native mana hold | exact `Heals` per-Sim `CurrentMana < GetCurrentMaxMana()*ManaNeededForPull` gate | unreadable relevant healer prevents a confident clear result |
| Role-comment seed | actual `SimPlayerGrouping` Manage Roles assignments | unknown assignments omitted; no class guess |
| Repeated-enemy seed | repeated exact verified pull-target name across distinct pull starts | no family inference and no kill claim |
| Rough encounter seed | verified `GameData.InCombat` duration + configurable Campmaster threshold | explicitly derived classification, not native difficulty state |

The pull lifecycle/target members are read via cached reflection so member
visibility drift fails closed instead of creating a compile-time dependency on
the nested pull enum. The reflection names are not speculative: they are the
members documented in the installed-assembly findings.

