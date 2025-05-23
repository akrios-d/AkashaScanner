﻿using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AkashaScanner.Core.DataCollections.Repositories
{
    public class ArtifactsHoYoWikiRepository : HoYoWikiRepository<ArtifactEntry>
    {
        public ArtifactsHoYoWikiRepository(ILogger<ArtifactsHoYoWikiRepository> logger)
        {
            Logger = logger;
        }

        public override async Task<List<ArtifactEntry>?> Load()
        {
            using var client = CreateClient();
            Logger.LogInformation("Loading artifacts");

            var resp = await LoadEntryPageList<Item>(client, "5");
            if (resp == null)
            {
                Logger.LogError("Fail to load artifacts");
                return null;
            }

            var output = new List<ArtifactEntry>();

            foreach (var item in resp)
            {
                var setName = item.name.Trim();
                Logger.LogInformation("Loading '{setName}'", setName);

                var result = await LoadEntryPage<Detail>(client, item.entry_page_id);
                if (result == null)
                {
                    Logger.LogError("Fail to load '{setName}'", setName);
                    return null;
                }

                var artifacts = result.modules.Find((m) => m.name == "Set")!.components.Find((c) => c.component_id == "artifact_list")!;
                var data = JsonConvert.DeserializeObject<Data>(artifacts.data)!;

                async Task loadSingle(DataContent data, ArtifactSlot slot)
                {
                    var name = data.title;
                    if (string.IsNullOrEmpty(name))
                    {
                        return;
                    }
                    name = name.Trim();
                    var icon = data.icon_url;
                    var iconPath = IconRepository.GetPath("Artifacts", name, Path.GetExtension(icon));
                    await IconRepository.SaveUrlAsIcon(client, icon, iconPath);
                    var entry = new ArtifactEntry()
                    {
                        Name = name,
                        Icon = iconPath,
                        Slot = slot,
                        SetName = setName,
                    };
                    entry = FixWikiErrors(entry);
                    if (entry.Slot == ArtifactSlot.Invalid)
                    {
                        Logger.LogError("Fail to load '{setName}'", setName);
                        return;
                    }
                    output.Add(entry);

                }

                await loadSingle(data.flower_of_life, ArtifactSlot.Flower);
                await loadSingle(data.plume_of_death, ArtifactSlot.Plume);
                await loadSingle(data.sands_of_eon, ArtifactSlot.Sands);
                await loadSingle(data.goblet_of_eonothem, ArtifactSlot.Goblet);
                await loadSingle(data.circlet_of_logos, ArtifactSlot.Circlet);
            }

            Logger.LogInformation("Artifacts loaded");

            return output;
        }

        // Data is wrong in the wiki, so we fix it here
        private static ArtifactEntry FixWikiErrors(ArtifactEntry entry)
        {
            if (string.Equals(entry.Name, "Bloom Times", StringComparison.OrdinalIgnoreCase))
                return entry with { Slot = ArtifactSlot.Flower };
            else if (string.Equals(entry.Name, "Plume of Luxury", StringComparison.OrdinalIgnoreCase))
                return entry with { Slot = ArtifactSlot.Plume };
            else if (string.Equals(entry.Name, "Song of Life", StringComparison.OrdinalIgnoreCase))
                return entry with { Slot = ArtifactSlot.Sands };
            else if (string.Equals(entry.Name, "Calabash of Awakening", StringComparison.OrdinalIgnoreCase))
                return entry with { Slot = ArtifactSlot.Goblet };
            else if (string.Equals(entry.Name, "Skeletal Hat", StringComparison.OrdinalIgnoreCase))
                return entry with { Slot = ArtifactSlot.Circlet };
            else if (string.Equals(entry.Name, "A rhyton fired with copper as the base, that was once filled with fine wine from paradise.", StringComparison.OrdinalIgnoreCase))
                return entry with { Slot = ArtifactSlot.Circlet, Name = "Whimsical Dance of the Withered" };
            else if (string.Equals(entry.Name, "Poem Passed Down from Days Past", StringComparison.OrdinalIgnoreCase))
                return entry with { Slot = ArtifactSlot.Circlet, Name = "Poetry Of Days Past" };
            else if (string.Equals(entry.Name, "Myths of the Night Realm", StringComparison.OrdinalIgnoreCase) && entry.Slot == ArtifactSlot.Flower)
                return entry with { Slot = ArtifactSlot.Flower, Name = "Reckoning of the Xenogenic" };       

            return entry;
        }

        private record Item
        {
            public string entry_page_id = default!;
            public string name = default!;
        }

        private record Detail
        {
            public List<Module> modules = default!;
        }

        private record Module
        {
            public string name = default!;
            public List<Component> components = default!;
        }

        private record Component
        {
            public string component_id = default!;
            public string data = default!;
        }

        private record Data
        {
            public DataContent flower_of_life = default!;
            public DataContent plume_of_death = default!;
            public DataContent sands_of_eon = default!;
            public DataContent goblet_of_eonothem = default!;
            public DataContent circlet_of_logos = default!;
        }

        public record DataContent
        {
            public string title = default!;
            public string icon_url = default!;
        }
    }
}
