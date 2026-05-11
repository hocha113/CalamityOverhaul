using CalamityOverhaul.Content.ADV.EntrustManager;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.UI;
using CalamityOverhaul.Content.QuestLogs;
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
    /// <summary>
    /// 义体技能雷达的可视层 —— 纯绘制，状态全部从 <see cref="CyberwareSkillRadialController"/> 读取
    /// <list type="bullet">
    ///   <item>使用 <see cref="SHPCRenderer"/> 的底层弧形绘制原语，与 SHPC 系列 UI 视觉语言保持一致</item>
    ///   <item>扇区图标复用各义体物品的原版纹理，无需额外美术资源</item>
    ///   <item>悬停扇区的信息面板复用 <see cref="SHPCRenderer.DrawInfoPanel"/></item>
    /// </list>
    /// </summary>
    internal class CyberwareSkillRadialUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "UI";
        public static CyberwareSkillRadialUI Instance => UIHandleLoader.GetUIHandleOfType<CyberwareSkillRadialUI>();

        public static LocalizedText StatusOn { get; private set; }
        public static LocalizedText HintHoldToCharge { get; private set; }
        public static LocalizedText HintReleaseToFire { get; private set; }
        public static LocalizedText HintToggle { get; private set; }
        public static LocalizedText HintInstant { get; private set; }
        public static LocalizedText HintNotReady { get; private set; }

        public override void SetStaticDefaults() {
            //本地化全部集中在这里，避免与具体义体的本地化文件混淆
            StatusOn = this.GetLocalization(nameof(StatusOn), () => "ON");
            HintHoldToCharge = this.GetLocalization(nameof(HintHoldToCharge), () => "悬停蓄力，松开释放");
            HintReleaseToFire = this.GetLocalization(nameof(HintReleaseToFire), () => "松开按键释放");
            HintToggle = this.GetLocalization(nameof(HintToggle), () => "松开按键切换开关");
            HintInstant = this.GetLocalization(nameof(HintInstant), () => "松开按键立即触发");
            HintNotReady = this.GetLocalization(nameof(HintNotReady), () => "条件不满足");
        }

        //仅在玩家存活、有任意主动技能义体、且雷达进度大于阈值时显示
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
                //打开中 / 关闭过程中都需要继续绘制，等待 OpenProgress 自然归零
                return ctrl.IsOpen || ctrl.OpenProgress > 0.01f;
            }
        }

        public override void Update() {
            //本 UI 不做任何输入处理，所有状态由 Controller 提供
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

            Vector2 center = ctrl.ScreenAnchor;
            float time = ctrl.Time;

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

                //蓄力技能的额外亮带：在外缘叠加一道暖色高光以突出蓄力进度
                if (sec.Skill.Kind == CyberwareSkillKind.Charge && sec.Skill.RadialChargeRatio > 0.01f) {
                    DrawChargeOverlay(sb, px, center, aStart, aEnd, sec.Skill.RadialChargeRatio, a);
                }

                //Toggle 类已激活时叠一道金色环带，提示"开启中"
                if (sec.Skill.Kind == CyberwareSkillKind.Toggle && sec.Skill.IsActivated) {
                    DrawToggleActiveOverlay(sb, px, center, aStart, aEnd, time, a);
                }

                //物品图标，绘制在扇区中线上
                DrawItemIcon(sb, sec, center, midA, sec.HoverAmount, enabled, a);

                //右上角状态文字（"ON" / "12s" / "RAM 3" / 百分比等）
                DrawStatusText(sb, sec, center, midA, a);
            }

            //悬停信息面板：仅在悬停且雷达完全展开时显示，避开雷达扇区外侧
            DrawInfoPanelForHovered(sb, px, ctrl, center, a);

            //雷达边缘的视觉框架装饰
            DrawOuterRing(sb, px, center, count, a, time);
        }

        /// <summary>
        /// 中心小核心：单纯的视觉地标，告诉玩家"这是技能盘的圆心"
        /// </summary>
        private static void DrawCenterCore(SpriteBatch sb, Texture2D px, Vector2 center, float a, float time) {
            //柔和外晕
            SHPCRenderer.DrawDisc(sb, px, center,
                CyberwareSkillRadialController.DeadZoneRadius * 0.42f, 6f,
                SHPCTheme.Cyan * (0.18f * a));
            //核心圆点
            float breath = 0.85f + MathF.Sin(time * 4.2f) * 0.12f;
            SHPCRenderer.DrawDisc(sb, px, center,
                4.5f * breath, 2.5f,
                SHPCTheme.CyanHi * (0.85f * a));
            //十字微元素
            SHPCRenderer.DrawLine(sb, px,
                center - new Vector2(3.5f, 0f), center + new Vector2(3.5f, 0f),
                1.0f, SHPCTheme.SlotBg * (0.9f * a));
            SHPCRenderer.DrawLine(sb, px,
                center - new Vector2(0f, 3.5f), center + new Vector2(0f, 3.5f),
                1.0f, SHPCTheme.SlotBg * (0.9f * a));
        }

        /// <summary>
        /// 蓄力进度高光带：沿扇区外缘按比例覆盖一道暖色弧线，模拟"蓄能上膛"
        /// </summary>
        private static void DrawChargeOverlay(SpriteBatch sb, Texture2D px,
            Vector2 center, float aStart, float aEnd, float ratio, float globalAlpha) {
            float fillEnd = MathHelper.Lerp(aStart, aEnd, MathHelper.Clamp(ratio, 0f, 1f));
            //外缘的金色高亮
            SHPCRenderer.DrawArcStroke(sb, px, center,
                SHPCTheme.ButtonOuterR - 1.5f, aStart, fillEnd,
                2.2f, SHPCTheme.Accent * (0.85f * globalAlpha));
            //内部柔光，强化蓄力的能量感
            SHPCRenderer.DrawArcStroke(sb, px, center,
                SHPCTheme.ButtonOuterR - 4f, aStart, fillEnd,
                3.5f, SHPCTheme.Accent * (0.32f * globalAlpha));
            //蓄满后增加一圈呼吸闪烁
            if (ratio >= 0.999f) {
                float pulse = (MathF.Sin(Main.GameUpdateCount * 0.32f) + 1f) * 0.5f;
                SHPCRenderer.DrawArcStroke(sb, px, center,
                    SHPCTheme.ButtonOuterR + 1.2f, aStart, aEnd,
                    1.4f, SHPCTheme.Accent * (0.5f * pulse * globalAlpha));
            }
        }

        /// <summary>
        /// Toggle 类已激活的金色环带，强化"开启中"语义
        /// </summary>
        private static void DrawToggleActiveOverlay(SpriteBatch sb, Texture2D px,
            Vector2 center, float aStart, float aEnd, float time, float globalAlpha) {
            float pulse = 0.65f + 0.35f * (MathF.Sin(time * 4.2f) + 1f) * 0.5f;
            SHPCRenderer.DrawArcStroke(sb, px, center,
                SHPCTheme.ButtonOuterR - 2.4f, aStart, aEnd,
                2.6f, SHPCTheme.Accent * (pulse * 0.7f * globalAlpha));
        }

        /// <summary>
        /// 绘制扇区中线上的物品图标，自适应缩放到 32x32 以下
        /// </summary>
        private static void DrawItemIcon(SpriteBatch sb, CyberwareSkillRadialSector sec,
            Vector2 center, float midA, float hoverAmt, bool enabled, float globalAlpha) {
            int type = sec.Skill.IconItemType;
            if (type <= 0) {
                return;
            }
            //安全：访问 TextureAssets 之前确认主线程已加载完成
            Texture2D tex = TextureAssets.Item[type]?.Value;
            if (tex == null) {
                return;
            }
            //把图标放在按钮中点半径，给图标一点点跳出动效
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

        /// <summary>
        /// 扇区右上角的状态文字（冷却剩余、RAM 消耗、ON 等）
        /// </summary>
        private static void DrawStatusText(SpriteBatch sb, CyberwareSkillRadialSector sec,
            Vector2 center, float midA, float globalAlpha) {
            string text = sec.Skill.StatusText;
            if (string.IsNullOrEmpty(text)) {
                return;
            }
            Vector2 dir = new(MathF.Cos(midA), MathF.Sin(midA));
            //略外侧 + 偏切向，避免与图标重叠
            Vector2 tangent = new(-dir.Y, dir.X);
            Vector2 pos = center + dir * (SHPCTheme.ButtonOuterR - 8f) + tangent * 10f;
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 size = font.MeasureString(text) * 0.55f;
            Utils.DrawBorderString(sb, text, pos - size * 0.5f,
                SHPCTheme.CyanHi * globalAlpha, 0.55f);
        }

        /// <summary>
        /// 悬停扇区的信息面板：复用 SHPCRenderer.DrawInfoPanel，锚点设为扇区外侧而非鼠标
        /// </summary>
        private static void DrawInfoPanelForHovered(SpriteBatch sb, Texture2D px,
            CyberwareSkillRadialController ctrl, Vector2 center, float globalAlpha) {
            int idx = ctrl.HoveredIndex;
            if (idx < 0 || idx >= ctrl.Sectors.Count) {
                return;
            }
            CyberwareSkillRadialSector sec = ctrl.Sectors[idx];
            ctrl.GetSectorAngles(idx, out float aStart, out float aEnd);
            float midA = (aStart + aEnd) * 0.5f;
            Vector2 dir = new(MathF.Cos(midA), MathF.Sin(midA));
            //锚点放在扇区外侧约 24 像素处，让面板"贴着扇区"展开
            Vector2 anchor = center + dir * (SHPCTheme.ButtonOuterR + 24f);

            string subtitle = BuildSubtitle(sec.Skill);
            SHPCRenderer.DrawInfoPanel(sb, px, anchor, globalAlpha, globalAlpha,
                title: sec.Skill.DisplayName,
                subtitle: subtitle,
                description: sec.Skill.Description,
                statusText: sec.Skill.StatusText);
        }

        /// <summary>
        /// 根据技能类型构造副标题，例如"按住蓄力" / "切换开关" / "立即触发" / "条件不满足"
        /// </summary>
        private static string BuildSubtitle(CyberwareSkillBase skill) {
            if (!skill.IsReady) {
                return HintNotReady.Value;
            }
            return skill.Kind switch {
                CyberwareSkillKind.Charge => HintHoldToCharge.Value,
                CyberwareSkillKind.Toggle => HintToggle.Value,
                CyberwareSkillKind.Instant => HintInstant.Value,
                _ => string.Empty,
            };
        }

        /// <summary>
        /// 雷达外圈的整体描边装饰，仅在多扇区时绘制
        /// </summary>
        private static void DrawOuterRing(SpriteBatch sb, Texture2D px,
            Vector2 center, int sectorCount, float globalAlpha, float time) {
            if (sectorCount <= 1) {
                return;
            }
            //最外圈细线，配旋转刻度
            float ringR = SHPCTheme.ButtonOuterR + 5f;
            SHPCRenderer.DrawArcStroke(sb, px, center, ringR,
                0f, MathHelper.TwoPi, 1.2f,
                SHPCTheme.Border * (0.6f * globalAlpha));
            //旋转刻度
            const int markCount = 8;
            float markSpan = 0.18f;
            float markGap = MathHelper.TwoPi / markCount;
            float markRot = time * 0.25f;
            for (int i = 0; i < markCount; i++) {
                float a0 = markRot + i * markGap - markSpan * 0.5f;
                SHPCRenderer.DrawArcStroke(sb, px, center, ringR + 4f,
                    a0, a0 + markSpan, 1.0f,
                    SHPCTheme.BorderHi * (0.5f * globalAlpha));
            }
        }
    }
}
