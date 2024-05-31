using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.RebelBladeProj
{
    internal class RebelBladeSlash : ModProjectile
    {
        public override string Texture => "CalamityMod/Projectiles/Melee/IridescentExcaliburSlash";
        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.ownerHitCheckDistance = 300f;
            Projectile.usesOwnerMeleeHitCD = true;
            Projectile.stopsDealingDamageAfterPenetrateHits = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.localAI[0] += 1f;
            Player player = Main.player[Projectile.owner];
            float num = Projectile.localAI[0] / Projectile.ai[1];
            float num2 = Projectile.ai[0];
            float num3 = Projectile.velocity.ToRotation();
            float num4 = (float)Math.PI * num2 * num + num3 + num2 * (float)Math.PI + player.fullRotation;
            Projectile.rotation = num4;
            float num5 = 1f;
            float num6 = 1.2f;

            Projectile.Center = player.RotatedRelativePoint(player.MountedCenter) - Projectile.velocity;
            Projectile.scale = num6 + num * num5;

            float colorAIScale = Projectile.localAI[0] / Projectile.ai[1];
            float amount = Utils.Remap(colorAIScale, 0f, 0.6f, 0f, 1f) * Utils.Remap(colorAIScale, 0.6f, 1f, 1f, 0f);
            Color rainbow = Color.Lerp(new Color(Main.DiscoR, Main.DiscoG, Main.DiscoB), new Color(255 - Main.DiscoR, 255 - Main.DiscoG, 255 - Main.DiscoB), amount);
            float num8 = Projectile.rotation + Main.rand.NextFloatDirection() * MathHelper.PiOver2 * 0.7f;
            Vector2 vector2 = Projectile.Center + num8.ToRotationVector2() * 84f * Projectile.scale;
            Vector2 vector3 = (num8 + Projectile.ai[0] * MathHelper.PiOver2).ToRotationVector2();
            if (Main.rand.NextFloat() * 2f < Projectile.Opacity) {
                Dust dust2 = Dust.NewDustPerfect(Projectile.Center + num8.ToRotationVector2() * (Main.rand.NextFloat() * 80f * Projectile.scale + 20f * Projectile.scale), 278, vector3 * 1f, 100, rainbow, 0.4f);
                dust2.fadeIn = 0.4f + Main.rand.NextFloat() * 0.15f;
                dust2.noGravity = true;
            }

            if (Main.rand.NextFloat() * 1.5f < Projectile.Opacity)
                Dust.NewDustPerfect(vector2, 43, vector3 * 1f, 100, rainbow * Projectile.Opacity, 1.2f * Projectile.Opacity);

            float num10 = Projectile.rotation + Main.rand.NextFloatDirection() * MathHelper.PiOver2 * 0.7f;
            Vector2 vector5 = Projectile.Center + num10.ToRotationVector2() * 84f * Projectile.scale;
            Vector2 vector6 = (num10 + Projectile.ai[0] * MathHelper.PiOver2).ToRotationVector2();
            if (Main.rand.NextFloat() < Projectile.Opacity) {
                Dust dust4 = Dust.NewDustPerfect(Projectile.Center + num10.ToRotationVector2() * (Main.rand.NextFloat() * 80f * Projectile.scale + 20f * Projectile.scale), 278, vector6 * 1f, 100, rainbow, 0.4f);
                dust4.fadeIn = 0.4f + Main.rand.NextFloat() * 0.15f;
                dust4.noGravity = true;
            }

            if (Main.rand.NextFloat() * 1.5f < Projectile.Opacity)
                Dust.NewDustPerfect(vector5, 43, vector6 * 1f, 100, rainbow * Projectile.Opacity, 1.2f * Projectile.Opacity);

            Projectile.scale *= Projectile.ai[2];
            if (Projectile.localAI[0] >= Projectile.ai[1])
                Projectile.Kill();

            for (float i = -MathHelper.PiOver4; i <= MathHelper.PiOver4; i += MathHelper.PiOver2) {
                Rectangle rect = Utils.CenteredRectangle(Projectile.Center + (Projectile.rotation + i).ToRotationVector2() * 70f * Projectile.scale, new Vector2(60f * Projectile.scale, 60f * Projectile.scale));
                Projectile.EmitEnchantmentVisualsAt(rect.TopLeft(), rect.Width, rect.Height);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 vector = Projectile.Center - Main.screenPosition;
            Texture2D asset = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle rectangle = asset.Frame(1, 4);
            Vector2 origin = rectangle.Size() / 2f;
            float scale = Projectile.scale * 1.5f;
            SpriteEffects effects = Projectile.ai[0] >= 0f ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float progress = Projectile.localAI[0] / Projectile.ai[1];
            float fade = Utils.Remap(progress, 0f, 0.6f, 0f, 1f) * Utils.Remap(progress, 0.6f, 1f, 1f, 0f);
            float lightAmount = Lighting.GetColor(Projectile.Center.ToTileCoordinates()).ToVector3().Length() / (float)Math.Sqrt(3D);
            lightAmount = Utils.Remap(lightAmount, 0.2f, 1f, 0f, 1f);
            Color baseColor = new Color(220, 0, 15);
            Color color = Color.Lerp(baseColor, Color.DarkRed, fade) * lightAmount * fade;

            void DrawSprite(Color drawColor, float rotation, float scaleMod) {
                Main.spriteBatch.Draw(asset, vector, rectangle, drawColor, Projectile.rotation + rotation, origin, scale * scaleMod, effects, 0f);
            }

            DrawSprite(color, Projectile.ai[0] * MathHelper.PiOver4 * -1f * (1f - progress), 1f);
            DrawSprite(color * 0.15f, Projectile.ai[0] * 0.01f, 1f);
            DrawSprite(color * 0.3f, 0f, 1f);
            DrawSprite(color * 0.5f, 0f, 0.975f);
            DrawSprite(baseColor * 0.6f * fade, Projectile.ai[0] * 0.01f, 1f);
            DrawSprite(baseColor * 0.5f * fade, Projectile.ai[0] * -0.05f, 0.8f);
            DrawSprite(baseColor * 0.4f * fade, Projectile.ai[0] * -0.1f, 0.6f);

            Texture2D extraTexture = TextureAssets.Extra[98].Value;
            Vector2 extraOrigin = extraTexture.Size() / 2f;
            float extraScale = scale * 0.75f;
            float lerpValue = Utils.GetLerpValue(0f, 0.5f, progress, clamped: true) * Utils.GetLerpValue(1f, 0.5f, progress, clamped: true);
            Vector2 vector3 = new Vector2((Vector2.One * extraScale).X * 0.5f, (new Vector2(0f, Utils.Remap(progress, 0f, 1f, 3f, 0f)) * extraScale).X) * lerpValue;
            Vector2 vector2 = new Vector2((Vector2.One * extraScale).Y * 0.5f, (new Vector2(0f, Utils.Remap(progress, 0f, 1f, 3f, 0f)) * extraScale).Y) * lerpValue;
            Color shineColor = color * 0.5f * lerpValue;

            void DrawShine(Vector2 position, float rotation, Vector2 scaleVector, Color drawColor) {
                Main.EntitySpriteDraw(extraTexture, position, null, drawColor, rotation, extraOrigin, scaleVector, SpriteEffects.None);
            }

            for (float i = 0f; i < 12f; i += 1f) {
                float rotation = Projectile.rotation + Projectile.ai[0] * i * -MathHelper.TwoPi * 0.025f + Utils.Remap(progress, 0f, 0.6f, 0f, 0.95504415f) * Projectile.ai[0];
                Vector2 drawPos = vector + rotation.ToRotationVector2() * (asset.Width * 0.5f - 6f) * scale;
                float lerpAmount = i / 12f;
                Color lerpColor = new Color(255, 255, 255, 0) * fade * lerpAmount * 0.5f * lerpValue;

                DrawShine(drawPos, MathHelper.PiOver2 + rotation, vector3, shineColor);
                DrawShine(drawPos, rotation, vector2, shineColor);
                DrawShine(drawPos, MathHelper.PiOver2 + rotation, vector3 * 0.6f, lerpColor);
                DrawShine(drawPos, rotation, vector2 * 0.6f, lerpColor);
            }

            Vector2 finalDrawPos = vector + (Projectile.rotation + Utils.Remap(progress, 0f, 0.6f, 0f, 0.95504415f) * Projectile.ai[0]).ToRotationVector2() * (asset.Width * 0.5f - 4f) * scale;
            Color finalColor = new Color(255, 255, 255, 0) * fade * 0.5f * lerpValue;

            DrawShine(finalDrawPos, MathHelper.PiOver2 + Projectile.rotation, vector3, shineColor);
            DrawShine(finalDrawPos, Projectile.rotation, vector2, shineColor);
            DrawShine(finalDrawPos, MathHelper.PiOver2 + Projectile.rotation, vector3 * 0.6f, finalColor);
            DrawShine(finalDrawPos, Projectile.rotation, vector2 * 0.6f, finalColor);

            return false;
        }

        public override void CutTiles() {
            Vector2 startPoint = (Projectile.rotation - MathHelper.PiOver4).ToRotationVector2() * 60f * Projectile.scale;
            Vector2 endPoint = (Projectile.rotation + MathHelper.PiOver4).ToRotationVector2() * 60f * Projectile.scale;
            float projectileSize = 60f * Projectile.scale;
            Utils.PlotTileLine(Projectile.Center + startPoint, Projectile.Center + endPoint, projectileSize, DelegateMethods.CutTiles);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.HitDirectionOverride = Main.player[Projectile.owner].Center.X < target.Center.X ? 1 : -1;

            Vector2 positionInWorld = Main.rand.NextVector2FromRectangle(target.Hitbox);
            ParticleOrchestraSettings particleOrchestraSettings = default;
            particleOrchestraSettings.PositionInWorld = positionInWorld;
            ParticleOrchestraSettings settings = particleOrchestraSettings;
            switch (Main.rand.Next(5)) {
                default:
                case 0:
                    ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, ParticleOrchestraType.NightsEdge, settings, Projectile.owner);
                    break;

                case 1:
                    ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, ParticleOrchestraType.TrueNightsEdge, settings, Projectile.owner);
                    break;

                case 2:
                    settings.MovementVector = Projectile.velocity;
                    ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, ParticleOrchestraType.TerraBlade, settings, Projectile.owner);
                    break;

                case 3:
                    ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, ParticleOrchestraType.Excalibur, settings, Projectile.owner);
                    break;

                case 4:
                    ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, ParticleOrchestraType.TrueExcalibur, settings, Projectile.owner);
                    break;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float coneLength = 94f * Projectile.scale;
            float scale = MathHelper.TwoPi / 25f * Projectile.ai[0];
            float maximumAngle = MathHelper.PiOver4;
            float coneRotation = Projectile.rotation + scale;
            if (targetHitbox.IntersectsConeSlowMoreAccurate(Projectile.Center, coneLength, coneRotation, maximumAngle))
                return true;

            float rotation = Utils.Remap(Projectile.localAI[0], Projectile.ai[1] * 0.3f, Projectile.ai[1] * 0.5f, 1f, 0f);
            if (rotation > 0f) {
                float coneRotation2 = coneRotation - MathHelper.PiOver4 * Projectile.ai[0] * rotation;
                if (targetHitbox.IntersectsConeSlowMoreAccurate(Projectile.Center, coneLength, coneRotation2, maximumAngle))
                    return true;
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
        }
    }
}
