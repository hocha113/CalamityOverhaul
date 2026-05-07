using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.ADV.Scenarios.VoidColonys.GlitchWraith
{
    /// <summary>
    /// 鬼乱码：仅在过去视角下可见的致命威胁
    /// 无视地形碰撞，持续向最近玩家缓慢蠕动并偶发乱码瞬移
    /// 骇客时间激活期间会被暂停，可被玩家作为灵异目标骇入
    /// </summary>
    internal class GlitchWraithActor : Actor, IHackTarget
    {
        //缓慢的基础爬行速度，像素每帧
        private const float CreepSpeed = 1.4f;
        //乱码瞬移参数，随机间隔触发一次短距离闪移
        private const int TeleportIntervalMin = 150;
        private const int TeleportIntervalMax = 300;
        private const float TeleportDistanceMin = 40f;
        private const float TeleportDistanceMax = 120f;
        //瞬移后的乱码闪烁余辉帧数
        private const int TeleportFlashFrames = 16;

        //触碰判定可见度阈值
        private const float ContactVisibility = 0.6f;
        //过去视角淡入速度与现在视角淡出速度
        private const float FadeInStep = 0.025f;
        private const float FadeOutStep = 0.08f;

        //失真影响的距离范围，越近失真越强
        private const float DistortionMaxDistance = 600f;
        private const float DistortionMinDistance = 100f;

        /// <summary>当前可见度0到1，过去视角下逼近1</summary>
        private float visibility;
        /// <summary>瞬移冷却帧数</summary>
        private int teleportTimer;
        /// <summary>瞬移后余辉闪烁帧数</summary>
        private int teleportFlash;
        /// <summary>头部乱码每帧滚动的随机种子</summary>
        private int glitchSeed;
        /// <summary>目标玩家whoAmI</summary>
        private int targetPlayer = -1;
        /// <summary>是否已触发处决演出，避免重复</summary>
        private bool hasExecuted;

        //骇客时间相关状态
        /// <summary>死机状态剩余帧数，大于0时完全冻结</summary>
        private int stunFrames;
        /// <summary>虚假记忆剩余帧数，大于0时无法锁定玩家</summary>
        private int falseMemoryFrames;
        /// <summary>自我肢解演出剩余帧数，大于0时正在自毁</summary>
        private int dismemberFrames;
        /// <summary>自我肢解演出总时长</summary>
        private const int DismemberDuration = 120;

        /// <summary>当前是否处于死机状态</summary>
        public bool IsStunned => stunFrames > 0;
        /// <summary>当前是否处于虚假记忆状态</summary>
        public bool IsMemoryAltered => falseMemoryFrames > 0;
        /// <summary>当前是否正在自我肢解</summary>
        public bool IsDismembering => dismemberFrames > 0;
        /// <summary>当前可见度，0到1</summary>
        public float Visibility => visibility;

        public new Vector2 Center {
            get => Position + new Vector2(Width * 0.5f, Height * 0.5f);
            set => Position = value - new Vector2(Width * 0.5f, Height * 0.5f);
        }

        public override void OnSpawn(params object[] args) {
            Width = 204;
            Height = 504;
            DrawLayer = ActorDrawLayer.AfterTiles;
            DrawExtendMode = 600;
            visibility = 0f;
            teleportTimer = Main.rand.Next(TeleportIntervalMin, TeleportIntervalMax);
            glitchSeed = Main.rand.Next(int.MaxValue);
        }

        public override void AI() {
            if (hasExecuted) {
                Velocity = Vector2.Zero;
                return;
            }

            //自我肢解演出优先，演出期间完全停下并逐步消亡
            if (dismemberFrames > 0) {
                Velocity = Vector2.Zero;
                UpdateDismember();
                return;
            }

            //骇客时间冻结：与NPC同步被暂停，保留可见度与乱码视觉循环
            if (HackTimeFreeze.IsActive) {
                Velocity = Vector2.Zero;
                bool inPastHack = false;
                float targetVisHack = inPastHack ? 1f : 0f;
                if (visibility < targetVisHack) visibility = MathF.Min(targetVisHack, visibility + FadeInStep);
                else if (visibility > targetVisHack) visibility = MathF.Max(targetVisHack, visibility - FadeOutStep);
                glitchSeed = unchecked(glitchSeed * 1664525 + 1013904223);
                return;
            }

            //死机状态：完全冻结，其他计时推进照常
            if (stunFrames > 0) {
                stunFrames--;
                Velocity = Vector2.Zero;
                //死机期间仍维持可见度变化，便于玩家观察
                bool inPastStun = false;
                float targetVisStun = inPastStun ? 1f : 0f;
                if (visibility < targetVisStun) visibility = MathF.Min(targetVisStun, visibility + FadeInStep);
                else if (visibility > targetVisStun) visibility = MathF.Max(targetVisStun, visibility - FadeOutStep);
                glitchSeed = unchecked(glitchSeed * 1664525 + 1013904223);
                return;
            }

            if (falseMemoryFrames > 0) falseMemoryFrames--;

            Player target = AcquireTarget();
            if (target == null) {
                visibility = MathF.Max(0f, visibility - FadeOutStep);
                Velocity = Vector2.Zero;
                return;
            }

            bool inPast = false;

            //可见度根据时代平滑变化
            float targetVis = inPast ? 1f : 0f;
            if (visibility < targetVis) {
                visibility = MathF.Min(targetVis, visibility + FadeInStep);
            }
            else if (visibility > targetVis) {
                visibility = MathF.Max(targetVis, visibility - FadeOutStep);
            }

            Vector2 toTarget = target.Center - Center;
            float dist = toTarget.Length();
            if (dist <= 1f) {
                Velocity = Vector2.Zero;
            }
            else {
                Vector2 dir = toTarget / dist;
                //虚假记忆期间反向游离，失去追击意图
                if (falseMemoryFrames > 0) dir = -dir;
                //始终缓慢蠕动，速度不随时代改变
                Velocity = dir * MathF.Min(CreepSpeed, dist);

                //乱码瞬移倒计时推进，触发时一次性跳跃一小段距离
                teleportTimer--;
                if (teleportTimer <= 0) {
                    teleportTimer = Main.rand.Next(TeleportIntervalMin, TeleportIntervalMax);
                    float teleDist = Main.rand.NextFloat(TeleportDistanceMin, TeleportDistanceMax);
                    teleDist = MathF.Min(teleDist, MathF.Max(0f, dist - 80f));
                    if (teleDist > 0f) {
                        Position += dir * teleDist;
                        teleportFlash = TeleportFlashFrames;
                        //瞬移同帧重置种子，让余辉闪烁更离散
                        glitchSeed = Main.rand.Next(int.MaxValue);
                    }
                }
            }

            if (teleportFlash > 0) teleportFlash--;

            glitchSeed = unchecked(glitchSeed * 1664525 + 1013904223);

            //过去视角下与玩家碰撞触发处决
            if (visibility > ContactVisibility && !target.dead && !target.immune) {
                Rectangle myBox = new((int)Position.X, (int)Position.Y, Width, Height);
                if (myBox.Intersects(target.Hitbox)) {
                    ExecutePlayer(target);
                }
            }
        }

        /// <summary>
        /// 推进自我肢解演出，结束时清除actor
        /// </summary>
        private void UpdateDismember() {
            dismemberFrames--;
            //演出期间加速乱码滚动，视觉上持续裂解
            glitchSeed = unchecked(glitchSeed * 1664525 + 1013904223);
            //可见度线性衰减至0
            float t = dismemberFrames / (float)DismemberDuration;
            visibility = MathHelper.Clamp(t, 0f, 1f);
            if (dismemberFrames <= 0) {
                ActorLoader.KillActor(WhoAmI);
            }
        }

        /// <summary>
        /// 对乱码鬼施加死机效果
        /// </summary>
        public void ApplySystemHalt(int frames) {
            if (dismemberFrames > 0) return;
            stunFrames = Math.Max(stunFrames, frames);
            teleportFlash = TeleportFlashFrames;
            glitchSeed = Main.rand.Next(int.MaxValue);
        }

        /// <summary>
        /// 对乱码鬼施加虚假记忆效果，期间失去追击意图
        /// </summary>
        public void ApplyFalseMemory(int frames) {
            if (dismemberFrames > 0) return;
            falseMemoryFrames = Math.Max(falseMemoryFrames, frames);
            //记忆修改会让其当前锁定目标松脱
            targetPlayer = -1;
            teleportTimer = Main.rand.Next(TeleportIntervalMin, TeleportIntervalMax);
        }

        /// <summary>
        /// 触发自我肢解，短暂演出后从场景中移除
        /// </summary>
        public void ApplySelfDismember() {
            if (dismemberFrames > 0) return;
            dismemberFrames = DismemberDuration;
            stunFrames = 0;
            falseMemoryFrames = 0;
            teleportFlash = TeleportFlashFrames;
        }

        /// <summary>
        /// 选择最近的活着玩家作为目标
        /// </summary>
        private Player AcquireTarget() {
            if (targetPlayer >= 0 && targetPlayer < Main.maxPlayers) {
                Player p = Main.player[targetPlayer];
                if (p.active && !p.dead) return p;
            }
            Player best = null;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;
                float d = (p.Center - Center).LengthSquared();
                if (d < bestDistSq) {
                    bestDistSq = d;
                    best = p;
                    targetPlayer = i;
                }
            }
            return best;
        }

        /// <summary>
        /// 触发处决演出
        /// </summary>
        private void ExecutePlayer(Player player) {
            if (hasExecuted) return;
            hasExecuted = true;
            GlitchWraithDeathSequence.Trigger(player);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (visibility <= 0.01f) return false;
            Texture2D tex = ADVAsset.Glitchwraith;
            if (tex == null) return false;

            Vector2 drawPos = Center - Main.screenPosition;
            Vector2 origin = new(tex.Width * 0.5f, tex.Height * 0.5f);

            //自我肢解演出：越靠近结束抖动越剧烈，分层撕裂残影向外飞散
            if (dismemberFrames > 0) {
                float t = 1f - dismemberFrames / (float)DismemberDuration;
                float shake = t * 24f;
                Vector2 shakeOffset = new(
                    (MathF.Sin(glitchSeed * 0.0001f) * 2f - 1f) * shake,
                    (MathF.Cos(glitchSeed * 0.00013f) * 2f - 1f) * shake);
                drawPos += shakeOffset;
                for (int i = 0; i < 4; i++) {
                    float ang = i * MathHelper.PiOver2 + t * MathHelper.TwoPi;
                    Vector2 limbOffset = ang.ToRotationVector2() * (t * 80f);
                    Color limbColor = new Color(255, 40, 80) * (visibility * 0.4f * (1f - t));
                    spriteBatch.Draw(tex, drawPos + limbOffset, null, limbColor,
                        ang * 0.3f, origin, 1f + t * 0.1f, SpriteEffects.None, 0f);
                }
            }

            //瞬移后余辉闪烁，分两层红紫残影左右抖动位移
            if (teleportFlash > 0) {
                float flashT = teleportFlash / (float)TeleportFlashFrames;
                float off = flashT * 10f;
                Color redGhost = new Color(255, 40, 80) * (visibility * 0.55f * flashT);
                Color purpleGhost = new Color(180, 80, 255) * (visibility * 0.55f * flashT);
                spriteBatch.Draw(tex, drawPos + new Vector2(-off, 0f), null, redGhost, 0f, origin, 1f, SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, drawPos + new Vector2(off, 0f), null, purpleGhost, 0f, origin, 1f, SpriteEffects.None, 0f);
            }

            //骇入滤镜：被骇客时间悬停或选中时通过灵异专用着色器渲染本体
            bool isSelected = ReferenceEquals(HackTime.CurrentScanTarget, this);
            bool isHovered = ReferenceEquals(HackTimeTargeting.HoveredWraith, this);
            float hackMark = (isSelected ? 1f : isHovered ? 0.55f : 0f) * HackTime.Intensity;
            if (hackMark > 0.01f) {
                DrawBodyWithHackShader(spriteBatch, tex, drawPos, origin, hackMark, isSelected);
            }
            else {
                //未进入骇客时间高亮时按常规绘制
                Color baseColor = Color.White * visibility;
                spriteBatch.Draw(tex, drawPos, null, baseColor, 0f, origin, 1f, SpriteEffects.None, 0f);
            }

            //头部球形乱码：使用着色器生成团状有机乱码斑块
            DrawGlitchHead(spriteBatch, drawPos, origin, tex.Width);

            return false;
        }

        /// <summary>
        /// 使用HackWraithHighlight着色器绘制本体
        /// 切到Immediate模式绑定着色器，由着色器内部完成撕裂、血雾、紫红色差、心跳脉冲等全部高亮效果
        /// 结束后恢复默认批次配置
        /// </summary>
        private void DrawBodyWithHackShader(SpriteBatch sb, Texture2D tex, Vector2 drawPos,
            Vector2 origin, float strength, bool selected) {
            Effect shader = HackTimeAssets.HackWraithHighlight;
            if (shader == null) {
                //着色器未加载时回落到普通绘制，确保始终可见
                Color fallback = Color.White * visibility;
                sb.Draw(tex, drawPos, null, fallback, 0f, origin, 1f, SpriteEffects.None, 0f);
                return;
            }

            //着色器参数：纹素大小、强度、选中标志、动画时间
            shader.Parameters["texelSize"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            shader.Parameters["intensity"]?.SetValue(strength);
            shader.Parameters["isSelected"]?.SetValue(selected ? 1f : 0f);
            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);

            //切到Immediate并绑定像素着色器
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, shader, Main.GameViewMatrix.TransformationMatrix);

            //顶点色携带visibility作为透明度，着色器里的smpColor即此值
            Color bodyColor = Color.White * visibility;
            sb.Draw(tex, drawPos, null, bodyColor, 0f, origin, 1f, SpriteEffects.None, 0f);

            //恢复默认批次配置
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 使用GlitchHead着色器绘制头顶球形乱码区域
        /// 中心位于纹理头部附近，覆盖区域为边长约纹理宽度1.6倍的正方形
        /// </summary>
        private void DrawGlitchHead(SpriteBatch sb, Vector2 drawPos, Vector2 origin, int texW) {
            Effect shader = EffectLoader.GlitchHead?.Value;
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            if (shader == null || pixel == null) return;

            //头部乱码正方形尺寸与锚点：覆盖纹理顶部区域并向上溢出
            int size = (int)(texW * 1.6f);
            Vector2 topCenter = new(drawPos.X, drawPos.Y - origin.Y + texW * 0.08f);
            Vector2 quadTopLeft = topCenter - new Vector2(size * 0.5f, size * 0.6f);
            Rectangle dest = new((int)quadTopLeft.X, (int)quadTopLeft.Y, size, size);

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["intensity"]?.SetValue(visibility);
            //块尺寸0.025相当于每个乱码块占40分之一UV，颗粒感明显
            shader.Parameters["pixelSize"]?.SetValue(0.025f);

            //结束当前批次，切换到Immediate以便绑定着色器
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, shader, Main.GameViewMatrix.TransformationMatrix);

            sb.Draw(pixel, dest, Color.White);

            //恢复默认批次配置，保证后续Actor正常绘制
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 以本地玩家视角返回当前场景中最近鬼乱码带来的失真强度0到1
        /// 距离越近值越大，与时代视角无关，因为鬼即使在现在不可见也会持续靠近
        /// 过去视角下会做少量加成以强化氛围
        /// </summary>
        public static float GetLocalDistortionStrength() {
            List<GlitchWraithActor> list = ActorLoader.GetActiveActors<GlitchWraithActor>();
            if (list == null || list.Count == 0) return 0f;
            Player p = Main.LocalPlayer;
            if (p == null || !p.active) return 0f;

            float bestDistSq = float.MaxValue;
            float bestVis = 0f;
            foreach (GlitchWraithActor w in list) {
                float d = (w.Center - p.Center).LengthSquared();
                if (d < bestDistSq) {
                    bestDistSq = d;
                    bestVis = w.visibility;
                }
            }

            float dist = MathF.Sqrt(bestDistSq);
            float range = DistortionMaxDistance - DistortionMinDistance;
            //基础强度只取决于距离，现在视角下依然生效
            float t = 1f - MathHelper.Clamp((dist - DistortionMinDistance) / range, 0f, 1f);
            //过去视角下额外叠加最多30%强度，作为视觉氛围加成
            float eraBoost = 1f + 0.3f * bestVis;
            return MathHelper.Clamp(t * eraBoost, 0f, 1f);
        }

        #region IHackTarget 实现

        Vector2 IScannable.WorldCenter => Center;

        bool IScannable.IsValid => Active && !hasExecuted && dismemberFrames <= 0;

        bool IScannable.IsHackable => true;

        int IScannable.ScanRowCount => 6;

        HackTargetType IHackTarget.TargetType
            => HackTargetType.Get<HackTimes.Targets.WraithTargetType>();

        Vector2 IHackTarget.LockFrameHalfSize => new(
            Math.Max(Width, 32) * 0.6f + 30f,
            Math.Max(Height, 32) * 0.6f + 30f);

        string IHackTarget.LockFrameTitle => HackTime.WraithScanNameValue.Value;

        bool IHackTarget.TryGetLockFrameStatus(out string text, out Color color) {
            text = HackTime.WraithScanIntegrityValue.Value;
            color = HackTheme.Contagion;
            return true;
        }

        bool IHackTarget.ApplyHack(QuickHackDef hack, Player caster) => hack.OnApply(this, caster);

        bool IHackTarget.TargetEquals(IHackTarget other) => ReferenceEquals(this, other);

        void IScannable.BuildScanData(string[] labels, string[] values, Color[] colors) {
            //NAME
            labels[0] = HackTime.WraithScanName.Value;
            values[0] = HackTime.WraithScanNameValue.Value;
            colors[0] = HackTheme.Danger;

            //TYPE
            labels[1] = HackTime.TypeLabel.Value;
            values[1] = HackTime.WraithScanType.Value;
            colors[1] = HackTheme.Danger;

            //THREAT
            labels[2] = HackTime.ThreatLabel.Value;
            values[2] = HackTime.WraithScanThreat.Value;
            colors[2] = HackTheme.Danger;

            //STATUS
            labels[3] = HackTime.WraithScanStatus.Value;
            if (dismemberFrames > 0) {
                values[3] = HackTime.WraithScanStatusDismember.Value;
                colors[3] = HackTheme.Danger;
            }
            else if (stunFrames > 0) {
                values[3] = HackTime.WraithScanStatusHalt.Value;
                colors[3] = HackTheme.Uploading;
            }
            else if (falseMemoryFrames > 0) {
                values[3] = HackTime.WraithScanStatusMemory.Value;
                colors[3] = HackTheme.AccentAlt;
            }
            else {
                values[3] = HackTime.WraithScanStatusStalking.Value;
                colors[3] = HackTheme.Danger;
            }

            //DATA（数据完整性）
            labels[4] = HackTime.WraithScanIntegrity.Value;
            values[4] = HackTime.WraithScanIntegrityValue.Value;
            colors[4] = HackTheme.Danger;

            //ORIGIN（来源）
            labels[5] = HackTime.WraithScanOrigin.Value;
            values[5] = HackTime.WraithScanOriginValue.Value;
            colors[5] = HackTheme.TextDim;
        }

        #endregion
    }
}
