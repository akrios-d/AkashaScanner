using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AkashaScanner.Core.DataCollections.Repositories
{
    public class ArtifactsHoYoWikiRepository : HoYoWikiRepository<ArtifactEntry>
    {
        private readonly ILogger<ArtifactsHoYoWikiRepository> _logger;
        private const string ArtifactCategoryId = "5";
        private const string ArtifactModuleName = "Set";
        private const string ArtifactComponentId = "artifact_list";

        public ArtifactsHoYoWikiRepository(ILogger<ArtifactsHoYoWikiRepository> logger)
        {
            _logger = logger;
        }

        public override async Task<List<ArtifactEntry>?> Load()
        {
            using var client = CreateClient();
            _logger.LogInformation("Loading artifacts...");

            var items = await LoadEntryPageList<Item>(client, ArtifactCategoryId);
            if (items is null)
            {
                _logger.LogError("Failed to load artifact list.");
                return null;
            }

            var output = new List<ArtifactEntry>();

            foreach (var item in items)
            {
                var setName = item.name.Trim();
                _logger.LogInformation("Processing artifact set: '{SetName}'", setName);

                var detail = await LoadEntryPage<Detail>(client, item.entry_page_id);
                if (detail is null)
                {
                    _logger.LogError("Failed to load details for set: '{SetName}'", setName);
                    continue;
                }

                var module = detail.modules.FirstOrDefault(m => m.name == ArtifactModuleName);
                var component = module?.components.FirstOrDefault(c => c.component_id == ArtifactComponentId);

                if (component is null)
                {
                    _logger.LogError("Artifact list component missing for set: '{SetName}'", setName);
                    continue;
                }

                var data = JsonConvert.DeserializeObject<Data>(component.data);
                if (data is null)
                {
                    _logger.LogError("Failed to parse artifact data for set: '{SetName}'", setName);
                    continue;
                }

                await LoadSingleArtifact(client, data.flower_of_life, ArtifactSlot.Flower, setName, output);
                await LoadSingleArtifact(client, data.plume_of_death, ArtifactSlot.Plume, setName, output);
                await LoadSingleArtifact(client, data.sands_of_eon, ArtifactSlot.Sands, setName, output);
                await LoadSingleArtifact(client, data.goblet_of_eonothem, ArtifactSlot.Goblet, setName, output);
                await LoadSingleArtifact(client, data.circlet_of_logos, ArtifactSlot.Circlet, setName, output);
            }

            _logger.LogInformation("Artifact loading complete. Total artifacts: {Count}", output.Count);
            return output;
        }

        private async Task LoadSingleArtifact(HttpClient client, DataContent data, ArtifactSlot slot, string setName, List<ArtifactEntry> output)
        {
            var name = data.title?.Trim();
            if (string.IsNullOrEmpty(name))
                return;

            var iconPath = IconRepository.GetPath("Artifacts", name, Path.GetExtension(data.icon_url));
            await IconRepository.SaveUrlAsIcon(client, data.icon_url, iconPath);

            var entry = new ArtifactEntry
            {
                Name = name,
                Icon = iconPath,
                Slot = slot,
                SetName = setName
            };

            entry = FixWikiErrors(entry);

            if (entry.Slot == ArtifactSlot.Invalid)
            {
                _logger.LogWarning("Invalid artifact slot detected for: '{Name}' in set '{SetName}'", entry.Name, setName);
                return;
            }

            output.Add(entry);
        }

        private static ArtifactEntry FixWikiErrors(ArtifactEntry entry)
        {
            return entry.Name.ToLowerInvariant() switch
            {
                "bloom times" => entry with { Slot = ArtifactSlot.Flower },
                "plume of luxury" => entry with { Slot = ArtifactSlot.Plume },
                "song of life" => entry with { Slot = ArtifactSlot.Sands },
                "calabash of awakening" => entry with { Slot = ArtifactSlot.Goblet },
                "skeletal hat" => entry with { Slot = ArtifactSlot.Circlet },
                "a rhyton fired with copper as the base, that was once filled with fine wine from paradise." =>
                    entry with { Slot = ArtifactSlot.Circlet, Name = "Whimsical Dance of the Withered" },
                "poem passed down from days past" =>
                    entry with { Slot = ArtifactSlot.Circlet, Name = "Poetry Of Days Past" },
                "myths of the night realm" when entry.Slot == ArtifactSlot.Flower =>
                    entry with { Slot = ArtifactSlot.Flower, Name = "Reckoning of the Xenogenic" },
                _ => entry
            };
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
