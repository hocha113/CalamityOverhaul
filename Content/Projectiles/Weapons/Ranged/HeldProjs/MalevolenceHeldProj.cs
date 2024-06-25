using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class MalevolenceHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Malevolence";
        public override int targetCayItem => ModContent.ItemType<Malevolence>();
        public override int targetCWRItem => ModContent.ItemType<MalevolenceEcType>();
        public override void SetRangedProperty() {
            BowArrowDrawNum = 2;
            HandFireDistance = 20;
            DrawArrowMode = -24;
        }
        public override void BowShoot() {
            if (AmmoTypes == ProjectileID.WoodenArrowFriendly) {
                AmmoTypes = ModContent.ProjectileType<PlagueArrow>();
            }
            FireOffsetVector = ShootVelocity.RotatedByRandom(0.3f) * Main.rand.NextFloat(0.2f, 0.23f);
            base.BowShoot();
            FireOffsetVector = ShootVelocity.RotatedByRandom(0.3f) * Main.rand.NextFloat(0.2f, 0.23f);
            base.BowShoot();
        }
    }
}
