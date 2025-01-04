namespace AkashaScanner.Core
{
    public interface ICharacterNamesConfig
    {
        Dictionary<string, string> CharacterNameOverrides { get; set; }

        /// <summary>
        /// Load a character override name to be used on a display page.
        /// </summary>
        /// <param name="character"></param>
        /// <returns>The override name from `CharacterNameOverrides`, or an
        /// empty string if there is no name set.</returns>        
        string GetCharacterNameOverride(string character)
        {
            return CharacterNameOverrides.GetValueOrDefault(character, "");
        }

        /// <summary>
        /// Set a character override name.
        /// </summary>
        /// <param name="character"></param>
        /// <param name="nameOverrideOrEmpty">The name to use. If empty, the
        /// character's name override will be removed.</param>
        void SetCharacterNameOverride(string character, string nameOverrideOrEmpty)
        {
            if (string.IsNullOrEmpty(nameOverrideOrEmpty))
            {
                CharacterNameOverrides.Remove(character);
            }
            else
            {
                CharacterNameOverrides[character] = nameOverrideOrEmpty;
            }
        }
    }
}
