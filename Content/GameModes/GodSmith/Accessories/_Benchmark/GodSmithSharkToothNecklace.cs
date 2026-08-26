using System;
using CalamityOverhaul.Content.GameModes.GodSmith.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Accessories._Benchmark
{
    /// <summary>
    /// 【范例·饰品重铸】鲨牙项链：保底数值行 +4% 伤害；
    /// 特色机制「撕裂」：命中叠层穿甲，叠层带内置冷却
    /// （冷却走 <see cref="GodSmithPlayer"/> 通用冷却表，键 = 物品 type 的用法范例）。<br/>
    /// 层数这类复杂每玩家状态放同文件私有 ModPlayer，
    /// 这就是「共享类零字段改动」规矩的标准做法
    /// </summary>
    internal class GodSmithSharkToothNecklace : GodSmithAccEffect
    {
        /// <summary>撕裂叠层上限</summary>
        internal const int RendMaxStacks = 8;

        /// <summary>撕裂持续帧数（命中刷新）</summary>
        internal const int RendDuration = 180;

        /// <summary>叠层内置冷却帧数，防高射速武器瞬间叠满</summary>
        internal const int RendICD = 10;

        public override int[] TargetItemIDs => [ItemID.SharkToothNecklace];

        protected override string EffectDescFallback =>
            "+4% damage\nStrikes tear the target's hide, granting Rend: +1 armor penetration per stack, up to 8 stacks, lasts 3s";

        public override void UpdateAccessory(Item item, Player player, bool hideVisual, GodSmithPlayer state) {
            //保底数值行
            player.GetDamage(DamageClass.Generic) += 0.04f;
            //撕裂层转穿甲；层数衰减在私有 ModPlayer 里自走
            SharkToothRendPlayer rend = player.GetModPlayer<SharkToothRendPlayer>();
            if (rend.Stacks > 0) {
                player.GetArmorPenetration(DamageClass.Generic) += rend.Stacks;
            }
        }

        public override void OnHitNPC(Item item, Player player, GodSmithPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, bool fromProjectile) {
            //内置冷却：通用冷却表一步完成判定加占用
            if (!state.TryUseCooldown(item.type, RendICD)) {
                return;
            }
            player.GetModPlayer<SharkToothRendPlayer>().AddStack();
            //撕咬血雾，全用原版 dust（命中钩子只在攻击方端跑，无需 isServer 守门）
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(target.Center, DustID.Blood,
                    Main.rand.NextVector2Circular(3f, 3f), 100, default, Main.rand.NextFloat(1f, 1.5f));
                dust.noGravity = Main.rand.NextBool();
            }
        }
    }

    /// <summary>撕裂层数的私有状态载体：内容文件自建 ModPlayer、不动共享类的范例。
    /// 状态只在攻击方本地端产生与消费，无需同步</summary>
    internal class SharkToothRendPlayer : ModPlayer
    {
        /// <summary>当前撕裂层数</summary>
        internal int Stacks { get; private set; }

        private int timer;

        internal void AddStack() {
            Stacks = Math.Min(Stacks + 1, GodSmithSharkToothNecklace.RendMaxStacks);
            timer = GodSmithSharkToothNecklace.RendDuration;
        }

        public override void PostUpdateMiscEffects() {
            if (timer > 0 && --timer == 0) {
                Stacks = 0;
            }
        }

        public override void UpdateDead() {
            Stacks = 0;
            timer = 0;
        }
    }
}
