using CalamityMod.Items.Weapons.Magic;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Magic;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic
{
    internal class SkyGlazeEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "SkyGlaze";
        public override void SetDefaults() {
            Item.SetItemCopySD<SkyGlaze>();
            Item.SetHeldProj<SkyGlazeHeld>();
        }
    }

    internal class RSkyGlaze : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<SkyGlaze>();
        public override int ProtogenesisID => ModContent.ItemType<SkyGlazeEcType>();
        public override string TargetToolTipItemName => "SkyGlazeEcType";
        public override void SetDefaults(Item item) => item.SetHeldProj<SkyGlazeHeld>();
    }

    internal class SkyGlazeHeld : BaseMagicGun
    {
        public override string Texture => CWRConstant.Cay_Wap_Magic + "SkyGlaze";
        public override int targetCayItem => ModContent.ItemType<SkyGlaze>();
        public override int targetCWRItem => ModContent.ItemType<SkyGlazeEcType>();
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
            for (int i = 3; i > 0; i--) {
                int leftorright = (InMousePos - Owner.Center).X > 0 ? 1 : -1;
                Vector2 starpos = Owner.Center + new Vector2(Main.rand.NextFloat(-120, 240) * leftorright, Main.rand.NextFloat(-600, -800));
                Vector2 vel = (InMousePos - starpos).UnitVector() * 16f;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item8);
                Projectile.NewProjectile(Source, starpos, vel
                    , AmmoTypes, WeaponDamage, WeaponKnockback, Owner.whoAmI, 0);
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float rot = DirSign > 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 * 3;
            Main.EntitySpriteDraw(TextureValue, drawPos, null, lightColor
                , Projectile.rotation + rot, TextureValue.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally);
        }
    }
}
