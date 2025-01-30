using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Voxol.Gpu;

public class MultiKeyDictionary<TKey, TValue> : IEnumerable<MultiKeyDictionary<TKey, TValue>.Pair> {
    private readonly EqualityComparer<TKey> keyComparer = EqualityComparer<TKey>.Default;
    private readonly List<Pair> pairs = [];
    
    public TValue this[ReadOnlySpan<TKey> keys] {
        set => Add(keys, value);
    }

    public void Add(ReadOnlySpan<TKey> keys, TValue value) {
        pairs.Add(new Pair(keys.ToArray(), value));
    }

    public bool TryGetValue(ReadOnlySpan<TKey> keys, [MaybeNullWhen(false)] out TValue value) {
        foreach (var pair in pairs) {
            if (pair.KeysEqual(keyComparer, keys)) {
                value = pair.Value;
                return true;
            }
        }
        
        value = default;
        return false;
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
    
    public IEnumerator<Pair> GetEnumerator() {
        return pairs.GetEnumerator();
    }

    public readonly record struct Pair(TKey[] Keys, TValue Value) {
        public bool KeysEqual(EqualityComparer<TKey> keyComparer, ReadOnlySpan<TKey> other) {
            if (Keys.Length != other.Length)
                return false;

            for (var i = 0; i < other.Length; i++) {
                if (!keyComparer.Equals(Keys[i], other[i]))
                    return false;
            }
            
            return true;
        }
    }
}