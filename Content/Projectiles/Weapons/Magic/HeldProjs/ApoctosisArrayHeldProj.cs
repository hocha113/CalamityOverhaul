using CalamityMod;
using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class ApoctosisArrayHeldProj : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "ApoctosisArray";
        public override int targetCayItem => ModContent.ItemType<ApoctosisArray>();
        public override int targetCWRItem => ModContent.ItemType<ApoctosisArrayEcType>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = 0;
            ShootPosNorlLengValue = 0;
            HandDistance = 25;
            HandDistanceY = 3;
            HandFireDistance = 25;
            HandFireDistanceY = -5;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
        }

        public override void FiringShoot() {
            Projectile proj = 
            Projectile.NewProjectileDirect(Source, GunShootPos, ShootVelocity, AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            float manaRatio = (float)Owner.statMana / Owner.statManaMax2;
            bool injectionNerf = Owner.Calamity().astralInjection;
            if (injectionNerf)
                manaRatio = MathHelper.Min(manaRatio, 0.65f);
            proj.scale = 0.75f + 0.75f * manaRatio;
            float manaRatio2 = (float)Owner.statMana / Owner.statManaMax2;
            bool injectionNerf2 = Owner.Calamity().astralInjection;
            if (injectionNerf2)
                manaRatio2 = MathHelper.Min(manaRatio2, 0.65f);
            float damageRatio = 0.2f + 1.4f * manaRatio2;
            proj.damage = (int)(proj.damage * damageRatio);
        }
    }
}
