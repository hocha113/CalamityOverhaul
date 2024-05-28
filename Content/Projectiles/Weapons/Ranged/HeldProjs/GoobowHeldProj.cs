using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class GoobowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Cay_Wap_Ranged + "Goobow";
        public override int targetCayItem => ModContent.ItemType<Goobow>();
        public override int targetCWRItem => ModContent.ItemType<GoobowEcType>();
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
