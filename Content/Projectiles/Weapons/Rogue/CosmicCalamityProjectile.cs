using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Graphics.Primitives;
using CalamityMod.NPCs.DevourerofGods;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Rogue
{
    internal class CosmicCalamityProjectile : ModProjectile
    {
        public static SoundStyle BelCanto = new("CalamityOverhaul/Assets/Sounds/BelCanto") { Volume = 2.5f };
        public override string Texture => CWRConstant.Item + "Rogue/CosmicCalamity";
        public int Time = 0;
        public int TimeUnderground = 0;
        public Vector2 SavedOldVelocity;
        public Vector2 NPCDestination;
        public Player Owner => Main.player[Projectile.owner];
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 26;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.DamageType = ModContent.GetInstance<RogueDamageClass>();
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.penetrate = 1;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Main.netMode != NetmodeID.Server) {
                Color color = Color.Lerp(Color.Cyan, Color.White, Main.rand.NextFloat(0.3f, 0.64f));
                BasePRT spark = new PRT_Spark(Projectile.Center - Projectile.velocity * 8f, -Projectile.velocity * 0.1f, false, 9, 2.3f, color * 0.1f);
                PRTLoader.AddParticle(spark);
            }
            if (Time > (Projectile.Calamity().stealthStrike ? 0 : 60)) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    if (Main.npc[i].CanBeChasedBy(Projectile.GetSource_FromThis(), false))
                        NPCDestination = Main.npc[i].Center + Main.npc[i].velocity * 5f;
                }

                TimeUnderground++;
                Vector3 DustLight = new Vector3(0.171f, 0.124f, 0.086f);
                Lighting.AddLight(Projectile.Center + Projectile.velocity, DustLight * 4);
                if (Time % 15 == 0 && TimeUnderground < 120) {
                    SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
                }
                float returnSpeed = 10;
                float acceleration = 0.2f;
                float xDist = NPCDestination.X - Projectile.Center.X;
                float yDist = NPCDestination.Y - Projectile.Center.Y;
                float dist = (float)Math.Sqrt(xDist * xDist + yDist * yDist);
                dist = returnSpeed / dist;
                xDist *= dist;
                yDist *= dist;
                float targetDist = Vector2.Distance(NPCDestination, Projectile.Center);
                if (targetDist < 1800 && TimeUnderground > 25) {
                    if (Projectile.velocity.X < xDist) {
                        Projectile.velocity.X = Projectile.velocity.X + acceleration;
                        if (Projectile.velocity.X < 0f && xDist > 0f)
                            Projectile.velocity.X += acceleration;
                    }
                    else if (Projectile.velocity.X > xDist) {
                        Projectile.velocity.X = Projectile.velocity.X - acceleration;
                        if (Projectile.velocity.X > 0f && xDist < 0f)
                            Projectile.velocity.X -= acceleration;
                    }
                    if (Projectile.velocity.Y < yDist) {
                        Projectile.velocity.Y = Projectile.velocity.Y + acceleration;
                        if (Projectile.velocity.Y < 0f && yDist > 0f)
                            Projectile.velocity.Y += acceleration;
                    }
                    else if (Projectile.velocity.Y > yDist) {
                        Projectile.velocity.Y = Projectile.velocity.Y - acceleration;
                        if (Projectile.velocity.Y > 0f && yDist < 0f)
                            Projectile.velocity.Y -= acceleration;
                    }
                }
            }
            Time++;
        }

        public static void SpanDimensionalWave(Vector2 spanPos, Vector2 vr, Color color1, Color color2, float starS, float endS, float starS2, float endS2) {
            BasePRT pulse = new PRT_DWave(spanPos - vr * 0.52f, vr / 1.5f, color1, new Vector2(1f, 2f), vr.ToRotation(), starS, endS, 60);
            PRTLoader.AddParticle(pulse);
            BasePRT pulse2 = new PRT_DWave(spanPos - vr * 0.40f, vr / 1.5f * 0.9f, color2, new Vector2(0.8f, 1.5f), vr.ToRotation(), starS2, starS2, 50);
            PRTLoader.AddParticle(pulse2);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                if (Projectile.Calamity().stealthStrike) {
                    Projectile.NewProjectile(Projectile.parent(), Projectile.Center, MathHelper.PiOver4.ToRotationVector2() * 13, ModContent.ProjectileType<CosmicCalamityRay>(), Projectile.damage / 2, 0, Projectile.owner);
                    Projectile.NewProjectile(Projectile.parent(), Projectile.Center, (MathHelper.PiOver2 + MathHelper.PiOver4).ToRotationVector2() * 13, ModContent.ProjectileType<CosmicCalamityRay>(), Projectile.damage / 2, 0, Projectile.owner);
                    for (int i = 0; i < 4; i++) {
                        float rot = MathHelper.PiOver2 * i;
                        Vector2 vr = rot.ToRotationVector2() * 10;
                        for (int j = 0; j < 16; j++) {
                            float slp = j / 16f;
                            float slp2 = 16f / j;
                            Vector2 spanPos = Projectile.Center + rot.ToRotationVector2() * 64 * j;
                            BasePRT pulse = new PRT_DWave(spanPos - vr * 0.52f, vr / 1.5f, Color.Gold, new Vector2(1f, 2f), vr.ToRotation(), 0.82f * slp, 0.32f, 60);
                            PRTLoader.AddParticle(pulse);
                            BasePRT pulse2 = new PRT_DWave(spanPos - vr * 0.40f, vr / 1.5f * 0.9f, Color.OrangeRed, new Vector2(0.8f, 1.5f), vr.ToRotation(), 0.58f * slp, 0.28f, 50);
                            PRTLoader.AddParticle(pulse2);
                        }
                    }
                    SoundEngine.PlaySound(BelCanto, Projectile.Center);
                    target.AddBuff(ModContent.BuffType<GodSlayerInferno>(), 1300);
                    Projectile.Explode(190, DevourerofGodsHead.DeathExplosionSound);
                    Projectile.Kill();
                }
                else {
                    Projectile.Explode(90, DevourerofGodsHead.DeathAnimationSound);
                    target.AddBuff(ModContent.BuffType<GodSlayerInferno>(), 300);
                }
                /*这段被注释掉的代码实现的效果非常有意思
                for (int i = 0; i < 4; i++) {
                    float rot = MathHelper.PiOver2 * i;
                    Vector2 vr = rot.ToRotationVector2() * 10;
                    for (int j = 0; j < 16; j++) {
                        float slp = j / 16f;
                        float slp2 = 16f / j;
                        Vector2 spanPos = Projectile.Center + rot.ToRotationVector2() * 64 * j;
                        CWRParticle pulse = new DimensionalWave(spanPos - vr * 0.52f, vr / 1.5f, Color.Fuchsia, new Vector2(1f, 2f), vr.ToRotation(), 0.82f * slp, 0.32f * slp2, 60);
                        CWRParticleHandler.SpawnParticle(pulse);
                        CWRParticle pulse2 = new DimensionalWave(spanPos - vr * 0.40f, vr / 1.5f * 0.9f, Color.Aqua, new Vector2(0.8f, 1.5f), vr.ToRotation(), 0.58f * slp, 0.28f * slp2, 50);
                        CWRParticleHandler.SpawnParticle(pulse2);
                    }
                }
                */
            }

            if (!Projectile.Calamity().stealthStrike) {
                BasePRT pulse = new PRT_DWave(Projectile.Center - Projectile.velocity * 0.52f, Projectile.velocity / 1.5f, Color.Fuchsia, new Vector2(1f, 2f), Projectile.velocity.ToRotation(), 0.82f, 0.32f, 60);
                PRTLoader.AddParticle(pulse);
                BasePRT pulse2 = new PRT_DWave(Projectile.Center - Projectile.velocity * 0.40f, Projectile.velocity / 1.5f * 0.9f, Color.Aqua, new Vector2(0.8f, 1.5f), Projectile.velocity.ToRotation(), 0.58f, 0.28f, 50);
                PRTLoader.AddParticle(pulse2);
            }

            for (int j = 0; j < 4; j++) {
                Vector2 dustVel = new Vector2(6, 6).RotatedByRandom(100) * Main.rand.NextFloat(0.5f, 1.2f);

                Dust dust = Dust.NewDustPerfect(Projectile.Center + dustVel * 2, 272, dustVel, 0, default, 1f);
                dust.shader = GameShaders.Armor.GetSecondaryShader(Owner.cShield, Owner);

                Dust dust2 = Dust.NewDustPerfect(Projectile.Center + dustVel * 2, 226, dustVel, 0, default, 1f);
                dust2.shader = GameShaders.Armor.GetSecondaryShader(Owner.cShield, Owner);
            }
            if (Projectile.numHits > 6) {
                Projectile.Explode(90, spanSound: false);
                Projectile.Kill();
            }
        }

        public override void OnKill(int timeLeft) {
            for (int j = 0; j < 14; j++) {
                Vector2 dustVel = new Vector2(6, 6).RotatedByRandom(100) * Main.rand.NextFloat(0.5f, 1.2f);

                Dust dust = Dust.NewDustPerfect(Projectile.Center + dustVel * 2, 272, dustVel, 0, default, 1f);
                dust.shader = GameShaders.Armor.GetSecondaryShader(Owner.cShield, Owner);

                Dust dust2 = Dust.NewDustPerfect(Projectile.Center + dustVel * 2, 226, dustVel, 0, default, 1f);
                dust.shader = GameShaders.Armor.GetSecondaryShader(Owner.cShield, Owner);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.velocity = -oldVelocity * 0.75f;
            return false;
        }

        internal Color ColorFunction(float completionRatio) {
            float amount = MathHelper.Lerp(0.65f, 1f, (float)Math.Cos((0f - Main.GlobalTimeWrappedHourly) * 3f) * 0.5f + 0.5f);
            float num = Utils.GetLerpValue(1f, 0.64f, completionRatio, clamped: true) * Projectile.Opacity;
            Color value = Color.Lerp(Main.hslToRgb(1, 1f, 0.8f), Color.PaleTurquoise, (float)Math.Sin(completionRatio * MathF.PI * 1.6f - Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f);
            return Color.Lerp(Color.White, value, amount) * num;
        }

        internal float WidthFunction(float completionRatio) {
            float amount = (float)Math.Pow(1f - completionRatio, 3.0);
            return MathHelper.Lerp(0f, 22f * Projectile.scale * Projectile.Opacity, amount);
        }

        public override bool PreDraw(ref Color lightColor) {
            GameShaders.Misc["CalamityMod:TrailStreak"].SetMiscShaderAsset_1(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/Trails/ScarletDevilStreak"));
            PrimitiveRenderer.RenderTrail(Projectile.oldPos, new PrimitiveSettings(WidthFunction, ColorFunction
                , (float _) => Projectile.Size * 0.5f, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:TrailStreak"]), 30);

            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation + MathHelper.PiOver4, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
