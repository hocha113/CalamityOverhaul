using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    internal abstract class BaseMagicGun : BaseGun
    {
        public override void SetRangedProperty() {
            Projectile.DamageType = DamageClass.Magic;
            SetMagicProperty();
        }

        public override void FiringShoot() {
            if (Owner.CheckMana(Item)) {
                Shoot();
                CreateRecoil();
                Owner.statMana -= Item.mana;
                if (Owner.statMana < 0) {
                    Owner.statMana = 0;
                }
            }
        }

        public virtual void SetMagicProperty() {

        }

        public virtual int Shoot() {
            return Projectile.NewProjectile(Source, Projectile.Center, ShootVelocity, Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }
    }
}
