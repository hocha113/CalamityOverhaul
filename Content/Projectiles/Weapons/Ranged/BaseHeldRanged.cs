using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal abstract class BaseHeldRanged : BaseHeldProj
    {
        public ref float Time => ref Projectile.ai[0];

        public Item heldItem => Owner.ActiveItem();
        public virtual int targetCayItem => 0;
        public virtual int targetCWRItem => 0;
        public int AmmoTypes;
        public float ScaleFactor = 11f;
        public int WeaponDamage;
        public float WeaponKnockback;
        public Vector2 ShootVelocity => ScaleFactor * UnitToMouseV;
        public bool HaveAmmo => Owner.PickAmmo(Owner.ActiveItem(), out _, out _, out _, out _, out _, true);
        protected bool onFire;

        protected bool UpdateConsumeAmmo() {
            bool canConsume = Owner.IsRangedAmmoFreeThisShot(new Item(Owner.GetShootState().UseAmmoItemType));
            Owner.PickAmmo(Owner.ActiveItem(), out _, out _, out _, out _, out _, canConsume);
            return canConsume;
        }

        protected void UpdateShootState() => Owner.PickAmmo(Owner.ActiveItem(), out AmmoTypes, out ScaleFactor, out WeaponDamage, out WeaponKnockback, out _, true);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.hide = true;
        }

        public override bool? CanDamage() => false;

        public override bool PreAI() {
            if (!CheckAlive()) {
                Projectile.Kill();
                return false;
            }
            if (Owner.PressKey() && !Owner.mouseInterface) {
                Owner.itemTime = 2;
            }
            UpdateShootState();
            return true;
        }

        public override void AI() {
            InOwner();
            if (Projectile.IsOwnedByLocalPlayer()) {
                SpanProj();
            }
            Time++;
        }

        public virtual bool CheckAlive() {
            bool heldBool1 = heldItem.type != targetCayItem;
            bool heldBool2 = heldItem.type != targetCWRItem;
            if (ContentConfig.Instance.ForceReplaceResetContent) {//如果开启了强制替换
                if (heldBool1) {//只需要判断原版的物品
                    return false;
                }
            }
            else {//如果没有开启强制替换
                if (heldBool2) {
                    return false;
                }
            }
            return true;
        }

        public virtual void InOwner() {

        }

        public virtual void SpanProj() {

        }
    }
}
