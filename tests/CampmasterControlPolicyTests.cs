using System;

namespace ErenshorCampmaster
{
    internal static class CampmasterControlPolicyTests
    {
        private static int _failures;

        private static int Main()
        {
            Check("ready + readable party accepts policy", Evaluate(true, false, true, true, true, true, false, true) == CampmasterDeclareHereDecision.Accepted);
            Check("missing tracker rejects request", Evaluate(false, false, true, true, true, true, false, true) == CampmasterDeclareHereDecision.NotReady);
            Check("active camp rejects duplicate request", Evaluate(true, true, true, true, true, true, false, true) == CampmasterDeclareHereDecision.AlreadyActive);
            Check("missing observation rejects request", Evaluate(true, false, false, false, false, false, false, false) == CampmasterDeclareHereDecision.ObservationUnavailable);
            Check("failed native read rejects request", Evaluate(true, false, true, false, true, true, false, true) == CampmasterDeclareHereDecision.ObservationUnavailable);
            Check("no party rejects request", Evaluate(true, false, true, true, false, false, false, true) == CampmasterDeclareHereDecision.NoParty);
            Check("no local Sims rejects request", Evaluate(true, false, true, true, true, false, false, true) == CampmasterDeclareHereDecision.NoLocalParty);
            Check("raid rejects request", Evaluate(true, false, true, true, true, true, true, true) == CampmasterDeclareHereDecision.RaidActive);
            Check("missing anchor rejects request", Evaluate(true, false, true, true, true, true, false, false) == CampmasterDeclareHereDecision.MissingAnchor);
            Check("rejections provide a reason", RejectionsHaveReasons());
            Check("control API absent plugin is unavailable", ApiAbsentPluginIsUnavailable());
            Check("control API consumes accepted request immediately", ApiQueuesAcceptedRequest());
            Check("accepted Hunt Camp handoff replaces Relax", ApiReplacesRelax());
            Check("rejected Hunt Camp handoff preserves Relax", ApiRejectedRequestKeepsRelax());
            Check("control API rejects active camp", ApiRejectsActiveCamp());
            Check("control API rejects unreadable observation", ApiRejectsUnreadableObservation());
            Check("control API rejects missing party", ApiRejectsMissingParty());

            Console.WriteLine(_failures == 0
                ? "Campmaster control API deterministic tests: ALL PASS"
                : "Campmaster control API deterministic tests: " + _failures + " FAIL");
            return _failures == 0 ? 0 : 1;
        }

        private static CampmasterDeclareHereDecision Evaluate(bool ready, bool active, bool present, bool readable, bool party,
            bool localParty, bool raid, bool anchor)
        {
            return CampmasterControlPolicy.Evaluate(ready, active, present, readable, party, localParty, raid, anchor);
        }

        private static bool RejectionsHaveReasons()
        {
            CampmasterDeclareHereDecision[] rejected =
            {
                CampmasterDeclareHereDecision.NotReady,
                CampmasterDeclareHereDecision.AlreadyActive,
                CampmasterDeclareHereDecision.ObservationUnavailable,
                CampmasterDeclareHereDecision.NoParty,
                CampmasterDeclareHereDecision.NoLocalParty,
                CampmasterDeclareHereDecision.RaidActive,
                CampmasterDeclareHereDecision.MissingAnchor
            };
            for (int i = 0; i < rejected.Length; i++)
                if (string.IsNullOrWhiteSpace(CampmasterControlPolicy.FailureMessage(rejected[i]))) return false;
            return CampmasterControlPolicy.FailureMessage(CampmasterDeclareHereDecision.Accepted) == null;
        }

        private static bool ApiAbsentPluginIsUnavailable()
        {
            CampmasterPlugin.Instance = null;
            return !CampmasterControlApi.IsAvailable;
        }

        private static bool ApiQueuesAcceptedRequest()
        {
            CampSessionTracker tracker = NewTracker(false);
            CampmasterPlugin.Instance = new CampmasterPlugin { Tracker = tracker };
            NativeGroupStateReader.Observation = FreshObservation(true, true, false, true);
            string failure;
            bool accepted = CampmasterControlApi.TryDeclareHere(out failure);
            return accepted && failure == null && tracker.RequestCount == 1 && tracker.TickCount == 1 &&
                   tracker.LastRequestedPosition.HasValue && tracker.LastRequestedPosition.Value.X == 5f && tracker.IsActive;
        }

        private static bool ApiReplacesRelax()
        {
            CampSessionTracker tracker = NewTracker(false);
            RelaxSessionTracker relax = new RelaxSessionTracker { IsActive = true };
            CampmasterPlugin.Instance = new CampmasterPlugin { Tracker = tracker, RelaxTracker = relax };
            NativeGroupStateReader.Observation = FreshObservation(true, true, false, true);
            string failure;
            bool accepted = CampmasterControlApi.TryDeclareHere(out failure);
            return accepted && failure == null && tracker.IsActive && !relax.IsActive && relax.StopCount == 1;
        }

        private static bool ApiRejectedRequestKeepsRelax()
        {
            CampSessionTracker tracker = NewTracker(false);
            RelaxSessionTracker relax = new RelaxSessionTracker { IsActive = true };
            CampmasterPlugin.Instance = new CampmasterPlugin { Tracker = tracker, RelaxTracker = relax };
            NativeGroupStateReader.Observation = FreshObservation(false, false, false, true);
            string failure;
            bool accepted = CampmasterControlApi.TryDeclareHere(out failure);
            return !accepted && !string.IsNullOrWhiteSpace(failure) && relax.IsActive && relax.StopCount == 0 && !tracker.IsActive;
        }

        private static bool ApiRejectsActiveCamp()
        {
            CampSessionTracker tracker = NewTracker(true);
            CampmasterPlugin.Instance = new CampmasterPlugin { Tracker = tracker };
            NativeGroupStateReader.Observation = FreshObservation(true, true, false, true);
            string failure;
            return !CampmasterControlApi.TryDeclareHere(out failure) && tracker.RequestCount == 0 &&
                   failure == "A hunt camp is already active.";
        }

        private static bool ApiRejectsUnreadableObservation()
        {
            CampSessionTracker tracker = NewTracker(false);
            CampmasterPlugin.Instance = new CampmasterPlugin { Tracker = tracker };
            NativeGroupStateReader.Observation = FreshObservation(true, true, false, true);
            NativeGroupStateReader.Observation.ReadSucceeded = false;
            string failure;
            return !CampmasterControlApi.TryDeclareHere(out failure) && tracker.RequestCount == 0 &&
                   !string.IsNullOrWhiteSpace(failure);
        }

        private static bool ApiRejectsMissingParty()
        {
            CampSessionTracker tracker = NewTracker(false);
            CampmasterPlugin.Instance = new CampmasterPlugin { Tracker = tracker };
            NativeGroupStateReader.Observation = FreshObservation(false, false, false, true);
            string failure;
            return !CampmasterControlApi.TryDeclareHere(out failure) && tracker.RequestCount == 0 &&
                   failure == "No party is currently detected.";
        }

        private static CampSessionTracker NewTracker(bool active)
        {
            CampSessionTracker tracker = new CampSessionTracker();
            tracker.IsActive = active;
            return tracker;
        }

        private static CampObservation FreshObservation(bool party, bool localParty, bool raid, bool anchor)
        {
            return new CampObservation
            {
                ReadSucceeded = true,
                HasParty = party,
                LocalResolvedMembers = localParty ? 1 : 0,
                RaidActive = raid,
                PlayerPosition = anchor ? (CampVector3?)new CampVector3(5f, 0f, 7f) : null
            };
        }

        private static void Check(string name, bool passed)
        {
            Console.WriteLine((passed ? "PASS  " : "FAIL  ") + name);
            if (!passed) _failures++;
        }
    }

    // Minimal stubs let the standalone harness compile the production
    // CampmasterControlApi itself without Unity/BepInEx/game assemblies.
    internal sealed class CampmasterPlugin
    {
        internal static CampmasterPlugin Instance;
        internal CampSessionTracker Tracker { get; set; }
        internal CampObservation LastObservation { get; set; }
        internal RelaxSessionTracker RelaxTracker { get; set; }
    }

    internal sealed class RelaxSessionTracker
    {
        internal bool IsActive { get; set; }
        internal int StopCount { get; private set; }
        private bool _pendingStop;

        internal void RequestStop(string reason)
        {
            StopCount++;
            _pendingStop = true;
        }

        internal void Tick(CampObservation observation, DateTime nowUtc)
        {
            if (_pendingStop)
            {
                _pendingStop = false;
                IsActive = false;
            }
        }
    }

    internal sealed class CampSessionTracker
    {
        internal bool IsActive { get; set; }
        internal int RequestCount { get; private set; }
        internal int TickCount { get; private set; }
        internal CampVector3? LastRequestedPosition { get; private set; }

        internal void RequestDeclareHere(CampVector3? playerPosition)
        {
            RequestCount++;
            LastRequestedPosition = playerPosition;
        }

        internal void Tick(CampObservation observation, DateTime nowUtc)
        {
            TickCount++;
            if (observation != null && observation.ReadSucceeded && observation.HasParty &&
                observation.LocalResolvedMembers > 0 && observation.RaidActive != true &&
                LastRequestedPosition.HasValue) IsActive = true;
        }
    }

    internal sealed class CampObservation
    {
        internal bool ReadSucceeded;
        internal bool HasParty;
        internal int LocalResolvedMembers;
        internal bool? RaidActive;
        internal CampVector3? PlayerPosition;
    }

    internal struct CampVector3
    {
        internal readonly float X;
        internal readonly float Y;
        internal readonly float Z;

        internal CampVector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    internal static class NativeGroupStateReader
    {
        internal static CampObservation Observation;
        internal static CampObservation Read(DateTime nowUtc) { return Observation; }
    }

    public static class CampmasterApi
    {
        public static bool IsHuntCampActive
        {
            get
            {
                return CampmasterPlugin.Instance != null && CampmasterPlugin.Instance.Tracker != null &&
                       CampmasterPlugin.Instance.Tracker.IsActive;
            }
        }
    }
}
