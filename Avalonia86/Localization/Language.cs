namespace Avalonia86.Localization
{
    internal class Language
    {
        public static readonly Language[] Languages = new Language[] 
        { 
            new Language("os-default", "System default"),
            new Language("en-US", "English"), 
            new Language("zh-Hans", "Simplified Chinese"), 
            new Language("zh-Hant", "Traditional Chinese") 
        };

        public readonly string Key;

        public readonly string DisplayName;

        private Language(string key, string displayName)
        {
            Key = key;
            DisplayName = displayName;
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is Language other && Key.Equals(other.Key);
        }

        internal static Language Find(string key)
        {
            for(int i = 0; i < Languages.Length; i++)
            {
                if (Languages[i].Key.Equals(key))
                    return Languages[i];
            }

            return null;
        }
    }
}
