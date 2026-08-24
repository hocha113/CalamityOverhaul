using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>
    /// 修罗模式：敌怪对同种伤害来源的自适应免疫。
    /// 来源键 = 弹幕类型（正数）/物品类型（负数），层数随命中积累、脱手一段时间后逐层衰减。
    /// tML 的打击判定在攻击方本机进行（伤害随打击包下发，服务端不重算），
    /// 因此适应状态无需网络同步；联机下每个攻击者面对的是敌怪对"自己"的适应
    /// </summary>
    internal class AsuraNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>每层减伤比例</summary>
        private const float ResistPerStack = 0.08f;
        /// <summary>减伤上限，保证永远打得动</summary>
        private const float ResistCap = 0.88f;
        /// <summary>层数上限（到顶后继续命中只刷新计时）</summary>
        private const float StackCap = 11f;
        /// <summary>无同类命中的宽限帧数，此后开始衰减</summary>
        private const int GraceTicks = 90;
        /// <summary>衰减速率：每这么多帧掉一层</summary>
        private const int DecayTicksPerStack = 30;

        private struct AdaptEntry
        {
            public float Stacks;
            public uint LastHitTick;
        }

        /// <summary>来源键 → 适应条目；懒初始化，条目衰减尽后懒清除</summary>
        private Dictionary<int, AdaptEntry> adapt;

        private static int ProjKey(int type) => type;
        private static int ItemKey(int type) => -type;

        private static bool Eligible(NPC npc) => GameModeSystem.AsuraActive && !npc.friendly;

        /// <summary>按衰减折算当前有效层数</summary>
        private static float EffectiveStacks(in AdaptEntry entry, uint now) {
            float stacks = entry.Stacks;
            uint elapsed = now - entry.LastHitTick;
            if (elapsed > GraceTicks) {
                stacks -= (elapsed - GraceTicks) / (float)DecayTicksPerStack;
            }
            return stacks;
        }

        /// <summary>该来源当前的伤害保留系数（1 = 无适应）</summary>
        private float ResistFactor(int key) {
            if (adapt == null || !adapt.TryGetValue(key, out AdaptEntry entry)) {
                return 1f;
            }
            float stacks = EffectiveStacks(in entry, Main.GameUpdateCount);
            if (stacks <= 0f) {
                adapt.Remove(key);
                return 1f;
            }
            return 1f - Math.Min(ResistCap, stacks * ResistPerStack);
        }

        /// <summary>记一次同类命中：折算现有层数后 +1，封顶并刷新计时</summary>
        private void Accumulate(int key) {
            adapt ??= [];
            uint now = Main.GameUpdateCount;
            float stacks = 0f;
            if (adapt.TryGetValue(key, out AdaptEntry entry)) {
                stacks = Math.Max(0f, EffectiveStacks(in entry, now));
            }
            adapt[key] = new AdaptEntry {
                Stacks = Math.Min(stacks + 1f, StackCap),
                LastHitTick = now,
            };
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (!Eligible(npc)) {
                return;
            }
            modifiers.FinalDamage *= ResistFactor(ItemKey(item.type));
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (!Eligible(npc)) {
                return;
            }
            modifiers.FinalDamage *= ResistFactor(ProjKey(projectile.type));
        }

        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) {
            if (!Eligible(npc)) {
                return;
            }
            Accumulate(ItemKey(item.type));
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) {
            if (!Eligible(npc)) {
                return;
            }
            Accumulate(ProjKey(projectile.type));
        }
    }
}
