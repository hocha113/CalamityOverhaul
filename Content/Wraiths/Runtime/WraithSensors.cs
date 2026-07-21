using System;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>感知几何判定，无状态；事件分发在 Actor</summary>
    public static class WraithSensors
    {
        /// <summary>近身盲区半宽 px，区内不看朝向</summary>
        private const float GazeDeadZone = 40f;

        /// <summary>是否注视，限距+面朝+LOS</summary>
        public static bool IsGazedBy(Player player, WraithActor wraith, float range) {
            Vector2 toWraith = wraith.Center - player.Center;
            if (toWraith.LengthSquared() > range * range) {
                return false;
            }
            if (Math.Abs(toWraith.X) > GazeDeadZone && Math.Sign(toWraith.X) != player.direction) {
                return false;
            }
            return Collision.CanHitLine(player.Center, 1, 1, wraith.Center, 1, 1);
        }

        /// <summary>最近存活玩家，无人则 null</summary>
        public static Player NearestPlayer(Vector2 center, out float distance) {
            Player nearest = null;
            float bestSq = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(player.Center, center);
                if (distSq < bestSq) {
                    bestSq = distSq;
                    nearest = player;
                }
            }
            distance = nearest == null ? float.MaxValue : MathF.Sqrt(bestSq);
            return nearest;
        }
    }
}
