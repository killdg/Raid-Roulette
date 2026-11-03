using Mono.Cecil;

namespace RaidRoulette.LanguagePackNativeChecks
{
    internal static class Program
    {
        private static int checks;

        private static void Require(bool condition, string message)
        {
            checks++;
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void Main(string[] args)
        {
            string root = args[0];
            using var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.Combine(root, "BepInEx/core"));
            resolver.AddSearchDirectory(Path.Combine(root, "EscapeFromTarkov_Data/Managed"));
            resolver.AddSearchDirectory(Path.Combine(root, "SPT_Runtime"));
            var options = new ReaderParameters { AssemblyResolver = resolver };
            using var game = AssemblyDefinition.ReadAssembly(
                Path.Combine(root, "EscapeFromTarkov_Data/Managed/Assembly-CSharp.dll"), options);
            TypeDefinition localization = game.MainModule.Types.Single(type => type.FullName == "EFT.LocalizationManager");
            Require(localization.Properties.Any(property => property.Name == "Culture"
                && property.PropertyType.FullName == "System.String"), "Native culture property missing.");
            Require(localization.Methods.Any(method => method.Name == "AddLocaleUpdateListener"
                && method.ReturnType.FullName == "System.Action"), "Native locale subscription contract changed.");
            using var server = AssemblyDefinition.ReadAssembly(Path.Combine(root, "SPT_Runtime/SPTarkov.Server.Core.dll"), options);
            TypeDefinition service = server.MainModule.Types.Single(type =>
                type.FullName == "SPTarkov.Server.Core.Services.Locales.LocaleService");
            Require(service.Methods.Any(method => method.Name == "GetLocaleDb"
                && method.Parameters.Count == 1 && method.Parameters[0].Name == "language"
                && method.ReturnType.FullName == "System.Collections.Generic.Dictionary`2<System.String,System.String>"),
                "Server Harmony argument/return contract changed.");
            using var rr = AssemblyDefinition.ReadAssembly(args[1], options);
            foreach ((string name, string method) in new[]
            {
                ("RaidEventNotice", "Consume"),
                ("FenceLocationBadge", "OnPointerEnter"),
                ("Plugin", "OnGUI"),
                ("RaidEventPicker", "Draw"),
                ("RaidChanceSettings", "DrawMix"),
                ("GunGameRespawn", "Draw")
            })
            {
                TypeDefinition type = rr.MainModule.Types.Single(type => type.FullName == "RaidRoulette.TestingClient." + name);
                Require(type.Methods.Count(candidate => candidate.Name == method) == 1, "Missing or ambiguous RR hook: " + name);
                if (name == "RaidEventNotice")
                {
                    Require(type.Fields.Any(field => field.Name == "eventId" && field.FieldType.FullName == "System.String"),
                        "RR event ID field changed.");
                }

                if (name == "FenceLocationBadge")
                {
                    Require(type.Fields.Any(field => field.Name == "tooltipText" && field.FieldType.FullName == "System.String"),
                        "RR map tooltip field changed.");
                }
            }

            using var manager = AssemblyDefinition.ReadAssembly(
                Path.Combine(root, "BepInEx/plugins/spt/ConfigurationManager/ConfigurationManager.dll"), options);
            TypeDefinition setting = manager.MainModule.Types.Single(type => type.FullName == "ConfigurationManager.ConfigSettingEntry");
            Require(setting.Methods.Any(method => method.IsConstructor && method.Parameters.Count == 2
                && method.Parameters[0].Name == "entry" && method.Parameters[1].Name == "owner"),
                "Configuration Manager constructor changed.");
            TypeDefinition settingBase = manager.MainModule.Types.Single(type => type.FullName == "ConfigurationManager.SettingEntryBase");
            foreach (string name in new[] { "DispName", "Description", "Category", "ObjToStr", "StrToObj" })
            {
                Require(settingBase.Properties.Any(property => property.Name == name && property.SetMethod != null),
                    "Configuration Manager presentation setter missing: " + name);
            }

            using var gui = AssemblyDefinition.ReadAssembly(
                Path.Combine(root, "EscapeFromTarkov_Data/Managed/UnityEngine.IMGUIModule.dll"), options);
            foreach (string name in new[] { "UnityEngine.GUI", "UnityEngine.GUILayout" })
            {
                foreach (MethodDefinition method in gui.MainModule.Types.Single(type => type.FullName == name).Methods)
                {
                    if (method.IsPublic && method.IsStatic && method.Name is "Label" or "Button" or "Toggle"
                        && method.Parameters.Any(parameter => parameter.ParameterType.FullName == "System.String"))
                    {
                        Require(method.HasBody, "Native-only GUI method cannot be patched: " + method.FullName);
                    }
                }
            }

            Console.WriteLine($"{checks} native API checks passed; no live UI or gameplay validation performed.");
        }
    }
}
