using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PathsSynchronizer.Core.Support.Collections
{
    public static class DictionaryExtensionMethods
    {
        public static ReadOnlyDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
            where TKey : notnull =>
            new ReadOnlyDictionary<TKey, TValue>(dictionary);
    }
}
