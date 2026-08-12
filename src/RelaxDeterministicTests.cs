using System;
using System.Collections.Generic;

namespace ErenshorCampmaster
{
    internal static class RelaxDeterministicTests
    {
        internal static List<string> Run()
        {
            List<string> lines = new List<string>();
            DateTime t = new DateTime(2026, 8, 10, 20, 0, 0, DateTimeKind.Utc);
            RelaxConfig cfg = new RelaxConfig { DepartureRadius = 20f, DepartureGraceSeconds = 5, PartyLossGraceSeconds = 5 };
            RelaxSessionTracker tracker = new RelaxSessionTracker(cfg);
            CampObservation safe = Observation("Brasse", false, true, new CampVector3(0, 0, 0));

            tracker.RequestStart(safe.PlayerPosition);
            tracker.Tick(safe, t);
            Add(lines, "explicit start enters Relax", tracker.State == RelaxSessionState.Active && tracker.IsActive);

            CampObservation combat = Observation("Brasse", true, true, new CampVector3(0, 0, 0));
            tracker.Tick(combat, t.AddSeconds(2));
            Add(lines, "combat suspends Relax", tracker.State == RelaxSessionState.SuspendedForCombat);

            CampObservation unknownCombat = Observation("Brasse", false, true, new CampVector3(0, 0, 0));
            unknownCombat.InCombat = null;
            tracker.Tick(unknownCombat, t.AddSeconds(3));
            Add(lines, "unknown combat state does not resume Relax", tracker.State == RelaxSessionState.SuspendedForCombat);

            CampObservation awayClear = Observation("Brasse", false, true, new CampVector3(30, 0, 0));
            tracker.Tick(awayClear, t.AddSeconds(4));
            Add(lines, "combat clear away from anchor stays suspended", tracker.State == RelaxSessionState.SuspendedForCombat);

            tracker.Tick(safe, t.AddSeconds(5));
            Add(lines, "combat clear at anchor resumes Relax", tracker.State == RelaxSessionState.Active);

            CampObservation far = Observation("Brasse", false, true, new CampVector3(50, 0, 0));
            tracker.Tick(far, t.AddSeconds(5));
            tracker.Tick(far, t.AddSeconds(11));
            Add(lines, "sustained anchor departure ends Relax", !tracker.IsActive);

            RelaxSessionTracker unknownDeparture = new RelaxSessionTracker(cfg);
            unknownDeparture.RequestStart(safe.PlayerPosition);
            unknownDeparture.Tick(safe, t);
            unknownDeparture.Tick(far, t.AddSeconds(1));
            CampObservation noPosition = Observation("Brasse", false, true, new CampVector3(0, 0, 0));
            noPosition.PlayerPosition = null;
            unknownDeparture.Tick(noPosition, t.AddSeconds(4));
            unknownDeparture.Tick(far, t.AddSeconds(8));
            Add(lines, "unknown position resets Relax departure grace", unknownDeparture.IsActive);
            unknownDeparture.Tick(far, t.AddSeconds(14));
            Add(lines, "new sustained departure still ends Relax", !unknownDeparture.IsActive);

            RelaxSessionTracker zoning = new RelaxSessionTracker(cfg);
            zoning.RequestStart(safe.PlayerPosition);
            zoning.Tick(safe, t);
            CampObservation otherZone = Observation("Azure", false, true, new CampVector3(0, 0, 0));
            zoning.Tick(otherZone, t.AddSeconds(2));
            Add(lines, "zone change ends Relax", !zoning.IsActive);

            RelaxSessionTracker missing = new RelaxSessionTracker(cfg);
            missing.RequestStart(safe.PlayerPosition);
            missing.Tick(safe, t);
            CampObservation noParty = Observation("Brasse", false, false, new CampVector3(0, 0, 0));
            missing.Tick(noParty, t.AddSeconds(1));
            Add(lines, "brief party churn does not immediately end Relax", missing.IsActive);
            missing.Tick(noParty, t.AddSeconds(7));
            Add(lines, "party loss beyond grace ends Relax", !missing.IsActive);

            RelaxSessionTracker unresolved = new RelaxSessionTracker(cfg);
            unresolved.RequestStart(safe.PlayerPosition);
            unresolved.Tick(safe, t);
            CampObservation unresolvedParty = Observation("Brasse", false, true, new CampVector3(0, 0, 0));
            unresolvedParty.LocalResolvedMembers = 0;
            unresolved.Tick(unresolvedParty, t.AddSeconds(1));
            Add(lines, "brief local-Sim resolution loss uses grace", unresolved.IsActive);
            unresolved.Tick(unresolvedParty, t.AddSeconds(7));
            Add(lines, "local-Sim loss beyond grace ends Relax", !unresolved.IsActive);

            RelaxSessionTracker stop = new RelaxSessionTracker(cfg);
            stop.RequestStart(safe.PlayerPosition);
            stop.Tick(safe, t);
            stop.RequestStop();
            stop.Tick(safe, t.AddSeconds(1));
            Add(lines, "explicit off ends Relax", !stop.IsActive);

            RelaxSessionTracker unsafeStart = new RelaxSessionTracker(cfg);
            unsafeStart.RequestStart(combat.PlayerPosition);
            unsafeStart.Tick(combat, t);
            Add(lines, "cannot establish Relax during verified combat", !unsafeStart.IsActive);

            RelaxSessionTracker events = new RelaxSessionTracker(cfg);
            events.RequestStart(safe.PlayerPosition);
            events.Tick(safe, t);
            events.Tick(combat, t.AddSeconds(1));
            events.Tick(safe, t.AddSeconds(2));
            events.RequestStop();
            events.Tick(safe, t.AddSeconds(3));
            List<RelaxEvent> emitted = events.GetEventsAfter(0);
            bool sequence = emitted.Count == 4 && emitted[0].Type == RelaxEventType.RelaxStarted &&
                emitted[1].Type == RelaxEventType.RelaxSuspended && emitted[2].Type == RelaxEventType.RelaxResumed &&
                emitted[3].Type == RelaxEventType.RelaxEnded && emitted[0].Sequence < emitted[3].Sequence;
            Add(lines, "Relax lifecycle events are ordered", sequence);

            RelaxSessionTracker sessions = new RelaxSessionTracker(cfg);
            sessions.RequestStart(safe.PlayerPosition);
            sessions.Tick(safe, t);
            sessions.RequestStop();
            sessions.Tick(safe, t.AddSeconds(1));
            sessions.RequestStart(safe.PlayerPosition);
            sessions.Tick(safe, t.AddSeconds(2));
            List<RelaxEvent> sessionEvents = sessions.GetEventsAfter(0);
            bool sessionIsolation = sessionEvents.Count == 3 &&
                sessionEvents[0].SessionId == sessionEvents[1].SessionId &&
                sessionEvents[2].SessionId != sessionEvents[0].SessionId;
            Add(lines, "Relax events preserve session identity", sessionIsolation);

            RelaxSessionTracker bounded = new RelaxSessionTracker(cfg);
            bounded.RequestStart(safe.PlayerPosition);
            bounded.Tick(safe, t);
            for (int i = 0; i < 40; i++)
            {
                bounded.Tick(combat, t.AddSeconds(1 + (i * 2)));
                bounded.Tick(safe, t.AddSeconds(2 + (i * 2)));
            }
            List<RelaxEvent> retained = bounded.GetEventsAfter(0);
            Add(lines, "Relax history is bounded with a detectable oldest sequence",
                retained.Count == 64 && bounded.OldestRetainedSequence == retained[0].Sequence && bounded.OldestRetainedSequence > 1);

            return lines;
        }

        private static CampObservation Observation(string zone, bool combat, bool party, CampVector3 player)
        {
            CampObservation obs = new CampObservation();
            obs.ReadSucceeded = true;
            obs.Zone = zone;
            obs.PartyPresent = party;
            if (party)
            {
                obs.PartyNames.Add("Dancer");
                obs.LocalResolvedMembers = 1;
            }
            obs.Authority = CampAuthority.FullLocal;
            obs.InCombat = combat;
            obs.PlayerPosition = player;
            return obs;
        }

        private static void Add(List<string> lines, string name, bool pass)
        {
            lines.Add("[Relax] " + name + ": " + (pass ? "PASS" : "FAIL"));
        }
    }
}
