using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.Wraiths.Deaths
{
    /// <summary>
    /// 夺身演出的具名拍表：用绝对帧声明，跨过即补触发一次。<br/>
    /// 取代 <c>Timer == X</c> 等值判定与 <c>PhaseProgress in [a,b)</c> 窗口。
    /// 前者在联机快照把 timer 从 122 直接修正到 126 时会整帧丢掉表现，
    /// 后者按帧落点可能触发零次也可能两次。
    /// </summary>
    internal sealed class SeizureBeatTable
    {
        private readonly List<(int Frame, Action Fire)> beats = [];
        private int next;
        private bool sealedTable;

        /// <summary>声明一拍。只允许在 <see cref="WraithDeathPerformance.BuildBeats"/> 里调用。</summary>
        internal void Add(int frame, Action fire) {
            if (fire == null || sealedTable) {
                return;
            }
            beats.Add((Math.Max(frame, 1), fire));
        }

        /// <summary>推进到 timer，把所有未触发且已到点的拍按帧序补齐。</summary>
        internal void Advance(int timer) {
            if (!sealedTable) {
                sealedTable = true;
                beats.Sort(static (a, b) => a.Frame.CompareTo(b.Frame));
            }
            while (next < beats.Count && beats[next].Frame <= timer) {
                beats[next].Fire();
                next++;
            }
        }
    }
}
