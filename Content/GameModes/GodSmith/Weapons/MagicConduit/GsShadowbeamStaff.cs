using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 暗影束法杖重铸：原版瞬发反射光束通道化为「持续折线影束」。
    /// 按住持续照射，束沿墙面反射至多 3 段；热量=侵蚀，白热反射 +1 段且束宽 ×1.4；
    /// 右键泄压「影爆」沿折线各拐点与落点连环炸开靛蓝环爆
    /// </summary>
    internal class GsShadowbeamStaff : GsHeatScheme
    {
        public override int TargetItemID => ItemID.ShadowbeamStaff;

        protected override string GsDescFallback =>
            "Reforged: hold to sustain a shadow beam that ricochets off the walls; white heat adds one more bounce and thickens the beam" +
            "\nRight click to vent everything as chained shadow bursts along every bend of the beam";

        internal override float HeatPerShot => 0f;
        internal override float CoolRatePerTick => 1.0f;
        internal override GsOverloadPolicy OverloadPolicy => GsOverloadPolicy.Sustain;
        internal override float VentMinHeat => 30f;
        internal override Color MuzzleTheme => GsShadowbeamStaffHeldProj.ShadowMain;

        public override bool? GsCanUseItem(Item item, Player player) {
            if (base.GsCanUseItem(item, player) == false) {
                return false;
            }
            if (HeldAlive<GsShadowbeamStaffHeldProj>(player)) {
                return false;
            }
            return null;
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //全接管：左键只负责唤起折线通道 held（动画法由 held 的族层持械姿态达标）
            if (player.whoAmI == Main.myPlayer && !HeldAlive<GsShadowbeamStaffHeldProj>(player)) {
                Projectile.NewProjectile(source, player.MountedCenter, GsAimUnit(player),
                    ModContent.ProjectileType<GsShadowbeamStaffHeldProj>(), damage, knockback, player.whoAmI);
            }
            return false;
        }

        /// <summary>泄压要求引导中（拐点来自在场折线）</summary>
        internal override bool VentReady(Player player, GsHeatPlayer hp)
            => HeldAlive<GsShadowbeamStaffHeldProj>(player);

        internal override void FireVent(Player player, GsHeatPlayer hp) {
            //影爆：沿当前折线每个拐点与落点各起一团靛蓝环爆（威力随侵蚀，逐点衰减 0.85）
            GsShadowbeamStaffHeldProj held = null;
            int type = ModContent.ProjectileType<GsShadowbeamStaffHeldProj>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == type && p.owner == player.whoAmI) {
                    held = p.ModProjectile as GsShadowbeamStaffHeldProj;
                    break;
                }
            }
            if (held == null) {
                return;
            }
            float frac = hp.Heat / GsHeatPlayer.HeatMax;
            float power = 0.7f + 1.3f * frac;
            for (int i = 1; i < held.NodeCount; i++) {
                int damage = Math.Max(1, (int)(player.GetWeaponDamage(player.HeldItem) * power));
                Projectile.NewProjectile(player.GetSource_Misc("GsConduitVent"), held.Nodes[i], Vector2.Zero,
                    ModContent.ProjectileType<GsConduitNovaProj>(), damage, 5f, player.whoAmI,
                    110f + 3 * 1024f);
                power *= 0.85f;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.9f, Pitch = -0.35f }, player.Center);
            }
        }
    }
}
