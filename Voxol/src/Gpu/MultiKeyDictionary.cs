using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Voxol.Gpu;

public class MultiKeyDictionary<TKey, TValue> : IEnumerable<MultiKeyDictionary<TKey, TValue>.Pair> {
    private readonly EqualityComparer<TKey> keyComparer;
    private readonly List<Pair> pairs = [];

    public MultiKeyDictionary(EqualityComparer<TKey> keyComparer) {
        this.keyComparer = keyComparer;
    }

    public MultiKeyDictionary() : this(EqualityComparer<TKey>.Default) { }

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

    public void Remove(Func<ReadOnlySpan<TKey>, bool> predicate) {
        var toRemove = new List<int>();

        for (var i = 0; i < pairs.Count; i++) {
            if (predicate(pairs[i].Keys)) {
                toRemove.Add(i);
            }
        }

        foreach (var i in toRemove) {
            pairs.RemoveAt(i);
        }
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

        public bool ContainsKey(TKey key) {
            return Keys.Contains(key);
        }
    }
}