namespace RaidRoulette.TestingClient.DecayEngine
{
    // Original killdg lighting engine source, compiled into RR independently.
    // This adapter supplies RR event state; no standalone mod DLL is required.
    internal static class DecayEngineContext
    {
        internal static bool PowerOut => InfrastructureDecayDirector.PowerOut;
        internal static bool BlockLightControls => true;
        internal static bool BlockExtractControls => true;
        internal static bool BlockOtherPoweredControls => true;

        internal static void Trace(string message)
        {
            Plugin.Trace(message);
        }

        internal static void ExplainBlockedControl()
        {
            InfrastructureDecayDirector.ExplainBlockedControl();
        }
    }
}
