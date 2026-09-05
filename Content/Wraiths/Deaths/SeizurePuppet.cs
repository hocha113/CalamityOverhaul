using System;
using Terraria;

namespace CalamityOverhaul.Content.Wraiths.Deaths
{
    /// <summary>
    /// 夺身期间的受害者体态控制。演出只声明想要的姿态，写玩家本体的活集中在这里。<br/>
    /// <b>时机</b>：活着时的体态必须在 <c>ModPlayer.PostUpdate</c> 阶段应用。原版
    /// <c>Player.PlayerFrame</c> 在 <c>Update</c> 中段重算 <c>bodyFrame</c>，比它早写会被覆盖；
    /// <c>fullRotation</c> 没有这个问题，但一并放在同一阶段省得两处判时序。<br/>
    /// 死亡后原版会把头、身、腿三段抛散并逐帧加重力（<c>Player.UpdateDead</c>），
    /// 那三段是公开可写的，所以「尸身怎么散」同样归演出管，不必另画一个假尸体。
    /// </summary>
    internal static class SeizurePuppet
    {
        /// <summary>原版跳跃帧：双腿并拢，拿来当悬挂与被提姿最省且不出戏</summary>
        private const int HangFrameIndex = 5;

        /// <summary>钉在世界坐标某点，速度清零。</summary>
        internal static void Anchor(Player player, Vector2 target, float lerp = 0.35f) {
            player.velocity = Vector2.Zero;
            player.Center = Vector2.Lerp(player.Center, target, lerp);
            player.fallStart = (int)(player.position.Y / 16f);
        }

        /// <summary>急减速后停住，用于「被制住但还站着」。</summary>
        internal static void Brake(Player player, float damping = 0.5f, bool freeze = false) {
            player.velocity *= damping;
            if (freeze) {
                player.velocity = Vector2.Zero;
            }
            player.fallStart = (int)(player.position.Y / 16f);
        }

        internal static void FaceTowards(Player player, Vector2 worldPos) {
            float dx = worldPos.X - player.Center.X;
            if (MathF.Abs(dx) > 4f) {
                player.direction = dx > 0f ? 1 : -1;
            }
        }

        /// <summary>整体倾倒：绕脚底旋转，读作被推倒或被拽歪。</summary>
        internal static void LeanFromFeet(Player player, float radians) {
            player.fullRotation = radians;
            player.fullRotationOrigin = new Vector2(player.width * 0.5f,
                player.gravDir >= 0f ? player.height : 0f);
        }

        /// <summary>绕身体中心旋转：悬空受力时用这个，绕脚底转会像被地面钉住。</summary>
        internal static void LeanFromBody(Player player, float radians) {
            player.fullRotation = radians;
            player.fullRotationOrigin = player.Size * 0.5f;
        }

        /// <summary>悬挂或被提起：并腿 + 上身随受力方向摆。</summary>
        internal static void Hang(Player player, float sway) {
            FreezeFrame(player, HangFrameIndex);
            LeanFromBody(player, sway);
        }

        /// <summary>
        /// 被攥紧：并腿蜷曲 + 随攥紧量加剧的高频抖。<br/>
        /// 玩家绘制层拿不到缩放，压扁只能靠体态与抖动表达，不要指望真的挤扁。
        /// </summary>
        internal static void Curl(Player player, float amount, float jitterSeed) {
            amount = MathHelper.Clamp(amount, 0f, 1f);
            FreezeFrame(player, HangFrameIndex);
            float jitter = amount * amount * 0.09f
                * MathF.Sin(jitterSeed * 2.7f + Main.GlobalTimeWrappedHourly * 47f);
            LeanFromBody(player, player.direction * amount * 0.55f + jitter);
        }

        /// <summary>把某一帧钉住，防止原版逐帧重算。</summary>
        internal static void FreezeFrame(Player player, int frameIndex) {
            player.bodyFrame.Y = player.bodyFrame.Height * frameIndex;
            player.legFrame.Y = player.legFrame.Height * frameIndex;
        }

        /// <summary>交还体态。演出收尾与兜底路径都要调，否则玩家会歪着身子复活。</summary>
        internal static void ReleasePose(Player player) {
            player.fullRotation = 0f;
            player.fullRotationOrigin = Vector2.Zero;
        }

        /// <summary>
        /// 死后残体的抛散方向：处决帧调一次，决定尸块按这只鬼的方式散开。<br/>
        /// 原版 <c>KillMe</c> 给的是随机上抛，各鬼应覆写成自己的语义（被攥爆是向外迸，
        /// 被雨吐回是整体下坠）。
        /// </summary>
        internal static void ThrowBody(Player player, Vector2 impulse, float spread, float spin) {
            player.headVelocity = impulse + Main.rand.NextVector2Circular(spread, spread);
            player.bodyVelocity = impulse + Main.rand.NextVector2Circular(spread, spread);
            player.legVelocity = impulse + Main.rand.NextVector2Circular(spread, spread);
            player.headRotation += spin;
            player.bodyRotation += spin * 0.6f;
            player.legRotation -= spin * 0.4f;
        }

        /// <summary>
        /// 逐帧压住残体速度，用于「尸身被某种力带着走」。<br/>
        /// 原版每帧给三段加 0.1 重力，不压就会各自飞散。
        /// </summary>
        internal static void DriveBody(Player player, Vector2 velocity, float blend = 0.35f) {
            player.headVelocity = Vector2.Lerp(player.headVelocity, velocity, blend);
            player.bodyVelocity = Vector2.Lerp(player.bodyVelocity, velocity, blend);
            player.legVelocity = Vector2.Lerp(player.legVelocity, velocity, blend);
        }

        /// <summary>残体落定：三段收拢回本体附近并停住，读作「尸体躺在这里」。</summary>
        internal static void SettleBody(Player player, float drag = 0.82f) {
            player.headVelocity *= drag;
            player.bodyVelocity *= drag;
            player.legVelocity *= drag;
            player.headPosition = Vector2.Lerp(player.headPosition, Vector2.Zero, 0.12f);
            player.bodyPosition = Vector2.Lerp(player.bodyPosition, Vector2.Zero, 0.12f);
            player.legPosition = Vector2.Lerp(player.legPosition, Vector2.Zero, 0.12f);
        }
    }
}
