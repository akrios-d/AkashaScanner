using Microsoft.Extensions.Logging;

namespace AkashaScanner.Core.DataCollections.Repositories
{
    public class WeaponsHoYoWikiRepository : HoYoWikiRepository<WeaponEntry>
    {
        private readonly ILogger<WeaponsHoYoWikiRepository> _logger;
        private const string WeaponCategoryId = "4";

        public WeaponsHoYoWikiRepository(ILogger<WeaponsHoYoWikiRepository> logger)
        {
            _logger = logger;
        }

        public override async Task<List<WeaponEntry>?> Load()
        {
            using var client = CreateClient();
            _logger.LogInformation("Starting to load weapon entries from HoYoWiki.");

            var items = await LoadEntryPageList<Item>(client, WeaponCategoryId);

            if (items is null)
            {
                _logger.LogError("Failed to retrieve weapon entries from HoYoWiki.");
                return null;
            }

            var weapons = new List<WeaponEntry>();

            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.icon_url) ||
                    item.filter_values.weapon_rarity is null ||
                    item.filter_values.weapon_type is null)
                {
                    _logger.LogWarning("Skipping weapon due to missing data: {WeaponName}", item.name);
                    continue;
                }

                var extension = Path.GetExtension(item.icon_url);
                var iconPath = IconRepository.GetPath("Weapons", item.name, extension);
                await IconRepository.SaveUrlAsIcon(client, item.icon_url, iconPath);

                if (!TryParseWeaponType(item.filter_values.weapon_type.values[0], out var weaponType))
                {
                    _logger.LogWarning("Unknown weapon type for: {WeaponName}", item.name);
                    continue;
                }

                if (!int.TryParse(item.filter_values.weapon_rarity.values[0].Substring(0, 1), out var rarity))
                {
                    _logger.LogWarning("Invalid rarity format for: {WeaponName}", item.name);
                    continue;
                }

                weapons.Add(new WeaponEntry
                {
                    Name = item.name.Trim(),
                    Icon = iconPath,
                    Rarity = rarity,
                    Type = weaponType
                });
            }

            _logger.LogInformation("Successfully loaded {Count} weapons.", weapons.Count);
            return weapons;
        }

        private static bool TryParseWeaponType(string value, out WeaponType type)
        {
            type = value switch
            {
                "Bow" => WeaponType.Bow,
                "Catalyst" => WeaponType.Catalyst,
                "Polearm" => WeaponType.Polearm,
                "Sword" => WeaponType.Sword,
                "Claymore" => WeaponType.Claymore,
                _ => WeaponType.Invalid
            };
            return type != WeaponType.Invalid;
        }

        private record Item
        {
            public string name = default!;
            public string icon_url = default!;
            public FilterValueList filter_values = default!;
        }

        private record FilterValueList
        {
            public FilterValue weapon_type = default!;
            public FilterValue weapon_rarity = default!;
        }

        private record FilterValue
        {
            public List<string> values = default!;
        }
    }
}
