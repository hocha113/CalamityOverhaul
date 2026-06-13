using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.SkillWheel
{
    /// <summary>
    /// 技能轮盘可视层，状态读 <see cref="HalibutWheelController"/>
    /// 全屏暗化 + 弧扇区 + 中心核 + 气泡
    /// </summary>
    internal class HalibutSkillWheel : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.HalibutText";
        public static HalibutSkillWheel Instance => UIHandleLoader.GetUIHandleOfType<HalibutSkillWheel>();

        public static LocalizedText ReleaseHint { get; private set; }
        public static LocalizedText CancelHint { get; private set; }
        public static LocalizedText NoSelection { get; private set; }

        private readonly HalibutUIParticlePool particles = new(90);
        private int bubbleTimer;

        public override void SetStaticDefaults() {
            ReleaseHint = this.GetLocalization(nameof(ReleaseHint), () => "松开 {0} 选定技能");
            CancelHint = this.GetLocalization(nameof(CancelHint), () => "右键取消");
            NoSelection = this.GetLocalization(nameof(NoSelection), () => "未选定技能");
        }

        public override bool Active {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active) {
                    return false;
                }
                HalibutWheelController ctrl = HalibutWheelController.LocalInstance;
                if (ctrl == null) {
                    return false;
                }
                return ctrl.IsOpen || ctrl.OpenProgress > 0.01f;
            }
        }

        public override void Update() {
            HalibutWheelController ctrl = HalibutWheelController.LocalInstance;
            if (ctrl == null) {
                return;
            }
            particles.Update();
            //开盘期间持续冒出上浮气泡
            if (ctrl.IsOpen && ctrl.OpenProgress > 0.6f) {
                bubbleTimer++;
                if (bubbleTimer % 9 == 0) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    float r = Main.rand.NextFloat(HalibutTheme.WheelInnerR, HalibutTheme.WheelOuterR + 40f);
                    particles.SpawnBubble(ctrl.ScreenAnchor + HalibutRenderer.AngleDir(ang) * r, 0.8f);
                }
            }
        }

        public override void Draw(SpriteBatch sb) {
            HalibutWheelController ctrl = HalibutWheelController.LocalInstance;
            if (ctrl == null) {
                return;
            }
            float a = MathHelper.Clamp(ctrl.OpenProgress, 0f, 1f);
            if (a < 0.01f) {
                return;
            }

            Vector2 center = new(HalibutTheme.UIScreenW * 0.5f, HalibutTheme.UIScreenH * HalibutTheme.WheelAnchorYRatio);
            ctrl.SetScreenAnchor(center);
            float time = ctrl.Time;
            float ease = VaultUtils.EaseOutBack(a);

            //1 全屏暗化
            DrawWaterVeil(sb, center, a);

            //2 底盘与装饰环
            DrawBackplate(sb, center, a, ease, time);

            //3 扇区
            int count = ctrl.Sectors.Count;
            for (int i = 0; i < count; i++) {
                ctrl.GetSectorAngles(i, out float aStart, out float aEnd);
                DrawSector(sb, center, ctrl.Sectors[i], aStart, aEnd, ease, a, time);
            }

            //4 中心核
            DrawCenterCore(sb, center, ctrl, a, time);

            //5 粒子
            particles.Draw(sb, a);

            //6 按键提示
            DrawHints(sb, center, a);
        }

        private static void DrawWaterVeil(SpriteBatch sb, Vector2 center, float a) {
            Texture2D px = HalibutRenderer.Pixel;
            //全屏压暗
            sb.Draw(px, new Rectangle(0, 0, (int)HalibutTheme.UIScreenW + 1, (int)HalibutTheme.UIScreenH + 1),
                new Rectangle(0, 0, 1, 1), HalibutTheme.Void * (0.62f * a));
            //中心提亮晕
            HalibutRenderer.DrawSoftGlow(sb, center, HalibutTheme.WheelOuterR * 2.1f,
                HalibutTheme.Mid * (0.85f * a));
        }

        private static void DrawBackplate(SpriteBatch sb, Vector2 center, float a, float ease, float time) {
            //底盘暗圆
            HalibutRenderer.DrawDisc(sb, center, HalibutTheme.WheelOuterR * ease + 14f, 22f,
                HalibutTheme.Deep * (0.78f * a));
            //外圈缓转刻度环
            float markRot = time * 0.3f;
            for (int i = 0; i < 5; i++) {
                float a0 = markRot + i * MathHelper.TwoPi / 5f;
                HalibutRenderer.DrawArcStroke(sb, center, (HalibutTheme.WheelOuterR + 9f) * ease,
                    a0, a0 + 0.5f, 1.2f, HalibutTheme.Teal * (0.8f * a));
            }
            //内圈呼吸环
            float breath = HalibutTheme.Breath(time, 0.3f, 2.2f);
            HalibutRenderer.DrawRing(sb, center, (HalibutTheme.WheelInnerR - 7f) * ease, 1.3f,
                HalibutTheme.Glow * ((0.32f + breath * 0.2f) * a));
        }

        private static void DrawSector(SpriteBatch sb, Vector2 center, HalibutWheelSector sec,
            float aStart, float aEnd, float ease, float a, float time) {
            //入场：半径从内向外滑出
            float rIn = MathHelper.Lerp(HalibutTheme.WheelDeadZoneR, HalibutTheme.WheelInnerR, ease);
            float rOut = MathHelper.Lerp(HalibutTheme.WheelDeadZoneR + 8f, HalibutTheme.WheelOuterR, ease);
            float hover = sec.HoverAmount;
            float selected = sec.SelectedAmount;

            //底板
            Color bg = Color.Lerp(HalibutTheme.PanelBg, HalibutTheme.Mid, hover * 0.55f);
            HalibutRenderer.DrawArc(sb, center, rIn + 1f, rOut - 1f, aStart, aEnd, bg * (0.9f * a));

            //冷却剩余：从外向内的暗化覆盖 + 前沿亮线
            float cdRatio = MathHelper.Clamp(sec.Skill.CooldownRatio, 0f, 1f);
            if (cdRatio > 0.01f) {
                float cdEnd = MathHelper.Lerp(aStart, aEnd, cdRatio);
                HalibutRenderer.DrawArc(sb, center, rIn + 1f, rOut - 1f, aStart, cdEnd,
                    HalibutTheme.Void * (0.55f * a));
                Vector2 edgeDir = HalibutRenderer.AngleDir(cdEnd);
                HalibutRenderer.DrawLine(sb, center + edgeDir * (rIn + 1f), center + edgeDir * (rOut - 1f),
                    1.2f, HalibutTheme.Glow * (0.5f * a));
            }

            //悬停水光
            if (hover > 0.01f) {
                HalibutRenderer.DrawArc(sb, center, rIn + 1f, rOut - 1f, aStart, aEnd,
                    HalibutTheme.GlowHi * (hover * 0.16f * a));
                //扫光带
                float scanT = (time * 0.7f) % 1f;
                float scanA = MathHelper.Lerp(aStart, aEnd, scanT);
                float scanW = (aEnd - aStart) * 0.16f;
                HalibutRenderer.DrawArc(sb, center, rIn + 2f, rOut - 2f,
                    MathF.Max(aStart, scanA - scanW * 0.5f), MathF.Min(aEnd, scanA + scanW * 0.5f),
                    HalibutTheme.Caustic * (hover * 0.18f * a));
            }

            //选中暖金外带
            if (selected > 0.01f) {
                HalibutRenderer.DrawArc(sb, center, rOut - 5f, rOut - 1f, aStart, aEnd,
                    HalibutTheme.Accent * (selected * 0.8f * a));
            }

            //描边与径向封口
            Color border = Color.Lerp(HalibutTheme.Teal, HalibutTheme.GlowHi, MathF.Max(hover, selected));
            HalibutRenderer.DrawArcStroke(sb, center, rOut - 0.5f, aStart, aEnd, 1.4f, border * a);
            HalibutRenderer.DrawArcStroke(sb, center, rIn + 0.5f, aStart, aEnd, 1.1f, border * (0.6f * a));
            Vector2 dirS = HalibutRenderer.AngleDir(aStart);
            Vector2 dirE = HalibutRenderer.AngleDir(aEnd);
            HalibutRenderer.DrawLine(sb, center + dirS * (rIn + 1f), center + dirS * (rOut - 1f), 1.2f, border * (0.6f * a));
            HalibutRenderer.DrawLine(sb, center + dirE * (rIn + 1f), center + dirE * (rOut - 1f), 1.2f, border * (0.6f * a));

            //图标
            Texture2D icon = sec.Skill.Icon;
            if (icon != null) {
                float midA = (aStart + aEnd) * 0.5f;
                Vector2 iconPos = center + HalibutRenderer.AngleDir(midA) * (HalibutTheme.WheelIconR * ease);
                float iconScale = 34f / MathF.Max(icon.Width, icon.Height);
                iconScale *= 1f + hover * 0.18f + selected * 0.06f;
                Color iconColor = Color.White * a;
                if (cdRatio > 0.01f) {
                    iconColor = Color.Lerp(iconColor, HalibutTheme.Disabled * a, 0.55f);
                }
                if (hover > 0.01f) {
                    sb.Draw(icon, iconPos, null, HalibutTheme.Glow with { A = 0 } * (hover * 0.5f * a),
                        0f, icon.Size() * 0.5f, iconScale * 1.25f, SpriteEffects.None, 0f);
                }
                sb.Draw(icon, iconPos, null, iconColor, 0f, icon.Size() * 0.5f, iconScale, SpriteEffects.None, 0f);

                //冷却剩余秒数
                if (cdRatio > 0.01f) {
                    int seconds = (int)MathF.Ceiling(sec.Skill.Cooldown / 60f);
                    HalibutRenderer.DrawGlowTextCentered(sb, seconds.ToString(),
                        iconPos + new Vector2(0f, 16f), HalibutTheme.Text * a,
                        HalibutTheme.Glow * (0.4f * a), 0.7f);
                }
            }
        }

        private static void DrawCenterCore(SpriteBatch sb, Vector2 center, HalibutWheelController ctrl,
            float a, float time) {
            var save = player.GetModPlayer<HalibutSave>();
            //悬停的技能优先显示，否则显示当前选定
            FishSkill focus = null;
            if (ctrl.HoveredIndex >= 0 && ctrl.HoveredIndex < ctrl.Sectors.Count) {
                focus = ctrl.Sectors[ctrl.HoveredIndex].Skill;
            }
            FishSkill display = focus ?? save.FishSkill;

            float breath = HalibutTheme.Breath(time, 1.7f);
            HalibutRenderer.DrawDisc(sb, center, 25f, 5f, HalibutTheme.Deep * (0.95f * a));
            HalibutRenderer.DrawRing(sb, center, 27f, 1.5f,
                HalibutTheme.Glow * ((0.5f + breath * 0.3f) * a));

            if (display?.Icon != null) {
                float scale = 36f / MathF.Max(display.Icon.Width, display.Icon.Height);
                sb.Draw(display.Icon, center, null, Color.White * a, 0f,
                    display.Icon.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }

            //技能名（中心下方）
            string name = display?.DisplayName?.Value ?? NoSelection.Value;
            Color nameColor = focus != null ? HalibutTheme.GlowHi : HalibutTheme.Text;
            HalibutRenderer.DrawGlowTextCentered(sb, name, center + new Vector2(0f, 44f),
                nameColor * a, HalibutTheme.Glow * (0.35f * a), 0.85f);
        }

        private void DrawHints(SpriteBatch sb, Vector2 center, float a) {
            string keyName = CWRKeySystem.Halibut_SkillWheel.ToTooltipString(CWRKeySystem.Notbound.Value);
            string hint = string.Format(ReleaseHint.Value, keyName) + "  ·  " + CancelHint.Value;
            HalibutRenderer.DrawGlowTextCentered(sb, hint,
                center + new Vector2(0f, HalibutTheme.WheelOuterR + 38f),
                HalibutTheme.TextDim * (0.9f * a), HalibutTheme.Deep * (0.5f * a), 0.74f);
        }
    }
}
