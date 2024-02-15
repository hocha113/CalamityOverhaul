using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using static Microsoft.Xna.Framework.MathHelper;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class EssenceFlamethrower : ModProjectile//这部分代码来自于炼狱模式，但我不准备应用这种弹幕，它的视觉效果并不理想
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "FireCrossburst";
        public Player Owner => Main.player[(int)Projectile.ai[1]];
        public Vector2 toMou = Vector2.Zero;
        public ref float Time => ref Projectile.ai[0];
        public const float FlameRotation = Pi / 25f;
        public const float FadeinTime = 30f;
        public const float FadeoutTime = 45f;
        public const float Lifetime = FadeinTime + FadeoutTime;
        public const float FireMaxLength = 1950f;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.alpha = 255;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Projectile.Center = Owner.Center + Projectile.rotation.ToRotationVector2() * 85f;
            Projectile.rotation = toMou.ToRotation();

            Vector2 flameDirection = Projectile.rotation.ToRotationVector2();

            if (Projectile.IsOwnedByLocalPlayer()) {
                toMou = Owner.Center.To(Main.MouseWorld);
            }

            DelegateMethods.v3_1 = new Vector3(1.2f, 0f, 0.9f);
            float fadeIn = Time / FadeinTime;
            if (fadeIn > 1f)
                fadeIn = 1f;

            float fadeOut = (Time - FadeoutTime) / FadeinTime;
            if (fadeOut < 0f)
                fadeOut = 0f;

            Utils.PlotTileLine(Projectile.Center + flameDirection * FireMaxLength * fadeOut
                , Projectile.Center + flameDirection * FireMaxLength * fadeIn, 16f, DelegateMethods.CastLight);
            Utils.PlotTileLine(Projectile.Center + flameDirection.RotatedBy(FlameRotation) * FireMaxLength * fadeOut
                , Projectile.Center + flameDirection.RotatedBy(FlameRotation) * FireMaxLength * fadeIn, 16f, DelegateMethods.CastLight);
            Utils.PlotTileLine(Projectile.Center + flameDirection.RotatedBy(-FlameRotation) * FireMaxLength * fadeOut
                , Projectile.Center + flameDirection.RotatedBy(-FlameRotation) * FireMaxLength * fadeIn, 16f, DelegateMethods.CastLight);

            if (fadeOut == 0f && fadeIn > 0.1f) {
                for (int i = 0; i < 3; i++) {
                    Dust fire = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.TerraBlade, 0f, 0f, 0, default, 1f);
                    fire.fadeIn = 1.5f;
                    fire.velocity = flameDirection.RotatedBy(Main.rand.NextFloatDirection() * FlameRotation * 2f) * Main.rand.NextFloat(0.5f, 3f) * FireMaxLength / 27f;
                    fire.velocity += Owner.velocity * 2f;
                    fire.noLight = true;
                    fire.noGravity = true;
                    fire.alpha = 200;
                }
            }
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(5) && Time >= 15f) {
                Vector2 smokeSpawnPosition = Projectile.Center + flameDirection * FireMaxLength * 0.75f + Main.rand.NextVector2Square(-20f, 20f);
                Gore smoke = Gore.NewGoreDirect(Projectile.GetSource_FromAI(), smokeSpawnPosition, Vector2.Zero, Main.rand.Next(61, 64), 0.5f);
                smoke.velocity *= 0.3f;
                smoke.velocity += flameDirection * 4f;
            }

            Dust smokeDust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, 0f, 0f, 0, default, 1f);
            smokeDust.fadeIn = 1.5f;
            smokeDust.scale = 0.4f;
            smokeDust.velocity = flameDirection.RotatedBy(Main.rand.NextFloatDirection() * Pi / 8f) * (0.5f + Main.rand.NextFloat() * 2.5f) * 15f;
            smokeDust.velocity += Owner.velocity * 2f;
            smokeDust.velocity *= 0.3f;
            smokeDust.noLight = true;
            smokeDust.noGravity = true;

            float smokeOffsetInterpolant = Main.rand.NextFloat();
            smokeDust.position = Vector2.Lerp(Projectile.Center + flameDirection * FireMaxLength * fadeOut, Projectile.Center + flameDirection * FireMaxLength * fadeIn, smokeOffsetInterpolant);
            smokeDust.position += flameDirection.RotatedBy(PiOver2) * (20f + 100f * (smokeOffsetInterpolant - 0.5f));

            Time++;
            Projectile.frameCounter++;

            if (Time >= Lifetime)
                Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 startOfFlame = Projectile.Center - Main.screenPosition;
            float relativeFrameCompletion = Projectile.frameCounter / 40f;
            Texture2D texture2D5 = TextureAssets.Projectile[Projectile.type].Value;
            Color flameDrawColor;
            Color startingFlameColor = new(255, 255, 255, 0);
            Color midFlameColor = new(167, 232, 30, 40);
            Color endFlameColor = new(0, 0, 0, 30);

            ulong flameDrawerSeed = (ulong)(Projectile.identity + 958);

            int flameCount = (int)(FireMaxLength / 6f);
            for (float i = 0f; i < flameCount; i++) {
                float flameOffsetDirectionAngle = Lerp(-0.05f, 0.05f, Utils.RandomFloat(ref flameDrawerSeed));
                Vector2 flameDirection = (Projectile.rotation + flameOffsetDirectionAngle).ToRotationVector2();
                Vector2 endOfFlame = startOfFlame + flameDirection * FireMaxLength;
                float flameDrawInterpolant = relativeFrameCompletion + i / flameCount;
                float flameRotation = Projectile.rotation + Pi * (flameDrawInterpolant + Main.GlobalTimeWrappedHourly * 1.2f) * 0.1f + (int)(flameDrawInterpolant * flameCount) * Pi * 0.4f;
                flameDrawInterpolant %= 1f;

                if ((flameDrawInterpolant <= relativeFrameCompletion % 1f || Projectile.frameCounter >= 40f) &&
                    (flameDrawInterpolant >= relativeFrameCompletion % 1f || Projectile.frameCounter < 40f)) {
                    if (flameDrawInterpolant < 0.1f)
                        flameDrawColor = Color.Lerp(Color.Transparent, startingFlameColor, Utils.GetLerpValue(0f, 0.1f, flameDrawInterpolant, true));

                    else if (flameDrawInterpolant < 0.35f)
                        flameDrawColor = startingFlameColor;

                    else if (flameDrawInterpolant < 0.7f)
                        flameDrawColor = Color.Lerp(startingFlameColor, midFlameColor, Utils.GetLerpValue(0.35f, 0.7f, flameDrawInterpolant, true));

                    else if (flameDrawInterpolant < 0.9f)
                        flameDrawColor = Color.Lerp(midFlameColor, endFlameColor, Utils.GetLerpValue(0.7f, 0.9f, flameDrawInterpolant, true));

                    else if (flameDrawInterpolant < 1f)
                        flameDrawColor = Color.Lerp(endFlameColor, Color.Transparent, Utils.GetLerpValue(0.9f, 1f, flameDrawInterpolant, true));

                    else
                        flameDrawColor = Color.Transparent;

                    float flameScale = MathF.Pow(Lerp(0.9f, 1.7f, flameDrawInterpolant), 2f) * 0.8f;

                    Vector2 currentFlameDrawPosition = Vector2.SmoothStep(startOfFlame, endOfFlame, flameDrawInterpolant);
                    Rectangle frame = texture2D5.Frame(1, 7, 0, (int)(flameDrawInterpolant * 7f));
                    Main.spriteBatch.Draw(texture2D5, currentFlameDrawPosition, frame, flameDrawColor, flameRotation, frame.Size() / 2f, flameScale, SpriteEffects.None, 0f);
                }
            }
            return false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Time < 32f)
                return false;

            float completelyUselessFuckYouLmao = 0f;
            float fadeIn = Projectile.ai[0] / 25f;
            if (fadeIn > 1f)
                fadeIn = 1f;

            float fadeOut = (Projectile.ai[0] - FadeoutTime) / FadeinTime;
            if (fadeOut < 0f)
                fadeOut = 0f;

            Vector2 lineStart = Projectile.Center + Projectile.rotation.ToRotationVector2() * FireMaxLength * fadeOut;
            Vector2 lineEnd = Projectile.Center + Projectile.rotation.ToRotationVector2() * FireMaxLength * fadeIn;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), lineStart, lineEnd, Projectile.scale * 66f, ref completelyUselessFuckYouLmao);
        }
    }
}
