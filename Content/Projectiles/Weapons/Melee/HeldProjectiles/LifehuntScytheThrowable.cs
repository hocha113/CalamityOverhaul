using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class LifehuntScytheThrowable : BaseThrowable
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "LifehuntScythe";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 17;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }
        public override void SetThrowable() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.scale = 2;
            HandOnTwringMode = -75;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 5;
            OffsetRoting = MathHelper.ToRadians(10);
        }

        public override void OnThrowing() {
            SetDirection();
            base.OnThrowing();
        }

        public override bool PreThrowOut() {
            SoundEngine.PlaySound(SoundID.Item45, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item71, Owner.Center);
            return true;
        }

        public override void FlyToMovementAI() {
            float rot = (MathHelper.PiOver2 * SafeGravDir - Owner.Center.To(Projectile.Center).ToRotation()) * DirSign * SafeGravDir;
            float rot2 = (MathHelper.PiOver2 * SafeGravDir - MathHelper.ToRadians(DirSign > 0 ? -20 : 200)) * DirSign * SafeGravDir;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, rot2 * -DirSign);

            if (DownLeft && Projectile.ai[2] < 60) {
                Owner.direction = Math.Sign(ToMouse.X);
                Projectile.ChasingBehavior(InMousePos, 33);
                Projectile.rotation += 0.6f * Owner.direction;
                if (Projectile.Distance(InMousePos) < 66) {
                    Projectile.ai[2]++;
                }
            }
            else {
                Projectile.ChasingBehavior(Owner.Center, 33);
                if (Projectile.Distance(Owner.Center) < 36) {
                    Projectile.Kill();
                }
                Projectile.rotation -= 0.6f * Owner.direction;
            }
            if (Projectile.soundDelay <= 0) {
                SoundEngine.PlaySound(SoundID.Item7, Projectile.Center);
                Projectile.soundDelay = 8;
            }
        }

        public override void DrawThrowable(Color lightColor) {
            Vector2 orig = TextureValue.Size() / 2;
            SpriteEffects spriteEffects = Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
            GameShaders.Armor.ApplySecondary(ContentSamples.CommonlyUsedContentSamples.ColorOnlyShaderIndex, Owner, null);

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + new Vector2(33, 33);
                Color color = Color.LimeGreen * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(TextureValue, drawPos, null, color * Projectile.Opacity * 0.3f
                    , Projectile.oldRot[k], orig, Projectile.scale, spriteEffects, 0);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + (MathHelper.PiOver4 + OffsetRoting) * (Projectile.velocity.X > 0 ? 1 : -1)
                , orig, Projectile.scale, spriteEffects, 0);
        }
    }
}
