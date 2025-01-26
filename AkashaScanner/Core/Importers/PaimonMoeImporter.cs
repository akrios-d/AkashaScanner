using Newtonsoft.Json.Linq;

namespace AkashaScanner.Core.Importers
{
    public static class PaimonMoeImporter
    {
      
        public static bool Import(
            string Import)
        {
            JObject obj = new();
            if (!string.IsNullOrEmpty(Import))
            {
                var parsed = JToken.Parse(Import);
                if (parsed is JObject ob)
                {
                    obj = ob;
                }
            }
            return true;
        }
    }
}
