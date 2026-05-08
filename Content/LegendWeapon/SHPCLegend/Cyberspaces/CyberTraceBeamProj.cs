using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.PRTTypes;
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
    /// <summary>
    /// 赛博追踪能量光束弹幕
    /// <br/>Cyberspace 系统的攻击手段 —— 带微追踪的拖尾能量光束
    /// <br/>蓝/黄/青三种随机颜色主题，光球头部，方形科幻粒子拖尾
    /// <br/>使用 <see cref="Trail"/> 条带 + CyberTraceBeam.fx 着色器渲染
    /// </summary>
    internal class CyberTraceBeamProj : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

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

        private static readonly ColorTheme[] Themes = {
            // 蓝色主题
            new() {
                Core = new Color(100, 180, 255),
                Glow = new Color(30, 100, 230),
                Aura = new Color(10, 40, 120),
                ParticleMain = new Color(80, 160, 255),
                ParticleEdge = new Color(30, 80, 200),
            },
            // 黄色主题
            new() {
                Core = new Color(255, 220, 80),
                Glow = new Color(230, 170, 30),
                Aura = new Color(150, 100, 15),
                ParticleMain = new Color(255, 200, 60),
                ParticleEdge = new Color(200, 150, 20),
            },
            // 青色主题
            new() {
                Core = new Color(80, 255, 230),
                Glow = new Color(20, 200, 180),
                Aura = new Color(10, 120, 110),
                ParticleMain = new Color(60, 240, 210),
                ParticleEdge = new Color(15, 170, 150),
            },
        };

        #endregion

        #region 超驱配色（高温红炽+白热）

        private static readonly ColorTheme OverdriveTheme = new() {
            Core = new Color(255, 255, 220),
            Glow = new Color(255, 40, 15),
            Aura = new Color(200, 10, 0),
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

        /// <summary>超驱混合量 0-1，在领域内平滑过渡到1</summary>
        private float overdriveAmount;
        /// <summary>故障爆发计时器（帧），到0时触发爆发</summary>
        private int glitchBurstTimer;
        /// <summary>当前故障爆发强度 0-1，触发后指数衰减</summary>
        private float glitchBurstIntensity;

        /// <summary>当前帧有效拖尾顶点数，供 WidthFunction 使用</summary>
        private int currentValidCount;

        /// <summary>追踪强度倍率，由 ai[1] 注入；默认 1f</summary>
        private float homingMul = 1f;

        //═════════════ 改件行为注入字段 ═════════════
        //由 SHPCOverride.OnShoot 在 Projectile.NewProjectile 之后直接写入
        //首帧根据这些字段调整属性，命中/消亡时再消费

        /// <summary>额外穿透次数</summary>
        public int ExtraPierce;
        /// <summary>生命周期倍率（>1 飞得更远）</summary>
        public float LifeMul = 1f;
        /// <summary>飞行速度倍率，由改件注入</summary>
        public float SpeedMul = 1f;
        /// <summary>命中时引爆微型脉冲爆炸</summary>
        public bool ExplodeOnHit;
        /// <summary>微型爆炸半径（像素）</summary>
        public float ExplodeRadius = 80f;
        /// <summary>剩余链式跳跃次数（每次跳跃 -1）</summary>
        public int ChainCount;
        /// <summary>链式跳跃搜索半径</summary>
        public float ChainRange = 240f;
        /// <summary>消亡时分裂的副光束数量</summary>
        public int SplitOnDeath;
        /// <summary>是否为子代光束（避免分裂/链跳无限递归）</summary>
        public bool IsDerived;
        /// <summary>爆炸伤害倍率（新星枪管特判注入，默认1f）</summary>
        public float ExplodeDamageMul = 1f;

        /// <summary>实际生命预算（按 LifeMul 缩放）</summary>
        private float lifeBudget = TotalAICalls;

        #endregion

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
            //初始化（仅第一帧）
            if (Projectile.localAI[0] == 0f) {
                themeIndex = (int)Projectile.ai[0] % Themes.Length;
                if (themeIndex < 0) themeIndex = 0;
                theme = Themes[themeIndex];
                flyAngle = Projectile.velocity.ToRotation();
                //ai[1] 由发射处注入，>0 时作为追踪倍率，未设置（==0）按 1f 处理
                homingMul = Projectile.ai[1] > 0f ? Projectile.ai[1] : 1f;
                //首帧消费改件注入：调整穿透与生命预算
                if (ExtraPierce > 0) {
                    Projectile.penetrate += ExtraPierce;
                }
                lifeBudget = TotalAICalls * MathF.Max(LifeMul, 0.1f);
                Projectile.localAI[0] = 1f;
            }

            float timeScale = TimeGear.TimeScale;
            float effectiveSpeed = Speed * MathF.Max(SpeedMul, 0.1f) * timeScale;

            //微追踪：仅在有运动时执行，方向存入flyAngle，冻结时保留原方向
            if (effectiveSpeed > 0.01f) {
                float searchRange = 120f * MathF.Max(homingMul, 1f);
                NPC target = Projectile.Center.FindClosestNPC(searchRange, true, true);
                if (target != null && Projectile.numHits == 0) {
                    float targetAngle = (target.Center - Projectile.Center).ToRotation();
                    float angleDiff = MathHelper.WrapAngle(targetAngle - flyAngle);
                    float maxTurn = 0.04f * homingMul;
                    flyAngle += MathHelper.Clamp(angleDiff, -maxTurn, maxTurn);
                }
            }

            Projectile.velocity = flyAngle.ToRotationVector2() * effectiveSpeed;
            Projectile.rotation = flyAngle;

            //自管理生命周期：age按timeScale推进，时缓期间等比延长寿命
            age += timeScale;
            Projectile.timeLeft = MaxLife;
            if (age >= lifeBudget) {
                Projectile.Kill();
                return;
            }

            //渐变：基于age比例，不依赖timeLeft
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

            //自管理拖尾位置：以最小间距记录，避免时缓下拖尾坍缩
            UpdateTrailHistory();

            // ---- 超驱检测与过渡
            bool insideDomain = Cyberspace.IsInsideDomainOf(Projectile.owner, Projectile.Center);
            float targetOD = insideDomain ? 1f : 0f;
            float prevOD = overdriveAmount;
            overdriveAmount = MathHelper.Lerp(overdriveAmount, targetOD, 0.055f); // ~0.4s过渡
            if (overdriveAmount < 0.005f) overdriveAmount = 0f;

            // 首次进入超驱阈值时，给 burstTimer 一个随机初始值，避免立即触发
            if (prevOD <= 0.3f && overdriveAmount > 0.3f) {
                glitchBurstTimer = Main.rand.Next(10, 25);
            }

            // 间歇性故障爆发（高频黑墙撕裂）
            if (overdriveAmount > 0.3f) {
                glitchBurstTimer--;
                if (glitchBurstTimer <= 0) {
                    glitchBurstIntensity = 1f;
                    glitchBurstTimer = Main.rand.Next(20, 40);
                }
            }
            glitchBurstIntensity *= 0.85f;
            if (glitchBurstIntensity < 0.01f) glitchBurstIntensity = 0f;

            // 飞行发光（超驱时高温红炽光）
            Color lightCol = overdriveAmount > 0.1f
                ? Color.Lerp(theme.Core, OverdriveTheme.Core, overdriveAmount)
                : theme.Core;
            Lighting.AddLight(Projectile.Center, lightCol.ToVector3() * (0.6f + overdriveAmount * 0.8f) * fadeAlpha);

            //方形科幻粒子（冻结时不生成）
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
            bool isod = od > 0.3f;
            int count = isod ? 3 : 2;

            // 超驱时混合配色（高温红白）
            Color mainCol = Color.Lerp(theme.ParticleMain, OverdriveTheme.ParticleMain, od);
            Color edgeCol = Color.Lerp(theme.ParticleEdge, OverdriveTheme.ParticleEdge, od);

            for (int i = 0; i < count; i++) {
                Vector2 offset = perpDir * Main.rand.NextFloat(-spread, spread);
                Vector2 spawnPos = Projectile.Center + offset;
                Vector2 particleVel = -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 4f + od * 4f)
                    + perpDir * Main.rand.NextFloat(-2f - od * 2f, 2f + od * 2f);

                float scale = Main.rand.NextFloat(0.6f, 1.4f + od * 1.2f);
                int lifeTime = Main.rand.Next(15, 35);

                PRTLoader.AddParticle(new PRT_CyberSquare(
                    spawnPos, particleVel,
                    mainCol, edgeCol,
                    scale, lifeTime
                ));
            }

            // 超驱时大量横向散射粒子（高温红炽强调）
            if (od > 0.3f && glitchBurstIntensity > 0.1f) {
                int burstCount = 3 + (int)(glitchBurstIntensity * 4f);
                for (int i = 0; i < burstCount; i++) {
                    Vector2 burstVel = perpDir * Main.rand.NextFloat(-8f, 8f)
                        + Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(-2f, 2f);
                    PRTLoader.AddParticle(new PRT_CyberSquare(
                        Projectile.Center, burstVel,
                        OverdriveTheme.ParticleEdge, OverdriveTheme.ParticleMain,
                        Main.rand.NextFloat(1.0f, 2.4f), Main.rand.Next(4, 6)//时间别太长
                    ));
                }
            }
        }

        #region Trail绘制

        private float WidthFunction(float progress) {
            // 将 tailTaper 的衰减范围压缩至有效顶点区间，
            // 使拖尾在实际末端处自然收为 0，避免刚发射时的断尾切口
            float validRatio = MathF.Max((float)currentValidCount / TrailCacheLen, 0.05f);
            float tailProgress = MathHelper.Clamp(progress / validRatio, 0f, 1f);

            float noseRise = MathF.Min(tailProgress / 0.06f, 1f);
            noseRise = MathF.Sin(noseRise * MathHelper.PiOver2);
            float tailTaper = 1f - MathF.Pow(tailProgress, 2.0f);
            float width = noseRise * tailTaper;
            // 超驱时光束更粗壮（30→50像素宽）
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

            //构建拖尾位置：头部为当前位置，后续从自管理历史取
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

            // 确保主题已初始化
            if (Projectile.localAI[0] == 0f) return;
            theme = Themes[themeIndex];

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            //取主人玩家的领域时间，避免远端拿成本地节奏
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
            // 超驱参数
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

            // 确保主题已初始化
            if (Projectile.localAI[0] == 0f) return;
            theme = Themes[themeIndex];

            // 超驱混合色
            float od = overdriveAmount;
            Color drawAura = Color.Lerp(theme.Aura, OverdriveTheme.Aura, od);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 0.9f + 0.1f * MathF.Sin((float)Main.timeForVisualEffects * 0.15f);
            // 超驱时脉冲剧烈震荡
            pulse += od * 0.3f * MathF.Sin((float)Main.timeForVisualEffects * 0.5f);
            pulse += od * glitchBurstIntensity * 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 1.2f);
            float alpha = fadeAlpha * pulse;
            Vector2 glowOrigin = glow.Size() * 0.5f;

            // 外层柔和bloom光晕（超驱时巨大炽热光晕）
            float outerScale = (2.0f + od * 2.5f) * Projectile.scale;
            Color outerColor = drawAura * alpha * (0.25f + od * 0.5f);
            spriteBatch.Draw(glow, drawPos, null, outerColor, 0f,
                glowOrigin, outerScale, SpriteEffects.None, 0f);

            // 结束当前批次，切换到Immediate模式应用能量球着色器
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
                // 传入超驱预混合色（与CyberChargeOrbProj保持一致）
                Color orbCore = Color.Lerp(theme.Core, OverdriveTheme.Core, od);
                Color orbGlow = Color.Lerp(theme.Glow, OverdriveTheme.Glow, od);
                Color orbAura = Color.Lerp(theme.Aura, OverdriveTheme.Aura, od);
                orbShader.Parameters["coreColor"]?.SetValue(orbCore.ToVector3());
                orbShader.Parameters["glowColor"]?.SetValue(orbGlow.ToVector3());
                orbShader.Parameters["auraColor"]?.SetValue(orbAura.ToVector3());
                orbShader.Parameters["orbScale"]?.SetValue(pulse);
                orbShader.Parameters["uNoiseTex"]?.SetValue(noise);
                // 超驱参数
                orbShader.Parameters["overdriveAmount"]?.SetValue(od);
                orbShader.Parameters["glitchBurst"]?.SetValue(glitchBurstIntensity);
                orbShader.Parameters["odCoreColor"]?.SetValue(OverdriveTheme.CoreVec);
                orbShader.Parameters["odGlowColor"]?.SetValue(OverdriveTheme.GlowVec);
                orbShader.Parameters["odAuraColor"]?.SetValue(OverdriveTheme.AuraVec);

                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                orbShader.CurrentTechnique.Passes[0].Apply();

                float orbDrawScale = (1.1f + od * 0.8f) * Projectile.scale;
                spriteBatch.Draw(glow, drawPos, null, Color.White, 0f,
                    glowOrigin, orbDrawScale, SpriteEffects.None, 0f);

                spriteBatch.End();
            }

            // 恢复调用者期望的批次状态（Additive + Deferred）
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
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    target.Center + vel * 2f, vel,
                    mainCol, edgeCol,
                    scale, Main.rand.Next(20, 40)
                ));
            }

            //改件钩子：仅本地玩家发射方处理派生弹幕，避免重复生成
            if (Projectile.owner == Main.myPlayer) {
                //爆炸只在原始光束触发，避免链跳每节都引爆
                if (!IsDerived && ExplodeOnHit && ExplodeRadius > 1f) {
                    SpawnMicroExplosion(target.Center);
                }
                //链跳由ChainCount计数控制递减，IsDerived不拦截，保证多段传递
                if (ChainCount > 0 && Projectile.numHits == 0) {
                    SpawnChainBeam(target);
                }
            }
            SHPCModificationSystem.ForEachModule(Main.player[Projectile.owner], mod => mod.OnBeamHitNPC(this, target, hit, damageDone));
        }
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (Projectile.localAI[0] == 0) {
                Projectile.localAI[0] = Projectile.damage;
            }

            int hitCount = Projectile.numHits;
            float multiplier = Math.Max(0.5f, (float)Math.Pow(0.7f, hitCount));
            modifiers.FinalDamage *= multiplier;
        }

        /// <summary>
        /// 命中处生成微型爆破，半径由 <see cref="ExplodeRadius"/> 控制
        /// 复用 <see cref="CyberDetonationProj"/> 并通过 localAI[2] 强制覆盖半径
        /// </summary>
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

        /// <summary>
        /// 链式跳跃：从命中目标朝最近的另一只敌人弹出一束子光束
        /// 子光束 IsDerived=true，避免再次链跳与分裂导致雪崩
        /// </summary>
        private void SpawnChainBeam(NPC source) {
            NPC next = source.Center.FindClosestNPC(ChainRange, false, true, new System.Collections.Generic.List<NPC> { source });
            if (next == null) {
                //没找到就不消耗链跳次数
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
                    //保留爆炸属性以便链上每个节点都能炸
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
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    Projectile.Center, vel,
                    mainCol, edgeCol,
                    scale, Main.rand.Next(25, 50)
                ));
            }

            //改件钩子：消亡时分裂出更小的副光束，向四周散射
            if (Projectile.owner == Main.myPlayer && !IsDerived && SplitOnDeath > 0) {
                SpawnSplitBeams();
            }
            SHPCModificationSystem.ForEachModule(Main.player[Projectile.owner], mod => mod.OnBeamKill(this, timeLeft));
        }

        /// <summary>
        /// 消亡时朝四周分裂出副光束，伤害与速度均削弱
        /// </summary>
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
