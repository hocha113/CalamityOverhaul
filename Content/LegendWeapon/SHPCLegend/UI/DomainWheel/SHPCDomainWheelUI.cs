using CalamityOverhaul.Common;
using CalamityOverhaul.Content.EntrustManager;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.QuestLogs;
using CalamityOverhaul.Content.RAMSystems;
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

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI.DomainWheel
{
    /// <summary>领域转盘可视层，状态读 <see cref="SHPCDomainWheelController"/></summary>
    internal class SHPCDomainWheelUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static SHPCDomainWheelUI Instance => UIHandleLoader.GetUIHandleOfType<SHPCDomainWheelUI>();

        public static LocalizedText Layer1_Desc { get; private set; }
        public static LocalizedText Layer2_Desc { get; private set; }
        public static LocalizedText Layer3_Desc { get; private set; }
        public static LocalizedText Layer_HintActivate { get; private set; }
        public static LocalizedText Layer_HintSwitch { get; private set; }
        public static LocalizedText Layer_HintShutdown { get; private set; }
        public static LocalizedText Layer_HintLowRam { get; private set; }
        public static LocalizedText Layer_HintLocked { get; private set; }
        public static LocalizedText Hack_Title { get; private set; }
        public static LocalizedText Hack_Subtitle { get; private set; }
        public static LocalizedText Hack_Desc { get; private set; }
        public static LocalizedText Hack_HintEnter { get; private set; }
        public static LocalizedText Hack_HintExit { get; private set; }
        public static LocalizedText Hack_HintDenied { get; private set; }
        public static LocalizedText Hack_HintKeyUnbound { get; private set; }
        public static LocalizedText WheelHint { get; private set; }

        public override void SetStaticDefaults() {
            Layer1_Desc = this.GetLocalization(nameof(Layer1_Desc)
                , () => "Foundation layer — establishes the domain field around you.");
            Layer2_Desc = this.GetLocalization(nameof(Layer2_Desc)
                , () => "Deep dive — a wider field, at a much steeper RAM cost.");
            Layer3_Desc = this.GetLocalization(nameof(Layer3_Desc)
                , () => "Blackwall breach — the whole world becomes the domain.");
            Layer_HintActivate = this.GetLocalization(nameof(Layer_HintActivate), () => "[Click] Deploy");
            Layer_HintSwitch = this.GetLocalization(nameof(Layer_HintSwitch), () => "[Click] Switch layer");
            Layer_HintShutdown = this.GetLocalization(nameof(Layer_HintShutdown), () => "[Click] Shut down domain");
            Layer_HintLowRam = this.GetLocalization(nameof(Layer_HintLowRam), () => "// insufficient RAM");
            Layer_HintLocked = this.GetLocalization(nameof(Layer_HintLocked), () => "// system crash lockout");
            Hack_Title = this.GetLocalization(nameof(Hack_Title), () => "HACK TIME");
            Hack_Subtitle = this.GetLocalization(nameof(Hack_Subtitle), () => "Neural Override");
            Hack_Desc = this.GetLocalization(nameof(Hack_Desc)
                , () => "Slow the world to a crawl and run protocols on whatever you can see.");
            Hack_HintEnter = this.GetLocalization(nameof(Hack_HintEnter), () => "[Click] Enter hack time");
            Hack_HintExit = this.GetLocalization(nameof(Hack_HintExit), () => "[Click] Leave hack time");
            Hack_HintDenied = this.GetLocalization(nameof(Hack_HintDenied), () => "// no access");
            Hack_HintKeyUnbound = this.GetLocalization(nameof(Hack_HintKeyUnbound)
                , () => "// hack time key unbound");
            WheelHint = this.GetLocalization(nameof(WheelHint)
                , () => "[{0}] Hold \u00b7 LMB select \u00b7 RMB cancel");
        }

        //存活 + OpenProgress>0 时显示
        public override bool Active {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead) {
                    return false;
                }
                if (QuestLog.Instance?.visible == true || QuestManagerUI.Instance?.IsOpen == true) {
                    return false;
                }
                SHPCDomainWheelController ctrl = SHPCDomainWheelController.LocalInstance;
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
            SHPCDomainWheelController ctrl = SHPCDomainWheelController.LocalInstance;
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

            //多盘并存时全屏压暗只由归属者画一次，否则两层滤镜会叠暗
            if (RadialWheelHub.OwnsBackdrop(ctrl)) {
                DrawBulletTimeOverlay(sb, px, center, time, a);
            }
            DrawRadialBackdrop(sb, px, center, a, time);
            TryDrawShaderBackplate(sb, px, center, time, a);

            int count = ctrl.Sectors.Count;
            for (int i = 0; i < count; i++) {
                ctrl.GetSectorAngles(i, out float aStart, out float aEnd);
                SHPCDomainWheelSector sec = ctrl.Sectors[i];
                bool enabled = ctrl.IsLayerReady(sec.Layer);
                float midA = (aStart + aEnd) * 0.5f;

                //核心 → 扇区的辐射连接线
                SHPCRenderer.DrawConnector(sb, px, center, midA, a, sec.HoverAmount, a);

                //扇区底板；填充比例按该层 RAM 占当前储量的份额
                SHPCRenderer.DrawSector(sb, px, center, aStart, aEnd, a,
                    sec.HoverAmount, 0f, enabled, SustainRatio(sec.Layer),
                    glyph: string.Empty, time, a);

                if (sec.SelectedAmount > 0.01f) {
                    DrawActiveLayerOverlay(sb, px, center, aStart, aEnd, time, sec.SelectedAmount, a);
                }

                DrawLayerGlyph(sb, sec, center, midA, enabled, a);
                DrawDrainText(sb, sec, center, midA, enabled, a);
            }

            DrawHackCore(sb, px, ctrl, center, a, time);
            DrawInfoPanelForHovered(sb, px, ctrl, center, a);
            DrawOuterRing(sb, px, center, a);
            //按键提示只由最底那个盘画，否则会糊在下方盘上
            if (RadialWheelHub.OwnsHint(ctrl)) {
                DrawWheelHint(sb, center, a);
            }
        }

        /// <summary>该层能靠现有 RAM 撑多久，0~1 归一到 10 秒</summary>
        private static float SustainRatio(int layer) {
            float drain = Cyberspace.GetLayerDrainRate(layer);
            if (drain <= 0f) {
                return 0f;
            }
            return MathHelper.Clamp(RamSystem.CurrentRam / drain / 10f, 0f, 1f);
        }

        /// <summary>子弹时间滤镜；freeze 未生效不绘；无着色器压暗</summary>
        private static void DrawBulletTimeOverlay(SpriteBatch sb, Texture2D px,
            Vector2 center, float time, float globalAlpha) {
            if (!WorldFreezeSystem.IsActive) {
                return;
            }
            //全屏铺底按 UI 空间算，否则 UIScale<1 时会露边
            int w = (int)MathF.Ceiling(RadialWheelHub.UIScreenW);
            int h = (int)MathF.Ceiling(RadialWheelHub.UIScreenH);

            Effect effect = EffectLoader.CyberwareBulletTime?.Value;
            if (effect == null) {
                sb.Draw(px, new Rectangle(0, 0, w, h),
                    new Color(8, 14, 22) * (0.42f * globalAlpha));
                return;
            }

            effect.Parameters["uTime"]?.SetValue(time);
            effect.Parameters["uAlpha"]?.SetValue(globalAlpha);
            effect.Parameters["uOpenProgress"]?.SetValue(globalAlpha);
            effect.Parameters["uResolution"]?.SetValue(new Vector2(w, h));
            effect.Parameters["uCenter"]?.SetValue(center);
            effect.Parameters["uHudColor"]?.SetValue(new Vector3(0.20f, 0.65f, 0.82f));

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(px, new Rectangle(0, 0, w, h), Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        /// <summary>外框描边 + 底投影 + 呼吸光晕</summary>
        private static void DrawRadialBackdrop(SpriteBatch sb, Texture2D px,
            Vector2 center, float globalAlpha, float time) {
            const float frameR = SHPCTheme.ButtonOuterR + 18f;
            SHPCRenderer.DrawArcStroke(sb, px, center, frameR, 0f, MathHelper.TwoPi,
                1.6f, SHPCTheme.Border * (0.65f * globalAlpha));
            float pulse = 0.55f + 0.45f * (MathF.Sin(time * 1.8f) + 1f) * 0.5f;
            SHPCRenderer.DrawArcStroke(sb, px, center, frameR + 4f, 0f, MathHelper.TwoPi,
                1.0f, SHPCTheme.CyanHi * (0.32f * pulse * globalAlpha));

            SHPCRenderer.DrawArc(sb, px, center + new Vector2(0f, 6f),
                frameR - 1f, frameR + 7f, 0.10f, MathHelper.Pi - 0.10f,
                new Color(0, 4, 8) * (0.45f * globalAlpha));
        }

        /// <summary>借用 CyberwareRadialPanel 铺底，缺 fxc 时静默走 CPU</summary>
        private static void TryDrawShaderBackplate(SpriteBatch sb, Texture2D px,
            Vector2 center, float time, float openProgress) {
            Effect effect = EffectLoader.CyberwareRadialPanel?.Value;
            if (effect == null) {
                return;
            }

            const float decoExtend = 12f;
            const float padding = 20f;
            float halfSize = SHPCTheme.ButtonOuterR + decoExtend + padding;

            float qLeft = center.X - halfSize;
            float qTop = center.Y - halfSize;
            int qSize = (int)MathF.Ceiling(halfSize * 2f);
            if (qSize <= 0) {
                return;
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
            effect.Parameters["uDeadZoneR"]?.SetValue(SHPCDomainWheelController.DeadZoneRadius);
            effect.Parameters["uDecoOuterR"]?.SetValue(SHPCTheme.ButtonOuterR + decoExtend);

            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);

            sb.Draw(px, dest, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);
        }

        /// <summary>当前所在层的高亮叠层</summary>
        private static void DrawActiveLayerOverlay(SpriteBatch sb, Texture2D px,
            Vector2 center, float aStart, float aEnd, float time, float amount, float globalAlpha) {
            float a = MathHelper.Clamp(amount, 0f, 1f) * globalAlpha;
            float pulse = 0.75f + 0.25f * (MathF.Sin(time * 3.6f) + 1f) * 0.5f;

            SHPCRenderer.DrawArcStroke(sb, px, center,
                SHPCTheme.ButtonInnerR + 1.8f, aStart, aEnd,
                1.6f, SHPCTheme.CyanHi * (0.85f * pulse * a));
            SHPCRenderer.DrawArcStroke(sb, px, center,
                SHPCTheme.ButtonOuterR - 2.4f, aStart, aEnd,
                2.2f, SHPCTheme.Accent * (0.7f * pulse * a));

            //中线三角指示当前层
            float midA = (aStart + aEnd) * 0.5f;
            Vector2 dir = SHPCRenderer.AngleDir(midA);
            Vector2 tangent = new(-dir.Y, dir.X);
            Vector2 tip = center + dir * (SHPCTheme.ButtonInnerR + 8f);
            Vector2 baseL = tip - dir * 6f + tangent * 4f;
            Vector2 baseR = tip - dir * 6f - tangent * 4f;
            SHPCRenderer.DrawLine(sb, px, tip, baseL, 1.5f, SHPCTheme.CyanHi * (0.9f * a));
            SHPCRenderer.DrawLine(sb, px, tip, baseR, 1.5f, SHPCTheme.CyanHi * (0.9f * a));
            SHPCRenderer.DrawLine(sb, px, baseL, baseR, 1.0f, SHPCTheme.CyanHi * (0.55f * a));
        }

        /// <summary>扇区中线的层号字形</summary>
        private static void DrawLayerGlyph(SpriteBatch sb, SHPCDomainWheelSector sec,
            Vector2 center, float midA, bool enabled, float globalAlpha) {
            string glyph = $"L{sec.Layer}";
            Vector2 pos = center + SHPCRenderer.AngleDir(midA) * SHPCTheme.ButtonMidR;
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float scale = 0.92f + sec.HoverAmount * 0.10f;
            Vector2 size = font.MeasureString(glyph) * scale;
            Color col = enabled
                ? Color.Lerp(SHPCTheme.Text, SHPCTheme.CyanHi, MathF.Max(sec.HoverAmount, sec.SelectedAmount))
                : SHPCTheme.Disabled;
            Utils.DrawBorderString(sb, glyph, pos - size * 0.5f, col * globalAlpha, scale);
        }

        /// <summary>层号外侧的每秒 RAM 消耗</summary>
        private static void DrawDrainText(SpriteBatch sb, SHPCDomainWheelSector sec,
            Vector2 center, float midA, bool enabled, float globalAlpha) {
            float drain = Cyberspace.GetLayerDrainRate(sec.Layer);
            if (drain <= 0f) {
                return;
            }
            string text = string.Format(SHPCUI.Cyber_DrainPerSec.Value, drain.ToString("F1"));
            Vector2 pos = center + SHPCRenderer.AngleDir(midA) * (SHPCTheme.ButtonOuterR - 11f);
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(text) * 0.5f;
            Color col = enabled ? new Color(255, 210, 180) : new Color(150, 100, 100);
            Utils.DrawBorderString(sb, text, pos - size * 0.5f, col * globalAlpha, 0.5f);
        }

        /// <summary>中心骇客按钮，领域在线时随强度呼吸</summary>
        private static void DrawHackCore(SpriteBatch sb, Texture2D px,
            SHPCDomainWheelController ctrl, Vector2 center, float globalAlpha, float time) {
            bool ready = ctrl.IsHackReady();
            float hover = ctrl.CenterHoverAmount;
            //领域在线时中心随场强呼吸，离线时只做基础脉动
            float domain = MathHelper.Clamp(Cyberspace.Intensity, 0f, 1f);
            float pulse = 0.82f + 0.18f * (MathF.Sin(time * (2.6f + domain * 2.2f)) + 1f) * 0.5f;
            float r = SHPCTheme.CoreRadius * (0.82f + hover * 0.16f) * pulse;

            Color baseCol = ready ? SHPCTheme.Cyan : SHPCTheme.Disabled;
            Color hiCol = ready ? SHPCTheme.CyanHi : SHPCTheme.Disabled;

            //底盘与外环
            SHPCRenderer.DrawDisc(sb, px, center, r, 4f,
                new Color(4, 14, 20) * (0.92f * globalAlpha));
            SHPCRenderer.DrawRing(sb, px, center, SHPCTheme.CoreRingR * (0.94f + hover * 0.06f),
                1.5f, Color.Lerp(baseCol, hiCol, hover) * (0.85f * globalAlpha));

            //十字准星，骇入的读法
            float armIn = r * 0.42f;
            float armOut = SHPCTheme.CoreRingR - 3f;
            for (int i = 0; i < 4; i++) {
                Vector2 dir = SHPCRenderer.AngleDir(MathHelper.PiOver2 * i + MathHelper.PiOver4);
                SHPCRenderer.DrawLine(sb, px, center + dir * armIn, center + dir * armOut,
                    1.3f, hiCol * ((0.35f + hover * 0.45f) * globalAlpha));
            }

            //核心亮点
            SHPCRenderer.DrawDisc(sb, px, center, r * 0.34f, 2.2f,
                hiCol * ((0.75f + hover * 0.25f) * globalAlpha));

            //骇客生效中时补一圈暖环，与"再点一次退出"对应
            if (HackTime.Active) {
                SHPCRenderer.DrawRing(sb, px, center, SHPCTheme.CoreRingR + 3f, 1.8f,
                    SHPCTheme.Accent * (0.75f * pulse * globalAlpha));
            }
        }

        /// <summary>悬停信息板，中心与扇区共用一块</summary>
        private void DrawInfoPanelForHovered(SpriteBatch sb, Texture2D px,
            SHPCDomainWheelController ctrl, Vector2 center, float globalAlpha) {
            string title;
            string subtitle;
            string description;
            string status;

            if (ctrl.CenterHovered) {
                title = Hack_Title.Value;
                subtitle = Hack_Subtitle.Value;
                description = Hack_Desc.Value;
                //先分清"没准入"和"键没绑"，后者要指路去设置
                bool access = HackTimeAccess.CanUse(Main.LocalPlayer);
                bool keyBound = !CWRKeySystem.IsKeybindUnbound(CWRKeySystem.HackTime_Toggle);
                status = !access ? Hack_HintDenied.Value
                    : !keyBound ? Hack_HintKeyUnbound.Value
                    : HackTime.Active ? Hack_HintExit.Value : Hack_HintEnter.Value;
            }
            else {
                int idx = ctrl.HoveredIndex;
                if (idx < 0 || idx >= ctrl.Sectors.Count) {
                    return;
                }
                SHPCDomainWheelSector sec = ctrl.Sectors[idx];
                bool isActiveLayer = Cyberspace.Active && Cyberspace.CurrentLayer == sec.Layer;
                title = LayerTitle(sec.Layer);
                subtitle = $"{SHPCUI.Cyber_LayerLabel.Value} {sec.Layer} \u00b7 "
                    + string.Format(SHPCUI.Cyber_DrainPerSec.Value,
                        Cyberspace.GetLayerDrainRate(sec.Layer).ToString("F1"));
                description = LayerDesc(sec.Layer);
                status = Cyberspace.IsCrashLockedOut ? Layer_HintLocked.Value
                    : isActiveLayer ? Layer_HintShutdown.Value
                    : !Cyberspace.CanAffordLayer(sec.Layer) ? Layer_HintLowRam.Value
                    : Cyberspace.Active ? Layer_HintSwitch.Value : Layer_HintActivate.Value;
            }

            //估算面板尺寸，挑空间多的一侧放，防溢出
            const float estPanelW = 230f;
            const float estPanelH = 130f;
            const float clearance = 14f;
            float radialEdge = SHPCTheme.ButtonOuterR + 6f;

            float spaceRight = RadialWheelHub.UIScreenW - (center.X + radialEdge + clearance) - estPanelW;
            float spaceLeft = center.X - radialEdge - clearance - estPanelW;
            bool placeLeft = spaceLeft > spaceRight && spaceLeft > 0f;

            float panelX = placeLeft
                ? center.X - radialEdge - clearance - estPanelW
                : center.X + radialEdge + clearance;
            float panelY = center.Y - estPanelH * 0.5f;
            float slide = (1f - globalAlpha) * 8f;

            Vector2 anchor = new(panelX - 18f - slide, panelY - 14f);
            anchor.X = MathHelper.Clamp(anchor.X, 4f, RadialWheelHub.UIScreenW - 4f);
            anchor.Y = MathHelper.Clamp(anchor.Y, 4f, RadialWheelHub.UIScreenH - 4f);

            SHPCRenderer.DrawInfoPanel(sb, px, anchor, globalAlpha, globalAlpha,
                title, subtitle, description, status);
        }

        private static string LayerTitle(int layer) => layer switch {
            1 => SHPCUI.Cyber_Layer1_Title.Value,
            2 => SHPCUI.Cyber_Layer2_Title.Value,
            _ => SHPCUI.Cyber_Layer3_Title.Value,
        };

        private static string LayerDesc(int layer) => layer switch {
            1 => Layer1_Desc.Value,
            2 => Layer2_Desc.Value,
            _ => Layer3_Desc.Value,
        };

        /// <summary>外圈细描边</summary>
        private static void DrawOuterRing(SpriteBatch sb, Texture2D px,
            Vector2 center, float globalAlpha) {
            SHPCRenderer.DrawArcStroke(sb, px, center, SHPCTheme.ButtonOuterR + 4f,
                0f, MathHelper.TwoPi, 1.0f, SHPCTheme.Border * (0.45f * globalAlpha));
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
    }
}
