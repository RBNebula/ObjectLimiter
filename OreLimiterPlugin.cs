using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace MineMogul.ObjectLimiter
{
    [BepInPlugin("OreLimiter", "Ore Limiter", "0.3.0")]
    public sealed class OreLimiterPlugin : BaseUnityPlugin
    {
        // ─────────────────────────────────────────────
        // 1) Static / identity
        // ─────────────────────────────────────────────
        internal static OreLimiterPlugin Instance = null!;
        internal static ManualLogSource Log = null!;

        // ─────────────────────────────────────────────
        // 2) Harmony
        // ─────────────────────────────────────────────
        private Harmony _harmony = null!;

        // ─────────────────────────────────────────────
        // 3) Config
        // ─────────────────────────────────────────────
        private ConfigEntry<bool> _cfgUnlimited = null!;
        private ConfigEntry<int> _cfgLimit = null!;
        private ConfigEntry<string> _cfgResourceTypeWhitelistCsv = null!;
        private ConfigEntry<bool> _cfgDebugOverlay = null!;

        // ─────────────────────────────────────────────
        // 4) Runtime state
        // ─────────────────────────────────────────────
        // AutoMiner-spawned deletion candidates
        private readonly LinkedList<int> _spawnOrder = new LinkedList<int>(); 
        private readonly Dictionary<int, WeakReference<OrePiece>> _tracked = new Dictionary<int, WeakReference<OrePiece>>();

        // Counters / debug state
        private int _deletedCount;
        private string _lastDeletedName = "";
        private int _lastTrackedCount;

        // ─────────────────────────────────────────────
        // 5) Small API (used by settings patch + other patches)
        // ─────────────────────────────────────────────
        internal void SetUnlimited(bool v) => _cfgUnlimited.Value = v;
        internal void SetLimit(int v) => _cfgLimit.Value = Mathf.Max(0, v);

        internal bool IsUnlimited() => _cfgUnlimited.Value;
        internal int GetLimit() => Mathf.Max(0, _cfgLimit.Value);

        // ─────────────────────────────────────────────
        // 6) Unity lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            _cfgUnlimited = Config.Bind("ObjectLimiter", "Unlimited", true,
                "If true, limiter is disabled (no checks, no deletions).");

            _cfgLimit = Config.Bind("ObjectLimiter", "Limit", 250,
                "Max number of AutoMiner-spawned Ore/Crushed objects allowed when Unlimited=false.");

            _cfgResourceTypeWhitelistCsv = Config.Bind(
                "ObjectLimiter",
                "ResourceTypeWhitelist",
                "",
                "Comma-separated ResourceType numeric values to limit. Empty = all ResourceType values.\n" +
                "Example:\n" +
                "ResourceTypeWhitelist = 1,2,3 // Will delete Iron Ore + Crushed Iron Ore, Coal + Crushed Coal, Gold Ore + Crushed Gold Ore.\n\n" +
                "ResourceType numeric IDs:\n" +
                "Iron\t1\n" +
                "Coal\t2\n" +
                "Gold\t3\n" +
                "Slag\t4\n" +
                "Diamond\t5\n" +
                "Emerald\t6\n" +
                "Copper\t7\n" +
                "Broken\t8\n" +
                "Ruby\t9\n" +
                "Steel\t10"
            );

            _cfgDebugOverlay = Config.Bind("ObjectLimiter", "DebugOverlay", false,
                "Shows limiter status text on screen.");

            _harmony = new Harmony("minemogul.orelimiter");
            _harmony.PatchAll();

            Logger.LogInfo("Ore Limiter loaded.");
        }

        private void Update()
        {
            if (_cfgUnlimited.Value) return;

            CleanupDeadTracked();
            _lastTrackedCount = CountTotalActiveEligible();
        }

        private void OnDestroy()
        {
            try { _harmony?.UnpatchSelf(); } catch { }
        }

        // ─────────────────────────────────────────────
        // 7) Entry points called by Harmony patches
        // ─────────────────────────────────────────────
        internal void RegisterAutoMinerSpawn(OrePiece ore)
        {
            if (ore == null) return;
            if (_cfgUnlimited.Value) return;

            // Only Ore + Crushed (ignore gems and everything else)
            if (ore.PieceType != PieceType.Ore && ore.PieceType != PieceType.Crushed)
                return;

            // Optional: ResourceType whitelist
            var whitelist = ParseIntSet(_cfgResourceTypeWhitelistCsv.Value);
            if (whitelist.Count > 0)
            {
                int rt = (int)ore.ResourceType;
                if (!whitelist.Contains(rt))
                    return;
            }

            int id = ore.GetInstanceID();
            if (_tracked.ContainsKey(id))
                return;

            _tracked[id] = new WeakReference<OrePiece>(ore);
            _spawnOrder.AddLast(id);
            _lastTrackedCount = _tracked.Count;
        }

        internal void EnforceLimitNow()
        {
            if (_cfgUnlimited.Value) return;

            int limit = Mathf.Max(0, _cfgLimit.Value);

            CleanupDeadTracked(); // keeps miner tracking accurate

            int totalActive = CountTotalActiveEligible();
            _lastTrackedCount = totalActive;

            if (totalActive <= limit)
                return;

            // We are over limit: delete newest AutoMiner spawns until totalActive <= limit
            while (totalActive > limit)
            {
                var node = _spawnOrder.Last;
                if (node == null)
                    break; // nothing AutoMiner-spawned left to remove

                int id = node.Value;
                _spawnOrder.RemoveLast();

                if (!_tracked.TryGetValue(id, out var wr) || !wr.TryGetTarget(out var ore) || ore == null)
                {
                    _tracked.Remove(id);
                    totalActive = CountTotalActiveEligible();
                    continue;
                }

                if (!ore.gameObject.activeSelf || !ore.isActiveAndEnabled)
                {
                    _tracked.Remove(id);
                    totalActive = CountTotalActiveEligible();
                    continue;
                }

                if (ore.PieceType != PieceType.Ore && ore.PieceType != PieceType.Crushed)
                {
                    _tracked.Remove(id);
                    totalActive = CountTotalActiveEligible();
                    continue;
                }

                _lastDeletedName = ore.name ?? "OrePiece";
                _deletedCount++;

                ore.Delete(); // return to pool
                _tracked.Remove(id);

                totalActive = CountTotalActiveEligible();
                _lastTrackedCount = totalActive;
            }
        }

        // ─────────────────────────────────────────────
        // 8) Core internal logic
        // ─────────────────────────────────────────────
        private void CleanupDeadTracked()
        {
            if (_tracked.Count == 0) return;

            var deadIds = new List<int>();

            foreach (var kv in _tracked)
            {
                // If we can't resolve it, it's dead.
                if (!kv.Value.TryGetTarget(out var ore) || ore == null)
                {
                    deadIds.Add(kv.Key);
                    continue;
                }

                // IMPORTANT: pooled ore is not destroyed; it is set inactive.
                // Treat inactive (or disabled) as removed from the world.
                if (!ore.gameObject.activeSelf || !ore.isActiveAndEnabled)
                {
                    deadIds.Add(kv.Key);
                    continue;
                }

                // Optional safety: if it changed piece type (e.g., became ingot), stop tracking it.
                if (ore.PieceType != PieceType.Ore && ore.PieceType != PieceType.Crushed)
                {
                    deadIds.Add(kv.Key);
                    continue;
                }
            }

            if (deadIds.Count == 0) return;

            foreach (var id in deadIds)
                _tracked.Remove(id);

            // Trim spawn order to only IDs that still exist in _tracked
            var keep = new HashSet<int>(_tracked.Keys);
            var n = _spawnOrder.First;
            while (n != null)
            {
                var next = n.Next;
                if (!keep.Contains(n.Value))
                    _spawnOrder.Remove(n);
                n = next;
            }
        }

        private int CountTotalActiveEligible()
        {
            var all = OrePiece.AllOrePieces;
            if (all == null) return 0;

            int count = 0;
            var whitelist = ParseIntSet(_cfgResourceTypeWhitelistCsv.Value);

            for (int i = 0; i < all.Count; i++)
            {
                var ore = all[i];
                if (ore == null) continue;

                if (!ore.gameObject.activeSelf || !ore.isActiveAndEnabled) continue;

                if (ore.PieceType != PieceType.Ore && ore.PieceType != PieceType.Crushed)
                    continue;

                if (whitelist.Count > 0)
                {
                    int rt = (int)ore.ResourceType;
                    if (!whitelist.Contains(rt))
                        continue;
                }

                count++;
            }

            return count;
        }

        // ─────────────────────────────────────────────
        // 9) Debug UI
        // ─────────────────────────────────────────────
        private string GetStatusString()
        {
            if (_cfgUnlimited.Value)
                return $"OreLimiter: Unlimited | totalActive={CountTotalActiveEligible()} | minerTracked={_tracked.Count}";

            return $"OreLimiter: Limit={_cfgLimit.Value} | totalActive={CountTotalActiveEligible()} | minerTracked={_tracked.Count} | deleted={_deletedCount}";

        }

        private void OnGUI()
        {
            if (!_cfgDebugOverlay.Value) return;
            GUI.Label(new Rect(10, 10, 900, 30), GetStatusString());
        }


        // ─────────────────────────────────────────────
        // 10) Utilities
        // ─────────────────────────────────────────────
        private static HashSet<int> ParseIntSet(string csv)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(csv)) return set;

            foreach (var part in csv.Split(','))
            {
                var t = part.Trim();
                if (t.Length == 0) continue;
                if (int.TryParse(t, out var n)) set.Add(n);
            }
            return set;
        }
    }
}
