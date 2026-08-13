using System.Collections.Generic;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Prefabs
{
    //字符画图例(§2.3):几何字符机器级固定,家具字符映射"语义槽"不映射tile+frame
    //几何字符表:
    //  ' '=透明跳过(不碰该格)  '#'=实心砖  '.'=空+室内墙  '-'=平台
    //  '1'~'4'=斜切砖(直映SlopeType枚举值,F24;垂直镜像对偶1↔3,2↔4)
    //  '_'=半砖(无垂直对偶,镜像按prefab声明换平台或删除)
    //  'D'=门插槽格(空+墙;贴边纵向3连=导出一个Door socket)

    //半砖的垂直镜像规则,逐prefab指定(§2.3镜像专论第2条)
    internal enum HalfBrickMirrorRule { ToPlatform, Drop }

    //语义槽定义:Furnish阶段统一走WorldGen.PlaceObject,锚定校验免费获得(§2.3/F9)
    internal sealed class PrefabSlotDef
    {
        internal char Ch;
        internal string Name;
        //MarkerOnly槽TileType=0:只登记坐标不落物(彩窗区/钟楼锚点这类留位)
        internal ushort TileType;
        internal int Style;
        //吊挂类(顶锚):镜像时经对偶表换槽(§2.3镜像专论第3条)
        internal bool TopAnchor;
        //吊挂物正下净空需求,PlaceObject前预检(§3.2-7)
        internal int ClearanceBelow;
        //垂直镜像对偶:'\0'=镜像时删除并记日志;自身=位置翻转后原样保留
        internal char MirrorCh;
        internal bool MarkerOnly;
    }

    //一套图例=几何字符(固定)+语义槽表+半砖镜像规则,prefab家族各持一套
    internal sealed class PrefabLegend
    {
        internal readonly Dictionary<char, PrefabSlotDef> Slots = [];
        internal HalfBrickMirrorRule HalfBrick = HalfBrickMirrorRule.ToPlatform;

        internal PrefabLegend Add(PrefabSlotDef def) {
            Slots.Add(def.Ch, def);
            return this;
        }

        internal static bool IsGeometryChar(char c)
            => c is ' ' or '#' or '.' or '-' or '_' or 'D' or (>= '1' and <= '4');
    }
}
