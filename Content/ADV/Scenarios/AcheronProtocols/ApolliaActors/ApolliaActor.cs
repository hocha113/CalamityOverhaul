using CalamityOverhaul.Common;
using CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors.States;
using CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines;
using InnoVault.Actors;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors
{
    /// <summary>
    /// 阿波利娅角色Actor——通过 <see cref="IApolliaState"/> 驱动行为，
    /// 运镜由 <see cref="CutsceneCamera"/> 管理并在 <see cref="ApolliaPlayer"/> 中应用
    /// </summary>
    internal class ApolliaActor : Actor
    {
        private const int TotalFrames = 11;

        #region 状态机

        /// <summary>当前行为状态实例，null 表示未激活</summary>
        internal IApolliaState CurrentState { get; private set; }

        /// <summary>
        /// 切换到新状态——自动调用旧状态的Exit和新状态的Enter
        /// </summary>
        internal void TransitionTo(IApolliaState newState) {
            CurrentState?.Exit(this);
            CurrentState = newState;
            CurrentState?.Enter(this);
        }

        #endregion

        #region 公开属性——供状态类读写

        /// <summary>运镜系统实例——由 <see cref="ApolliaPlayer"/> 读取并在ModifyScreenPosition中应用</summary>
        internal CutsceneCamera Camera { get; } = new();

        /// <summary>当前动画帧索引 (0=站立, 1~10=行走)</summary>
        internal int FrameIndex;

        /// <summary>行走朝向 (-1=左, 1=右)</summary>
        internal int WalkDirection;

        /// <summary>降落阶段的透明度 (0~1)</summary>
        internal float DescendAlpha;

        /// <summary>着陆辉光强度 (0~1)</summary>
        internal float GlowIntensity;

        /// <summary>帧纹理的单帧宽度</summary>
        internal int FrameWidth { get; private set; }

        /// <summary>帧纹理的单帧高度</summary>
        internal int FrameHeight { get; private set; }

        /// <summary>速度向量——供状态类进行物理运动</summary>
        internal new Vector2 Velocity;

        /// <summary>角色是否站在实心地面上</summary>
        internal bool OnGround;

        /// <summary>是否使用跳跃/飞行单帧纹理进行绘制</summary>
        internal bool UseJumpTexture;

        #endregion

        #region 尾焰系统

        /// <summary>尾焰Trail是否激活——由飞行状态Enter/Exit控制</summary>
        internal bool JetTrailActive;

        private const int JetTrailPointCount = 16;
        private readonly Vector2[] jetTrailPoints = new Vector2[JetTrailPointCount];
        private Trail jetTrail;
        private int jetTimer;

        private const int MaxJetParticles = 30;
        private readonly List<JetTrailParticle> jetParticles = [];

        #endregion

        #region 指令访问

        /// <summary>读取英雄面板上的当前指令</summary>
        internal HeroCommand CurrentCommand =>
            ApolliaHeroPanelUI.Instance?.HeroData?.CurrentCommand ?? HeroCommand.Follow;

        #endregion

        public override void OnSpawn(params object[] args) {
            Width = 30;
            Height = 50;
            DrawExtendMode = 600;
            DrawLayer = ActorDrawLayer.Default;

            FrameIndex = 0;
            WalkDirection = 1;
            DescendAlpha = 0f;
            GlowIntensity = 0f;
            Velocity = Vector2.Zero;
            OnGround = false;
            UseJumpTexture = false;
            Camera.Reset();

            if (ADVAsset.ApolliaActor != null) {
                FrameWidth = ADVAsset.ApolliaActor.Width;
                FrameHeight = ADVAsset.ApolliaActor.Height / TotalFrames;
            }
        }

        /// <summary>
        /// 外部触发：开始着陆演出
        /// </summary>
        internal void StartLandingCutscene(Vector2 landingPodCenter) {
            if (CurrentState != null) return;

            float offsetX = 200f;
            Vector2 rawTarget = new Vector2(landingPodCenter.X + offsetX, landingPodCenter.Y);
            Vector2 groundPos = FindGroundPosition(rawTarget);

            TransitionTo(new ApolliaDescendingState(groundPos));
        }

        public override void AI() {
            if (!MachineWorld.Active) {
                Camera.Reset();
                ActorLoader.KillActor(WhoAmI);
                return;
            }

            //状态机驱动
            if (CurrentState != null) {
                IApolliaState next = CurrentState.Update(this);
                if (next != null) {
                    TransitionTo(next);
                }
            }

            //辉光衰减
            if (GlowIntensity > 0.01f) {
                GlowIntensity *= 0.96f;
            }

            //尾焰系统更新
            if (JetTrailActive) {
                jetTimer++;
                UpdateJetTrailPoints();
            }

            for (int i = jetParticles.Count - 1; i >= 0; i--) {
                jetParticles[i].Life++;
                jetParticles[i].Position += jetParticles[i].Velocity;
                jetParticles[i].Velocity *= 0.94f;
                jetParticles[i].Velocity.X += Main.rand.NextFloat(-0.15f, 0.15f);
                if (jetParticles[i].Life >= jetParticles[i].MaxLife) {
                    jetParticles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 在角色脚下生成一个尾焰粒子——由飞行状态每帧调用
        /// </summary>
        internal void SpawnJetParticle() {
            if (VaultUtils.isServer || jetParticles.Count >= MaxJetParticles) return;

            Vector2 footPos = new(Center.X + Main.rand.NextFloat(-6, 6), Position.Y + Height);
            Vector2 vel = new(
                -WalkDirection * Main.rand.NextFloat(0.5f, 1.5f) + Main.rand.NextFloat(-0.3f, 0.3f),
                Main.rand.NextFloat(1f, 3f));

            jetParticles.Add(new JetTrailParticle {
                Position = footPos,
                Velocity = vel,
                Color = Color.Lerp(new Color(120, 200, 255), new Color(80, 140, 220), Main.rand.NextFloat()),
                Alpha = Main.rand.NextFloat(0.5f, 0.85f),
                Scale = Main.rand.NextFloat(1.5f, 3.5f),
                Life = 0,
                MaxLife = Main.rand.Next(15, 35)
            });
        }

        /// <summary>
        /// 停止尾焰并清除所有粒子——飞行状态退出时调用
        /// </summary>
        internal void StopJetTrail() {
            JetTrailActive = false;
            jetTimer = 0;
            jetTrail = null;
            jetParticles.Clear();
        }

        /// <summary>
        /// 更新尾焰Trail路径点——从脚底向下后方延伸，模拟喷射推进器尾焰。
        /// [0]=喷口(脚底), [Length-1]=火焰远端
        /// </summary>
        private void UpdateJetTrailPoints() {
            Vector2 nozzle = new(Center.X, Position.Y + Height - 10);

            float fullLength = 120f;
            float growProgress = MathHelper.Clamp(jetTimer / 15f, 0f, 1f);
            float currentLength = growProgress * fullLength;

            float trailAngleX = -WalkDirection * 0.6f;

            for (int i = 0; i < JetTrailPointCount; i++) {
                float t = i / (float)(JetTrailPointCount - 1);
                float dist = MathF.Min(t * fullLength, currentLength);
                float normalizedDist = dist / fullLength;

                float y = nozzle.Y + dist;
                float offsetX = trailAngleX * dist * normalizedDist;

                float jitter = normalizedDist * normalizedDist * 1.5f;
                float jitterX = MathF.Sin(jetTimer * 0.25f + normalizedDist * 15f) * jitter;

                jetTrailPoints[i] = new Vector2(nozzle.X + offsetX + jitterX, y);
            }
        }

        /// <summary>
        /// 使用DropPodFlame着色器绘制尾焰Trail
        /// </summary>
        private void DrawJetFlameTrail(SpriteBatch spriteBatch) {
            if (jetTimer < 3) return;
            if (EffectLoader.DropPodFlame == null || !EffectLoader.DropPodFlame.IsLoaded) return;

            jetTrail ??= new Trail(jetTrailPoints, GetJetTrailWidth, GetJetTrailColor);
            jetTrail.TrailPositions = jetTrailPoints;

            spriteBatch.End();

            Effect effect = EffectLoader.DropPodFlame.Value;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["globalTime"].SetValue((float)Main.timeForVisualEffects * 0.025f);
            effect.Parameters["heatIntensity"].SetValue(MathHelper.Clamp(jetTimer / 30f, 0.3f, 0.8f));
            effect.Parameters["uNoise"].SetValue(CWRAsset.Extra_193.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            jetTrail.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private float GetJetTrailWidth(float progress) {
            float intensityFactor = MathHelper.Clamp(jetTimer / 20f, 0.2f, 1f);
            float baseWidth = 32f;
            float taper = 1f - MathF.Pow(progress, 1.0f);
            float pulse = 1f + MathF.Sin(jetTimer * 0.2f + progress * 5f) * 0.12f;
            return baseWidth * taper * intensityFactor * pulse;
        }

        private Color GetJetTrailColor(Vector2 texCoords) {
            float progress = texCoords.X;
            float intensityFactor = MathHelper.Clamp(jetTimer / 20f, 0.2f, 1f);

            Color coreColor = new(220, 240, 255);
            Color midColor = new(80, 170, 240);
            Color tipColor = new(40, 80, 180);

            Color result = progress < 0.35f
                ? Color.Lerp(coreColor, midColor, progress / 0.35f)
                : Color.Lerp(midColor, tipColor, (progress - 0.35f) / 0.65f);

            float alpha = (1f - MathF.Pow(progress, 1.5f)) * intensityFactor;
            return result * alpha;
        }

        #region 物理辅助——供状态类调用

        /// <summary>
        /// 探测角色脚下到最近实心地面的像素距离。
        /// 返回值 ≥ 0 表示找到地面（距离像素），-1 表示在扫描范围内未找到
        /// </summary>
        /// <param name="maxTiles">向下扫描的最大格数</param>
        internal float ProbeGroundDistance(int maxTiles = 8) {
            int tileX = (int)(Center.X / 16f);
            int footTileY = (int)((Position.Y + Height) / 16f);

            for (int y = footTileY; y < footTileY + maxTiles; y++) {
                if (!WorldGen.InWorld(tileX, y)) break;
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    float groundWorldY = y * 16f;
                    return groundWorldY - (Position.Y + Height);
                }
            }

            return -1f;
        }

        /// <summary>
        /// 将角色吸附到脚下最近的实心地面上，并更新 <see cref="OnGround"/> 状态
        /// </summary>
        internal void SnapToGround() {
            int tileX = (int)(Center.X / 16f);
            int tileY = (int)((Position.Y + Height) / 16f);

            for (int y = tileY; y < tileY + 10; y++) {
                if (!WorldGen.InWorld(tileX, y)) break;
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    float groundY = y * 16f - Height;
                    if (Position.Y < groundY) {
                        Position.Y = MathHelper.Lerp(Position.Y, groundY, 0.3f);
                        OnGround = Math.Abs(Position.Y - groundY) < 2f;
                    }
                    else {
                        Position.Y = groundY;
                        OnGround = true;
                    }
                    return;
                }
            }

            OnGround = false;
            Position.Y += 4f;
        }

        /// <summary>
        /// 检测角色前方是否有实心墙壁阻挡，扫描范围覆盖完整身体高度（头到脚）。
        /// 修正：原逻辑只检测脚部往上3行，导致头部碰撞遗漏、角色穿入上半部分墙壁。
        /// </summary>
        internal bool IsWallAhead(int direction) {
            int checkX = (int)((Center.X + direction * (Width * 0.5f + 8f)) / 16f);
            int footY = (int)((Position.Y + Height - 4f) / 16f);
            int headY = (int)((Position.Y + 4f) / 16f); // 从脚扫到头，覆盖全身

            for (int y = footY; y >= headY; y--) {
                if (!WorldGen.InWorld(checkX, y)) continue;
                Tile tile = Framing.GetTileSafely(checkX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检测角色身体中心列是否当前已陷入实心方块中（帧间穿越后的紧急检测）。
        /// 不做严格碰撞，仅作为寻路兜底：一旦发现陷入则立即触发飞行逃逸。
        /// </summary>
        internal bool IsEmbeddedInSolid() {
            int tileX = (int)(Center.X / 16f);
            int headTileY = (int)((Position.Y + 4f) / 16f);
            int footTileY = (int)((Position.Y + Height - 4f) / 16f);

            for (int y = headTileY; y <= footTileY; y++) {
                if (!WorldGen.InWorld(tileX, y)) continue;
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检测角色前方脚下是否存在深坑 (超过6格没有地面)
        /// </summary>
        internal bool IsGapAhead(int direction) {
            int checkX = (int)((Center.X + direction * (Width * 0.5f + 16f)) / 16f);
            int footY = (int)((Position.Y + Height) / 16f);

            for (int y = footY; y < footY + 6; y++) {
                if (!WorldGen.InWorld(checkX, y)) return true;
                Tile tile = Framing.GetTileSafely(checkX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 应用重力和速度到位置，用于非吸附式的物理运动
        /// </summary>
        internal void ApplyGravity(float gravity = 0.4f, float maxFallSpeed = 12f) {
            Velocity.Y = Math.Min(Velocity.Y + gravity, maxFallSpeed);
            Position += Velocity;
        }

        internal static Vector2 FindGroundPosition(Vector2 startPos) {
            int tileX = (int)(startPos.X / 16f);
            int startTileY = (int)(startPos.Y / 16f);

            for (int y = startTileY - 50; y < startTileY + 100; y++) {
                if (!WorldGen.InWorld(tileX, y)) continue;
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                    return new Vector2(startPos.X, y * 16f - 50f);
                }
            }

            return startPos;
        }

        #endregion

        #region 绘制

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (CurrentState == null) return false;

            SpriteEffects fx = WalkDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float alpha = DescendAlpha < 1f && CurrentState is ApolliaDescendingState ? DescendAlpha : 1f;
            Color bodyColor = Lighting.GetColor((int)(Center.X / 16), (int)(Center.Y / 16)) * alpha;
            Vector2 drawPos = new Vector2(Center.X, Position.Y + Height) - Main.screenPosition;

            //选择纹理：飞行/跳跃时使用单帧Jump纹理，否则使用行走帧图
            if (UseJumpTexture && ADVAsset.ApolliaActor_Jump != null) {
                Texture2D jumpTex = ADVAsset.ApolliaActor_Jump;
                Vector2 jumpOrigin = new Vector2(jumpTex.Width * 0.5f, jumpTex.Height);

                //尾焰Trail（着色器绘制，在最后面）
                if (JetTrailActive) {
                    DrawJetFlameTrail(spriteBatch);
                }

                //尾焰粒子
                DrawJetParticles(spriteBatch);

                //辉光
                DrawGlow(spriteBatch, drawPos, alpha);

                spriteBatch.Draw(jumpTex, drawPos, null, bodyColor, 0f, jumpOrigin, 0.7f, fx, 0f);
                return false;
            }

            if (ADVAsset.ApolliaActor == null) return false;

            Texture2D tex = ADVAsset.ApolliaActor;
            if (FrameWidth <= 0 || FrameHeight <= 0) return false;

            int clampedFrame = Math.Clamp(FrameIndex, 0, TotalFrames - 1);
            Rectangle sourceRect = new Rectangle(0, clampedFrame * FrameHeight, FrameWidth, FrameHeight);
            Vector2 origin = new Vector2(FrameWidth * 0.5f, FrameHeight);

            //辉光效果
            DrawGlow(spriteBatch, drawPos, alpha);

            //降落阶段电弧边缘光
            if (CurrentState is ApolliaDescendingState && DescendAlpha > 0.3f) {
                Color edgeColor = new Color(100, 200, 255) * (DescendAlpha * 0.4f);
                for (int i = 0; i < 4; i++) {
                    float angle = MathHelper.PiOver2 * i;
                    Vector2 off = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 2f;
                    spriteBatch.Draw(tex, drawPos + off, sourceRect, edgeColor, 0f, origin, 0.7f, fx, 0f);
                }
            }

            //主体
            spriteBatch.Draw(tex, drawPos, sourceRect, bodyColor, 0f, origin, 0.7f, fx, 0f);

            return false;
        }

        private void DrawGlow(SpriteBatch spriteBatch, Vector2 drawPos, float alpha) {
            if (GlowIntensity > 0.02f && CWRAsset.SoftGlow != null && !CWRAsset.SoftGlow.IsDisposed) {
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Color glowColor = new Color(80, 160, 255) * (GlowIntensity * alpha);
                float glowScale = 80f / (glow.Width * 0.5f);
                spriteBatch.Draw(glow, drawPos - new Vector2(0, FrameHeight * 0.4f), null,
                    glowColor with { A = 0 }, 0f, glow.Size() * 0.5f, glowScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawJetParticles(SpriteBatch spriteBatch) {
            if (jetParticles.Count == 0) return;
            if (CWRAsset.SoftGlow == null || CWRAsset.SoftGlow.IsDisposed) return;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 glowOrigin = glow.Size() * 0.5f;

            foreach (var p in jetParticles) {
                float lifeRatio = (float)p.Life / p.MaxLife;
                float fadeAlpha = p.Alpha * (1f - lifeRatio * lifeRatio);
                float scale = p.Scale * (1f + lifeRatio * 0.3f) * 0.03f;
                Vector2 screenPos = p.Position - Main.screenPosition;

                Color c = p.Color * fadeAlpha;
                spriteBatch.Draw(glow, screenPos, null, c with { A = 0 }, 0f, glowOrigin, scale, SpriteEffects.None, 0f);

                //内层白热核心
                Color core = Color.White * (fadeAlpha * 0.4f * (1f - lifeRatio));
                spriteBatch.Draw(glow, screenPos, null, core with { A = 0 }, 0f, glowOrigin, scale * 0.4f, SpriteEffects.None, 0f);
            }
        }

        #endregion

        private class JetTrailParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public float Alpha;
            public float Scale;
            public int Life;
            public int MaxLife;
        }
    }
}
