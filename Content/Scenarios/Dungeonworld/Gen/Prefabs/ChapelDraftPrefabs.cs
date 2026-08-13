using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Gen.Prefabs
{
    //====================================================================
    //教堂内饰草案prefab(§2.4-①的缩小样张)
    //【草案,待用户美术方向签字后再量产】——本文件是切片候选,
    //家具样式全部蓝地牢系占位(对源核实的placeStyle),构图/配色未锁定
    //====================================================================
    //构图:阶梯收分穹顶(每4列收1行)+吊柱拱廊(下方3高通行)+右侧祭坛台
    //(2高台+1格台阶自动登F3)+彩窗留位W+钟楼锚留位B+左右各一Door socket
    internal static class ChapelDraftPrefabs
    {
        //语义槽图例:家具经PlaceObject落地(F9),对偶表按§2.3镜像专论第3条
        //吊挂物(L吊灯)↔落地物(c烛台)互为对偶;门板+自身对称(F4);
        //长椅b/祭坛A/钟锚B无倒吊对偶=镜像时删除;彩窗W区域随几何翻转保留
        private static PrefabLegend BuildLegend() => new PrefabLegend {
            HalfBrick = HalfBrickMirrorRule.ToPlatform
        }
            .Add(new PrefabSlotDef {
                Ch = 'A', Name = "祭坛(蓝地牢桌占位)",
                TileType = TileID.Tables, Style = 10, MirrorCh = '\0'
            })
            .Add(new PrefabSlotDef {
                Ch = 'b', Name = "长椅(蓝地牢沙发占位)",
                TileType = TileID.Benches, Style = 6, MirrorCh = '\0'
            })
            .Add(new PrefabSlotDef {
                Ch = 'c', Name = "烛台(蓝地牢烛台)",
                TileType = TileID.Candelabras, Style = 22, MirrorCh = 'L'
            })
            .Add(new PrefabSlotDef {
                Ch = 'L', Name = "吊灯(蓝地牢吊灯)",
                TileType = TileID.Chandeliers, Style = 27,
                TopAnchor = true, ClearanceBelow = 5, MirrorCh = 'c'
            })
            .Add(new PrefabSlotDef {
                Ch = '+', Name = "门板(蓝地牢门)",
                TileType = TileID.ClosedDoor, Style = 16, MirrorCh = '+'
            })
            .Add(new PrefabSlotDef {
                Ch = 'W', Name = "彩窗留位", MarkerOnly = true, MirrorCh = 'W'
            })
            .Add(new PrefabSlotDef {
                Ch = 'B', Name = "钟楼锚留位", MarkerOnly = true, MirrorCh = '\0'
            });

        //44x24;字符表见PrefabLegend头注释
        private static readonly string[] ChapelArt = [
            "                ############                ",
            "            ####################            ",
            "        ########........B...########        ",
            "    ########..##.....L......##..########    ",
            "  ######......##............##......######  ",
            "  ##..........##............##..........##  ",
            "  ##..........##.....WW.....##..........##  ",
            "  ##..........##.....WW.....##..........##  ",
            "  ##..........##............##..........##  ",
            "  ##..........##............##..........##  ",
            "  ##..........##............##..........##  ",
            "  ##..........##............##..........##  ",
            "  ##..........##............##..........##  ",
            "  ##..........##............##..........##  ",
            "  ##..........##............##..........##  ",
            "  ##..........##............##..........##  ",
            "  ##..........##............##..........##  ",
            "  ##..........##............##..........##  ",
            "  ##.........3##4..........3##4.........##  ",
            "  DD..............................A.....DD  ",
            "  D+............................######..DD  ",
            "  DD..b.....c.....b.......c....#######..DD  ",
            "  ########################################  ",
            "  ########################################  ",
        ];

        private static Prefab _chapel;
        internal static Prefab Chapel => _chapel ??= Prefab.Parse("ChapelDraft", ChapelArt, BuildLegend());
    }
}
