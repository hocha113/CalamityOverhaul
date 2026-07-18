using System;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>厉鬼感知的纯几何判定，无状态；边沿检测与事件分发在 <see cref="WraithActor"/> 内</summary>
    public static class WraithSensors
    {
        /// <summary>近身盲区半宽（像素），此距离内不看朝向恒判为注视</summary>
        private const float GazeDeadZone = 40f;

        /// <summary>
        /// 玩家是否正注视该实体：限距 + 面朝（水平近身盲区内忽略朝向）+ 视线不被物块遮挡
        /// </summary>
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

        /// <summary>最近的存活玩家，无人返回 null，distance 给 float.MaxValue</summary>
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
