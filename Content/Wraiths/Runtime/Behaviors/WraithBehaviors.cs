using CalamityOverhaul.Content.Wraiths.Core;
using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Runtime.Behaviors
{
    /// <summary>
    /// 锚点游荡：围绕 SpawnAnchor 在圆内挑点漂移，到点或超时后重新挑点。
    /// 数值全参数化，无主题；列表中应放在最前（后续积木会在它给出的意图上修正）
    /// </summary>
    public sealed class HoverWanderBehavior(float radius, float speed, int retargetMin = 100, int retargetMax = 220) : IWraithBehavior
    {
        private Vector2 target;
        private int retargetTimer;

        public void Update(WraithActor wraith) {
            if (--retargetTimer <= 0 || Vector2.DistanceSquared(wraith.Center, target) < 32f * 32f) {
                retargetTimer = Main.rand.Next(retargetMin, retargetMax);
                target = wraith.SpawnAnchor + Main.rand.NextVector2Circular(radius, radius * 0.7f);
            }
            Vector2 desired = (target - wraith.Center).SafeNormalize(Vector2.Zero) * speed;
            wraith.Velocity = Vector2.Lerp(wraith.Velocity, desired, 0.045f);
        }
    }

    /// <summary>
    /// 对最近玩家保距：距离偏出 preferred±band 才径向修正，带内不干预。
    /// 只修正径向分量，与游荡积木叠加时保留切向漂移
    /// </summary>
    public sealed class KeepDistanceBehavior(float preferred, float band, float speed) : IWraithBehavior
    {
        public void Update(WraithActor wraith) {
            Player player = WraithSensors.NearestPlayer(wraith.Center, out float distance);
            if (player == null || distance == float.MaxValue) {
                return;
            }
            float error = distance - preferred;
            if (Math.Abs(error) < band) {
                return;
            }
            //error>0 离得远向玩家靠,error<0 贴得近向外退
            Vector2 radial = (player.Center - wraith.Center).SafeNormalize(Vector2.Zero) * Math.Sign(error) * speed;
            wraith.Velocity = Vector2.Lerp(wraith.Velocity, radial, 0.08f);
        }
    }

    /// <summary>
    /// 凝视僵直（石像鬼原语，正典承诺"看着它，它不动"）：被任意玩家注视时真停——
    /// 残速按 damping 衰减入定，阈值之下直接归零并逐帧压制。必须放在行为列表最后：
    /// 前序积木每帧注入的运动意图在这里被整体清算，注视期内实体以零速收帧
    /// </summary>
    public sealed class FreezeWhenGazedBehavior(float damping = 0.5f) : IWraithBehavior
    {
        /// <summary>低于此速率（像素/帧）直接归零，不留假僵直的余漂</summary>
        private const float SnapThreshold = 0.3f;

        public void Update(WraithActor wraith) {
            if (!wraith.GazedByAnyPlayer) {
                return;
            }
            wraith.Velocity *= damping;
            if (wraith.Velocity.LengthSquared() < SnapThreshold * SnapThreshold) {
                wraith.Velocity = Vector2.Zero;
            }
        }
    }
}
