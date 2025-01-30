using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged.Core;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class GoobowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Goobow";
        public override int TargetID => ModContent.ItemType<Goobow>();
        public override void BowShoot() {
            base.BowShoot();
            AmmoTypes = ModContent.ProjectileType<SlimeStream>();
            FireOffsetPos = ShootVelocity.GetNormalVector() * -5;
            base.BowShoot();
            FireOffsetPos = ShootVelocity.GetNormalVector() * 5;
            base.BowShoot();
            FireOffsetPos = Vector2.Zero;
        }
    }
}
