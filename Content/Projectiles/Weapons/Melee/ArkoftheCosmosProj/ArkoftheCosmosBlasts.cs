using CalamityMod;
using CalamityMod.Particles;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.ArkoftheCosmosProj
{
    /// <summary>
    /// 冲刺弹幕
    /// </summary>
    internal class ArkoftheCosmosBlasts : ModProjectile
    {
        private bool initialized;

        private const int maxStitches = 8;

        public float[] StitchRotations = new float[8];

        public float[] StitchLifetimes = new float[8];

        private const float MaxTime = 70f;

        private const float SnapTime = 25f;

        private const float HoldTime = 15f;

        public Particle PolarStar;

        public CalamityUtils.CurveSegment anticipation = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.SineBump, 0f, 0.2f, -0.1f);

        public CalamityUtils.CurveSegment thrust = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.PolyOut, 0.3f, 0.2f, 3f, 3);

        public CalamityUtils.CurveSegment openMore = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.SineBump, 0f, 0f, -0.15f);

        public CalamityUtils.CurveSegment close = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.PolyIn, 0.35f, 0f, 1f, 4);

        public CalamityUtils.CurveSegment stayClosed = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.Linear, 0.5f, 1f, 0f);

        public new string LocalizationCategory => "Projectiles.Melee";

        public override string Texture => "CalamityMod/Projectiles/Melee/RendingScissorsRight";

        public ref float Charge => ref Projectile.ai[0];

        public bool Dashing {
            get {
                return Projectile.ai[1] == 1f;
            }
            set {
                Projectile.ai[1] = value ? 1f : 0f;
            }
        }

        public int CurrentStitches => (int)Math.Ceiling((1f - (float)Math.Sqrt(1f - (float)Math.Pow(MathHelper.Clamp(StitchProgress * 3f, 0f, 1f), 2.0))) * 8f);

        public float SnapTimer => 70f - Projectile.timeLeft;

        public float HoldTimer => 45f - Projectile.timeLeft;

        public float StitchTimer => 37.5f - Projectile.timeLeft;

        public float SnapProgress => MathHelper.Clamp(SnapTimer / 25f, 0f, 1f);

        public float HoldProgress => MathHelper.Clamp(HoldTimer / 15f, 0f, 1f);

        public float StitchProgress => MathHelper.Clamp(StitchTimer / 37.5f, 0f, 1f);

        public int CurrentAnimation {
            get {
                if (Projectile.timeLeft <= 45f) {
                    if (Projectile.timeLeft <= 30) {
                        return 2;
                    }

                    return 1;
                }

                return 0;
            }
        }

        public Vector2 scissorPosition => Projectile.Center + ThrustDisplaceRatio() * Projectile.velocity * 200f;

        public Player Owner => Main.player[Projectile.owner];

        public override void SetStaticDefaults() {
        }

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.width = Projectile.height = 300;
            Projectile.width = Projectile.height = 300;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 72;
        }

        public override bool? CanDamage() {
            return HoldProgress > 0f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (HoldProgress == 0f) {
                return false;
            }

            float collisionPoint = 0f;
            float num = ThrustDisplaceRatio() * 242f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + Projectile.velocity * num, 30f, ref collisionPoint);
        }

        public override bool ShouldUpdatePosition() {
            return false;
        }

        public override void AI() {
            if (!initialized) {
                Projectile.timeLeft = 70;
                SoundStyle style = SoundID.Item84;
                style.Volume = SoundID.Item84.Volume * 0.3f;
                SoundEngine.PlaySound(in style, Projectile.Center);
                Projectile.velocity.Normalize();
                Projectile.rotation = Projectile.velocity.ToRotation();
                initialized = true;
                Projectile.netUpdate = true;
                Projectile.netSpam = 0;
            }

            Projectile.scale = 1.4f;
            HandleParticles();
            if (HoldTimer == 1f) {
                if (Owner.controlUp && Charge >= 5f) {
                    Owner.GiveIFrames(ArkoftheCosmos.DashIframes);
                    Dashing = true;
                }

                for (int i = 0; i < 20; i++) {
                    float num = MathHelper.Lerp(0f, ThrustDisplaceRatio() * 242f, Main.rand.NextFloat(0f, 1f));
                    Vector2 position = Projectile.Center + Projectile.velocity * num;
                    Color color = Main.rand.NextBool() ? Color.OrangeRed : Main.rand.NextBool() ? Color.White : Color.Orange;
                    float num2 = Main.rand.NextFloat(0.05f, 0.4f) * (0.4f + 0.6f * (float)Math.Sin(num / (ThrustDisplaceRatio() * 242f) * MathF.PI));
                    switch (Main.rand.Next(3)) {
                        case 0:
                            GeneralParticleHandler.SpawnParticle(new StrongBloom(position, Vector2.UnitY * Main.rand.NextFloat(-4f, -1f), color, num2, Main.rand.Next(20) + 10));
                            break;
                        case 1:
                            GeneralParticleHandler.SpawnParticle(new GenericBloom(position, Vector2.UnitY * Main.rand.NextFloat(-4f, -1f), color, num2, Main.rand.Next(20) + 10));
                            break;
                        case 2:
                            GeneralParticleHandler.SpawnParticle(new CritSpark(position, Vector2.UnitY * Main.rand.NextFloat(-10f, -1f), Color.White, color, num2 * 7f, Main.rand.Next(20) + 10, 0.1f, 3f));
                            break;
                    }
                }
            }

            Owner.Calamity().LungingDown = false;
            if (!Dashing) {
                return;
            }

            Owner.Calamity().LungingDown = true;
            Owner.fallStart = (int)(Owner.position.Y / 16f);
            Owner.velocity = Owner.SafeDirectionTo(scissorPosition, Vector2.Zero) * 60f;
            if (Owner.Distance(scissorPosition) > 60f) {
                return;
            }

            Dashing = false;
            Owner.velocity *= 0.1f;
            SoundEngine.PlaySound(in CommonCalamitySounds.MeatySlashSound, Projectile.Center);
            if (Owner.whoAmI == Main.myPlayer) {
                for (int j = 0; j < 5; j++) {
                    Projectile.NewProjectileDirect(Projectile.GetSource_FromThis(), Owner.Center, Main.rand.NextVector2CircularEdge(28f, 28f), ModContent.ProjectileType<EonBolt>(), (int)(ArkoftheCosmos.SlashBoltsDamageMultiplier * Projectile.damage), 0f, Owner.whoAmI, 0.55f, 0.219911486f).timeLeft = 100;
                }
            }
        }

        public void HandleParticles() {
            if (PolarStar == null) {
                PolarStar = new GenericSparkle(Projectile.Center, Vector2.Zero, Color.White, Color.CornflowerBlue, Projectile.scale * 2f, 2, 0.1f, 5f, needed: true);
                GeneralParticleHandler.SpawnParticle(PolarStar);
            }
            else if (HoldProgress <= 0.4f) {
                PolarStar.Time = 0;
                PolarStar.Position = scissorPosition;
                PolarStar.Scale = Projectile.scale * 2f;
            }

            for (int i = 0; i < CurrentStitches; i++) {
                if (StitchRotations[i] == 0f) {
                    StitchRotations[i] = Main.rand.NextFloat(-MathF.PI / 4f, MathF.PI / 4f) + MathF.PI / 2f;
                    SoundStyle soundStyle = i % 3 == 0 ? SoundID.Item63 : i % 3 == 1 ? SoundID.Item64 : SoundID.Item65;
                    SoundStyle style = soundStyle;
                    style.Volume = soundStyle.Volume * 0.5f;
                    SoundEngine.PlaySound(in style, Owner.Center);
                    float num = ThrustDisplaceRatio() * 242f / 8f * 0.5f + MathHelper.Lerp(0f, ThrustDisplaceRatio() * 242f, i / 8f);
                    GeneralParticleHandler.SpawnParticle(new CritSpark(Projectile.Center + Projectile.velocity * num, Vector2.Zero, Color.White, Color.Cyan, 3f, 8, 0.1f, 3f));
                }

                StitchLifetimes[i] += 1f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ArkoftheCosmos arkoftheCosmos = Owner.HeldItem.ModItem as ArkoftheCosmos;
            if (arkoftheCosmos != null) {
                arkoftheCosmos.Charge += 1;
                if (arkoftheCosmos.Charge > 10)
                    arkoftheCosmos.Charge = 10;
            }
            Color color = !Main.rand.NextBool() ? Main.rand.NextBool() ? Color.OrangeRed : Color.Gold : Main.rand.NextBool() ? Color.Orange : Color.Coral;
            GeneralParticleHandler.SpawnParticle(new PulseRing(target.Center, Vector2.Zero, color, 0.05f, 0.2f + Main.rand.NextFloat(0f, 1f), 30));
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Projectile.velocity.RotatedByRandom(0.62831854820251465) * Main.rand.NextFloat(2.6f, 4f);
                GeneralParticleHandler.SpawnParticle(new SquishyLightParticle(target.Center, velocity, Main.rand.NextFloat(0.3f, 0.6f), Color.Red, 60, 1f, 1.5f, 3f, 0.002f));
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            modifiers.SourceDamage *= (float)Math.Pow(1f - ArkoftheCosmos.blastFalloffStrenght, Projectile.numHits * ArkoftheCosmos.blastFalloffSpeed);
        }

        public override void OnKill(int timeLeft) {
            if (Dashing) {
                Owner.velocity *= 0.1f;
            }

            Owner.Calamity().LungingDown = false;
        }

        internal float ThrustDisplaceRatio() {
            return CalamityUtils.PiecewiseAnimation(SnapProgress, anticipation, thrust);
        }

        internal float RotationRatio() {
            return CalamityUtils.PiecewiseAnimation(SnapProgress, openMore, close, stayClosed);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            Texture2D value = ModContent.Request<Texture2D>("CalamityMod/Particles/BloomLine").Value;
            Color color = Color.Lerp(Color.OrangeRed, Color.White, SnapProgress);
            float rotation = Projectile.rotation + MathF.PI / 2f;
            Main.EntitySpriteDraw(scale: new Vector2(0.2f * (1f - SnapProgress), ThrustDisplaceRatio() * 240f), texture: value, position: Projectile.Center - Main.screenPosition, sourceRectangle: null, color: color, rotation: rotation, origin: new Vector2(value.Width / 2f, value.Height), effects: SpriteEffects.None);
            if (HoldProgress <= 0.4f) {
                Texture2D value2 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsLeft").Value;
                Texture2D value3 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsRight").Value;
                float num = Projectile.rotation + MathF.PI / 4f;
                float rotation2 = MathHelper.Lerp(num - MathF.PI / 4f, num, RotationRatio());
                float rotation3 = MathHelper.Lerp(num + MathF.PI / 4f, num, RotationRatio());
                Vector2 origin = new Vector2(33f, 86f);
                Vector2 origin2 = new Vector2(44f, 86f);
                Vector2 position = scissorPosition - Main.screenPosition;
                float num2 = (0.4f - HoldProgress) / 0.4f;
                Color color2 = Color.Tomato * num2 * 0.9f;
                Color color3 = Color.DeepSkyBlue * num2 * 0.9f;
                Main.EntitySpriteDraw(value3, position, null, color3, rotation3, origin2, Projectile.scale, SpriteEffects.None);
                Main.EntitySpriteDraw(value2, position, null, color2 * num2, rotation2, origin, Projectile.scale, SpriteEffects.None);
            }

            if (HoldProgress > 0f) {
                Texture2D value4 = ModContent.Request<Texture2D>("CalamityMod/Particles/ThinEndedLine").Value;
                Vector2 vector = HoldProgress > 0.2f ? Vector2.Zero : Vector2.One.RotatedByRandom(6.2831854820251465) * (1f - HoldProgress * 5f) * 0.5f;
                float amount = (float)Math.Sin(HoldProgress * (MathF.PI / 2f));
                Vector2 origin3 = new Vector2(value4.Width / 2f, value4.Height);
                float x = StitchProgress < 0.75f ? 0.2f : (1f - (StitchProgress - 0.75f) * 4f) * 0.2f;
                Vector2 scale2 = new Vector2(x, ThrustDisplaceRatio() * 242f / value4.Height);
                float num3 = StitchProgress < 0.75f ? 1f : 1f - (StitchProgress - 0.75f) * 4f;
                Main.EntitySpriteDraw(value4, Projectile.Center - Main.screenPosition + vector, null, Color.Lerp(Color.White, Color.OrangeRed * 0.7f, amount) * num3, rotation, origin3, scale2, SpriteEffects.None);
                if (StitchProgress > 0f) {
                    for (int i = 0; i < CurrentStitches; i++) {
                        float num4 = ThrustDisplaceRatio() * 242f / 8f * 0.5f + MathHelper.Lerp(0f, ThrustDisplaceRatio() * 242f, i / 8f);
                        Vector2 vector2 = Projectile.Center + Projectile.velocity * num4;
                        rotation = Projectile.rotation + MathF.PI / 2f + StitchRotations[i];
                        origin3 = new Vector2(value4.Width / 2f, value4.Height / 2f);
                        float y = (float)Math.Sin(i / 7f * MathF.PI) * 0.5f + 0.5f;
                        float num5 = (1f + (float)Math.Sin(MathHelper.Clamp(StitchLifetimes[i] / 7f, 0f, 1f) * MathF.PI) * 0.3f) * 0.85f;
                        if (CurrentStitches == 8) {
                            num5 *= 1f - (StitchTimer - 11.25f) / 37.5f * 0.7f * 0.8f;
                        }

                        scale2 = new Vector2(0.2f, y) * num5;
                        Color color4 = Color.Lerp(Color.White, Color.CornflowerBlue * 0.7f, (float)Math.Sin(MathHelper.Clamp(StitchLifetimes[i] / 7f, 0f, 1f) * (MathF.PI / 2f)));
                        Main.EntitySpriteDraw(value4, vector2 - Main.screenPosition + vector, null, color4, rotation, origin3, scale2, SpriteEffects.None);
                    }
                }
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(initialized);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            initialized = reader.ReadBoolean();
        }
    }
}
