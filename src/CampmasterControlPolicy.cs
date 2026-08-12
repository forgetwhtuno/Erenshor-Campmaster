namespace ErenshorCampmaster
{
    internal enum CampmasterDeclareHereDecision
    {
        Accepted = 0,
        NotReady = 1,
        AlreadyActive = 2,
        ObservationUnavailable = 3,
        NoParty = 4,
        NoLocalParty = 5,
        RaidActive = 6,
        MissingAnchor = 7
    }

    // Unity-free admission policy shared by the optional control API and its
    // deterministic tests. It decides only whether Campmaster may queue its
    // own existing declare-here intent; it does not mutate native game state.
    internal static class CampmasterControlPolicy
    {
        internal static CampmasterDeclareHereDecision Evaluate(
            bool trackerReady,
            bool huntCampActive,
            bool observationPresent,
            bool observationReadSucceeded,
            bool hasParty,
            bool hasLocalParty,
            bool raidActive,
            bool anchorAvailable)
        {
            if (!trackerReady) return CampmasterDeclareHereDecision.NotReady;
            if (huntCampActive) return CampmasterDeclareHereDecision.AlreadyActive;
            if (!observationPresent || !observationReadSucceeded) return CampmasterDeclareHereDecision.ObservationUnavailable;
            if (!hasParty) return CampmasterDeclareHereDecision.NoParty;
            if (!hasLocalParty) return CampmasterDeclareHereDecision.NoLocalParty;
            if (raidActive) return CampmasterDeclareHereDecision.RaidActive;
            if (!anchorAvailable) return CampmasterDeclareHereDecision.MissingAnchor;
            return CampmasterDeclareHereDecision.Accepted;
        }

        internal static string FailureMessage(CampmasterDeclareHereDecision decision)
        {
            switch (decision)
            {
                case CampmasterDeclareHereDecision.NotReady:
                    return "Campmaster is not ready.";
                case CampmasterDeclareHereDecision.AlreadyActive:
                    return "A hunt camp is already active.";
                case CampmasterDeclareHereDecision.ObservationUnavailable:
                    return "Campmaster cannot read party state right now; try again in a moment.";
                case CampmasterDeclareHereDecision.NoParty:
                    return "No party is currently detected.";
                case CampmasterDeclareHereDecision.NoLocalParty:
                    return "No locally resolved Sim party is currently available.";
                case CampmasterDeclareHereDecision.RaidActive:
                    return "Hunt Camp declaration is unavailable while a raid is active.";
                case CampmasterDeclareHereDecision.MissingAnchor:
                    return "Campmaster cannot verify the player's current position.";
                default:
                    return null;
            }
        }
    }
}
