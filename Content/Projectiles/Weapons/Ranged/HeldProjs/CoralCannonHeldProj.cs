using CalamityMod.Projectiles.Ranged;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class CoralCannonHeldProj : BaseFeederGun
    {
        public override string Texture => IsKreload ? CWRConstant.Item_Ranged + "CoralCannon_PrimedForAction" : CWRConstant.Cay_Wap_Ranged + "CoralCannon";
        public override int targetCayItem => ModContent.ItemType<CalamityMod.Items.Weapons.Ranged.CoralCannon>();
        public override int targetCWRItem => ModContent.ItemType<CoralCannonEcType>();
        public override void SetRangedProperty() => loadTheRounds = CWRSound.CaseEjection2 with { Pitch = -0.2f };
        public override void FiringShoot() {
            SoundEngine.PlaySound(Item.UseSound.Value, Projectile.Center);
            DragonsBreathRifleHeldProj.SpawnGunDust(Projectile, Projectile.Center, ShootVelocity);
            Projectile.NewProjectile(Owner.parent(), Projectile.Center, ShootVelocity
                    , ModContent.ProjectileType<SmallCoral>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, 0, 1);
        }
    }
}
