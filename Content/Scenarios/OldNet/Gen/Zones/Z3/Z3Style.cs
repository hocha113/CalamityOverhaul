using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z3
{
    //Z3 信号衰减带 材质表：信号尽头的焦黑——黑曜石底 + 零星导管残骸
    //主题锚：M3 疯域宿主；M2a 只给地形与最小结构
    internal static class Z3Style
    {
        internal const ushort FloorBrick = TileID.ObsidianBrick;
        internal const ushort RoomBrick = TileID.ObsidianBrick;
        //自然系黑曜石砖墙（WallID.cs L90=14，Unsafe 不计房屋）
        internal const ushort RoomWall = WallID.ObsidianBrickUnsafe;
        internal const short PlatformFrameY = 9 * 18;
    }
}
