using HarmonyLib;

namespace MineMogul.ObjectLimiter
{
    internal static class OreLimiterRuntimeContext
    {
        [System.ThreadStatic]
        public static bool InAutoMinerSpawn;
    }

    [HarmonyPatch(typeof(AutoMiner), "TrySpawnOre")]
    internal static class AutoMinerSpawnContextPatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            OreLimiterRuntimeContext.InAutoMinerSpawn = true;
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            OreLimiterRuntimeContext.InAutoMinerSpawn = false;
        }
    }
}
