using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CoralCannonHeldProj : TyrannysEndHeldProj
    {
        public override string Texture => isKreload ? CWRConstant.Item_Ranged + "CoralCannon_PrimedForAction" : CWRConstant.Cay_Wap_Ranged + "CoralCannon";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.CoralCannon>();
        public override int targetCWRItem => ModContent.ItemType<CoralCannon>();
        public override float ControlForce => 0.04f;
        public override float GunPressure => 0.75f;
        public override float Recoil => 5f;
        protected override int HandDistance => 10;
        protected override SoundStyle loadTheRounds => CWRSound.CaseEjection2 with { Pitch = -0.6f };
        public override void OnSpanProjFunc() {
            SoundEngine.PlaySound(heldItem.UseSound.Value, Projectile.Center);
            DragonsBreathRifleHeldProj.SpawnGunDust(Projectile, Projectile.Center, ShootVelocity);
            Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                    , ModContent.ProjectileType<SmallCoral>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 1);
        }
    }
}
