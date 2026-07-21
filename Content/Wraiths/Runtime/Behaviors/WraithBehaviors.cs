using CalamityOverhaul.Content.Wraiths.Core;
using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Runtime.Behaviors
{
    /// <summary>锚点游荡，列表宜放最前</summary>
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

    /// <summary>对最近玩家保距，只改径向</summary>
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
            //error>0 靠拢，error<0 外退
            Vector2 radial = (player.Center - wraith.Center).SafeNormalize(Vector2.Zero) * Math.Sign(error) * speed;
            wraith.Velocity = Vector2.Lerp(wraith.Velocity, radial, 0.08f);
        }
    }

    /// <summary>凝视僵直，须放行为列表最后</summary>
    public sealed class FreezeWhenGazedBehavior(float damping = 0.5f) : IWraithBehavior
    {
        /// <summary>低于此速直接归零</summary>
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
