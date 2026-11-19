using System;
using System.Collections.Generic;

namespace RevitFamilyImporter.LinkConflict.Services
{
    // Access occurs only on the Revit API thread. A failed factory leaves the cache unchanged.
    internal sealed class BoundedLruCache<TKey, TValue> where TKey : notnull
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _entries =
            new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>();
        private readonly LinkedList<KeyValuePair<TKey, TValue>> _usage = new LinkedList<KeyValuePair<TKey, TValue>>();

        public BoundedLruCache(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_entries.TryGetValue(key, out var node))
            {
                _usage.Remove(node);
                _usage.AddLast(node);
                value = node.Value.Value;
                return true;
            }
            value = default!;
            return false;
        }

        public void Set(TKey key, TValue value)
        {
            if (_entries.TryGetValue(key, out var current))
            {
                _usage.Remove(current);
                _entries.Remove(key);
            }
            else if (_entries.Count == _capacity)
            {
                var oldest = _usage.First!;
                _entries.Remove(oldest.Value.Key);
                _usage.RemoveFirst();
            }
            _entries.Add(key, _usage.AddLast(new KeyValuePair<TKey, TValue>(key, value)));
        }

        public TValue GetOrAdd(TKey key, Func<TValue> factory)
        {
            if (_entries.TryGetValue(key, out var node))
            {
                _usage.Remove(node);
                _usage.AddLast(node);
                return node.Value.Value;
            }
            TValue value = factory();
            if (_entries.Count == _capacity)
            {
                var oldest = _usage.First!;
                _entries.Remove(oldest.Value.Key);
                _usage.RemoveFirst();
            }
            _entries.Add(key, _usage.AddLast(new KeyValuePair<TKey, TValue>(key, value)));
            return value;
        }
    }
}
