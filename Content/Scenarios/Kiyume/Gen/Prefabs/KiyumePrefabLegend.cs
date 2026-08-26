using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.Gen.Prefabs
{
    //字符画图例：几何字符机器级固定，语义槽字符映射放置委托
    //几何字符表（沿 OldNet 惯例 + Kiyume 扩展三字符）：
    //  ' '=透明跳过（不碰该格）  '#'=实心砖  '.'=空+室内墙  '-'=平台
    //  '1'~'4'=斜切砖（直映SlopeType枚举值）  'D'=门洞（空+墙，语义上是开口标记）
    //  '~'=水（空+墙+灌水）  '|'=绳（空+墙+垂绳）  'w'=空+围栏墙（柜门/院界，墙种固定）
    //镜像 OldNetPrefabLegend，不引用
    internal sealed class KiyumePrefabSlotDef
    {
        internal char Ch;
        internal string Name;
        /// <summary>放置委托：(x,y)→是否成功；失败由prefab记日志跳过，绝不强写</summary>
        internal Func<int, int, bool> Place;
    }

    internal sealed class KiyumePrefabLegend
    {
        internal readonly Dictionary<char, KiyumePrefabSlotDef> Slots = [];

        internal KiyumePrefabLegend Add(KiyumePrefabSlotDef def) {
            Slots.Add(def.Ch, def);
            return this;
        }

        internal static bool IsGeometryChar(char c)
            => c is ' ' or '#' or '.' or '-' or 'D' or '~' or '|' or 'w' or (>= '1' and <= '4');
    }
}
