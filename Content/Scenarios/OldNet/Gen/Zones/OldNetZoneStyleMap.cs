using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z1;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z2;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones.Z3;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Gen.Zones
{
    //带索引→样式表的映射（P20竖井衬里等跨带消费点用；带内容入口直接用自己的Style）
    internal static class OldNetZoneStyleMap
    {
        internal static ushort RoomBrick(int bandIndex) => bandIndex switch {
            1 => Z1Style.RoomBrick,
            2 => Z2Style.RoomBrick,
            3 => Z3Style.RoomBrick,
            _ => Z2Style.RoomBrick,
        };

        internal static ushort RoomWall(int bandIndex) => bandIndex switch {
            1 => Z1Style.RoomWall,
            2 => Z2Style.RoomWall,
            3 => Z3Style.RoomWall,
            _ => Z2Style.RoomWall,
        };

        internal static short PlatformFrameY(int bandIndex) => bandIndex switch {
            1 => Z1Style.PlatformFrameY,
            2 => Z2Style.PlatformFrameY,
            3 => Z3Style.PlatformFrameY,
            _ => Z2Style.PlatformFrameY,
        };
    }
}
