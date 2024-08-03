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
        private bool onTIle;
        private float tileRot;
        public override void SetThrowable() {
            CWRUtils.SafeLoadItem(ItemID.Spear);
            Projectile.width = Projectile.height = 11;
            Projectile.alpha = 255;
            HandOnTwringMode = -15;
            OnThrowingGetRotation = (float a) => ToMouseA;
            OnThrowingGetCenter = (float armRotation)
                => Owner.GetPlayerStabilityCenter() + Vector2.UnitY.RotatedBy(armRotation * Owner.gravDir)
                * HandOnTwringMode * Owner.gravDir + UnitToMouseV * 6;
        }

        public override void FlyToMovementAI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (++Projectile.ai[2] > 60 && !onTIle) {
                Projectile.velocity.Y += 0.3f;
                Projectile.velocity.X *= 0.99f;
            }
            if (onTIle) {
                Projectile.rotation = tileRot;
                Projectile.velocity *= 0.9f;
            }
        }

        public override bool PreThrowOut() {
            SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
            Projectile.velocity = UnitToMouseV * 17.5f;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            if (stealthStrike) {
                Projectile.damage *= 2;
                Projectile.ArmorPenetration = 10;
                Projectile.penetrate = 6;
                Projectile.extraUpdates = 3;
                Projectile.scale = 1.5f;
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) { }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (!onTIle) {
                Projectile.velocity /= 10;
                tileRot = Projectile.rotation;
                onTIle = true;
            }

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
