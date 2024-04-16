using CalamityMod;
using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class SnowblindnessHeldProj : BaseFeederGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "Snowblindness";
        public override int targetCayItem => ModContent.ItemType<Snowblindness>();
        public override int targetCWRItem => ModContent.ItemType<Snowblindness>();
        public override void SetRangedProperty() {
            loadTheRounds = CWRSound.Gun_HandGun_ClipOut with { Pitch = -0.2f };
            HandDistance = 30;
            HandFireDistance = 20;
            HandFireDistanceY = -6;
            Recoil = 0.75f;
            FireTime = 3;
            EnableRecoilRetroEffect = true;
            RecoilRetroForceMagnitude = 6;
            RecoilOffsetRecoverValue = 0.6f;
            RangeOfStress = 50;
            ShootPosNorlLengValue = 1;
        }
        public override void FiringShoot() {
            int proj = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity.RotatedByRandom(0.1f), AmmoTypes, WeaponDamage / 2, WeaponKnockback, Owner.whoAmI, 0, 1);
            Main.projectile[proj].Calamity().allProjectilesHome = true;
            Main.projectile[proj].CWR().GetHitAttribute.SuperAttack = true;
            Main.projectile[proj].extraUpdates = 1;
            Main.projectile[proj].usesLocalNPCImmunity = true;
            Main.projectile[proj].localNPCHitCooldown = -1;
            int bolt = ProjectileID.IceBolt;
            bool isbeam = false;
            if (Main.rand.NextBool(3)) {
                bolt = ProjectileID.FrostBeam;
                isbeam = true;
            }
            int proj2 = Projectile.NewProjectile(Source, GunShootPos, ShootVelocity, bolt, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 1);
            Main.projectile[proj2].extraUpdates = 1;
            Main.projectile[proj2].friendly = true;
            Main.projectile[proj2].hostile = false;
            Main.projectile[proj2].DamageType = DamageClass.Ranged;
            if (isbeam) {
                Main.projectile[proj2].damage *= 2;
                Main.projectile[proj2].usesLocalNPCImmunity = true;
                Main.projectile[proj2].localNPCHitCooldown = -1;
            }
        }
    }
}
