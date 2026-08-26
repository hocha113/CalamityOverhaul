using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.TrialQuests;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 鬼伞自绘物品面板皮肤:主题恒定鬼雨冷灰青(用户裁定 2026-08——物品面板的主题
    /// 是鬼雨不是血湖,禁红),不随领域形态浸染;布局归 <see cref="LegendTooltipPanel"/>
    /// </summary>
    internal sealed class KikasaTooltipSkin : LegendTooltipSkin
    {
        /// <summary>色板恒取鬼雨端</summary>
        private const float Rain = 1f;

        /// <summary>shader 蚀边外扩量(px)</summary>
        private const int EdgePad = 12;

        public override Color TextMain => KikasaHudTheme.Text(Rain);
        public override Color TextDim => KikasaHudTheme.TextDim(Rain);
        public override Color KeyLit => KikasaHudTheme.Glow(Rain);
        public override Color KeyWarn => new(214, 78, 84);
        public override Color WorldAccent => Color.Gold;

        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        public override void DrawPanel(SpriteBatch sb, Rectangle panel, float time) {
            Effect effect = EffectLoader.KikasaItemPanel?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                //缺噪声不能进 shader:s1 残留脏纹理会让雨层采出错值
                DrawFallbackPanel(sb, panel, time);
                return;
            }
            //伞下水鏡 shader 面板:湿墨静场+淅沥小雨+伞盖弧+溺月+底沿积水线
            Rectangle extRect = panel;
            extRect.Inflate(EdgePad, EdgePad);
            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(0.97f);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(extRect.Width, extRect.Height));
            effect.Parameters["uEdgePad"]?.SetValue((float)EdgePad);
            effect.Parameters["uColVoid"]?.SetValue(KikasaHudTheme.Void(Rain).ToVector3());
            effect.Parameters["uColDeep"]?.SetValue(KikasaHudTheme.Deep(Rain).ToVector3());
            effect.Parameters["uColRain"]?.SetValue(KikasaHudTheme.Accent(Rain).ToVector3());
            effect.Parameters["uColMoon"]?.SetValue(KikasaHudTheme.Glow(Rain).ToVector3());

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);
            GraphicsDevice device = Main.instance.GraphicsDevice;
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            sb.Draw(Pixel, extRect, Color.White);
            sb.End();
            //还原 tooltip 层原批次(Deferred + null 默认参数)
            sb.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
        }

        /// <summary>CPU 降级:投影+暗水玻璃实底+水线,shader 缺编时用</summary>
        private void DrawFallbackPanel(SpriteBatch sb, Rectangle panel, float time) {
            sb.Draw(Pixel, new Rectangle(panel.X + 2, panel.Y + 3, panel.Width, panel.Height), PixelSrc,
                Color.Black * 0.45f);
            sb.Draw(Pixel, panel, PixelSrc, KikasaHudTheme.Void(Rain) * 0.97f);
            float breath = KikasaHudTheme.Breath(time, 3.1f);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(panel.X + 2, panel.Y),
                new Vector2(panel.Right - 2, panel.Y), 1.4f,
                KikasaHudTheme.Glow(Rain) * (0.42f + breath * 0.18f));
            KikasaVaultRenderer.DrawLine(sb, new Vector2(panel.X + 6, panel.Bottom - 1),
                new Vector2(panel.Right - 6, panel.Bottom - 1), 1f,
                KikasaHudTheme.Accent(Rain) * 0.38f);
        }

        public override void DrawDivider(SpriteBatch sb, Vector2 left, Vector2 right, float time) {
            KikasaVaultRenderer.DrawLine(sb, left, right, 1f, KikasaHudTheme.Accent(Rain) * 0.42f);
            //左端一点溺月水光
            sb.Draw(Pixel, left + new Vector2(1f, -0.4f), PixelSrc, KikasaHudTheme.Glow(Rain) * 0.5f,
                0f, new Vector2(0.5f), new Vector2(9f, 1.6f), SpriteEffects.None, 0f);
        }

        public override void DrawProgressBar(SpriteBatch sb, Rectangle bar, float fill, bool passed, float time) {
            //槽底+上缘沉线
            sb.Draw(Pixel, bar, PixelSrc, KikasaHudTheme.Deep(Rain) * 0.92f);
            KikasaVaultRenderer.DrawLine(sb, new Vector2(bar.X, bar.Y),
                new Vector2(bar.Right, bar.Y), 1f, Color.Black * 0.35f);
            if (passed) {
                float breath = KikasaHudTheme.Breath(time, 5.3f);
                sb.Draw(Pixel, new Rectangle(bar.X + 1, bar.Y + 1, bar.Width - 2, bar.Height - 2), PixelSrc,
                    KikasaHudTheme.Glow(Rain) * (0.62f + breath * 0.18f));
                return;
            }
            int fillW = (int)(bar.Width * MathHelper.Clamp(fill, 0f, 1f));
            if (fillW <= 0) {
                return;
            }
            //水位涨线:体色+前沿波光立柱
            sb.Draw(Pixel, new Rectangle(bar.X, bar.Y + 1, fillW, bar.Height - 2), PixelSrc,
                KikasaHudTheme.Accent(Rain) * 0.85f);
            float pulse = KikasaHudTheme.Breath(time, 7.7f, 3f);
            sb.Draw(Pixel, new Rectangle(bar.X + Math.Max(0, fillW - 2), bar.Y, 2, bar.Height), PixelSrc,
                KikasaHudTheme.Glow(Rain) * (0.45f + pulse * 0.4f));
        }

        public override void DecoratePanel(SpriteBatch sb, Rectangle panel, float time) {
            //底缘檐角垂珠:血湖滴血,鬼雨滴水;聚珠-坠落两拍循环
            Vector2 half = new(0.5f);
            Vector2 anchor = new(panel.Right - 18f, panel.Bottom);
            Color body = KikasaHudTheme.Accent(Rain);
            Color lit = KikasaHudTheme.Glow(Rain);
            sb.Draw(Pixel, anchor, PixelSrc, body * 0.35f, 0f, half, new Vector2(3.2f, 1.1f), SpriteEffects.None, 0f);
            float cycle = (time * 0.55f + 0.31f) % 1f;
            if (cycle < 0.65f) {
                float grow = cycle / 0.65f;
                sb.Draw(Pixel, anchor + new Vector2(0f, 1.2f + grow * 1.6f), PixelSrc,
                    Color.Lerp(body, lit, grow * 0.5f) * (0.3f + grow * 0.45f),
                    0f, half, new Vector2(1.2f + grow * 1.2f, 1.6f + grow * 2.2f), SpriteEffects.None, 0f);
            }
            else if (cycle < 0.9f) {
                float fall = (cycle - 0.65f) / 0.25f;
                sb.Draw(Pixel, anchor + new Vector2(0f, 3f + fall * fall * 15f), PixelSrc,
                    lit * (0.55f * (1f - fall)), 0f, half, new Vector2(1.5f, 2.5f), SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>鬼伞物品面板入口:组装键位/试炼数据,交给共享引擎</summary>
    internal static class KikasaItemTooltipPanel
    {
        private static readonly KikasaTooltipSkin skin = new();

        /// <summary>返回 false=接管绘制;菜单等无玩家语境让回原生</summary>
        public static bool Draw(Item item, ReadOnlyCollection<TooltipLine> lines, int x, int y) {
            if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
                return true;
            }

            List<LegendKeybindRow> keyRows = [
                LegendTooltipPanel.BuildKeyRow(KikasaOverride.KeyLabelDomain, CWRKeySystem.Legend_Domain),
                LegendTooltipPanel.BuildKeyRow(KikasaOverride.KeyLabelSink, CWRKeySystem.Kikasa_Sink),
                LegendTooltipPanel.BuildKeyRow(KikasaOverride.KeyLabelMutate, CWRKeySystem.Kikasa_DomainMutate,
                    KikasaOverride.MutateFallback.Value),
                LegendTooltipPanel.BuildKeyRow(KikasaOverride.KeyLabelWheel, CWRKeySystem.RadialWheel_Key),
                LegendTooltipPanel.BuildKeyRow(KikasaOverride.KeyLabelRestart, CWRKeySystem.Legend_Restart),
                LegendTooltipPanel.BuildKeyRow(KikasaOverride.KeyLabelTeleport, CWRKeySystem.Legend_Teleport),
                LegendTooltipPanel.BuildKeyRow(KikasaOverride.KeyLabelPanorama, CWRKeySystem.Legend_UIControl),
            ];

            LegendTrialInfo trial = LegendTooltipPanel.ReadTrial(item);
            string trialLine = LegendTooltipPanel.BuildTrialLine(trial);
            string nextLine = null;
            string[] worldLines = null;
            if (trial.Valid) {
                if (!trial.Passed && !string.IsNullOrEmpty(trial.NextNames)) {
                    nextLine = KikasaTrialQuestLine.TrackerBrief.Format(trial.NextNames);
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
