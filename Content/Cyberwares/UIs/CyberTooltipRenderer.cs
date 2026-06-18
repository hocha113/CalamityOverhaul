using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;

namespace CalamityOverhaul.Content.Cyberwares.UIs
{
    /// <summary>义体自定义 Tooltip，替代原版</summary>
    internal static class CyberTooltipRenderer
    {
        private const float Padding = 10f;
        private const float IconSize = 36f;
        private const float LineSpacing = 18f;
        private const float ScanLineSpeed = 3.5f;

        private static float scanPhase;

        /// <summary>鼠标旁绘制义体 Tooltip</summary>
        public static void DrawTooltip(SpriteBatch sb, Item item, Vector2 mousePos) {
            if (item == null || item.IsAir) return;
            if (item.ModItem is not BaseCyberware cyber) return;

            Texture2D px = CWRAsset.Placeholder_White?.Value;
            if (px == null) return;

            scanPhase += (float)Main.gameTimeCache.ElapsedGameTime.TotalSeconds * ScanLineSpeed;
            if (scanPhase > MathHelper.TwoPi) scanPhase -= MathHelper.TwoPi;

            //收集文本行
            string name = item.Name ?? "???";
            string slotName = CyberwareUI.Instance?.GetSlotLabel((int)cyber.SlotCategory) ?? cyber.SlotCategory.ToString();
            string capText = Language.GetTextValue("Mods.CalamityOverhaul.UI.CyberwareUI.TooltipCapacityCost", cyber.CapacityCost);
            string desc = cyber.Tooltip.Value;

            //测量尺寸
            float textScale = 0.76f;
            float smallScale = 0.66f;
            float descScale = 0.64f;

            Vector2 nameSize = CWRUtils.MeasureText(name, textScale);
            Vector2 slotSize = CWRUtils.MeasureText(slotName, smallScale);

            //计算描述文本行，先按换行符分段再调用官方换行接口
            var descLineList = new System.Collections.Generic.List<string>();
            foreach (string para in desc.Split('\n')) {
                foreach (string wl in CWRUtils.WrapTextArray(para.TrimEnd('\r'), FontAssets.MouseText.Value, (int)(220f / descScale), 99, out _)) {
                    if (wl != null) descLineList.Add(wl);
                }
            }
            string[] descLines = [.. descLineList];

            float contentWidth = Math.Max(nameSize.X + IconSize + Padding, 200f);
            contentWidth = Math.Max(contentWidth, slotSize.X + 80f);
            foreach (string line in descLines) {
                contentWidth = Math.Max(contentWidth, CWRUtils.MeasureText(line, descScale).X);
            }
            contentWidth += Padding * 2;

            float contentHeight = Padding; //顶部
            contentHeight += 20; //名称行
            contentHeight += 4; //间隔
            contentHeight += 14; //分隔线区域
            contentHeight += LineSpacing; //槽位行
            contentHeight += LineSpacing; //容量行
            if (descLines.Length > 0) {
                contentHeight += 8; //描述前间距
                contentHeight += descLines.Length * (LineSpacing - 2);
            }
            contentHeight += Padding; //底部

            //面板位置（在鼠标旁边，避免超出屏幕）
            float panelX = mousePos.X + 16;
            float panelY = mousePos.Y + 16;
            if (panelX + contentWidth > Main.screenWidth - 8) {
                panelX = mousePos.X - contentWidth - 8;
            }
            if (panelY + contentHeight > Main.screenHeight - 8) {
                panelY = mousePos.Y - contentHeight - 8;
            }
            panelX = Math.Max(8, panelX);
            panelY = Math.Max(8, panelY);

            Rectangle panelRect = new((int)panelX, (int)panelY, (int)contentWidth, (int)contentHeight);

            //=== 绘制面板 ===

            //背景
            sb.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), CyberwareTheme.BgPanel * 0.95f);

            //暗角
            for (int i = 0; i < 4; i++) {
                float fade = 1f - i / 4f;
                Color vig = CyberwareTheme.InnerShadow * (0.5f * fade);
                sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y + i, panelRect.Width, 1), new Rectangle(0, 0, 1, 1), vig);
                sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - 1 - i, panelRect.Width, 1), new Rectangle(0, 0, 1, 1), vig);
            }

            //顶部红色边框
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 2), new Rectangle(0, 0, 1, 1), CyberwareTheme.Accent * 0.8f);
            //底部边框
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Bottom - 1, panelRect.Width, 1), new Rectangle(0, 0, 1, 1), CyberwareTheme.Border * 0.6f);
            //左右边框
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, 1, panelRect.Height), new Rectangle(0, 0, 1, 1), CyberwareTheme.Accent * 0.4f);
            sb.Draw(px, new Rectangle(panelRect.Right - 1, panelRect.Y, 1, panelRect.Height), new Rectangle(0, 0, 1, 1), CyberwareTheme.Accent * 0.4f);

            //斜切角
            int cut = 3;
            sb.Draw(px, new Rectangle(panelRect.X, panelRect.Y, cut, cut), new Rectangle(0, 0, 1, 1), CyberwareTheme.BgDark);
            sb.Draw(px, new Rectangle(panelRect.Right - cut, panelRect.Y, cut, cut), new Rectangle(0, 0, 1, 1), CyberwareTheme.BgDark);

            //微扫描线
            float scanY = panelRect.Y + scanPhase / MathHelper.TwoPi % 1f * panelRect.Height;
            sb.Draw(px, new Rectangle(panelRect.X + 1, (int)scanY, panelRect.Width - 2, 1),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.Accent * 0.06f);

            //=== 绘制内容 ===
            float x = panelRect.X + Padding;
            float y = panelRect.Y + Padding;

            //物品图标
            Texture2D itemTex = TextureAssets.Item[item.type]?.Value;
            if (itemTex != null) {
                float maxDim = Math.Max(itemTex.Width, itemTex.Height);
                float iconScale = maxDim > IconSize ? IconSize / maxDim : 1f;
                Vector2 iconCenter = new(x + IconSize / 2f, y + 10);
                sb.Draw(itemTex, iconCenter, null, Color.White, 0f, itemTex.Size() / 2f, iconScale, SpriteEffects.None, 0f);
            }

            //名称
            Utils.DrawBorderString(sb, name, new Vector2(x + IconSize + 6, y), CyberwareTheme.TextBright, textScale);
            y += 20;

            //分隔线
            y += 4;
            sb.Draw(px, new Rectangle((int)x, (int)y, panelRect.Width - (int)(Padding * 2), 1),
                new Rectangle(0, 0, 1, 1), CyberwareTheme.Accent * 0.35f);
            y += 10;

            //槽位分类
            Utils.DrawBorderString(sb, Language.GetTextValue("Mods.CalamityOverhaul.UI.CyberwareUI.TooltipSlot"), new Vector2(x, y), CyberwareTheme.TextDim, smallScale);
            Utils.DrawBorderString(sb, slotName, new Vector2(x + 42, y), CyberwareTheme.AccentCyan, smallScale);
            y += LineSpacing;

            //容量消耗
            Utils.DrawBorderString(sb, capText, new Vector2(x, y),
                CyberwareTheme.AccentGold, smallScale);
            y += LineSpacing;

            //自定义描述
            if (descLines.Length > 0) {
                y += 4;
                sb.Draw(px, new Rectangle((int)x, (int)y, panelRect.Width - (int)(Padding * 2), 1),
                    new Rectangle(0, 0, 1, 1), CyberwareTheme.Border * 0.3f);
                y += 4;

                foreach (string line in descLines) {
                    Utils.DrawBorderString(sb, line, new Vector2(x, y), CyberwareTheme.TextNormal, descScale);
                    y += LineSpacing - 2;
                }
            }
        }

    }
}
