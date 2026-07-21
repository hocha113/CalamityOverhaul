using InnoVault.Cinematics;
using System;

namespace CalamityOverhaul.Common
{
    /// <summary>每帧回调驱动运镜的演出轨道</summary>
    internal sealed class DynamicCameraTrack : CutsceneTrack
    {
        private readonly Action<CutsceneContext> perFrame;

        /// <param name="startTick">开始 tick</param>
        /// <param name="duration">持续 tick，不定长可给大上限由外部 Stop</param>
        public DynamicCameraTrack(int startTick, int duration, Action<CutsceneContext> perFrame)
            : base(startTick, duration) {
            this.perFrame = perFrame ?? throw new ArgumentNullException(nameof(perFrame));
        }

        protected override void Update(CutsceneContext context, float progress) => perFrame(context);
    }
}
