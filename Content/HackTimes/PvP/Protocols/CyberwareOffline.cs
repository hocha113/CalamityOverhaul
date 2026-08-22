using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Cyberwares;
using CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans;
using CalamityOverhaul.Content.Cyberwares.Skills;
using CalamityOverhaul.Content.HackTimes.PvP.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 义体离线（芯片档）：八秒内断掉防守方全部义体
    /// <c>CyberwarePlayer</c> 的两条效果 Update 通道（UpdateEquipped /
    /// PostUpdateEquipped）被 <see cref="CyberwareOfflineHook"/> 旁路跳过，
    /// Sandevistan 正在运行则立即经它自己的开关请求链中止，技能轮盘当场合拢。<br/>
    /// <b>不动的东西</b>：装配表一格不卸（服务端拥有的 loadout 一个字不碰）、
    /// 义体物品不消失、容量不变，纯防守方本机的收益抑制，到期收益自然回流。<br/>
    /// <b>防火墙豁免</b>（设计 §3.3：义体离线明确豁免防火墙义体，否则自相矛盾）：
    /// 按类型名含 "Firewall" 判定，防火墙义体是第三波内容尚未落地，
    /// 这是给它留的名字接缝，落地时命中或换成标记接口都行
    /// </summary>
    internal class CyberwareOffline : PlayerHackDef
    {
        /// <summary>Sandevistan 中止请求的重发间隔（帧）：开关走请求-回执链，
        /// 一个 RTT 内 IsActive 不会立刻翻转，逐帧重发会刷爆请求限频</summary>
        private const int SandevistanRetryFrames = 30;

        /// <summary>晶粒纹：人形轮廓 + 脊柱总线上被切断的义体引线 + 离线叉（芯片与 HUD 条目共用）</summary>
        internal const string Die =
            "M -0.46 -0.62 L -0.26 -0.62 L -0.26 -0.44 L -0.46 -0.44 Z "
            + "M -0.54 -0.34 L -0.18 -0.34 L -0.24 0.26 L -0.48 0.26 Z "
            + "M -0.36 -0.34 V 0.26 "
            + "M -0.36 -0.16 L -0.14 -0.16 M -0.04 -0.16 "
            + "M -0.36 0.06 L -0.18 0.14 M -0.08 0.18 "
            + "M 0.24 -0.10 L 0.58 0.24 M 0.58 -0.10 L 0.24 0.24 "
            + "M 0.34 -0.44 Q 0.52 -0.60 0.66 -0.44";

        /// <summary>per-effect 状态：Sandevistan 中止重发计时，随条目自清</summary>
        private sealed class OfflineState
        {
            public int SandevistanRetry;
        }

        public override void SetDefaults() {
            UploadTime = 160;
            RamCost = 6;
            Category = QuickHackCategory.Control;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 480;

        /// <summary>防火墙类义体豁免判定（旁路的放行名单）</summary>
        internal static bool IsFirewallExempt(BaseCyberware cyberware)
            => cyberware != null && cyberware.GetType().Name
                .Contains("Firewall", StringComparison.OrdinalIgnoreCase);

        public override bool OnDefenderApply(Player defender, PlayerHackEffect effect) {
            effect.ProtocolState = new OfflineState();

            //技能轮盘当场合拢：正在蓄力的技能按取消结算（防守方本机 UI 状态）
            var radial = defender.GetModPlayer<CyberwareSkillRadialController>();
            radial.CancelChargeIfAny();
            radial.ForceCloseRadial();

            //Sandevistan 正在运行则立即中止，走它自己的开关请求链
            //（单人直落权威，联机发请求给服务端，权威语义不被绕过）
            var sandevistan = defender.GetModPlayer<SandevistanPlayer>();
            if (sandevistan.IsActive) {
                sandevistan.RequestToggle(false);
            }

            SoundEngine.PlaySound(CWRSound.Hacker with {
                Volume = 0.65f,
                Pitch = -0.55f,
            }, defender.Center);
            return true;
        }

        public override bool OnDefenderTick(Player defender, PlayerHackEffect effect) {
            if (effect.ProtocolState is not OfflineState state) {
                return true;
            }
            //离线期重开 Sandevistan 的尝试按节拍持续拍熄（含中止请求丢包自愈）
            if (--state.SandevistanRetry <= 0) {
                state.SandevistanRetry = SandevistanRetryFrames;
                var sandevistan = defender.GetModPlayer<SandevistanPlayer>();
                if (sandevistan.IsActive) {
                    sandevistan.RequestToggle(false);
                }
            }
            return true;
        }

        //到期无清理项：旁路按"本机帐本在册"逐帧判定，条目消失收益即恢复

        public override void DrawDefenderOverlay(SpriteBatch spriteBatch, Player defender,
            PlayerHackEffect effect) {
            Texture2D pixel = HackTheme.Pixel;
            if (pixel == null) {
                return;
            }

            //义体 HUD 聚在左下角（IBottomLeftHud 簇），离线标记压在它上方：
            //角标 + 每枚已装义体一格离线叉
            float x = 26f;
            float y = HackTheme.UIScreenH - 206f;
            HackTheme.DrawBadge(spriteBatch, new Vector2(x, y),
                PvPDefenderText.CyberOffline.Value, PvPTheme.Hostile,
                0.7f + 0.3f * MathF.Sin(Main.GameUpdateCount * 0.1f));

            Item[] equipped = defender.GetModPlayer<CyberwarePlayer>()
                ?.EquippedCyberwares;
            if (equipped == null) {
                return;
            }
            int drawn = 0;
            for (int i = 0; i < equipped.Length; i++) {
                if (equipped[i]?.IsAir != false) {
                    continue;
                }
                var cell = new Rectangle((int)x + drawn * 17, (int)y + 18, 13, 13);
                bool exempt = equipped[i].ModItem is BaseCyberware ware
                    && IsFirewallExempt(ware);
                Color line = exempt ? HackTheme.AccentAlt : PvPTheme.Hostile;
                //1px 空框 + 叉；豁免的义体画通电色不打叉
                spriteBatch.Draw(pixel, new Rectangle(cell.X, cell.Y, cell.Width, 1),
                    HackTheme.SrcPixel, line * 0.8f);
                spriteBatch.Draw(pixel,
                    new Rectangle(cell.X, cell.Bottom - 1, cell.Width, 1),
                    HackTheme.SrcPixel, line * 0.8f);
                spriteBatch.Draw(pixel, new Rectangle(cell.X, cell.Y, 1, cell.Height),
                    HackTheme.SrcPixel, line * 0.8f);
                spriteBatch.Draw(pixel,
                    new Rectangle(cell.Right - 1, cell.Y, 1, cell.Height),
                    HackTheme.SrcPixel, line * 0.8f);
                if (!exempt) {
                    HackTheme.DrawLine(spriteBatch,
                        new Vector2(cell.X + 2, cell.Y + 2),
                        new Vector2(cell.Right - 2, cell.Bottom - 2), 1f, line);
                    HackTheme.DrawLine(spriteBatch,
                        new Vector2(cell.Right - 2, cell.Y + 2),
                        new Vector2(cell.X + 2, cell.Bottom - 2), 1f, line);
                }
                drawn++;
            }
        }

        //攻击方与旁观者的外化：断电渣从躯干簌簌往下掉（镜像驱动，各端可见）
        public override void OnSpectatorTick(Player defender, int casterIndex,
            int elapsed, int duration) {
            if (Main.dedServ || !Main.rand.NextBool(7)) {
                return;
            }
            Vector2 pos = defender.Center + new Vector2(
                Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(-16f, 10f));
            PRTLoader.NewParticle<PRT_TBUGGlitch>(pos,
                new Vector2(Main.rand.NextFloat(-0.3f, 0.3f),
                    Main.rand.NextFloat(1.0f, 2.2f)),
                default, Main.rand.NextFloat(0.45f, 0.8f))?.Configure(22);
        }

        public override string GlyphDiePath => Die;
    }
}
