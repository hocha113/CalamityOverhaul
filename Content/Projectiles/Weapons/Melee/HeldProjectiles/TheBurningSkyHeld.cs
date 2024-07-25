using CalamityMod;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class TheBurningSkyHeld : BaseSwing
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TheBurningSky";
        public float progress;
        Vector2 armCenter;
        public override string gradientTexturePath => CWRConstant.Masking + "DragonRageEffectColorBar";
        public override void SetSwingProperty() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = 72;
            Projectile.height = 72;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 4;
            Projectile.usesLocalNPCImmunity = true;
            distanceToOwner = 30;
            trailTopWidth = 60;
            canDrawSlashTrail = true;
        }

        public override void SwingAI() {
            if (Time == 0) {
                SoundEngine.PlaySound(SoundID.Item71, Owner.position);
                Rotation = MathHelper.ToRadians(-33 * Owner.direction);
                startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                speed = MathHelper.ToRadians(5);
            }
            if (Time == 10 * updateCount && Projectile.IsOwnedByLocalPlayer()) {
                float meleeSpeedAf = SetSwingSpeed(1);
                float count = 9 / meleeSpeedAf;
                SoundEngine.PlaySound(SoundID.Item70, Owner.Center);
                float speed = 9 / meleeSpeedAf;
                for (int i = 0; i < count; ++i) {
                    float randomSpeed = speed * Main.rand.NextFloat(0.7f, 1.4f);
                    CalamityUtils.ProjectileRain(Projectile.GetSource_FromAI(), InMousePos
                        , 290f, 130f, 850f, 1100f, randomSpeed, ModContent.ProjectileType<BurningMeteor>()
                        , Projectile.damage / 6, 6f, Owner.whoAmI);
                }
                for (int i = 0; i < 20; i++) {
                    int dust = Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height, DustID.OrangeTorch, Scale: 6);
                    Main.dust[dust].position = Projectile.Center;
                    Main.dust[dust].velocity = (MathHelper.TwoPi / 20f * i).ToRotationVector2() * 16;
                    Main.dust[dust].noGravity = true;
                }
            }
            if (Time < 10) {
                Length *= 1 + 0.1f / updateCount;
                Rotation += speed * Projectile.spriteDirection;
                speed *= 1 + 0.2f / updateCount;
                vector = startVector.RotatedBy(Rotation) * Length;
                Projectile.scale += 0.06f;
            }
            else {
                Length *= 1 - 0.01f / updateCount;
                Rotation += speed * Projectile.spriteDirection;
                speed *= 1 - 0.2f / updateCount;
                vector = startVector.RotatedBy(Rotation) * Length;
                if (Projectile.scale > 1f) {
                    Projectile.scale -= 0.01f;
                }
            }
            if (Time >= 22 * updateCount) {
                Projectile.Kill();
            }
            if (Time % updateCount == updateCount - 1) {
                Length = MathHelper.Clamp(Length, 120, 160);
            }
        }

        public override void DrawTrail(List<VertexPositionColorTexture> bars) {
            Effect effect = CWRMod.Instance.Assets.Request<Effect>(CWRConstant.noEffects + "KnifeRendering").Value;

            effect.Parameters["transformMatrix"].SetValue(GetTransfromMaxrix());
            effect.Parameters["sampleTexture"].SetValue(TrailTexture);
            effect.Parameters["gradientTexture"].SetValue(GradientTexture);
            //应用shader，并绘制顶点
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
                Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
            }
        }

        public override void DrawSwing(SpriteBatch spriteBatch, Color lightColor) {
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            Rectangle rect = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 drawOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Vector2 v = Projectile.Center - RodingToVer(48, (Projectile.Center - Owner.Center).ToRotation());

            float drawRoting = Projectile.rotation + MathHelper.ToRadians(24) * Projectile.spriteDirection;
            if (Projectile.spriteDirection == -1) {
                drawRoting += MathHelper.Pi;
            }

            Main.EntitySpriteDraw(texture, v - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY, new Rectangle?(rect)
                , Color.White, drawRoting, drawOrigin, Projectile.scale, effects, 0);
        }
    }
}
