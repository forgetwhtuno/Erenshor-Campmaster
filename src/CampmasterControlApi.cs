using System;

namespace ErenshorCampmaster
{
    // Narrow OPTIONAL control surface for explicit player-invoked handoffs
    // from companion mods such as Erenshor Follow / Sim-Led Expeditions.
    //
    // CampmasterApi remains the read-only integration surface. This optional
    // control API may only submit Campmaster's existing context declaration
    // intent against one fresh observation; CampSessionTracker.Tick remains
    // the sole owner of establishing the session. No native roles, Auto Pull,
    // targets, movement, combat,
    // inventory, or saves are changed here.
    public static class CampmasterControlApi
    {
        public const int SchemaVersion = 1;

        public static bool IsAvailable
        {
            get
            {
                CampmasterPlugin plugin = CampmasterPlugin.Instance;
                return plugin != null && plugin.Tracker != null;
            }
        }

        public static bool IsHuntCampActive
        {
            get { return CampmasterApi.IsHuntCampActive; }
        }

        public static bool TryDeclareHere(out string failure)
        {
            failure = null;

            CampmasterPlugin plugin = CampmasterPlugin.Instance;
            CampSessionTracker tracker = plugin == null ? null : plugin.Tracker;
            DateTime nowUtc = DateTime.UtcNow;
            CampObservation observation = plugin == null ? null : NativeGroupStateReader.Read(nowUtc);
            CampmasterDeclareHereDecision decision = CampmasterControlPolicy.Evaluate(
                tracker != null,
                tracker != null && tracker.IsActive,
                observation != null,
                observation != null && observation.ReadSucceeded,
                observation != null && observation.HasParty,
                observation != null && observation.LocalResolvedMembers > 0,
                observation != null && observation.RaidActive == true,
                observation != null && observation.PlayerPosition.HasValue);

            if (decision != CampmasterDeclareHereDecision.Accepted)
            {
                failure = CampmasterControlPolicy.FailureMessage(decision);
                return false;
            }

            if (plugin.RelaxTracker != null && plugin.RelaxTracker.IsActive)
            {
                plugin.RelaxTracker.RequestStop("replaced by Hunt Camp control declaration");
                plugin.RelaxTracker.Tick(observation, nowUtc);
                if (plugin.RelaxTracker.IsActive)
                {
                    failure = "Campmaster could not resolve the active Relax session.";
                    return false;
                }
            }

            try
            {
                // Consume the bounded request against the exact same fresh
                // observation that admitted it. There is no deferred request
                // left behind to fire after zoning or a later Follow arrival.
                tracker.RequestDeclareHere(observation.PlayerPosition);
                tracker.Tick(observation, nowUtc);
                if (!tracker.IsActive)
                {
                    failure = "Campmaster could not establish the Hunt Camp from the fresh local party state.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                failure = "Campmaster rejected the handoff: " + ex.Message;
                return false;
            }
        }
    }
}
