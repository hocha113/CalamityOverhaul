using CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors.States;
using InnoVault.Cinematics;
using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.ApolliaActors
{
    /// <summary>
    /// 可复用的演出运镜系统
    /// 通过 <see cref="ApolliaPlayer.ModifyScreenPosition"/> 驱动屏幕位置和缩放，
    /// 不在AI帧中直接修改 Main.screenPosition，避免与引擎的摄像机流程冲突。
    /// 运镜参数由 <see cref="UpdateFocus"/> 根据Actor当前状态自动推导，状态类无需感知Camera
    /// </summary>
    internal class CutsceneCamera
    {
        /// <summary>期望摄像机聚焦的世界坐标</summary>
        public Vector2 FocusTarget;

        /// <summary>摄像机位置插值速度 (0~1)</summary>
        public float PositionLerpSpeed = 0.03f;

        /// <summary>目标缩放倍率</summary>
        public float TargetZoom = 1f;

        /// <summary>缩放插值速度 (0~1)</summary>
        public float ZoomLerpSpeed = 0.02f;

        /// <summary>是否在运镜期间锁定玩家操作</summary>
        public bool LockPlayerControls = true;

        private ApolliaActor owner;
        private bool manualActive;

        /// <summary>运镜是否处于激活状态</summary>
        public bool Active => manualActive || owner != null
            && CutsceneDirector.CurrentClip is ApolliaCameraClip
            && ReferenceEquals(CutsceneDirector.CurrentContext?.Tag, owner);

        /// <summary>
        /// 绑定所属 Actor。因为 Camera 由 Actor 字段初始化，不能直接在构造函数里传入 this。
        /// </summary>
        public void Bind(ApolliaActor actor) {
            owner = actor;
        }

        /// <summary>
        /// 启动运镜
        /// </summary>
        public void Start(Vector2 initialFocus, float posLerp = 0.03f, float zoom = 1f, float zoomLerp = 0.02f) {
            if (VaultUtils.isServer) {
                return;
            }

            FocusTarget = initialFocus;
            PositionLerpSpeed = posLerp;
            TargetZoom = zoom;
            ZoomLerpSpeed = zoomLerp;

            if (owner == null) {
                CutsceneDirector.Stop();
                manualActive = true;
                CutsceneDirector.Camera.Begin(initialFocus);
                CutsceneDirector.Camera.SetZoom(zoom, zoomLerp);
                return;
            }

            if (CutsceneDirector.Play<ApolliaCameraClip>(Main.LocalPlayer, tag: owner)) {
                CutsceneDirector.Camera.SetFocus(FocusTarget, PositionLerpSpeed);
                CutsceneDirector.Camera.SetZoom(TargetZoom, ZoomLerpSpeed);
            }
        }

        /// <summary>
        /// 停止运镜并开始平滑恢复
        /// </summary>
        public void Stop() {
            if (manualActive) {
                manualActive = false;
                CutsceneDirector.Camera.End();
            }
            else if (Active) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>
        /// 强制立即重置到默认状态
        /// </summary>
        public void Reset() {
            if (manualActive) {
                manualActive = false;
                CutsceneDirector.Camera.Reset();
            }
            else if (Active) {
                CutsceneDirector.Reset();
            }
        }

        /// <summary>
        /// 触发屏幕震动——在运镜锁定期间替代原版 PunchCameraModifier
        /// </summary>
        /// <param name="direction">震动方向（会自动归一化），传入 Zero 则随机方向</param>
        /// <param name="intensity">初始偏移像素强度</param>
        /// <param name="decay">每帧衰减系数 (0~1)，越小衰减越快</param>
        /// <param name="duration">持续帧数</param>
        public void Shake(Vector2 direction, float intensity, float decay = 0.9f, int duration = 20) {
            if (Active || manualActive) {
                CutsceneDirector.Camera.Shake(direction, intensity, decay, duration);
            }
        }

        /// <summary>
        /// 根据Actor当前状态自动推导运镜参数——在 <see cref="Apply"/> 之前每帧调用。
        /// 运镜逻辑集中在此处，状态类完全不感知Camera
        /// </summary>
        public void UpdateFocus(ApolliaActor actor, Player player) { }

        /// <summary>
        /// 在 <see cref="ApolliaPlayer.ModifyScreenPosition"/> 中调用，
        /// 平滑地将屏幕位置和缩放过渡到目标值
        /// </summary>
        public void Apply() {
            if (!manualActive) {
                return;
            }

            CutsceneDirector.Camera.SetFocus(FocusTarget, PositionLerpSpeed);
            CutsceneDirector.Camera.SetZoom(TargetZoom, ZoomLerpSpeed);
            if (LockPlayerControls) {
                CutsceneDirector.Camera.RequestInputLock(CutsceneInputLockFlags.All);
                CutsceneDirector.Camera.ApplyInputLock(Main.LocalPlayer);
            }
            CutsceneDirector.Camera.ApplyScreenPosition();
        }
    }

    internal sealed class ApolliaCameraClip : CutsceneClip
    {
        private const int TimelineDuration = int.MaxValue - 2;

        public override int Priority => 10;

        public override bool CanPlay(Player player, object tag) => base.CanPlay(player, tag) && tag is ApolliaActor;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = TimelineDuration;
            timeline.Add(new ApolliaCameraTrack(0, TimelineDuration));
        }
    }

    internal sealed class ApolliaCameraTrack : CutsceneTrack
    {
        public ApolliaCameraTrack(int startTick, int duration) : base(startTick, duration) { }

        protected override void Update(CutsceneContext context, float progress) {
            if (!context.TryGetTag(out ApolliaActor actor) || !actor.Active) {
                CutsceneDirector.Stop();
                return;
            }

            Player player = context.Player;
            if (player == null || !player.active) {
                CutsceneDirector.Stop();
                return;
            }

            switch (actor.CurrentState) {
                case ApolliaDescendingState:
                    context.Camera.SetFocus(actor.Center, 0.03f);
                    context.Camera.SetZoom(1f, 0.02f);
                    break;

                case ApolliaWalkingState:
                    context.Camera.SetFocus((actor.Center + player.Center) * 0.5f, 0.025f);
                    context.Camera.SetZoom(GetWalkingZoom(actor, player), 0.015f);
                    break;

                case ApolliaArrivedState:
                    context.Camera.SetFocus((actor.Center + player.Center) * 0.5f + new Vector2(0, -20), 0.04f);
                    context.Camera.SetZoom(2f, 0.02f);
                    break;

                default:
                    context.Camera.SetFocus(actor.Center, 0.03f);
                    context.Camera.SetZoom(1f, 0.02f);
                    break;
            }

            if (actor.Camera.LockPlayerControls) {
                context.Camera.RequestInputLock(CutsceneInputLockFlags.All);
            }
        }

        private static float GetWalkingZoom(ApolliaActor actor, Player player) {
            float distX = Math.Abs(actor.Center.X - player.Center.X);
            float zoomFactor = MathHelper.Clamp(1f - (distX - 60f) / 400f, 0f, 1f);
            float eased = zoomFactor < 0.5f
                ? 2f * zoomFactor * zoomFactor
                : 1f - MathF.Pow(-2f * zoomFactor + 2f, 2f) / 2f;
            return MathHelper.Lerp(1f, 1.5f, eased);
        }
    }
}
