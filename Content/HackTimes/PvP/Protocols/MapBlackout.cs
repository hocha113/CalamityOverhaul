using CalamityOverhaul.Content.HackTimes.PvP.UI;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 地图熄灭（芯片档）：十五秒内防守方本机的小地图 / 覆盖式大地图 / 全屏地图
    /// 被雪花噪声覆盖，地图上的队友头像与图钉随图层一起熄灭。<br/>
    /// <b>纯渲染压制</b>：不写 <c>Main.mapEnabled</c>、不动地图数据
    /// （<c>Main.Map</c> 的探索进度一字不改），效果到期图层原样回来。
    /// 图层压制与全屏雪花在 <see cref="MapBlackoutRenderer"/>（防守方本机钩子）；
    /// 小地图角落的雪花面板画在本类的 HUD 覆盖层。<br/>
    /// 红线遵守：只糊地图区——覆盖式大地图（mapStyle 2）是整层隐藏而不是整屏糊雪花，
    /// 角色本体与来袭弹幕的可见性不受任何影响
    /// </summary>
    internal class MapBlackout : PlayerHackDef
    {
        /// <summary>晶粒纹：人形轮廓 + 被切断的寻路线 + 打了叉的地图页（芯片与 HUD 条目共用）</summary>
        internal const string Die =
            "M -0.46 -0.62 L -0.26 -0.62 L -0.26 -0.44 L -0.46 -0.44 Z "
            + "M -0.54 -0.34 L -0.18 -0.34 L -0.24 0.26 L -0.48 0.26 Z "
            + "M -0.18 -0.06 L 0.00 -0.06 M 0.10 -0.06 L 0.22 -0.06 "
            + "M 0.02 -0.14 L 0.08 0.02 "
            + "M 0.28 -0.32 L 0.72 -0.24 L 0.72 0.24 L 0.28 0.16 Z "
            + "M 0.42 -0.29 L 0.42 0.19 "
            + "M 0.50 -0.16 L 0.64 -0.02 M 0.64 -0.14 L 0.50 0.00";

        /// <summary>per-effect 状态：只存噪声种子，随条目自清</summary>
        private sealed class BlackoutState
        {
            public float Seed;
        }

        public override void SetDefaults() {
            UploadTime = 120;
            RamCost = 4;
            Category = QuickHackCategory.Covert;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 900;

        public override bool OnDefenderApply(Player defender, PlayerHackEffect effect) {
            effect.ProtocolState = new BlackoutState {
                Seed = (effect.ActivationId % 1000) * 0.377f,
            };
            return true;
        }

        //真实数值与地图数据零改动：本协议没有 Tick 侧逻辑，
        //图层压制按"本机帐本在册"逐帧判定（MapBlackoutRenderer），到期自动恢复

        public override void DrawDefenderOverlay(SpriteBatch spriteBatch, Player defender,
            PlayerHackEffect effect) {
            if (Main.mapFullscreen || !Main.mapEnabled) {
                return;   //全屏地图由 MapBlackoutRenderer.PostDrawFullscreenMap 糊
            }
            float seed = effect.ProtocolState is BlackoutState state ? state.Seed : 0f;

            if (Main.mapStyle == 1) {
                //角落小地图：图层被压制后在原位画雪花面板（尺寸字段是上一帧真值，
                //同分辨率下保持有效；从未画过地图时走锚定兜底）
                Rectangle rect = Main.miniMapWidth > 0
                    ? new Rectangle(Main.miniMapX - 4, Main.miniMapY - 4,
                        Main.miniMapWidth + 8, Main.miniMapHeight + 8)
                    : new Rectangle((int)(HackTheme.UIScreenW - 256f), 90, 248, 248);
                MapBlackoutRenderer.DrawSnow(spriteBatch, rect, seed, 1f, 8);
            }
            else if (Main.mapStyle == 2) {
                //覆盖式大地图被整层隐藏（糊整屏会遮挡战场，踩红线）：
                //只在它原本的右上信息位挂一枚失联角标
                HackTheme.DrawBadge(spriteBatch,
                    new Vector2(HackTheme.UIScreenW - 210f, 96f),
                    PvPHudText.SignalLost.Value, PvPTheme.Hostile, 0.9f);
            }
        }

        public override string GlyphDiePath => Die;
    }
}
