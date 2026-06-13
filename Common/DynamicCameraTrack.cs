using InnoVault.Cinematics;
using System;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 每帧执行自定义运镜逻辑的演出轨道。
    /// <para>InnoVault 内置的 <see cref="CameraFocusTrack"/>/<see cref="CameraZoomTrack"/> 走固定编排（from→to 插值），
    /// 而本轨道把每帧的镜头推导完全交给回调，用于"焦点/缩放/震动随运行时状态动态变化、且时长不固定"的过场
    /// （角色跟随、动态缩放、玩家状态驱动的死亡演出等）。回调内通过
    /// <see cref="CutsceneContext"/> 调用 <c>SetCameraFocus</c>/<c>SetCameraZoom</c>/<c>RequestInputLock</c>/<c>Shake</c>。</para>
    /// </summary>
    internal sealed class DynamicCameraTrack : CutsceneTrack
    {
        private readonly Action<CutsceneContext> perFrame;

        /// <param name="startTick">轨道开始帧</param>
        /// <param name="duration">轨道持续帧数（不定长演出可给一个足够大的上限，由外部 Stop 收尾）</param>
        /// <param name="perFrame">每帧的运镜推导回调</param>
        public DynamicCameraTrack(int startTick, int duration, Action<CutsceneContext> perFrame)
            : base(startTick, duration) {
            this.perFrame = perFrame ?? throw new ArgumentNullException(nameof(perFrame));
        }

        protected override void Update(CutsceneContext context, float progress) => perFrame(context);
    }
}
