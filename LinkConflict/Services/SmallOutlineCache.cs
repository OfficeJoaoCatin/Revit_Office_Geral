namespace RevitFamilyImporter.LinkConflict.Services
{
    // Apenas resultados compactos; sólidos e geometrias grandes nunca entram nesta cache.
    internal sealed class SmallOutlineCache<T>
    {
        internal const int DefaultCapacity = 4096;
        internal const int DefaultMaxOutlines = 8;
        private readonly BoundedLruCache<int, T> _entries;
        private readonly int _maxOutlines;

        public SmallOutlineCache(int capacity = DefaultCapacity, int maxOutlines = DefaultMaxOutlines)
        {
            if (maxOutlines < 0) throw new System.ArgumentOutOfRangeException(nameof(maxOutlines));
            _entries = new BoundedLruCache<int, T>(capacity);
            _maxOutlines = maxOutlines;
        }
        public bool TryGetValue(int id, out T value) => _entries.TryGetValue(id, out value);
        public bool TryStore(int id, T value, int outlineCount)
        {
            if (outlineCount < 0 || outlineCount > _maxOutlines) return false;
            _entries.Set(id, value);
            return true;
        }
    }
}
