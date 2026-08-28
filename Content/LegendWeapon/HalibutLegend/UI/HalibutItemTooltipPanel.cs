using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.TrialQuests;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>比目鱼自绘物品面板皮肤:有机深海(缓/曲线/光柱),布局归 <see cref="LegendTooltipPanel"/></summary>
    internal sealed class HalibutTooltipSkin : LegendTooltipSkin
    {
        /// <summary>shader 蚀边外扩量(px)</summary>
        private const int EdgePad = 12;

        public override Color TextMain => HalibutTheme.Text;
        public override Color TextDim => HalibutTheme.TextDim;
        public override Color KeyLit => HalibutTheme.Glow;
        public override Color KeyWarn => HalibutTheme.Danger;
        public override Color WorldAccent => Color.Gold;

        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        public override void DrawPanel(SpriteBatch sb, Rectangle panel, float time) {
            Effect effect = EffectLoader.HalibutItemPanel?.Value;
            if (effect == null) {
                DrawFallbackPanel(sb, panel);
                return;
            }
            //深海鉴 shader 面板:水下体积+顶沿海面波光+斜射光柱+缓升气泡,色板内置
            Rectangle extRect = panel;
            extRect.Inflate(EdgePad, EdgePad);
            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(0.97f);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
            effect.Parameters["uEdgePad"]?.SetValue((float)EdgePad);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            sb.Draw(Pixel, extRect, Color.White);
            sb.End();
            //还原 tooltip 层原批次(Deferred + null 默认参数)
            sb.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
        }

        /// <summary>CPU 降级:投影+深海底色+冷光顶线,shader 缺编时用</summary>
        private static void DrawFallbackPanel(SpriteBatch sb, Rectangle panel) {
            sb.Draw(Pixel, new Rectangle(panel.X + 2, panel.Y + 3, panel.Width, panel.Height), PixelSrc,
                Color.Black * 0.45f);
            sb.Draw(Pixel, panel, PixelSrc, HalibutTheme.PanelBg * 0.97f);
            sb.Draw(Pixel, new Rectangle(panel.X + 2, panel.Y, panel.Width - 4, 1), PixelSrc,
                HalibutTheme.Glow * 0.5f);
            sb.Draw(Pixel, new Rectangle(panel.X + 5, panel.Bottom - 1, panel.Width - 10, 1), PixelSrc,
                HalibutTheme.Teal * 0.6f);
        }

        public override void DrawDivider(SpriteBatch sb, Vector2 left, Vector2 right, float time) {
            //一线缓流:细线 + 左端一粒气泡亮点
            int width = (int)(right.X - left.X);
            sb.Draw(Pixel, new Rectangle((int)left.X, (int)left.Y, width, 1), PixelSrc,
                HalibutTheme.Teal * 0.85f);
            float breath = HalibutTheme.Breath(time, 4.1f, 1.4f);
            sb.Draw(Pixel, left + new Vector2(2f, -0.5f), PixelSrc,
                HalibutTheme.GlowHi * (0.35f + breath * 0.25f),
                0f, new Vector2(0.5f), new Vector2(3f, 2f), SpriteEffects.None, 0f);
        }

        public override void DrawProgressBar(SpriteBatch sb, Rectangle bar, float fill, bool passed, float time) {
            //深度计:深海槽底+冷光水位
            sb.Draw(Pixel, bar, PixelSrc, HalibutTheme.Deep * 0.95f);
            sb.Draw(Pixel, new Rectangle(bar.X, bar.Y, bar.Width, 1), PixelSrc, Color.Black * 0.4f);
            if (passed) {
                //通关=暖金满灌
                float breath = HalibutTheme.Breath(time, 5.9f);
                sb.Draw(Pixel, new Rectangle(bar.X + 1, bar.Y + 1, bar.Width - 2, bar.Height - 2), PixelSrc,
                    HalibutTheme.Accent * (0.66f + breath * 0.16f));
                return;
            }
            int fillW = (int)(bar.Width * MathHelper.Clamp(fill, 0f, 1f));
            if (fillW <= 0) {
                return;
            }
            sb.Draw(Pixel, new Rectangle(bar.X, bar.Y + 1, fillW, bar.Height - 2), PixelSrc,
                HalibutTheme.Glow * 0.72f);
            float pulse = HalibutTheme.Breath(time, 8.3f, 3f);
            sb.Draw(Pixel, new Rectangle(bar.X + Math.Max(0, fillW - 2), bar.Y, 2, bar.Height), PixelSrc,
                HalibutTheme.GlowHi * (0.45f + pulse * 0.4f));
        }
    }

    /// <summary>比目鱼物品面板入口:组装键位/试炼数据,交给共享引擎</summary>
    internal static class HalibutItemTooltipPanel
    {
        private static readonly HalibutTooltipSkin skin = new();

        /// <summary>返回 false=接管绘制;菜单等无玩家语境让回原生</summary>
        public static bool Draw(Item item, ReadOnlyCollection<TooltipLine> lines, int x, int y) {
            if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
                return true;
            }

            List<LegendKeybindRow> keyRows = [
                LegendTooltipPanel.BuildKeyRow(HalibutOverride.KeyLabelDomain, CWRKeySystem.Legend_Domain),
                LegendTooltipPanel.BuildKeyRow(HalibutOverride.KeyLabelClone, CWRKeySystem.Halibut_Clone),
                LegendTooltipPanel.BuildKeyRow(HalibutOverride.KeyLabelSuperpose, CWRKeySystem.Halibut_Superposition),
                //七眼起重启升格为大范围重启，键位行同步改口
                LegendTooltipPanel.BuildKeyRow(
                    HalibutData.GetDomainLayer() >= DomainSkills.Restarts.HalibutReset.UnlockLayers
                        ? HalibutOverride.KeyLabelRestartWide : HalibutOverride.KeyLabelRestart,
                    CWRKeySystem.Legend_Restart),
                LegendTooltipPanel.BuildKeyRow(HalibutOverride.KeyLabelTeleport, CWRKeySystem.Legend_Teleport),
                LegendTooltipPanel.BuildKeyRow(HalibutOverride.KeyLabelWheel, CWRKeySystem.RadialWheel_Key),
                LegendTooltipPanel.BuildKeyRow(HalibutOverride.KeyLabelAtlas, CWRKeySystem.Legend_UIControl),
            ];

            LegendTrialInfo trial = LegendTooltipPanel.ReadTrial(item);
            string trialLine = LegendTooltipPanel.BuildTrialLine(trial);
            string nextLine = null;
            string[] worldLines = null;
            if (trial.Valid) {
                if (!trial.Passed && !string.IsNullOrEmpty(trial.NextNames)) {
                    nextLine = HalibutTrialQuestLine.TrackerBrief.Format(trial.NextNames);
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
