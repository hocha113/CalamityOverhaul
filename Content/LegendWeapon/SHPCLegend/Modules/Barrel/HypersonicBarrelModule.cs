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
    /// <summary>超音速枪管，飞420px音爆70%，其后+30%伤+2穿透</summary>
    internal sealed class HypersonicBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        //曙金
        public override Color TintColor => new(255, 200, 90);

        private const float BoomDistance = 420f;

        private sealed class BeamFlightState
        {
            public Vector2 SpawnPos;
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

            //冲压加速
            beam.SpeedMul = MathF.Min(beam.SpeedMul + 0.022f, 3.2f);

            if (state.Boomed) {
                //白热蒸汽尾
                if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_Smoke>(
                        beam.Projectile.Center - beam.FlightDirection * 14f,
                        -beam.FlightDirection * 0.8f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                        new Color(255, 240, 220), Main.rand.NextFloat(0.25f, 0.5f))
                        .Configure(Main.rand.Next(22, 38), 0.65f, Main.rand.NextFloat(-0.03f, 0.03f));
                }
                return;
            }

            if (Vector2.DistanceSquared(state.SpawnPos, beam.Projectile.Center) < BoomDistance * BoomDistance) {
                return;
            }

            //破音障
            state.Boomed = true;
            beam.Projectile.damage = (int)(beam.Projectile.damage * 1.15f);
            if (beam.Projectile.penetrate > 0) {
                beam.Projectile.penetrate += 2;
            }
            if (beam.Projectile.owner == Main.myPlayer) {
                int boomDmg = Math.Max((int)(beam.Projectile.damage * 0.45f), 1);
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
                }
                SHPCNaturalFx.Shake(2.5f);
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
