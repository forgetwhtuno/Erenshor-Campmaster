# Erenshor Campmaster 0.4.0

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

**Status:** this native build compiles cleanly against the installed Lunaris/Assembly-CSharp and passes its full deterministic test suite (Camp, Relax, and Control API cases). It has not yet been live-tested in-game under Lunaris (enable/disable/reload behavior). Do not assume hot-reload safety until that pass is done.

## Credits and Inspiration

### Compatibility / related projects

- **[Erenshor COOP](https://github.com/MizukiBelhi/ErenshorCoop) by MizukiBelhi** — important technical reference and compatibility target for remote-human/networked-Sim detection. I have also tested against a locally updated copy for recent Erenshor and Deep Sims compatibility.

## Development note

This project has been developed heavily with AI-assisted coding tools. The goal has been to build features I wanted to use in Erenshor, with development guided through design, testing, playtesting, audits, and iteration against the game. Bug reports, code review, corrections, and contributions from experienced Erenshor modders are welcome.

This is an unofficial, community-made mod for Erenshor and is not affiliated with or endorsed by the game's developer.
