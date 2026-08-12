# CAMPMASTER PHASE 1 — ASSEMBLY FINDINGS

**Source of truth:** the installed `Assembly-CSharp.dll`
(`<SteamLibrary>\steamapps\common\Erenshor\Erenshor_Data\Managed\Assembly-CSharp.dll`,
SHA256 `B840CB8076ED0553F7DC3BEB4042ABA653917882F763181EC0D2C13C26C17847`).

**Method:** reflection member enumeration plus IL disassembly of every relevant
method, and a whole-assembly reference scan for each field to establish its
read/write call graph. Public GitHub and public decompiled mods were used only
to decide what to look for — never as the contract.

**Verification status legend**

- **VERIFIED (call graph)** — member located, its readers and writers
  enumerated, semantics derived from the IL.
- **NEEDS LIVE TEST** — call-graph semantics are clear but the transition has
  not yet been observed in a running game.

---

## 1. Role Manager storage — VERIFIED (call graph)

The Manage Roles UI is **`GroupTasks`** (a `MonoBehaviour` with `RolesScreen`,
`PullingScreen`, `HealingScreen`, `DPSScreen` and per-slot `Toggle`s). It does
not hold role state itself: its `AssignMT` / `AssignMA` / `AssignCC` /
`AssignPULL` / `AssignHEAL` handlers write directly to the singleton
`GameData.SimPlayerGrouping`.

Authoritative storage on **`SimPlayerGrouping`** (`GameData.SimPlayerGrouping`):

| Role | Field | Type | Player representation |
|---|---|---|---|
| Main Tank | `MainTank` | `SimPlayerTracking` | `MainTank == null` **and** `PlayerIsTank == true` |
| Main Assist (live) | `MainAssist` | `SimPlayerTracking` | `PlayerIsMA` |
| Main Assist (designated) | `DesignatedMA` | `SimPlayerTracking` | `PlayerIsDesignatedMA` |
| Puller | `Puller` | `SimPlayerTracking` | `Puller == null` **and** `PlayerIsPuller == true` |
| Crowd Control | `CC` | `List<SimPlayerTracking>` | `PlayerIsCC` |
| Healing/Mana | `Heals` | `List<SimPlayerTracking>` | (player is not added to `Heals`) |

Supporting capability buckets, all `List<SimPlayerTracking>`: `Tanks`, `DPS`,
`CC`, `Heals`, `Mana`.

Key semantics:

- Assignments are **object references into `GameData.GroupMembers`**, not
  names, indexes or enums. `AssignPULL` does
  `SimPlayerGrouping.Puller = GameData.GroupMembers[n]` and sets
  `PlayerIsPuller = false`; the "player" toggle does the inverse
  (`Puller = null; PlayerIsPuller = true`).
- Main Tank / Main Assist / Puller are **single-holder**. Crowd Control and
  Healing/Mana are **multi-holder lists**.
- `SetRoles()` is the automatic default assignment and is **class-derived**
  (Paladin/Reaver ⇒ tank+puller candidates, Druid/Paladin ⇒ `Heals`,
  Arcanist ⇒ `CC`, etc.). It is called by `InviteToGroup`,
  `ForceDismissFromGroup`, `ZoneAnnounce::Start` and `DemonstrationMngr::LoadGroups`
  — so **role assignments are re-derived on zone entry** and on party changes.
  Campmaster therefore re-reads roles every poll and never caches them.
- `MainAssist` is **volatile**: `NPC::SetBackupMA`,
  `NPC::ReplaceMainAssistTemporarily` and `NPC::TryResetToDesignatedMA` rewrite
  it during combat. `DesignatedMA` is the player's stated choice. Campmaster
  reports both and never treats a temporary MA as the player's assignment.

Campmaster reads these directly. **No reflection and no class-based guessing.**

---

## 2. `SimPlayerGrouping.isPulling` — VERIFIED (call graph)

Complete call graph in the installed build:

```text
WRITE  SimPlayerGrouping::GroupPull   -> isPulling = true   (unconditionally, at the end)
WRITE  SimPlayerGrouping::HoldPulls   -> isPulling = false
WRITE  SimPlayerGrouping::RunAway     -> isPulling = false
WRITE  SceneChange::ChangeScene       -> isPulling = false
READ   SimPlayerGrouping::Update      -> gamepad D-pad-left toggle only
```

The single read is:

```text
if (!isPulling) GroupPull(true); else HoldPulls(true);
```

**Conclusion: `isPulling` is NOT the Auto Pull toggle.** It is a latch meaning
"the group has been told to pull", used to make one gamepad button alternate
between pull and hold. It is set true even by a one-shot manual Pull Target —
and `IndivPull()` explicitly sets `PullConstant = false` in the same call, so
`isPulling == true` and Auto Pull off co-exist routinely.

Campmaster surfaces it as `groupPullModeEngaged` and **never** uses it as
evidence for Auto Pull.

---

## 3. Auto Pull, Pull Target, Stop Pulling — VERIFIED (call graph)

**Auto Pull enabled state: `SimPlayerGrouping.PullConstant` (`bool`).**

```text
TogglePullConstant()                    // Shift+5 and the party-window button
  PullConstant = !PullConstant
  if (!PullConstant) { HoldPulls(true);  PullButton.text = "Auto Pull: OFF" }
  else               { GroupPull(false); PullButton.text = "Auto Pull: ON"  }
```

`SimPlayer::DoPulling` reads `PullConstant` to decide whether to keep pulling;
`SimPlayer::HandleBeingDeadInPlayerGroup` reads it as well.

| Command | Entry point | Effect on `PullConstant` |
|---|---|---|
| Auto Pull toggle | `TogglePullConstant()` (Shift+5) | flips it |
| Pull Target | `IndivPull()` (Shift+4) | sets `ForcePullTarget` = current target, `GroupPull(true)`, then **`PullConstant = false`** |
| Stop pulling | `HoldPulls(bool announce)` | not changed directly (see below) |
| Guard | `GroupGuard()` (Shift+6) | **if on, calls `TogglePullConstant()` → OFF** |
| Follow | `GroupFollow()` (Shift+3) | **if on, calls `TogglePullConstant()` → OFF**, then `HoldPulls(false)` |
| Run Away | `RunAway()` (Shift+7) | **if on, calls `TogglePullConstant()` → OFF** |
| Attack | `GroupAttack()` (Shift+1) | reads `PullConstant`, sets `ForcePullTarget` |

`HoldPulls(announce)` is the real "stop pulling" body: `isPulling = false`,
and per member `isPuller = false`, `MyAvatar.IgnoreAllCombat = false`,
`MyAvatar.ClearPullTarget()`, plus a `RunningAway`/`NPC.retreat` reset.

`EndPullConstant()` calls `HoldPulls(false)` and sets the button caption to
"Auto Pull: OFF" but **does not clear `PullConstant`**. Its only caller is
`Zoneline::CallZoning`, so immediately after crossing a zone line
`PullConstant` can still read `true` while pulls are actually held. Campmaster
ends the camp session on any zone change and requires the full predicate —
including a fresh native Guard — before recognizing a camp in the new zone, so
a stale `PullConstant` alone cannot start one. It can still make a
`/camp status` line optimistic right after zoning; see open question 1.

**Consequence for camp recognition:** pressing Guard turns Auto Pull off. The
real EQ-style camp order is therefore *Guard first, then Auto Pull* — which
leaves `GuardSpot == true` on every Sim **and** `PullConstant == true`. That is
exactly the state Campmaster recognizes.

Current pull target: `SimPlayerGrouping.ForcePullTarget` (`Character`), plus
per-Sim `SimPlayer.PullTarget` and `SimPlayer.CurrentPullPhase`.

---

## 4. Pull settings — VERIFIED (call graph)

All on `SimPlayerGrouping`, written only by `GroupTasks` sliders and the
constructor:

| Setting | Field | Type | Default | Slider handler |
|---|---|---|---|---|
| Max level above puller | `PullerRangeHigh` | `int` | `3` | `AdjustPullThreshholdHigh` |
| Max level below puller | `PullerRangeLow` | `int` | `-3` | `AdjustPullThreshholdLow` |
| Max pull distance | `MaxPullDist` | `int` | `500` | `AdjustPullDistance` |
| Hold if under % mana | `ManaNeededForPull` | `float` | `0.5` | `AdjustManaPullLimit` |

`AdjustManaPullLimit` stores `ManaWait.Slider.value / 100f`, so
`ManaNeededForPull` is a **normalized 0..1 fraction**, not an integer percent.
All four are consumed by `SimPlayer::FindNearestSpawn` (level window and
distance) and `SimPlayer::CheckPullReadiness` (mana).

---

## 5. The mana hold is NOT a group average — VERIFIED (call graph)

`SimPlayer::CheckPullReadiness()` in the installed build:

```text
if (MyStats.CurrentHP < RoundToInt(MyStats.CurrentMaxHP / 2)) {
    if (PullSpell == null) return false;
    if (MyStats.CurrentMana < PullSpell.ManaCost) return false;
}
foreach (SimPlayerTracking h in GameData.SimPlayerGrouping.Heals) {
    if (h?.MyAvatar?.MyStats == null) continue;
    if (h.MyAvatar.MyStats.CurrentMana <
        h.MyAvatar.MyStats.GetCurrentMaxMana() * ManaNeededForPull) return false;
}
return true;
```

So the wiki's "hold if under % group mana" is implemented as a **per-healer
minimum**, evaluated over `SimPlayerGrouping.Heals` only:

- it is a **worst-healer gate**, not an average;
- the **player is never included** (`Heals` holds `SimPlayerTracking` from
  `GameData.GroupMembers`, which contains Sims only);
- non-healer Sims are not considered at all;
- the puller's own gate is HP-based, with a pull-spell mana fallback.

There is **no explicit "holding for mana" state field** — it is a condition
evaluated inside the pull loop. Phase 1 therefore reports the configured
threshold but does **not** claim the party is holding for mana.

**Mana fields:** `Stats.CurrentMana` (`int`, public) and
`Stats.GetCurrentMaxMana()` (public method; the backing field
`Stats.CurrentMaxMana` is private).

---

## 6. Guard / Stay and the camp anchor — VERIFIED (call graph)

`SimPlayerGrouping.GroupGuard()` (Shift+6 and the party-window Guard button):

```text
if (PullConstant) TogglePullConstant();          // Auto Pull off
foreach member in GameData.GroupMembers:
    member.MyAvatar.freeRoamAzure = false
    member.MyAvatar.GetComponent<NPC>().CurrentAggroTarget = null
    member.MyAvatar.AssignGuardSpot(GameData.PlayerControl.transform.position)
```

Per-Sim storage on `SimPlayer`:

```text
public  bool    GuardSpot            // "I am guarding"
private Vector3 GuardPos             // where
public  Vector3 GetGuardPos()        // public accessor
public  void    AssignGuardSpot(Vector3 pos)   // GuardPos = pos + randomizeOffset; GuardSpot = true
public  void    FreeFollow()                   // GuardSpot = false
public  Character suspendGuard                 // transient combat leash, NOT the guard flag
```

Findings:

- There is **no central group Guard boolean**. Guard is per-Sim, but
  `GroupGuard()` assigns every member from the same player position, so the
  guard positions form one cluster.
- The cluster is not identical per Sim: `AssignGuardSpot` adds that Sim's
  `randomizeOffset`, whose components are drawn from
  `±randomizeMagnitude` where `randomizeMagnitude = 3 + SimPlayerGrouping.SpreadMagnitude`
  (`SimPlayer::MaintainTimersAndCounters`, `RandomizeStandingPosition`).
  Campmaster therefore derives its anchor tolerance from the live
  `SpreadMagnitude` rather than hard-coding a distance.
- **Combat does not clear Guard.** `SimPlayer::DoGuard` chases an aggro target
  via the separate `suspendGuard` field (33-unit leash) and walks back to
  `GuardPos` when it exceeds 8 units. `GuardSpot` itself is only cleared by
  `FreeFollow()` (i.e. `GroupFollow()`), raid orders
  (`RaidManager::OrderAttack` / `OrderTanksOnly` / `OrderGroupAttack` /
  `DismissRaider` → `NPC::ResetGuardOrder`), and revive paths
  (`GroupRevive`, `RaidRevive`, `HandleBeingDeadInSimPlayerGroup`).
- **The Puller keeps its Guard flag while pulling** — `SimPlayer::DoPulling`
  never touches `GuardSpot`.

**A stable common guard anchor can be read without writing anything:** the
centroid of `GetGuardPos()` over every locally-resolvable party Sim whose
`GuardSpot` is true. Campmaster requires *all* resolvable local Sims to be
guarding and the worst member-to-centroid distance to be within the
spread-derived tolerance; otherwise `GuardActive` is reported false/unknown.

---

## 7. Party membership and ownership — VERIFIED (call graph)

- `GameData.GroupMembers` is `SimPlayerTracking[]`, hard-indexed `0..3`
  throughout the native code, holding **Sims only** — the local player is
  implicit (`GroupMembers[0].SimName` is a Sim that speaks to the group). Every
  native group routine
  (`GroupPull`, `HoldPulls`, `GroupGuard`, `GroupFollow`, `RunAway`,
  `SetRoles`) iterates this array and null-checks each slot.
- Membership changes go through `InviteToGroup` / `DismissMember1..4` /
  `ForceDismissFromGroup`, all of which re-run `SetRoles()` or `ClearRoles()`.
- A tracking entry can exist while `MyAvatar` is null or not yet usable
  (zoning, slot reshuffle). Campmaster counts those as **unresolved** and
  degrades `Authority` to `PartialCoop`, rather than dropping the member.
- Local membership is confirmed with both `SimPlayer.InGroup` and
  `SimPlayerGrouping.IsSimInPlayerGroup(sim)` (which compares
  `GroupMembers[i].MyAvatar.transform.name` against the candidate).
- Raids use a separate structure (`GameData.RaidManager`, `RaidMemberSlot`,
  `RaidManager.Group1`). Campmaster refuses to auto-recognize while
  `GameData.RaidActive` is true.
- COOP is detected by reflection only (`ErenshorCoop.NetworkedPlayer`,
  `ErenshorCoop.Client.NetworkedPlayer`, `ErenshorCoop.NetworkedSim`), reusing
  the existing Deep Sims / Party Tools pattern. Remote entries are excluded
  from the guard-anchor computation and force `Authority = PartialCoop`.

---

## 8. Other verified members used

| Member | Use |
|---|---|
| `GameData.SceneName` (`string`) | current zone; a change finalizes the session |
| `GameData.InCombat` (`bool`) | activity classification + encounter counting |
| `GameData.RaidActive` (`bool`) | blocks auto recognition |
| `GameData.PlayerControl.transform.position` | `/camp here` anchor, departure detection |
| `GameData.PlayerStats.MyName` (`string`) | naming the player when they hold a role |
| `TypeText.CheckCommands()` (private, no args) | Harmony prefix for `/camp`; passes everything else through |
| `TypeText.typed` (`UnityEngine.UI.Text`) | reading/clearing the chat input |
| `UpdateSocialLog.LogAdd(string, string)` | chat output |

Shift-key command map confirmed from `SimPlayerGrouping::Update`:
`Shift+1 GroupAttack`, `+2 AssistMA`, `+3 GroupFollow`, `+4 IndivPull`,
`+5 TogglePullConstant`, `+6 GroupGuard`, `+7 RunAway`, `+8 InvisGroup`.

---

## 9. Pull lifecycle — found, but deliberately NOT used in Phase 1

`SimPlayer.CurrentPullPhase` is a real enum, `SimPlayer+PullPhases`:

```text
NotPulling = 0, FindTarget = 1, GoToTarget = 2,
EngageTarget = 3, ReturnTarget = 4, AttackTarget = 5
```

with supporting fields `SimPlayer.PullerPulling`, `SimPlayer.PullTarget`,
`SimPlayer.InCampWhilePulling`, and `SimPlayerGrouping.GroupTargets`.

Writers are `DoPulling`, `DoPullingRaid`, `Flee`, `Character::DoDeath` and
`GroupAttack`. This is a genuine pull lifecycle and is the right basis for a
`Pulling` activity state — but the transition timing has not been observed
live, so Phase 1 keeps activity at `Waiting / Fighting / Unknown` and counts
**completed encounters, not pulls**, exactly as the design requires. This is
the first item for Phase 3.

---

## 10. Unresolved / open questions

1. **`EndPullConstant()` (called from `Zoneline::CallZoning`) does not clear
   `PullConstant`.** So `AutoPullEnabled` can read `true` immediately after
   zoning while pulls are held. It cannot start a camp (Guard is also
   required), but `/camp status` could show a stale `Auto Pull: ON` in the
   seconds after a zone line. **Needs live test:** confirm whether the party
   window's own refresh, or `TogglePullConstant`'s next use, resynchronizes it.
2. **`SetRoles()` runs on `ZoneAnnounce::Start`.** Role assignments made by the
   player in `GroupTasks` are therefore expected to be re-derived from class on
   zone entry. Whether the UI re-applies the player's toggles afterwards
   (`GroupTasks::LoadValues` / `Init*`) has not been confirmed live.
3. **Guard cluster tolerance.** The spread-derived tolerance
   (`max(8, 1.5 * (3 + SpreadMagnitude) + 2)`) is arithmetic from
   `randomizeOffset`'s range, not an observed distribution. **Needs live test**
   with the Formation Spread slider at both extremes.
4. **COOP authority.** Whether `PullConstant`, `Puller` and `GuardSpot` are
   replicated to clients is unknown. Campmaster fails closed: remote/unresolved
   members degrade authority and are excluded from the guard anchor, and no
   write path exists at all.
5. **`GameData.InCombat` scope.** It is used as the party-combat signal, but
   whether it covers pull travel before first damage was not established. This
   only affects the encounter counter, never the session lifecycle.
6. **`SimPlayerGrouping.Mana`** is only ever read (`SetRoles`, `ClearRoles`,
   `GroupTasks::InitHeals`); no populating write was found in the assembly, so
   its intended membership is unclear. Campmaster does not read it and reports
   healers from `Heals`, which is the list `CheckPullReadiness` actually gates
   on.
---

## 11. Phase 3 implementation addendum — 0.3.0

The Phase-1 discovery above is now consumed by the read-only Phase-3 code. No
new gameplay writes were introduced.

### 11.1 Pull lifecycle

`NativeGroupStateReader` reads the already-verified `SimPlayer.CurrentPullPhase`
through one cached reflection handle and interprets enum value `0` only as the
verified `NotPulling` value. This avoids a hard dependency on nested-enum
visibility while still failing closed if the field changes. The assigned Sim
Puller comes from `SimPlayerGrouping.Puller`; no class-based puller guess is
used.

The same reader optionally reads the assigned puller's verified
`SimPlayer.PullTarget` and exposes only its current native name. Campmaster does
not select or modify that target.

A rising edge from `NotPulling` to any active pull phase increments
`verifiedPulls` and emits `camp_pull_started`. `Fighting` still outranks
`Pulling` in the displayed activity because `GameData.InCombat` is the stronger
current fact.

### 11.2 Native healer mana gate

Phase 3 recomputes the exact per-healer check documented in section 5:

```text
CurrentMana < GetCurrentMaxMana() * ManaNeededForPull
```

- a single readable healer below threshold is enough to prove the gate is
  blocked;
- a clear/not-blocked result is exposed only if every listed relevant healer
  was safely readable;
- unresolved/remote healers make the clear result `unknown`;
- the player remains excluded because native `Heals` is a Sim tracking list.

`Recovering` therefore means only: out of combat + Auto Pull on + this verified
native healer gate blocked. It does not mean Campmaster initiated recovery.

### 11.3 Repeated enemy and rough encounter seeds

The Phase-3 seeds are intentionally conservative:

- `camp_repeated_enemy` is based on the **same exact verified pull-target name**
  appearing on distinct pull starts. It does not claim a kill and does not
  normalize names into guessed enemy families. Default threshold: 3 pulls.
- `camp_rough_encounter` is a **Campmaster-derived long-combat classification**
  using verified `GameData.InCombat` duration only. Default threshold: 45s. Its
  payload explicitly identifies the classification as
  `CampmasterDerivedLongCombat`; it is not evidence of a death, wipe, close
  call, or native difficulty rating.

Both thresholds are mod policy and configurable.

### 11.4 Role-comment seed

`camp_role_snapshot` is emitted from the same verified Manage Roles storage
listed in sections 1/4 (`MainTank`, `MainAssist`, `DesignatedMA`, `Puller`,
`CC`, `Heals`, and the player-role flags). Unknown assignments are omitted.
This event is intended as a factual seed for an optional Deep Sims consumer;
Campmaster itself does not generate role banter.

