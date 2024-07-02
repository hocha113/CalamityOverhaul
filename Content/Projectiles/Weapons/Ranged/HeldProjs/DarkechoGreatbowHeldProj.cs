using CalamityMod.Items.Weapons.Ranged;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class DarkechoGreatbowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "DarkechoGreatbow";
        public override int targetCayItem => ModContent.ItemType<DarkechoGreatbow>();
        public override int targetCWRItem => ModContent.ItemType<DarkechoGreatbowEcType>();
        public override void SetRangedProperty() => BowArrowDrawNum = 2;
        public override void BowShoot() {
            FireOffsetVector = ShootVelocity.UnitVector().RotatedByRandom(0.3f) * Main.rand.NextFloat(3.2f, 5.5f);
            base.BowShoot();
            FireOffsetVector = ShootVelocity.UnitVector().RotatedByRandom(0.3f) * Main.rand.NextFloat(3.2f, 5.5f);
            base.BowShoot();
            FireOffsetVector = Microsoft.Xna.Framework.Vector2.Zero;
            AmmoTypes = ProjectileID.CrystalDart;
            WeaponDamage *= 2;
            base.BowShoot();
        }
    }
}
