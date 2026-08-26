using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.TrialQuests;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI
{
    /// <summary>SHPC 自绘物品面板皮肤:数据硬面(快/直线/切角),布局归 <see cref="LegendTooltipPanel"/></summary>
    internal sealed class SHPCTooltipSkin : LegendTooltipSkin
    {
        /// <summary>shader 蚀边外扩量(px)</summary>
        private const int EdgePad = 12;

        public override Color TextMain => SHPCTheme.Text;
        public override Color TextDim => SHPCTheme.TextDim;
        public override Color KeyLit => SHPCTheme.CyanHi;
        public override Color KeyWarn => new(255, 120, 110);
        public override Color WorldAccent => Color.Gold;

        private static Texture2D Pixel => VaultAsset.placeholder2.Value;
        private static readonly Rectangle PixelSrc = new(0, 0, 1, 1);

        public override void DrawPanel(SpriteBatch sb, Rectangle panel, float time) {
            Effect effect = EffectLoader.SHPCItemPanel?.Value;
            if (effect == null) {
                DrawFallbackPanel(sb, panel);
                return;
            }
            //枪匠数据鉴 shader 面板:切角硬轮廓+数据网格+扫描头+青紫框线,色板内置
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

        /// <summary>CPU 降级:投影+深青蓝实底+边框线,shader 缺编时用</summary>
        private static void DrawFallbackPanel(SpriteBatch sb, Rectangle panel) {
            sb.Draw(Pixel, new Rectangle(panel.X + 2, panel.Y + 3, panel.Width, panel.Height), PixelSrc,
                SHPCTheme.ShadowDark * 0.5f);
            sb.Draw(Pixel, panel, PixelSrc, SHPCTheme.SlotBg * 0.97f);
            sb.Draw(Pixel, new Rectangle(panel.X + 2, panel.Y, panel.Width - 4, 1), PixelSrc,
                SHPCTheme.Cyan * 0.5f);
            sb.Draw(Pixel, new Rectangle(panel.X + 5, panel.Bottom - 1, panel.Width - 10, 1), PixelSrc,
                SHPCTheme.Border * 0.7f);
        }

        public override void DrawDivider(SpriteBatch sb, Vector2 left, Vector2 right, float time) {
            //数据段分隔:1px 线 + 左端亮短块(读作数据头),机械感直线
            int width = (int)(right.X - left.X);
            sb.Draw(Pixel, new Rectangle((int)left.X, (int)left.Y, width, 1), PixelSrc,
                SHPCTheme.Border * 0.8f);
            sb.Draw(Pixel, new Rectangle((int)left.X, (int)left.Y - 1, 10, 3), PixelSrc,
                SHPCTheme.Cyan * 0.75f);
        }

        public override void DrawProgressBar(SpriteBatch sb, Rectangle bar, float fill, bool passed, float time) {
            //数据槽:实底+1px 边,填充分段刻齿(机械读数,非 css 条)
            sb.Draw(Pixel, bar, PixelSrc, SHPCTheme.SlotBg * 0.95f);
            sb.Draw(Pixel, new Rectangle(bar.X, bar.Y, bar.Width, 1), PixelSrc, Color.Black * 0.4f);
            if (passed) {
                float breath = MathF.Sin(time * 2f) * 0.5f + 0.5f;
                sb.Draw(Pixel, new Rectangle(bar.X + 1, bar.Y + 1, bar.Width - 2, bar.Height - 2), PixelSrc,
                    SHPCTheme.Accent * (0.62f + breath * 0.16f));
                return;
            }
            int fillW = (int)(bar.Width * MathHelper.Clamp(fill, 0f, 1f));
            if (fillW <= 0) {
                return;
            }
            //分段刻齿:每 9px 一格,格间 2px 暗缝
            const int segW = 9, gap = 2;
            for (int x = 0; x + 1 < fillW; x += segW) {
                int w = Math.Min(segW - gap, fillW - x);
                sb.Draw(Pixel, new Rectangle(bar.X + x, bar.Y + 1, w, bar.Height - 2), PixelSrc,
                    SHPCTheme.Cyan * 0.72f);
            }
            float pulse = MathF.Sin(time * 3f) * 0.5f + 0.5f;
            sb.Draw(Pixel, new Rectangle(bar.X + Math.Max(0, fillW - 2), bar.Y, 2, bar.Height), PixelSrc,
                SHPCTheme.CyanHi * (0.45f + pulse * 0.4f));
        }

        public override void DecoratePanel(SpriteBatch sb, Rectangle panel, float time) {
            //右下角机器码读数:MOD n/6,与改件区同源
            string tag = $"MOD {SHPCItemTooltipPanel.ModuleTally}/{SHPCData.SlotCount}";
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float scale = 0.6f;
            Vector2 size = font.MeasureString(tag) * scale;
            Utils.DrawBorderString(sb, tag,
                new Vector2(panel.Right - size.X - 10f, panel.Bottom - size.Y - 4f),
                SHPCTheme.TextDim * 0.85f, scale);
        }
    }

    /// <summary>改件区:旧 SHPCModTooltipDraw 右侧小窗融入面板,图标行+加成行</summary>
    internal sealed class SHPCModuleSection : LegendTooltipCustomSection
    {
        private readonly List<(Item item, SHPCModuleItem mod)> modules = [];
        private readonly List<(string text, bool neg)> bonus = [];

        private const float HeaderScale = 0.9f;
        private const float LineScale = 0.85f;
        private const int IconSize = 16;
        private const float RowH = 19f;
        private const float GroupGap = 4f;

        public int ModuleCount => modules.Count;

        /// <summary>每帧从玩家装配状态重建(数据源与旧右侧小窗一致)</summary>
        public void Refresh(Player player) {
            modules.Clear();
            bonus.Clear();
            SHPCPlayer sp = player.GetModPlayer<SHPCPlayer>();
            for (int i = 0; i < SHPCData.SlotCount; i++) {
                Item m = sp.GetModule(i);
                if (m != null && !m.IsAir && m.ModItem is SHPCModuleItem mod) {
                    modules.Add((m, mod));
                }
            }
            ShootContext ctx = SHPCModificationSystem.Resolve(player);
            foreach ((string text, bool neg) in SHPCModuleItem.BuildStatLines(ctx)) {
                if (!string.IsNullOrEmpty(text)) {
                    bonus.Add((text, neg));
                }
            }
        }

        public override float Measure(float contentWidth) {
            if (modules.Count == 0 && bonus.Count == 0) {
                return 0f;
            }
            float height = 0f;
            if (modules.Count > 0) {
                height += RowH + modules.Count * RowH;
            }
            if (bonus.Count > 0) {
                height += (modules.Count > 0 ? GroupGap : 0f) + RowH + bonus.Count * RowH;
            }
            return height;
        }

        public override void Draw(SpriteBatch sb, Vector2 origin, float contentWidth, float time) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float y = origin.Y;

            void DrawRow(string text, Color color, float xOff, float scale) {
                float w = font.MeasureString(text).X;
                float maxW = contentWidth - xOff;
                float drawScale = w > 0f && w * scale > maxW ? maxW / w : scale;
                Utils.DrawBorderString(sb, text, new Vector2(origin.X + xOff, y), color, drawScale);
            }

            if (modules.Count > 0) {
                DrawRow(SHPCModTooltipDraw.InstalledHeader.Value, SHPCTheme.CyanHi, 0f, HeaderScale);
                y += RowH;
                foreach ((Item item, SHPCModuleItem mod) in modules) {
                    SHPCModuleRender.DrawIcon(sb, item,
                        new Vector2(origin.X + IconSize * 0.5f, y + RowH * 0.5f - 1f),
                        IconSize, mod.TintColor, 1f, Main.UIScaleMatrix, mod.TintIntensity);
                    DrawRow(item.Name, mod.TintColor, IconSize + 4f, LineScale);
                    y += RowH;
                }
            }
            if (bonus.Count > 0) {
                if (modules.Count > 0) {
                    y += GroupGap;
                }
                DrawRow(SHPCModTooltipDraw.BonusHeader.Value, SHPCTheme.CyanHi, 0f, HeaderScale);
                y += RowH;
                foreach ((string text, bool neg) in bonus) {
                    DrawRow(text, neg ? new Color(255, 120, 110) : new Color(120, 255, 170), 0f, LineScale);
                    y += RowH;
                }
            }
        }
    }

    /// <summary>SHPC 物品面板入口:组装键位/试炼/改件区,交给共享引擎</summary>
    internal static class SHPCItemTooltipPanel
    {
        private static readonly SHPCTooltipSkin skin = new();
        private static readonly SHPCModuleSection moduleSection = new();

        /// <summary>右下角机器码读数用的改件计数</summary>
        public static int ModuleTally => moduleSection.ModuleCount;

        /// <summary>返回 false=接管绘制;菜单等无玩家语境让回原生</summary>
        public static bool Draw(Item item, ReadOnlyCollection<TooltipLine> lines, int x, int y) {
            if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active) {
                return true;
            }
            moduleSection.Refresh(Main.LocalPlayer);

            List<LegendKeybindRow> keyRows = [
                LegendTooltipPanel.BuildKeyRow(SHPCOverride.KeyLabelDomain, CWRKeySystem.Legend_Domain),
                LegendTooltipPanel.BuildKeyRow(SHPCOverride.KeyLabelWheel, CWRKeySystem.RadialWheel_Key),
                LegendTooltipPanel.BuildKeyRow(SHPCOverride.KeyLabelTeleport, CWRKeySystem.Legend_Teleport),
                LegendTooltipPanel.BuildKeyRow(SHPCOverride.KeyLabelRestart, CWRKeySystem.Legend_Restart),
                LegendTooltipPanel.BuildKeyRow(SHPCOverride.KeyLabelBanish, CWRKeySystem.CyberBanish_Key),
                LegendTooltipPanel.BuildKeyRow(SHPCOverride.KeyLabelFreeze, CWRKeySystem.CyberFreeze_Key),
            ];

            LegendTrialInfo trial = LegendTooltipPanel.ReadTrial(item);
            string trialLine = LegendTooltipPanel.BuildTrialLine(trial);
            string nextLine = null;
            string[] worldLines = null;
            if (trial.Valid) {
                if (!trial.Passed && !string.IsNullOrEmpty(trial.NextNames)) {
                    nextLine = SHPCTrialQuestLine.TrackerBrief.Format(trial.NextNames);
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
                Custom = moduleSection,
            });
            return false;
        }
    }
}
