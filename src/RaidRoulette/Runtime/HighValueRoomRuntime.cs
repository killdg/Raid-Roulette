using RaidRoulette.Services;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;

namespace RaidRoulette.Runtime
{
    public static class HighValueRoomRuntime
    {
        private static TemplateTable? templates;
        private static EventArsenal? arsenal;
        public static void Initialize(TemplateTable items, GlobalTable globals, ICloner cloner)
        {
            templates = items;
            arsenal = new EventArsenal(items, globals, cloner);
        }

        public static object GetKits(MongoId session)
        {
            if (RaidRulesRuntime.Get(session)?.EventId != "high_value_loot" || templates == null || arsenal == null)
            {
                return new
                {
                    ok = false,
                    error = "No active Locked Loot raid."
                };
            }

            var kits = new List<List<Item>>();
            // Explicit native-template allowlist: avoid quest-only variants and fake items.
            // These IDs are resolved against the installed SPT item database before use.
            foreach (string id in new[]
            {
                "59faff1d86f7746c51718c9c", // Physical bitcoin
                "619cbf7d23893217ec30b689", // Injector case
                "5b7c710788a4506dec015957", // Lucky Scav Junk box
                "5d235bb686f77443f4331278", // SICC organizational pouch
                "567143bf4bdc2d1a0f8b4567", // Pistol case
                "5c093db286f7740a1b2617e3", // Holodilnick food case
                "57347ca924597744596b4e71", // Graphics card
                "5c0126f40db834002a125382", // Red Rebel ice pick
                "5c0530ee86f774697952d952", // LEDX
                "5c05300686f7746dce784e5d", // VPX flash storage
                "5d0378d486f77420421a5ff4", // Military power filter
                "5c12613b86f7743bbe2c3f76", // Intelligence folder
                "6389c8c5dbfd5e4b95197e6b", // TerraGroup Blue Folders
                "590c60fc86f77412b13fddcf", // Documents case
                "59fb016586f7746d0d4b423a", // Money case
                "59fb023c86f7746d0d4b423c", // Weapon case
                "59fb042886f7746c5005a7b2", // Item case
                "5aafbcd986f7745e590fff23", // Medicine case
                "5aafbde786f774389d0cbc0f", // Ammunition case
                "5b6d9ce188a4501afc1b2b25", // THICC weapon case
                "5c0a840b86f7742ffa4f2482", // THICC item case
                "5c093e3486f77430cb02e593", // Dogtag case
                "5c127c4486f7745625356c13", // Magazine case
                "5e2af55f86f7746d4159f07c", // Grenade case
                "619cbf9e0a7c3a1a2731940a", // Keycard holder
                "67600929bd0a0549d70993f6", // Ballistic plate case
                "67d3ed3271c17ff82e0a5b0b", // Key case
            }

            )
            {
                if (templates.Items.ContainsKey(new MongoId(id)))
                {
                    kits.Add([new Item { Id = new MongoId(), Template = new MongoId(id), Upd = new() { SpawnedInSession = true,
                         StackObjectsCount = 1 } }]);
                }
            }

            if (arsenal.Available("ak50"))
            {
                kits.Add(arsenal.Kit("ak50"));
            }

            return new
            {
                ok = true,
                kits,
                // Native SimpleContainer templates cover cases and pouches, not backpacks
                // or secure containers. The client applies these IDs only to its local bag.
                backpackCaseTemplates = templates.Items.Where(entry => (entry.Value.Parent
                    .ToString() == "5795f317245977243854e041" || entry.Key.ToString() == "567143bf4bdc2d1a0f8b4567") && entry.Value
                    .Properties?.QuestItem != true).Select(entry => entry.Key.ToString()).ToArray()
            };
        }
    }
}
