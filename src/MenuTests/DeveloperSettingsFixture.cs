using BepInEx.Configuration;

namespace Killdg.DeveloperTools
{
    // This .NET-only harness tests menu values/actions without booting Unity's
    // Chainloader. Bind to its isolated temporary ConfigFile; actual optional-tool
    // discovery and migration are integration behavior, not verified by this fixture.
    internal static class DeveloperSettings
    {
        internal static ConfigEntry<T> Bind<T>(
            ConfigFile config,
            string section,
            string key,
            T value,
            ConfigDescription description)
        {
            return config.Bind(section, key, value, description);
        }
    }
}
