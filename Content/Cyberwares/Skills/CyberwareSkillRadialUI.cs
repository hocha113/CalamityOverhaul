using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.TimeFreezes;
using CalamityOverhaul.Content.UIs.RadialWheels;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Skills
{
    /// <summary>雷达可视层，状态读 Controller</summary>
    internal class CyberwareSkillRadialUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static CyberwareSkillRadialUI Instance => UIHandleLoader.GetUIHandleOfType<CyberwareSkillRadialUI>();

        public static LocalizedText StatusOn { get; private set; }
        public static LocalizedText HintClickToSelect { get; private set; }
        public static LocalizedText HintAlreadySelected { get; private set; }
        public static LocalizedText HintKindCharge { get; private set; }
        public static LocalizedText HintKindToggle { get; private set; }
        public static LocalizedText HintKindInstant { get; private set; }
        public static LocalizedText HintNotReady { get; private set; }
        public static LocalizedText WheelHint { get; private set; }

        public override void SetStaticDefaults() {
            //本地化集中于此
            StatusOn = this.GetLocalization(nameof(StatusOn), () => "ON");
            HintClickToSelect = this.GetLocalization(nameof(HintClickToSelect), () => "[点击] 选定为当前技能");
            HintAlreadySelected = this.GetLocalization(nameof(HintAlreadySelected), () => "[当前选定]");
            HintKindCharge = this.GetLocalization(nameof(HintKindCharge), () => "蓄力型 · 按住触发键释放");
            HintKindToggle = this.GetLocalization(nameof(HintKindToggle), () => "开关型 · 按触发键切换");
            HintKindInstant = this.GetLocalization(nameof(HintKindInstant), () => "瞬发型 · 按触发键释放");
            HintNotReady = this.GetLocalization(nameof(HintNotReady), () => "条件不满足");
            WheelHint = this.GetLocalization(nameof(WheelHint)
                , () => "[{0}] Hold \u00b7 release to confirm \u00b7 LMB select \u00b7 RMB cancel");
        }

        //存活+有主动技能+OpenProgress>0 时显示
        public override bool Active {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead) {
                    return false;
                }
                if (QuestLog.Instance?.visible == true || QuestManagerUI.Instance?.IsOpen == true) {
                    return false;
                }
                CyberwareSkillRadialController ctrl = CyberwareSkillRadialController.LocalInstance;
                if (ctrl == null) {
                    return false;
                }
                //开/关过程继续绘至 OpenProgress 归零
                return ctrl.IsOpen || ctrl.OpenProgress > 0.01f;
            }
        }

        public override void Update() {
            //本 UI 无输入，全由 Controller
        }

        public override void Draw(SpriteBatch sb) {
            CyberwareSkillRadialController ctrl = CyberwareSkillRadialController.LocalInstance;
            if (ctrl == null) {
                return;
            }
            Texture2D px = VaultAsset.placeholder2.Value;
            if (px == null) {
                return;
            }

            float a = MathHelper.Clamp(ctrl.OpenProgress, 0f, 1f);
            if (a < 0.01f) {
                return;
            }

            //中心由 Hub 排布，命中与绘制共用
            Vector2 center = ctrl.ScreenAnchor;

            float time = ctrl.Time;

            //子弹时间全屏滤镜；多盘并存时只由归属者画一次，否则两层滤镜会叠暗
            if (RadialWheelHub.OwnsBackdrop(ctrl)) {
                DrawBulletTimeOverlay(sb, px, a);
            }

            //雷达底盘圆盘
            DrawRadialBackdrop(sb, px, center, a, time);

            //CyberwareRadialPanel 着色器铺底，失败走 CPU
            TryDrawShaderBackplate(sb, px, center, time, a);

            DrawCenterCore(sb, px, center, a, time);

            //每个扇区分别绘制
            int count = ctrl.Sectors.Count;
            for (int i = 0; i < count; i++) {
                ctrl.GetSectorAngles(i, out float aStart, out float aEnd);
                CyberwareSkillRadialSector sec = ctrl.Sectors[i];
                bool enabled = sec.Skill.IsReady;
                float fill = MathHelper.Clamp(sec.Skill.StatusFillRatio, 0f, 1f);

                //核心 → 扇区的辐射连接线
                float midA = (aStart + aEnd) * 0.5f;
                SHPCRenderer.DrawConnector(sb, px, center, midA, a, sec.HoverAmount, a);

                //扇区底板与状态弧填充
                SHPCRenderer.DrawSector(sb, px, center, aStart, aEnd, a,
                    sec.HoverAmount, 0f, enabled, fill,
                    glyph: string.Empty, time, a);

                //Toggle 开时金环
                if (sec.Skill.Kind == CyberwareSkillKind.Toggle && sec.Skill.IsActivated) {
                    DrawToggleActiveOverlay(sb, px, center, aStart, aEnd, time, a);
                }

                //选中叠层
                if (sec.SelectedAmount > 0.01f) {
                    DrawSelectedOverlay(sb, px, center, aStart, aEnd, time, sec.SelectedAmount, a);
                }

                //扇区中线图标
                DrawItemIcon(sb, sec, center, midA, sec.HoverAmount, enabled, a);

                //右上状态字
                DrawStatusText(sb, sec, center, midA, a);
            }

            //悬停信息板
            DrawInfoPanelForHovered(sb, px, ctrl, center, a);

            //外框
            DrawOuterRing(sb, px, center, count, a, time);

            //按键提示只由最底那个盘画，否则会糊在下方盘上
            if (RadialWheelHub.OwnsHint(ctrl)) {
                DrawWheelHint(sb, center, a);
            }
        }

        /// <summary>转盘底部按键提示</summary>
        private static void DrawWheelHint(SpriteBatch sb, Vector2 center, float globalAlpha) {
            string keyText = CWRKeySystem.GetKeybindText(CWRKeySystem.RadialWheel_Key,
                CWRKeySystem.Notbound.Value);
            string text = string.Format(WheelHint.Value, keyText);
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            const float scale = 0.62f;
            Vector2 size = font.MeasureString(text) * scale;
            Vector2 pos = center + new Vector2(-size.X * 0.5f, SHPCTheme.ButtonOuterR + 30f);
            Utils.DrawBorderString(sb, text, pos, SHPCTheme.TextDim * (0.9f * globalAlpha), scale);
        }

        /// <summary>
        /// 子弹时间滤镜；freeze 失败不绘；无着色器压暗；alpha 跟 OpenProgress
        /// </summary>
        private static void DrawBulletTimeOverlay(SpriteBatch sb, Texture2D px, float globalAlpha) {
            if (!WorldFreezeSystem.IsActive) {
                return;
            }

            //全屏铺底同样按 UI 空间算，否则 UIScale<1 时会露边
            int w = (int)MathF.Ceiling(RadialWheelHub.UIScreenW);
            int h = (int)MathF.Ceiling(RadialWheelHub.UIScreenH);
            CyberwareSkillRadialController ctrl = CyberwareSkillRadialController.LocalInstance;
            //freeze 时用 ctrl.Time
            float time = ctrl != null ? ctrl.Time : 0f;
            Vector2 anchor = ctrl?.ScreenAnchor ?? RadialWheelHub.ResolveAnchor();

            Effect effect = EffectLoader.CyberwareBulletTime?.Value;
            if (effect == null) {
                //无着色器压暗
                sb.Draw(px, new Rectangle(0, 0, w, h),
                    new Color(8, 14, 22) * (0.42f * globalAlpha));
                return;
            }

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(globalAlpha);
            effect.Parameters["uOpenProgress"]?.SetValue(globalAlpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(w, h));
            effect.Parameters["uCenter"]?.SetValue(anchor);
            //与雷达同青蓝
            effect.Parameters["uHudColor"]?.SetValue(new Vector3(0.20f, 0.65f, 0.82f));

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(px, new Rectangle(0, 0, w, h), Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        /// <summary>外框描边+底投影+呼吸光晕</summary>
        private static void DrawRadialBackdrop(SpriteBatch sb, Texture2D px,
            Vector2 center, float globalAlpha, float time) {
            const float frameR = SHPCTheme.ButtonOuterR + 18f;
            SHPCRenderer.DrawArcStroke(sb, px, center, frameR, 0f, MathHelper.TwoPi,
                1.6f, SHPCTheme.Border * (0.65f * globalAlpha));
            //外侧呼吸
            float pulse = 0.55f + 0.45f * (MathF.Sin(time * 1.8f) + 1f) * 0.5f;
            SHPCRenderer.DrawArcStroke(sb, px, center, frameR + 4f, 0f, MathHelper.TwoPi,
                1.0f, SHPCTheme.CyanHi * (0.32f * pulse * globalAlpha));

            //底投影
            SHPCRenderer.DrawArc(sb, px, center + new Vector2(0f, 6f),
                frameR - 1f, frameR + 7f, 0.10f, MathHelper.Pi - 0.10f,
                new Color(0, 4, 8) * (0.45f * globalAlpha));
        }

        /// <summary>CyberwareRadialPanel 着色器铺底，失败 return 走 CPU</summary>
        private static bool TryDrawShaderBackplate(SpriteBatch sb, Texture2D px,
            Vector2 center, float time, float openProgress) {
            Effect effect = EffectLoader.CyberwareRadialPanel?.Value;
            if (effect == null) {
                return false;
            }

            //quad 覆盖 OuterR+刻度+边距
            const float decoExtend = 12f;
            const float padding = 20f;
            float halfSize = SHPCTheme.ButtonOuterR + decoExtend + padding;

            float qLeft = center.X - halfSize;
            float qTop = center.Y - halfSize;
            int qSize = (int)MathF.Ceiling(halfSize * 2f);
            if (qSize <= 0) {
                return true;
            }

            Rectangle dest = new((int)qLeft, (int)qTop, qSize, qSize);
            Vector2 relCenter = new(center.X - qLeft, center.Y - qTop);

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(openProgress);
            effect.Parameters["uOpenProgress"]?.SetValue(openProgress);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(qSize, qSize));
            effect.Parameters["uCenter"]?.SetValue(relCenter);
            effect.Parameters["uInnerR"]?.SetValue(SHPCTheme.ButtonInnerR);
            effect.Parameters["uOuterR"]?.SetValue(SHPCTheme.ButtonOuterR);
            effect.Parameters["uDeadZoneR"]?.SetValue(CyberwareSkillRadialController.DeadZoneRadius);
            effect.Parameters["uDecoOuterR"]?.SetValue(SHPCTheme.ButtonOuterR + decoExtend);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(px, dest, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);

            return true;
        }

        /// <summary>中心瞄星，iris 靠着色器</summary>
        private static void DrawCenterCore(SpriteBatch sb, Texture2D px, Vector2 center, float a, float time) {
            float breath = 0.85f + MathF.Sin(time * 2.8f) * 0.15f;
            SHPCRenderer.DrawDisc(sb, px, center, 1.8f * breath, 1.0f,
                SHPCTheme.CyanHi * (0.95f * a));
        }

        /// <summary>Toggle 激活金环</summary>
        private static void DrawToggleActiveOverlay(SpriteBatch sb, Texture2D px,
            Vector2 center, float aStart, float aEnd, float time, float globalAlpha) {
            float pulse = 0.65f + 0.35f * (MathF.Sin(time * 4.2f) + 1f) * 0.5f;
            SHPCRenderer.DrawArcStroke(sb, px, center,
                SHPCTheme.ButtonOuterR - 2.4f, aStart, aEnd,
                2.6f, SHPCTheme.Accent * (pulse * 0.7f * globalAlpha));
        }

        /// <summary>选中叠层，内外缘+中线三角</summary>
        private static void DrawSelectedOverlay(SpriteBatch sb, Texture2D px,
            Vector2 center, float aStart, float aEnd, float time, float selectedAmt, float globalAlpha) {
            float a = MathHelper.Clamp(selectedAmt, 0f, 1f) * globalAlpha;
            float pulse = 0.75f + 0.25f * (MathF.Sin(time * 3.6f) + 1f) * 0.5f;

            //内缘双线
            SHPCRenderer.DrawArcStroke(sb, px, center,
                SHPCTheme.ButtonInnerR + 1.8f, aStart, aEnd,
                1.6f, SHPCTheme.CyanHi * (0.85f * pulse * a));
            SHPCRenderer.DrawArcStroke(sb, px, center,
                SHPCTheme.ButtonInnerR + 4.0f, aStart, aEnd,
                1.0f, SHPCTheme.Cyan * (0.45f * a));

            //外缘亮带
            SHPCRenderer.DrawArcStroke(sb, px, center,
                SHPCTheme.ButtonOuterR - 0.5f, aStart, aEnd,
                1.4f, SHPCTheme.CyanHi * (0.55f * pulse * a));

            //中线三角
            float midA = (aStart + aEnd) * 0.5f;
            Vector2 dir = new(MathF.Cos(midA), MathF.Sin(midA));
            Vector2 tangent = new(-dir.Y, dir.X);
            Vector2 tip = center + dir * (SHPCTheme.ButtonInnerR + 8f);
            Vector2 baseL = tip - dir * 6f + tangent * 4f;
            Vector2 baseR = tip - dir * 6f - tangent * 4f;
            SHPCRenderer.DrawLine(sb, px, tip, baseL, 1.5f, SHPCTheme.CyanHi * (0.9f * a));
            SHPCRenderer.DrawLine(sb, px, tip, baseR, 1.5f, SHPCTheme.CyanHi * (0.9f * a));
            SHPCRenderer.DrawLine(sb, px, baseL, baseR, 1.0f, SHPCTheme.CyanHi * (0.55f * a));
        }

        /// <summary>扇区中线图标，≤28px</summary>
        private static void DrawItemIcon(SpriteBatch sb, CyberwareSkillRadialSector sec,
            Vector2 center, float midA, float hoverAmt, bool enabled, float globalAlpha) {
            int type = sec.Skill.IconItemType;
            if (type <= 0) {
                return;
            }
            Texture2D tex = TextureAssets.Item[type]?.Value;
            if (tex == null) {
                return;
            }
            //中点半径，悬停微跳
            float jump = 1f + hoverAmt * 0.12f;
            Vector2 dir = new(MathF.Cos(midA), MathF.Sin(midA));
            Vector2 iconCenter = center + dir * SHPCTheme.ButtonMidR;
            float maxDim = Math.Max(tex.Width, tex.Height);
            float scale = maxDim > 28f ? 28f / maxDim : 1f;
            scale *= jump;
            Color tint = enabled ? Color.White : SHPCTheme.Disabled;
            sb.Draw(tex, iconCenter, null, tint * globalAlpha, 0f,
                tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
        }

        /// <summary>扇区右上角状态字</summary>
        private static void DrawStatusText(SpriteBatch sb, CyberwareSkillRadialSector sec,
            Vector2 center, float midA, float globalAlpha) {
            string text = sec.Skill.StatusText;
            if (string.IsNullOrEmpty(text)) {
                return;
            }
            Vector2 dir = new(MathF.Cos(midA), MathF.Sin(midA));
            //略外侧偏切向
            Vector2 tangent = new(-dir.Y, dir.X);
            Vector2 pos = center + dir * (SHPCTheme.ButtonOuterR - 8f) + tangent * 10f;
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(text) * 0.55f;
            Utils.DrawBorderString(sb, text, pos - size * 0.5f,
                SHPCTheme.CyanHi * globalAlpha, 0.55f);
        }

        /// <summary>悬停信息面板，布置在雷达外侧</summary>
        private static void DrawInfoPanelForHovered(SpriteBatch sb, Texture2D px,
            CyberwareSkillRadialController ctrl, Vector2 center, float globalAlpha) {
            int idx = ctrl.HoveredIndex;
            if (idx < 0 || idx >= ctrl.Sectors.Count) {
                return;
            }
            CyberwareSkillRadialSector sec = ctrl.Sectors[idx];

            //DrawInfoPanel 估算尺寸，防溢出误判 flip
            const float estPanelW = 230f;
            const float estPanelH = 130f;
            const float clearance = 14f;
            float radialEdge = SHPCTheme.ButtonOuterR + 6f;

            //空间多的一侧
            float spaceRight = RadialWheelHub.UIScreenW - (center.X + radialEdge + clearance) - estPanelW;
            float spaceLeft = (center.X - radialEdge - clearance) - estPanelW;
            bool placeLeft = spaceLeft > spaceRight && spaceLeft > 0f;

            float panelX = placeLeft
                ? center.X - radialEdge - clearance - estPanelW
                : center.X + radialEdge + clearance;
            float panelY = center.Y - estPanelH * 0.5f;
            //展开未满时 slide 提前减
            float slide = (1f - globalAlpha) * 8f;

            //反推 cursor 命中内部默认偏移
            Vector2 anchor = new(panelX - 18f - slide, panelY - 14f);
            anchor.X = MathHelper.Clamp(anchor.X, 4f, RadialWheelHub.UIScreenW - 4f);
            anchor.Y = MathHelper.Clamp(anchor.Y, 4f, RadialWheelHub.UIScreenH - 4f);

            //已选以 Resolved 为准
            string effectiveId = ctrl.ResolvedCurrentSkill?.Identifier ?? ctrl.CurrentSkillId;
            bool isSelected = string.Equals(sec.Skill.Identifier, effectiveId,
                StringComparison.Ordinal);
            string subtitle = BuildSubtitle(sec.Skill, isSelected);
            string fullDesc = AppendKindHint(sec.Skill);
            SHPCRenderer.DrawInfoPanel(sb, px, anchor, globalAlpha, globalAlpha,
                title: sec.Skill.DisplayName,
                subtitle: subtitle,
                description: fullDesc,
                statusText: sec.Skill.StatusText);
        }

        /// <summary>副标题 已选/可点/不可用</summary>
        private static string BuildSubtitle(CyberwareSkillBase skill, bool isSelected) {
            if (isSelected) {
                return HintAlreadySelected.Value;
            }
            if (!skill.IsReady) {
                return HintNotReady.Value;
            }
            return HintClickToSelect.Value;
        }

        /// <summary>描述末尾附技能类型触发提示</summary>
        private static string AppendKindHint(CyberwareSkillBase skill) {
            string kindHint = skill.Kind switch {
                CyberwareSkillKind.Charge => HintKindCharge.Value,
                CyberwareSkillKind.Toggle => HintKindToggle.Value,
                CyberwareSkillKind.Instant => HintKindInstant.Value,
                _ => string.Empty,
            };
            if (string.IsNullOrEmpty(kindHint)) {
                return skill.Description ?? string.Empty;
            }
            if (string.IsNullOrEmpty(skill.Description)) {
                return kindHint;
            }
            return skill.Description + "\n" + kindHint;
        }

        /// <summary>外圈细描边，刻度已迁入着色器</summary>
        private static void DrawOuterRing(SpriteBatch sb, Texture2D px,
            Vector2 center, int sectorCount, float globalAlpha, float time) {
            if (sectorCount <= 1) {
                return;
            }
            float ringR = SHPCTheme.ButtonOuterR + 4f;
            SHPCRenderer.DrawArcStroke(sb, px, center, ringR,
                0f, MathHelper.TwoPi, 1.0f,
                SHPCTheme.Border * (0.45f * globalAlpha));
        }
    }
}
