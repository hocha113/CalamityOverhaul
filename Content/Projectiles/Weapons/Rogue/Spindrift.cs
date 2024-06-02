using Microsoft.Xna.Framework;
using System;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Particles;
using CalamityMod;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;
using Terraria.Audio;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue
{
    internal class Spindrift : ModProjectile
    {
        public override string Texture => "CalamityMod/Projectiles/Melee/MendedBiomeBlade_GestureForTheDrownedWave";
        public Player Owner => Main.player[Projectile.owner];
        public ref float HasLanded => ref Projectile.ai[0]; 
        public ref float TimeSinceLanding => ref Projectile.ai[1];
        public float Timer => MaxTime - Projectile.timeLeft;
        public const float MaxTime = 160f;
        public float groundOffset;
        public float scaleX => 1 + 0.2f * (1f + -(float)Math.Sin((TimeSinceLanding - 10f) * 0.1f)) * 0.5f;
        public float scaleY => (1 + 0.4f * (1f + (float)Math.Sin((TimeSinceLanding - 10f) * 0.1f)) * 0.5f) * MathHelper.Clamp((TimeSinceLanding - 10) / 30f, 0f, 1f) * MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            Main.projFrames[Projectile.type] = 3;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = (int)MaxTime;
            Projectile.DamageType = ModContent.GetInstance<RogueDamageClass>();
            Projectile.alpha = 90;
            Projectile.ignoreWater = true;
            Projectile.hide = true;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float collisionPoint = 0;
            float halfLength = 66f * Projectile.scale;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , Projectile.Center + Utils.SafeNormalize(Projectile.velocity.RotatedBy(MathHelper.PiOver2)
                , Vector2.Zero) * halfLength, Projectile.Center - Utils.SafeNormalize(Projectile.velocity.RotatedBy(MathHelper.PiOver2), Vector2.Zero) * halfLength, 40f, ref collisionPoint);
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindNPCsAndTiles.Add(index);
        }

        public override void AI() {
            TimeSinceLanding++;

            groundOffset = -1;
            //Check 7 tiles under the projectile for solid ground
            for (float i = 0; i < 7; i += 0.5f) {
                Vector2 positionToCheck = Projectile.position;
                if (Main.tile[(int)(positionToCheck.X / 16), (int)((positionToCheck.Y / 16) + 1 * i)].IsTileSolidGround() && groundOffset == -1) {
                    groundOffset = i * 16;
                    break;
                }
            }

            Projectile.rotation += Projectile.velocity.X;


            if (Projectile.velocity.Y < 15f) {
                Projectile.velocity.Y += 0.3f;
            }

            if (Math.Abs(Projectile.velocity.X) < 8) {
                Projectile.velocity.X += 0.3f * ((Projectile.velocity.X > 0) ? 1f : -1f);
            }

            Projectile.frameCounter++;
            if (Projectile.frameCounter % 6 == 0)
                Projectile.frame = (Projectile.frame + 1) % 3;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Splash with { PitchVariance = 2f }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D wave = TextureAssets.Projectile[Type].Value;
            Rectangle frame = new Rectangle(0, 0 + 110 * Projectile.frame, 40, 110);

            float drawRotation = 0f;
            Vector2 drawOrigin = new Vector2(0f, frame.Height);


            Vector2 drawOffset;

            Vector2 Scale;

            SpriteEffects flip = Projectile.velocity.X > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            for (int i = 2; i >= 0; i--) {
                drawOffset = Projectile.position + Vector2.UnitY * groundOffset + Vector2.UnitY / 2f - Main.screenPosition;
                drawOffset -= Vector2.UnitX * 10f * i * Math.Sign(Projectile.velocity.X);
                float ScaleYAlter = (1 + 0.4f * (1f + (float)Math.Sin((TimeSinceLanding) * 0.1f + 0.2 * i)) * 0.5f) * MathHelper.Clamp((TimeSinceLanding) / 30f, 0f, 1f) * MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);
                Scale = new Vector2(scaleX, ScaleYAlter - 0.05f * i);
                float slightfade = 1f - 0.24f * i;
                Color darkenedColor = Color.Lerp(lightColor, Color.Black, i * 0.15f);

                Main.EntitySpriteDraw(wave, drawOffset, frame, darkenedColor * MathHelper.Clamp(TimeSinceLanding / 30f, 0f, 1f) * slightfade, drawRotation, drawOrigin, Scale * Projectile.scale, flip, 0);
            }

            return false;
        }
    }
}
