using CalamityOverhaul.Content.HackTimes.PvP.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 冷却注入（芯片档）：六秒内防守方本机 useTime/useAnimation ×1.35
    /// （出手放缓 35%），落地瞬间若药水冷却计时中则一次性 +300f。<br/>
    /// 不锁输入不禁物品，出手全能出，只是慢。数值全走
    /// <see cref="HackPvPRules.ClampUseSlow"/> / <see cref="HackPvPRules.ClampUseSlowDuration"/>
    /// 红线（≤35%·≤360f），落点是防守方本机的物品使用速度乘区
    /// （<see cref="CooldownInjectUseHook"/>，GlobalItem.UseSpeedMultiplier
    /// 物品级乘子，全 DamageClass 与工具一体覆盖）。<br/>
    /// 迟滞类必须带屏幕语汇（设计 §7.3）：漂移扫描线 + 出手迟滞角标，
    /// 让"慢"读成"被骇"而不是"掉帧"
    /// </summary>
    internal class CooldownInject : PlayerHackDef
    {
        /// <summary>设计值：出手放缓比例（进红线 Clamp 后落地）</summary>
        private const float DesignSlowFraction = 0.35f;
        /// <summary>落地瞬间药水冷却追加帧数（仅在计时中时）</summary>
        private const int PotionDelayInject = 300;

        /// <summary>晶粒纹：人形轮廓 + 被切断的出手臂线 + 慢走的表盘（芯片与 HUD 条目共用）</summary>
        internal const string Die =
            "M -0.46 -0.62 L -0.26 -0.62 L -0.26 -0.44 L -0.46 -0.44 Z "
            + "M -0.54 -0.34 L -0.18 -0.34 L -0.24 0.26 L -0.48 0.26 Z "
            + "M -0.18 -0.10 L 0.02 -0.10 M 0.12 -0.10 L 0.22 -0.10 "
            + "M 0.04 -0.18 L 0.10 -0.02 "
            + "M 0.50 -0.28 Q 0.68 -0.28 0.68 -0.10 Q 0.68 0.08 0.50 0.08 "
            + "Q 0.32 0.08 0.32 -0.10 Q 0.32 -0.28 0.50 -0.28 Z "
            + "M 0.50 -0.10 L 0.60 -0.20 M 0.50 -0.10 V 0.02 "
            + "M 0.50 0.16 L 0.50 0.26 M 0.50 0.34";

        /// <summary>per-effect 状态：Clamp 后的实际放缓比例，随条目自清</summary>
        private sealed class SlowState
        {
            public float Fraction;
        }

        public override void SetDefaults() {
            UploadTime = 120;
            RamCost = 4;
            Category = QuickHackCategory.Control;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 360;

        public override bool OnDefenderApply(Player defender, PlayerHackEffect effect) {
            //红线落地点①：放缓比例进 ClampUseSlow（≤0.35）
            //红线落地点②：时长进 ClampUseSlowDuration（≤360f），授予时长本就 360，
            //这里是防御性压回，防未来有人只改 GetDuration 不看红线
            effect.ProtocolState = new SlowState {
                Fraction = HackPvPRules.ClampUseSlow(DesignSlowFraction),
            };
            effect.Duration = Math.Min(effect.Duration,
                HackPvPRules.ClampUseSlowDuration(effect.Duration));

            //药水冷却注入：只在计时中追加（不给没喝药的人凭空挂冷却）
            if (defender.potionDelay > 0) {
                defender.potionDelay += PotionDelayInject;
            }
            return true;
        }

        /// <summary>
        /// 本机玩家当前在册的出手放缓比例，0 = 无效果。
        /// <see cref="CooldownInjectUseHook"/> 每次物品使用取一次；
        /// 帐本只在防守方自己的客户端非空，远端与服务端天然返回 0
        /// </summary>
        internal static float GetSlowFraction(Player player) {
            if (Main.dedServ || player == null || player.whoAmI != Main.myPlayer) {
                return 0f;
            }
            PlayerHackEffect effect = PvPDefenderLocal.FindEffect<CooldownInject>();
            return effect?.ProtocolState is SlowState state ? state.Fraction : 0f;
        }

        //防守方本机的手部拖影（世界空间，只在防守方自己的屏幕上
        //本簇唯一出防守方本机的表现是隐身剥离的轮廓光，这里不走镜像）
        public override bool OnDefenderTick(Player defender, PlayerHackEffect effect) {
            if (defender.itemAnimation > 0 && Main.rand.NextBool(3)) {
                Vector2 pos = defender.itemLocation + new Vector2(
                    Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-10f, 10f));
                PRTLoader.NewParticle<PRT_TBUGGlitch>(pos,
                    -defender.velocity * 0.1f, default,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(18);
            }
            return true;
        }

        public override void DrawDefenderOverlay(SpriteBatch spriteBatch, Player defender,
            PlayerHackEffect effect) {
            Texture2D pixel = HackTheme.Pixel;
            if (pixel == null) {
                return;
            }
            float fraction = effect.ProtocolState is SlowState state
                ? state.Fraction : DesignSlowFraction;

            //迟滞屏幕语汇：两道缓慢下漂的横向扫描线（亮色低透明，不遮挡任何读数）
            float screenW = HackTheme.UIScreenW;
            float screenH = HackTheme.UIScreenH;
            for (int i = 0; i < 2; i++) {
                int y = (int)((Main.GameUpdateCount * 1.2f + i * screenH * 0.5f)
                    % screenH);
                spriteBatch.Draw(pixel, new Rectangle(0, y, (int)screenW, 2),
                    HackTheme.SrcPixel, PvPTheme.Amber * 0.10f);
                spriteBatch.Draw(pixel, new Rectangle(0, y + 1, (int)screenW, 1),
                    HackTheme.SrcPixel, Color.White * 0.05f);
            }

            //出手迟滞角标：快捷栏右端，明说慢了多少
            HackTheme.DrawBadge(spriteBatch, new Vector2(498f, 24f),
                PvPDefenderText.ActuationLagFormat.Format(
                    (int)MathF.Round(fraction * 100f)),
                PvPTheme.Amber, 0.95f);
        }

        public override string GlyphDiePath => Die;
    }
}
