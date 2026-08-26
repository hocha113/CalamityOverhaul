using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 热射线重铸：原版瞬发点射通道化为「持续照射」。
    /// 按住高频灼烧（0.25× / 3t），束长探地有物理落点；白热带束宽 ×1.6，
    /// 命中叠熔融满 5 层小爆 ×1.2；右键泄压「日冕闪射」按热量放全宽爆束；过载熄火进锁
    /// </summary>
    internal class GsHeatRay : GsHeatScheme
    {
        public override int TargetItemID => ItemID.HeatRay;

        protected override string GsDescFallback =>
            "Reforged: hold to sustain a scorching ray; white heat widens the beam and molten stacks detonate on the fifth\nRight click to vent all heat as a corona flash lance";

        internal override float HeatPerShot => 0f;
        internal override float CoolRatePerTick => 1.0f;
        internal override Color MuzzleTheme => GsConduitVFX.ForgeMain;

        public override bool? GsCanUseItem(Item item, Player player) {
            if (base.GsCanUseItem(item, player) == false) {
                return false;
            }
            if (HeldAlive<GsHeatBeamProj>(player)) {
                return false;
            }
            return null;
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.whoAmI == Main.myPlayer && !HeldAlive<GsHeatBeamProj>(player)) {
                Projectile.NewProjectile(source, player.MountedCenter, GsAimUnit(player),
                    ModContent.ProjectileType<GsHeatBeamProj>(), damage, knockback, player.whoAmI);
            }
            return false;
        }

        internal override void FireVent(Player player, GsHeatPlayer hp) {
            //日冕闪射：单向全宽重束，总伤随热量走（热满约基伤 ×3）
            float power = 0.8f + 2.2f * hp.Heat / GsHeatPlayer.HeatMax;
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(player.HeldItem) * power));
            Projectile.NewProjectile(player.GetSource_Misc("GsConduitVent"), player.MountedCenter, Vector2.Zero,
                ModContent.ProjectileType<GsConduitRayProj>(), damage, 7f, player.whoAmI,
                1f, GsAimUnit(player).ToRotation(), 2f);
        }
    }
}
