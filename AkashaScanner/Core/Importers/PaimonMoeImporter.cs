using AkashaScanner.Core.DataFiles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AkashaScanner.Core.Importers
{
    public class PaimonMoeImporter : IPaimonMoeImporter
    {
        private readonly IDataFileRepository<AchievementOutput> _dataFileRepository;
        private readonly AchievementOutput _achievementDict = new();

        public PaimonMoeImporter(IDataFileRepository<AchievementOutput> dataFileRepository)
        {
            _dataFileRepository = dataFileRepository ?? throw new ArgumentNullException(nameof(dataFileRepository));
        }

        /// <summary>
        /// Initializes the importer by clearing the existing dictionary.
        /// </summary>
        public void Init() => _achievementDict.Clear();

        /// <summary>
        /// Adds an achievement to the dictionary.
        /// </summary>
        /// <param name="item">The achievement to add.</param>
        public void Add(Achievement item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            _achievementDict[item.Id] = item.CategoryId;
        }

        /// <summary>
        /// Saves the current dictionary to a data file.
        /// </summary>
        public void Save()
        {
            if (_achievementDict.Count == 0) return; // No data to save.

            var file = _dataFileRepository.Create(_achievementDict.Count);
            file.Write(_achievementDict);
        }

        /// <summary>
        /// Imports achievements from a JSON string.
        /// </summary>
        /// <param name="import">The JSON string to import.</param>
        /// <returns>True if the import is successful, otherwise false.</returns>
        public bool Import(string import)
        {
            if (string.IsNullOrWhiteSpace(import))
                return false;

            try
            {
                // Parse the JSON input.
                var parsed = JToken.Parse(import);
                if (parsed is not JObject jsonObject)
                    return false;

                // Extract the "achievement" node.
                var achievementsGroups = jsonObject["achievement"];
                if (achievementsGroups == null)
                    return false;

                // Deserialize into a dictionary.
                var parsedData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, bool>>>(achievementsGroups.ToString());
                if (parsedData == null)
                    return false;

                // Process the achievements.
                Init();
                foreach (var (groupId, achievements) in parsedData)
                {
                    if (int.TryParse(groupId, out int categoryId))
                    {
                        foreach (var (achievementId, isUnlocked) in achievements)
                        {
                            if (isUnlocked && int.TryParse(achievementId, out int id))
                            {
                                var achievement = new Achievement
                                {
                                    CategoryId = categoryId,
                                    Id = id
                                };
                                Add(achievement);
                            }
                        }
                    }
                }

                Save();
                return true;
            }
            catch (JsonException ex)
            {
                // Log JSON-specific exceptions for debugging purposes.
                Console.WriteLine($"JSON Parsing Error: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                // Log unexpected exceptions.
                Console.WriteLine($"An error occurred during import: {ex.Message}");
                return false;
            }
        }
    }
}
