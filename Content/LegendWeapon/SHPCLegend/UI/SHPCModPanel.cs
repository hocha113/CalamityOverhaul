using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI
{
    /// <summary>
    /// SHPC枪体改造面板
    /// 中央悬浮SHPC物品纹理，数据分析线从枪体关键部位延伸连接六个改件插槽
    /// 由 <see cref="SHPCUI"/> 在固定二级面板模式下调用（按钮索引1）
    /// </summary>
    internal static class SHPCModPanel
    {
        //空间布局缩放系数，面板/插槽偏移/枪体处于该系数下，略大以缓解拥挤感
        private const float Scale = 1f;
        //字号缩放系数，与面板状态区保持一致比例
        private const float FontScale = 1.2f;

        public const float PanelW = 300f * Scale;
        private const float PresetBarH = 38f * Scale;
        public const float PanelH = 260f * Scale;
        private const float EdgePad = 12f * Scale;

        private const int SlotCount = 6;
        private const float SlotW = 56f * Scale;
        private const float SlotH = 22f * Scale;

        private const int PresetCount = SHPCPlayer.PresetCount;

        //六个插槽中心相对于枪体中心的偏移
        private static readonly Vector2[] SlotOffsets = {
            new(84f * Scale, -38f * Scale),      //BARREL  枪管  右上（枪管向前延伸侧）
            new(0f, -76f * Scale),               //OPTIC   瞄具  正上（镜座自然安装位）
            new(84f * Scale, 38f * Scale),       //POWER   能源  右下
            new(-84f * Scale, -38f * Scale),     //STOCK   枪托  左上
            new(-84f * Scale, 38f * Scale),      //GRIP    握把  左下
            new(0f, 76f * Scale),                //FRAME   机匣  正下
        };

        //枪体纹理显示缩放，SHPC原始贴图约82×26px，1.4x时绘制尺寸约115×36
        private static float GunScale => 1.4f;

        //数据线在枪体纹理上的接出点，坐标单位为屏幕像素（相对枪体绘制中心）
        //以枪口朝右为基准，对应SHPC贴图各功能区域的边缘位置，按 GunScale 同步缩放
        //如果实际贴图尺寸与预估不符可按照以下规律等比调整：
        //  X轴：负值朝左（枪托侧），正值朝右（枪口侧），最大约±82*GunScale/2
        //  Y轴：负值朝上（瞄具/枪管顶），正值朝下（握把/弹匣底），最大约±26*GunScale/2
        private const float ConnectFactor = 1.4f / 1.2f;
        private static readonly Vector2[] ConnectPoints = {
            new(62f * ConnectFactor, -20f * ConnectFactor),  //BARREL  右上引出  枪口侧上方
            new(5f * ConnectFactor, -24f * ConnectFactor),   //OPTIC   向上引出  枪管顶部镜座
            new(62f * ConnectFactor, 18f * ConnectFactor),   //POWER   右下引出  枪口侧下方
            new(-62f * ConnectFactor, -18f * ConnectFactor), //STOCK   左上引出  枪托侧上方
            new(-15f * ConnectFactor, 26f * ConnectFactor),  //GRIP    左下引出  握把底部
            new(-55f * ConnectFactor, 26f * ConnectFactor),  //FRAME   向下引出  机匣底部
        };

        //槽位显示标签通过本地化系统获取，避免硬编码英文
        private static string GetSlotLabel(int i) => i switch {
            0 => SHPCUI.Modify_Slot_Barrel.Value,
            1 => SHPCUI.Modify_Slot_Optic.Value,
            2 => SHPCUI.Modify_Slot_Power.Value,
            3 => SHPCUI.Modify_Slot_Stock.Value,
            4 => SHPCUI.Modify_Slot_Grip.Value,
            5 => SHPCUI.Modify_Slot_Frame.Value,
            _ => "?",
        };

        public enum HitKind
        {
            None,
            Slot0, Slot1, Slot2, Slot3, Slot4, Slot5,
            Preset0, Preset1, Preset2,
        }

        public ref struct Layout
        {
            public Rectangle Panel;
            public Vector2 GunCenter;
        }

        public static Rectangle GetSlotRect(in Layout layout, int idx) {
            Vector2 off = SlotOffsets[idx];
            return new Rectangle(
                (int)(layout.GunCenter.X + off.X - SlotW * 0.5f),
                (int)(layout.GunCenter.Y + off.Y - SlotH * 0.5f),
                (int)SlotW, (int)SlotH);
        }

        public static Layout Compute(Vector2 anchor, float midAngle, float panelAlpha) {
            Vector2 outDir = SHPCRenderer.AngleDir(midAngle);
            float slide = (1f - panelAlpha) * 14f;
            Vector2 panelPos = anchor + outDir * (SHPCTheme.InfoPanelGap + slide);
            panelPos.Y -= PanelH * 0.5f;
            Rectangle panel = new((int)panelPos.X, (int)panelPos.Y, (int)PanelW, (int)PanelH);
            //枪体中心在内容区中心基础上下移16px，使上方槽位不贴边、下方机匣槽靠近预设栏
            Vector2 gunCenter = new(panel.X + PanelW * 0.5f, panel.Y + (PanelH - PresetBarH) * 0.5f + 16f);
            return new Layout { Panel = panel, GunCenter = gunCenter };
        }

        private static Rectangle GetPresetBtnRect(in Layout layout, int idx) {
            float barTop = layout.Panel.Y + PanelH - PresetBarH;
            float btnH = 22f * Scale;
            float btnY = barTop + (PresetBarH - btnH) * 0.5f;
            float totalW = PanelW - EdgePad * 2f;
            float gap = 5f * Scale;
            float btnW = (totalW - gap * (PresetCount - 1)) / PresetCount;
            float btnX = layout.Panel.X + EdgePad + idx * (btnW + gap);
            return new Rectangle((int)btnX, (int)btnY, (int)btnW, (int)btnH);
        }

        public static HitKind HitTest(in Layout layout, Vector2 mouse) {
            for (int i = 0; i < SlotCount; i++) {
                if (GetSlotRect(layout, i).Contains((int)mouse.X, (int)mouse.Y)) {
                    return (HitKind)(i + 1);
                }
            }
            for (int i = 0; i < PresetCount; i++) {
                if (GetPresetBtnRect(layout, i).Contains((int)mouse.X, (int)mouse.Y)) {
                    return HitKind.Preset0 + i;
                }
            }
            return HitKind.None;
        }

        public static void HandleClick(HitKind hit, Player owner) {
            if (hit >= HitKind.Slot0 && hit <= HitKind.Slot5) {
                int slotIdx = (int)hit - 1;
                SHPCUI ui = SHPCUI.Instance;
                if (ui == null) {
                    return;
                }
                if (ui.PinnedModuleSlot == slotIdx) {
                    ui.CloseModuleSelect();
                }
                else {
                    ui.OpenModuleSelect(slotIdx);
                }
                return;
            }
            if (hit >= HitKind.Preset0 && hit <= HitKind.Preset2) {
                int presetIdx = hit - HitKind.Preset0;
                SHPCPlayer sp = SHPCPlayer.Get(owner);
                sp.SwitchPreset(presetIdx);
                //切换预设时关闭模块选择面板避免错位显示
                SHPCUI.Instance?.CloseModuleSelect();
            }
        }

        public static void Draw(SpriteBatch sb, Texture2D px, in Layout layout,
            float panelAlpha, float globalAlpha, HitKind hover) {
            if (panelAlpha < 0.02f) {
                return;
            }
            float a = panelAlpha * globalAlpha;
            float time = (float)Main.GameUpdateCount / 60f;
            Rectangle rect = layout.Panel;
            Vector2 gun = layout.GunCenter;

            //投影
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(rect.X + 3, rect.Y + 4, rect.Width, rect.Height),
                new Color(0, 0, 0) * (0.55f * a));

            //着色器背景（肩负背景填充、扫描线、中央光场、内边柔光）
            DrawShaderBackground(sb, px, rect, gun, panelAlpha, globalAlpha);

            //外框与四角L形装饰
            SHPCRenderer.DrawRectStroke(sb, px, rect, 1.2f, SHPCTheme.Border * (0.9f * a));
            SHPCRenderer.DrawCornerBrackets(sb, px, rect, 10f * Scale, 1.5f, SHPCTheme.BorderHi * (0.9f * a));

            //顶部青色色带
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(rect.X, rect.Y, rect.Width, (int)(3 * Scale)),
                SHPCTheme.Cyan * (0.85f * a));

            DynamicSpriteFont font = FontAssets.MouseText.Value;

            //面板标题
            Utils.DrawBorderString(sb, SHPCUI.Modify_Title.Value,
                new Vector2(rect.X + 10f * Scale, rect.Y + 7f * Scale), SHPCTheme.Text * a, 0.72f * FontScale);
            Utils.DrawBorderString(sb, SHPCUI.Modify_Subtitle.Value,
                new Vector2(rect.X + 10f * Scale, rect.Y + 24f * Scale), SHPCTheme.TextDim * a, 0.52f * FontScale);

            //右上滚动ID码，增强科技感
            string idCode = $"SYS#{(int)(time * 13f) % 9999:D4}";
            Vector2 idSz = font.MeasureString(idCode) * (0.42f * FontScale);
            Utils.DrawBorderString(sb, idCode,
                new Vector2(rect.Right - 10f * Scale - idSz.X, rect.Y + 9f * Scale),
                SHPCTheme.Cyan * (0.70f * a), 0.42f * FontScale);

            //数据分析线（绘于枪体下方）
            DrawDataLines(sb, px, gun, hover, time, a);

            //六个改件槽位
            SHPCPlayer sp = Main.LocalPlayer != null ? SHPCPlayer.Get(Main.LocalPlayer) : null;
            for (int i = 0; i < SlotCount; i++) {
                Rectangle slotRect = GetSlotRect(layout, i);
                bool isHover = hover == (HitKind)(i + 1);
                bool isPinned = SHPCUI.Instance?.PinnedModuleSlot == i;
                Item equipped = sp?.GetModule(i);
                DrawSlot(sb, px, font, slotRect, GetSlotLabel(i), isHover || isPinned, equipped, a);
            }

            //SHPC枪体纹理（绘于最上层，置于分析线和槽位之上）
            DrawGunTexture(sb, px, gun, time, a);

            //悬停已装配槽位时在槽位正上方显示模块信息卡
            if (hover >= HitKind.Slot0 && hover <= HitKind.Slot5) {
                int hoverIdx = (int)hover - 1;
                Item equipped = sp?.GetModule(hoverIdx);
                if (equipped != null && !equipped.IsAir) {
                    Rectangle slotRect = GetSlotRect(layout, hoverIdx);
                    DrawEquippedTooltip(sb, px, font, slotRect, equipped, a);
                }
            }

            //底部预设切换栏
            DrawPresetBar(sb, px, font, layout, hover, a);
        }

        private static void DrawShaderBackground(SpriteBatch sb, Texture2D px,
            Rectangle rect, Vector2 gunCenter, float panelAlpha, float globalAlpha) {
            float a = panelAlpha * globalAlpha;
            //着色器未加载时降级为纯色背景
            if (EffectLoader.SHPCModPanel?.Value == null) {
                SHPCRenderer.DrawFilledRect(sb, px, rect, new Color(4, 14, 22) * (0.96f * a));
                return;
            }

            Effect effect = EffectLoader.SHPCModPanel.Value;
            float time = (float)Main.GameUpdateCount / 60f;

            //枪体光场中心转换为面板局部坐标
            Vector2 gunRel = new(gunCenter.X - rect.X, gunCenter.Y - rect.Y);
            float gunRadius = 60f * Scale;

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(a * 0.97f);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uEdgePad"]?.SetValue(EdgePad);
            effect.Parameters["uGunCenter"]?.SetValue(gunRel);
            effect.Parameters["uGunRadius"]?.SetValue(gunRadius);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(px, rect, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp, DepthStencilState.None,
                RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }

        private static void DrawDataLines(SpriteBatch sb, Texture2D px,
            Vector2 gun, HitKind hover, float time, float a) {
            for (int i = 0; i < SlotCount; i++) {
                bool isHover = hover == (HitKind)(i + 1);
                Vector2 start = gun + ConnectPoints[i];
                Vector2 slotCenter = gun + SlotOffsets[i];

                Color lineCol = isHover
                    ? SHPCTheme.CyanHi * (0.85f * a)
                    : SHPCTheme.Border * (0.55f * a);

                //折线：上下槽先横后竖，左右槽先竖后横
                Vector2 mid = (i == 0 || i == 3)
                    ? new Vector2(slotCenter.X, start.Y)
                    : new Vector2(start.X, slotCenter.Y);

                SHPCRenderer.DrawLine(sb, px, start, mid, 1.2f, lineCol);
                SHPCRenderer.DrawLine(sb, px, mid, slotCenter, 1.2f, lineCol);

                //折点处的菱形节点
                SHPCRenderer.DrawFilledRect(sb, px,
                    new Rectangle((int)(mid.X - 2 * Scale), (int)(mid.Y - 2 * Scale), (int)(4 * Scale), (int)(4 * Scale)),
                    lineCol);

                //枪体接出点的小方块
                SHPCRenderer.DrawFilledRect(sb, px,
                    new Rectangle((int)(start.X - 2 * Scale), (int)(start.Y - 2 * Scale), (int)(4 * Scale), (int)(4 * Scale)),
                    lineCol * 1.2f);

                //悬停时线上增加流动脉冲效果
                if (isHover) {
                    float t = (time * 1.6f) % 1f;
                    Vector2 pulseA = Vector2.Lerp(start, mid, t);
                    Vector2 pulseB = Vector2.Lerp(mid, slotCenter, t);
                    Vector2 pulsePos = t < 0.5f ? pulseA : pulseB;
                    SHPCRenderer.DrawDisc(sb, px, pulsePos, 2.2f * Scale, 2f,
                        SHPCTheme.CyanHi * (0.8f * a));
                }
            }
        }

        private static void DrawSlot(SpriteBatch sb, Texture2D px,
            DynamicSpriteFont font, Rectangle r, string label, bool isHover, Item equipped, float a) {
            //投影
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(r.X + 2, r.Y + 2, r.Width, r.Height),
                new Color(0, 0, 0) * (0.4f * a));

            //背景
            Color bg = isHover
                ? new Color(12, 50, 70) * (0.92f * a)
                : new Color(6, 20, 30) * (0.85f * a);
            SHPCRenderer.DrawFilledRect(sb, px, r, bg);

            //描边
            Color border = isHover
                ? SHPCTheme.CyanHi * (0.95f * a)
                : SHPCTheme.Border * (0.75f * a);
            SHPCRenderer.DrawRectStroke(sb, px, r, 1.2f, border);

            //悬停时四角装饰
            if (isHover) {
                SHPCRenderer.DrawCornerBrackets(sb, px, r, 4f * Scale, 1.2f, SHPCTheme.CyanHi * a);
            }

            //左侧状态色条（空槽为暗色，已安装时可改为亮色）
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(r.X, r.Y, (int)(3 * Scale), r.Height),
                (isHover ? SHPCTheme.Cyan : SHPCTheme.Border) * (0.8f * a));

            //槽位标签
            float labelScale = 0.50f * FontScale;
            Vector2 labelSz = font.MeasureString(label) * labelScale;
            Utils.DrawBorderString(sb, label,
                new Vector2(r.X + 7f * Scale, r.Y + (r.Height - labelSz.Y) * 0.5f),
                (isHover ? SHPCTheme.Text : SHPCTheme.TextDim) * a, labelScale);

            //右侧：已装配时绘制该模块图标，否则绘制空槽标记
            if (equipped != null && !equipped.IsAir) {
                Main.instance.LoadItem(equipped.type);
                Texture2D iconTex = TextureAssets.Item[equipped.type]?.Value;
                if (iconTex != null) {
                    Rectangle frame = Main.itemAnimations[equipped.type] != null
                        ? Main.itemAnimations[equipped.type].GetFrame(iconTex)
                        : iconTex.Bounds;
                    float maxIcon = r.Height - 4f;
                    float iconScale = MathF.Min(maxIcon / frame.Width, maxIcon / frame.Height);
                    if (iconScale > 1f) iconScale = 1f;
                    Vector2 iconCenter = new(r.Right - 6f * Scale - frame.Width * iconScale * 0.5f,
                        r.Y + r.Height * 0.5f);
                    //已装配的改件走赛博朋克滤镜，按其识别色重映射
                    if (equipped.ModItem is Modules.SHPCModuleItem mod
                        && Modules.SHPCModuleRender.Begin(sb, mod.TintColor,
                            new Vector2(iconTex.Width, iconTex.Height), Main.UIScaleMatrix, mod.TintIntensity)) {
                        sb.Draw(iconTex, iconCenter, frame, Color.White * a, 0f,
                            new Vector2(frame.Width * 0.5f, frame.Height * 0.5f),
                            iconScale, SpriteEffects.None, 0f);
                        Modules.SHPCModuleRender.End(sb);
                    }
                    else {
                        sb.Draw(iconTex, iconCenter, frame, Color.White * a, 0f,
                            new Vector2(frame.Width * 0.5f, frame.Height * 0.5f),
                            iconScale, SpriteEffects.None, 0f);
                    }
                }
            }
            else {
                const string emptyMark = "--";
                const float emptyScale = 0.46f * FontScale;
                Vector2 emptySz = font.MeasureString(emptyMark) * emptyScale;
                Utils.DrawBorderString(sb, emptyMark,
                    new Vector2(r.Right - 6f * Scale - emptySz.X, r.Y + (r.Height - emptySz.Y) * 0.5f),
                    SHPCTheme.TextDim * (0.55f * a), emptyScale);
            }
        }

        private static void DrawEquippedTooltip(SpriteBatch sb, Texture2D px,
            DynamicSpriteFont font, Rectangle slotRect, Item item, float a) {
            List<string> lines = new();
            List<Color> colors = new();

            lines.Add(item.Name);
            colors.Add(SHPCTheme.Text);

            //物品tooltip第1行通常是槽位标注，整段都输出
            if (item.ToolTip != null) {
                int n = item.ToolTip.Lines;
                for (int i = 0; i < n; i++) {
                    string ln = item.ToolTip.GetLine(i);
                    if (!string.IsNullOrWhiteSpace(ln)) {
                        lines.Add(ln);
                        colors.Add(SHPCTheme.TextDim);
                    }
                }
            }

            if (item.ModItem is SHPCModuleItem mod) {
                foreach (var (ln, isNeg) in mod.GetStatLines()) {
                    if (string.IsNullOrEmpty(ln)) continue;
                    lines.Add(ln);
                    colors.Add(isNeg ? new Color(255, 120, 110) : new Color(120, 255, 170));
                }
            }

            float scale = 0.60f * FontScale;
            float lineH = font.LineSpacing * scale;
            float maxW = 0f;
            foreach (string ln in lines) {
                float w = font.MeasureString(ln).X * scale;
                if (w > maxW) maxW = w;
            }
            const float padX = 10f;
            const float padY = 7f;
            float totalH = lineH * lines.Count + padY * 2f;
            float totalW = maxW + padX * 2f;

            //卡片在槽位正上方居中，空间不足时翻到槽位下方
            float cardX = slotRect.X + (slotRect.Width - totalW) * 0.5f;
            float cardY = slotRect.Y - totalH - 6f;
            if (cardY < 4f) {
                cardY = slotRect.Bottom + 6f;
            }
            if (cardX < 4f) cardX = 4f;
            if (cardX + totalW > Main.screenWidth - 4f) cardX = Main.screenWidth - totalW - 4f;

            Rectangle box = new((int)cardX, (int)cardY, (int)totalW, (int)totalH);

            //投影
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(box.X + 3, box.Y + 4, box.Width, box.Height),
                new Color(0, 0, 0) * (0.55f * a));
            //背景
            SHPCRenderer.DrawFilledRect(sb, px, box, new Color(4, 14, 22) * (0.96f * a));
            //顶部色带：按模块识别色高亮
            Color topBar = item.ModItem is SHPCModuleItem m ? m.TintColor : SHPCTheme.Cyan;
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(box.X, box.Y, box.Width, (int)(3f * Scale)), topBar * (0.85f * a));
            //边框与四角装饰
            SHPCRenderer.DrawRectStroke(sb, px, box, 1.2f, SHPCTheme.Border * (0.9f * a));
            SHPCRenderer.DrawCornerBrackets(sb, px, box, 6f * Scale, 1.2f, SHPCTheme.BorderHi * (0.9f * a));

            //图标（右上角小图）
            Main.instance.LoadItem(item.type);
            Texture2D iconTex = TextureAssets.Item[item.type]?.Value;
            if (iconTex != null) {
                Rectangle frame = Main.itemAnimations[item.type] != null
                    ? Main.itemAnimations[item.type].GetFrame(iconTex)
                    : iconTex.Bounds;
                float maxIcon = 20f * Scale;
                float iconScale = MathF.Min(maxIcon / frame.Width, maxIcon / frame.Height);
                if (iconScale > 1f) iconScale = 1f;
                Vector2 iconPos = new(box.Right - padX - frame.Width * iconScale * 0.5f,
                    box.Y + padY + lineH * 0.5f);
                if (item.ModItem is SHPCModuleItem modIcon
                    && SHPCModuleRender.Begin(sb, modIcon.TintColor,
                        new Vector2(iconTex.Width, iconTex.Height), Main.UIScaleMatrix, modIcon.TintIntensity)) {
                    sb.Draw(iconTex, iconPos, frame, Color.White * a, 0f,
                        new Vector2(frame.Width * 0.5f, frame.Height * 0.5f), iconScale, SpriteEffects.None, 0f);
                    SHPCModuleRender.End(sb);
                }
                else {
                    sb.Draw(iconTex, iconPos, frame, Color.White * a, 0f,
                        new Vector2(frame.Width * 0.5f, frame.Height * 0.5f), iconScale, SpriteEffects.None, 0f);
                }
            }

            //文字
            float y = box.Y + padY;
            for (int i = 0; i < lines.Count; i++) {
                Utils.DrawBorderString(sb, lines[i],
                    new Vector2(box.X + padX, y), colors[i] * a, scale);
                y += lineH;
            }
        }

        private static void DrawGunTexture(SpriteBatch sb, Texture2D px,
            Vector2 gun, float time, float a) {
            Texture2D gunTex = SHPCOverride.ID > 0
                ? TextureAssets.Item[SHPCOverride.ID]?.Value
                : null;

            //悬浮呼吸脉冲强度
            float pulse = 0.90f + MathF.Sin(time * 2.6f) * 0.10f;

            if (gunTex == null) {
                //纹理尚未加载时降级绘制一个占位圆
                SHPCRenderer.DrawRing(sb, px, gun, 18f, 2f, SHPCTheme.Border * a);
                return;
            }

            //枪体在面板中的轻微悬浮上下位移（呼吸感）
            Vector2 floatOffset = new(0f, MathF.Sin(time * 1.8f) * 2.5f);
            Vector2 drawPos = gun + floatOffset;

            //背光晕染（椭圆形软边，贴合枪体横向轮廓）
            float glowW = gunTex.Width * GunScale * 0.55f;
            float glowH = gunTex.Height * GunScale * 0.80f;
            for (int gi = 3; gi >= 1; gi--) {
                float scaleAdd = gi * 0.12f;
                sb.Draw(px,
                    new Rectangle(
                        (int)(drawPos.X - glowW * 0.5f - glowW * scaleAdd * 0.5f),
                        (int)(drawPos.Y - glowH * 0.5f - glowH * scaleAdd * 0.5f),
                        (int)(glowW * (1f + scaleAdd)),
                        (int)(glowH * (1f + scaleAdd))),
                    new Rectangle(0, 0, 1, 1),
                    SHPCTheme.Cyan * (0.04f * (4 - gi) * pulse * a));
            }

            //枪体纹理本体，以纹理中心为原点绘制，带半透明调色强化科技感
            Color tint = Color.Lerp(Color.White, SHPCTheme.CyanHi, 0.15f * pulse) * a;
            sb.Draw(gunTex, drawPos, null, tint,
                0f, gunTex.Size() * 0.5f, GunScale, SpriteEffects.None, 0f);
        }

        private static void DrawPresetBar(SpriteBatch sb, Texture2D px,
            DynamicSpriteFont font, in Layout layout, HitKind hover, float a) {
            Rectangle panel = layout.Panel;
            float barTop = panel.Y + PanelH - PresetBarH;

            //分隔线
            SHPCRenderer.DrawLine(sb, px,
                new Vector2(panel.X + EdgePad, barTop + 1f),
                new Vector2(panel.X + PanelW - EdgePad, barTop + 1f),
                1f, SHPCTheme.Border * (0.5f * a));

            SHPCPlayer sp = Main.LocalPlayer != null ? SHPCPlayer.Get(Main.LocalPlayer) : null;
            int activePreset = sp?.ActivePreset ?? 0;

            string[] labels = {
                SHPCUI.Modify_Preset_A.Value,
                SHPCUI.Modify_Preset_B.Value,
                SHPCUI.Modify_Preset_C.Value,
            };

            for (int i = 0; i < PresetCount; i++) {
                Rectangle r = GetPresetBtnRect(layout, i);
                bool isActive = i == activePreset;
                bool isHover = hover == HitKind.Preset0 + i;

                //投影
                SHPCRenderer.DrawFilledRect(sb, px,
                    new Rectangle(r.X + 2, r.Y + 2, r.Width, r.Height),
                    new Color(0, 0, 0) * (0.35f * a));

                //背景
                Color bg = isActive
                    ? new Color(10, 45, 62) * (0.95f * a)
                    : isHover
                        ? new Color(8, 30, 44) * (0.90f * a)
                        : new Color(4, 14, 22) * (0.80f * a);
                SHPCRenderer.DrawFilledRect(sb, px, r, bg);

                //顶部激活指示条
                if (isActive) {
                    SHPCRenderer.DrawFilledRect(sb, px,
                        new Rectangle(r.X, r.Y, r.Width, (int)(2f * Scale)),
                        SHPCTheme.Cyan * (0.95f * a));
                }

                //边框
                Color border = isActive
                    ? SHPCTheme.CyanHi * (0.90f * a)
                    : isHover
                        ? SHPCTheme.Border * (0.85f * a)
                        : SHPCTheme.Border * (0.45f * a);
                SHPCRenderer.DrawRectStroke(sb, px, r, 1.1f, border);

                //悬停四角装饰
                if (isHover && !isActive) {
                    SHPCRenderer.DrawCornerBrackets(sb, px, r, 3f * Scale, 1.1f, SHPCTheme.CyanHi * a);
                }

                //标签文字
                float labelScale = 0.48f * FontScale;
                Color textColor = isActive
                    ? SHPCTheme.CyanHi * a
                    : SHPCTheme.TextDim * (0.85f * a);
                Vector2 labelSz = font.MeasureString(labels[i]) * labelScale;
                Utils.DrawBorderString(sb, labels[i],
                    new Vector2(r.X + (r.Width - labelSz.X) * 0.5f, r.Y + (r.Height - labelSz.Y) * 0.5f),
                    textColor, labelScale);
            }
        }
    }
}
