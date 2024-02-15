using CalamityMod;
using CalamityMod.Particles;
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
    internal class ArkoftheAncientsParryHoldouts : ModProjectile
    {
        private bool initialized;

        private const float MaxTime = 340f;

        private static float ParryTime = 15f;

        public CalamityUtils.CurveSegment anticipation = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.SineBump, 0f, 0.2f, -0.05f);

        public CalamityUtils.CurveSegment thrust = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.PolyInOut, 0.2f, 0.2f, 0.8f, 2);

        public CalamityUtils.CurveSegment retract = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.CircIn, 0.7f, 1f, -0.1f);

        public CalamityUtils.CurveSegment openMore = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.SineBump, 0f, 0f, -0.15f);

        public CalamityUtils.CurveSegment close = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.PolyIn, 0.3f, 0f, 1f, 4);

        public CalamityUtils.CurveSegment stayClosed = new CalamityUtils.CurveSegment(CalamityUtils.EasingType.Linear, 0.5f, 1f, 0f);

        public new string LocalizationCategory => "Projectiles.Melee";

        public override string Texture => "CalamityMod/Projectiles/Melee/RendingScissorsRight";

        public Vector2 DistanceFromPlayer => Projectile.velocity * 10f + Projectile.velocity * 10f * ThrustDisplaceRatio();

        public float Timer => 340f - Projectile.timeLeft;

        public float ParryProgress => (340f - Projectile.timeLeft) / ParryTime;

        public ref float AlreadyParried => ref Projectile.ai[1];

        public Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.width = Projectile.height = 75;
            Projectile.width = Projectile.height = 75;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.noEnchantmentVisuals = true;
        }

        public override bool? CanDamage() {
            return Timer <= ParryTime && AlreadyParried == 0f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float collisionPoint = 0f;
            float num = 142f * Projectile.scale;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Owner.Center + DistanceFromPlayer, Owner.Center + DistanceFromPlayer + Projectile.velocity * num, 44f, ref collisionPoint);
        }

        public void GeneralParryEffects() {
            ArkoftheCosmos arkoftheCosmos = Owner.HeldItem.ModItem as ArkoftheCosmos;
            if (arkoftheCosmos != null) {
                arkoftheCosmos.Charge = 10f;
                arkoftheCosmos.Combo = 0f;
            }

            SoundEngine.PlaySound(in SoundID.DD2_WitherBeastCrystalImpact);
            SoundStyle style = CommonCalamitySounds.ScissorGuillotineSnapSound;
            style.Volume = CommonCalamitySounds.ScissorGuillotineSnapSound.Volume * 1.3f;
            SoundEngine.PlaySound(in style, Projectile.Center);
            CombatText.NewText(Projectile.Hitbox, new Color(111, 247, 200), CalamityUtils.GetTextValue("Misc.ArkParry"), dramatic: true);
            for (int i = 0; i < 5; i++) {
                Vector2 vector = Main.rand.NextVector2Circular(Owner.Hitbox.Width * 2f, Owner.Hitbox.Height * 1.2f);
                float num = Main.rand.NextFloat(0.5f, 1.4f);
                GeneralParticleHandler.SpawnParticle(new FlareShine(Owner.Center + vector, vector * 0.01f, Color.White, Color.Red, 0f, new Vector2(0.6f, 1f) * num, new Vector2(1.5f, 2.7f) * num, 20 + Main.rand.Next(6), 0f, 3f, 0f, Main.rand.Next(7) * 2));
            }

            AlreadyParried = 1f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!(AlreadyParried > 0f)) {
                GeneralParryEffects();
                if (target.damage > 0) {
                    Owner.GiveIFrames(35);
                }

                Vector2 position = target.Hitbox.Size().Length() < 140f ? target.Center : Projectile.Center + Projectile.rotation.ToRotationVector2() * 60f;
                GeneralParticleHandler.SpawnParticle(new GenericSparkle(position, Vector2.Zero, Color.White, Color.HotPink, 1.2f, 35, 0.1f, 2f));
                for (int i = 0; i < 10; i++) {
                    Vector2 velocity = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2.6f, 4f);
                    GeneralParticleHandler.SpawnParticle(new SquishyLightParticle(position, velocity, Main.rand.NextFloat(0.3f, 0.6f), Color.Cyan, 60, 1f, 1.5f, 3f, 0.02f));
                }
            }
        }

        public override void AI() {
            if (!initialized) {
                Projectile.timeLeft = 340;
                SoundStyle style = SoundID.Item84;
                style.Volume = SoundID.Item84.Volume * 0.3f;
                SoundEngine.PlaySound(in style, Projectile.Center);
                Projectile.velocity = Owner.SafeDirectionTo(Owner.Calamity().mouseWorld, Vector2.Zero);
                Projectile.velocity.Normalize();
                Projectile.rotation = Projectile.velocity.ToRotation();
                initialized = true;
                Projectile.netUpdate = true;
                Projectile.netSpam = 0;
            }

            Projectile.Center = Owner.Center + DistanceFromPlayer;
            Projectile.scale = 1.4f + ThrustDisplaceRatio() * 0.2f;
            if (Timer > ParryTime) {
                return;
            }

            float collisionPoint = 0f;
            float num = 142f * Projectile.scale;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active || !projectile.hostile || projectile.damage <= 1 || !(projectile.velocity.Length() * (projectile.extraUpdates + 1) > 1f) || !(projectile.Size.Length() < 300f) || !Collision.CheckAABBvLineCollision(projectile.Hitbox.TopLeft(), projectile.Hitbox.Size(), Owner.Center + DistanceFromPlayer, Owner.Center + DistanceFromPlayer + Projectile.velocity * num, 24f, ref collisionPoint)) {
                    continue;
                }

                if (AlreadyParried == 0f) {
                    GeneralParryEffects();
                    if (Owner.velocity.Y != 0f) {
                        Owner.velocity += (Owner.Center - projectile.Center).SafeNormalize(Vector2.Zero) * 2f;
                    }
                }

                if (projectile.Calamity().flatDR < 160) {
                    projectile.Calamity().flatDR = 160;
                }

                if (projectile.Calamity().flatDRTimer < 60) {
                    projectile.Calamity().flatDRTimer = 60;
                }

                break;
            }

            Owner.heldProj = Projectile.whoAmI;
            Owner.direction = Math.Sign(Projectile.velocity.X);
            Owner.itemRotation = Projectile.rotation;
            if (Owner.direction != 1) {
                Owner.itemRotation -= MathF.PI;
            }

            Owner.itemRotation = MathHelper.WrapAngle(Owner.itemRotation);
            if (AlreadyParried > 0f) {
                AlreadyParried += 1f;
            }
        }

        internal float ThrustDisplaceRatio() {
            return CalamityUtils.PiecewiseAnimation(ParryProgress, anticipation, thrust, retract);
        }

        internal float RotationRatio() {
            return CalamityUtils.PiecewiseAnimation(ParryProgress, openMore, close, stayClosed);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Timer > ParryTime) {
                if (Main.myPlayer == Owner.whoAmI) {
                    Texture2D value = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarBack").Value;
                    Texture2D value2 = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarFront").Value;
                    Vector2 position = Owner.Center - Main.screenPosition + new Vector2(0f, -36f) - value.Size() / 2f;
                    Rectangle value3 = new Rectangle(0, 0, (int)((Timer - ParryTime) / (340f - ParryTime) * value2.Width), value2.Height);
                    float num = Timer <= ParryTime + 25f ? (Timer - ParryTime) / 25f : 340f - Timer <= 8f ? Projectile.timeLeft / 8f : 1f;
                    Color color = Main.hslToRgb((float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.2f) * 0.05f + 0.08f, 1f, 0.65f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 7f) * 0.1f);
                    Main.spriteBatch.Draw(value, position, color * num);
                    Main.spriteBatch.Draw(value2, position, value3, color * num * 0.8f);
                }

                return false;
            }

            Texture2D value4 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsLeft").Value;
            Texture2D value5 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsLeftGlow").Value;
            Texture2D value6 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsRight").Value;
            Texture2D value7 = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Melee/SunderingScissorsRightGlow").Value;
            float num2 = Projectile.rotation + MathF.PI / 4f;
            float rotation = MathHelper.Lerp(num2 - MathF.PI / 4f, num2, RotationRatio());
            float rotation2 = MathHelper.Lerp(num2 + MathF.PI / 4f, num2, RotationRatio());
            Vector2 origin = new Vector2(33f, 86f);
            Vector2 origin2 = new Vector2(44f, 86f);
            Vector2 position2 = Owner.Center + Projectile.velocity * 15f + Projectile.velocity * ThrustDisplaceRatio() * 50f - Main.screenPosition;
            Main.EntitySpriteDraw(value6, position2, null, lightColor, rotation2, origin2, Projectile.scale, SpriteEffects.None);
            Main.EntitySpriteDraw(value7, position2, null, Color.Lerp(lightColor, Color.White, 0.75f), rotation2, origin2, Projectile.scale, SpriteEffects.None);
            Main.EntitySpriteDraw(value4, position2, null, lightColor, rotation, origin, Projectile.scale, SpriteEffects.None);
            Main.EntitySpriteDraw(value5, position2, null, Color.Lerp(lightColor, Color.White, 0.75f), rotation, origin, Projectile.scale, SpriteEffects.None);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.myPlayer == Owner.whoAmI) {
                SoundStyle style = SoundID.Item35;
                style.Volume = SoundID.Item35.Volume * 2f;
                SoundEngine.PlaySound(in style);
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(initialized);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            initialized = reader.ReadBoolean();
        }
    }
}
