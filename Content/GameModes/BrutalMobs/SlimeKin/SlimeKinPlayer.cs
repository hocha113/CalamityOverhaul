using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.SlimeKin
{
    /// <summary>
    /// 冰滑黏浆的打滑物理。倒计时由黏化带实体在各端为本地玩家写入
    /// （帧序免疫：弹幕更新晚于玩家更新，布尔标记会被 ResetEffects 先清掉，倒计时不会）。
    /// 实例字段天然按玩家持有，无静态每玩家状态
    /// </summary>
    internal class SlimeKinPlayer : ModPlayer
    {
        /// <summary>地面加速度保留比（越小越难起步/变向）</summary>
        private const float SlickAcceleration = 0.45f;
        /// <summary>松键减速保留比（越小滑得越远）</summary>
        private const float SlickSlowdown = 0.12f;

        /// <summary>剩余打滑帧；黏化带每帧刷成 2，离开后自然耗尽</summary>
        internal int slickFrames;

        public override void ResetEffects() {
            if (slickFrames > 0) {
                slickFrames--;
            }
        }

        public override void PostUpdateRunSpeeds() {
            //只影响踩地移动，空中不滑
            if (slickFrames <= 0 || Player.velocity.Y != 0f) {
                return;
            }
            Player.runAcceleration *= SlickAcceleration;
            Player.runSlowdown *= SlickSlowdown;
        }
    }
}
