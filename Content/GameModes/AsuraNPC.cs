using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>
    /// 修罗模式：敌怪对同种伤害来源的自适应免疫。
    /// 来源键 = 弹幕类型（正数）/物品类型（负数），层数随命中积累、脱手一段时间后逐层衰减。
    /// 近战是适应的裂隙：刀刃本体只承受部分适应减伤，近战弹幕次之；
    /// 近战命中还按出手距离获得贴身增幅，越近越痛。数值见 <see cref="GameModeTuning"/>。
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

        /// <summary>该来源当前的伤害保留系数（1 = 无适应）；adaptTaken 为该攻击实际承受的适应减伤比例</summary>
        private float ResistFactor(int key, float adaptTaken) {
            if (adapt == null || !adapt.TryGetValue(key, out AdaptEntry entry)) {
                return 1f;
            }
            float stacks = EffectiveStacks(in entry, Main.GameUpdateCount);
            if (stacks <= 0f) {
                adapt.Remove(key);
                return 1f;
            }
            return 1f - Math.Min(ResistCap, stacks * ResistPerStack) * adaptTaken;
        }

        /// <summary>贴身增幅倍率：按玩家中心到目标碰撞箱最近点的距离线性增伤，贴脸满额、出增幅圈归 1</summary>
        private static float CloseRangeMult(Player player, NPC npc) {
            Rectangle box = npc.Hitbox;
            Vector2 nearest = new(
                MathHelper.Clamp(player.Center.X, box.Left, box.Right),
                MathHelper.Clamp(player.Center.Y, box.Top, box.Bottom));
            float dist = player.Center.Distance(nearest);
            float t = MathHelper.Clamp(
                (GameModeTuning.AsuraCloseRangeZeroDist - dist)
                / (GameModeTuning.AsuraCloseRangeZeroDist - GameModeTuning.AsuraCloseRangeFullDist), 0f, 1f);
            return 1f + GameModeTuning.AsuraCloseRangeMaxBonus * t;
        }

        /// <summary>记一次同类命中：折算现有层数后加层（毁灭下适应更快），封顶并刷新计时</summary>
        private void Accumulate(int key) {
            adapt ??= [];
            uint now = Main.GameUpdateCount;
            float stacks = 0f;
            if (adapt.TryGetValue(key, out AdaptEntry entry)) {
                stacks = Math.Max(0f, EffectiveStacks(in entry, now));
            }
            float gain = GameModeSystem.AnnihilationActive ? GameModeTuning.AnnihilationAdaptStacksPerHit : 1f;
            adapt[key] = new AdaptEntry {
                Stacks = Math.Min(stacks + gain, StackCap),
                LastHitTick = now,
            };
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (!Eligible(npc)) {
                return;
            }
            //物品挥击就是刀刃本体：适应减伤按真近战折扣，并吃贴身增幅
            bool melee = item.DamageType.CountsAsClass(DamageClass.Melee);
            float adaptTaken = melee ? GameModeTuning.AsuraTrueMeleeAdaptTaken : 1f;
            modifiers.FinalDamage *= ResistFactor(ItemKey(item.type), adaptTaken);
            if (melee) {
                modifiers.FinalDamage *= CloseRangeMult(player, npc);
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (!Eligible(npc)) {
                return;
            }
            //ownerHitCheck 是手持刀刃弹幕的通行标记，灾厄真近战伤害类是另一路信号
            bool melee = projectile.DamageType.CountsAsClass(DamageClass.Melee);
            bool blade = melee && (projectile.ownerHitCheck || CWRRef.IsTrueMeleeClass(projectile.DamageType));
            float adaptTaken = blade ? GameModeTuning.AsuraTrueMeleeAdaptTaken
                : melee ? GameModeTuning.AsuraMeleeProjAdaptTaken : 1f;
            modifiers.FinalDamage *= ResistFactor(ProjKey(projectile.type), adaptTaken);
            Player owner = Main.player[projectile.owner];
            if (melee && owner.active) {
                modifiers.FinalDamage *= CloseRangeMult(owner, npc);
            }
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
