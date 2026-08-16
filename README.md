# Forgotten Roads: Campmaster 0.4.0

Part of the **Forgotten Roads for Erenshor** mod collection.

Campmaster observes native party state and exposes two social context modes: Hunt Camp and Relax. It is intentionally read-only with respect to Erenshor gameplay.

## Commands

```text
/camp status                 show camp state and native observations
/camp setup                  explain missing/required camp signals
/camp here                   explicitly declare a camp at the current anchor
/camp clear                  clear the current camp context
/camp auto on|off            enable/disable automatic recognition
/camp selftest               run deterministic camp tests
/relax here                  begin explicit social downtime context
/relax off                   end Relax
/relax status                show Relax state
```

## Hunt Camp

Automatic recognition requires a stable, locally readable native party state: a normal local Sim party, guarded/common position, verified role assignments, native Auto Pull state, and the relevant pull/combat signals. Recognition waits through configurable stability, departure, party-loss, signal-loss, and encounter-quiet windows. `/camp here` is an explicit player declaration when automatic recognition is disabled or unavailable.

Status reports zone, anchor, party, activity, completed/rough encounters, social seeds, native Main Tank/Main Assist/Puller/CC/Healing assignments, Guard, Auto Pull, pull lifecycle, current pull target, and healer mana gating where readable. Unknown remains unknown; class capability is not substituted for a native assignment.

Campmaster can emit context events for Deep Sims, but those events are not gameplay facts beyond what the native observation supports. It does not assign roles, toggle Guard or Auto Pull, pick targets, pull, attack, heal, move, loot, or change saves.

## Relax

Relax is explicit social downtime anchored with `/relax here`. It is mutually exclusive with an active Hunt Camp, suspends for verified combat, resumes after combat, and ends after departure, party loss, zoning, or `/relax off`. Relax changes only context and bounded social/event behavior; it does not move or control the party.

## Configuration

Recognition and Relax thresholds are native Lunaris config settings: stability, departure radius/grace, party-loss grace, signal-loss grace, encounter quiet time, rough-encounter threshold, repeated-enemy threshold, and automatic recognition. These are Campmaster thresholds, not new Erenshor mechanics.

## Compatibility and build

The plugin is `forgetwhtuno.erenshor.campmaster`, version `0.4.0`. COOP/local authority is classified conservatively. Native state is polled at a low fixed interval, and unreadable state causes warnings/unknown fields rather than guesses.

This version requires **native Lunaris** — BepInEx is no longer required. `BUILD_AND_INSTALL.ps1` locates the current Erenshor install and the Lunaris developer reference, compiles the plugin, and installs only `ErenshorCampmaster.dll` to `<Erenshor>\plugins\`. Lunaris manages enable/disable and config. A legacy BepInEx release remains available in this repository's Git history.

**Status:** the deterministic Camp, Relax, and Control API suites pass for the current source snapshot. A fresh native build and plugin-identity audit remain pending because the current Lunaris resolver is unavailable in this session. It has not yet been live-tested in-game under Lunaris (enable/disable/reload behavior). Do not assume hot-reload safety until that pass is done.

## Credits and Inspiration

### Compatibility / related projects

- **[Erenshor COOP](https://github.com/MizukiBelhi/ErenshorCoop) by MizukiBelhi** — important technical reference and compatibility target for remote-human/networked-Sim detection. I have also tested against a locally updated copy for recent Erenshor and Deep Sims compatibility.

## Development note

The goal is to build features for Erenshor, with development guided through design, testing, playtesting, audits, and iteration against the game. Bug reports, code review, corrections, and contributions from experienced Erenshor modders are welcome.

This is an unofficial, community-made mod for Erenshor and is not affiliated with or endorsed by the game's developer.


## Optional Forgotten Roads Hub integration

Forgotten Roads Hub is **optional**. When it is installed, this mod can expose its normal player-facing controls there through the versioned public `CampmasterControlApi` surface. The mod remains independently usable without Forgotten Roads Hub and does not compile against Hub types or assume Hub load order.

Campmaster keeps gameplay scope small, but it now has the shared retained fallback entry point for mouse discoverability. A healthy Hub hides that fallback; if Hub is absent/unavailable it returns automatically. `/camp` and `/relax` remain compatibility controls.

Hub can show Hunt Camp/Relax status, toggle Campmaster's existing automatic Hunt Camp recognition setting, and invoke the existing explicit Relax Here/Off intents. It never changes roles, Guard, Auto Pull, targets, combat, movement, loot, equipment, or quests. `/camp auto on|off` and the Hub setting now use the same persisted Campmaster setting path.

The shared control/API and fully-in-world UI policy in this handoff are source-validated but **not yet live-tested under Lunaris hot reload**.
