using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VainZero.Vsix.FieldInitializationAnalyzer
{
    public static class KeyValuePairExtension
    {
        public static void Deconstruct<K, V>(this KeyValuePair<K, V> kv, out K key, out V value)
        {
            key = kv.Key;
            value = kv.Value;
        }
    }
}
