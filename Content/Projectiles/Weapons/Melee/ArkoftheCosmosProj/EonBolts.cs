using CalamityMod;
using CalamityMod.Particles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.ArkoftheCosmosProj
{
    internal class EonBolts : ModProjectile
    {
        internal PrimitiveTrail TrailDrawer;

        public NPC target;

        private Particle Head;

        public new string LocalizationCategory => "Projectiles.Melee";

        public override string Texture => "CalamityMod/Projectiles/Melee/GalaxiaBolt";

        public Player Owner => Main.player[Projectile.owner];

        public ref float Hue => ref Projectile.ai[0];

        public ref float HomingStrenght => ref Projectile.ai[1];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 25;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 80;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Projectile.ai[2]++;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathF.PI / 2f;
            if (Head == null) {
                Head = new GenericSparkle(Projectile.Center, Vector2.Zero, Color.White, Main.hslToRgb(Hue, 100f, 50f), 1.2f, 2, 0.06f, 3f, needed: true);
                GeneralParticleHandler.SpawnParticle(Head);
            }
            else {
                Head.Position = Projectile.Center + Projectile.velocity * 0.5f;
                Head.Time = 0;
                Head.Scale += (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.02f * Projectile.scale;
            }

            if (Projectile.ai[2] > 30) {
                if (target == null) {
                    target = Projectile.Center.ClosestNPCAt(812f);
                }
                else if (Projectile.velocity.AngleBetween(target.Center - Projectile.Center) < MathF.PI) {
                    float targetAngle = Projectile.AngleTo(target.Center);
                    float f = Projectile.velocity.ToRotation().AngleTowards(targetAngle, HomingStrenght);
                    Projectile.velocity = f.ToRotationVector2() * Projectile.velocity.Length() * 1.01f;
                }
            }

            Lighting.AddLight(Projectile.Center, 0.75f, 1f, 0.24f);
            if (Main.rand.NextBool(2)) {
                GeneralParticleHandler.SpawnParticle(new HeavySmokeParticle(Projectile.Center, Projectile.velocity * 0.5f, Color.Lerp(Color.DodgerBlue, Color.MediumVioletRed, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f)), 20, Main.rand.NextFloat(0.6f, 1.2f) * Projectile.scale, 0.28f, 0f, glowing: false, 0f, required: true));
                if (Main.rand.NextBool(3)) {
                    GeneralParticleHandler.SpawnParticle(new HeavySmokeParticle(Projectile.Center, Projectile.velocity * 0.5f, Main.hslToRgb(Hue, 1f, 0.7f), 15, Main.rand.NextFloat(0.4f, 0.7f) * Projectile.scale, 0.8f, 0f, glowing: true, 0.05f, required: true));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            //if (Projectile.IsOwnedByLocalPlayer())
            //    Projectile.NewProjectileDirect(
            //        AiBehavior.GetEntitySource_Parent(Projectile),
            //        Projectile.Center,
            //        Vector2.Zero,
            //        ModContent.ProjectileType<SlaughterExplosion>(),
            //        Projectile.damage / 2,
            //        0,
            //        Projectile.owner
            //        );
        }

        internal Color ColorFunction(float completionRatio) {
            float amount = MathHelper.Lerp(0.65f, 1f, (float)Math.Cos((0f - Main.GlobalTimeWrappedHourly) * 3f) * 0.5f + 0.5f);
            float num = Utils.GetLerpValue(1f, 0.64f, completionRatio, clamped: true) * Projectile.Opacity;
            Color value = Color.Lerp(Main.hslToRgb(Hue, 1f, 0.8f), Color.PaleTurquoise, (float)Math.Sin(completionRatio * MathF.PI * 1.6f - Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f);
            return Color.Lerp(Color.White, value, amount) * num;
        }

        internal float WidthFunction(float completionRatio) {
            float amount = (float)Math.Pow(1f - completionRatio, 3.0);
            return MathHelper.Lerp(0f, 22f * Projectile.scale * Projectile.Opacity, amount);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (TrailDrawer == null) {
                TrailDrawer = new PrimitiveTrail(WidthFunction, ColorFunction, null, GameShaders.Misc["CalamityMod:TrailStreak"]);
            }

            GameShaders.Misc["CalamityMod:TrailStreak"].SetShaderTexture(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/Trails/ScarletDevilStreak"));
            TrailDrawer.Draw(Projectile.oldPos, Projectile.Size * 0.5f - Main.screenPosition, 30);
            Texture2D value = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/GalaxiaBolt").Value;
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.Lerp(lightColor, Color.White, 0.5f), Projectile.rotation, value.Size() / 2f, Projectile.scale, SpriteEffects.None);
            return false;
        }
    }
}
