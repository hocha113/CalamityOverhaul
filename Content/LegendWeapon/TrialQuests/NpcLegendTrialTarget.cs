using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.TrialQuests
{
    internal sealed class NpcLegendTrialTarget : ILegendTrialTarget
    {
        private readonly Func<int[]> npcTypeProvider;
        private readonly Func<bool> completedCheck;
        private readonly string fallbackName;
        private bool resolvedNpcTypes;
        private int[] cachedNpcTypes;

        public NpcLegendTrialTarget(Func<int[]> npcTypeProvider, Func<bool> completedCheck, string fallbackName = "") {
            this.npcTypeProvider = npcTypeProvider;
            this.completedCheck = completedCheck;
            this.fallbackName = fallbackName ?? string.Empty;
        }

        public bool IsAvailable => ResolveNpcTypes().Length > 0;
        public bool IsCompleted => completedCheck?.Invoke() == true;

        /// <summary>列出的类型都是同一席的备选目标，亲手杀过任意一个即算</summary>
        public bool IsPersonallyCleared(Func<int, bool> hasKilled) {
            if (hasKilled == null) {
                return false;
            }
            int[] npcTypes = ResolveNpcTypes();
            for (int i = 0; i < npcTypes.Length; i++) {
                if (hasKilled(npcTypes[i])) {
                    return true;
                }
            }
            return false;
        }

        public IEnumerable<string> GetDisplayNames() {
            string[] names = [.. ResolveNpcTypes()
                .Select(static t => Lang.GetNPCNameValue(t))
                .Where(static n => !string.IsNullOrEmpty(n))];

            if (names.Length > 0) {
                return names;
            }
            if (!string.IsNullOrEmpty(fallbackName)) {
                return [fallbackName];
            }
            return [];
        }

        public LegendTrialTargetSnapshot GetSnapshot() {
            if (IsCompleted) {
                return LegendTrialTargetSnapshot.Completed;
            }

            int[] npcTypes = ResolveNpcTypes();
            if (npcTypes.Length == 0) {
                return LegendTrialTargetSnapshot.Inactive;
            }

            bool alive = false;
            float bestRatio = 1f;
            string bestName = string.Empty;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.lifeMax <= 0) {
                    continue;
                }
                if (Array.IndexOf(npcTypes, npc.type) < 0) {
                    continue;
                }

                alive = true;
                float ratio = (float)npc.life / npc.lifeMax;
                if (ratio < bestRatio) {
                    bestRatio = ratio;
                    bestName = Lang.GetNPCNameValue(npc.type);
                }
            }

            if (!alive) {
                return LegendTrialTargetSnapshot.Inactive;
            }

            return new LegendTrialTargetSnapshot(true, 1f - bestRatio, bestRatio, bestName);
        }

        private int[] ResolveNpcTypes() {
            if (resolvedNpcTypes) {
                return cachedNpcTypes;
            }

            int[] types = npcTypeProvider?.Invoke() ?? [];
            cachedNpcTypes = [.. types.Where(static t => t > NPCID.None).Distinct()];
            resolvedNpcTypes = true;
            return cachedNpcTypes;
        }
    }
}
