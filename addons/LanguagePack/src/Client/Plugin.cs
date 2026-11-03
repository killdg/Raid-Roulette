using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using EFT;
using HarmonyLib;
using Newtonsoft.Json;
using RaidRoulette.LanguagePack;
using UnityEngine;

namespace RaidRoulette.LanguagePackClient
{
    [BepInPlugin("com.killdg.raidroulette.languages.client", "Raid Roulette Language Pack", "1.0.0")]
    [BepInDependency("com.killdg.raidroulette.testing", "2.0.146")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static CatalogStore Catalogs = null!;
        internal static string Language = "en";
        [ThreadStatic]
        internal static int DrawDepth;
        private Harmony? harmony;
        private Action? unsubscribe;
        private static FieldInfo? noticeEventId;
        private static FieldInfo? badgeTooltip;

        private void Start()
        {
            try
            {
                string directory = Path.GetDirectoryName(GetType().Assembly.Location)!;
                Catalogs = new CatalogStore(Path.Combine(directory, "Locales"), value =>
                    JsonConvert.DeserializeObject<LocaleCatalog>(value)
                    ?? throw new InvalidDataException("Invalid language catalog."));
                RefreshLanguage();
                unsubscribe = LocalizationManager.Instance.AddLocaleUpdateListener(RefreshLanguage);
                harmony = new Harmony("com.killdg.raidroulette.languages.client");
                InstallHooks();
                Logger.LogInfo("Offline RR translations loaded. Untranslated text uses English.");
            }
            catch (Exception exception)
            {
                harmony?.UnpatchSelf();
                unsubscribe?.Invoke();
                unsubscribe = null;
                Logger.LogError("Language pack disabled; RR remains unchanged: " + exception.Message);
            }
        }

        private static void RefreshLanguage()
        {
            Language = CatalogStore.Normalize(LocalizationManager.Instance.Culture);
        }

        private void InstallHooks()
        {
            // These hooks affect presentation only. No inventory, raid state,
            // quest state, saved configuration keys or network routes are changed.
            Type notice = AccessTools.TypeByName("RaidRoulette.TestingClient.RaidEventNotice");
            if (notice != null)
            {
                noticeEventId = AccessTools.Field(notice, "eventId");
                harmony!.Patch(AccessTools.Method(notice, "Consume"),
                    postfix: new HarmonyMethod(typeof(Plugin), nameof(NoticePostfix)));
            }

            Type badge = AccessTools.TypeByName("RaidRoulette.TestingClient.FenceLocationBadge");
            if (badge != null)
            {
                badgeTooltip = AccessTools.Field(badge, "tooltipText");
                harmony!.Patch(AccessTools.Method(badge, "OnPointerEnter"),
                    prefix: new HarmonyMethod(typeof(Plugin), nameof(BeginTooltip)),
                    finalizer: new HarmonyMethod(typeof(Plugin), nameof(EndTooltip)));
            }

            string[] drawTypes =
            {
                "RaidRoulette.TestingClient.Plugin",
                "RaidRoulette.TestingClient.RaidEventPicker",
                "RaidRoulette.TestingClient.RaidChanceSettings",
                "RaidRoulette.TestingClient.GunGameRespawn"
            };
            string[] drawMethods = { "OnGUI", "Draw", "DrawMix", "Draw" };
            for (int index = 0; index < drawTypes.Length; index++)
            {
                Type type = AccessTools.TypeByName(drawTypes[index]);
                MethodInfo? method = type == null ? null : AccessTools.Method(type, drawMethods[index]);
                if (method != null)
                {
                    harmony!.Patch(method,
                        prefix: new HarmonyMethod(typeof(Plugin), nameof(BeginDraw)),
                        finalizer: new HarmonyMethod(typeof(Plugin), nameof(EndDraw)));
                }
            }

            // Scope is entered only by RR's own draw calls. Other mods and the
            // native UI pass through these hooks without string lookup or scans.
            foreach (Type type in new[] { typeof(GUILayout), typeof(GUI) })
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name is "Label" or "Button" or "Toggle"
                        && method.GetParameters().Any(parameter => parameter.ParameterType == typeof(string)))
                    {
                        harmony!.Patch(method,
                            prefix: new HarmonyMethod(typeof(Plugin), nameof(TranslateDrawArguments)));
                    }
                }
            }

            Type settingsType = AccessTools.TypeByName("ConfigurationManager.ConfigSettingEntry");
            ConstructorInfo? constructor = settingsType == null ? null : AccessTools.Constructor(
                settingsType, new[] { typeof(ConfigEntryBase), typeof(BaseUnityPlugin) });
            if (constructor != null)
            {
                harmony!.Patch(constructor,
                    postfix: new HarmonyMethod(typeof(Plugin), nameof(SettingPostfix)));
            }
        }

        private static void NoticePostfix(object __instance, ref string? __result)
        {
            if (__result == null || Language == "en")
            {
                return;
            }

            // The native RR field records the stable event ID before Consume
            // creates the English notification. Never parse or change saved IDs.
            string? id = noticeEventId?.GetValue(__instance) as string;
            if (id != null)
            {
                __result = string.Format(Catalogs.Text(Language, "Raid Event : {0}"),
                    Catalogs.EventName(Language, id));
            }
        }

        private static void BeginTooltip(object __instance, out string? __state)
        {
            __state = badgeTooltip?.GetValue(__instance) as string;
            if (__state != null && Language != "en")
            {
                badgeTooltip?.SetValue(__instance, string.Join("\n",
                    __state.Split('\n').Select(line => Catalogs.Text(Language, line))));
            }
        }

        private static Exception? EndTooltip(object __instance, string? __state, Exception? __exception)
        {
            // Localize only while native tooltip content is created. Restoring
            // the source text prevents RR's refresh loop from closing it again.
            if (__state != null)
            {
                badgeTooltip?.SetValue(__instance, __state);
            }

            return __exception;
        }

        private static void BeginDraw(out int __state)
        {
            __state = DrawDepth;
            DrawDepth++;
        }

        private static Exception? EndDraw(Exception? __exception, int __state)
        {
            // Restore the previous scope even if the original renderer throws.
            DrawDepth = __state;
            return __exception;
        }

        private static void TranslateDrawArguments(object[] __args)
        {
            if (DrawDepth == 0 || Language == "en")
            {
                return;
            }

            for (int index = 0; index < __args.Length; index++)
            {
                if (__args[index] is string value)
                {
                    __args[index] = Catalogs.Text(Language, value);
                }
            }
        }

        private static void SettingPostfix(object __instance, ConfigEntryBase entry, BaseUnityPlugin owner)
        {
            if (owner.Info.Metadata.GUID != "com.killdg.raidroulette.testing" || Language == "en")
            {
                return;
            }

            if (entry.SettingType.IsEnum)
            {
                PropertyInfo output = AccessTools.Property(__instance.GetType(), "ObjToStr");
                PropertyInfo input = AccessTools.Property(__instance.GetType(), "StrToObj");
                var originalOutput = output.GetValue(__instance) as Func<object, string>;
                var originalInput = input.GetValue(__instance) as Func<string, object>;
                if (originalOutput != null && originalInput != null)
                {
                    var reverse = new Dictionary<string, string>();
                    foreach (object value in Enum.GetValues(entry.SettingType))
                    {
                        string original = originalOutput(value);
                        reverse[Catalogs.Text(Language, original)] = original;
                    }

                    output.SetValue(__instance, new Func<object, string>(value =>
                        Catalogs.Text(Language, originalOutput(value))));
                    input.SetValue(__instance, new Func<string, object>(value =>
                        originalInput(reverse.TryGetValue(value, out string original) ? original : value)));
                }
            }

            // Configuration Manager separates display labels from ConfigDefinition.
            // Modify the former only, keeping the user's existing config intact.
            foreach (string name in new[] { "DispName", "Description", "Category" })
            {
                PropertyInfo? property = AccessTools.Property(__instance.GetType(), name);
                if (property?.GetValue(__instance) is string original)
                {
                    property.SetValue(__instance, Catalogs.Text(Language, original));
                }
            }
        }

        private void OnDestroy()
        {
            unsubscribe?.Invoke();
            harmony?.UnpatchSelf();
        }
    }
}
