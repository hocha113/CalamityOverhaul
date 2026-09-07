using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 投掷系两套（忍者/化石）的公共层：单件沿用原版；手掷消耗品（飞刀、毒飞刀、骨飞刀、手里剑、尖球）不再消耗。
    /// 弹幕来源靠 <see cref="GodSmithArmorProjMark"/> 回溯（子弹幕承签，分裂镖同样算）
    /// </summary>
    internal abstract class GsThrownArmorScheme : GsResetArmorScheme
    {
        /// <summary>手掷类消耗品</summary>
        private static readonly int[] ThrownItems = [
            ItemID.ThrowingKnife, ItemID.PoisonedKnife, ItemID.BoneDagger, ItemID.Shuriken, ItemID.SpikyBall,
        ];

        public sealed override bool OverridesPieceStats => false;

        /// <summary>该物品是否手掷消耗品</summary>
        protected static bool IsThrownItem(Item item)
            => item != null && !item.IsAir && Array.IndexOf(ThrownItems, item.type) >= 0;

        /// <summary>这次命中的弹幕是否出自手掷消耗品</summary>
        protected static bool HitFromThrown(Projectile sourceProj) {
            if (sourceProj == null) {
                return false;
            }
            GodSmithArmorProjMark.SourceOf(sourceProj, out int itemType, out _);
            return Array.IndexOf(ThrownItems, itemType) >= 0;
        }

        public override bool? EndowConsumeItem(Player player, Item item) => IsThrownItem(item) ? false : null;
    }

    /// <summary>
    /// 忍者套 · 投术（通用）。单件沿用原版（三件各 +3% 暴击）。<br/>
    /// 原版旗标清点：+20% 移速 → 原样补回；无删除项。<br/>
    /// 签名：飞刀、手里剑、尖球不消耗、出手快 15%、伤害 +3 并造成流血
    /// </summary>
    internal class GsNinjaArmor : GsThrownArmorScheme
    {
        public override int[] HeadIDs => [ItemID.NinjaHood];
        public override int BodyID => ItemID.NinjaShirt;
        public override int LegsID => ItemID.NinjaPants;

        protected override string SetBonusLineFallback =>
            "20% increased movement speed; throwing knives, shurikens and spiky balls are not consumed, are thrown 15% faster, deal 3 more damage and inflict Bleeding";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.moveSpeed += 0.20f;
        }

        public override void ModifyEndowWeaponDamage(Player player, Item item, ref StatModifier damage) {
            if (IsThrownItem(item)) {
                damage.Flat += 3f;
            }
        }

        public override float EndowUseSpeedMultiplier(Player player, Item item) => IsThrownItem(item) ? 1.15f : 1f;

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (HitFromThrown(sourceProj)) {
                target.AddBuff(BuffID.Bleeding, 300);
            }
        }
    }

    /// <summary>
    /// 化石套 · 琥珀凝滞（远程）。单件沿用原版（头 +4% 远暴、衣 +5% 远伤、裤 +4% 远暴）。<br/>
    /// 原版旗标清点：20% 概率不消耗弹药（ammoCost80）→ 原样补回；无删除项。<br/>
    /// 签名：远程命中 20% 概率使敌人琥珀凝滞 2 秒（移速 −30%）；手掷消耗品照旧不消耗
    /// </summary>
    internal class GsFossilArmor : GsThrownArmorScheme
    {
        public override int[] HeadIDs => [ItemID.FossilHelm];
        public override int BodyID => ItemID.FossilShirt;
        public override int LegsID => ItemID.FossilPants;

        protected override string SetBonusLineFallback =>
            "20% chance not to consume ammo, and throwing knives, shurikens and spiky balls are not consumed; ranged hits have a 20% chance to trap the enemy in amber, slowing it by 30% for 2 seconds";

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.ammoCost80 = true;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (!hit.DamageType.CountsAsClass(DamageClass.Ranged) || Main.rand.Next(100) >= 20) {
                return;
            }
            target.AddBuff(ModContent.BuffType<GsAmberSlowBuff>(), 120);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustDirect(target.position, target.width, target.height, DustID.AmberBolt,
                    Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1f, 1f), 80, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
