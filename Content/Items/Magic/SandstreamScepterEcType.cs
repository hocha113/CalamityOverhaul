using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class SandstreamScepterEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "SandstreamScepter";
        public override void SetDefaults() {
            Item.SetItemCopySD<SandstreamScepter>();
            Item.SetHeldProj<SandstreamScepterHeld>();
        }
    }

    internal class RSandstreamScepter : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<SandstreamScepter>();
        public override int ProtogenesisID => ModContent.ItemType<SandstreamScepterEcType>();
        public override string TargetToolTipItemName => "SandstreamScepterEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<SandstreamScepterHeld>();
    }

    internal class SandstreamScepterHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "SandstreamScepter";
        public override int targetCayItem => ModContent.ItemType<SandstreamScepter>();
        public override int targetCWRItem => ModContent.ItemType<SandstreamScepterEcType>();
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
