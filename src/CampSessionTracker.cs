using System;
using System.Collections.Generic;
using System.Globalization;

namespace ErenshorCampmaster
{
    // ---------------------------------------------------------------------
    // Deterministic Hunt Camp session lifecycle.
    //
    // This class is intentionally Unity-free and side-effect-free apart from
    // its own state: it takes an already-read CampObservation plus a clock and
    // returns the events it produced. That makes recognition, Phase-3 pull /
    // recovery semantics, and seed generation testable without Erenshor.
    //
    // It never writes native state. It never decides what the party should do.
    // ---------------------------------------------------------------------
    internal sealed class CampSessionTracker
    {
        private readonly CampConfig _config;
        private readonly List<CampEvent> _events = new List<CampEvent>();
        private long _sequence;

        // Session identity
        private string _sessionId;
        private CampSessionState _state = CampSessionState.Inactive;
        private CampRecognitionSource _source = CampRecognitionSource.None;
        private DateTime _startedUtc;
        private string _zone;
        private CampVector3? _anchor;
        private string _anchorOrigin;
        private List<string> _party = new List<string>();

        // Latest verified native facts belonging to the running session.
        private CampObservation _last;

        // Counters
        private int _completedEncounters;
        private int _verifiedPulls;
        private int _roughEncounters;
        private int _repeatedEnemySignals;
        private int _partyChanges;
        private int _suspensions;

        // Timers (null = not currently running)
        private DateTime? _autoCandidateSince;
        private DateTime? _partyMissingSince;
        private DateTime? _departedSince;
        private DateTime? _readFailedSince;

        // Encounter bookkeeping
        private bool _encounterOpen;
        private DateTime? _encounterStartedUtc;
        private DateTime? _combatClearedAt;

        // Phase-3 pull/recovery bookkeeping
        private bool _pullOpen;
        private bool _pullTargetCounted;
        private bool _recoveryActive;
        private readonly Dictionary<string, int> _pullTargetCounts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _repeatedEnemySeeded =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _lastRoleFingerprint;

        // One-shot explicit intents queued by the command router.
        private bool _pendingDeclare;
        private CampVector3? _pendingDeclareAnchor;
        private bool _pendingClear;

        internal CampSessionTracker(CampConfig config)
        {
            _config = config ?? new CampConfig();
        }

        internal CampConfig Config { get { return _config; } }
        internal CampSessionState State { get { return _state; } }
        internal bool IsActive { get { return _state == CampSessionState.Active || _state == CampSessionState.Suspended; } }
        internal long LatestSequence { get { return _sequence; } }
        internal long OldestRetainedSequence { get { return _events.Count == 0 ? 0L : _events[0].Sequence; } }

        internal void RequestDeclareHere(CampVector3? playerPosition)
        {
            _pendingDeclare = true;
            _pendingDeclareAnchor = playerPosition;
        }

        internal void RequestClear()
        {
            _pendingClear = true;
        }

        internal void SetAutoRecognitionEnabled(bool enabled)
        {
            _config.AutoRecognitionEnabled = enabled;
            if (!enabled) _autoCandidateSince = null;
        }

        // -----------------------------------------------------------------
        // Main deterministic step.
        // -----------------------------------------------------------------
        internal void Tick(CampObservation obs, DateTime nowUtc)
        {
            if (obs == null) return;

            if (!obs.ReadSucceeded)
            {
                // A failed read proves nothing. Hold the session, do not break it.
                if (!_readFailedSince.HasValue) _readFailedSince = nowUtc;
                if (_state == CampSessionState.Active &&
                    Elapsed(_readFailedSince, nowUtc) >= _config.SignalLossGraceSeconds)
                {
                    Suspend(nowUtc, "native state unreadable");
                }
                // A pending clear must still work while state is unreadable.
                if (_pendingClear)
                {
                    _pendingClear = false;
                    if (IsActive) End(nowUtc, "cleared by player");
                }
                _pendingDeclare = false;
                return;
            }

            _readFailedSince = null;
            _last = obs;

            // 1. Explicit clear always wins.
            if (_pendingClear)
            {
                _pendingClear = false;
                _pendingDeclare = false;
                if (IsActive) End(nowUtc, "cleared by player");
                return;
            }

            // 2. Hard break conditions for a running session.
            if (IsActive && !EvaluateBreaks(obs, nowUtc)) return;

            // 3. Explicit declaration.
            if (_pendingDeclare)
            {
                _pendingDeclare = false;
                CampVector3? anchor = _pendingDeclareAnchor;
                _pendingDeclareAnchor = null;
                if (obs.HasParty && obs.LocalResolvedMembers > 0 && obs.RaidActive != true && anchor.HasValue)
                {
                    if (IsActive) End(nowUtc, "replaced by /camp here");
                    Start(obs, nowUtc, CampRecognitionSource.Explicit, anchor, "player position");
                    return;
                }
            }

            // 4. Automatic recognition (Inactive only).
            if (_state == CampSessionState.Inactive)
            {
                EvaluateAutoRecognition(obs, nowUtc);
                return;
            }

            // 5. Running session upkeep.
            TrackPartyChange(obs, nowUtc);
            TrackRoleSnapshot(obs, nowUtc, false);
            TrackSignalQuality(obs, nowUtc);
            TrackPulls(obs, nowUtc);
            TrackEncounters(obs, nowUtc);
            TrackRecovery(obs, nowUtc);
            RefreshAnchor(obs);
        }

        // -----------------------------------------------------------------
        // Break / suspend evaluation. Returns false when the session ended.
        // -----------------------------------------------------------------
        private bool EvaluateBreaks(CampObservation obs, DateTime nowUtc)
        {
            // Zone change always finalizes the session.
            if (!string.IsNullOrEmpty(_zone) && !string.IsNullOrEmpty(obs.Zone) &&
                !string.Equals(_zone, obs.Zone, StringComparison.Ordinal))
            {
                End(nowUtc, "zone changed to " + obs.Zone);
                return false;
            }

            // Party loss, with a grace window so zoning/slot churn does not
            // finalize a camp that is about to come back. A roster entry with
            // no locally resolved Sim is not enough to keep an existing camp
            // alive: it can be a remote proxy or a stale/zoning tracking.
            // Treat it as temporarily unavailable until the same grace elapses.
            if (!obs.HasParty || obs.LocalResolvedMembers <= 0)
            {
                if (!_partyMissingSince.HasValue) _partyMissingSince = nowUtc;
                if (Elapsed(_partyMissingSince, nowUtc) >= _config.PartyLossGraceSeconds)
                {
                    End(nowUtc, obs.HasParty
                        ? "local party was unavailable beyond the grace period"
                        : "party no longer present");
                    return false;
                }
                if (_state == CampSessionState.Active)
                    Suspend(nowUtc, obs.HasParty ? "local party temporarily unresolved" : "party temporarily unresolved");
                return true;
            }
            _partyMissingSince = null;

            // Sustained departure from the camp anchor.
            if (_anchor.HasValue && obs.PlayerPosition.HasValue)
            {
                float distance = CampVector3.Distance(_anchor.Value, obs.PlayerPosition.Value);
                if (distance > _config.DepartureRadius)
                {
                    if (!_departedSince.HasValue) _departedSince = nowUtc;
                    if (Elapsed(_departedSince, nowUtc) >= _config.DepartureGraceSeconds)
                    {
                        End(nowUtc, "left camp");
                        return false;
                    }
                }
                else
                {
                    _departedSince = null;
                }
            }
            else
            {
                _departedSince = null;
            }

            return true;
        }

        // -----------------------------------------------------------------
        // Automatic recognition.
        //
        // Fail-closed: every element of the predicate must be a verified
        // native fact. A null/unknown field can never satisfy it.
        // -----------------------------------------------------------------
        internal static bool MeetsAutoPredicate(CampObservation obs)
        {
            if (obs == null || !obs.ReadSucceeded) return false;
            if (!obs.HasParty) return false;
            if (obs.LocalResolvedMembers <= 0) return false;       // a local Sim party must actually be loaded
            if (obs.Authority != CampAuthority.FullLocal) return false; // remote/unresolved members fail closed
            if (obs.RaidActive == true) return false;              // raids are a different system
            if (obs.GuardActive != true) return false;             // native Guard/Stay verified
            if (!obs.GuardAnchor.HasValue) return false;           // stable common anchor
            if (!obs.Puller.IsKnown) return false;                 // verified Puller assignment
            if (obs.AutoPullEnabled != true) return false;         // verified Auto Pull enabled
            return true;
        }

        // Recognition additionally requires the player to actually be at the
        // anchor. Without this, walking away from a camp whose Sims are still
        // guarding would break the session and then immediately recognize it
        // again from across the zone.
        internal static bool IsAutoRecognitionCandidate(CampObservation obs, float radius)
        {
            if (!MeetsAutoPredicate(obs)) return false;
            if (!obs.PlayerPosition.HasValue) return false;  // "player at anchor" is required evidence
            return CampVector3.Distance(obs.GuardAnchor.Value, obs.PlayerPosition.Value) <= Math.Max(1f, radius);
        }

        internal static string DescribeRecognitionWait(CampObservation obs, float radius)
        {
            if (!MeetsAutoPredicate(obs)) return DescribeMissingSignal(obs);
            if (!obs.PlayerPosition.HasValue) return "player position is unknown";
            if (CampVector3.Distance(obs.GuardAnchor.Value, obs.PlayerPosition.Value) > Math.Max(1f, radius))
                return "player is not at the Guard anchor";
            return "recognition stability window";
        }

        private void EvaluateAutoRecognition(CampObservation obs, DateTime nowUtc)
        {
            if (!_config.AutoRecognitionEnabled || !IsAutoRecognitionCandidate(obs, _config.DepartureRadius))
            {
                _autoCandidateSince = null;
                return;
            }

            if (!_autoCandidateSince.HasValue)
            {
                _autoCandidateSince = nowUtc;
                return;
            }

            if (Elapsed(_autoCandidateSince, nowUtc) >= Math.Max(0.0, _config.AutoStabilitySeconds))
            {
                _autoCandidateSince = null;
                Start(obs, nowUtc, CampRecognitionSource.Auto, obs.GuardAnchor, "native guard");
            }
        }

        // -----------------------------------------------------------------
        // Upkeep
        // -----------------------------------------------------------------
        private void TrackPartyChange(CampObservation obs, DateTime nowUtc)
        {
            if (SameNames(_party, obs.PartyNames)) return;
            _party = new List<string>(obs.PartyNames);
            _partyChanges++;
            Emit(CampEventType.CampPartyChanged, nowUtc, "party roster changed");
        }

        private void TrackSignalQuality(CampObservation obs, DateTime nowUtc)
        {
            // Only an automatically recognized camp depends on the native
            // guard/puller/auto-pull signals staying true. An explicit
            // /camp here camp is context the player asserted, so losing those
            // signals must not suspend it.
            if (_source != CampRecognitionSource.Auto)
            {
                if (_state == CampSessionState.Suspended && obs.HasParty) Resume(nowUtc, "party resolved");
                return;
            }

            bool ok = MeetsAutoPredicate(obs);
            if (_state == CampSessionState.Active && !ok)
            {
                Suspend(nowUtc, DescribeMissingSignal(obs));
            }
            else if (_state == CampSessionState.Suspended && ok)
            {
                Resume(nowUtc, "native camp signals verified again");
            }
        }

        internal static string DescribeMissingSignal(CampObservation obs)
        {
            if (obs == null || !obs.ReadSucceeded) return "native state unreadable";
            if (!obs.HasParty) return "party unresolved";
            if (obs.LocalResolvedMembers <= 0) return "no local Sim party is resolved";
            if (obs.Authority != CampAuthority.FullLocal) return "party authority is partial or unknown";
            if (obs.RaidActive == true) return "raid active";
            if (obs.GuardActive != true) return "party is no longer guarding";
            if (!obs.GuardAnchor.HasValue) return "no stable guard anchor";
            if (!obs.Puller.IsKnown) return "puller unknown";
            if (obs.AutoPullEnabled != true) return "auto pull is off";
            return "camp signals incomplete";
        }

        // Verified pull counting uses only the assigned Sim Puller's native
        // CurrentPullPhase transition from NotPulling -> any active phase.
        private void TrackPulls(CampObservation obs, DateTime nowUtc)
        {
            if (obs.PullerActivelyPulling == true)
            {
                if (!_pullOpen)
                {
                    _pullOpen = true;
                    _pullTargetCounted = false;
                    RecordPullStarted(obs, nowUtc);
                }
                else if (!_pullTargetCounted)
                {
                    CountPullTargetIfAvailable(obs, nowUtc);
                }
                return;
            }

            if (obs.PullerActivelyPulling == false)
            {
                _pullOpen = false;
                _pullTargetCounted = false;
            }
            // null = unknown; retain the previous edge state rather than
            // manufacturing a pull end/start pair from an unreadable sample.
        }

        private void RecordPullStarted(CampObservation obs, DateTime nowUtc)
        {
            _verifiedPulls++;
            Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.Ordinal);
            string target = CleanName(obs.CurrentPullTargetName);
            if (!string.IsNullOrEmpty(target)) fields["target"] = target;
            fields["verifiedPullNumber"] = _verifiedPulls.ToString(CultureInfo.InvariantCulture);
            Emit(CampEventType.CampPullStarted, nowUtc,
                "assigned Sim Puller entered the native pull lifecycle", fields);
            CountPullTargetIfAvailable(obs, nowUtc);
        }

        private void CountPullTargetIfAvailable(CampObservation obs, DateTime nowUtc)
        {
            string target = CleanName(obs.CurrentPullTargetName);
            if (string.IsNullOrEmpty(target)) return;
            _pullTargetCounted = true;

            int count;
            _pullTargetCounts.TryGetValue(target, out count);
            count++;
            _pullTargetCounts[target] = count;

            int threshold = Math.Max(2, _config.RepeatedEnemyThreshold);
            if (count < threshold || _repeatedEnemySeeded.Contains(target)) return;

            _repeatedEnemySeeded.Add(target);
            _repeatedEnemySignals++;
            Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.Ordinal);
            fields["target"] = target;
            fields["observedPulls"] = count.ToString(CultureInfo.InvariantCulture);
            fields["threshold"] = threshold.ToString(CultureInfo.InvariantCulture);
            Emit(CampEventType.CampRepeatedEnemy, nowUtc,
                "same verified pull-target name reached the Campmaster repeat threshold", fields);
        }

        // Combat is normal inside a camp and never ends the session. An
        // encounter is only counted once combat has been clear for the
        // configured quiet period (a Campmaster threshold, not a game rule).
        private void TrackEncounters(CampObservation obs, DateTime nowUtc)
        {
            if (obs.InCombat == null) return;

            if (obs.InCombat == true)
            {
                if (!_encounterOpen)
                {
                    _encounterOpen = true;
                    _encounterStartedUtc = nowUtc;
                    Emit(CampEventType.CampEncounterStarted, nowUtc, "verified party combat began");
                }
                _combatClearedAt = null;
                return;
            }

            if (!_encounterOpen) return;
            if (!_combatClearedAt.HasValue) _combatClearedAt = nowUtc;
            if (Elapsed(_combatClearedAt, nowUtc) < _config.EncounterQuietSeconds) return;

            DateTime completedCombatUtc = _combatClearedAt.Value;
            double durationSeconds = _encounterStartedUtc.HasValue
                ? Math.Max(0.0, (completedCombatUtc - _encounterStartedUtc.Value).TotalSeconds)
                : 0.0;

            _encounterOpen = false;
            _encounterStartedUtc = null;
            _combatClearedAt = null;
            _completedEncounters++;

            Dictionary<string, string> completedFields = new Dictionary<string, string>(StringComparer.Ordinal);
            completedFields["encounterNumber"] = _completedEncounters.ToString(CultureInfo.InvariantCulture);
            completedFields["combatDurationSeconds"] = ((int)Math.Round(durationSeconds)).ToString(CultureInfo.InvariantCulture);
            Emit(CampEventType.CampEncounterCompleted, nowUtc,
                "verified combat ended and remained quiet for the Campmaster encounter window", completedFields);

            if (durationSeconds >= Math.Max(1.0, _config.RoughEncounterSeconds))
            {
                _roughEncounters++;
                Dictionary<string, string> roughFields = new Dictionary<string, string>(StringComparer.Ordinal);
                roughFields["combatDurationSeconds"] = ((int)Math.Round(durationSeconds)).ToString(CultureInfo.InvariantCulture);
                roughFields["roughThresholdSeconds"] = ((int)Math.Round(_config.RoughEncounterSeconds)).ToString(CultureInfo.InvariantCulture);
                roughFields["classification"] = "CampmasterDerivedLongCombat";
                Emit(CampEventType.CampRoughEncounter, nowUtc,
                    "Campmaster-derived rough encounter classification from verified combat duration only", roughFields);
            }
        }

        // Recovering is deliberately narrow: it means Auto Pull is on and the
        // exact native per-healer mana gate currently evaluates blocked. It
        // never means Campmaster is making anyone sit/heal or resume pulling.
        private void TrackRecovery(CampObservation obs, DateTime nowUtc)
        {
            if (obs.InCombat == true)
            {
                if (_recoveryActive)
                {
                    _recoveryActive = false;
                    Emit(CampEventType.CampRecoveryEnded, nowUtc, "combat began");
                }
                return;
            }

            if (obs.PullerActivelyPulling == true)
            {
                if (_recoveryActive)
                {
                    _recoveryActive = false;
                    Emit(CampEventType.CampRecoveryEnded, nowUtc, "verified pull began");
                }
                return;
            }

            if (obs.AutoPullEnabled == false)
            {
                if (_recoveryActive)
                {
                    _recoveryActive = false;
                    Emit(CampEventType.CampRecoveryEnded, nowUtc, "Auto Pull is off");
                }
                return;
            }

            if (obs.AutoPullEnabled != true || !obs.HoldingForMana.HasValue) return;

            if (obs.HoldingForMana.Value && !_recoveryActive)
            {
                _recoveryActive = true;
                Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.Ordinal);
                if (obs.HoldManaFraction.HasValue)
                    fields["holdManaPercent"] = ((int)Math.Round(obs.HoldManaFraction.Value * 100f)).ToString(CultureInfo.InvariantCulture);
                fields["reason"] = "nativePerHealerManaGate";
                Emit(CampEventType.CampRecoveryStarted, nowUtc,
                    "native per-healer pull-readiness mana gate is blocking Auto Pull", fields);
            }
            else if (!obs.HoldingForMana.Value && _recoveryActive)
            {
                _recoveryActive = false;
                Emit(CampEventType.CampRecoveryEnded, nowUtc,
                    "native per-healer pull-readiness mana gate is clear");
            }
        }

        // Emits an additive role-comment seed from actual native Manage Roles
        // assignments. Unknown roles are omitted, never guessed from class.
        private void TrackRoleSnapshot(CampObservation obs, DateTime nowUtc, bool force)
        {
            Dictionary<string, string> fields = BuildRoleFields(obs);
            if (fields.Count == 0) return;

            string fingerprint = Fingerprint(fields);
            if (!force && string.Equals(_lastRoleFingerprint, fingerprint, StringComparison.Ordinal)) return;
            _lastRoleFingerprint = fingerprint;
            Emit(CampEventType.CampRoleSnapshot, nowUtc,
                force ? "verified native Manage Roles snapshot" : "verified native Manage Roles assignments changed",
                fields);
        }

        // An auto camp keeps tracking the live native guard anchor so a
        // deliberate re-Guard at a slightly different spot does not look like
        // a departure. An explicit camp keeps the anchor the player declared.
        private void RefreshAnchor(CampObservation obs)
        {
            if (_source != CampRecognitionSource.Auto) return;
            if (obs.GuardActive == true && obs.GuardAnchor.HasValue)
            {
                _anchor = obs.GuardAnchor;
                _anchorOrigin = "native guard";
            }
        }

        // -----------------------------------------------------------------
        // Transitions
        // -----------------------------------------------------------------
        private void Start(CampObservation obs, DateTime nowUtc, CampRecognitionSource source,
                           CampVector3? anchor, string anchorOrigin)
        {
            _sessionId = "camp-" + nowUtc.ToString("yyyyMMddHHmmss") + "-" + (_sequence + 1).ToString(CultureInfo.InvariantCulture);
            _state = CampSessionState.Active;
            _source = source;
            _startedUtc = nowUtc;
            _zone = obs.Zone;
            _anchor = anchor;
            _anchorOrigin = anchor.HasValue ? anchorOrigin : null;
            _party = new List<string>(obs.PartyNames);
            _completedEncounters = 0;
            _verifiedPulls = 0;
            _roughEncounters = 0;
            _repeatedEnemySignals = 0;
            _partyChanges = 0;
            _suspensions = 0;
            _encounterOpen = false;
            _encounterStartedUtc = null;
            _combatClearedAt = null;
            _pullOpen = false;
            _pullTargetCounted = false;
            _recoveryActive = false;
            _pullTargetCounts.Clear();
            _repeatedEnemySeeded.Clear();
            _lastRoleFingerprint = null;
            _departedSince = null;
            _partyMissingSince = null;
            _last = obs;

            Emit(CampEventType.CampStarted, nowUtc,
                source == CampRecognitionSource.Auto ? "recognized from native camp signals" : "declared with /camp here");
            TrackRoleSnapshot(obs, nowUtc, true);

            // Preserve verified activity already in progress at recognition.
            if (obs.PullerActivelyPulling == true)
            {
                _pullOpen = true;
                RecordPullStarted(obs, nowUtc);
            }
            if (obs.InCombat == true)
            {
                _encounterOpen = true;
                _encounterStartedUtc = nowUtc;
                Emit(CampEventType.CampEncounterStarted, nowUtc, "verified party combat already active when camp started");
            }
            TrackRecovery(obs, nowUtc);
        }

        private void Suspend(DateTime nowUtc, string reason)
        {
            if (_state != CampSessionState.Active) return;
            _state = CampSessionState.Suspended;
            _suspensions++;
            Emit(CampEventType.CampSuspended, nowUtc, reason);
        }

        private void Resume(DateTime nowUtc, string reason)
        {
            if (_state != CampSessionState.Suspended) return;
            _state = CampSessionState.Active;
            Emit(CampEventType.CampResumed, nowUtc, reason);
        }

        private void End(DateTime nowUtc, string reason)
        {
            if (!IsActive) return;
            _state = CampSessionState.Breaking;
            Emit(CampEventType.CampEnded, nowUtc, reason);
            _state = CampSessionState.Inactive;
            _source = CampRecognitionSource.None;
            _sessionId = null;
            _anchor = null;
            _anchorOrigin = null;
            _zone = null;
            _party = new List<string>();
            _autoCandidateSince = null;
            _partyMissingSince = null;
            _departedSince = null;
            _encounterOpen = false;
            _encounterStartedUtc = null;
            _combatClearedAt = null;
            _pullOpen = false;
            _pullTargetCounted = false;
            _recoveryActive = false;
            _pullTargetCounts.Clear();
            _repeatedEnemySeeded.Clear();
            _lastRoleFingerprint = null;
        }

        // -----------------------------------------------------------------
        // Snapshot / events
        // -----------------------------------------------------------------
        internal CampSnapshot BuildSnapshot(DateTime nowUtc)
        {
            CampSnapshot snap = new CampSnapshot();
            snap.State = _state;
            snap.Source = _source;
            snap.SessionId = _sessionId;
            snap.CompletedEncounters = _completedEncounters;
            snap.VerifiedPulls = _verifiedPulls;
            snap.RoughEncounters = _roughEncounters;
            snap.RepeatedEnemySignals = _repeatedEnemySignals;
            snap.PartyChanges = _partyChanges;
            snap.Suspensions = _suspensions;

            if (IsActive)
            {
                snap.Zone = _zone;
                snap.StartedUtc = _startedUtc;
                snap.ElapsedSeconds = (nowUtc - _startedUtc).TotalSeconds;
                snap.Anchor = _anchor;
                snap.AnchorOrigin = _anchorOrigin;
                snap.Party = new List<string>(_party);
            }

            CampObservation obs = _last;
            if (obs != null && obs.ReadSucceeded)
            {
                snap.Authority = obs.Authority;
                snap.MainTank = obs.MainTank;
                snap.MainAssist = obs.MainAssist;
                snap.DesignatedMainAssist = obs.DesignatedMainAssist;
                snap.Puller = obs.Puller;
                snap.CrowdControl = new List<string>(obs.CrowdControlNames);
                snap.CrowdControlKnown = obs.CrowdControlKnown;
                snap.Healers = new List<string>(obs.HealerNames);
                snap.HealersKnown = obs.HealersKnown;
                snap.AutoPullEnabled = obs.AutoPullEnabled;
                snap.GroupPullModeEngaged = obs.GroupPullModeEngaged;
                snap.ForcePullTargetName = obs.ForcePullTargetName;
                snap.CurrentPullTargetName = obs.CurrentPullTargetName;
                snap.MaxPullLevelAbove = obs.MaxPullLevelAbove;
                snap.MaxPullLevelBelow = obs.MaxPullLevelBelow;
                snap.MaxPullDistance = obs.MaxPullDistance;
                snap.HoldManaFraction = obs.HoldManaFraction;
                snap.PullerActivelyPulling = obs.PullerActivelyPulling;
                snap.HoldingForMana = obs.HoldingForMana;
                if (!IsActive) snap.Zone = obs.Zone;

                if (_state == CampSessionState.Active)
                    snap.Activity = ClassifyActivity(obs);
            }

            return snap;
        }

        internal static CampActivity ClassifyActivity(CampObservation obs)
        {
            if (obs == null || !obs.ReadSucceeded) return CampActivity.Unknown;
            if (obs.InCombat == true) return CampActivity.Fighting;
            if (obs.PullerActivelyPulling == true) return CampActivity.Pulling;
            if (obs.InCombat == false && obs.AutoPullEnabled == true && obs.HoldingForMana == true)
                return CampActivity.Recovering;
            if (obs.InCombat == false && obs.Puller.Kind == RoleHolderKind.Player && !obs.PullerActivelyPulling.HasValue)
                return CampActivity.Unknown;
            if (obs.InCombat == false) return CampActivity.Waiting;
            return CampActivity.Unknown;
        }

        internal List<CampEvent> GetEventsAfter(long sequence)
        {
            List<CampEvent> result = new List<CampEvent>();
            for (int i = 0; i < _events.Count; i++)
                if (_events[i].Sequence > sequence) result.Add(_events[i]);
            return result;
        }

        private void Emit(CampEventType type, DateTime nowUtc, string detail)
        {
            Emit(type, nowUtc, detail, null);
        }

        private void Emit(CampEventType type, DateTime nowUtc, string detail, Dictionary<string, string> verifiedFields)
        {
            CampEvent evt = new CampEvent();
            evt.Sequence = ++_sequence;
            evt.EventId = Guid.NewGuid().ToString("N");
            evt.Utc = nowUtc;
            evt.SessionId = _sessionId;
            evt.Type = type;
            evt.Zone = _zone;
            evt.Detail = detail;
            evt.PartyNames = new List<string>(_party);
            if (verifiedFields != null)
            {
                foreach (KeyValuePair<string, string> pair in verifiedFields)
                    if (!string.IsNullOrEmpty(pair.Key) && !string.IsNullOrEmpty(pair.Value))
                        evt.VerifiedFields[pair.Key] = pair.Value;
            }
            _events.Add(evt);
            while (_events.Count > _config.MaxRetainedEvents) _events.RemoveAt(0);
        }

        private static Dictionary<string, string> BuildRoleFields(CampObservation obs)
        {
            Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.Ordinal);
            if (obs == null) return fields;
            PutRole(fields, "mainTank", obs.MainTank);
            PutRole(fields, "mainAssist", obs.MainAssist);
            PutRole(fields, "designatedMainAssist", obs.DesignatedMainAssist);
            PutRole(fields, "puller", obs.Puller);
            if (obs.CrowdControlKnown && obs.CrowdControlNames != null && obs.CrowdControlNames.Count > 0)
                fields["crowdControl"] = string.Join(", ", obs.CrowdControlNames.ToArray());
            if (obs.HealersKnown && obs.HealerNames != null && obs.HealerNames.Count > 0)
                fields["healers"] = string.Join(", ", obs.HealerNames.ToArray());
            return fields;
        }

        private static void PutRole(Dictionary<string, string> fields, string key, RoleHolder role)
        {
            if (!role.IsResolved) return;
            fields[key] = role.Describe();
            fields[key + "Kind"] = role.Kind.ToString();
        }

        private static string Fingerprint(Dictionary<string, string> fields)
        {
            List<string> keys = new List<string>(fields.Keys);
            keys.Sort(StringComparer.Ordinal);
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                if (i > 0) sb.Append('|');
                sb.Append(key).Append('=').Append(fields[key]);
            }
            return sb.ToString();
        }

        private static string CleanName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static double Elapsed(DateTime? since, DateTime nowUtc)
        {
            return since.HasValue ? (nowUtc - since.Value).TotalSeconds : 0.0;
        }

        private static bool SameNames(List<string> a, List<string> b)
        {
            if (a == null || b == null) return a == b;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < b.Count; j++)
                {
                    if (string.Equals(a[i], b[j], StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }
    }
}
