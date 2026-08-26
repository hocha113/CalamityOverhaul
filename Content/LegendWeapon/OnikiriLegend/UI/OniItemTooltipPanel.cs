using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.TrialQuests;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>鬼切自绘物品面板皮肤:墨底纸字绯刀痕,布局归 <see cref="LegendTooltipPanel"/></summary>
    internal sealed class OniTooltipSkin : LegendTooltipSkin
    {
        /// <summary>shader 蚀边外扩量(px)</summary>
        private const int EdgePad = 12;

        public override Color TextMain => OnikiriUITheme.Paper;
        public override Color TextDim => OnikiriUITheme.TextDim;
        public override Color KeyLit => OnikiriUITheme.GoldInlay;
        public override Color KeyWarn => OnikiriUITheme.Bright;
        public override Color WorldAccent => Color.Gold;

        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        public override void DrawPanel(SpriteBatch sb, Rectangle panel, float time) {
            Effect effect = EffectLoader.OniItemPanel?.Value;
            if (effect == null) {
                DrawFallbackPanel(sb, panel);
                return;
            }
            //拔刀纸鉴 shader 面板:墨染和纸+顶沿刀痕+绯月+远山脊,色板与绯红裂空斩同源
            Rectangle extRect = panel;
            extRect.Inflate(EdgePad, EdgePad);
            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(0.97f);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
            effect.Parameters["uEdgePad"]?.SetValue((float)EdgePad);
            effect.Parameters["uColHot"]?.SetValue(CrimsonSlashRenderer.ColHot);
            effect.Parameters["uColBright"]?.SetValue(CrimsonSlashRenderer.ColBright);
            effect.Parameters["uColDeep"]?.SetValue(CrimsonSlashRenderer.ColDeep);
            effect.Parameters["uColDark"]?.SetValue(CrimsonSlashRenderer.ColDark);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            sb.Draw(Pixel, extRect, Color.White);
            sb.End();
            //还原 tooltip 层原批次(Deferred + null 默认参数)
            sb.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
        }

        /// <summary>CPU 降级:投影+墨底实色+刀痕线,shader 缺编时用</summary>
        private static void DrawFallbackPanel(SpriteBatch sb, Rectangle panel) {
            sb.Draw(Pixel, new Rectangle(panel.X + 2, panel.Y + 3, panel.Width, panel.Height), PixelSrc,
                new Color(8, 2, 5) * 0.5f);
            sb.Draw(Pixel, panel, PixelSrc, OnikiriUITheme.Ink * 0.97f);
            OniBrush.DrawTaperedSlash(sb, new Vector2(panel.X + 3f, panel.Y + 0.5f),
                new Vector2(panel.Right - 3f, panel.Y - 0.5f), 1.7f, 0.8f, 0.85f);
            sb.Draw(Pixel, new Rectangle(panel.X + 5, panel.Bottom - 1, panel.Width - 10, 1), PixelSrc,
                OnikiriUITheme.Deep * 0.55f);
        }

        public override void DrawDivider(SpriteBatch sb, Vector2 left, Vector2 right, float time) {
            OniBrush.DrawTaperedSlash(sb, left, right + new Vector2(0f, -1f), 1.2f, 0.6f, 0.55f);
        }

        public override void DrawProgressBar(SpriteBatch sb, Rectangle bar, float fill, bool passed, float time) {
            //樋槽:暗酒红底+上缘沉线
            sb.Draw(Pixel, bar, PixelSrc, OnikiriUITheme.Dark * 0.95f);
            sb.Draw(Pixel, new Rectangle(bar.X, bar.Y, bar.Width, 1), PixelSrc, Color.Black * 0.4f);
            if (passed) {
                //满樋金填,芯线白热呼吸
                float breath = OnikiriUITheme.Breath(time, 4.7f);
                sb.Draw(Pixel, new Rectangle(bar.X + 1, bar.Y + 1, bar.Width - 2, bar.Height - 2), PixelSrc,
                    OnikiriUITheme.GoldInlay * (0.72f + breath * 0.14f));
                sb.Draw(Pixel, new Rectangle(bar.X + 2, bar.Y + bar.Height / 2, bar.Width - 4, 1), PixelSrc,
                    OnikiriUITheme.HotWhite * (0.3f + breath * 0.25f));
                return;
            }
            int fillW = (int)(bar.Width * MathHelper.Clamp(fill, 0f, 1f));
            if (fillW <= 0) {
                return;
            }
            //刀樋金填:氧化金衬底+亮金上层,前沿烧口呼吸
            sb.Draw(Pixel, new Rectangle(bar.X, bar.Y + 1, fillW, bar.Height - 2), PixelSrc,
                OnikiriUITheme.GoldDeep * 0.9f);
            sb.Draw(Pixel, new Rectangle(bar.X, bar.Y + 2, fillW, bar.Height - 4), PixelSrc,
                OnikiriUITheme.GoldInlay * 0.8f);
            float pulse = OnikiriUITheme.Breath(time, 8.9f, 3f);
            sb.Draw(Pixel, new Rectangle(bar.X + Math.Max(0, fillW - 2), bar.Y, 2, bar.Height), PixelSrc,
                OnikiriUITheme.BurnHot * (0.4f + pulse * 0.4f));
        }

        public override void DecoratePanel(SpriteBatch sb, Rectangle panel, float time) {
            //右下角朱印落款,轻倾+缓呼吸
            float breath = OnikiriUITheme.Breath(time, 2.3f, 1.5f);
            OniBrush.DrawSealGlyph(sb, new Vector2(panel.Right - 17f, panel.Bottom - 16f),
                11f, 0.62f + breath * 0.12f, 0.08f);
        }
    }

    /// <summary>鬼切物品面板入口:组装键位/试炼数据,交给共享引擎</summary>
    internal static class OniItemTooltipPanel
    {
        private static readonly OniTooltipSkin skin = new();

        /// <summary>返回 false=接管绘制;菜单等无玩家语境让回原生</summary>
        public static bool Draw(Item item, ReadOnlyCollection<TooltipLine> lines, int x, int y) {
            if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
                return true;
            }

            List<LegendKeybindRow> keyRows = [
                LegendTooltipPanel.BuildKeyRow(OnikiriOverride.KeyLabelFlashStep, CWRKeySystem.Onikiri_FlashStep,
                    CWRKeySystem.RightClickFallback.Value),
                LegendTooltipPanel.BuildKeyRow(OnikiriOverride.KeyLabelSakura, CWRKeySystem.Onikiri_SakuraFlight),
                LegendTooltipPanel.BuildKeyRow(OnikiriOverride.KeyLabelExecute, CWRKeySystem.Onikiri_Execute),
                LegendTooltipPanel.BuildKeyRow(OnikiriOverride.KeyLabelDomain, CWRKeySystem.Legend_Domain),
                LegendTooltipPanel.BuildKeyRow(OnikiriOverride.KeyLabelFlip, CWRKeySystem.Onikiri_DomainFlip),
                LegendTooltipPanel.BuildKeyRow(OnikiriOverride.KeyLabelTeleport, CWRKeySystem.Legend_Teleport),
            ];

            LegendTrialInfo trial = LegendTooltipPanel.ReadTrial(item);
            string trialLine = LegendTooltipPanel.BuildTrialLine(trial);
            string nextLine = null;
            string[] worldLines = null;
            if (trial.Valid) {
                if (!trial.Passed && !string.IsNullOrEmpty(trial.NextNames)) {
                    nextLine = OnikiriTrialQuestLine.TrackerBrief.Format(trial.NextNames);
                }
                if (trial.WorldName != null) {
                    worldLines = LegendUpgradeManagerSystem.World_Text0
                        .Format(trial.WorldName, trial.RecordLevel).Split('|');
                }
            }
            string keyDisplay = CWRKeySystem.QuestLog_Key?.GetAssignedKeys() is { Count: > 0 } assigned
                ? assigned[0] : CWRKeySystem.Notbound.Value;
            string questHint = LegendUpgradeManagerSystem.QuestManagerHint.Value.Replace("{KEY}", keyDisplay);

            LegendTooltipPanel.Draw(Main.spriteBatch, item, lines, x, y, new LegendTooltipRequest {
                Skin = skin,
                KeyRows = keyRows,
                Trial = trial,
                TrialLine = trialLine,
                NextLine = nextLine,
                WorldLines = worldLines,
                QuestHint = questHint,
            });
            return false;
        }
    }
}
