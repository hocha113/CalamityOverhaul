using CalamityMod;
using CalamityMod.Graphics.Primitives;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.StormGoddessSpearProj
{
    internal class StormLightning : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "StormLightning";
        public Color Light => Lighting.GetColor((int)(Projectile.position.X + (Projectile.width * 0.5)) / 16, (int)((Projectile.position.Y + (Projectile.height * 0.5)) / 16.0));
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 30;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.alpha = 255;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 4;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120 * (Projectile.extraUpdates + 1);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                projHitbox.X = (int)Projectile.oldPos[i].X;
                projHitbox.Y = (int)Projectile.oldPos[i].Y;
                if (projHitbox.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return base.Colliding(projHitbox, targetHitbox);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = new Vector2(0, Math.Sign(Projectile.velocity.Y));
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity * 15
                , ModContent.ProjectileType<StormArc>(), Projectile.damage / 2, Projectile.knockBack, Projectile.owner);
            }
        }

        public override void AI() {
            Projectile.velocity.X += -0.1f * Math.Sign(Projectile.velocity.X);
            if (Math.Abs(Projectile.velocity.X) < 0.1f) {
                if (Math.Abs(Projectile.velocity.Y) < 1) {
                    Projectile.velocity.Y *= 1.01f;
                }
            }
            Lighting.AddLight(Projectile.Center, Color.Magenta.R / 255, Color.Magenta.G / 255, Color.Magenta.B / 255);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item94, Projectile.position);
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < Main.rand.Next(13, 26); i++) {
                    Vector2 pos = Projectile.Center;
                    Vector2 particleSpeed = Main.rand.NextVector2Unit() * Main.rand.NextFloat(15.5f, 37.7f);
                    BaseParticle energyLeak = new PRK_Light(pos, particleSpeed
                        , 0.3f, Light, 6 + Main.rand.Next(5), 1, 1.5f, hueShift: 0.0f);
                    DRKLoader.AddParticle(energyLeak);
                }
            }
            if (Projectile.numHits == 0 && Projectile.IsOwnedByLocalPlayer()) {
                int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Main.rand.NextVector2Unit() * 15
                , ModContent.ProjectileType<StormArc>(), Projectile.damage / 3, Projectile.knockBack, Projectile.owner);
                Main.projectile[proj].timeLeft = 15;
            }
        }

        public float PrimitiveWidthFunction(float completionRatio) => CalamityUtils.Convert01To010(completionRatio) * Projectile.scale * Projectile.width * 0.6f;

        public Color PrimitiveColorFunction(float completionRatio) {
            float colorInterpolant = (float)Math.Sin(Projectile.identity / 3f + completionRatio * 20f + Main.GlobalTimeWrappedHourly * 1.1f) * 0.5f + 0.5f;
            Color color = CalamityUtils.MulticolorLerp(colorInterpolant, Light);
            return color;
        }

        public override bool PreDraw(ref Color lightColor) {
            GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"].UseImage1("Images/Misc/Perlin");
            GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"].Apply();

            PrimitiveRenderer.RenderTrail(Projectile.oldPos, new PrimitiveSettings(PrimitiveWidthFunction, PrimitiveColorFunction
                , (float _) => Projectile.Size * 0.5f, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"]), 80);
            return false;
        }
    }
}
