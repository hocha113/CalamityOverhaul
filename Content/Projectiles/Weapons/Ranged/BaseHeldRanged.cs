using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal abstract class BaseHeldRanged : BaseHeldProj
    {
        /// <summary>
        /// 一个通用的计时器
        /// </summary>
        public ref float Time => ref Projectile.ai[0];
        /// <summary>
        /// 手持物品实例
        /// </summary>
        public Item Item => Owner.ActiveItem();
        public virtual int targetCayItem => ItemID.None;
        public virtual int targetCWRItem => ItemID.None;
        public int AmmoTypes;
        public float ScaleFactor = 11f;
        public int WeaponDamage;
        public float WeaponKnockback;
        public Vector2 ShootVelocity => ScaleFactor * UnitToMouseV;
        public Vector2 ShootVelocityInProjRot => ScaleFactor * Projectile.rotation.ToRotationVector2();
        public bool HaveAmmo => Owner.PickAmmo(Owner.ActiveItem(), out _, out _, out _, out _, out _, true);
        protected bool onFire;

        public override bool ShouldUpdatePosition() => false;//一般来讲，不希望这类手持弹幕可以移动，因为如果受到速度更新，弹幕会发生轻微的抽搐

        protected bool UpdateConsumeAmmo(bool preCanConsumeAmmo = true) {
            bool canConsume = Owner.IsRangedAmmoFreeThisShot(new Item(Owner.GetShootState().UseAmmoItemType));
            Owner.PickAmmo(Owner.ActiveItem(), out _, out _, out _, out _, out _, canConsume && preCanConsumeAmmo);
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
            SetRangedProperty();
        }

        /// <summary>
        /// 用于设置额外的基础属性，在<see cref="SetDefaults"/>中被最后调用
        /// </summary>
        public virtual void SetRangedProperty() {

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
            bool heldBool1 = Item.type != targetCayItem;
            bool heldBool2 = Item.type != targetCWRItem;
            if (CWRServerConfig.Instance.ForceReplaceResetContent) {//如果开启了强制替换
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
