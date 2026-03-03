using HarmonyLib;
using UnityEngine;

namespace MineMogul.ObjectLimiter
{
    [HarmonyPatch(typeof(OrePiecePoolManager), nameof(OrePiecePoolManager.SpawnPooledOre),
        new[] { typeof(OrePiece), typeof(Vector3), typeof(Quaternion), typeof(Transform) })]
    internal static class OrePiecePoolManager_SpawnPooledOre_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(OrePiece __result)
        {
            if (__result == null) return;
            if (!OreLimiterRuntimeContext.InAutoMinerSpawn) return;

            if (__result.PieceType != PieceType.Ore && __result.PieceType != PieceType.Crushed)
                return;

            var plugin = OreLimiterPlugin.Instance;
            if (plugin == null) return;

            plugin.RegisterAutoMinerSpawn(__result);
            plugin.EnforceLimitNow();
        }
    }
}
