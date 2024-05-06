using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Ranged.Extras;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged.HeldProjs
{
    internal class NeutronBowHeldProj : BaseBow
    {
        public override string Texture => CWRConstant.Item_Ranged + "NeutronBow";
        public override int targetCayItem => NeutronBow.PType;
        public override int targetCWRItem => NeutronBow.PType;
        float Charge {
            get => ((NeutronBow)Item.ModItem).Charge;
            set => ((NeutronBow)Item.ModItem).Charge = value;
        }
        int uiframe;
        public override void SetRangedProperty() {
            base.SetRangedProperty();
        }

        public override void PostInOwner() {
            if (CanFire) {
                CWRUtils.ClockFrame(ref Projectile.frame, 2, 15);
            }
        }

        public override void BowShoot() {
            base.BowShoot();
        }

        public override void BowDraw(ref Color lightColor) {
            if (Item != null && !Item.IsAir && Item.type == NeutronBow.PType) {
                NeutronGlaiveHeld.DrawBar(Owner, Charge, uiframe);
            }
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(TextureValue, Projectile.frame, 16), onFire ? Color.White : lightColor
                , Projectile.rotation, CWRUtils.GetOrig(TextureValue, 16), Projectile.scale, DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }
}
