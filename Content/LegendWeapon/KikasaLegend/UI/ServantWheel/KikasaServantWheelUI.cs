using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.UIs.RadialWheels;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI.ServantWheel
{
    /// <summary>
    /// 鬼伞转盘可视层，状态读 <see cref="KikasaServantWheelController"/>。
    /// 语汇是血湖的：涟漪弧带作扇区、沉影笔作徽章、伞章作中心，
    /// 收起的影下沉变暗、在外的影负形留旋涡，与湖心景同一副沉影笔
    /// </summary>
    internal class KikasaServantWheelUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.KikasaText";
        public static KikasaServantWheelUI Instance => UIHandleLoader.GetUIHandleOfType<KikasaServantWheelUI>();

        public static LocalizedText WheelEmptyTitle { get; private set; }
        public static LocalizedText WheelEmptyDesc { get; private set; }
        public static LocalizedText WheelEmptyStatus { get; private set; }
        public static LocalizedText WheelSeatFormat { get; private set; }
        public static LocalizedText WheelStatusRecall { get; private set; }
        public static LocalizedText WheelStatusSummon { get; private set; }
        public static LocalizedText WheelStatusAwaitOrder { get; private set; }
        public static LocalizedText WheelCenterTitle { get; private set; }
        public static LocalizedText WheelCenterDesc { get; private set; }
        public static LocalizedText WheelCenterCountFormat { get; private set; }
        public static LocalizedText WheelCenterRecallAll { get; private set; }
        public static LocalizedText WheelCenterSummonAll { get; private set; }
        public static LocalizedText WheelCenterEmpty { get; private set; }
        public static LocalizedText WheelDamageFormat { get; private set; }
        public static LocalizedText WheelHint { get; private set; }

        public override void SetStaticDefaults() {
            WheelEmptyTitle = this.GetLocalization(nameof(WheelEmptyTitle), () => "Vacant Seat");
            WheelEmptyDesc = this.GetLocalization(nameof(WheelEmptyDesc),
                () => "Drown a registered foe and its sunken shade will take this seat");
            WheelEmptyStatus = this.GetLocalization(nameof(WheelEmptyStatus), () => "Nothing seated here");
            WheelSeatFormat = this.GetLocalization(nameof(WheelSeatFormat), () => "Seat {0}");
            WheelStatusRecall = this.GetLocalization(nameof(WheelStatusRecall), () => "[Release] Recall this shade");
            WheelStatusSummon = this.GetLocalization(nameof(WheelStatusSummon), () => "[Release] Call it out of the water");
            WheelStatusAwaitOrder = this.GetLocalization(nameof(WheelStatusAwaitOrder),
                () => "[Release] Accept the order — it surfaces once the lake rises");
            WheelCenterTitle = this.GetLocalization(nameof(WheelCenterTitle), () => "Umbrella Decree");
            WheelCenterDesc = this.GetLocalization(nameof(WheelCenterDesc),
                () => "One decree for all seats: recall every shade afield, or call out every shade held back");
            WheelCenterCountFormat = this.GetLocalization(nameof(WheelCenterCountFormat), () => "{0}/{1} afield");
            WheelCenterRecallAll = this.GetLocalization(nameof(WheelCenterRecallAll), () => "[Release] Recall all");
            WheelCenterSummonAll = this.GetLocalization(nameof(WheelCenterSummonAll), () => "[Release] Call out all");
            WheelCenterEmpty = this.GetLocalization(nameof(WheelCenterEmpty), () => "No shades seated in the lake");
            WheelDamageFormat = this.GetLocalization(nameof(WheelDamageFormat), () => "Output per shade afield: {0}%");
            WheelHint = this.GetLocalization(nameof(WheelHint), () => "[{0}] Hold \u00b7 Release to commit \u00b7 RMB cancel");
        }

        //存活 + OpenProgress>0 时显示
        public override bool Active {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead) {
                    return false;
                }
                KikasaServantWheelController ctrl = KikasaServantWheelController.LocalInstance;
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
            KikasaServantWheelController ctrl = KikasaServantWheelController.LocalInstance;
            Texture2D px = VaultAsset.placeholder2.Value;
            if (ctrl == null || px == null) {
                return;
            }
            float a = MathHelper.Clamp(ctrl.OpenProgress, 0f, 1f);
            if (a < 0.01f) {
                return;
            }

            Player player = Main.LocalPlayer;
            KikasaServantPlayer servant = player.GetModPlayer<KikasaServantPlayer>();
            KikasaDomainPlayer domain = player.GetModPlayer<KikasaDomainPlayer>();
            float rain = MathHelper.Clamp(domain.RainBlend, 0f, 1f);
            Vector2 center = ctrl.ScreenAnchor;
            float time = ctrl.Time;

            //多盘并存时全屏压暗只由归属者画一次
            if (RadialWheelHub.OwnsBackdrop(ctrl)) {
                int w = (int)MathF.Ceiling(RadialWheelHub.UIScreenW);
                int h = (int)MathF.Ceiling(RadialWheelHub.UIScreenH);
                sb.Draw(px, new Rectangle(0, 0, w, h), KikasaHudTheme.Void(rain) * (0.44f * a));
            }

            DrawRippleBackdrop(sb, center, a, rain, time);

            int count = ctrl.Sectors.Count;
            bool lakeReady = ctrl.LakeReadyNow;
            for (int i = 0; i < count; i++) {
                ctrl.GetSectorAngles(i, out float aStart, out float aEnd);
                DrawSeatSector(sb, ctrl, servant, ctrl.Sectors[i], center,
                    aStart, aEnd, a, rain, time, lakeReady);
            }

            DrawCenterSeal(sb, ctrl, servant, center, a, rain, time);
            DrawInfoPanelForHovered(sb, px, ctrl, servant, center, a, rain, lakeReady);

            //按键提示只由最底那个盘画
            if (RadialWheelHub.OwnsHint(ctrl)) {
                DrawWheelHint(sb, center, a, rain);
            }
        }

        //==================== 盘体 ====================

        /// <summary>外缘两圈慢涟漪 + 底部沉影：盘是浮在水面上的一圈令阵</summary>
        private static void DrawRippleBackdrop(SpriteBatch sb, Vector2 center,
            float a, float rain, float time) {
            float breath = KikasaHudTheme.Breath(time, 0.7f, 1.6f);
            DrawWaterArc(sb, center, KikasaServantWheelController.WheelOuterR + 14f + breath * 2f,
                0f, MathHelper.TwoPi, 1.2f, KikasaHudTheme.Accent(rain) * (0.30f * a), time, 2.2f);
            DrawWaterArc(sb, center, KikasaServantWheelController.WheelOuterR + 22f + breath * 3f,
                0f, MathHelper.TwoPi, 1.0f, KikasaHudTheme.Accent(rain) * (0.16f * a), time, 3.0f);

            //盘下衬一枚极淡的软光，把黑幕上的令阵托出来
            KikasaVaultRenderer.BeginAdditive(sb);
            KikasaVaultRenderer.DrawGlowDot(sb, center,
                KikasaServantWheelController.WheelOuterR + 26f,
                KikasaHudTheme.Deep(rain) * (0.30f * a));
            KikasaVaultRenderer.RestoreUIBatch(sb);
        }

        /// <summary>
        /// 单席扇区：内外涟漪弧夹出环带，出战令亮中脉，徽章用沉影笔
        /// 在外=负形旋涡、候湖=干泥痕、收起=下沉变暗
        /// </summary>
        private void DrawSeatSector(SpriteBatch sb, KikasaServantWheelController ctrl,
            KikasaServantPlayer servant, KikasaWheelSector sec, Vector2 center,
            float aStart, float aEnd, float a, float rain, float time, bool lakeReady) {

            int key = servant.SlotKeyAt(sec.SeatIndex);
            bool held = servant.SlotHeldAt(sec.SeatIndex);
            bool present = key != 0 && servant.FindServantOf(key) != null;
            float hover = sec.HoverAmount;
            float order = sec.OrderAmount;
            float midA = (aStart + aEnd) * 0.5f;
            float midR = (KikasaServantWheelController.WheelInnerR
                + KikasaServantWheelController.WheelOuterR) * 0.5f;

            //环带内外缘（悬停亮起）
            Color band = KikasaHudTheme.Accent(rain);
            DrawWaterArc(sb, center, KikasaServantWheelController.WheelInnerR, aStart, aEnd,
                1.2f, band * ((0.30f + hover * 0.25f) * a), time, 1.4f);
            DrawWaterArc(sb, center, KikasaServantWheelController.WheelOuterR, aStart, aEnd,
                1.5f, band * ((0.38f + hover * 0.30f) * a), time, 1.8f);

            //出战令水脉：贴外缘内侧的一道亮弧，跟 SlotHeld 真值走
            if (order > 0.01f) {
                DrawWaterArc(sb, center, KikasaServantWheelController.WheelOuterR - 7f,
                    aStart + 0.06f, aEnd - 0.06f, 1.8f,
                    KikasaHudTheme.Glow(rain) * (0.45f * order * a), time, 1.2f);
            }

            Vector2 seatPos = center + AngleDir(midA) * midR;
            //悬停时徽章微微浮起，收起时下沉
            seatPos.Y += held ? 3f : -hover * 3f;

            if (key == 0) {
                DrawEmptySocket(sb, seatPos, a, rain, time, hover);
                return;
            }

            //徽章：沉影笔按状态换形，submerge 干湖⇄水下，absent=在外负形
            float submerge = lakeReady ? 1f : 0f;
            float effigyA = (held ? 0.62f : 0.95f) * a;
            KikasaVaultRenderer.DrawEffigyByKey(sb, key, seatPos, 40f, effigyA,
                submerge, tamed: true, absent: present, rain,
                0.2f + hover * 0.4f, KikasaHudTheme.Accent(rain));

            //在外：席上慢旋涡（沿用「鬼奴在外」的既定语言）
            if (present) {
                KikasaVaultRenderer.BeginAdditive(sb);
                for (int ring = 0; ring < 2; ring++) {
                    float rp = (time * 0.35f + ring * 0.5f) % 1f;
                    float r = MathHelper.Lerp(15f, 4f, rp);
                    KikasaVaultRenderer.DrawRing(sb, seatPos, r, r * 0.4f,
                        KikasaHudTheme.Glow(rain) * (0.22f * (1f - rp) * a));
                }
                KikasaVaultRenderer.RestoreUIBatch(sb);
            }

            //亲和烬点：徽章右下一粒身份色
            KikasaAffinity affinity = servant.SlotAffinity(sec.SeatIndex);
            if (affinity != KikasaAffinity.None) {
                float breath = KikasaHudTheme.Breath(time, sec.SeatIndex * 3.1f, 2.2f);
                KikasaVaultRenderer.DrawGlowDot(sb, seatPos + new Vector2(16f, 14f), 5f,
                    KikasaEffigyBoard.AffinityColor(affinity) * ((0.45f + breath * 0.3f) * a));
            }

            //名字沿中线放到外缘之外，空间最宽
            string name = KikasaServantPlayer.KeyDisplayName(key);
            if (!string.IsNullOrEmpty(name)) {
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                float scale = 0.82f + hover * 0.06f;
                Vector2 size = font.MeasureString(name) * scale;
                Vector2 pos = center + AngleDir(midA)
                    * (KikasaServantWheelController.WheelOuterR + 22f + size.Length() * 0.18f);
                Color col = held ? KikasaHudTheme.TextDim(rain) : KikasaHudTheme.Text(rain);
                Utils.DrawBorderString(sb, name, pos - size * 0.5f, col * a, scale);
            }
        }

        /// <summary>空席：一圈断续的虚环，等一位沉影落座</summary>
        private static void DrawEmptySocket(SpriteBatch sb, Vector2 pos,
            float a, float rain, float time, float hover) {
            Color dim = KikasaHudTheme.TextDim(rain) * ((0.32f + hover * 0.25f) * a);
            const int dashes = 9;
            for (int d = 0; d < dashes; d++) {
                float a0 = MathHelper.TwoPi * d / dashes + time * 0.3f;
                float a1 = a0 + MathHelper.TwoPi / dashes * 0.55f;
                DrawWaterArc(sb, pos, 15f, a0, a1, 1.1f, dim, time, 0.6f);
            }
            KikasaVaultRenderer.DrawGlowDot(sb, pos, 8f,
                KikasaHudTheme.Deep(rain) * (0.5f * a));
        }

        /// <summary>中心伞章：全席齐令的按钮，出战席越多环越亮</summary>
        private static void DrawCenterSeal(SpriteBatch sb, KikasaServantWheelController ctrl,
            KikasaServantPlayer servant, Vector2 center, float a, float rain, float time) {
            float hover = ctrl.CenterHoverAmount;
            int filled = servant.FilledSlotCount;
            int active = servant.ActiveSlotCount;

            //章底暗盘
            KikasaVaultRenderer.DrawGlowDot(sb, center, KikasaServantWheelController.DeadZoneRadius + 6f,
                KikasaHudTheme.Void(rain) * (0.85f * a));

            //死区环：有影出战时随水呼吸
            float pulse = filled > 0 && active > 0
                ? 0.55f + 0.35f * KikasaHudTheme.Breath(time, 1.3f, 2.4f) : 0.4f;
            DrawWaterArc(sb, center, KikasaServantWheelController.DeadZoneRadius - 2f,
                0f, MathHelper.TwoPi, 1.4f,
                KikasaHudTheme.Accent(rain) * ((pulse + hover * 0.3f) * a), time, 1.0f);

            //伞章本体：悬停微涨
            Color bone = KikasaHudTheme.TextDim(rain);
            Color canopy = Color.Lerp(KikasaHudTheme.Accent(rain), KikasaHudTheme.Glow(rain), hover * 0.6f);
            KikasaVaultRenderer.DrawSeal(sb, center - new Vector2(0f, 2f),
                17f + hover * 1.5f, a * (filled > 0 ? 1f : 0.55f), time, 1f,
                bone, canopy, KikasaHudTheme.Glow(rain));
        }

        //==================== 悬停信息板 ====================

        /// <summary>中心与扇区共用一块信息板：标题/亲和/说明/松键动作，按测量宽高自适应</summary>
        private void DrawInfoPanelForHovered(SpriteBatch sb, Texture2D px,
            KikasaServantWheelController ctrl, KikasaServantPlayer servant,
            Vector2 center, float a, float rain, bool lakeReady) {

            string title;
            string subtitle;
            string desc;
            string extra = string.Empty;
            string status;

            if (ctrl.CenterHovered) {
                int filled = servant.FilledSlotCount;
                int active = servant.ActiveSlotCount;
                title = WheelCenterTitle.Value;
                subtitle = string.Format(WheelCenterCountFormat.Value, active, filled);
                desc = WheelCenterDesc.Value;
                status = filled <= 0 ? WheelCenterEmpty.Value
                    : active > 0 ? WheelCenterRecallAll.Value : WheelCenterSummonAll.Value;
            }
            else {
                int idx = ctrl.HoveredIndex;
                if (idx < 0 || idx >= ctrl.Sectors.Count) {
                    return;
                }
                int seat = ctrl.Sectors[idx].SeatIndex;
                int key = servant.SlotKeyAt(seat);
                if (key == 0) {
                    title = WheelEmptyTitle.Value;
                    subtitle = string.Format(WheelSeatFormat.Value, seat + 1);
                    desc = WheelEmptyDesc.Value;
                    status = WheelEmptyStatus.Value;
                }
                else {
                    bool held = servant.SlotHeldAt(seat);
                    bool present = servant.FindServantOf(key) != null;
                    string stateTag = held ? KikasaUIText.StateHeld.Value
                        : present ? KikasaUIText.StateOut.Value : KikasaUIText.StateAwait.Value;
                    string affinityName = KikasaUIText.AffinityName(servant.SlotAffinity(seat), key);
                    title = KikasaServantPlayer.KeyDisplayName(key);
                    subtitle = string.IsNullOrEmpty(affinityName)
                        ? stateTag : $"{affinityName} \u00b7 {stateTag}";
                    desc = string.Format(WheelDamageFormat.Value,
                        (int)MathF.Round(KikasaEffigyBoard.ServantDamageScale(Main.LocalPlayer) * 100f));
                    if (!lakeReady) {
                        extra = KikasaUIText.LakeNotReadyLine.Value;
                    }
                    status = held
                        ? (lakeReady ? WheelStatusSummon.Value : WheelStatusAwaitOrder.Value)
                        : WheelStatusRecall.Value;
                }
            }

            DrawInfoPanel(sb, px, center, a, rain, title, subtitle, desc, extra, status);
        }

        /// <summary>信息板本体：暗水玻璃底 + 水线顶缘，行高按测量走，字号跟全域字体规范</summary>
        private static void DrawInfoPanel(SpriteBatch sb, Texture2D px, Vector2 center,
            float a, float rain, string title, string subtitle, string desc,
            string extra, string status) {

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            const float titleScale = 1.0f;
            const float bodyScale = 0.85f;
            const float pad = 14f;
            const float lineGap = 6f;

            //逐行测量，面板尺寸跟内容走
            Vector2 titleSize = font.MeasureString(title) * titleScale;
            Vector2 subSize = string.IsNullOrEmpty(subtitle) ? Vector2.Zero
                : font.MeasureString(subtitle) * bodyScale;
            Vector2 descSize = string.IsNullOrEmpty(desc) ? Vector2.Zero
                : font.MeasureString(desc) * bodyScale;
            Vector2 extraSize = string.IsNullOrEmpty(extra) ? Vector2.Zero
                : font.MeasureString(extra) * bodyScale;
            Vector2 statusSize = string.IsNullOrEmpty(status) ? Vector2.Zero
                : font.MeasureString(status) * bodyScale;

            float w = MathF.Max(MathF.Max(titleSize.X, subSize.X),
                MathF.Max(MathF.Max(descSize.X, extraSize.X), statusSize.X)) + pad * 2f;
            float h = pad + titleSize.Y
                + (subSize.Y > 0f ? subSize.Y + lineGap : 0f)
                + (descSize.Y > 0f ? descSize.Y + lineGap : 0f)
                + (extraSize.Y > 0f ? extraSize.Y + lineGap : 0f)
                + (statusSize.Y > 0f ? statusSize.Y + lineGap + 4f : 0f)
                + pad * 0.6f;

            //挑空间多的一侧放，四边钳制
            float radialEdge = KikasaServantWheelController.WheelOuterR + 40f;
            float spaceRight = RadialWheelHub.UIScreenW - (center.X + radialEdge) - w;
            float spaceLeft = center.X - radialEdge - w;
            bool placeLeft = spaceLeft > spaceRight && spaceLeft > 0f;
            float x = placeLeft ? center.X - radialEdge - w : center.X + radialEdge;
            float y = center.Y - h * 0.5f;
            x = MathHelper.Clamp(x, 6f, MathF.Max(6f, RadialWheelHub.UIScreenW - w - 6f));
            y = MathHelper.Clamp(y, 6f, MathF.Max(6f, RadialWheelHub.UIScreenH - h - 6f));
            Rectangle rect = new((int)x, (int)y, (int)w, (int)h);

            //底：暗水玻璃 + 贴身投影；顶缘一道水线
            Rectangle shadow = rect;
            shadow.Offset(3, 4);
            sb.Draw(px, shadow, Color.Black * (0.4f * a));
            sb.Draw(px, rect, KikasaHudTheme.Void(rain) * (0.93f * a));
            KikasaVaultRenderer.DrawLine(sb, new Vector2(rect.Left + 2, rect.Top),
                new Vector2(rect.Right - 2, rect.Top), 1.4f,
                KikasaHudTheme.Glow(rain) * (0.5f * a));
            KikasaVaultRenderer.DrawLine(sb, new Vector2(rect.Left, rect.Bottom),
                new Vector2(rect.Right, rect.Bottom), 1.0f,
                KikasaHudTheme.Accent(rain) * (0.35f * a));

            float ty = rect.Y + pad;
            Utils.DrawBorderString(sb, title, new Vector2(rect.X + pad, ty),
                KikasaHudTheme.Text(rain) * a, titleScale);
            ty += titleSize.Y + (subSize.Y > 0f ? lineGap : 0f);
            if (subSize.Y > 0f) {
                Utils.DrawBorderString(sb, subtitle, new Vector2(rect.X + pad, ty),
                    KikasaHudTheme.Glow(rain) * (0.9f * a), bodyScale);
                ty += subSize.Y + lineGap;
            }
            if (descSize.Y > 0f) {
                Utils.DrawBorderString(sb, desc, new Vector2(rect.X + pad, ty),
                    KikasaHudTheme.TextDim(rain) * a, bodyScale);
                ty += descSize.Y + lineGap;
            }
            if (extraSize.Y > 0f) {
                Utils.DrawBorderString(sb, extra, new Vector2(rect.X + pad, ty),
                    KikasaHudTheme.Accent(rain) * a, bodyScale);
                ty += extraSize.Y + lineGap;
            }
            if (statusSize.Y > 0f) {
                Utils.DrawBorderString(sb, status, new Vector2(rect.X + pad, ty + 4f),
                    KikasaHudTheme.Text(rain) * a, bodyScale);
            }
        }

        /// <summary>转盘底部按键提示</summary>
        private static void DrawWheelHint(SpriteBatch sb, Vector2 center, float a, float rain) {
            string keyText = CWRKeySystem.GetKeybindText(CWRKeySystem.RadialWheel_Key,
                CWRKeySystem.Notbound.Value);
            string text = string.Format(WheelHint.Value, keyText);
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            const float scale = 0.78f;
            Vector2 size = font.MeasureString(text) * scale;
            Vector2 pos = center + new Vector2(-size.X * 0.5f,
                KikasaServantWheelController.WheelOuterR + 44f);
            Utils.DrawBorderString(sb, text, pos, KikasaHudTheme.TextDim(rain) * (0.9f * a), scale);
        }

        //==================== 水弧笔 ====================

        private static Vector2 AngleDir(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

        /// <summary>
        /// 涟漪弧：半径随角度与时间轻晃的弧线，盘上没有一条死直的几何线。
        /// wobble 是晃幅（px），传 0 即普通弧
        /// </summary>
        private static void DrawWaterArc(SpriteBatch sb, Vector2 center, float radius,
            float aStart, float aEnd, float width, Color color, float time, float wobble) {
            float span = aEnd - aStart;
            if (MathF.Abs(span) < 0.01f || radius < 2f) {
                return;
            }
            int segs = Math.Max(6, (int)(MathF.Abs(span) * radius / 8f));
            Vector2 prev = center + AngleDir(aStart)
                * (radius + MathF.Sin(aStart * 5f + time * 1.7f) * wobble);
            for (int i = 1; i <= segs; i++) {
                float t = aStart + span * i / segs;
                float r = radius + MathF.Sin(t * 5f + time * 1.7f) * wobble;
                Vector2 cur = center + AngleDir(t) * r;
                KikasaVaultRenderer.DrawLine(sb, prev, cur, width, color);
                prev = cur;
            }
        }
    }
}
