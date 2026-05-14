using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.RAMSystems;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI
{
    /// <summary>
    /// 赛博空间领域管理面板
    /// 黑墙AI风格 深红色系 + 三段式领域指示环 + 故障层叠环 + 自定义着色器背景
    /// 由 <see cref="SHPCUI"/> 在固定二级面板模式下调用
    /// </summary>
    internal static class SHPCCyberPanel
    {
        //整体UI放大系数，所有尺寸字号都按此比例缩放，便于统一调节
        private const float Scale = 1.1f;
        //字号缩放系数
        private const float FontScale = 1.2f;

        public const float PanelW = 248f * Scale;
        public const float PanelH = 210f * Scale;
        private const float EdgePad = 12f * Scale;

        //三色环主区域参数
        private const float RingOuterR = 50f * Scale;
        private const float RingInnerR = 32f * Scale;
        //三色环纵向相对面板的位置比例，略向上偏离正中心，给底部按钮留出空间
        private const float RingCenterYRatio = 0.44f;

        //段间缝隙
        private const float SegmentGap = 0.10f;
        //12点方向起始
        private const float StartAngle = -MathHelper.PiOver2;

        //底部开关按钮与提示的纵向布局
        private const int ToggleHeight = (int)(26 * Scale);
        //开关按钮底部相对面板底边的距离
        private const int ToggleBottomMargin = (int)(24 * Scale);
        //快捷键提示底部相对面板底边的距离
        private const int HintBottomMargin = (int)(4 * Scale);

        //三级技能面板尺寸与定位
        private const float SkillPanelW = 250f * Scale;
        private const float SkillPanelH = 128f * Scale;
        private const float SkillPanelGapX = 16f * Scale;
        //单个技能条目中描述文本的行高（像素）
        private const int SkillDescLineHeight = (int)(16 * Scale);
        //单个技能条目除描述多行以外的固定高度（标题区 30 + 状态行+底部间距 28）
        private const int SkillEntryFixedHeight = (int)((30 + 28) * Scale);
        //技能列表内容左右内边距
        private const int SkillEntryLeftPad = (int)(10 * Scale);
        //技能描述文本缩放
        private const float SkillDescScale = 0.54f * FontScale;
        //三级面板纵向偏移：L1 在上、L2 在中、L3 在下
        private const float SkillPanelYOffset = 64f * Scale;
        //段悬停时外径最大延展量
        private const float SegmentExpandMax = 14f * Scale;

        /// <summary>
        /// 面板内可点击控件枚举
        /// Skill1/Skill2/Skill3 表示对应层的三级面板悬停区域，仅用于阻止面板收起
        /// </summary>
        public enum HitKind
        {
            None,
            Toggle,
            Layer1,
            Layer2,
            Layer3,
            Skill1,
            Skill2,
            Skill3,
        }

        public ref struct Layout
        {
            public Rectangle Panel;
            public Vector2 RingCenter;
            public Rectangle Toggle;
        }

        //三段环各自的悬停延展进度，0=未悬停 1=完全延展，由 <see cref="UpdateHover"/> 平滑跟随
        private static readonly float[] segmentExpandAmt = new float[3];

        /// <summary>
        /// 三级技能面板单条技能定义
        /// </summary>
        private readonly struct SkillEntry
        {
            public readonly Func<string> Name;
            public readonly Func<string> Desc;
            public readonly Func<ModKeybind> Hotkey;
            public readonly int RequiredLayer;

            public SkillEntry(Func<string> name, Func<string> desc,
                Func<ModKeybind> hotkey, int requiredLayer) {
                Name = name;
                Desc = desc;
                Hotkey = hotkey;
                RequiredLayer = requiredLayer;
            }
        }

        /// <summary>
        /// 取出某段对应的技能列表，目前每段最多一个技能
        /// 数组为延迟初始化，确保本地化文本已就绪
        /// </summary>
        private static SkillEntry[] GetLayerSkills(int layer) {
            switch (layer) {
                case 1:
                    return [
                        new SkillEntry(
                            () => SHPCUI.Cyber_Skill_Teleport_Name.Value,
                            () => SHPCUI.Cyber_Skill_Teleport_Desc.Value,
                            () => CWRKeySystem.Legend_Teleport,
                            requiredLayer: 1),
                        new SkillEntry(
                            () => SHPCUI.Cyber_Skill_Restart_Name.Value,
                            () => SHPCUI.Cyber_Skill_Restart_Desc.Value,
                            () => CWRKeySystem.Legend_Restart,
                            requiredLayer: 1)
                    ];
                case 2:
                    return [
                        new SkillEntry(
                            () => SHPCUI.Cyber_Skill_Banish_Name.Value,
                            () => SHPCUI.Cyber_Skill_Banish_Desc.Value,
                            () => CWRKeySystem.CyberBanish_Key,
                            requiredLayer: 2)
                    ];
                case 3:
                    return [
                        new SkillEntry(
                            () => SHPCUI.Cyber_Skill_Freeze_Name.Value,
                            () => SHPCUI.Cyber_Skill_Freeze_Desc.Value,
                            () => CWRKeySystem.CyberFreeze_Key,
                            requiredLayer: 3)
                    ];
                default:
                    return [];
            }
        }

        /// <summary>
        /// 根据扇区锚点与中线方向计算面板位置
        /// </summary>
        public static Layout Compute(Vector2 anchor, float midAngle, float panelAlpha) {
            Vector2 outDir = SHPCRenderer.AngleDir(midAngle);
            float slide = (1f - panelAlpha) * 14f;
            Vector2 panelPos = anchor + outDir * (SHPCTheme.InfoPanelGap + slide);
            panelPos.Y -= PanelH * 0.5f;

            Rectangle panel = new((int)panelPos.X, (int)panelPos.Y, (int)PanelW, (int)PanelH);
            Vector2 ringCenter = new(
                panel.X + panel.Width * 0.32f,
                panel.Y + panel.Height * RingCenterYRatio);
            int toggleW = panel.Width - (int)(24 * Scale);
            int toggleY = panel.Bottom - ToggleBottomMargin - ToggleHeight;
            Rectangle toggle = new(panel.X + (int)(12 * Scale), toggleY, toggleW, ToggleHeight);

            return new Layout {
                Panel = panel,
                RingCenter = ringCenter,
                Toggle = toggle,
            };
        }

        /// <summary>
        /// 给定层数(1..3)取该段的角度区间
        /// </summary>
        private static void GetSegmentAngles(int layer, out float a0, out float a1) {
            float per = MathHelper.TwoPi / 3f;
            float center = StartAngle + (layer - 1) * per + per * 0.5f;
            a0 = center - per * 0.5f + SegmentGap * 0.5f;
            a1 = center + per * 0.5f - SegmentGap * 0.5f;
        }

        /// <summary>
        /// 取得指定层(1..3)三级技能面板的屏幕矩形
        /// 面板锁定于二级面板右侧，按层数纵向分布（上/中/下）
        /// 高度依据该层技能数量自适应，确保多技能也能完整展开
        /// </summary>
        private static Rectangle GetSkillPanelRect(in Layout layout, int layer) {
            int x = layout.Panel.Right + (int)SkillPanelGapX;
            float ringY = layout.RingCenter.Y;
            float yOffset = (layer - 2) * SkillPanelYOffset;
            int height = ComputeSkillPanelHeight(layer);
            int y = (int)(ringY + yOffset - height * 0.5f);
            return new Rectangle(x, y, (int)SkillPanelW, height);
        }

        /// <summary>
        /// 根据该层技能数量与描述文本实际换行行数动态计算面板高度
        /// 无技能占位面板维持原始紧凑高度，多技能按描述行数逐条累加
        /// </summary>
        private static int ComputeSkillPanelHeight(int layer) {
            SkillEntry[] skills = GetLayerSkills(layer);
            if (skills.Length == 0) {
                return (int)SkillPanelH;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float descMaxW = SkillPanelW - SkillEntryLeftPad * 2;
            //头部标题区 40 + 下方留白 8
            int total = 40 + 8;
            foreach (SkillEntry entry in skills) {
                total += ComputeSkillEntryHeight(font, entry, descMaxW);
            }
            return Math.Max(total, (int)SkillPanelH);
        }

        /// <summary>
        /// 计算单个技能条目的完整高度（含描述多行）
        /// </summary>
        private static int ComputeSkillEntryHeight(DynamicSpriteFont font,
            SkillEntry entry, float descMaxW) {
            string desc = entry.Desc?.Invoke() ?? string.Empty;
            int lines = MeasureWrappedLineCount(font, desc, SkillDescScale, descMaxW);
            if (lines < 1) {
                lines = 1;
            }
            return SkillEntryFixedHeight + lines * SkillDescLineHeight;
        }

        /// <summary>
        /// 取得指定段编号(0..2) 对应的悬停延展进度
        /// </summary>
        public static float GetSegmentExpand(int segIdx) {
            if (segIdx < 0 || segIdx > 2) {
                return 0f;
            }
            return segmentExpandAmt[segIdx];
        }

        /// <summary>
        /// 命中测试
        /// 优先级：开关按钮 → 三级面板（已展开）→ 三段环 → 无
        /// </summary>
        public static HitKind HitTest(in Layout layout, Vector2 mouse) {
            if (layout.Toggle.Contains((int)mouse.X, (int)mouse.Y)) {
                return HitKind.Toggle;
            }

            //三级面板命中：仅当对应段已展开时才参与命中，避免无悬停时出现幽灵区
            for (int i = 0; i < 3; i++) {
                if (segmentExpandAmt[i] <= 0.05f) {
                    continue;
                }
                Rectangle skillRect = GetSkillPanelRect(layout, i + 1);
                if (skillRect.Contains((int)mouse.X, (int)mouse.Y)) {
                    return i == 0 ? HitKind.Skill1
                        : i == 1 ? HitKind.Skill2
                        : HitKind.Skill3;
                }
            }

            Vector2 d = mouse - layout.RingCenter;
            float dist = d.Length();
            //命中检测半径需考虑当前最大延展量，否则延展时鼠标会落出"环段"
            float maxOuter = RingOuterR + SegmentExpandMax + 6f;
            if (dist >= RingInnerR - 4f && dist <= maxOuter) {
                float ang = MathF.Atan2(d.Y, d.X);
                float rel = MathHelper.WrapAngle(ang - StartAngle);
                if (rel < 0f) rel += MathHelper.TwoPi;
                int seg = (int)(rel / (MathHelper.TwoPi / 3f));
                seg = Math.Clamp(seg, 0, 2);
                return seg == 0 ? HitKind.Layer1 : seg == 1 ? HitKind.Layer2 : HitKind.Layer3;
            }
            return HitKind.None;
        }

        /// <summary>
        /// 平滑更新各段悬停延展进度
        /// 面板可见时调用，面板收起后会被传入 <see cref="HitKind.None"/>
        /// 面板整体不可见时强制衰减为 0
        /// </summary>
        public static void UpdateHover(HitKind hover, bool panelVisible) {
            for (int i = 0; i < 3; i++) {
                bool isHover = panelVisible && (
                    (i == 0 && (hover == HitKind.Layer1 || hover == HitKind.Skill1))
                    || (i == 1 && (hover == HitKind.Layer2 || hover == HitKind.Skill2))
                    || (i == 2 && (hover == HitKind.Layer3 || hover == HitKind.Skill3)));
                float target = isHover ? 1f : 0f;
                float speed = isHover ? 0.22f : 0.16f;
                segmentExpandAmt[i] = MathHelper.Lerp(segmentExpandAmt[i], target, speed);
                if (MathF.Abs(segmentExpandAmt[i] - target) < 0.003f) {
                    segmentExpandAmt[i] = target;
                }
            }
        }

        /// <summary>
        /// 整体面板绘制
        /// </summary>
        public static void Draw(SpriteBatch sb, Texture2D px, in Layout layout,
            float panelAlpha, float globalAlpha, HitKind hover) {
            if (panelAlpha < 0.02f) {
                return;
            }
            float a = panelAlpha * globalAlpha;
            Rectangle rect = layout.Panel;
            float time = (float)Main.GameUpdateCount / 60f;

            //投影
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(rect.X + 3, rect.Y + 4, rect.Width, rect.Height),
                new Color(0, 0, 0) * (0.55f * a));

            //着色器背景
            DrawShaderBackground(sb, px, rect, panelAlpha, globalAlpha);

            //外框 + 四角L形装饰
            SHPCRenderer.DrawCornerBrackets(sb, px, rect, 9f * Scale, 1.5f,
                new Color(255, 90, 80) * (0.95f * a));
            SHPCRenderer.DrawRectStroke(sb, px, rect, 1.2f, new Color(140, 30, 30) * (0.9f * a));

            DynamicSpriteFont font = FontAssets.MouseText.Value;

            //标题
            Utils.DrawBorderString(sb, SHPCUI.Cyber_PanelTitle.Value,
                new Vector2(rect.X + 10f * Scale, rect.Y + 7f * Scale), new Color(255, 230, 230) * a, 0.62f * FontScale);
            Utils.DrawBorderString(sb, SHPCUI.Cyber_PanelSubtitle.Value,
                new Vector2(rect.X + 10f * Scale, rect.Y + 24f * Scale), new Color(205, 85, 80) * a, 0.40f * FontScale);

            //右上ID码，每秒滚动
            string idCode = $"#{(int)(time * 11f) % 999:D3}";
            Vector2 idSize = font.MeasureString(idCode) * (0.45f * FontScale);
            Utils.DrawBorderString(sb, idCode,
                new Vector2(rect.Right - 10f * Scale - idSize.X, rect.Y + 9f * Scale),
                new Color(255, 135, 115) * (0.85f * a), 0.45f * FontScale);

            //三色环主体
            DrawTriRing(sb, px, layout.RingCenter, time, hover, a);

            //右侧状态栏
            DrawStatusColumn(sb, font, rect, layout, a);

            //开关按钮
            DrawToggleButton(sb, px, font, layout.Toggle, hover == HitKind.Toggle, a);

            //快捷键提示，使用稍大字号并贴近底边但与开关按钮留出 8~10px 间隙
            string keyName = CWRKeySystem.Legend_Domain != null
                && CWRKeySystem.Legend_Domain.GetAssignedKeys().Count > 0
                ? CWRKeySystem.Legend_Domain.GetAssignedKeys()[0]
                : "?";
            string hint = string.Format(SHPCUI.Cyber_Hint.Value, keyName);
            const float hintScale = 0.50f * FontScale;
            Vector2 hintSize = font.MeasureString(hint) * hintScale;
            Utils.DrawBorderString(sb, hint,
                new Vector2(rect.X + (rect.Width - hintSize.X) * 0.5f,
                    rect.Bottom - HintBottomMargin - hintSize.Y),
                new Color(230, 145, 135) * (0.85f * a), hintScale);

            //三级技能面板，最后绘制保证置顶
            DrawSkillPanels(sb, px, font, layout, panelAlpha, globalAlpha, time);
        }

        /// <summary>
        /// 使用CyberDomainPanel着色器绘制背景，失败则降级为纯色
        /// </summary>
        private static void DrawShaderBackground(SpriteBatch sb, Texture2D px,
            Rectangle rect, float panelAlpha, float globalAlpha) {
            float a = panelAlpha * globalAlpha;
            if (EffectLoader.CyberDomainPanel?.Value == null) {
                SHPCRenderer.DrawFilledRect(sb, px, rect, new Color(28, 6, 10) * (0.96f * a));
                return;
            }

            Effect effect = EffectLoader.CyberDomainPanel.Value;
            float time = (float)Main.GameUpdateCount / 60f;

            //收缩动画期间用层展开进度求得带平滑过渡的浮点层数
            float floatLayer = 0f;
            for (int i = 0; i < Cyberspace.MaxLayerCount; i++) {
                floatLayer += Cyberspace.GetLayerExpand(i);
            }
            float layerParam = Cyberspace.Active
                ? MathF.Max(Cyberspace.CurrentLayer * 0.4f + floatLayer * 0.6f, floatLayer)
                : floatLayer;

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(a * 0.97f);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(rect.Width, rect.Height));
            effect.Parameters["uEdgePad"]?.SetValue(EdgePad);
            effect.Parameters["uLayer"]?.SetValue(layerParam);
            effect.Parameters["uIntensity"]?.SetValue(MathHelper.Clamp(Cyberspace.Intensity, 0f, 1f));

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

        /// <summary>
        /// 绘制三段式领域指示环 + 中央心跳核心
        /// </summary>
        private static void DrawTriRing(SpriteBatch sb, Texture2D px,
            Vector2 center, float time, HitKind hover, float a) {
            //中央深色底盘
            SHPCRenderer.DrawDisc(sb, px, center, RingInnerR - 4f * Scale, 4f,
                new Color(10, 4, 6) * a);

            //核心心跳
            float beat = 0.5f + 0.5f * MathF.Sin(time * 3f);
            float beatScale = Cyberspace.Active ? 0.6f + 0.4f * Cyberspace.Intensity : 0.25f;
            SHPCRenderer.DrawDisc(sb, px, center,
                (RingInnerR - 12f * Scale) * (0.55f + 0.10f * beat), 3f,
                new Color(255, 80, 60) * (beatScale * a));

            //三段
            for (int i = 0; i < 3; i++) {
                int layer = i + 1;
                bool lit = Cyberspace.Active && Cyberspace.CurrentLayer >= layer;
                float litAmt = Cyberspace.GetLayerExpand(i);
                bool isHover = (i == 0 && (hover == HitKind.Layer1 || hover == HitKind.Skill1))
                    || (i == 1 && (hover == HitKind.Layer2 || hover == HitKind.Skill2))
                    || (i == 2 && (hover == HitKind.Layer3 || hover == HitKind.Skill3));
                float expand = segmentExpandAmt[i];

                GetSegmentAngles(layer, out float a0, out float a1);
                DrawRingSegment(sb, px, center, a0, a1, layer,
                    litAmt, lit, isHover, expand, time, a);
            }

            //中央层数文本
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string txt = Cyberspace.Active ? Cyberspace.CurrentLayer.ToString() : "0";
            Vector2 size = font.MeasureString(txt) * (0.95f * FontScale);
            Color textCol = Cyberspace.Active ? new Color(255, 225, 220) : new Color(145, 100, 100);
            Utils.DrawBorderString(sb, txt,
                center - size * 0.5f + new Vector2(0f, -2f * Scale), textCol * a, 0.95f * FontScale);

            //中央上方LAYER小字
            string label = SHPCUI.Cyber_LayerLabel.Value;
            Vector2 lblSize = font.MeasureString(label) * (0.35f * FontScale);
            Utils.DrawBorderString(sb, label,
                center + new Vector2(-lblSize.X * 0.5f, -RingInnerR + 6f * Scale),
                new Color(205, 95, 90) * (0.85f * a), 0.35f * FontScale);
        }

        /// <summary>
        /// 绘制单段环
        /// expandAmt 表示段位悬停延展进度，影响外径与额外辉光
        /// </summary>
        private static void DrawRingSegment(SpriteBatch sb, Texture2D px,
            Vector2 center, float a0, float a1, int layer,
            float litAmt, bool litFlag, bool hover, float expandAmt,
            float time, float a) {
            //延展后的实际外径
            float outerR = RingOuterR + expandAmt * SegmentExpandMax;

            //底环
            SHPCRenderer.DrawArc(sb, px, center, RingInnerR, outerR, a0, a1,
                new Color(60, 14, 18) * (0.95f * a));

            //点亮填充，按litAmt插值半径
            if (litAmt > 0.01f) {
                Color hot = layer == 1 ? new Color(255, 70, 50)
                    : layer == 2 ? new Color(255, 130, 60)
                    : new Color(255, 200, 80);
                float fillR = MathHelper.Lerp(RingInnerR, outerR, MathHelper.Clamp(litAmt, 0f, 1f));
                SHPCRenderer.DrawArc(sb, px, center, RingInnerR, fillR, a0, a1, hot * (0.55f * a));
                SHPCRenderer.DrawArcStroke(sb, px, center, fillR - 1f, a0, a1, 1.6f,
                    hot * (0.95f * a));

                //段内扫光，沿角度推进
                float scanT = (time * (0.55f + 0.20f * layer)) % 1f;
                float scanA = MathHelper.Lerp(a0, a1, scanT);
                float scanW = (a1 - a0) * 0.18f;
                float sa0 = MathF.Max(a0, scanA - scanW * 0.5f);
                float sa1 = MathF.Min(a1, scanA + scanW * 0.5f);
                SHPCRenderer.DrawArc(sb, px, center, RingInnerR + 2f, outerR - 2f, sa0, sa1,
                    new Color(255, 240, 220) * (0.45f * litAmt * a));
            }

            //悬停柔光
            if (hover) {
                SHPCRenderer.DrawArc(sb, px, center, RingInnerR, outerR, a0, a1,
                    new Color(255, 200, 180) * (0.18f * a));
            }

            //段描边
            Color border = litFlag
                ? new Color(255, 220, 200)
                : hover ? new Color(255, 150, 130) : new Color(160, 50, 50);
            SHPCRenderer.DrawArcStroke(sb, px, center, outerR - 0.5f, a0, a1, 1.4f, border * a);
            SHPCRenderer.DrawArcStroke(sb, px, center, RingInnerR + 0.5f, a0, a1, 1.1f, border * (0.6f * a));

            //径向封口
            Vector2 dirS = SHPCRenderer.AngleDir(a0);
            Vector2 dirE = SHPCRenderer.AngleDir(a1);
            SHPCRenderer.DrawLine(sb, px,
                center + dirS * RingInnerR, center + dirS * outerR,
                1.4f, border * (0.7f * a));
            SHPCRenderer.DrawLine(sb, px,
                center + dirE * RingInnerR, center + dirE * outerR,
                1.4f, border * (0.7f * a));

            //延展时的外缘辉光与小刻度，提供"打开三级面板"的视觉反馈
            if (expandAmt > 0.02f) {
                Color glow = layer == 1 ? new Color(255, 90, 70)
                    : layer == 2 ? new Color(255, 150, 80)
                    : new Color(255, 210, 100);

                //外缘高光环带
                SHPCRenderer.DrawArc(sb, px, center, outerR - 1f, outerR + 2.5f, a0, a1,
                    glow * (0.45f * expandAmt * a));
                SHPCRenderer.DrawArcStroke(sb, px, center, outerR + 1.2f, a0, a1, 1.4f,
                    glow * (0.85f * expandAmt * a));

                //三个小指示刻度，沿外缘均匀分布
                int tickCount = 3;
                for (int t = 0; t < tickCount; t++) {
                    float tt = (t + 1f) / (tickCount + 1f);
                    float tickA = MathHelper.Lerp(a0, a1, tt);
                    Vector2 td = SHPCRenderer.AngleDir(tickA);
                    float tickIn = outerR + 2.5f;
                    float tickOut = outerR + 2.5f + 4f * expandAmt;
                    SHPCRenderer.DrawLine(sb, px,
                        center + td * tickIn, center + td * tickOut,
                        1.5f, glow * (0.95f * expandAmt * a));
                }
            }

            //段内层数文字
            float midA = (a0 + a1) * 0.5f;
            Vector2 textPos = center + SHPCRenderer.AngleDir(midA) * (RingInnerR + RingOuterR) * 0.5f;
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string s = layer.ToString();
            Vector2 sz = font.MeasureString(s) * (0.55f * FontScale);
            Color tc = litFlag ? new Color(255, 250, 240) : new Color(200, 115, 105);
            Utils.DrawBorderString(sb, s, textPos - sz * 0.5f, tc * a, 0.55f * FontScale);
        }

        /// <summary>
        /// 右侧状态文字栏，列出每层激活状态、RAM 消耗速度及当前可维持时长
        /// </summary>
        private static void DrawStatusColumn(SpriteBatch sb, DynamicSpriteFont font,
            Rectangle panel, in Layout layout, float a) {
            float colX = layout.RingCenter.X + RingOuterR + 18f * Scale;
            float colY = panel.Y + 44f * Scale;
            float lineH = 18f * Scale;

            string state = Cyberspace.Active
                ? SHPCUI.Cyber_StateOnline.Value
                : SHPCUI.Cyber_StateOffline.Value;
            Color stateCol = Cyberspace.Active ? new Color(255, 210, 190) : new Color(160, 100, 100);
            Utils.DrawBorderString(sb, state, new Vector2(colX, colY), stateCol * a, 0.56f * FontScale);
            colY += lineH;

            //每层激活标记 + 该层的 RAM 消耗速度
            for (int i = 0; i < 3; i++) {
                int layer = i + 1;
                bool lit = Cyberspace.Active && Cyberspace.CurrentLayer >= layer;
                string mark = lit ? "[#]" : "[ ]";
                Color tc = lit ? new Color(255, 210, 180) : new Color(145, 85, 85);
                Utils.DrawBorderString(sb, $"{mark} L{layer}", new Vector2(colX, colY), tc * a, 0.50f * FontScale);

                //每层消耗速度："-X.X/s"，当前激活层用更亮的红色
                float drainRate = Cyberspace.GetLayerDrainRate(layer);
                if (drainRate > 0f) {
                    string drainTxt = string.Format(SHPCUI.Cyber_DrainPerSec.Value, drainRate.ToString("F1"));
                    Color drainCol = lit ? new Color(255, 210, 180) : new Color(160, 100, 100);
                    Utils.DrawBorderString(sb, drainTxt,
                        new Vector2(colX + 36f * Scale, colY + 1f), drainCol * a, 0.44f * FontScale);
                }
                colY += lineH;
            }

            //当前层的 SUSTAIN 估算："SUSTAIN ~Xs"
            //仅在激活态显示，剩余时间过短(<2s)用闪烁红警示，无限模式下显示 ∞
            if (Cyberspace.Active && Cyberspace.CurrentLayer >= 1) {
                colY += 2f * Scale;
                if (HackTime.InfiniteHack) {
                    Utils.DrawBorderString(sb, SHPCUI.Cyber_SustainInfinite.Value,
                        new Vector2(colX, colY), new Color(255, 230, 200) * a, 0.46f * FontScale);
                }
                else {
                    float drainNow = Cyberspace.GetCurrentDrainRate();
                    if (drainNow > 0f) {
                        float sustain = RamSystem.CurrentRam / drainNow;
                        string sustainTxt = string.Format(SHPCUI.Cyber_SustainTime.Value,
                            sustain.ToString("F1"));
                        //剩余时间紧张时颜色转红并轻微脉冲
                        float t = (float)Main.GameUpdateCount / 60f;
                        Color sustainCol;
                        if (sustain < 2f) {
                            float pulse = MathF.Sin(t * 8f) * 0.4f + 0.6f;
                            sustainCol = Color.Lerp(new Color(255, 200, 170), new Color(255, 80, 80), pulse);
                        }
                        else if (sustain < 5f) {
                            sustainCol = new Color(255, 170, 130);
                        }
                        else {
                            sustainCol = new Color(230, 210, 190);
                        }
                        Utils.DrawBorderString(sb, sustainTxt,
                            new Vector2(colX, colY), sustainCol * a, 0.46f * FontScale);
                    }
                }
            }
        }

        /// <summary>
        /// 底部开关按钮
        /// </summary>
        private static void DrawToggleButton(SpriteBatch sb, Texture2D px,
            DynamicSpriteFont font, Rectangle r, bool hover, float a) {
            bool active = Cyberspace.Active;
            string txt = active
                ? SHPCUI.Cyber_BtnDeactivate.Value
                : SHPCUI.Cyber_BtnActivate.Value;

            Color bg = active ? new Color(140, 30, 30) : new Color(60, 12, 18);
            if (hover) bg = active ? new Color(200, 60, 50) : new Color(110, 30, 30);
            SHPCRenderer.DrawFilledRect(sb, px, r, bg * (0.85f * a));

            Color border = hover ? new Color(255, 220, 200)
                : active ? new Color(255, 130, 110) : new Color(180, 50, 50);
            SHPCRenderer.DrawRectStroke(sb, px, r, 1.4f, border * a);

            //方块端帽
            int capSize = (int)(4 * Scale);
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(r.X - 2, r.Y + r.Height / 2 - capSize, capSize, capSize * 2), border * a);
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(r.Right - 2, r.Y + r.Height / 2 - capSize, capSize, capSize * 2), border * a);

            Vector2 size = font.MeasureString(txt) * (0.60f * FontScale);
            Color textCol = active ? new Color(255, 242, 232) : new Color(255, 210, 190);
            Utils.DrawBorderString(sb, txt,
                new Vector2(r.X + (r.Width - size.X) * 0.5f, r.Y + (r.Height - size.Y) * 0.5f - 1f),
                textCol * a, 0.60f * FontScale);
        }

        /// <summary>
        /// 三级技能面板：遍历每段，按延展进度淡入淡出绘制
        /// </summary>
        private static void DrawSkillPanels(SpriteBatch sb, Texture2D px,
            DynamicSpriteFont font, in Layout layout,
            float panelAlpha, float globalAlpha, float time) {
            for (int i = 0; i < 3; i++) {
                float exp = segmentExpandAmt[i];
                if (exp < 0.02f) {
                    continue;
                }
                int layer = i + 1;
                Rectangle skillRect = GetSkillPanelRect(layout, layer);
                DrawSkillPanel(sb, px, font, layout, skillRect, layer,
                    exp, panelAlpha, globalAlpha, time);
            }
        }

        /// <summary>
        /// 单个三级技能面板绘制
        /// 由二级面板右侧滑出，附带连接弧线，背景使用半透明深色色板
        /// </summary>
        private static void DrawSkillPanel(SpriteBatch sb, Texture2D px,
            DynamicSpriteFont font, in Layout layout, Rectangle skillRect,
            int layer, float expandAmt, float panelAlpha, float globalAlpha, float time) {
            float a = panelAlpha * globalAlpha * MathHelper.Clamp(expandAmt * 1.15f, 0f, 1f);
            if (a < 0.02f) {
                return;
            }

            //入场偏移：从二级面板右侧向外滑出
            float slide = (1f - expandAmt) * 22f * Scale;
            Rectangle drawRect = new(
                skillRect.X - (int)slide, skillRect.Y,
                skillRect.Width, skillRect.Height);

            Color hot = layer == 1 ? new Color(255, 90, 70)
                : layer == 2 ? new Color(255, 140, 80)
                : new Color(255, 200, 90);

            //连接弧线：从环段外缘指向面板左边缘中点
            DrawSkillConnector(sb, px, layout, layer, drawRect, expandAmt, panelAlpha * globalAlpha, hot);

            //投影
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(drawRect.X + 2, drawRect.Y + 3, drawRect.Width, drawRect.Height),
                new Color(0, 0, 0) * (0.55f * a));

            //背景深色板
            SHPCRenderer.DrawFilledRect(sb, px, drawRect,
                new Color(22, 6, 9) * (0.95f * a));

            //左侧色带，颜色随层级
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(drawRect.X, drawRect.Y, 3, drawRect.Height),
                hot * (0.85f * a));

            //顶部色带
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(drawRect.X, drawRect.Y, drawRect.Width, 2),
                hot * (0.55f * a));

            //外框 + 四角L形装饰
            SHPCRenderer.DrawRectStroke(sb, px, drawRect, 1.2f,
                new Color(140, 30, 30) * (0.9f * a));
            SHPCRenderer.DrawCornerBrackets(sb, px, drawRect, 8f * Scale, 1.5f,
                hot * (0.95f * a));

            //顶部扫光带，随时间在面板宽度上推进
            float scanT = (time * 0.45f + layer * 0.33f) % 1f;
            int scanX = drawRect.X + (int)(scanT * drawRect.Width);
            int scanW = (int)(drawRect.Width * 0.18f);
            int scanX0 = Math.Max(drawRect.X, scanX - scanW / 2);
            int scanX1 = Math.Min(drawRect.Right, scanX + scanW / 2);
            if (scanX1 > scanX0) {
                SHPCRenderer.DrawFilledRect(sb, px,
                    new Rectangle(scanX0, drawRect.Y, scanX1 - scanX0, 2),
                    new Color(255, 240, 220) * (0.45f * a));
            }

            //标题：L{layer} · 层名
            string title = layer == 1 ? SHPCUI.Cyber_Layer1_Title.Value
                : layer == 2 ? SHPCUI.Cyber_Layer2_Title.Value
                : SHPCUI.Cyber_Layer3_Title.Value;
            string layerTag = $"L{layer}";
            const float tagScale = 0.62f * FontScale;
            const float titleScale = 0.65f * FontScale;
            Utils.DrawBorderString(sb, layerTag,
                new Vector2(drawRect.X + 10f * Scale, drawRect.Y + 6f * Scale),
                hot * a, tagScale);
            Vector2 tagSize = font.MeasureString(layerTag) * tagScale;
            Utils.DrawBorderString(sb, title,
                new Vector2(drawRect.X + 10f * Scale + tagSize.X + 8f * Scale, drawRect.Y + 9f * Scale),
                new Color(255, 230, 220) * a, titleScale);

            //右上 ID 码
            string idCode = $"S{layer}{((int)(time * 7f) % 99):D2}";
            const float idScale = 0.42f * FontScale;
            Vector2 idSize = font.MeasureString(idCode) * idScale;
            Utils.DrawBorderString(sb, idCode,
                new Vector2(drawRect.Right - 8f * Scale - idSize.X, drawRect.Y + 10f * Scale),
                hot * (0.75f * a), idScale);

            //横向分割线
            int divY = drawRect.Y + (int)(32 * Scale);
            SHPCRenderer.DrawFilledRect(sb, px,
                new Rectangle(drawRect.X + (int)(8 * Scale), divY, drawRect.Width - (int)(16 * Scale), 1),
                new Color(120, 30, 30) * (0.65f * a));

            //技能列表
            SkillEntry[] skills = GetLayerSkills(layer);
            if (skills.Length == 0) {
                //无特有技能时给出占位提示
                const float noneScale = 0.60f * FontScale;
                string none = SHPCUI.Cyber_NoSkill.Value;
                Vector2 sz = font.MeasureString(none) * noneScale;
                Utils.DrawBorderString(sb, none,
                    new Vector2(drawRect.X + (drawRect.Width - sz.X) * 0.5f,
                        drawRect.Y + 50f * Scale),
                    new Color(190, 125, 115) * a, noneScale);

                const float footerScale = 0.52f * FontScale;
                string footer = SHPCUI.Cyber_LayerBaseFooter.Value;
                Vector2 fsz = font.MeasureString(footer) * footerScale;
                Utils.DrawBorderString(sb, footer,
                    new Vector2(drawRect.X + (drawRect.Width - fsz.X) * 0.5f,
                        drawRect.Y + 80f * Scale),
                    new Color(195, 145, 135) * (0.85f * a), footerScale);
            }
            else {
                int entryY = divY + (int)(8 * Scale);
                float descMaxW = drawRect.Width - SkillEntryLeftPad * 2;
                for (int i = 0; i < skills.Length; i++) {
                    int entryHeight = ComputeSkillEntryHeight(font, skills[i], descMaxW);
                    DrawSkillEntry(sb, px, font, drawRect, entryY,
                        skills[i], hot, a);
                    entryY += entryHeight;
                }
            }
        }

        /// <summary>
        /// 单条技能条目：左侧名称 + 描述（自动换行），右侧快捷键徽标，下方解锁状态
        /// </summary>
        private static void DrawSkillEntry(SpriteBatch sb, Texture2D px,
            DynamicSpriteFont font, Rectangle panel, int entryY,
            SkillEntry entry, Color hot, float a) {
            bool unlocked = Cyberspace.Active && Cyberspace.CurrentLayer >= entry.RequiredLayer;
            Color nameCol = unlocked ? new Color(255, 242, 232) : new Color(190, 135, 125);
            Color descCol = unlocked ? new Color(230, 190, 180) : new Color(160, 105, 100);

            const float keyScale = 0.60f * FontScale;
            const float nameScale = 0.65f * FontScale;
            const float statusScale = 0.52f * FontScale;
            const int keyHeight = (int)(24 * Scale);
            const int keyPadX = (int)(14 * Scale);

            //快捷键徽标，绘制于条目右上
            ModKeybind keybind = entry.Hotkey?.Invoke();
            string keyName = keybind != null && keybind.GetAssignedKeys().Count > 0
                ? keybind.GetAssignedKeys()[0]
                : SHPCUI.Cyber_KeyUnbound.Value;
            string keyTxt = $"[{keyName}]";
            Vector2 keySize = font.MeasureString(keyTxt) * keyScale;
            Rectangle keyRect = new(
                panel.Right - (int)(10 * Scale) - (int)keySize.X - keyPadX,
                entryY - 2,
                (int)keySize.X + keyPadX, keyHeight);
            Color keyBg = unlocked ? hot * 0.35f : new Color(60, 40, 40) * 0.6f;
            SHPCRenderer.DrawFilledRect(sb, px, keyRect, keyBg * a);
            SHPCRenderer.DrawRectStroke(sb, px, keyRect, 1.1f,
                (unlocked ? hot : new Color(120, 80, 80)) * (0.95f * a));
            Color keyTextCol = unlocked ? new Color(255, 246, 232) : new Color(190, 145, 135);
            Utils.DrawBorderString(sb, keyTxt,
                new Vector2(keyRect.X + 7f * Scale, keyRect.Y + (keyRect.Height - keySize.Y) * 0.5f - 1f),
                keyTextCol * a, keyScale);

            //技能名（左对齐，避开右上徽标）
            string name = entry.Name?.Invoke() ?? string.Empty;
            float nameMaxX = keyRect.X - 6f * Scale;
            string trimmedName = TrimToWidth(font, name, nameScale, nameMaxX - (panel.X + SkillEntryLeftPad));
            Utils.DrawBorderString(sb, trimmedName,
                new Vector2(panel.X + SkillEntryLeftPad, entryY + 2f * Scale),
                nameCol * a, nameScale);

            //描述自动换行：超出面板内宽时按字符断行，避免长描述被简单省略
            string desc = entry.Desc?.Invoke() ?? string.Empty;
            float descMaxW = panel.Width - SkillEntryLeftPad * 2;
            int lineCount = 0;
            int descTopY = entryY + (int)(30 * Scale);
            foreach (string line in EnumerateWrappedLines(font, desc, SkillDescScale, descMaxW)) {
                Utils.DrawBorderString(sb, line,
                    new Vector2(panel.X + SkillEntryLeftPad, descTopY + lineCount * SkillDescLineHeight),
                    descCol * a, SkillDescScale);
                lineCount++;
            }
            if (lineCount < 1) {
                lineCount = 1;
            }

            //解锁状态行：紧随描述末行之后，避免长描述与状态行重叠
            int statusY = descTopY + lineCount * SkillDescLineHeight + (int)(4 * Scale);
            string status = unlocked
                ? SHPCUI.Cyber_SkillUnlocked.Value
                : string.Format(SHPCUI.Cyber_SkillLocked.Value, entry.RequiredLayer);
            Color statusCol = unlocked ? new Color(135, 230, 145) : new Color(230, 120, 110);
            Utils.DrawBorderString(sb, status,
                new Vector2(panel.X + SkillEntryLeftPad, statusY),
                statusCol * a, statusScale);
        }

        /// <summary>
        /// 估算给定文本按指定最大宽度换行后的行数
        /// </summary>
        private static int MeasureWrappedLineCount(DynamicSpriteFont font, string text,
            float scale, float maxWidth) {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0f) {
                return 1;
            }
            int count = 0;
            foreach (string _ in EnumerateWrappedLines(font, text, scale, maxWidth)) {
                count++;
            }
            return count <= 0 ? 1 : count;
        }

        /// <summary>
        /// 按字符级别贪心切分文本以适配最大宽度，逐行 yield
        /// 中英混排下足以胜任，避免在描述里插换行符
        /// </summary>
        private static System.Collections.Generic.IEnumerable<string> EnumerateWrappedLines(
            DynamicSpriteFont font, string text, float scale, float maxWidth) {
            if (string.IsNullOrEmpty(text)) {
                yield break;
            }
            if (maxWidth <= 1f) {
                yield return text;
                yield break;
            }
            int start = 0;
            int n = text.Length;
            while (start < n) {
                int len = 1;
                int lastFit = 1;
                while (start + len <= n) {
                    float w = font.MeasureString(text.AsSpan(start, len).ToString()).X * scale;
                    if (w <= maxWidth) {
                        lastFit = len;
                        len++;
                    }
                    else {
                        break;
                    }
                }
                yield return text.Substring(start, lastFit);
                start += lastFit;
            }
        }

        /// <summary>
        /// 简易文本宽度截断，超出 maxWidth 时尾部加省略号
        /// </summary>
        private static string TrimToWidth(DynamicSpriteFont font, string text,
            float scale, float maxWidth) {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0f) {
                return text ?? string.Empty;
            }
            if (font.MeasureString(text).X * scale <= maxWidth) {
                return text;
            }
            const string ellipsis = "...";
            float ellipsisW = font.MeasureString(ellipsis).X * scale;
            int n = text.Length;
            while (n > 0 && font.MeasureString(text[..n]).X * scale + ellipsisW > maxWidth) {
                n--;
            }
            return n <= 0 ? ellipsis : text[..n] + ellipsis;
        }

        /// <summary>
        /// 段位与三级面板之间的连接弧线，沿径向延伸到面板左边缘中点
        /// </summary>
        private static void DrawSkillConnector(SpriteBatch sb, Texture2D px,
            in Layout layout, int layer, Rectangle drawRect,
            float expandAmt, float baseAlpha, Color glow) {
            if (expandAmt < 0.05f) {
                return;
            }
            float a = baseAlpha * MathHelper.Clamp(expandAmt * 1.2f, 0f, 1f);
            GetSegmentAngles(layer, out float a0, out float a1);
            float midA = (a0 + a1) * 0.5f;
            float outerR = RingOuterR + expandAmt * SegmentExpandMax + 5f * Scale;
            Vector2 segPoint = layout.RingCenter + SHPCRenderer.AngleDir(midA) * outerR;
            Vector2 panelAttach = new(drawRect.X, drawRect.Y + drawRect.Height * 0.5f);

            //中转点：水平在面板左侧附近，给一段内凹折角
            Vector2 mid = new(panelAttach.X - 12f * Scale, panelAttach.Y);

            SHPCRenderer.DrawLine(sb, px, segPoint, mid, 1.2f, glow * (0.45f * a));
            SHPCRenderer.DrawLine(sb, px, mid, panelAttach, 1.6f, glow * (0.95f * a));

            //端点小亮点
            SHPCRenderer.DrawDisc(sb, px, panelAttach, 2.2f, 1.6f, glow * (0.9f * a));
            SHPCRenderer.DrawDisc(sb, px, segPoint, 1.8f, 1.4f, glow * (0.7f * a));
        }

        /// <summary>
        /// 处理面板控件点击效果
        /// Skill1/2/3 仅作为悬停穿透判定，不响应点击
        /// </summary>
        public static void HandleClick(HitKind hit, Player owner) {
            switch (hit) {
                case HitKind.Toggle:
                    Cyberspace.Toggle(owner);
                    break;
                case HitKind.Layer1:
                case HitKind.Layer2:
                case HitKind.Layer3:
                    int target = hit == HitKind.Layer1 ? 1 : hit == HitKind.Layer2 ? 2 : 3;
                    if (!Cyberspace.Active) {
                        Cyberspace.Activate(owner);
                    }
                    Cyberspace.SetLayer(target, owner);
                    break;
            }
        }
    }
}
