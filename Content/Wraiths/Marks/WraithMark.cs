using System;

namespace CalamityOverhaul.Content.Wraiths.Marks
{
    /// <summary>
    /// 灵异印记：一只鬼在猎物身上留下的痕，供同场其他鬼读取。<br/>
    /// 这是"灵异叠加"的唯一介质——鬼不直接认识彼此，只认得对方留下的印
    /// </summary>
    [Flags]
    internal enum WraithMark : byte
    {
        None = 0,
        /// <summary>湿——鬼雨淋着</summary>
        Soaked = 1 << 0,
        /// <summary>攥——焦黑枯手压着</summary>
        Gripped = 1 << 1,
        /// <summary>断——无头鬼影穿过</summary>
        Severed = 1 << 2,
        /// <summary>照——提灯童子的灯照见</summary>
        Lit = 1 << 3,
        /// <summary>缚——绯嫁的喜堂圈进来了</summary>
        Betrothed = 1 << 4,

        All = Soaked | Gripped | Severed | Lit | Betrothed,
    }

    internal static class WraithMarkExtensions
    {
        /// <summary>印记位序，用于索引每印一份的槽</summary>
        internal const int Count = 5;

        internal static int Index(this WraithMark mark) => mark switch {
            WraithMark.Soaked => 0,
            WraithMark.Gripped => 1,
            WraithMark.Severed => 2,
            WraithMark.Lit => 3,
            WraithMark.Betrothed => 4,
            _ => -1,
        };

        internal static WraithMark FromIndex(int index) => index switch {
            0 => WraithMark.Soaked,
            1 => WraithMark.Gripped,
            2 => WraithMark.Severed,
            3 => WraithMark.Lit,
            4 => WraithMark.Betrothed,
            _ => WraithMark.None,
        };
    }
}
