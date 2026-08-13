using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>岩浆枪管，命中/消亡留喷口，周期喷发</summary>
    internal sealed class MagmaVentBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(255, 105, 30);

        public override void Apply(ref ShootContext ctx) {
            ctx.DamageMul += -0.06f;
            ctx.HomingMul += -0.34f;
            ctx.BeamSpeedMul += -0.1f;
            ctx.ManaCostMul += 0.36f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            SpawnVent(beam, target.Bottom, Math.Max(damageDone / 4, 1));
        }

        public override void OnBeamKill(CyberTraceBeamProj beam, int timeLeft) {
            if (beam.IsDerived || beam.SuppressDeathEffects || beam.Projectile.numHits > 0) return;
            SpawnVent(beam, beam.Projectile.Center, Math.Max(beam.Projectile.damage / 4, 1));
        }

        private static void SpawnVent(CyberTraceBeamProj beam, Vector2 center, int damage) {
            if (beam.Projectile.owner != Main.myPlayer) return;
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                center, Vector2.Zero,
                ModContent.ProjectileType<SHPCMagmaVentProj>(),
                damage, 0f, beam.Projectile.owner);
        }
    }

    /// <summary>熔岩喷口，过热间歇泉；喷涌满柱+熔珠挂淌+余温简芯，柱高对齐判定线</summary>
    internal sealed class SHPCMagmaVentProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private const int Lifetime = 120;
        private const int PulseInterval = 30;
        private const int JetPointCount = 12;
        //熔岩色阶,同武器超驱语系
        private static readonly Vector3 JetCoreVec = new Color(255, 175, 70).ToVector3();
        private static readonly Vector3 JetGlowVec = new Color(255, 64, 18).ToVector3();
        private static readonly Vector3 JetAuraVec = new Color(150, 12, 2).ToVector3();

        private Vector2[] jetPos;
        private Trail jetTrail;

        /// <summary>喷发包络 0-1，localAI[0] 同时是伤害窗</summary>
        private float Pulse => MathHelper.Clamp(Projectile.localAI[0] / 9f, 0f, 1f);
        /// <summary>破土升起 8f</summary>
        private float BirthEnv => MathHelper.Clamp((Lifetime - Projectile.timeLeft) / 8f, 0f, 1f);
        /// <summary>塌熄回落 14f</summary>
        private float DeathEnv => MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);

        /// <summary>可视柱高，喷发满 190px 与 Colliding 判定线同源</summary>
        private float JetHeight(float pulse, float env) => 190f * env * (0.52f + 0.48f * MathF.Sqrt(pulse));

        public override void SetDefaults() {
            Projectile.width = 56;
            Projectile.height = 180;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.72f;
            }
        }

        public override void AI() {
            //出生拍,破土喷溅
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                BirthBurst();
            }
            if (Projectile.localAI[0] > 0f) {
                Projectile.localAI[0]--;
            }
            if (!VaultUtils.IsPointOnScreen(Projectile.Center - Main.screenPosition, 220)) {
                return;
            }
            int age = Lifetime - Projectile.timeLeft;
            Tile tile = Framing.GetTileSafely(Projectile.Center.ToTileCoordinates());
            bool nearLava = Projectile.Center.Y / 16f > Main.UnderworldLayer
                || (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Lava);
            int interval = nearLava ? 20 : PulseInterval;
            if (Main.rand.NextBool(interval) && age > 0) {
                EruptionPulse(nearLava);
            }
            //熔岩区持续吐烟火星
            if (nearLava && Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 6 == 0) {
                PRTLoader.NewParticle<PRT_LavaFire>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-22f, 22f), Main.rand.NextFloat(-4f, 6f)),
                    new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-3.6f, -0.8f)),
                    Color.White, Main.rand.NextFloat(0.6f, 1.0f))?.SetLifetime(28, 50);
            }
            float pulse = Projectile.localAI[0] / 9f;
            Lighting.AddLight(Projectile.Center, new Vector3(1.5f, 0.5f, 0.1f) * MathHelper.Clamp(0.4f + pulse * 0.6f, 0f, 1.5f));
            Lighting.AddLight(Projectile.Center - Vector2.UnitY * 80f, new Vector3(1.0f, 0.35f, 0.08f) * pulse);
        }

        private void EruptionPulse(bool nearLava) {
            Projectile.localAI[0] = 9f;
            if (Main.netMode == NetmodeID.Server) return;
            //熔岩/地狱火混合
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_LavaFire>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-26f, 26f), Main.rand.NextFloat(-2f, 8f)),
                    new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), Main.rand.NextFloat(-9f, -4f)),
                    Color.White, Main.rand.NextFloat(0.9f, 1.6f))?.SetLifetime(40, 90);
            }
            for (int i = 0; i < 6; i++) {
                PRT_HellFlame hf = new PRT_HellFlame {
                    Position = Projectile.Center + new Vector2(Main.rand.NextFloat(-22f, 22f), Main.rand.NextFloat(-12f, 4f)),
                    Velocity = new Vector2(Main.rand.NextFloat(-1.8f, 1.8f), Main.rand.NextFloat(-7f, -3f)),
                    Scale = Main.rand.NextFloat(0.7f, 1.2f),
                };
                PRTLoader.AddParticle(hf);
            }
            //地面热浪环
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center + Vector2.UnitY * 4f, Vector2.Zero, new Color(255, 140, 50, 0), 0.05f).Configure(0.05f, 0.55f, 22);
            //碎屑火星
            for (int i = 0; i < 12; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-9f, -2f));
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + new Vector2(Main.rand.NextFloat(-22f, 22f), 0f), vel, Color.Lerp(new Color(255, 220, 130), new Color(255, 90, 25), Main.rand.NextFloat()), Main.rand.NextFloat(0.5f, 1.2f)).Configure(true, Main.rand.Next(20, 38));
            }
            //熔珠越顶抛出,重力回落挂淌
            for (int i = 0; i < 5; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-3.2f, 3.2f), Main.rand.NextFloat(-13f, -8f));
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), -Main.rand.NextFloat(90f, 150f)),
                    vel, Color.Lerp(new Color(255, 200, 110), new Color(255, 70, 20), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.8f, 1.5f)).Configure(true, Main.rand.Next(30, 55));
            }
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.55f, Pitch = -0.2f }, Projectile.Center);
            if (nearLava) {
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.4f, Pitch = -0.4f }, Projectile.Center);
            }
        }

        /// <summary>出生拍，破土喷溅+闷响</summary>
        private void BirthBurst() {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.6f, Pitch = -0.45f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.5f, Pitch = -0.2f }, Projectile.Center);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-4.5f, 4.5f), Main.rand.NextFloat(-7.5f, -2f));
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), 4f), vel,
                    Color.Lerp(new Color(255, 220, 130), new Color(255, 90, 25), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.5f, 1.1f)).Configure(true, Main.rand.Next(22, 40));
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center + Vector2.UnitY * 6f, Vector2.Zero,
                new Color(255, 140, 50, 0), 0.05f).Configure(0.05f, 0.5f, 20);
        }

        public override void OnKill(int timeLeft) {
            //塌熄拍,余浆回落+闷响
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.45f, Pitch = -0.5f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Vector2 pos = Projectile.Center - Vector2.UnitY * Main.rand.NextFloat(10f, 110f);
                Vector2 vel = new(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1f, 2.5f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    Color.Lerp(new Color(255, 170, 80), new Color(200, 60, 20), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.5f, 1f)).Configure(true, Main.rand.Next(18, 34));
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero,
                new Color(255, 120, 40, 0), 0.05f).Configure(0.05f, 0.35f, 18);
        }

        public override bool? CanDamage() => Projectile.localAI[0] > 0f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float width = 28f + Projectile.localAI[0] * 2.5f;
            float point = 0f;
            Vector2 top = Projectile.Center - Vector2.UnitY * 190f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, top, width, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            //底层,熔池基座+黑烟;暗色只能走真alpha的AlphaBlend,加色物理加不出暗
            float pulse = Pulse;
            float env = BirthEnv * DeathEnv;
            if (env <= 0.03f) return false;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;

            Texture2D pool = CWRAsset.Extra_98?.Value;
            if (pool != null) {
                Vector2 porigin = pool.Size() * 0.5f;
                Vector2 poolPos = baseScreen + Vector2.UnitY * 10f;
                //张力挂边,暗基座在下更暗
                Main.spriteBatch.Draw(pool, poolPos, null, new Color(46, 12, 8) * (env * 0.78f),
                    0f, porigin, new Vector2(1.75f, 0.5f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(pool, poolPos, null, new Color(120, 34, 12) * (env * 0.6f),
                    0f, porigin, new Vector2(1.25f, 0.34f), SpriteEffects.None, 0f);
                //池面热膜,A=0 预乘加亮随脉冲呼吸
                Main.spriteBatch.Draw(pool, poolPos + Vector2.UnitY * 2f, null,
                    new Color(255, 150, 60, 0) * (env * (0.3f + pulse * 0.5f)),
                    0f, porigin, new Vector2(0.95f, 0.2f), SpriteEffects.None, 0f);
            }

            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog != null) {
                Vector2 forigin = fog.Size() * 0.5f;
                float t = (float)Main.timeForVisualEffects * 0.05f;
                //烟锚定满高比例不吃脉冲,烟有惯性不许随喷发瞬移
                float smokeAnchor = 190f * env * 0.8f;
                int seed = Projectile.whoAmI * 131;
                //黑烟三瓣,逐层随机转向+镜像防贴纸感
                for (int i = 0; i < 3; i++) {
                    float phase = t * (0.6f + (i % 2) * 0.25f) + i * 2.1f + seed;
                    Vector2 offset = new(MathF.Sin(phase) * (10f + i * 6f), -smokeAnchor - 26f - i * 34f);
                    Color smokeCol = new Color(52, 34, 32) * (env * (0.36f - i * 0.08f) * (0.55f + 0.45f * pulse));
                    SpriteEffects fx = ((seed >> i) & 1) == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                    Main.spriteBatch.Draw(fog, baseScreen + offset, null, smokeCol, phase * 0.23f, forigin,
                        0.62f + i * 0.2f, fx, 0f);
                }
            }
            return false;
        }

        private float JetWidth(float progress) {
            //口部全宽顶端收窄,撕裂交给shader尾端渐隐
            float baseW = MathHelper.Lerp(13f, 25f, Pulse);
            float taper = 1f - 0.62f * progress;
            float ripple = 1f + 0.12f * MathF.Sin((float)Main.timeForVisualEffects * 0.3f - progress * 9f);
            return baseW * taper * ripple;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            //熔浆喷柱本体,复用共享光束shader的熔岩语系;头=口部光球,尾端=顶端撕散
            if (!VaultUtils.IsPointOnScreen(Projectile.Center - Main.screenPosition, 420)) return;
            float pulse = Pulse;
            float env = BirthEnv * DeathEnv;
            if (env <= 0.03f) return;
            Effect shader = EffectLoader.CyberTraceBeam?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            float height = JetHeight(pulse, env);
            float t = (float)Main.timeForVisualEffects;
            jetPos ??= new Vector2[JetPointCount];
            Vector2 basePos = Projectile.Center + Vector2.UnitY * 8f;
            for (int i = 0; i < JetPointCount; i++) {
                float f = i / (float)(JetPointCount - 1);
                float sway = MathF.Sin(t * 0.11f + f * 5.3f + Projectile.whoAmI * 1.7f)
                    * (2.5f + 10f * f) * (0.35f + 0.65f * pulse);
                jetPos[i] = basePos - Vector2.UnitY * (height * f) + Vector2.UnitX * sway;
            }
            jetTrail ??= new Trail(jetPos, JetWidth, _ => Color.White);
            jetTrail.TrailPositions = jetPos;

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue(t * 0.055f);
            shader.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp((0.3f + 0.85f * pulse) * env, 0f, 1.15f));
            shader.Parameters["coreColor"]?.SetValue(JetCoreVec);
            shader.Parameters["glowColor"]?.SetValue(JetGlowVec);
            shader.Parameters["auraColor"]?.SetValue(JetAuraVec);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);
            shader.Parameters["overdriveAmount"]?.SetValue(0f);
            shader.Parameters["glitchBurst"]?.SetValue(0f);
            shader.Parameters["odCoreColor"]?.SetValue(JetCoreVec);
            shader.Parameters["odGlowColor"]?.SetValue(JetGlowVec);
            shader.Parameters["odAuraColor"]?.SetValue(JetAuraVec);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            jetTrail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            //加色层,顶端火冠+口部熔光;真加色批 tint 必须带 A,A=0 整层不显示
            float pulse = Pulse;
            float env = BirthEnv * DeathEnv;
            if (env <= 0.03f) return;
            float intensity = 0.35f + pulse * 0.65f;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float height = JetHeight(pulse, env);
            Vector2 crownBase = baseScreen - Vector2.UnitY * height + Vector2.UnitY * 26f;

            Texture2D fire = CWRAsset.Fire?.Value;
            if (fire != null) {
                //Fire 4x4 取一格,双层错帧防同贴图叠亮
                int frameW = fire.Width / 4;
                int frameH = fire.Height / 4;
                int idx = (int)(Main.GameUpdateCount / 4) % 16;
                Rectangle frame = new(frameW * (idx % 4), frameH * (idx / 4), frameW, frameH);
                Vector2 origin = new(frameW * 0.5f, frameH);
                spriteBatch.Draw(fire, crownBase, frame, new Color(255, 120, 40) * (intensity * env * 0.8f),
                    0f, origin, new Vector2(0.62f, 0.9f + pulse * 0.5f), SpriteEffects.None, 0f);
                int idx2 = (int)(Main.GameUpdateCount / 3 + 7) % 16;
                Rectangle frame2 = new(frameW * (idx2 % 4), frameH * (idx2 / 4), frameW, frameH);
                spriteBatch.Draw(fire, crownBase, frame2, new Color(255, 220, 150) * (intensity * env * 0.5f),
                    0f, origin, new Vector2(0.4f, 0.62f + pulse * 0.35f), SpriteEffects.FlipHorizontally, 0f);
            }

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                //口部熔光单点,只作衬垫不再当柱体
                Color inner = new Color(255, 190, 90) * (intensity * env * 0.85f);
                Color outer = new Color(150, 35, 8) * (intensity * env * 0.4f);
                SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen, inner, outer, 1.15f + pulse * 0.5f, 0f, 3);
            }
        }
    }
}
