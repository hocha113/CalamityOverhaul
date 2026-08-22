using System;

namespace CalamityOverhaul.Content.Wraiths.Marks
{
    /// <summary>
    /// 灵异状态：一只鬼在猎物身上留下的痕，供同场其他鬼读取。<br/>
    /// 这是"灵异叠加"的唯一介质，鬼不直接认识彼此，只认得对方留下的状态。<br/>
    /// 状态只描述猎物"怎么了"（湿了/被攥住/开了口子），不描述"谁干的"；
    /// 谁干的记在印记槽的施加鬼 Key 上（见 <see cref="WraithMarkNPC"/>）
    /// </summary>
    [Flags]
    internal enum WraithMark : byte
    {
        None = 0,
        /// <summary>湿，正被鬼雨淋着</summary>
        Soaked = 1 << 0,
        /// <summary>攥，被枯手压着，动弹不得</summary>
        Gripped = 1 << 1,
        /// <summary>断，被利刃穿过，创口未合</summary>
        Severed = 1 << 2,
        /// <summary>照，被鬼灯照见，无所遁形</summary>
        Lit = 1 << 3,
        /// <summary>缚，被圈进喜堂，身上时间停住</summary>
        Betrothed = 1 << 4,

        All = Soaked | Gripped | Severed | Lit | Betrothed,
    }

    /// <summary>状态语义元数据：描述一种状态"是什么性质"，与哪只鬼发它无关。</summary>
    internal readonly struct WraithStateDef(WraithMark state, bool timelock)
    {
        public WraithMark State { get; } = state;
        /// <summary>滞：该状态在身时，宿主身上其余印记停止走表</summary>
        public bool Timelock { get; } = timelock;
    }

    internal static class WraithMarkExtensions
    {
        /// <summary>印记位序，用于索引每印一份的槽</summary>
        internal const int Count = 5;

        /// <summary>全部状态的语义表；新增状态在这里登记它的性质，位序与 <see cref="Index"/> 对齐</summary>
        internal static readonly WraithStateDef[] Defs = [
            new(WraithMark.Soaked, timelock: false),
            new(WraithMark.Gripped, timelock: false),
            new(WraithMark.Severed, timelock: false),
            new(WraithMark.Lit, timelock: false),
            new(WraithMark.Betrothed, timelock: true),
        ];

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

        /// <summary>该状态是否滞时（冻结宿主其余印记走表）。</summary>
        internal static bool IsTimelock(this WraithMark mark) {
            int index = mark.Index();
            return index >= 0 && Defs[index].Timelock;
        }
    }
}
