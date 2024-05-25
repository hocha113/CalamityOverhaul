using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria.GameContent;
using Terraria.ID;
using Terraria;
using System.IO;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs.Vanilla
{
    internal class FlamarangHeld : BaseBoomerang
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Flamarang].Value;
        public override void SetBoomerang() {
            CWRUtils.SafeLoadItem(ItemID.Flamarang);
            HandOnTwringMode = -30;
            OffsetRoting = MathHelper.ToRadians(30 + 180);
        }

        public override void PostSetBoomerang() {
            if (stealthStrike && Projectile.ai[2] == 0) {
                Projectile.scale *= 1.25f;
            }
        }

        public override bool PreThrowOut() {
            if (stealthStrike && Projectile.ai[2] == 0) {
                for (int i = 0; i < 2; i++) {
                    Projectile.NewProjectileDirect(Projectile.GetSource_FromAI(), Projectile.Center
                        , Projectile.velocity.RotatedBy(i == 0 ? -0.3f : 0.3f), Type, Projectile.damage, 0.2f, Owner.whoAmI, ai2: 1);
                }
            }
            return base.PreThrowOut();
        }
    }
}
