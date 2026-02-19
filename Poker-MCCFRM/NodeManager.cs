namespace Poker_MCCFRM
{
    public static class NodeManager
    {
        public static Infoset GetOrCreate(string key, int actionCount)
        {
            // Check cache first
            if (Global.nodeCache.TryGetValue(key, out var cached))
                return cached;

            // Check FASTER store
            if (Global.nodeStore.TryGet(key, out var stored))
            {
                // Add to cache
                Global.nodeCache.TryAdd(key, stored);
                return stored;
            }

            // Create new
            var newInfoset = new Infoset(actionCount);
            Global.nodeStore.Upsert(key, newInfoset);
            Global.nodeCache.TryAdd(key, newInfoset);
            return newInfoset;
        }

        public static void Update(string key, Infoset value)
        {
            Global.nodeStore.Upsert(key, value);
            Global.nodeCache.AddOrUpdate(key, value, (k, v) => value);
        }
    }
}