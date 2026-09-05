using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 暗影束法杖重铸：原版瞬发反射光束通道化为「持续折线影束」。
    /// 按住持续照射，束沿墙面反射至多 3 段；热量=侵蚀，白热反射 +1 段且束宽 ×1.4
    /// </summary>
    internal class GsShadowbeamStaff : GsHeatScheme
    {
        public override int TargetItemID => ItemID.ShadowbeamStaff;

        protected override string GsDescFallback =>
            "Reforged: hold to sustain a shadow beam that ricochets off the walls; white heat adds one more bounce and thickens the beam";
        internal override float HeatPerShot => 0f;
        internal override float CoolRatePerTick => 1.0f;
        internal override GsOverloadPolicy OverloadPolicy => GsOverloadPolicy.Sustain;

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
    }
}
