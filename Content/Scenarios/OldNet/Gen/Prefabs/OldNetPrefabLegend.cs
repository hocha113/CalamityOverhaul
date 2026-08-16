using System;
using System.Collections.Generic;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Prefabs
{
    //字符画图例：几何字符机器级固定，语义槽字符映射放置委托
    //几何字符表：
    //  ' '=透明跳过（不碰该格）  '#'=实心砖  '.'=空+室内墙  '-'=平台
    //  '1'~'4'=斜切砖（直映SlopeType枚举值）  'D'=门洞（空+墙，语义上是开口标记）
    //镜像 Dungeonworld PrefabLegend 的精简版：旧网自定义节点 tile 无 TileObjectData，
    //槽位放置走委托而非 PlaceObject（普通家具委托内自行调 PlaceObject 即可）
    internal sealed class OldNetPrefabSlotDef
    {
        internal char Ch;
        internal string Name;
        /// <summary>放置委托：(x,y)→是否成功；失败由prefab记日志跳过，绝不强写</summary>
        internal Func<int, int, bool> Place;
    }

    internal sealed class OldNetPrefabLegend
    {
        internal readonly Dictionary<char, OldNetPrefabSlotDef> Slots = [];

        internal OldNetPrefabLegend Add(OldNetPrefabSlotDef def) {
            Slots.Add(def.Ch, def);
            return this;
        }

        internal static bool IsGeometryChar(char c)
            => c is ' ' or '#' or '.' or '-' or 'D' or (>= '1' and <= '4');
    }
}
