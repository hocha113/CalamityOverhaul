using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Draedon.Quest.DeploySignaltowers.SignalTower
{
    public class SignalTowerTargetPoint
    {
        public Point TilePosition { get; set; }
        public int Range { get; set; }//图格
        public bool IsCompleted { get; set; }
        public int Index { get; set; }

        public SignalTowerTargetPoint(Point position, int range, int index) {
            TilePosition = position;
            Range = range;
            IsCompleted = false;
            Index = index;
        }

        public bool IsInRange(Point tilePos) {
            //正方形范围,与渲染边框一致
            int deltaX = Math.Abs(tilePos.X - TilePosition.X);
            int deltaY = Math.Abs(tilePos.Y - TilePosition.Y);
            return deltaX <= Range && deltaY <= Range;
        }

        public bool IsPlayerInRange(Player player) {
            Point playerTilePos = player.Center.ToTileCoordinates();
            return IsInRange(playerTilePos);
        }

        public Vector2 WorldPosition => TilePosition.ToVector2() * 16f;
    }
}
