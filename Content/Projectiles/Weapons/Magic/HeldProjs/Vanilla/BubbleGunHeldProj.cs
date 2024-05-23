using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs.Vanilla
{
    internal class BubbleGunHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.BubbleGun].Value;
        public override int targetCayItem => ItemID.BubbleGun;
        public override int targetCWRItem => ItemID.BubbleGun;
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 15;
            HandDistanceY = 0;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
        }

        public override void FiringShoot() {
            for (int i = 0; i < Main.rand.Next(6, 9); i++) {
                int proj = Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity.RotatedByRandom(0.3f) * Main.rand.NextFloat(0.3f, 1.1f)
                        , Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
                if (Main.rand.NextBool(6)) {
                    Main.projectile[proj].Calamity().allProjectilesHome = true;
                }
            }
        }
    }
}
