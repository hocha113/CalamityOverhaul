using System;
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
        /// 检查指定位置是否在范围内(正方形范围判断)
        /// </summary>
        public bool IsInRange(Point tilePos) {
            //使用正方形范围判断，与渲染器显示的正方形边框一致
            int deltaX = Math.Abs(tilePos.X - TilePosition.X);
            int deltaY = Math.Abs(tilePos.Y - TilePosition.Y);
            return deltaX <= Range && deltaY <= Range;
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
