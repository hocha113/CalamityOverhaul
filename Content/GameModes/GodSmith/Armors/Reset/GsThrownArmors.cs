using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 投掷系两套（忍者/化石）的公共层：套装奖励作用于飞镖（吹箭弹药）、飞刀、手里剑与尖球——
    /// 不再消耗、射速 +20%、固定加伤、命中挂减益；加伤与减益量由子类给出。
    /// 弹幕来源靠 <see cref="GodSmithArmorProjMark"/> 回溯（子弹幕承签，分裂镖同样算）
    /// </summary>
    internal abstract class GsThrownArmorScheme : GsResetArmorScheme
    {
        /// <summary>手掷类消耗品：飞刀、毒飞刀、骨飞刀、手里剑、尖球</summary>
        private static readonly int[] ThrownItems = [
            ItemID.ThrowingKnife, ItemID.PoisonedKnife, ItemID.BoneDagger, ItemID.Shuriken, ItemID.SpikyBall,
        ];

        /// <summary>套装奖励附加的固定伤害</summary>
        protected abstract int BonusDamage { get; }

        /// <summary>命中挂的减益</summary>
        protected abstract int InflictBuff { get; }

        /// <summary>减益持续帧数</summary>
        protected virtual int InflictFrames => 300;

        /// <summary>该物品是否受本套奖励：手掷消耗品或吹箭类武器</summary>
        protected static bool IsThrownWeapon(Item item)
            => item != null && !item.IsAir && (Array.IndexOf(ThrownItems, item.type) >= 0 || item.useAmmo == AmmoID.Dart);

        public override void ModifyEndowWeaponDamage(Player player, Item item, ref StatModifier damage) {
            if (IsThrownWeapon(item)) {
                damage.Flat += BonusDamage;
            }
        }

        public override float EndowUseSpeedMultiplier(Player player, Item item) => IsThrownWeapon(item) ? 1.2f : 1f;

        public override bool? EndowConsumeItem(Player player, Item item)
            => Array.IndexOf(ThrownItems, item.type) >= 0 ? false : null;

        public override bool? EndowCanConsumeAmmo(Player player, Item weapon, Item ammo)
            => ammo != null && ammo.ammo == AmmoID.Dart ? false : null;

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (sourceProj == null) {
                return;
            }
            GodSmithArmorProjMark.SourceOf(sourceProj, out int itemType, out int ammoType);
            bool thrown = Array.IndexOf(ThrownItems, itemType) >= 0;
            bool dart = ammoType > 0 && ContentSamples.ItemsByType.TryGetValue(ammoType, out Item ammo) && ammo.ammo == AmmoID.Dart;
            if (thrown || dart) {
                target.AddBuff(InflictBuff, InflictFrames);
            }
        }
    }

    /// <summary>
    /// 忍者套：兜帽暴击 +10%，衣召唤栏 +1，裤移速 +10%；套装奖励为投掷物不消耗、射速 +20%、伤害 +10、命中流血
    /// </summary>
    internal class GsNinjaArmor : GsThrownArmorScheme
    {
        public override int[] HeadIDs => [ItemID.NinjaHood];
        public override int BodyID => ItemID.NinjaShirt;
        public override int LegsID => ItemID.NinjaPants;

        protected override int BonusDamage => 10;
        protected override int InflictBuff => BuffID.Bleeding;

        protected override string HeadLineFallback => "10% increased critical strike chance";
        protected override string BodyLineFallback => "Increases your max number of minions by 1";
        protected override string LegsLineFallback => "10% increased movement speed";
        protected override string SetBonusLineFallback =>
            "Darts, throwing knives, shurikens and spiky balls are not consumed, fire 20% faster, deal 10 more damage and inflict Bleeding";

        public override void UpdateHead(Player player, Item item) => player.GetCritChance(DamageClass.Generic) += 10f;

        public override void UpdateBody(Player player, Item item) => player.maxMinions += 1;

        public override void UpdateLegs(Player player, Item item) => player.moveSpeed += 0.10f;
    }

    /// <summary>
    /// 化石套：头盔暴击 +15%，甲召唤栏 +2，腿移速 +10%；套装奖励为投掷物不消耗、射速 +20%、伤害 +7、命中中毒
    /// </summary>
    internal class GsFossilArmor : GsThrownArmorScheme
    {
        public override int[] HeadIDs => [ItemID.FossilHelm];
        public override int BodyID => ItemID.FossilShirt;
        public override int LegsID => ItemID.FossilPants;

        protected override int BonusDamage => 7;
        protected override int InflictBuff => BuffID.Poisoned;

        protected override string HeadLineFallback => "15% increased critical strike chance";
        protected override string BodyLineFallback => "Increases your max number of minions by 2";
        protected override string LegsLineFallback => "10% increased movement speed";
        protected override string SetBonusLineFallback =>
            "Darts, throwing knives, shurikens and spiky balls are not consumed, fire 20% faster, deal 7 more damage and inflict Poisoned";

        public override void UpdateHead(Player player, Item item) => player.GetCritChance(DamageClass.Generic) += 15f;

        public override void UpdateBody(Player player, Item item) => player.maxMinions += 2;

        public override void UpdateLegs(Player player, Item item) => player.moveSpeed += 0.10f;
    }
}
