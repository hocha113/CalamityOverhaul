using CalamityOverhaul.Content.Narrative.Data;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Shenyo
{
    /// <summary>
    /// 沈幽初遇进度的本地读写口（镜像 <see cref="Himayo.HimayoStorySync"/>）。<br/>
    /// 全部按本地玩家取数——鬼雨世界是本地叠加层，进度也随玩家存档。
    /// </summary>
    internal static class ShenyoStorySync
    {
        public static ShenyoStoryData Story
            => Main.LocalPlayer.GetModPlayer<StoryPlayer>().Get<ShenyoStoryData>();

        /// <summary>初遇对话已触发过（用于统计口径，重播判定走 <see cref="PostFirstMetIsComplete"/>）</summary>
        public static bool FirstMet => Story.FirstMet;

        public static void MarkFirstMet() => Story.FirstMet = true;

        /// <summary>初遇播完，送出与发伞的门禁</summary>
        public static bool PostFirstMetIsComplete => Story.PostFirstMetIsComplete;

        public static void MarkPostFirstMetComplete() => Story.PostFirstMetIsComplete = true;

        /// <summary>本次抵达深层的方式：true=被鬼奴杀死拖入，false=夺伞下潜</summary>
        public static bool ArrivedByDeath {
            get => Story.ArrivedByDeath;
            set => Story.ArrivedByDeath = value;
        }

        /// <summary>鬼伞已发放，防重复</summary>
        public static bool KikasaGranted {
            get => Story.KikasaGranted;
            set => Story.KikasaGranted = value;
        }
    }
}
