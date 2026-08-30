namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Core
{
    //抽象材质:核心层输出,游戏侧薄壳映射TileID/WallID(蓝图§4调色板)
    internal enum HadalMat : byte
    {
        None = 0,      //开凿空间(水/气由海面规则+气穴登记决定)
        Sand,          //沙
        HardSand,      //硬化沙
        Sandstone,     //砂岩
        Stone,         //石
        Silt,          //淤泥
        Clay,          //黏土
        Mud,           //泥
        MushroomMud,   //蘑菇泥(微光斑块表层,游戏侧映射蘑菇草)
        Granite,       //花岗岩
        Obsidian,      //黑曜石
        Ash,           //灰烬
        RoomShell,     //出生房砂岩壳(与沟壁区分的人工感)
    }

    //生成参数:游戏侧从HadalworldMetrics填入,harness用同数复印
    //(蓝图R1:此结构是核心层唯一外部输入,防预览-游戏漂移)
    internal sealed class HadalGenParams
    {
        internal int Width = 2200;
        internal int Height = 5100;
        internal int SeaLevelRow = 100;
        internal int SunlitBottom = 500;
        internal int TwilightBottom = 1300;
        internal int MidnightBottom = 2700;
        internal int AbyssalBottom = 4100;
        internal int DeepestPlayableRow = 4780;
        internal ulong Seed;

        //玩家钳制死区让渡(蓝图H2),一切开凿钳在[PlayLeft,PlayRight)
        internal int PlayLeft = 44;
        internal int PlayRight => Width - PlayLeft;
    }
}
