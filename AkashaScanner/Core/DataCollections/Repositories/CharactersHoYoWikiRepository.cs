using AkashaScanner.Core.Dtos.Character;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace AkashaScanner.Core.DataCollections.Repositories
{
    public class CharactersHoYoWikiRepository : HoYoWikiRepository<CharacterEntry>
    {
        private static readonly Regex RemoveHtmlTags = new(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex FindTalentLevelUpConstellation = new(@"Increases\s+the\s+Level\s+of\s*(.*)\s*by\s+(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private const int TalentNameScore = 90;

        private readonly HttpClient client;
        private readonly Dictionary<string, CharacterEntry> missingEntries;
        private readonly ILogger<CharactersHoYoWikiRepository> Logger;

        public CharactersHoYoWikiRepository(ILogger<CharactersHoYoWikiRepository> logger)
        {
            Logger = logger;
            client = CreateClient();
            missingEntries = new Dictionary<string, CharacterEntry>();
        }

        private async Task<List<Item>?> GetCharactersList()
        {
            Logger.LogInformation("Loading characters");

            var resp = await LoadEntryPageList<Item>(client, "2");
            if (resp is null)
            {
                Logger.LogError("Failed to load character list.");
                return null;
            }
            return resp;
        }

        public override async Task<List<CharacterEntry>?> Load()
        {
            var output = new List<CharacterEntry>();
            var charactersList = await GetCharactersList() ?? new();

            foreach (var character in charactersList)
            {
                var entry = await LoadCharacterEntry(character);
                if (entry is not null)
                {
                    output.Add(entry);
                }
            }

            Logger.LogInformation("Characters loaded successfully.");
            return output;
        }

        private async Task<CharacterEntry?> LoadCharacterEntry(Item character)
        {
            Logger.LogInformation("Loading character: {name}", character.name);

            var detail = await LoadEntryPage<Detail>(client, character.entry_page_id);
            if (detail is null)
            {
                Logger.LogError("Failed to load detail for {name}", character.name);
                return null;
            }

            var characterInfo = FilteredData(character.name, detail.filter_values);
            var talents = ExtractTalents(detail);
            if (talents is null)
            {
                Logger.LogError("Failed to extract talents for {name}", character.name);
                return null;
            }

            var skillName = talents.Find(t => t.Type == TalentType.Skill)?.Name;
            var burstName = talents.Find(t => t.Type == TalentType.Burst)?.Name;

            var constellations = ExtractConstellations(detail, skillName, burstName);

            var iconPath = IconRepository.GetPath("Characters", character.name, Path.GetExtension(character.icon_url));
            await IconRepository.SaveUrlAsIcon(client, character.icon_url, iconPath);

            return new CharacterEntry
            {
                Name = character.name.Trim(),
                Rarity = characterInfo.Rarity,
                Element = characterInfo.Element,
                WeaponType = characterInfo.WeaponType,
                IsTraveler = character.name.Contains("Traveler"),
                Icon = iconPath,
                Talents = talents,
                Constellations = constellations
            };
        }

        private CharacterEntry FilteredData(string name, FilterValues filter)
        {
            if (missingEntries.TryGetValue(name, out var cachedEntry))
                return cachedEntry;

            var rarity = ParseRarity(filter.character_rarity?.values?.FirstOrDefault());
            var element = ParseElement(filter.character_vision?.values?.FirstOrDefault());
            var weaponType = ParseWeaponType(filter.character_weapon?.values?.FirstOrDefault());

            return new CharacterEntry
            {
                Rarity = rarity,
                Element = element,
                WeaponType = weaponType,
            };
        }

        private int ParseRarity(string? rarityStr) =>
            int.TryParse(rarityStr?.Replace("-Star", ""), out var rarity) ? rarity : 4;

        private Element ParseElement(string? element) => element?.ToLowerInvariant() switch
        {
            "anemo" => Element.Anemo,
            "pyro" => Element.Pyro,
            "cryo" => Element.Cryo,
            "electro" => Element.Electro,
            "hydro" => Element.Hydro,
            "geo" => Element.Geo,
            "dendro" => Element.Dendro,
            _ => Element.Invalid,
        };

        private WeaponType ParseWeaponType(string? weapon) => weapon?.ToLowerInvariant() switch
        {
            "bow" => WeaponType.Bow,
            "catalyst" => WeaponType.Catalyst,
            "polearm" => WeaponType.Polearm,
            "sword" => WeaponType.Sword,
            "claymore" => WeaponType.Claymore,
            _ => WeaponType.Invalid,
        };

        private List<CharacterEntry.Talent>? ExtractTalents(Detail detail)
        {
            var talentsComp = detail.modules
                .Find(m => m.name == "Talents")
                ?.components.Find(c => c.component_id == "talent");

            if (string.IsNullOrEmpty(talentsComp?.data))
                return null;

            var talentsData = JsonConvert.DeserializeObject<TalentData>(talentsComp.data.ToLower())?.list;
            if (talentsData == null) return null;

            return talentsData.Select(data =>
            {
                var name = data.title.Trim();
                var attr = data.attributes;

                if (attr?.Find(a => a.key.Equals("level") || a.key == "")?.values.Count >= 10)
                {
                    var hasCD = attr.Find(a => a.key.Contains("cd", StringComparison.OrdinalIgnoreCase));
                    var hasEnergy = attr.Find(a => a.key.Equals("energy cost", StringComparison.OrdinalIgnoreCase));
                    var hasFightingSpirit = attr.Find(a => a.key.Equals("fighting spirit limit", StringComparison.OrdinalIgnoreCase));

                    if (hasCD is not null)
                    {
                        if (name.Equals("havoc: ruin", StringComparison.InvariantCultureIgnoreCase) || hasEnergy is not null || hasFightingSpirit is not null)
                            return new CharacterEntry.Talent { Name = name, Type = TalentType.Burst };
                        return new CharacterEntry.Talent { Name = name, Type = TalentType.Skill };
                    }
                    return new CharacterEntry.Talent { Name = name, Type = TalentType.Attack };
                }

                return new CharacterEntry.Talent { Name = name, Type = TalentType.Other };
            }).ToList();
        }

        private List<CharacterEntry.Constellation> ExtractConstellations(Detail detail, string? skillName, string? burstName)
        {
            var consComp = detail.modules
                .Find(m => m.name == "Constellation")
                ?.components.Find(c => c.component_id == "summaryList");

            if (string.IsNullOrEmpty(consComp?.data))
                return new();

            var constellationsData = JsonConvert.DeserializeObject<ConstellationData>(consComp.data)?.list ?? new();

            return constellationsData.Select(data =>
            {
                var name = data.name.Trim();
                var description = RemoveHtmlTags.Replace(data.desc, string.Empty);
                var match = FindTalentLevelUpConstellation.Match(description);

                if (match.Success && int.TryParse(match.Groups[2].Value, out int levelUp))
                {
                    var talentName = match.Groups[1].Value.Trim();
                    var skillScore = skillName?.FuzzySearch(talentName) ?? 0;
                    var burstScore = burstName?.FuzzySearch(talentName) ?? 0;

                    if (skillScore >= TalentNameScore && skillScore > burstScore)
                    {
                        return new CharacterEntry.Constellation { Name = name, SkillLevel = levelUp };
                    }
                    if (burstScore >= TalentNameScore)
                    {
                        return new CharacterEntry.Constellation { Name = name, BurstLevel = levelUp };
                    }
                }

                return new CharacterEntry.Constellation { Name = name };
            }).ToList();
        }
    }
}
