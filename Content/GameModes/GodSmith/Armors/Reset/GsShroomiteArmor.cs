using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 蘑菇套（三顶头盔）：单件与原版潜行机制照旧（放行原版套装奖励），叠加远程暴击 +15%、
    /// 弹药 30% 概率不消耗；潜行深时远程命中有 30% 概率炸开孢子云，造成武器面板 1.3 倍的伤害
    /// </summary>
    internal class GsShroomiteArmor : GsResetArmorScheme
    {
        public override int[] HeadIDs => [ItemID.ShroomiteHeadgear, ItemID.ShroomiteMask, ItemID.ShroomiteHelmet];
        public override int BodyID => ItemID.ShroomiteBreastplate;
        public override int LegsID => ItemID.ShroomiteLeggings;
        public override bool OverridesPieceStats => false;
        public override bool KeepsVanillaSetBonus => true;

        protected override string SetBonusLineFallback =>
            "Standing still turns you stealthy as before; 15% increased ranged critical strike chance and a 30% chance not to consume ammo; while deep in stealth, ranged hits have a 30% chance to burst a spore cloud for 1.3x your weapon's damage";

        /// <summary>潜行阈值：原版 stealth 从 1 降到 0，越低越隐蔽</summary>
        private const float DeepStealth = 0.3f;

        public override void UpdateSetBonus(Player player, GodSmithArmorPlayer state) {
            player.GetCritChance(DamageClass.Ranged) += 15f;
        }

        public override bool? EndowCanConsumeAmmo(Player player, Item weapon, Item ammo)
            => weapon.DamageType.CountsAsClass(DamageClass.Ranged) && Main.rand.Next(100) < 30 ? false : null;

        public override bool IsOwnEndowProj(Projectile proj) => proj.type == ModContent.ProjectileType<GsArmorBlastProj>();

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (player.whoAmI != Main.myPlayer || player.stealth > DeepStealth
                || !hit.DamageType.CountsAsClass(DamageClass.Ranged) || Main.rand.Next(100) >= 30) {
                return;
            }
            SpawnBlast(player, target.Center, (int)(WeaponPanelDamage(player, hit) * 1.3f), 120f,
                "GodSmithShroomiteEndow", GsArmorBlastProj.Style.Spore);
        }
    }
}
