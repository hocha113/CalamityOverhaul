using CalamityMod;
using CalamityMod.Particles;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.ArkoftheCosmosProj
{
    /// <summary>
    /// 大型螺旋星辰弹幕
    /// </summary>
    internal class RendingNeedles : ModProjectile
    {
        internal PrimitiveTrail TrailDrawer;

        private const float MaxTime = 30f;

        public new string LocalizationCategory => "Projectiles.Melee";

        public override string Texture => CWRConstant.Projectile_Melee + "RendingNeedle";

        public float Timer => 30f - Projectile.timeLeft;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.scale = 2.4f;
            Projectile.friendly = true;
            Projectile.penetrate = 13;
            Projectile.timeLeft = 30;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float collisionPoint = 0f;
            float num = 44f * Projectile.scale;
            Vector2 vector = -Projectile.velocity.SafeNormalize(Vector2.Zero) * 16f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center + vector, Projectile.Center + vector + Projectile.velocity.SafeNormalize(Vector2.Zero) * num, 24f, ref collisionPoint);
        }

        public override void OnSpawn(IEntitySource source) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                int types = ModContent.ProjectileType<ProximaCentauri>();
                Projectile.NewProjectileDirect(
                    Projectile.parent(),
                    Projectile.Center,
                    Vector2.Zero,
                    types,
                    Projectile.damage,
                    0,
                    Projectile.owner,
                    0,
                    Projectile.whoAmI
                    ).netUpdate = true;
                Projectile.NewProjectileDirect(
                    Projectile.parent(),
                    Projectile.Center,
                    Vector2.Zero,
                    types,
                    Projectile.damage,
                    0,
                    Projectile.owner,
                    1,
                    Projectile.whoAmI
                    ).netUpdate = true;
            }//让对应的端生成两颗伴飞的比邻星
        }

        public override void AI() {
            Projectile.scale += 0.01f;
            if (Projectile.scale > 3.3f)
                Projectile.scale = 3.3f;
            Projectile.Opacity = 0.6f;
            Lighting.AddLight(Projectile.Center, 0.75f, 1f, 0.24f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathF.PI / 2f;
            Projectile.velocity *= 1f - (float)Math.Pow(Timer / 60f, 3.0) * 0.3f;
            if (Main.rand.NextBool(2)) {
                GeneralParticleHandler.SpawnParticle(new HeavySmokeParticle(Projectile.Center, Projectile.velocity * 0.5f, Color.Lerp(Color.DodgerBlue, Color.MediumVioletRed, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f)), 30, Main.rand.NextFloat(0.6f, 1.2f) * Projectile.scale * 0.6f, 0.28f, 0f, glowing: false, 0f, required: true));
                if (Main.rand.NextBool(3)) {
                    GeneralParticleHandler.SpawnParticle(new HeavySmokeParticle(Projectile.Center, Projectile.velocity * 0.5f, Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.7f), 20, Main.rand.NextFloat(0.4f, 0.7f) * Projectile.scale * 0.6f, 0.8f, 0f, glowing: true, 0.05f, required: true));
                }
            }

            if (Projectile.velocity.LengthSquared() < 1f) {
                Projectile.Kill();
            }
        }

        internal Color ColorFunction(float completionRatio) {
            float amount = MathHelper.Lerp(0.65f, 1f, (float)Math.Cos((0f - Main.GlobalTimeWrappedHourly) * 3f) * 0.5f + 0.5f);
            float num = Utils.GetLerpValue(1f, 0.64f, completionRatio, clamped: true) * Projectile.Opacity;
            Color value = Color.Lerp(Color.Cyan, Color.Crimson, (float)Math.Sin(completionRatio * MathF.PI * 1.6f - Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f);
            return Color.Lerp(Color.White, value, amount) * num;
        }

        internal float WidthFunction(float completionRatio) {
            float amount = (float)Math.Pow(1f - completionRatio, 2.0);
            return MathHelper.Lerp(0f, 14f * Projectile.scale, amount);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.timeLeft > 35) {
                return false;
            }

            Texture2D value = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/RendingNeedle").Value;
            if (TrailDrawer == null) {
                TrailDrawer = new PrimitiveTrail(WidthFunction, ColorFunction, null, GameShaders.Misc["CalamityMod:TrailStreak"]);
            }

            GameShaders.Misc["CalamityMod:TrailStreak"].SetShaderTexture(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/Trails/ScarletDevilStreak"));
            TrailDrawer.Draw(Projectile.oldPos, Projectile.Size * 0.5f - Projectile.velocity.SafeNormalize(Vector2.Zero) * 30.5f - Main.screenPosition, 30);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.Lerp(lightColor, Color.White, 0.5f), Projectile.rotation, value.Size() / 2f, Projectile.scale, SpriteEffects.None);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            Texture2D value2 = ModContent.Request<Texture2D>("CalamityMod/Particles/Sparkle").Value;
            Texture2D value3 = ModContent.Request<Texture2D>("CalamityMod/Particles/BloomCircle").Value;
            float num = value2.Height / (float)value3.Height;
            Color color = Main.hslToRgb(Main.GlobalTimeWrappedHourly * 0.6f % 1f, 1f, 0.85f);
            float num2 = Main.GlobalTimeWrappedHourly * 8f;
            Vector2 position = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.Zero) * 30.5f - Main.screenPosition;
            Main.EntitySpriteDraw(value3, position, null, color * 0.5f, 0f, value3.Size() / 2f, 4f * num, SpriteEffects.None);
            Main.EntitySpriteDraw(value2, position, null, color * 0.5f, num2 + MathF.PI / 4f, value2.Size() / 2f, 1.5f, SpriteEffects.None);
            Main.EntitySpriteDraw(value2, position, null, Color.White, num2, value2.Size() / 2f, 2f, SpriteEffects.None);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(in SoundID.DD2_WitherBeastDeath, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(1.2f, 2.3f);
                GeneralParticleHandler.SpawnParticle(new SquishyLightParticle(Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 40f, velocity, Main.rand.NextFloat(0.3f, 0.6f), Color.Cyan, 60, 1f, 1.5f, 3f, 0.02f));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Vector2 position = target.Hitbox.Size().Length() < 140f ? target.Center : Projectile.Center + Projectile.rotation.ToRotationVector2() * 60f;
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2.6f, 4f);
                GeneralParticleHandler.SpawnParticle(new SquishyLightParticle(position, velocity, Main.rand.NextFloat(0.3f, 0.6f), Color.Cyan, 60, 1f, 1.5f, 3f, 0.02f));
            }
        }
    }
}
