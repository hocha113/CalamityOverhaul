using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class AethersWhisperHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "AethersWhisper";
        public override int targetCayItem => ModContent.ItemType<AethersWhisper>();
        public override int targetCWRItem => ModContent.ItemType<AethersWhisperEcType>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 60;
            ShootPosNorlLengValue = -2;
            HandDistance = 45;
            HandDistanceY = 9;
            HandFireDistance = 45;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;        
        }

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos, Vector2.Zero, ModContent.ProjectileType<AethersWhisperOnSpan>()
                , WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, Projectile.whoAmI);
        }
    }
}
