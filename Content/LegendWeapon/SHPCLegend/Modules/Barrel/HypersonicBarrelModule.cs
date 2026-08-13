using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>超音速枪管，飞720px音爆70%，其后+30%伤+2穿透</summary>
    internal sealed class HypersonicBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //曙金
        public override Color TintColor => new(255, 200, 90);

        private const float BoomDistance = 720f;
        private const float RamjetAcceleration = 0.022f;
        private const float MaxRamjetSpeedBonus = 1.8f;

        private sealed class BeamFlightState
        {
            public Vector2 SpawnPos;
            public float RamjetSpeedBonus;
            public bool Boomed;
        }

        private readonly Dictionary<int, BeamFlightState> flightStates = [];

        public override void Apply(ref ShootContext ctx) {
            ctx.BeamSpeedMul += 0.4f;
            ctx.AttackSpeedMul += 0.1f;
            ctx.DamageMul += -0.14f;
            ctx.HomingMul += -0.4f;
        }

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived) return;
            int id = beam.Projectile.whoAmI;
            if (!flightStates.TryGetValue(id, out BeamFlightState state)) {
                state = new BeamFlightState { SpawnPos = beam.Projectile.Center };
                flightStates[id] = state;
            }

            //只限制本枪管提供的冲压增量，不截断其他改件的速度收益
            float nextRamjetBonus = MathF.Min(
                state.RamjetSpeedBonus + RamjetAcceleration, MaxRamjetSpeedBonus);
            beam.SpeedMul += nextRamjetBonus - state.RamjetSpeedBonus;
            state.RamjetSpeedBonus = nextRamjetBonus;

            if (state.Boomed) {
                //白热蒸汽尾
                if (Main.netMode != NetmodeID.Server) {
                    if (Main.rand.NextBool(2)) {
                        PRTLoader.NewParticle<PRT_Smoke>(
                            beam.Projectile.Center - beam.FlightDirection * 14f,
                            -beam.FlightDirection * 0.8f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                            new Color(255, 240, 220), Main.rand.NextFloat(0.3f, 0.55f))
                            .Configure(Main.rand.Next(22, 38), 0.65f, Main.rand.NextFloat(-0.03f, 0.03f));
                    }
                    //白热拉丝，超音速划痕
                    if (Main.rand.NextBool(5)) {
                        PRTLoader.NewParticle<PRT_Line>(
                            beam.Projectile.Center - beam.FlightDirection * Main.rand.NextFloat(6f, 26f),
                            -beam.FlightDirection * Main.rand.NextFloat(1.5f, 3.5f),
                            new Color(255, 235, 190), Main.rand.NextFloat(0.5f, 0.9f))
                            .Configure(false, Main.rand.Next(8, 14));
                    }
                }
                return;
            }

            float distSq = Vector2.DistanceSquared(state.SpawnPos, beam.Projectile.Center);
            if (distSq < BoomDistance * BoomDistance) {
                //临近音障，凝结雾环随接近度加密（把不可见的冲压加速可视化）
                float near01 = MathF.Sqrt(distSq) / BoomDistance;
                if (Main.netMode != NetmodeID.Server && near01 > 0.55f
                    && Main.rand.NextFloat() < (near01 - 0.55f) * 1.4f) {
                    Vector2 perp = beam.FlightDirection.RotatedBy(MathHelper.PiOver2)
                        * Main.rand.NextFloat(-9f, 9f) * (1.3f - near01 * 0.5f);
                    PRTLoader.NewParticle<PRT_Smoke>(
                        beam.Projectile.Center + beam.FlightDirection * 8f + perp,
                        beam.Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.3f, 0.3f),
                        new Color(235, 245, 255), Main.rand.NextFloat(0.14f, 0.26f))
                        .Configure(Main.rand.Next(10, 18), 0.5f, Main.rand.NextFloat(-0.02f, 0.02f));
                }
                return;
            }

            //破音障
            state.Boomed = true;
            beam.Projectile.damage = (int)(beam.Projectile.damage * 1.3f);
            if (beam.Projectile.penetrate > 0) {
                beam.Projectile.penetrate += 2;
            }
            if (beam.Projectile.owner == Main.myPlayer) {
                int boomDmg = Math.Max((int)(beam.Projectile.damage * 0.7f), 1);
                Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                    beam.Projectile.Center, Vector2.Zero,
                    ModContent.ProjectileType<SHPCSonicBoomProj>(),
                    boomDmg, 5f, beam.Projectile.owner,
                    ai0: beam.FlightDirection.ToRotation());
            }
            if (Main.netMode != NetmodeID.Server) {
                //破障白闪+锥喷
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = beam.FlightDirection.RotatedBy(Main.rand.NextFloat(-2.4f, 2.4f))
                        * -Main.rand.NextFloat(2f, 7f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(beam.Projectile.Center, vel,
                        new Color(255, 235, 200), Main.rand.NextFloat(0.5f, 1.1f))
                        .Configure(new Color(200, 120, 30), Main.rand.Next(10, 20));
                }
            }
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            flightStates.Remove(beam.Projectile.whoAmI);
        }
    }

    /// <summary>音爆马赫环，波前伤害；SHPCSonicBoom.fx</summary>
    internal sealed class SHPCSonicBoomProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 22;
        private const int ExpandFrames = 18;
        private const float MaxRadius = 150f;

        private static readonly Color BoomCore = new(255, 245, 225);
        private static readonly Color BoomRing = new(255, 185, 80);

        private float FlightRotation => Projectile.ai[0];
        private float Progress => MathHelper.Clamp((Lifetime - Projectile.timeLeft) / (float)ExpandFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一圈每敌一次
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item38 with { Volume = 0.55f, Pitch = 0.5f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item89 with { Volume = 0.4f, Pitch = -0.2f }, Projectile.Center);
                    //屏震距离衰减，1300px 归零，禁全端无条件满幅
                    float k = 1f - MathHelper.Clamp(Main.LocalPlayer.Distance(Projectile.Center) / 1300f, 0f, 1f);
                    SHPCNaturalFx.Shake(2.8f * k);
                }
            }
            Lighting.AddLight(Projectile.Center, BoomRing.ToVector3() * 0.6f * (1f - Progress));
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //椭圆波前，沿飞行压扁
            Vector2 rel = targetHitbox.Center.ToVector2() - Projectile.Center;
            Vector2 flightDir = FlightRotation.ToRotationVector2();
            float along = Vector2.Dot(rel, flightDir) * 1.9f;
            float across = Vector2.Dot(rel, flightDir.RotatedBy(MathHelper.PiOver2));
            float dist = MathF.Sqrt(along * along + across * across);
            float radius = MaxRadius * Progress;
            return dist >= radius - 52f && dist <= radius + 52f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2CircularEdge(4f, 4f),
                    BoomRing, Main.rand.NextFloat(0.5f, 1.0f)).Configure(true, Main.rand.Next(10, 18));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.SHPCSonicBoom?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) return false;

            float fade = MathHelper.Clamp(Projectile.timeLeft / (float)Lifetime * 1.6f, 0f, 1f);
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.04f);
            shader.Parameters["progress"]?.SetValue(MathHelper.Lerp(0.08f, 0.95f, Progress));
            shader.Parameters["fadeAlpha"]?.SetValue(fade);
            shader.Parameters["coreColor"]?.SetValue(BoomCore.ToVector3());
            shader.Parameters["ringColor"]?.SetValue(BoomRing.ToVector3());

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float drawSize = MaxRadius * 2.4f;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                FlightRotation, canvas.Size() * 0.5f,
                new Vector2(drawSize, drawSize), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
