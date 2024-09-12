namespace AkashaScanner.Core.Dtos.Character
{
    public record Item
    {
        public string entry_page_id = default!;
        public string name = default!;
        public string icon_url = default!;
    }

    public record Detail
    {
        public List<Module> modules = default!;
        public FilterValues filter_values = default!;
    }

    public record FilterValues
    {
        public FilterValue character_vision = default!;
        public FilterValue character_weapon = default!;
        public FilterValue character_rarity = default!;
    }

    public record FilterValue
    {
        public List<string> values = default!;
    }

    public record Module
    {
        public string name = default!;
        public List<Component> components = default!;
    }

    public record Component
    {
        public string component_id = default!;
        public string data = default!;
    }

    public record ConstellationData
    {
        public List<ConstellationDataContent> list = default!;
    }

    public record ConstellationDataContent
    {
        public string name = default!;
        public string desc = default!;
    }

    public record TalentData
    {
        public List<TalentDataContent> list = default!;
    }

    public record TalentDataContent
    {
        public string title = default!;
        public List<TalentAttribute>? attributes = default!;
    }

    public record TalentAttribute
    {
        public string key = default!;
        public List<string> values = default!;
    }
}