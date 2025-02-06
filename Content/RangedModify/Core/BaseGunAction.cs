using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.RangedModify.Core
{
    internal abstract class BaseGunAction : BaseGun
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override Texture2D TextureValue => TextureAssets.Item[TargetID].Value;
        public override int TargetID => ItemID.None;
        private int useAnimation;
        public override void SetRangedProperty() {
            base.SetRangedProperty();
        }

        public override void Initialize() => useAnimation = Item.useAnimation;

        public override bool CanSpanProj() {
            bool reset = base.CanSpanProj();
            if (Item.useLimitPerAnimation.HasValue) {
                if (fireIndex > Item.useLimitPerAnimation) {
                    if (--useAnimation <= 0) {
                        fireIndex = 0;
                        return reset;
                    }
                    return false;
                }
            }
            else {
                if (--useAnimation <= 0) {
                    fireIndex = 0;
                    return reset;
                }
                return false;
            }
            return reset;
        }

        public override void HanderPlaySound() {
            if (Item.ModItem != null && !Item.ModItem.CanUseItem(Owner)) {
                return;
            }
            useAnimation -= Item.useTime;
            if (useAnimation <= 0) {
                SoundEngine.PlaySound(Item.UseSound, Projectile.Center);
                useAnimation = Item.useAnimation;
            }
        }

        public override void FiringShoot() {
            if (Item.ModItem != null && !Item.ModItem.CanUseItem(Owner)) {
                return;
            }
            fireIndex++;
            OrigItemShoot();
        }

        public override void FiringShootR() {
            if (Item.ModItem != null && !Item.ModItem.CanUseItem(Owner)) {
                return;
            }
            fireIndex++;
            Owner.altFunctionUse = 2;
            OrigItemShoot();
        }


        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            base.GunDraw(drawPos, ref lightColor);
        }
    }
}
