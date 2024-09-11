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

        public CharactersHoYoWikiRepository(ILogger<CharactersHoYoWikiRepository> logger)
        {
            Logger = logger;
            client = CreateClient();
            missingEntries = new Dictionary<string, CharacterEntry>
            {
                {
                    "Mualani",
                    new CharacterEntry()
                    {
                        Element = Element.Hydro,
                        WeaponType = WeaponType.Catalyst,
                        Rarity = 5,
                    }
                },
                {
                    "Kinich",
                    new CharacterEntry()
                    {
                        Element = Element.Dendro,
                        WeaponType = WeaponType.Claymore,
                        Rarity = 5,
                    }
                }
            };
        }

        private async Task<List<Item>?> GetCharactersList()
        {
            Logger.LogInformation("Loading characters");

            var resp = await LoadEntryPageList<Item>(client, "2");
            if (resp is null)
            {
                Logger.LogError("Fail to load characters");
                return null;
            }
            return resp;
        }

        private CharacterEntry FilteredData(string name, FilterValues filter)
        {
            missingEntries.TryGetValue(name, out var item);
            if (item is not null)
            {
                return item;
            }
            var rarityData = filter.character_rarity?.values?.FirstOrDefault();

            // If can't parse we assume 4*
            int rarity = int.TryParse(rarityData?.Replace("-Star", string.Empty), out rarity) ? rarity : 4;            

            var elementData = filter.character_vision?.values?.FirstOrDefault();
            
            Element element = elementData switch
            {
                "Anemo" => Element.Anemo,
                "Pyro" => Element.Pyro,
                "Cryo" => Element.Cryo,
                "Electro" => Element.Electro,
                "Hydro" => Element.Hydro,
                "Geo" => Element.Geo,
                "Dendro" => Element.Dendro,
                _ => Element.Invalid,
            };
          
            var weaponData = filter.character_weapon?.values?.FirstOrDefault();
            var weaponType = weaponData switch
            {
                "Bow" => WeaponType.Bow,
                "Catalyst" => WeaponType.Catalyst,
                "Polearm" => WeaponType.Polearm,
                "Sword" => WeaponType.Sword,
                "Claymore" => WeaponType.Claymore,
                _ => WeaponType.Invalid,
            };

            var characterInfo = new CharacterEntry()
            {
                Rarity = rarity,
                WeaponType = weaponType,
                Element = element,
            };

            return characterInfo;
        }

        public override async Task<List<CharacterEntry>?> Load()
        {
            var output = new List<CharacterEntry>();
            var charactersList = await GetCharactersList();

            foreach (var character in charactersList ?? new List<Item>())
            {
                var name = character.name;
                Logger.LogInformation("Loading '{name}'", name);

                var result = await LoadEntryPage<Detail>(client, character.entry_page_id);
                if (result is null)
                {
                    Logger.LogError("Fail to load '{name}'", name);
                    return null;
                }
                var characterInfo = FilteredData(name, result.filter_values);

                var consComp = result.modules.Find((m) => m.name == "Constellation")?.components.Find((c) => c.component_id == "summaryList");
                if (string.IsNullOrEmpty(consComp?.data))
                {
                    Logger.LogError("Fail to load '{name}'", name);
                    continue;
                }

                var constellationsData = JsonConvert.DeserializeObject<ConstellationData>(consComp.data)!.list;
                if (constellationsData.Count == 0)
                {
                    Logger.LogError("Fail to load '{name}'", name);
                    continue; // Skip Traveler without element
                }

                var talentsComp = result.modules.Find((m) => m.name == "Talents")?.components.Find((c) => c.component_id == "talent");
                if (string.IsNullOrEmpty(talentsComp?.data))
                {
                    Logger.LogError("Fail to load '{name}'", name);
                    continue;
                }

                var talentsData = JsonConvert.DeserializeObject<TalentData>(talentsComp.data.ToLower())?.list;

                var talents = talentsData?.Select(data =>
                {
                    var name = data.title.Trim();
                    if (data.attributes is not null)
                    {
                        var levelAttr = data.attributes.Find(a => a.key.Equals("level") || a.key == "");
                        if (levelAttr is not null && levelAttr.values.Count >= 10)
                        {
                            // Assume all normal attack starts with the word 'Normal Attack'
                            if (name.StartsWith("normal attack", StringComparison.InvariantCultureIgnoreCase))
                            {
                                return new CharacterEntry.Talent()
                                {
                                    Name = name,
                                    Type = TalentType.Attack,
                                };
                            }
                            // Assume all bursts have an energy cost
                            if (data.attributes.Find(a => a.key.Trim().Equals("energy cost", StringComparison.InvariantCultureIgnoreCase)) != null)
                            {
                                return new CharacterEntry.Talent()
                                {
                                    Name = name,
                                    Type = TalentType.Burst,
                                };
                            }
                            // Assume anything that can be leveled and is not a normal attack/burst is a skill
                            return new CharacterEntry.Talent()
                            {
                                Name = name,
                                Type = TalentType.Skill,
                            };
                        }
                    }
                    return new CharacterEntry.Talent()
                    {
                        Name = name,
                        Type = TalentType.Other,
                    };
                }).ToList();

                var skillName = talents?.Find(t => t.Type == TalentType.Skill)?.Name;
                var burstName = talents?.Find(t => t.Type == TalentType.Burst)?.Name;

                var iconPath = IconRepository.GetPath("Characters", character.name, Path.GetExtension(character.icon_url));
                await IconRepository.SaveUrlAsIcon(client, character.icon_url, iconPath);

                var constellations = constellationsData.Select(data =>
                {
                    var name = data.name.Trim();
                    var match = FindTalentLevelUpConstellation.Match(RemoveHtmlTags.Replace(data.desc, string.Empty));
                    if (match.Success && int.TryParse(match.Groups[2].Value, out int levelUp))
                    {
                        var talentName = match.Groups[1].Value.Trim();
                        var skillScore = talentName.FuzzySearch(skillName);
                        var burstScore = talentName.FuzzySearch(burstName);

                        if (skillScore >= TalentNameScore && skillScore > burstScore)
                        {
                            return new CharacterEntry.Constellation()
                            {
                                Name = name,
                                SkillLevel = levelUp,
                            };
                        }
                        else if (burstScore >= TalentNameScore)
                        {
                            return new CharacterEntry.Constellation()
                            {
                                Name = name,
                                BurstLevel = levelUp,
                            };
                        }
                    }
                    return new CharacterEntry.Constellation()
                    {
                        Name = name,
                    };
                });

                output.Add(new CharacterEntry()
                {
                    Name = character.name.Trim(),
                    Rarity = characterInfo.Rarity,
                    Element = characterInfo.Element,
                    WeaponType = characterInfo.WeaponType,
                    IsTraveler = character.name.Contains("Traveler"),
                    Icon = iconPath,
                    Talents = talents,
                    Constellations = constellations.ToList(),
                });
            }
            Logger.LogInformation("Characters loaded");

            return output;
        }
    }
}
