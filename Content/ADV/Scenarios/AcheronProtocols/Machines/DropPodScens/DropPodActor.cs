using CalamityOverhaul.Common;
using InnoVault.Actors;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.DropPodScens
{
    /// <summary>
    /// 空降仓实体，在世界坐标中管理空降仓的逻辑、物理和绘制
    /// </summary>
    internal class DropPodActor : Actor
    {
        /// <summary>
        /// 坠落累计计时（帧）
        /// </summary>
        private int dropTimer;

        /// <summary>

        /// 当前震动偏移（世界坐标像素）

        /// </summary>
        private Vector2 shakeOffset;

        /// <summary>

        /// 再入灼烧强度 0~1

        /// </summary>
        private float reentryHeat;

        /// <summary>

        /// 尾焰粒子列表

        /// </summary>
        private readonly List<TrailParticle> trailParticles = [];

        /// <summary>

        /// 尾焰Trail路径点，从空降仓顶部向上延伸

        /// </summary>
        private const int FlameTrailPointCount = 24;
        private readonly Vector2[] flameTrailPoints = new Vector2[FlameTrailPointCount];
        private Trail flameTrail;

        /// <summary>

        /// 冲击波环列表：环绕仓头的大气压缩波

        /// </summary>
        private readonly List<ShockwaveRing> shockwaveRings = [];

        //常量
        private const int MaxTrailParticles = 60;
        private const float ShakeIntensityBase = 1.5f;
        private const float ShakeIntensityMax = 4f;

        /// <summary>

        /// 玩家控制的水平偏移（屏幕像素）

        /// </summary>
        private float horizontalOffset;
        /// <summary>
        /// 空降仓倾斜角度
        /// </summary>
        private float tiltAngle;
        /// <summary>
        /// 残骸生成计时器
        /// </summary>
        private int debrisSpawnTimer;

        /// <summary>

        /// 是否已触发嘉登来电

        /// </summary>
        private bool incomingCallTriggered;

        //── 坠入环节 ──────────────────────────────────────────────────────────────
        /// <summary>
        /// 坠入环节是否已激活：空降仓向下移出屏幕并渐黑
        /// </summary>
        private bool landingPhaseActive;

        /// <summary>

        /// 坠入环节已运行帧数

        /// </summary>
        private int landingTimer;

        /// <summary>

        /// 坠入下坠速度（世界坐标 px/帧），每帧加速

        /// </summary>
        private float landingVelocityY;

        /// <summary>

        /// 由外部调用 <see cref="TriggerLandingPhase"/> 设置的一次性触发标记

        /// </summary>
        private static bool landingPhaseRequested;
        //──────────────────────────────────────────────────────────────────────────

        /// <summary>触发坠入环节：空降仓向下移出屏幕，同时屏幕渐黑<br/> 此接口可在任意时机调用，常</summary>
        internal static void TriggerLandingPhase() {
            landingPhaseRequested = true;
        }

        public override void OnSpawn(params object[] args) {
            Width = 240;
            Height = 280;
            DrawExtendMode = 600;
            DrawLayer = ActorDrawLayer.Default;
            dropTimer = 0;
            reentryHeat = 0f;
            shakeOffset = Vector2.Zero;
            horizontalOffset = 0f;
            tiltAngle = 0f;
            debrisSpawnTimer = 0;
            incomingCallTriggered = false;
            landingPhaseActive = false;
            landingTimer = 0;
            landingVelocityY = 0f;
            landingPhaseRequested = false;
        }

        public override void AI() {
            if (!DropPodWorld.Active) {
                DropPodHeatHazeRender.Reset();
                ActorLoader.KillActor(WhoAmI);
                return;
            }

            dropTimer++;

            //── 坠入环节处理 ────────────────────────────────────────────────────────
            //接收外部触发请求
            if (landingPhaseRequested) {
                landingPhaseRequested = false;
                landingPhaseActive = true;
                landingTimer = 0;
                landingVelocityY = 0f;
            }

            if (landingPhaseActive) {
                landingTimer++;

                //加速下坠（模拟重力），上限 60px/帧
                landingVelocityY = MathHelper.Clamp(landingVelocityY + 0.45f, 0f, 60f);
                Position = new Vector2(Position.X, Position.Y + landingVelocityY);

                //坠入时消除震动，保持画面平稳
                shakeOffset = Vector2.Zero;

                //渐黑进度：80 帧内平滑插值到全黑
                float fadeProg = MathHelper.Clamp(landingTimer / 80f, 0f, 1f);
                DropPodDrawSystem.SyncLandingFade(MathHelper.SmoothStep(0f, 1f, fadeProg));
            }
            //──────────────────────────────────────────────────────────────────────

            //震动效果：坠入阶段已在上方置零，正常阶段随时间加剧
            if (!landingPhaseActive) {
                float shakeProgress = MathHelper.Clamp(dropTimer / 600f, 0f, 1f);
                float shakeIntensity = MathHelper.Lerp(ShakeIntensityBase, ShakeIntensityMax, shakeProgress);
                shakeOffset = new Vector2(
                    Main.rand.NextFloat(-shakeIntensity, shakeIntensity),
                    Main.rand.NextFloat(-shakeIntensity, shakeIntensity));
            }

            //微幅摇摆旋转：叠加玩家操控的倾斜
            Rotation = tiltAngle;

            //再入灼烧强度随时间增加
            reentryHeat = MathHelper.Clamp(dropTimer / 480f, 0f, 1f);

            //读取玩家的水平偏移和倾斜角度
            Player player = Main.LocalPlayer;
            if (player != null && player.active && player.TryGetOverride<DropPodPlayer>(out var dpPlayer)) {
                horizontalOffset = dpPlayer.HorizontalOffset;
                tiltAngle = dpPlayer.TiltAngle;
            }

            //正常阶段才锁定在玩家位置：坠入阶段 Position 已由上方的速度驱动，不能再被锁回去
            if (!landingPhaseActive && player != null && player.active) {
                Position = player.Center - Size / 2f + new Vector2(horizontalOffset, 0);
            }

            //生成残骸和新粒子：坠入阶段停止生成
            if (!landingPhaseActive) {
                if (dropTimer > 120) {
                    debrisSpawnTimer++;
                    int spawnInterval = Math.Max(40, 120 - (int)(reentryHeat * 120));
                    if (debrisSpawnTimer >= spawnInterval) {
                        debrisSpawnTimer = 0;
                        SpawnDebris();
                    }
                }

                if (dropTimer > 30 && Main.rand.NextBool(2)) {
                    SpawnTrailParticle();
                }
            }

            //更新尾焰粒子：始终更新，使坠入时已有粒子继续运动直至消失
            for (int i = trailParticles.Count - 1; i >= 0; i--) {
                trailParticles[i].Update();
                if (trailParticles[i].IsDead) {
                    trailParticles.RemoveAt(i);
                }
            }

            //更新尾焰Trail路径点：始终更新，使火焰跟随仓体下移
            UpdateFlameTrailPoints();

            if (!landingPhaseActive) {
                //生成冲击波环（再入灼烧达到一定强度后开始产生）
                if (reentryHeat > 0.15f && dropTimer % Math.Max(8, (int)(30 * (1f - reentryHeat * 0.7f))) == 0) {
                    SpawnShockwaveRing();
                }
            }

            //更新冲击波环：始终更新，坠入时已有的环继续跟随仓体下移
            for (int i = shockwaveRings.Count - 1; i >= 0; i--) {
                shockwaveRings[i].Update(Center.X);
                if (shockwaveRings[i].IsDead) {
                    shockwaveRings.RemoveAt(i);
                }
            }

            //在坠落初期触发嘉登来电
            if (!incomingCallTriggered && dropTimer == 60) {
                incomingCallTriggered = true;
                DropPodCallScenario.Instance.Start();
            }

            //同步dropTimer给DropPodDrawSystem的屏幕特效使用
            DropPodDrawSystem.SyncDropTimer(dropTimer, reentryHeat);

            //同步热浪扭曲数据：仓体屏幕中心归一化坐标 + 强度
            Vector2 screenCenter = (Center - Main.screenPosition + shakeOffset)
                / new Vector2(Main.screenWidth, Main.screenHeight);
            float hazeStrength = reentryHeat * (landingPhaseActive ? 0.3f : 1f);
            DropPodHeatHazeRender.SyncHazeData(screenCenter, hazeStrength);
        }

        /// <summary>

        /// 更新尾焰路径点：火焰从仓体顶部喷口向上(-Y)延伸 喷口（根部）固定在仓顶中心，火焰远端随倾斜角度逐渐偏转 Trail约定：[0]=起点(喷口/根部)，[Length-1]=末尾(火焰远端) 这样 GetFlameTrailWidth/Color 中 progress=0 对应根部（宽/亮），progress=1 对应远端（窄/暗）

        /// </summary>
        private void UpdateFlameTrailPoints() {
            //喷口位置：仓体中心略偏下，确保根部被仓体遮挡不露出断口
            Vector2 nozzle = Center + shakeOffset + new Vector2(0, -Height * 0.05f);

            //火焰的最终完整长度
            float fullFlameLength = 680f;

            //当前火焰实际长度：快速增长到完整长度（10帧内完成）
            float growProgress = MathHelper.Clamp(dropTimer / 10f, 0f, 1f);
            float currentFlameLength = growProgress * fullFlameLength;

            //倾斜偏转：tiltAngle > 0 仓体右倾，火焰末端向左偏（X负方向）
            float maxTiltOffsetX = -MathF.Sin(tiltAngle) * fullFlameLength * 1.5f;

            for (int i = 0; i < FlameTrailPointCount; i++) {
                //[0]=喷口(t=0), [Length-1]=火焰远端(t=1)
                float t = i / (float)(FlameTrailPointCount - 1);

                //这个点应该距离喷口多远
                float targetDist = t * fullFlameLength;

                //如果火焰还没生长到这个点的位置，就钉在当前火焰末端
                float actualDist = MathF.Min(targetDist, currentFlameLength);

                //Y方向：从喷口向上延伸（-Y）
                float y = nozzle.Y - actualDist;

                //X方向：根部(t=0)无偏移，远端(t=1)偏移最大，二次曲线过渡
                float normalizedDist = actualDist / fullFlameLength;
                float tiltOffsetX = maxTiltOffsetX * normalizedDist * normalizedDist;

                //末端轻微热扰动（仅在远离喷口的位置）
                float disturbance = normalizedDist * normalizedDist * 2f;
                float jitterX = MathF.Sin(dropTimer * 0.2f + normalizedDist * 20f) * disturbance;

                flameTrailPoints[i] = new Vector2(nozzle.X + tiltOffsetX + jitterX, y);
            }
        }

        private void SpawnShockwaveRing() {
            Texture2D podTex = DropPod.DropPodAsset?.Value;
            float podHalfHeight = podTex != null ? podTex.Height * 0.5f : 40f;

            //环生成在仓头前方，略有随机偏移
            float yOffset = podHalfHeight + 80;
            Vector2 ringCenter = Center + new Vector2(0, yOffset);

            shockwaveRings.Add(new ShockwaveRing {
                WorldCenter = ringCenter,
                YOffset = yOffset,
                Velocity = new Vector2(
                    Main.rand.NextFloat(-0.3f, 0.3f),
                    -2.5f - reentryHeat * 2f - Main.rand.NextFloat(-0.5f, 0.5f)),
                Life = 0,
                MaxLife = 60,
                MaxRadius = 50f + reentryHeat * 160f,
                Thickness = 0.08f,
                Intensity = 0.4f + reentryHeat * 0.6f
            });
        }

        internal Vector2 GetPodScreenCenter() => Center - Main.screenPosition + shakeOffset;

        /// <summary>

        /// 生成一个残骸障碍物Actor

        /// </summary>
        private void SpawnDebris() {
            //在屏幕宽度范围内随机选择一个 X 位置
            float debrisX = Main.rand.NextFloat(-Main.screenWidth * 0.3f, Main.screenWidth * 0.3f);
            //转换为世界坐标
            Vector2 spawnWorldPos = Center + new Vector2(debrisX, Main.screenHeight * 0.6f);
            ActorLoader.NewActor<DropPodDebrisActor>(spawnWorldPos, Vector2.Zero);
        }

        private void SpawnTrailParticle() {
            if (trailParticles.Count >= MaxTrailParticles) return;

            //从空降仓顶部向上喷射（世界坐标）
            Texture2D podTex = DropPod.DropPodAsset?.Value;
            float nozzleOffsetY = podTex != null ? podTex.Height * 0.45f : 120f;
            Vector2 spawnPos = Center + new Vector2(
                Main.rand.NextFloat(-20, 20),
                -nozzleOffsetY - Main.rand.NextFloat(0, 15));

            Vector2 velocity = new Vector2(
                Main.rand.NextFloat(-1.5f, 1.5f),
                Main.rand.NextFloat(-8f, -3f));

            Color baseColor = Color.Lerp(
                new Color(255, 200, 100),
                new Color(255, 120, 40),
                Main.rand.NextFloat());

            trailParticles.Add(new TrailParticle {
                Position = spawnPos,
                Velocity = velocity,
                Color = baseColor,
                Alpha = Main.rand.NextFloat(0.6f, 1f),
                Scale = Main.rand.NextFloat(2f, 5f),
                Life = 0,
                MaxLife = Main.rand.Next(20, 50)
            });
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (DropPod.DropPodAsset == null || !DropPod.DropPodAsset.IsLoaded) return false;

            Vector2 drawCenter = Center - Main.screenPosition + shakeOffset;

            //绘制再入灼烧光效（在空降仓后面）
            DrawReentryHeatEffect(spriteBatch, drawCenter);

            //绘制冲击波环（在仓体后面，营造大气压缩效果）
            DrawShockwaveRings(spriteBatch);

            //绘制尾焰Trail（在粒子和仓体之前，作为主火焰效果）
            DrawFlameTrail(spriteBatch);

            //绘制尾焰粒子
            DrawTrailParticles(spriteBatch);

            //绘制空降仓主体
            DrawDropPod(spriteBatch, drawCenter);

            return false;
        }

        /// <summary>

        /// 使用Trail和自定义着色器绘制尾焰效果

        /// </summary>
        private void DrawFlameTrail(SpriteBatch spriteBatch) {
            if (dropTimer < 10) return;
            if (EffectLoader.DropPodFlame == null || !EffectLoader.DropPodFlame.IsLoaded) return;

            //初始化或更新Trail
            flameTrail ??= new Trail(flameTrailPoints, GetFlameTrailWidth, GetFlameTrailColor);
            flameTrail.TrailPositions = flameTrailPoints;

            //切换到顶点绘制模式：与DestroyerRenderHelper一致的模式
            spriteBatch.End();

            Effect effect = EffectLoader.DropPodFlame.Value;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["globalTime"].SetValue((float)Main.timeForVisualEffects * 0.02f);
            effect.Parameters["heatIntensity"].SetValue(reentryHeat);
            effect.Parameters["uNoise"].SetValue(CWRAsset.Extra_193.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            flameTrail.DrawTrail(effect);
            flameTrail.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;

            //恢复SpriteBatch：与项目中其他End/Begin对保持一致
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>

        /// 尾焰Trail宽度函数：根部宽、末端窄，呈火焰锥形

        /// </summary>
        private float GetFlameTrailWidth(float progress) {
            //progress: 0=喷口(根部), 1=火焰远端
            float intensityFactor = MathHelper.Clamp(dropTimer / 120f, 0.3f, 1f);

            //火焰根部宽，末端收窄：加粗基础宽度，收窄更平缓
            float baseWidth = 55f + reentryHeat * 25f;
            float taper = 1f - MathF.Pow(progress, 1.2f);

            //添加脉动感
            float pulse = 1f + MathF.Sin(dropTimer * 0.15f + progress * 4f) * 0.08f;

            return baseWidth * taper * intensityFactor * pulse;
        }

        /// <summary>

        /// 尾焰Trail颜色函数：根部白黄，中段橙色，末端暗红渐隐

        /// </summary>
        private Color GetFlameTrailColor(Vector2 texCoords) {
            float progress = texCoords.X; //沿火焰长度方向
            float intensityFactor = MathHelper.Clamp(dropTimer / 120f, 0.3f, 1f);

            //三段颜色渐变
            Color coreColor = new Color(255, 240, 200);   //白黄核心
            Color midColor = new Color(255, 140, 40);     //橙色中段
            Color tipColor = new Color(180, 40, 10);      //暗红末端

            Color result;
            if (progress < 0.3f) {
                result = Color.Lerp(coreColor, midColor, progress / 0.3f);
            }
            else {
                result = Color.Lerp(midColor, tipColor, (progress - 0.3f) / 0.7f);
            }

            //末端透明度衰减
            float alpha = (1f - MathF.Pow(progress, 1.5f)) * intensityFactor;

            //再入灼烧时整体更亮
            float heatBoost = 1f + reentryHeat * 0.5f;

            return result * alpha * heatBoost;
        }

        private void DrawShockwaveRings(SpriteBatch spriteBatch) {
            if (shockwaveRings.Count == 0) return;
            if (CWRAsset.Placeholder_White == null || CWRAsset.Placeholder_White.IsDisposed) return;
            if (EffectLoader.DropPodShockwave == null || !EffectLoader.DropPodShockwave.IsLoaded) return;

            Texture2D canvas = CWRAsset.Placeholder_White.Value;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (var ring in shockwaveRings) {
                float progress = (float)ring.Life / ring.MaxLife;
                float currentRadius = ring.MaxRadius * MathHelper.SmoothStep(0.2f, 1f, progress);
                float alpha = MathF.Sin(progress * MathHelper.Pi) * ring.Intensity;

                Vector2 drawPos = ring.WorldCenter - Main.screenPosition + shakeOffset;

                //透视压缩比：横向宽、纵向窄，模拟从正上方俯视的水平环
                const float perspectiveSquish = 0.45f;

                Effect effect = EffectLoader.DropPodShockwave.Value;
                effect.Parameters["globalTime"].SetValue((float)Main.timeForVisualEffects * 0.015f);
                effect.Parameters["shockwaveIntensity"].SetValue(alpha);
                effect.Parameters["ringRadius"].SetValue(MathHelper.Lerp(0.3f, 0.85f, progress));
                effect.Parameters["ringThickness"].SetValue(ring.Thickness * (1f + (1f - progress) * 0.5f));
                effect.Parameters["squishY"].SetValue(perspectiveSquish);
                effect.Parameters["uNoise"].SetValue(CWRAsset.Extra_193.Value);
                effect.CurrentTechnique.Passes[0].Apply();

                //非等比缩放：X方向完整半径，Y方向压缩产生透视椭圆
                float drawSize = currentRadius * 2f;
                Vector2 drawScale = new Vector2(drawSize, drawSize * perspectiveSquish);
                Color ringColor = Color.Lerp(
                    new Color(180, 210, 255),
                    new Color(255, 200, 150),
                    reentryHeat * 0.6f) * alpha;
                spriteBatch.Draw(canvas, drawPos, null, ringColor,
                    0f, canvas.Size() * 0.5f, drawScale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawDropPod(SpriteBatch sb, Vector2 drawCenter) {
            Texture2D podTex = DropPod.DropPodAsset.Value;
            Vector2 origin = podTex.Size() * 0.5f;

            //外层光晕
            float glowPulse = MathF.Sin(dropTimer * 0.05f) * 0.15f + 0.85f;
            DrawSoftGlow(sb, drawCenter, new Color(80, 120, 200) * (0.3f * glowPulse), 100f);

            //主体绘制
            sb.Draw(podTex, drawCenter, null, Color.White, Rotation, origin, 1f, SpriteEffects.None, 0f);

            //再入灼烧时在空降仓顶部叠加橙红辉光
            if (reentryHeat > 0.1f) {
                Color heatColor = new Color(255, 150, 50) * (reentryHeat * 0.4f * glowPulse);
                Vector2 heatPos = drawCenter - new Vector2(0, podTex.Height * 0.35f);
                DrawSoftGlow(sb, heatPos, heatColor, 60f + reentryHeat * 30f);
            }
        }

        private void DrawReentryHeatEffect(SpriteBatch sb, Vector2 drawCenter) {
            if (reentryHeat < 0.05f) return;

            float pulse = MathF.Sin(dropTimer * 0.08f) * 0.2f + 0.8f;

            //橙红灼烧光圈
            DrawSoftGlow(sb, drawCenter - new Vector2(0, 20),
                new Color(200, 100, 30) * (reentryHeat * 0.25f * pulse), 120f);

            //外层蓝紫等离子体光圈
            DrawSoftGlow(sb, drawCenter,
                new Color(60, 80, 200) * (reentryHeat * 0.15f * pulse), 160f);
        }

        private void DrawTrailParticles(SpriteBatch sb) {
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed) return;
            Texture2D glow = CWRAsset.SoftGlow.Value;

            foreach (var p in trailParticles) {
                float lifeRatio = (float)p.Life / p.MaxLife;
                float alpha = p.Alpha * (1f - lifeRatio * lifeRatio);
                Color c = p.Color * alpha;
                float scale = p.Scale * (1f + lifeRatio * 0.5f) * 0.04f;
                Vector2 drawPos = p.Position - Main.screenPosition;
                sb.Draw(glow, drawPos, null, c with { A = 0 }, 0f, glow.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
        }

        private static void DrawSoftGlow(SpriteBatch sb, Vector2 center, Color color, float radius) {
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed) return;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float scale = radius / (glow.Width * 0.5f);
            sb.Draw(glow, center, null, color with { A = 0 }, 0f, glow.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }

        private class TrailParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public float Alpha;
            public float Scale;
            public int Life;
            public int MaxLife;
            public bool IsDead => Life >= MaxLife;

            public void Update() {
                Life++;
                Position += Velocity;
                Velocity *= 0.96f;
                Velocity.X += Main.rand.NextFloat(-0.3f, 0.3f);
            }
        }

        /// <summary>

        /// 冲击波环数据：模拟大气再入时仓头前方的弓形激波

        /// </summary>
        private class ShockwaveRing
        {
            /// <summary>环中心的世界坐标</summary>
            public Vector2 WorldCenter;
            /// <summary>环相对于仓体中心的Y偏移</summary>
            public float YOffset;
            /// <summary>每帧移动速度（世界坐标）</summary>
            public Vector2 Velocity;
            /// <summary>当前生命帧</summary>
            public int Life;
            /// <summary>最大生命帧</summary>
            public int MaxLife;
            /// <summary>完全展开时的半径（像素）</summary>
            public float MaxRadius;
            /// <summary>环的厚度比例 0~1</summary>
            public float Thickness;
            /// <summary>亮度强度</summary>
            public float Intensity;
            public bool IsDead => Life >= MaxLife;

            /// <summary>

            /// 更新环的位置：X轴跟随仓体当前位置，Y轴按自身速度独立运动

            /// </summary>
            public void Update(float podCenterX) {
                Life++;
                //X轴平滑跟随仓体位置，避免左右移动时环滞留
                WorldCenter.X = MathHelper.Lerp(WorldCenter.X, podCenterX, 0.15f);
                //Y轴按自身速度运动
                WorldCenter.Y += Velocity.Y;
                Velocity.X *= 0.97f;
                Velocity.Y *= 0.97f;
            }
        }
    }
}
