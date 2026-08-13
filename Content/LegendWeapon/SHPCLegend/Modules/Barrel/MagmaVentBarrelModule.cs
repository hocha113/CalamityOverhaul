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

    /// <summary>
    /// 熔岩喷口，过热间歇泉；熔融液体两态：空中熔浆柱+液团抛洒(SDF摆动/颈缩断滴)，
    /// 近地自动贴地成熔岩池(弯月挂边/黑壳结皮渐干)；柱高对齐 190px 判定线
    /// </summary>
    internal sealed class SHPCMagmaVentProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private const int Lifetime = 120;
        private const int PulseInterval = 30;
        //熔岩色阶:黑壳/熔浆/炽芯
        private static readonly Vector3 CrustVec = new Color(30, 9, 6).ToVector3();
        private static readonly Vector3 LavaVec = new Color(255, 78, 16).ToVector3();
        private static readonly Vector3 HotVec = new Color(255, 216, 128).ToVector3();

        /// <summary>空中熔岩团,仅客户端表现层模拟</summary>
        private struct MagmaGlob
        {
            public Vector2 pos;
            public Vector2 vel;
            public float seed;
            public float scale;
            public int life;
        }

        /// <summary>落地熔痕小池,仅客户端表现层</summary>
        private struct MagmaSmear
        {
            public Vector2 pos;
            public float seed;
            public float width;
            public int life;
            public int maxLife;
        }

        private readonly List<MagmaGlob> globs = [];
        private readonly List<MagmaSmear> smears = [];
        private float poolGroundY;
        private bool grounded;

        /// <summary>喷发包络 0-1，localAI[0] 同时是伤害窗</summary>
        private float Pulse => MathHelper.Clamp(Projectile.localAI[0] / 9f, 0f, 1f);
        /// <summary>破土升起 8f</summary>
        private float BirthEnv => MathHelper.Clamp((Lifetime - Projectile.timeLeft) / 8f, 0f, 1f);
        /// <summary>塌熄回落 14f</summary>
        private float DeathEnv => MathHelper.Clamp(Projectile.timeLeft / 14f, 0f, 1f);

        /// <summary>可视柱高，喷发满 190px 与 Colliding 判定线同源</summary>
        private float JetHeight(float pulse, float env) => 190f * env * (0.52f + 0.48f * MathF.Sqrt(pulse));

        //熔岩团可飞离喷口数百px,放宽画面裁切余量防边缘团块凭空消失
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;

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
            //出生拍:地形探测决定贴地态(仅表现层,判定线不动),破土喷溅
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                float? gy = FindGroundY(Projectile.Center, 7);
                grounded = gy.HasValue && gy.Value - Projectile.Center.Y < 110f;
                poolGroundY = gy ?? Projectile.Center.Y;
                BirthBurst();
            }
            if (Projectile.localAI[0] > 0f) {
                Projectile.localAI[0]--;
            }
            if (Main.netMode != NetmodeID.Server) {
                UpdateFluidSim();
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

        /// <summary>向下探地表,返回首个实心瓷砖顶面 Y(世界坐标)</summary>
        private static float? FindGroundY(Vector2 from, int maxTiles) {
            int tileX = (int)(from.X / 16f);
            int tileY = Math.Max((int)(from.Y / 16f), 10);
            for (int i = 0; i < maxTiles; i++) {
                int y = tileY + i;
                if (y >= Main.maxTilesY - 10) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]) {
                    return y * 16f;
                }
            }
            return null;
        }

        /// <summary>熔岩团/熔痕表现层模拟:重力回落,逐团探地,落地贴形留痕</summary>
        private void UpdateFluidSim() {
            for (int i = globs.Count - 1; i >= 0; i--) {
                MagmaGlob g = globs[i];
                g.vel.Y += 0.34f;
                if (g.vel.Y > 14f) {
                    g.vel.Y = 14f;
                }
                g.vel.X *= 0.995f;
                g.pos += g.vel;
                g.life--;
                Lighting.AddLight(g.pos, new Vector3(0.8f, 0.3f, 0.06f) * g.scale * 0.4f);

                Tile tile = Framing.GetTileSafely(g.pos.ToTileCoordinates());
                bool solid = tile.HasUnactuatedTile && Main.tileSolid[tile.TileType];
                if (!solid && g.life > 0) {
                    globs[i] = g;
                    continue;
                }
                if (solid) {
                    //落地:溅浆+贴地熔痕(表面张力铺开的小池)
                    Vector2 landPos = new(g.pos.X, MathF.Floor(g.pos.Y / 16f) * 16f);
                    SplashAt(landPos, g.scale);
                    if (smears.Count < 10) {
                        smears.Add(new MagmaSmear {
                            pos = landPos,
                            seed = Main.rand.NextFloat(9f),
                            width = 30f + g.scale * 28f,
                            life = 75,
                            maxLife = 75,
                        });
                    }
                }
                else {
                    //空中耗尽:凝成一粒火星坠散
                    PRTLoader.NewParticle<PRT_Spark>(g.pos, g.vel * 0.4f,
                        new Color(255, 110, 30), g.scale * 0.7f).Configure(true, Main.rand.Next(12, 22));
                }
                globs.RemoveAt(i);
            }
            for (int i = smears.Count - 1; i >= 0; i--) {
                MagmaSmear s = smears[i];
                s.life--;
                if (s.life <= 0) {
                    smears.RemoveAt(i);
                    continue;
                }
                smears[i] = s;
            }
        }

        /// <summary>熔岩团落地溅浆</summary>
        private static void SplashAt(Vector2 pos, float scale) {
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_LavaFire>(pos + new Vector2(Main.rand.NextFloat(-6f, 6f), -2f),
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(1.5f, 3.5f)),
                    Color.White, Main.rand.NextFloat(0.4f, 0.8f) * scale)?.SetLifetime(18, 34);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(pos,
                    new Vector2(Main.rand.NextFloat(-2.2f, 2.2f), -Main.rand.NextFloat(1f, 4f)),
                    Color.Lerp(new Color(255, 200, 110), new Color(255, 80, 22), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.4f, 0.9f) * scale).Configure(true, Main.rand.Next(14, 26));
            }
            if (scale > 0.95f) {
                SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.22f, Pitch = 0.1f, MaxInstances = 4 }, pos);
            }
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
            //熔岩团越顶抛出:CPU液团,重力回落,落地自动贴形
            int count = Main.rand.Next(3, 6);
            for (int i = 0; i < count; i++) {
                if (globs.Count >= 20) break;
                globs.Add(new MagmaGlob {
                    pos = Projectile.Center - Vector2.UnitY * Main.rand.NextFloat(120f, 175f)
                        + Vector2.UnitX * Main.rand.NextFloat(-12f, 12f),
                    vel = new Vector2(Main.rand.NextFloat(-3.4f, 3.4f), Main.rand.NextFloat(-7.5f, -3.5f)),
                    seed = Main.rand.NextFloat(9f),
                    scale = Main.rand.NextFloat(0.55f, 1.15f),
                    life = 130,
                });
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
            //塌熄拍,余浆回落+闷响;空中残余熔岩团凝火星散场
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.LiquidsWaterLava with { Volume = 0.45f, Pitch = -0.5f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Vector2 pos = Projectile.Center - Vector2.UnitY * Main.rand.NextFloat(10f, 110f);
                Vector2 vel = new(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1f, 2.5f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    Color.Lerp(new Color(255, 170, 80), new Color(200, 60, 20), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.5f, 1f)).Configure(true, Main.rand.Next(18, 34));
            }
            foreach (MagmaGlob g in globs) {
                PRTLoader.NewParticle<PRT_Spark>(g.pos, g.vel * 0.5f,
                    Color.Lerp(new Color(255, 170, 80), new Color(255, 80, 22), Main.rand.NextFloat()),
                    g.scale * 0.8f).Configure(true, Main.rand.Next(14, 26));
            }
            globs.Clear();
            //残留熔痕不许无声消失,留一撮余烬
            foreach (MagmaSmear s in smears) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        s.pos + new Vector2(Main.rand.NextFloat(-0.3f, 0.3f) * s.width, -2f),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.5f, 1.6f)),
                        new Color(220, 90, 30), Main.rand.NextFloat(0.3f, 0.6f)).Configure(true, Main.rand.Next(12, 22));
                }
            }
            smears.Clear();
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
            float pulse = Pulse;
            float env = BirthEnv * DeathEnv;
            if (env <= 0.03f) return false;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;

            DrawSmokeAndSeat(baseScreen, pulse, env);

            Effect effect = EffectLoader.SHPCModMagma?.Value;
            if (effect != null) {
                DrawFluid(effect, baseScreen, pulse, env);
            }
            else {
                DrawSpriteFallback(baseScreen, pulse, env);
            }
            return false;
        }

        /// <summary>底层:真alpha黑烟+贴地暗座;暗色只能走AlphaBlend,加色物理加不出暗</summary>
        private void DrawSmokeAndSeat(Vector2 baseScreen, float pulse, float env) {
            //暗座垫在熔池下,替熔池压出灼地感(仅贴地态)
            Texture2D pad = CWRAsset.Extra_98?.Value;
            if (pad != null && grounded) {
                Vector2 poolScreen = new Vector2(Projectile.Center.X, poolGroundY) - Main.screenPosition;
                Main.spriteBatch.Draw(pad, poolScreen + Vector2.UnitY * 8f, null, new Color(40, 12, 8) * (env * 0.62f),
                    0f, pad.Size() * 0.5f, new Vector2(1.65f, 0.42f), SpriteEffects.None, 0f);
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
        }

        /// <summary>着色器液体三态:贴地熔池→落地熔痕→喷浆柱→空中熔岩团,单次批翻转全画完</summary>
        private void DrawFluid(Effect effect, Vector2 baseScreen, float pulse, float env) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float seed = Projectile.whoAmI % 97 * 0.173f;
            int age = Lifetime - Projectile.timeLeft;

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uColorCrust"]?.SetValue(CrustVec);
            effect.Parameters["uColorLava"]?.SetValue(LavaVec);
            effect.Parameters["uColorHot"]?.SetValue(HotVec);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);

            //---- 贴地熔池:出生铺开,随寿命结皮,塌熄蚀退 ----
            if (grounded) {
                float spread = 0.55f + 0.45f * MathHelper.Clamp(age / 30f, 0f, 1f);
                float poolW = (150f + pulse * 26f) * spread;
                const float poolH = 44f;
                float dry = MathHelper.Clamp(age / (float)Lifetime * 0.35f + (1f - DeathEnv) * 0.65f, 0f, 1f);
                Vector2 poolDraw = new Vector2(Projectile.Center.X, poolGroundY) - Main.screenPosition;

                effect.CurrentTechnique = effect.Techniques["TechPool"];
                effect.Parameters["uSeed"]?.SetValue(seed);
                effect.Parameters["uEnv"]?.SetValue(env);
                effect.Parameters["uLife"]?.SetValue(dry);
                effect.Parameters["uAspect"]?.SetValue(poolW / poolH);
                effect.Parameters["uGlow"]?.SetValue(pulse);
                effect.CurrentTechnique.Passes[0].Apply();
                //quad中心略沉入地面,上缘为液面
                sb.Draw(pixel, poolDraw + new Vector2(0f, 10f), null, Color.White, 0f, pixel.Size() / 2f,
                    new Vector2(poolW / pixel.Width, poolH / pixel.Height), SpriteEffects.None, 0f);
            }

            //---- 落地熔痕:熔岩团摔出的小池,快速结皮干涸 ----
            if (smears.Count > 0) {
                effect.CurrentTechnique = effect.Techniques["TechPool"];
                foreach (MagmaSmear s in smears) {
                    float lifeT = 1f - s.life / (float)s.maxLife;
                    const float smearH = 26f;
                    effect.Parameters["uSeed"]?.SetValue(s.seed);
                    effect.Parameters["uEnv"]?.SetValue(env * MathHelper.Clamp(s.life / 10f, 0f, 1f));
                    effect.Parameters["uLife"]?.SetValue(lifeT);
                    effect.Parameters["uAspect"]?.SetValue(s.width / smearH);
                    effect.Parameters["uGlow"]?.SetValue(0f);
                    effect.CurrentTechnique.Passes[0].Apply();
                    sb.Draw(pixel, s.pos - Main.screenPosition + new Vector2(0f, 6f), null, Color.White, 0f,
                        pixel.Size() / 2f, new Vector2(s.width / pixel.Width, smearH / pixel.Height), SpriteEffects.None, 0f);
                }
            }

            //---- 喷浆柱:底边锚喷口,柱高与判定线同源 ----
            //画布仅放大2%:shader收头在0.94±撕裂,可视顶端≈height,不越190px判定线
            float height = JetHeight(pulse, env);
            const float canvasW = 120f;
            float canvasH = height * 1.02f;
            effect.CurrentTechnique = effect.Techniques["TechJet"];
            effect.Parameters["uSeed"]?.SetValue(seed);
            effect.Parameters["uEnv"]?.SetValue(env * (0.55f + 0.45f * pulse));
            effect.Parameters["uRise"]?.SetValue(pulse);
            effect.Parameters["uAspect"]?.SetValue(canvasH / canvasW);
            effect.Parameters["uGlow"]?.SetValue(pulse);
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(pixel, baseScreen + Vector2.UnitY * 10f, null, Color.White, 0f,
                new Vector2(pixel.Width / 2f, pixel.Height),
                new Vector2(canvasW / pixel.Width, canvasH / pixel.Height), SpriteEffects.None, 0f);

            //---- 空中熔岩团:quad长轴沿速度拉伸 ----
            if (globs.Count > 0) {
                effect.CurrentTechnique = effect.Techniques["TechGlob"];
                foreach (MagmaGlob g in globs) {
                    float stretch = MathHelper.Clamp(g.vel.Length() / 8f, 0.75f, 2.0f);
                    float sizePx = 34f * g.scale;
                    effect.Parameters["uSeed"]?.SetValue(g.seed);
                    effect.Parameters["uEnv"]?.SetValue(env);
                    effect.Parameters["uStretch"]?.SetValue(stretch);
                    effect.CurrentTechnique.Passes[0].Apply();
                    sb.Draw(pixel, g.pos - Main.screenPosition, null, Color.White,
                        g.vel.ToRotation(), pixel.Size() / 2f,
                        new Vector2(sizePx * stretch / pixel.Width, sizePx / pixel.Height), SpriteEffects.None, 0f);
                }
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>精灵回退:着色器缺失时熔池垫层+简柱+熔岩团亮点,不空窗</summary>
        private void DrawSpriteFallback(Vector2 baseScreen, float pulse, float env) {
            Texture2D pool = CWRAsset.Extra_98?.Value;
            if (pool != null && grounded) {
                Vector2 porigin = pool.Size() * 0.5f;
                Vector2 poolPos = new Vector2(Projectile.Center.X, poolGroundY) - Main.screenPosition + Vector2.UnitY * 6f;
                Main.spriteBatch.Draw(pool, poolPos, null, new Color(120, 34, 12) * (env * 0.6f),
                    0f, porigin, new Vector2(1.25f, 0.34f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(pool, poolPos + Vector2.UnitY * 2f, null,
                    new Color(255, 150, 60, 0) * (env * (0.3f + pulse * 0.5f)),
                    0f, porigin, new Vector2(0.95f, 0.2f), SpriteEffects.None, 0f);
            }
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 gorigin = glow.Size() * 0.5f;
            float height = JetHeight(pulse, env);
            for (int i = 0; i < 5; i++) {
                float f = i / 4f;
                Vector2 pos = baseScreen - Vector2.UnitY * (height * f);
                float s = MathHelper.Lerp(0.55f, 0.26f, f) * (0.8f + 0.4f * pulse);
                Main.spriteBatch.Draw(glow, pos, null, new Color(255, 90, 24, 60) * (env * (0.72f - 0.34f * f)),
                    0f, gorigin, s, SpriteEffects.None, 0f);
            }
            foreach (MagmaGlob g in globs) {
                Main.spriteBatch.Draw(glow, g.pos - Main.screenPosition, null, new Color(255, 120, 40, 60) * env,
                    0f, gorigin, 0.16f * g.scale, SpriteEffects.None, 0f);
            }
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            //加色层只作辐射热,不再当柱体;真加色批 tint 必须带 A,A=0 整层不显示
            float pulse = Pulse;
            float env = BirthEnv * DeathEnv;
            if (env <= 0.03f) return;
            float intensity = 0.35f + pulse * 0.65f;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            if (!VaultUtils.IsPointOnScreen(baseScreen, 520)) return;
            float height = JetHeight(pulse, env);

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 gorigin = glow.Size() * 0.5f;

            //口部辐射热光
            Color inner = new Color(255, 190, 90) * (intensity * env * 0.85f);
            Color outer = new Color(150, 35, 8) * (intensity * env * 0.4f);
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen, inner, outer, 1.15f + pulse * 0.5f, 0f, 3);

            //柱身热雾衬:窄长竖光,弱到只读作辐射不读作柱体
            spriteBatch.Draw(glow, baseScreen - Vector2.UnitY * height * 0.5f, null,
                new Color(255, 90, 24) * (env * (0.14f + 0.18f * pulse)),
                0f, gorigin, new Vector2(0.72f, height / glow.Height * 1.2f), SpriteEffects.None, 0f);

            //熔岩团辐射热点
            foreach (MagmaGlob g in globs) {
                spriteBatch.Draw(glow, g.pos - Main.screenPosition, null,
                    new Color(255, 120, 40) * (env * 0.45f * g.scale),
                    0f, gorigin, 0.22f * g.scale, SpriteEffects.None, 0f);
            }
        }
    }
}
