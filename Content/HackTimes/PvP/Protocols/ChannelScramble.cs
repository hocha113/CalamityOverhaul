using CalamityOverhaul.Content.HackTimes.PvP.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 信道乱码（芯片档）：二十秒内防守方本机聊天<b>显示层</b>乱码化
    /// 期间新到的聊天行 40% 字符替换为故障字形，同期队伍层信号
    /// （队友抬头名牌、地图队伍图钉）在其屏幕上不显示。<br/>
    /// <b>表达边界（设计 §7.7，别越线）</b>：只改防守方看到的渲染
    /// 聊天存储（RemadeChatMonitor 的消息表）一字不改、不冒充任何人发言、
    /// 不影响任何其他玩家看到的聊天；效果到期连乱码期到达的旧行都恢复原文。
    /// 未来别在这里"顺手加个假消息注入"，那是平台层风险。<br/>
    /// 渲染拦截在 <see cref="ChannelScrambleChatHook"/>（防守方本机钩子）
    /// </summary>
    internal class ChannelScramble : PlayerHackDef
    {
        /// <summary>晶粒纹：人形轮廓 + 断裂的声波弧 + 破碎的文本行（芯片与 HUD 条目共用）</summary>
        internal const string Die =
            "M -0.46 -0.62 L -0.26 -0.62 L -0.26 -0.44 L -0.46 -0.44 Z "
            + "M -0.54 -0.34 L -0.18 -0.34 L -0.24 0.26 L -0.48 0.26 Z "
            + "M 0.00 -0.60 Q 0.12 -0.48 0.00 -0.36 "
            + "M 0.14 -0.68 Q 0.30 -0.50 0.14 -0.30 "
            + "M 0.30 -0.76 Q 0.38 -0.66 0.40 -0.56 "
            + "M 0.46 -0.40 Q 0.44 -0.30 0.36 -0.22 "
            + "M 0.52 -0.52 L 0.60 -0.44 "
            + "M 0.16 0.00 L 0.28 0.00 M 0.36 0.00 L 0.42 0.00 M 0.50 0.00 L 0.64 0.00 "
            + "M 0.20 0.14 L 0.26 0.14 M 0.34 0.14 L 0.52 0.14 "
            + "M 0.62 -0.68 M 0.70 -0.56 M 0.66 -0.30";

        /// <summary>per-effect 状态：噪声种子，随条目自清</summary>
        private sealed class ScrambleState
        {
            public float Seed;
        }

        public override void SetDefaults() {
            UploadTime = 100;
            RamCost = 3;
            Category = QuickHackCategory.Covert;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 1200;

        public override bool OnDefenderApply(Player defender, PlayerHackEffect effect) {
            effect.ProtocolState = new ScrambleState {
                Seed = (effect.ActivationId % 1000) * 0.613f,
            };
            return true;
        }

        //聊天框边缘故障描边：左缘竖向故障短划 + 顶部角标，标出"这一片读数不可信"。
        //只描边不遮字，乱码本身已经由渲染钩子完成
        public override void DrawDefenderOverlay(SpriteBatch spriteBatch, Player defender,
            PlayerHackEffect effect) {
            Texture2D pixel = HackTheme.Pixel;
            if (pixel == null) {
                return;
            }
            float seed = effect.ProtocolState is ScrambleState state ? state.Seed : 0f;
            float time = Main.GameUpdateCount * 0.05f + seed;

            //原版聊天行锚区：x=88 起，自底部向上最多 10 行 × 21px
            int zoneBottom = Main.screenHeight - 34;
            int zoneTop = Main.screenHeight - 58 - 10 * 21;

            for (int i = 0; i < 9; i++) {
                float h = Hash(time * 0.37f + i * 5.13f);
                if (h < 0.3f) {
                    continue;
                }
                int y = zoneTop + (int)(Hash(i * 11.7f + time * 0.21f)
                    * (zoneBottom - zoneTop));
                int len = 6 + (int)(h * 14f);
                Color body = i % 3 == 0 ? PvPTheme.HostileAlt : PvPTheme.Hostile;
                spriteBatch.Draw(pixel, new Rectangle(80, y, 3, len),
                    HackTheme.SrcPixel, body * 0.55f);
                spriteBatch.Draw(pixel, new Rectangle(81, y + len / 3, 1, len / 3),
                    HackTheme.SrcPixel, Color.White * 0.5f);
            }

            HackTheme.DrawBadge(spriteBatch, new Vector2(88f, zoneTop - 16f),
                PvPDefenderText.CommsGarbled.Value, PvPTheme.Hostile, 0.9f);
        }

        public override string GlyphDiePath => Die;

        private static float Hash(float p) {
            p = MathF.Abs(p * 0.1031f % 1f);
            p *= p + 33.33f;
            p *= p + p;
            return MathF.Abs(p % 1f);
        }
    }
}
