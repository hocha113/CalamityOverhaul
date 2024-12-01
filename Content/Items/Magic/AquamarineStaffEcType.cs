using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class AquamarineStaffEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "AquamarineStaff";
        public override void SetDefaults() {
            Item.SetItemCopySD<AquamarineStaff>();
            Item.SetHeldProj<AquamarineStaffHeld>();
        }
    }

    internal class RAquamarineStaff : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<AquamarineStaff>();
        public override int ProtogenesisID => ModContent.ItemType<AquamarineStaffEcType>();
        public override string TargetToolTipItemName => "AquamarineStaffEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<AquamarineStaffHeld>();
    }

    internal class AquamarineStaffHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "AquamarineStaff";
        public override int targetCayItem => ModContent.ItemType<AquamarineStaff>();
        public override int targetCWRItem => ModContent.ItemType<AquamarineStaffEcType>();
        public override void SetMagicProperty() {
            ShootPosToMouLengValue = -30;
            ShootPosNorlLengValue = 0;
            HandDistance = 30;
            HandDistanceY = 0;
            HandFireDistance = 30;
            HandFireDistanceY = 0;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            Onehanded = true;
            GunPressure = 0;
            ControlForce = 0;
            Recoil = 0;
        }

        public virtual bool DrawingInfo => false;

        public override void FiringShoot() {
            Projectile.NewProjectile(Source, GunShootPos + (InMousePos - Owner.Center).UnitVector() * 48f, ShootVelocity
                , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float rot = DirSign > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 * 3;
            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , Projectile.rotation + rot, TextureValue.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally);
        }
    }
}
