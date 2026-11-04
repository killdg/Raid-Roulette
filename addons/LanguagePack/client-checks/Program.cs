using System.Reflection;
using System.Runtime.Loader;

namespace RaidRoulette.LanguagePackClientChecks
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            string root = args[0];
            string[] folders =
            {
                Path.Combine(root, "BepInEx/core"),
                Path.Combine(root, "EscapeFromTarkov_Data/Managed"),
                Path.GetDirectoryName(Path.GetFullPath(args[1]))!
            };
            // Resolve local native references for type loading only. Do not
            // instantiate a Unity component or run plugin startup/game code.
            AssemblyLoadContext.Default.Resolving += (_, name) =>
            {
                foreach (string folder in folders)
                {
                    string path = Path.Combine(folder, name.Name + ".dll");
                    if (File.Exists(path))
                    {
                        return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                    }
                }

                return null;
            };
            Assembly addon = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[1]));
            Type plugin = addon.GetType("RaidRoulette.LanguagePackClient.Plugin", true)!;
            MethodInfo postfix = plugin.GetMethod("SettingPostfix", BindingFlags.Static | BindingFlags.NonPublic)!;
            FieldInfo language = plugin.GetField("Language", BindingFlags.Static | BindingFlags.NonPublic)!;
            string[] codes =
            {
                "en", "ch", "cz", "es", "es-mx", "fr", "ge", "hu", "it",
                "jp", "kr", "pl", "po", "ro", "ru", "sk", "tu"
            };
            foreach (string code in codes)
            {
                language.SetValue(null, code);
                // Configuration Manager uses a null plugin owner for BepInEx
                // core settings. Calling the actual compiled hook reproduces
                // the failing boundary without requiring a running game.
                postfix.Invoke(null, new object?[] { null, null, null });
            }

            Console.WriteLine($"{codes.Length} compiled-client null-owner regression checks passed.");
        }
    }
}
