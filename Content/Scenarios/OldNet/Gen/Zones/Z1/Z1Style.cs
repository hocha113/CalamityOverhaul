using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z1
{
    //Z1 墙脚带 材质表：接入区的"还有人维护"，锡镀板整洁灰亮，规整几何
    //主题锚：安全区起步，教学密度；做旧最少
    internal static class Z1Style
    {
        //地表地板砖（带表同源）
        internal const ushort FloorBrick = TileID.GrayBrick;
        //地下结构壳/竖井衬里
        internal const ushort RoomBrick = TileID.TinPlating;
        //室内墙
        internal const ushort RoomWall = WallID.TinPlating;
        //平台=金属架（tile19 style9，对源 Item.cs case1387）
        internal const short PlatformFrameY = 9 * 18;
    }
}
