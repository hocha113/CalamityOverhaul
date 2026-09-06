using Terraria.Audio;

namespace CalamityOverhaul.Content.Wraiths.Deaths
{
    /// <summary>
    /// 处决音的三层结构：低频体感 / 材质本征 / 尖端细节，同帧齐发。<br/>
    /// <b>硬规</b>：六只鬼的低频层必须互不相同。此前四只（鬼影、枯手、绯嫁、替死）共用
    /// <c>DD2_MonkStaffGroundImpact</c> 当低频层，是「六只处决一个味」的直接原因。<br/>
    /// 自有音效放进 <c>Assets/Sounds/Wraiths/</c> 即自动接管，缺席时退回本层声明的原版音，
    /// 因此缺资源不阻塞开发，也不构成继续共用同一条低频的理由。
    /// </summary>
    internal readonly struct SeizureCue
    {
        private const string CustomRoot = CWRConstant.Asset + "Sounds/Wraiths/";

        private readonly SoundStyle low;
        private readonly SoundStyle mid;
        private readonly SoundStyle high;

        internal SeizureCue(SoundStyle low, SoundStyle mid = default, SoundStyle high = default) {
            this.low = low;
            this.mid = mid;
            this.high = high;
        }

        /// <summary>自有音效槽：<paramref name="clip"/> 存在即用它，否则退回原版层。</summary>
        internal static SoundStyle Custom(string clip, SoundStyle fallback)
            => (CustomRoot + clip).GetSound(fallback);

        internal void Play(Vector2 at) {
            PlayLayer(low, at);
            PlayLayer(mid, at);
            PlayLayer(high, at);
        }

        private static void PlayLayer(SoundStyle style, Vector2 at) {
            if (style == default) {
                return;
            }
            SoundEngine.PlaySound(style, at);
        }
    }
}
