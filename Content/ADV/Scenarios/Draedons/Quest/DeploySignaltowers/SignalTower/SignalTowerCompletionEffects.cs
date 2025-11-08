using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltowers.SignalTower
{
    /// <summary>
    /// 信号塔完成效果管理器
    /// </summary>
    internal static class SignalTowerCompletionEffects
    {
        /// <summary>
        /// 播放信号塔完成效果
        /// </summary>
        public static void PlayCompletionEffect(Vector2 worldPosition, int pointIndex) {
            if (VaultUtils.isServer) {
                return;
            }

            //播放成功音效
            SoundEngine.PlaySound(SoundID.Item4 with {
                Volume = 1.2f,
                Pitch = 0.3f,
                PitchVariance = 0.1f
            }, worldPosition);

            //创建科技粒子爆发
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);

                Dust dust = Dust.NewDustPerfect(worldPosition, DustID.Electric, velocity, 0, default, Main.rand.NextFloat(1.5f, 2.5f));
                dust.noGravity = true;
                dust.fadeIn = 1.2f;
            }

            //创建冲击波效果
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * Main.rand.NextFloat();
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 3f);

                Dust dust = Dust.NewDustPerfect(worldPosition, DustID.TreasureSparkle, velocity, 0, new Color(100, 220, 255), Main.rand.NextFloat(1f, 2f));
                dust.noGravity = true;
            }

            //显示完成文本
            string completionText = SignalTowerTargetRenderer.TargetCompletedText.Value.Replace("[NUM]", (pointIndex + 1).ToString());
            CombatText.NewText(new Rectangle((int)worldPosition.X - 50, (int)worldPosition.Y - 100, 100, 50),
                new Color(100, 220, 255), completionText, true, false);
        }

        /// <summary>
        /// 播放全部完成效果
        /// </summary>
        public static void PlayAllCompletionEffect() {
            if (VaultUtils.isServer) {
                return;
            }

            Player player = Main.LocalPlayer;

            //播放胜利音效
            SoundEngine.PlaySound(SoundID.Item4 with {
                Volume = 1.5f,
                Pitch = 0.5f
            }, player.Center);

            //创建大型粒子爆发
            for (int i = 0; i < 50; i++) {
                float angle = MathHelper.TwoPi * i / 50f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 12f);

                Dust dust = Dust.NewDustPerfect(player.Center, DustID.Electric, velocity, 0, default, Main.rand.NextFloat(2f, 3f));
                dust.noGravity = true;
                dust.fadeIn = 1.5f;
            }

            //显示完成文本
            CombatText.NewText(new Rectangle((int)player.Center.X - 100, (int)player.Center.Y - 100, 200, 50),
                Color.Gold, SignalTowerTargetRenderer.AllCompletedText.Value, true, true);
        }
    }
}
