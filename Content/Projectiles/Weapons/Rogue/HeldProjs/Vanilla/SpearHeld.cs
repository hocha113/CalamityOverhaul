using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs.Vanilla
{
    internal class SpearHeld : BaseThrowable
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.Spear].Value;
        public override void SetThrowable() {
            CWRUtils.SafeLoadItem(ItemID.Spear);
            Projectile.alpha = 255;
            HandOnTwringMode = -15;
            OnThrowingGetRotation = (float a) => ToMouseA;
            OnThrowingGetCenter = (float armRotation) 
                => Owner.GetPlayerStabilityCenter() + Vector2.UnitY.RotatedBy(armRotation * Owner.gravDir) 
                * HandOnTwringMode * Owner.gravDir + UnitToMouseV * 6;
        }

        public override void OnThrowing() {
            base.OnThrowing();
        }

        public override void FlyToMovementAI() => Projectile.rotation = Projectile.velocity.ToRotation();

        public override bool PreThrowOut() {
            SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
            Projectile.velocity = UnitToMouseV * 17.5f;
            Projectile.tileCollide = true;
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) { }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = oldVelocity * 0.6f;
            Projectile.alpha -= 15;
            return false;
        }

        public override void DrawThrowable(Color lightColor) {
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, null, lightColor * (Projectile.alpha / 255f)
                , Projectile.rotation + (MathHelper.PiOver4 + OffsetRoting) * (Projectile.velocity.X > 0 ? 1 : -1)
                , TextureValue.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0);
        }
    }
}
