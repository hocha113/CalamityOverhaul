using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.Onikiris.OniDomains
{
    /// <summary>
    /// 鬼斩领域 起手蓄力相：鬼灭式血色丝带螺旋汇聚
    /// 玩家为中心，6+9 条对数螺旋血色丝带向心卷吸，沿丝带流动血液脉冲与湿润前沿
    /// 调试触发：<see cref="Begin(Player, int, bool)"/> 或 <see cref="OniDomainDebug"/>
    /// </summary>
    internal class OniDomain : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        //=== 配色：真实血液色谱（运行期可调） ===
        /// <summary>暗血底 域底色</summary>
        public static Color BloodDark = new Color(40, 2, 5);
        /// <summary>血肉色 丝带主体</summary>
        public static Color BloodFlesh = new Color(130, 8, 14);
        /// <summary>鲜血色 高光/脉冲</summary>
        public static Color BloodBright = new Color(210, 28, 32);
        /// <summary>反光色 中心爆光与最锐利高光</summary>
        public static Color BloodGleam = new Color(255, 200, 195);

        //=== 节奏参数（可调） ===
        /// <summary>淡入帧数</summary>
        public const int FadeInTicks = 35;
        /// <summary>淡出帧数</summary>
        public const int FadeOutTicks = 30;
        /// <summary>主蓄力最大半径(像素)</summary>
        public const float MaxRadius = 640f;
        /// <summary>蓄力起始半径(像素)</summary>
        public const float StartRadius = 220f;

        //ai[0] 累计 tick；ai[1] 总时长 ticks；localAI[0] 随机种子
        private ref float Ticks => ref Projectile.ai[0];
        private ref float Duration => ref Projectile.ai[1];
        private Player Owner => Main.player[Projectile.owner];

        private float seed;
        private float progress;        //0~1 总体蓄力进度（含淡入淡出整形）
        private float chargeProgress;  //0~1 线性 charging 进度
        private float dramaProgress;   //0~1 演绎进度（ease+三阶段整形，shader 用）
        private float pulse;           //外部脉冲注入
        private float drawRadius;      //当前绘制半径
        private float rotationAngle;   //C# 累积的螺旋旋转角（脉冲式而非匀速）
        private float beatValue;       //当前心跳爆发 0~1

        private List<BloodMist> bloodMists = new List<BloodMist>();
        private List<BloodShard> bloodShards = new List<BloodShard>();

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        private static Asset<Texture2D> GlowAsset = null;
        [VaultLoaden(CWRConstant.Masking + "Fog")]
        private static Asset<Texture2D> FogAsset = null;
        [VaultLoaden(CWRConstant.Masking + "Extra_193")]
        private static Asset<Texture2D> NoiseAsset = null;
        [VaultLoaden(CWRConstant.Masking + "StarTexture")]
        private static Asset<Texture2D> StarAsset = null;
        [VaultLoaden(CWRConstant.Placeholder2)]
        private static Asset<Texture2D> QuadAsset = null;

        /// <summary>
        /// 螺旋向心血雾点：沿对数螺线从外围旋入中心
        /// </summary>
        private struct BloodMist
        {
            public float Angle;       //当前极角
            public float Radius;      //当前半径
            public float AngularVel;  //角速度
            public float RadialVel;   //径向速度(向心为负)
            public float Life;        //已存在帧
            public float MaxLife;
            public float Scale;
            public float Alpha;
            public Color Color;
            public float Rotation;
            public float RotationSpeed;
        }

        /// <summary>
        /// 裂空血色碎片：高速沿切向运动的细长几何，强化裂空感
        /// </summary>
        private struct BloodShard
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Length;
            public float Width;
            public float Rotation;
        }

        //=========================================================
        // 触发 API
        //=========================================================

        /// <summary>
        /// <see cref="Begin(Player, int, bool)"/> 的便捷别名，使用默认时长
        /// </summary>
        public static int Spawn(Player player) => Begin(player, 240, false);

        /// <summary>
        /// 在指定玩家位置开始蓄力相，单玩家仅维持一个实例（再次调用会刷新时长）
        /// </summary>
        /// <param name="player">所有者</param>
        /// <param name="duration">总时长(帧)</param>
        /// <param name="silent">true 时跳过开场音效，便于 debug 反复触发</param>
        /// <returns>新生 Projectile 的 whoAmI；失败为 -1</returns>
        public static int Begin(Player player, int duration = 240, bool silent = false) {
            if (player == null || !player.active) {
                return -1;
            }

            //同玩家已有实例时刷新计时并返回原 id
            int existing = FindOwned(player);
            if (existing >= 0) {
                var p = Main.projectile[existing];
                p.ai[0] = 0;
                p.ai[1] = Math.Max(60, duration);
                p.timeLeft = (int)p.ai[1] + 10;
                return existing;
            }

            int id = Projectile.NewProjectile(
                player.GetSource_Misc("OniDomainCharge"),
                player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<OniDomain>(),
                0, 0f, player.whoAmI,
                0f,
                Math.Max(60, duration));

            if (id < 0 || id >= Main.maxProjectiles) {
                return -1;
            }

            if (!silent) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with {
                    Volume = 1.1f,
                    Pitch = -0.55f
                }, player.Center);
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with {
                    Volume = 1.05f,
                    Pitch = -0.4f
                }, player.Center);
            }

            return id;
        }

        /// <summary>查找指定玩家已存在的 OniDomain 实例</summary>
        public static int FindOwned(Player player) {
            int type = ModContent.ProjectileType<OniDomain>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                var p = Main.projectile[i];
                if (p.active && p.type == type && p.owner == player.whoAmI) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>外部脉冲注入（如阶段切换瞬时高光叠加）</summary>
        public void InjectPulse(float strength) {
            pulse = MathHelper.Clamp(pulse + strength, 0f, 1.5f);
        }

        //=========================================================
        // 生命周期
        //=========================================================

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.alpha = 0;
            Projectile.netImportant = true;
        }

        public override void OnSpawn(IEntitySource source) {
            //每实例固定种子，错开多人施法的节律
            seed = (Projectile.whoAmI * 0.1731f + Projectile.owner * 0.2917f) % 1f;
            Projectile.localAI[0] = seed;
        }

        public override void AI() {
            Player owner = Owner;
            if (owner == null || !owner.active || owner.dead) {
                FadeOutKill();
                return;
            }

            if (Projectile.localAI[0] == 0f) {
                seed = (Projectile.whoAmI * 0.1731f + Projectile.owner * 0.2917f) % 1f;
                if (seed == 0f) {
                    seed = 0.137f;
                }
                Projectile.localAI[0] = seed;
            }
            else {
                seed = Projectile.localAI[0];
            }

            //总时长安全下限
            if (Duration < 60f) {
                Duration = 240f;
            }

            //跟随玩家
            Projectile.Center = owner.Center;

            Ticks++;
            float t = Ticks;
            float dur = Duration;

            //=== 进度整形 ===
            //charging 部分（淡入完成后开始累计到 0~1）
            float effectiveStart = FadeInTicks;
            float effectiveEnd = dur - FadeOutTicks;
            float chargeSpan = Math.Max(1f, effectiveEnd - effectiveStart);
            chargeProgress = MathHelper.Clamp((t - effectiveStart) / chargeSpan, 0f, 1f);

            //淡入/淡出整形（用于 opacity）
            float fadeIn = MathHelper.Clamp(t / Math.Max(1f, (float)FadeInTicks), 0f, 1f);
            fadeIn = EaseOutCubic(fadeIn);
            float fadeOut = MathHelper.Clamp((dur - t) / Math.Max(1f, (float)FadeOutTicks), 0f, 1f);
            fadeOut = EaseOutCubic(fadeOut);
            float life = fadeIn * fadeOut;

            //=== 演绎进度（含三阶段非线性整形） ===
            //聚集相 0~25%(雾起)、凝聚相 25~65%(丝带显形)、爆发相 65~100%(心跳脉冲)
            //先用 SmoothStep 给线性 chargeProgress 一个 S-curve，避免匀速感
            float ease = chargeProgress * chargeProgress * (3f - 2f * chargeProgress);
            dramaProgress = ease;
            progress = dramaProgress;

            //=== 心跳节奏：每 ~28 帧一次高斯尖峰 ===
            float beatPhase = (t * 0.036f) % 1f;
            float beatRaw = (float)Math.Exp(-Math.Pow((beatPhase - 0.5f) * 5.5f, 2.0));
            //仅在中后段显形
            float beatGate = MathHelper.Clamp((chargeProgress - 0.25f) / 0.5f, 0f, 1f);
            beatValue = beatRaw * beatGate;

            //=== 旋转累积：基础慢速 + 心跳推动（脉冲式而非匀速高速） ===
            //基础速度：起步极慢→爆发期中速；远低于以前的 0.36+0.55*prog
            float baseRotSpeed = MathHelper.Lerp(0.0035f, 0.0085f, dramaProgress);
            //心跳推动：每次心跳额外推一把
            float beatPush = beatValue * 0.014f;
            //外部脉冲也推动
            float pulsePush = pulse * 0.020f;
            rotationAngle += baseRotSpeed + beatPush + pulsePush;
            if (rotationAngle > MathHelper.TwoPi * 4f) {
                rotationAngle -= MathHelper.TwoPi * 4f;
            }

            //=== 半径整形：先扩张，凝聚相微缩，爆发相小幅"心跳起伏" ===
            float baseR = MathHelper.Lerp(StartRadius, MaxRadius, fadeIn);
            float shrink = MathHelper.Lerp(1f, 0.84f, dramaProgress);
            float radiusBeat = 1f + beatValue * 0.025f * dramaProgress;
            drawRadius = baseR * shrink * radiusBeat;

            //=== 粒子供给（密度随阶段，爆发期更多） ===
            SpawnBloodMist(life);
            SpawnBloodShards(life);
            SpawnDustAccent(life);
            UpdateBloodMist();
            UpdateBloodShards();

            //=== 屏幕震动：底层细微 + 心跳爆发 ===
            if (owner.whoAmI == Main.myPlayer) {
                float shake = MathHelper.Lerp(0.35f, 1.7f, dramaProgress) * life;
                shake += beatValue * (1.4f + dramaProgress * 2.4f) * life;
                owner.GetModPlayer<CWRPlayer>().GetScreenShake(shake);
            }

            //=== 光照：纯血红，心跳时刻更亮 ===
            float lightIntens = (0.80f + dramaProgress * 1.3f + beatValue * 0.55f) * life;
            Lighting.AddLight(Projectile.Center,
                1.55f * lightIntens,
                0.10f * lightIntens,
                0.10f * lightIntens);

            //外部脉冲缓慢衰减
            pulse = MathHelper.Lerp(pulse, 0f, 0.12f);

            if (t >= dur) {
                Projectile.Kill();
            }
        }

        private void FadeOutKill() {
            //剩余时长压缩到 FadeOutTicks 让画面自然消散
            float dur = Duration < 60f ? 240f : Duration;
            float remaining = dur - Ticks;
            if (remaining > FadeOutTicks) {
                Ticks = dur - FadeOutTicks;
            }
        }

        //=========================================================
        // 粒子生成 / 更新
        //=========================================================

        private void SpawnBloodMist(float life) {
            if (life < 0.05f) {
                return;
            }
            //密度随阶段：聚集相只有 2 个；凝聚相 3~4；爆发期心跳瞬间额外 3~6 个
            int baseN = (int)MathHelper.Lerp(2f, 5f, dramaProgress);
            int beatN = (int)(beatValue * 5f * dramaProgress);
            int spawnPer = baseN + beatN;
            for (int i = 0; i < spawnPer; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                //从外缘略外起步，跟着丝带"卷进来"
                float rad = drawRadius * Main.rand.NextFloat(0.9f, 1.20f);

                //向心径向 + 顺时切向（统一旋向）形成螺旋
                //角速度降低 ~50%，避免后期粒子糊成一团；爆发心跳时短暂加速
                float radialVel = -Main.rand.NextFloat(2.5f, 4.2f) * (0.75f + dramaProgress * 0.55f);
                float angularVel = Main.rand.NextFloat(0.015f, 0.030f)
                    * (0.85f + dramaProgress * 0.85f + beatValue * 0.6f);

                //真血色谱：暗血/血肉/鲜血混搭，主体偏暗
                Color c = Main.rand.Next(5) switch {
                    0 => new Color(165, 14, 22),   //血肉色
                    1 => new Color(115, 6, 12),    //深血肉
                    2 => new Color(75, 3, 8),      //凝血
                    3 => new Color(195, 24, 30),   //偶发鲜血亮
                    _ => new Color(50, 2, 5)       //极暗凝血
                };

                bloodMists.Add(new BloodMist {
                    Angle = ang,
                    Radius = rad,
                    AngularVel = angularVel,
                    RadialVel = radialVel,
                    Life = 0,
                    MaxLife = Main.rand.NextFloat(60f, 100f),
                    Scale = Main.rand.NextFloat(0.55f, 1.10f) * (0.95f + chargeProgress * 0.55f),
                    Alpha = 0f,
                    Color = c,
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    RotationSpeed = Main.rand.NextFloat(-0.06f, 0.06f)
                });
            }
        }

        private void UpdateBloodMist() {
            for (int i = bloodMists.Count - 1; i >= 0; i--) {
                var m = bloodMists[i];
                m.Life++;
                m.Alpha = MathHelper.Lerp(m.Alpha, 1f, 0.10f);
                m.Angle += m.AngularVel;
                m.Radius += m.RadialVel;
                //开普勒感：靠近中心角速度加快，但比之前更克制(0.02 系数)
                if (m.Radius > 10f) {
                    m.AngularVel = MathHelper.Lerp(m.AngularVel, m.AngularVel * 1.02f, 0.5f);
                }
                m.RadialVel *= 0.987f;
                m.Rotation += m.RotationSpeed;

                //尾声 fade
                float lifeR = m.Life / m.MaxLife;
                if (lifeR > 0.7f) {
                    m.Alpha = MathHelper.Lerp(1f, 0f, (lifeR - 0.7f) / 0.3f);
                }
                if (m.Radius < 14f || m.Life >= m.MaxLife) {
                    bloodMists.RemoveAt(i);
                }
                else {
                    bloodMists[i] = m;
                }
            }
        }

        private void SpawnBloodShards(float life) {
            //血色飞溅：仅凝聚相后开始，爆发心跳期密集
            if (life < 0.1f || dramaProgress < 0.30f) {
                return;
            }
            int chance = Math.Max(2, 9 - (int)(dramaProgress * 7f));
            //心跳爆发期保底高频
            if (beatValue > 0.4f) {
                chance = Math.Max(1, chance - 2);
            }
            if (!Main.rand.NextBool(chance)) {
                return;
            }
            float ang = Main.rand.NextFloat(MathHelper.TwoPi);
            float r = drawRadius * Main.rand.NextFloat(0.55f, 0.95f);
            Vector2 pos = Projectile.Center + ang.ToRotationVector2() * r;
            //沿丝带切向 + 较强向心，模拟血液被卷向中心的飞溅高光
            //丝带切向 = 极角 +90° 旋转，与 BloodMist 同旋向
            Vector2 tangent = ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
            Vector2 radial = (Projectile.Center - pos).SafeNormalize(Vector2.Zero);
            //切向 / 向心 比例 ~ 1.0 : 0.6，制造螺旋飞溅而非纯切向
            Vector2 vel = tangent * Main.rand.NextFloat(7f, 12f)
                        + radial * Main.rand.NextFloat(4f, 8f);

            bloodShards.Add(new BloodShard {
                Position = pos,
                Velocity = vel,
                Life = 0,
                MaxLife = Main.rand.NextFloat(18f, 34f),
                Length = Main.rand.NextFloat(28f, 65f),
                Width = Main.rand.NextFloat(1.2f, 2.2f),
                Rotation = vel.ToRotation()
            });
        }

        private void UpdateBloodShards() {
            for (int i = bloodShards.Count - 1; i >= 0; i--) {
                var s = bloodShards[i];
                s.Life++;
                s.Position += s.Velocity;
                s.Velocity *= 0.965f;
                s.Rotation = s.Velocity.ToRotation();
                if (s.Life >= s.MaxLife) {
                    bloodShards.RemoveAt(i);
                }
                else {
                    bloodShards[i] = s;
                }
            }
        }

        private void SpawnDustAccent(float life) {
            if (life < 0.15f || !Main.rand.NextBool(2)) {
                return;
            }
            //外缘血色尘屑：沿切向略向心，模拟丝带涡流卷起的微粒
            float ang = Main.rand.NextFloat(MathHelper.TwoPi);
            float r = drawRadius * Main.rand.NextFloat(0.7f, 1.05f);
            Vector2 pos = Projectile.Center + ang.ToRotationVector2() * r;
            Vector2 tangent = ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
            Vector2 radial = (Projectile.Center - pos).SafeNormalize(Vector2.Zero);
            Vector2 vel = tangent * Main.rand.NextFloat(2.0f, 4.0f)
                        + radial * Main.rand.NextFloat(2.0f, 4.5f);

            //血色 vanilla dust，颜色压在血红范围
            Dust d = Dust.NewDustPerfect(pos, DustID.Blood, vel, 100,
                Color.Lerp(new Color(170, 12, 18), new Color(70, 4, 8), Main.rand.NextFloat()),
                Main.rand.NextFloat(1.0f, 1.7f));
            d.noGravity = true;
            d.fadeIn = 1.1f;

            //PRT 火花：鲜亮血红飞溅，爆发心跳期更密
            bool sparkOK = (dramaProgress > 0.45f && Main.rand.NextBool(3))
                        || (beatValue > 0.5f && Main.rand.NextBool(2));
            if (sparkOK) {
                Vector2 sparkVel = vel.RotatedBy(Main.rand.NextFloat(-0.3f, 0.3f))
                    * Main.rand.NextFloat(1.5f, 2.4f);
                PRTLoader.NewParticle<PRT_Spark>(pos, sparkVel,
                    BloodBright, Main.rand.NextFloat(0.4f, 0.75f))
                    .Configure(false, 20);
            }
        }

        //=========================================================
        // 绘制
        //=========================================================

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 center = Projectile.Center - Main.screenPosition;
            float time = Main.GlobalTimeWrappedHourly;
            float fadeIn = MathHelper.Clamp(Ticks / Math.Max(1f, (float)FadeInTicks), 0f, 1f);
            fadeIn = EaseOutCubic(fadeIn);
            float fadeOut = MathHelper.Clamp((Duration - Ticks) / Math.Max(1f, (float)FadeOutTicks), 0f, 1f);
            fadeOut = EaseOutCubic(fadeOut);
            float life = fadeIn * fadeOut;

            //CPU 粒子（在主着色器面片下层，先 Additive 一批 mist 作背景烟雾）
            DrawBloodMistBack(sb, life);

            //主着色器面片
            DrawChargeShader(sb, center, time, life);

            //CPU 粒子前景（mist 顶层强调 + shards 裂空碎屑 + 中心 glow）
            DrawBloodMistFront(sb, life);
            DrawBloodShards(sb, life);
            DrawCoreGlow(sb, center, time, life);

            return false;
        }

        private void DrawChargeShader(SpriteBatch sb, Vector2 center, float time, float life) {
            Effect shader = EffectLoader.OniDomainCharge?.Value;
            Texture2D quad = QuadAsset?.Value;
            Texture2D noise = NoiseAsset?.Value;
            if (shader == null || quad == null || noise == null) {
                DrawFallback(sb, center, life);
                return;
            }

            //共用 uniform
            shader.Parameters["uTime"]?.SetValue(time);
            shader.Parameters["uProgress"]?.SetValue(dramaProgress);
            shader.Parameters["uIntensity"]?.SetValue(0.78f + dramaProgress * 0.65f);
            shader.Parameters["uOpacity"]?.SetValue(life);
            shader.Parameters["uSeed"]?.SetValue(seed);
            shader.Parameters["uPulse"]?.SetValue(pulse);
            shader.Parameters["uBeat"]?.SetValue(beatValue);
            shader.Parameters["uRotation"]?.SetValue(rotationAngle);
            shader.Parameters["uBloodDark"]?.SetValue(BloodDark.ToVector3());
            shader.Parameters["uBloodFlesh"]?.SetValue(BloodFlesh.ToVector3());
            shader.Parameters["uBloodBright"]?.SetValue(BloodBright.ToVector3());
            shader.Parameters["uBloodGleam"]?.SetValue(BloodGleam.ToVector3());
            shader.Parameters["uImage1"]?.SetValue(noise);

            float diameter = drawRadius * 2.15f;
            Vector2 scale = new Vector2(diameter / quad.Width, diameter / quad.Height);
            Vector2 origin = quad.Size() * 0.5f;

            //=== Pass 1: TechBase (AlphaBlend) 写暗血底+丝带阴影 ===
            shader.CurrentTechnique = shader.Techniques["TechBase"];
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(quad, center, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);

            //=== Pass 2: TechHighlight (Additive) 叠加鲜血脉冲与反光 ===
            shader.CurrentTechnique = shader.Techniques["TechHighlight"];
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(quad, center, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);

            //恢复
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawFallback(SpriteBatch sb, Vector2 center, float life) {
            Texture2D glow = GlowAsset?.Value;
            if (glow == null) {
                return;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            float scale = drawRadius / glow.Width * 4f;
            sb.Draw(glow, center, null, BloodFlesh * life * 0.85f, 0f,
                glow.Size() / 2f, scale, SpriteEffects.None, 0f);
            sb.Draw(glow, center, null, BloodBright * life * 0.5f, 0f,
                glow.Size() / 2f, scale * 0.4f, SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawBloodMistBack(SpriteBatch sb, float life) {
            Texture2D fog = FogAsset?.Value;
            if (fog == null || bloodMists.Count == 0) {
                return;
            }
            //AlphaBlend：浓血色覆盖屏幕，制造"血雾浸染"而非"发光"
            //Color * scale 起 A=255 → 自动预乘 alpha 与 BlendState.AlphaBlend 匹配
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            foreach (var m in bloodMists) {
                if (m.Radius < drawRadius * 0.45f) {
                    continue;
                }
                Vector2 pos = Projectile.Center + m.Angle.ToRotationVector2() * m.Radius
                            - Main.screenPosition;
                //外圈血雾偏暗，保留厚重感
                Color c = m.Color * (m.Alpha * 0.60f * life);
                sb.Draw(fog, pos, null, c, m.Rotation, fog.Size() / 2f,
                    m.Scale * 1.35f, SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawBloodMistFront(SpriteBatch sb, float life) {
            Texture2D fog = FogAsset?.Value;
            if (fog == null || bloodMists.Count == 0) {
                return;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            foreach (var m in bloodMists) {
                if (m.Radius >= drawRadius * 0.45f) {
                    continue;
                }
                Vector2 pos = Projectile.Center + m.Angle.ToRotationVector2() * m.Radius
                            - Main.screenPosition;
                //近中心：颜色向血肉色偏，加强"血池"质感
                float hot = 1f - MathHelper.Clamp(m.Radius / (drawRadius * 0.45f), 0f, 1f);
                Color c = Color.Lerp(m.Color, BloodFlesh, hot * 0.6f);
                c *= m.Alpha * life * (0.75f + hot * 0.5f);
                sb.Draw(fog, pos, null, c, m.Rotation, fog.Size() / 2f,
                    m.Scale * (0.9f + hot * 0.55f), SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawBloodShards(SpriteBatch sb, float life) {
            Texture2D pixel = QuadAsset?.Value;
            if (pixel == null || bloodShards.Count == 0) {
                return;
            }
            //血色飞溅：保留 Additive 让条状有"反光"高亮（湿润血液反光的物理直觉）
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            foreach (var s in bloodShards) {
                float l = 1f - (s.Life / s.MaxLife);
                Vector2 pos = s.Position - Main.screenPosition;
                //鲜血主体（不带白核心）
                Color color = (BloodBright with { A = 0 }) * l * life;
                Vector2 scale = new Vector2(s.Length * l / pixel.Width, s.Width / pixel.Height);
                sb.Draw(pixel, pos, null, color, s.Rotation, pixel.Size() * 0.5f,
                    scale, SpriteEffects.None, 0f);
                //内核更亮的反光丝
                Color hi = (BloodGleam with { A = 0 }) * l * 0.55f * life;
                sb.Draw(pixel, pos, null, hi,
                    s.Rotation, pixel.Size() * 0.5f,
                    new Vector2(s.Length * l / pixel.Width, s.Width * 0.35f / pixel.Height),
                    SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawCoreGlow(SpriteBatch sb, Vector2 center, float time, float life) {
            Texture2D glow = GlowAsset?.Value;
            Texture2D star = StarAsset?.Value;
            if (glow == null) {
                return;
            }

            //与 AI 中 beatValue 直接同步，确保画面节拍统一
            float beat = beatValue;
            float fastPulse = 0.55f + 0.45f * (float)Math.Sin(time * 3.4f);
            float coreScale = (0.50f + dramaProgress * 1.30f) * life;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            //外层柔光：纯血红
            sb.Draw(glow, center, null,
                (BloodFlesh with { A = 0 }) * life * (0.55f + fastPulse * 0.35f + beat * 0.5f),
                0f, glow.Size() / 2f, coreScale * 2.1f, SpriteEffects.None, 0f);
            //内层鲜血热核
            sb.Draw(glow, center, null,
                (BloodBright with { A = 0 }) * life * (0.55f + fastPulse * 0.55f + beat * 0.6f),
                0f, glow.Size() / 2f, coreScale * 1.05f, SpriteEffects.None, 0f);
            //最白热点（charging 中后期），反光色，避免纯白星云感
            if (dramaProgress > 0.35f && life > 0.2f) {
                float hotR = (dramaProgress - 0.35f) / 0.65f * life;
                sb.Draw(glow, center, null,
                    (BloodGleam with { A = 0 }) * hotR * (0.35f + fastPulse * 0.5f + beat * 0.8f),
                    0f, glow.Size() / 2f, coreScale * 0.55f, SpriteEffects.None, 0f);
            }
            //十字反光：模拟血液表面的镜面高光，仅心跳爆发瞬间显现
            if (star != null && dramaProgress > 0.25f) {
                float starI = dramaProgress * life * (0.35f + beat * 1.1f);
                sb.Draw(star, center, null, (BloodBright with { A = 0 }) * starI,
                    time * 0.5f, star.Size() / 2f,
                    new Vector2(coreScale * 0.95f, coreScale * 0.16f),
                    SpriteEffects.None, 0f);
                sb.Draw(star, center, null, (BloodGleam with { A = 0 }) * starI * 0.7f,
                    time * 0.5f + MathHelper.PiOver2, star.Size() / 2f,
                    new Vector2(coreScale * 0.65f, coreScale * 0.11f),
                    SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
        }

        public override bool ShouldUpdatePosition() => false;
        public override bool? CanDamage() => false;

        private static float EaseOutCubic(float x) {
            float v = 1f - x;
            return 1f - v * v * v;
        }
    }
}
