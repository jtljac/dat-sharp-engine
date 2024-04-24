namespace dat_sharp_engine.Util {}

namespace LanguageExtensions {
    public static class DictionaryExtensions {
        /// <summary>
        /// Add all the values of the <paramref name="otherDict"/> to this dictionary
        /// </summary>
        /// <param name="dict">This dictionary</param>
        /// <param name="otherDict">The dictionary to take values from</param>
        /// <param name="overwrite">If true, overwrite existing keys in this dictionary, otherwise skip them</param>
        /// <typeparam name="TKey">The type of the keys in the dictionary</typeparam>
        /// <typeparam name="TValue">The type of the values in the dictionary</typeparam>
        /// <returns>This dictionary</returns>
        public static IDictionary<TKey, TValue> AddRange<TKey, TValue>(this IDictionary<TKey, TValue> dict, IDictionary<TKey, TValue> otherDict, bool overwrite = false) {
            foreach (var (key, value) in otherDict) {
                if (!dict.ContainsKey(key) || overwrite) dict[key] = value;
            }

            return dict;
        }
    }
}