using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>SHPC 追踪光束，微 homing+Trail+CyberTraceBeam.fx</summary>
    internal class CyberTraceBeamProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        #region 常量与配置

        private const int TrailCacheLen = 40;
        private const int MaxLife = 180;
        private const float Speed = 14f;
        private const int ParticleInterval = 3;
        private const int ExtraUpdates = 2;
        private const int TotalAICalls = MaxLife * (1 + ExtraUpdates);
        private const float MinTrailSpacing = 10f;

        #endregion

        #region 颜色主题

        private struct ColorTheme
        {
            public Color Core;
            public Color Glow;
            public Color Aura;
            public Color ParticleMain;
            public Color ParticleEdge;

            public Vector3 CoreVec => Core.ToVector3();
            public Vector3 GlowVec => Glow.ToVector3();
            public Vector3 AuraVec => Aura.ToVector3();
        }

        //等离子三阶 青→电蓝→幻紫
        private static readonly ColorTheme[] Themes = {
            //等离子青
            new() {
                Core = new Color(110, 255, 235),
                Glow = new Color(25, 200, 185),
                Aura = new Color(8, 95, 95),
                ParticleMain = new Color(85, 240, 220),
                ParticleEdge = new Color(20, 165, 155),
            },
            //电蓝
            new() {
                Core = new Color(120, 190, 255),
                Glow = new Color(40, 115, 235),
                Aura = new Color(12, 48, 120),
                ParticleMain = new Color(95, 165, 255),
                ParticleEdge = new Color(35, 90, 205),
            },
            //幻紫
            new() {
                Core = new Color(190, 150, 255),
                Glow = new Color(125, 65, 235),
                Aura = new Color(55, 20, 115),
                ParticleMain = new Color(170, 130, 255),
                ParticleEdge = new Color(110, 55, 205),
            },
        };

        #endregion

        #region 超驱配色（熔岩橙+深红）

        //核心熔岩橙，避 Additive 纯白饱和
        private static readonly ColorTheme OverdriveTheme = new() {
            Core = new Color(255, 150, 35),      //熔岩橙芯
            Glow = new Color(255, 55, 20),       //深红辉
            Aura = new Color(160, 8, 0),         //暗红晕
            ParticleMain = new Color(255, 200, 50),
            ParticleEdge = new Color(255, 30, 5),
        };

        #endregion

        #region 实例字段

        private Trail trail;
        private Vector2[] trailPositions;
        private int themeIndex;
        private ColorTheme theme;
        private float fadeAlpha;
        private int particleTimer;
        private float age;
        private float flyAngle;
        private Vector2[] trailHistory;
        private int trailHistoryCount;

        /// <summary>超驱混合 0-1</summary>
        private float overdriveAmount;
        /// <summary>故障爆发计时</summary>
        private int glitchBurstTimer;
        /// <summary>故障爆发强度 0-1</summary>
        private float glitchBurstIntensity;

        /// <summary>有效拖尾顶点数</summary>
        private int currentValidCount;

        /// <summary>追踪倍率，ai[1]，默认1</summary>
        private float homingMul = 1f;

        //改件注入
        //SHPCOverride.OnShoot 写入，首帧/命中/消亡消费

        /// <summary>额外穿透次数</summary>
        public int ExtraPierce;
        /// <summary>寿命倍率</summary>
        public float LifeMul = 1f;
        /// <summary>飞行速倍率</summary>
        public float SpeedMul = 1f;
        /// <summary>命中微爆</summary>
        public bool ExplodeOnHit;
        /// <summary>微爆半径 px</summary>
        public float ExplodeRadius = 80f;
        /// <summary>剩余链跳</summary>
        public int ChainCount;
        /// <summary>链跳搜索半径</summary>
        public float ChainRange = 240f;
        /// <summary>消亡分裂数</summary>
        public int SplitOnDeath;
        /// <summary>子代，防递归</summary>
        public bool IsDerived;
        /// <summary>吸收/合并置位，OnBeamKill 跳过死亡派生</summary>
        public bool SuppressDeathEffects;
        /// <summary>爆炸伤倍率，默认1</summary>
        public float ExplodeDamageMul = 1f;

        /// <summary>生命预算，LifeMul</summary>
        private float lifeBudget = TotalAICalls;

        #endregion

        /// <summary>改件改方向，须写 flyAngle 非仅 velocity</summary>
        public void SetFlightDirection(Vector2 dir) {
            if (dir == Vector2.Zero) return;
            flyAngle = dir.ToRotation();
            float speed = Projectile.velocity.Length();
            Projectile.velocity = flyAngle.ToRotationVector2() * speed;
        }

        /// <summary>飞行方向，改件可读</summary>
        public Vector2 FlightDirection => flyAngle.ToRotationVector2();

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = TrailCacheLen;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = MaxLife;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.extraUpdates = ExtraUpdates;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            //首帧初始化
            if (Projectile.localAI[0] == 0f) {
                themeIndex = (int)Projectile.ai[0] % Themes.Length;
                if (themeIndex < 0) themeIndex = 0;
                theme = Themes[themeIndex];
                flyAngle = Projectile.velocity.ToRotation();
                //ai1 追踪倍率，0→1，负压制
                homingMul = Projectile.ai[1] != 0f ? Projectile.ai[1] : 1f;
                //首帧消费改件穿透/寿命
                if (ExtraPierce > 0) {
                    Projectile.penetrate += ExtraPierce;
                }
                lifeBudget = TotalAICalls * MathF.Max(LifeMul, 0.1f);
                Projectile.localAI[0] = 1f;
            }

            float timeScale = TimeGear.TimeScale;
            float effectiveSpeed = Speed * MathF.Max(SpeedMul, 0.1f) * timeScale;

            //微追踪写 flyAngle，冻结保向
            if (effectiveSpeed > 0.01f) {
                float searchRange = 120f * MathF.Max(homingMul, 1f);
                NPC target = Projectile.Center.FindClosestNPC(searchRange, true, true);
                if (target != null && Projectile.numHits == 0 && homingMul > 0f) {
                    float targetAngle = (target.Center - Projectile.Center).ToRotation();
                    float angleDiff = MathHelper.WrapAngle(targetAngle - flyAngle);
                    float maxTurn = 0.04f * homingMul;
                    flyAngle += MathHelper.Clamp(angleDiff, -maxTurn, maxTurn);
                }
            }

            Projectile.velocity = flyAngle.ToRotationVector2() * effectiveSpeed;
            Projectile.rotation = flyAngle;

            //age 按 timeScale，时缓延寿
            age += timeScale;
            Projectile.timeLeft = MaxLife;
            if (age >= lifeBudget) {
                Projectile.Kill();
                return;
            }

            //渐变按 age 比
            float lifeRatio = age / lifeBudget;
            if (lifeRatio < 0.08f) {
                fadeAlpha = lifeRatio / 0.08f;
            }
            else if (lifeRatio > 0.9f) {
                fadeAlpha = (1f - lifeRatio) / 0.1f;
            }
            else {
                fadeAlpha = 1f;
            }

            //拖尾最小间距，防时缓坍缩
            UpdateTrailHistory();

            //超驱过渡
            bool insideDomain = Cyberspace.IsInsideDomainOf(Projectile.owner, Projectile.Center);
            float targetOD = insideDomain ? 1f : 0f;
            float prevOD = overdriveAmount;
            overdriveAmount = MathHelper.Lerp(overdriveAmount, targetOD, 0.055f); //~0.4s过渡
            if (overdriveAmount < 0.005f) overdriveAmount = 0f;

            //进超驱阈值随机 burstTimer
            if (prevOD <= 0.3f && overdriveAmount > 0.3f) {
                glitchBurstTimer = Main.rand.Next(10, 25);
            }
            Projectile.extraUpdates = insideDomain ? (ExtraUpdates + 1) : ExtraUpdates;

            //间歇故障爆发
            if (overdriveAmount > 0.3f) {
                glitchBurstTimer--;
                if (glitchBurstTimer <= 0) {
                    glitchBurstIntensity = 1f;
                    glitchBurstTimer = Main.rand.Next(20, 40);
                }
            }
            glitchBurstIntensity *= 0.85f;
            if (glitchBurstIntensity < 0.01f) glitchBurstIntensity = 0f;

            //超驱红光，压加成
            Color lightCol = overdriveAmount > 0.1f
                ? Color.Lerp(theme.Core, OverdriveTheme.Core, overdriveAmount)
                : theme.Core;
            Lighting.AddLight(Projectile.Center, lightCol.ToVector3() * (0.6f + overdriveAmount * 0.35f) * fadeAlpha);

            //方粒子，冻结跳过
            if (timeScale > 0.01f) {
                int baseInterval = overdriveAmount > 0.3f ? 1 : ParticleInterval;
                int interval = (int)MathHelper.Max(baseInterval / timeScale, baseInterval);
                particleTimer++;
                if (particleTimer >= interval && Main.netMode != NetmodeID.Server) {
                    particleTimer = 0;
                    SpawnCyberParticles();
                }
            }
            SHPCModificationSystem.ForEachModule(Main.player[Projectile.owner], mod => mod.OnBeamAI(this));
        }

        private void UpdateTrailHistory() {
            trailHistory ??= new Vector2[TrailCacheLen];
            Vector2 center = Projectile.Center;
            if (trailHistoryCount == 0) {
                trailHistory[0] = center;
                trailHistoryCount = 1;
            }
            else if (Vector2.DistanceSquared(center, trailHistory[0]) >= MinTrailSpacing * MinTrailSpacing) {
                int copyLen = Math.Min(trailHistoryCount, TrailCacheLen - 1);
                Array.Copy(trailHistory, 0, trailHistory, 1, copyLen);
                trailHistory[0] = center;
                if (trailHistoryCount < TrailCacheLen) trailHistoryCount++;
            }
        }

        private void SpawnCyberParticles() {
            Vector2 perpDir = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            float od = overdriveAmount;
            float spread = 8f + od * 16f;
            int count = 2;

            //超驱混色
            Color mainCol = Color.Lerp(theme.ParticleMain, OverdriveTheme.ParticleMain, od);
            Color edgeCol = Color.Lerp(theme.ParticleEdge, OverdriveTheme.ParticleEdge, od);

            for (int i = 0; i < count; i++) {
                Vector2 offset = perpDir * Main.rand.NextFloat(-spread, spread);
                Vector2 spawnPos = Projectile.Center + offset;
                Vector2 particleVel = -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 4f + od * 4f)
                    + perpDir * Main.rand.NextFloat(-2f - od * 2f, 2f + od * 2f);

                float scale = Main.rand.NextFloat(0.6f, 1.4f + od * 1.2f);
                int lifeTime = Main.rand.Next(15, 35);

                PRTLoader.NewParticle<PRT_CyberSquare>(spawnPos, particleVel, mainCol, scale).Configure(edgeCol, lifeTime);
            }

            //超驱横散粒子
            if (od > 0.3f && glitchBurstIntensity > 0.1f) {
                int burstCount = 1 + (int)(glitchBurstIntensity * 2f);
                for (int i = 0; i < burstCount; i++) {
                    Vector2 burstVel = perpDir * Main.rand.NextFloat(-8f, 8f)
                        + Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(-2f, 2f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, burstVel, OverdriveTheme.ParticleEdge, Main.rand.NextFloat(1.0f, 2.4f)).Configure(OverdriveTheme.ParticleMain, Main.rand.Next(4, 6));
                }
            }
        }

        #region Trail绘制

        private float WidthFunction(float progress) {
            //tailTaper 压到有效顶点，避断尾
            float validRatio = MathF.Max((float)currentValidCount / TrailCacheLen, 0.05f);
            float tailProgress = MathHelper.Clamp(progress / validRatio, 0f, 1f);

            float noseRise = MathF.Min(tailProgress / 0.06f, 1f);
            noseRise = MathF.Sin(noseRise * MathHelper.PiOver2);
            float tailTaper = 1f - MathF.Pow(tailProgress, 2.0f);
            float width = noseRise * tailTaper;
            //超驱加粗 30→50
            return MathF.Max(width, 0f) * (30f + overdriveAmount * 20f);
        }

        private Color ColorFunction(Vector2 _) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailHistory == null || fadeAlpha < 0.01f)
                return;

            Effect shader = EffectLoader.CyberTraceBeam?.Value;
            if (shader == null) return;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) return;

            //拖尾位，头=当前
            trailPositions ??= new Vector2[TrailCacheLen];
            trailPositions[0] = Projectile.Center;
            for (int i = 1; i < TrailCacheLen; i++) {
                int histIdx = i - 1;
                trailPositions[i] = histIdx < trailHistoryCount
                    ? trailHistory[histIdx]
                    : trailPositions[i - 1];
            }
            currentValidCount = Math.Min(trailHistoryCount + 1, TrailCacheLen);

            if (currentValidCount < 3) return;

            trail ??= new Trail(trailPositions, WidthFunction, ColorFunction);
            trail.TrailPositions = trailPositions;

            if (Projectile.localAI[0] == 0f) return;
            theme = Themes[themeIndex];

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            //uTime 取主人领域时间
            CyberspacePlayer ownerCp = Cyberspace.For(Projectile.owner);
            float beamTime = ownerCp != null && ownerCp.Active
                ? ownerCp.EffectTime
                : (float)Main.timeForVisualEffects * 0.04f;
            shader.Parameters["uTime"]?.SetValue(beamTime);
            shader.Parameters["fadeAlpha"]?.SetValue(MathHelper.Clamp(fadeAlpha, 0f, 1f));
            shader.Parameters["coreColor"]?.SetValue(theme.CoreVec);
            shader.Parameters["glowColor"]?.SetValue(theme.GlowVec);
            shader.Parameters["auraColor"]?.SetValue(theme.AuraVec);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);
            shader.Parameters["overdriveAmount"]?.SetValue(overdriveAmount);
            shader.Parameters["glitchBurst"]?.SetValue(glitchBurstIntensity);
            shader.Parameters["odCoreColor"]?.SetValue(OverdriveTheme.CoreVec);
            shader.Parameters["odGlowColor"]?.SetValue(OverdriveTheme.GlowVec);
            shader.Parameters["odAuraColor"]?.SetValue(OverdriveTheme.AuraVec);

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = BlendState.AlphaBlend;
        }

        #endregion

        #region 光球头部绘制

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.01f) return;

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;

            if (Projectile.localAI[0] == 0f) return;
            theme = Themes[themeIndex];

            float od = overdriveAmount;
            Color drawAura = Color.Lerp(theme.Aura, OverdriveTheme.Aura, od);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 0.9f + 0.1f * MathF.Sin((float)Main.timeForVisualEffects * 0.15f);
            //超驱微脉冲
            pulse += od * 0.18f * MathF.Sin((float)Main.timeForVisualEffects * 0.5f);
            pulse += od * glitchBurstIntensity * 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 1.2f);
            float alpha = fadeAlpha * pulse;
            Vector2 glowOrigin = glow.Size() * 0.5f;

            //外 bloom，超驱增量 2.5→1.0
            float outerScale = (2.0f + od * 1.0f) * Projectile.scale;
            Color outerColor = drawAura * alpha * (0.30f + od * 0.30f);
            spriteBatch.Draw(glow, drawPos, null, outerColor, 0f,
                glowOrigin, outerScale, SpriteEffects.None, 0f);

            //Immediate 能量球着色器
            spriteBatch.End();

            Effect orbShader = EffectLoader.CyberEnergyOrb?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (orbShader != null && noise != null) {
                CyberspacePlayer ownerOrbCp = Cyberspace.For(Projectile.owner);
                float timeVal = ownerOrbCp != null && ownerOrbCp.Active
                    ? ownerOrbCp.EffectTime
                    : (float)Main.timeForVisualEffects * 0.04f;

                orbShader.Parameters["uTime"]?.SetValue(timeVal);
                orbShader.Parameters["fadeAlpha"]?.SetValue(alpha);
                //超驱预混色
                Color orbCore = Color.Lerp(theme.Core, OverdriveTheme.Core, od);
                Color orbGlow = Color.Lerp(theme.Glow, OverdriveTheme.Glow, od);
                Color orbAura = Color.Lerp(theme.Aura, OverdriveTheme.Aura, od);
                orbShader.Parameters["coreColor"]?.SetValue(orbCore.ToVector3());
                orbShader.Parameters["glowColor"]?.SetValue(orbGlow.ToVector3());
                orbShader.Parameters["auraColor"]?.SetValue(orbAura.ToVector3());
                orbShader.Parameters["orbScale"]?.SetValue(pulse);
                orbShader.Parameters["uNoiseTex"]?.SetValue(noise);
                    orbShader.Parameters["overdriveAmount"]?.SetValue(od);
                orbShader.Parameters["glitchBurst"]?.SetValue(glitchBurstIntensity);
                orbShader.Parameters["odCoreColor"]?.SetValue(OverdriveTheme.CoreVec);
                orbShader.Parameters["odGlowColor"]?.SetValue(OverdriveTheme.GlowVec);
                orbShader.Parameters["odAuraColor"]?.SetValue(OverdriveTheme.AuraVec);

                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                orbShader.CurrentTechnique.Passes[0].Apply();

                //超驱球增量 0.8→0.35
                float orbDrawScale = (1.1f + od * 0.35f) * Projectile.scale;
                spriteBatch.Draw(glow, drawPos, null, Color.White, 0f,
                    glowOrigin, orbDrawScale, SpriteEffects.None, 0f);

                spriteBatch.End();
            }

            //恢复 Additive+Deferred
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        #endregion

        public override bool PreDraw(ref Color lightColor) => false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.5f, Pitch = 0.3f }, target.Center);
            float od = overdriveAmount;
            int count = od > 0.3f ? 22 : 8;
            Color mainCol = Color.Lerp(theme.ParticleMain, OverdriveTheme.ParticleMain, od);
            Color edgeCol = Color.Lerp(theme.ParticleEdge, OverdriveTheme.ParticleEdge, od);
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f + od * 6f, 5f + od * 6f);
                float scale = Main.rand.NextFloat(0.8f, 2.0f + od * 1.2f);
                PRTLoader.NewParticle<PRT_CyberSquare>(target.Center + vel * 2f, vel, mainCol, scale).Configure(edgeCol, Main.rand.Next(20, 40));
            }

            //改件派生仅 myPlayer
            if (Projectile.owner == Main.myPlayer) {
                //爆炸仅原始光束
                if (!IsDerived && ExplodeOnHit && ExplodeRadius > 1f) {
                    SpawnMicroExplosion(target.Center);
                }
                //链跳靠 ChainCount，IsDerived 不拦
                if (ChainCount > 0 && Projectile.numHits == 0) {
                    SpawnChainBeam(target);
                }
            }
            SHPCModificationSystem.ForEachModule(Main.player[Projectile.owner], mod => mod.OnBeamHitNPC(this, target, hit, damageDone));
        }
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            int hitCount = Projectile.numHits;
            float multiplier = Math.Max(0.5f, (float)Math.Pow(0.7f, hitCount));
            modifiers.FinalDamage *= multiplier;
        }

        /// <summary>命中微爆，半径 <see cref="ExplodeRadius"/>，localAI[2] 覆写</summary>
        private void SpawnMicroExplosion(Vector2 center) {
            int dmg = Math.Max((int)(Projectile.damage * ExplodeDamageMul), 1);
            int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                center, Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                dmg, 0f, Projectile.owner,
                ai0: 0f, ai1: overdriveAmount);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].localAI[2] = ExplodeRadius;
                Main.projectile[idx].originalDamage = Projectile.originalDamage;
            }
        }

        /// <summary>链跳最近敌，子束 IsDerived</summary>
        private void SpawnChainBeam(NPC source) {
            NPC next = source.Center.FindClosestNPC(ChainRange, false, true, new System.Collections.Generic.List<NPC> { source });
            if (next == null) {
                //无目标不耗链跳
                return;
            }
            Vector2 dir = (next.Center - source.Center).SafeNormalize(Vector2.UnitX);
            int dmg = (int)(Projectile.damage * 0.55f);
            if (dmg < 1) dmg = 1;
            int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                source.Center, dir * Speed,
                ModContent.ProjectileType<CyberTraceBeamProj>(),
                dmg, Projectile.knockBack, Projectile.owner,
                ai0: themeIndex);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].ai[1] = MathHelper.Max(homingMul, 1.6f);
                if (Main.projectile[idx].ModProjectile is CyberTraceBeamProj child) {
                    child.IsDerived = true;
                    child.ChainCount = ChainCount - 1;
                    child.ChainRange = ChainRange;
                    //链节点保留爆炸
                    child.ExplodeOnHit = ExplodeOnHit;
                    child.ExplodeRadius = ExplodeRadius;
                }
            }
            Projectile.Kill();
            Projectile.netUpdate = true;
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            float od = overdriveAmount;
            int count = od > 0.3f ? 30 : 12;
            Color mainCol = Color.Lerp(theme.ParticleMain, OverdriveTheme.ParticleMain, od);
            Color edgeCol = Color.Lerp(theme.ParticleEdge, OverdriveTheme.ParticleEdge, od);
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4f + od * 6f, 4f + od * 6f) + Projectile.velocity * 0.3f;
                float scale = Main.rand.NextFloat(0.5f, 1.5f + od * 1.2f);
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, vel, mainCol, scale).Configure(edgeCol, Main.rand.Next(25, 50));
            }

            //改件消亡分裂
            if (Projectile.owner == Main.myPlayer && !IsDerived && SplitOnDeath > 0) {
                SpawnSplitBeams();
            }
            SHPCModificationSystem.ForEachModule(Main.player[Projectile.owner], mod => mod.OnBeamKill(this, timeLeft));
        }

        /// <summary>消亡四周分裂副光束</summary>
        private void SpawnSplitBeams() {
            int n = SplitOnDeath;
            int dmg = (int)(Projectile.damage * 0.6f);
            if (dmg < 1) dmg = 1;
            float baseAngle = Projectile.velocity.ToRotation();
            for (int i = 0; i < n; i++) {
                float ang = baseAngle + MathHelper.Lerp(-MathHelper.Pi * 0.6f, MathHelper.Pi * 0.6f, (i + 0.5f) / n);
                Vector2 vel = ang.ToRotationVector2() * Speed * 0.7f;
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    Projectile.Center, vel,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    dmg, Projectile.knockBack, Projectile.owner,
                    ai0: themeIndex);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].ai[1] = homingMul;
                    if (Main.projectile[idx].ModProjectile is CyberTraceBeamProj child) {
                        child.IsDerived = true;
                        child.LifeMul = 0.55f;
                    }
                }
            }
        }
    }
}
