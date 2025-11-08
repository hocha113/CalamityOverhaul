using Terraria;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers.SignalTower
{
    /// <summary>
    /// 信号塔目标点数据
    /// </summary>
    public class SignalTowerTargetPoint
    {
        /// <summary>
        /// 目标点位置(图格坐标)
        /// </summary>
        public Point TilePosition { get; set; }

        /// <summary>
        /// 有效范围(图格单位)
        /// </summary>
        public int Range { get; set; }

        /// <summary>
        /// 是否已完成
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// 点位索引
        /// </summary>
        public int Index { get; set; }

        public SignalTowerTargetPoint(Point position, int range, int index) {
            TilePosition = position;
            Range = range;
            IsCompleted = false;
            Index = index;
        }

        /// <summary>
        /// 检查指定位置是否在范围内
        /// </summary>
        public bool IsInRange(Point tilePos) {
            float distance = Vector2.Distance(TilePosition.ToVector2(), tilePos.ToVector2());
            return distance <= Range;
        }

        /// <summary>
        /// 检查玩家是否在范围内
        /// </summary>
        public bool IsPlayerInRange(Player player) {
            Point playerTilePos = player.Center.ToTileCoordinates();
            return IsInRange(playerTilePos);
        }

        /// <summary>
        /// 获取世界坐标
        /// </summary>
        public Vector2 WorldPosition => TilePosition.ToVector2() * 16f;
    }
}
