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

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Optic
{
    /// <summary>全息瞄具，交战中周期投光栅，消解敌弹≤12，友方穿栅 +15% 伤</summary>
    internal sealed class HoloOpticModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Optic;
        //全息湖蓝
        public override Color TintColor => new(90, 220, 230);

        private const int DeployCooldown = 280;
        private int cooldownTimer;
        private float cooldownCarry;

        public override void Apply(ref ShootContext ctx) {
            ctx.SpreadMul += -0.3f;
            ctx.ManaCostMul += 0.15f;
        }

        public override void OnPlayerUpdate(Player player) {
            TickDown(ref cooldownTimer, ref cooldownCarry);
            if (player.whoAmI != Main.myPlayer) return;
            if (player.HeldItem == null || player.HeldItem.type != SHPCOverride.ID) return;
            //仅交战中投放
            if (cooldownTimer > 0 || !player.ItemAnimationActive) return;

            cooldownTimer = DeployCooldown;
            cooldownCarry = 0f;
            Vector2 aim = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(player.GetSource_FromThis(),
                Main.MouseWorld, Vector2.Zero,
                ModContent.ProjectileType<SHPCHoloLatticeProj>(),
                0, 0f, player.whoAmI,
                ai0: aim.ToRotation());
        }
    }

    /// <summary>全息光栅 190px，触栅消解/校准；SHPCHoloLattice.fx</summary>
    internal sealed class SHPCHoloLatticeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 270;
        private const float HalfLength = 95f;
        private const float SlabHalfThickness = 20f;
        private const int MaxAbsorb = 12;

        private static readonly Color LatticeMain = new(80, 215, 235);
        private static readonly Color LatticeAccent = new(200, 255, 250);

        /// <summary>已校准光束，防一束反复增伤</summary>
        private readonly HashSet<int> calibratedBeams = [];
        private int absorbCount;
        private float glitchAmount;
        private float fadeAlpha;
        private float deployProgress;

        /// <summary>弹道方向，光栅法线</summary>
        private float AimRotation => Projectile.ai[0];
        /// <summary>栅板长边方向</summary>
        private Vector2 PanelDir => (AimRotation + MathHelper.PiOver2).ToRotationVector2();
        private Vector2 NormalDir => AimRotation.ToRotationVector2();

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
        }

        public override void AI() {
            int age = Lifetime - Projectile.timeLeft;
            if (age == 0 && Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item78 with { Volume = 0.5f, Pitch = 0.55f }, Projectile.Center);
            }

            deployProgress = MathHelper.Clamp(age / 18f, 0f, 1f);
            fadeAlpha = deployProgress * MathHelper.Clamp(Projectile.timeLeft / 25f, 0f, 1f);
            glitchAmount = MathF.Max(glitchAmount - 0.06f, 0f);
            Lighting.AddLight(Projectile.Center, LatticeMain.ToVector3() * 0.5f * fadeAlpha);

            if (deployProgress < 1f) return;

            DissolveHostiles();
            CalibrateBeams();
        }

        /// <summary>敌弹触栅消解，上限 MaxAbsorb，耗尽提前退役</summary>
        private void DissolveHostiles() {
            //仅拥有者端，Kill 自同步
            if (Projectile.owner != Main.myPlayer) return;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile hostile = Main.projectile[i];
                if (!hostile.active || !hostile.hostile || hostile.friendly || hostile.damage <= 0) continue;
                if (!InsideSlab(hostile.Center)) continue;

                hostile.Kill();
                absorbCount++;
                glitchAmount = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item110 with { Volume = 0.35f, Pitch = 0.5f }, hostile.Center);
                    for (int k = 0; k < 6; k++) {
                        PRTLoader.NewParticle<PRT_CyberSquare>(hostile.Center,
                            Main.rand.NextVector2CircularEdge(3.5f, 3.5f),
                            LatticeAccent, Main.rand.NextFloat(0.5f, 1.0f))
                            .Configure(LatticeMain, Main.rand.Next(12, 22));
                    }
                }
                if (absorbCount >= MaxAbsorb) {
                    //过载退役留淡出
                    Projectile.timeLeft = Math.Min(Projectile.timeLeft, 25);
                    return;
                }
            }
        }

        /// <summary>己方穿栅校准，+25% 伤并指向最近敌</summary>
        private void CalibrateBeams() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.owner != Projectile.owner) continue;
                if (proj.ModProjectile is not CyberTraceBeamProj beam) continue;
                if (calibratedBeams.Contains(proj.whoAmI) || !InsideSlab(proj.Center)) continue;

                calibratedBeams.Add(proj.whoAmI);
                proj.damage = (int)(proj.damage * 1.15f);
                NPC target = proj.Center.FindClosestNPC(700f, false, true);
                if (target != null) {
                    Vector2 desired = target.Center - proj.Center;
                    float diff = MathHelper.WrapAngle(desired.ToRotation() - beam.FlightDirection.ToRotation());
                    if (Math.Abs(diff) < MathHelper.ToRadians(65f)) {
                        beam.SetFlightDirection(desired);
                    }
                }
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item114 with { Volume = 0.3f, Pitch = 0.9f }, proj.Center);
                    for (int k = 0; k < 4; k++) {
                        PRTLoader.NewParticle<PRT_CyberSquare>(proj.Center,
                            Main.rand.NextVector2CircularEdge(2.5f, 2.5f),
                            LatticeMain, Main.rand.NextFloat(0.4f, 0.8f))
                            .Configure(LatticeAccent, Main.rand.Next(8, 16));
                    }
                }
            }
        }

        /// <summary>点是否在栅板薄片内，局部坐标</summary>
        private bool InsideSlab(Vector2 point) {
            Vector2 rel = point - Projectile.Center;
            float along = Vector2.Dot(rel, PanelDir);
            float depth = Vector2.Dot(rel, NormalDir);
            return Math.Abs(along) <= HalfLength && Math.Abs(depth) <= SlabHalfThickness;
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.SHPCHoloLattice?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) return false;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.03f);
            shader.Parameters["fadeAlpha"]?.SetValue(fadeAlpha);
            shader.Parameters["deployProgress"]?.SetValue(deployProgress);
            shader.Parameters["glitchAmount"]?.SetValue(glitchAmount);
            shader.Parameters["mainColor"]?.SetValue(LatticeMain.ToVector3());
            shader.Parameters["accentColor"]?.SetValue(LatticeAccent.ToVector3());

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            //长边 PanelDir，短边视觉高 56px
            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                AimRotation + MathHelper.PiOver2, canvas.Size() * 0.5f,
                new Vector2(HalfLength * 2f, 56f), SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
