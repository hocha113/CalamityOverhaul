using InnoVault.Cinematics;
using System;

namespace CalamityOverhaul.Common
{
    /// <summary>
    /// 每帧回调驱动运镜的演出轨道
    /// <br/>相对 <see cref="CameraFocusTrack"/>/<see cref="CameraZoomTrack"/> 固定插值，本轨道交回调处理焦点/缩放/震动随运行时变化的不定长过场
    /// <br/>回调经 <see cref="CutsceneContext"/> 调 <c>SetCameraFocus</c>/<c>SetCameraZoom</c>/<c>RequestInputLock</c>/<c>Shake</c>
    /// </summary>
    internal sealed class DynamicCameraTrack : CutsceneTrack
    {
        private readonly Action<CutsceneContext> perFrame;

        /// <param name="startTick">开始 tick</param>
        /// <param name="duration">持续 tick，不定长可给大上限由外部 Stop</param>
        /// <param name="perFrame">每帧运镜回调</param>
        public DynamicCameraTrack(int startTick, int duration, Action<CutsceneContext> perFrame)
            : base(startTick, duration) {
            this.perFrame = perFrame ?? throw new ArgumentNullException(nameof(perFrame));
        }

        protected override void Update(CutsceneContext context, float progress) => perFrame(context);
    }
}
