using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDrowns;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaThralls;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaVaults;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaWisps;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI.Panorama;
using CalamityOverhaul.Content.UIs.HudStack;
using CalamityOverhaul.Content.UIs.UIEffect;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.UI
{
    /// <summary>
    /// 掌中风铃：鬼伞常驻 HUD（左下，持伞或领域激活时浮现）。
    /// 檐钩垂一只玻璃风铃，铃身盛着一小汪血湖，液面=涨水进度、晃荡=事件涌浪、
    /// 液中烬点=湖藏填充、整铃随形态浸染。
    /// 信息层（2026-08 重做）：短册只留纸与三席驻影小印（亲和色+在场/收起态），
    /// 铃右一列图标读数，鬼梦犬眼（睡/醒/梦中）、鬼火焰苗（熄/燃/压制，点击点燃/收火）、
    /// 沉溺冷却、鬼雨态的重启冷却与伞奴计数；
    /// 一切悬停说明走顶层 <see cref="KikasaHudTipOverlay"/>，题行 1.0 与原版 tooltip 同级。
    /// 点铃展开「湖心景」全屏（任何域状态都响应）。
    /// </summary>
    internal class KikasaHud : UIHandle, ILocalizedModType, IBottomLeftHud
    {
        public string LocalizationCategory => "Legend.KikasaText";

        public static KikasaHud Instance => UIHandleLoader.GetUIHandleOfType<KikasaHud>();

        public static LocalizedText ScrollName { get; private set; }
        public static LocalizedText OpenTag { get; private set; }
        public static LocalizedText TipWaterFormat { get; private set; }
        public static LocalizedText TipVaultFormat { get; private set; }
        public static LocalizedText TipWispClick { get; private set; }
        public static LocalizedText TipCooldownFormat { get; private set; }
        public static LocalizedText TipDrownTitle { get; private set; }
        public static LocalizedText TipDrownHintFormat { get; private set; }
        public static LocalizedText TipDrownReady { get; private set; }
        public static LocalizedText TipResetTitle { get; private set; }
        public static LocalizedText TipResetHintFormat { get; private set; }
        public static LocalizedText TipThrallFormat { get; private set; }
        public static LocalizedText TipHoundCountFormat { get; private set; }
        public static LocalizedText TipSeatsHintFormat { get; private set; }
        public static LocalizedText TipSeatEmpty { get; private set; }

        public override void SetStaticDefaults() {
            ScrollName = this.GetLocalization(nameof(ScrollName), () => "Lakeheart");
            OpenTag = this.GetLocalization(nameof(OpenTag), () => "Click to unfold");
            TipWaterFormat = this.GetLocalization(nameof(TipWaterFormat), () => "Water {0}%");
            TipVaultFormat = this.GetLocalization(nameof(TipVaultFormat), () => "Hoard {0} / {1}");
            TipWispClick = this.GetLocalization(nameof(TipWispClick),
                () => "[Click] Light / draw back the flame");
            TipCooldownFormat = this.GetLocalization(nameof(TipCooldownFormat),
                () => "Cooling down \u00b7 {0}%");
            TipDrownTitle = this.GetLocalization(nameof(TipDrownTitle), () => "The Drowning Hand");
            TipDrownHintFormat = this.GetLocalization(nameof(TipDrownHintFormat),
                () => "Point at a foe or hold an item and press {0}");
            TipDrownReady = this.GetLocalization(nameof(TipDrownReady), () => "The hand is ready");
            TipResetTitle = this.GetLocalization(nameof(TipResetTitle), () => "Wide Restart");
            TipResetHintFormat = this.GetLocalization(nameof(TipResetHintFormat),
                () => "Press {0} in the ghost rain to wind the field back");
            TipThrallFormat = this.GetLocalization(nameof(TipThrallFormat),
                () => "Umbrella thralls afield {0} / {1}");
            TipHoundCountFormat = this.GetLocalization(nameof(TipHoundCountFormat),
                () => "Hounds afield {0} / {1}");
            TipSeatsHintFormat = this.GetLocalization(nameof(TipSeatsHintFormat),
                () => "{0} wheel calls/recalls \u00b7 manage seats in the Lakeheart");
            TipSeatEmpty = this.GetLocalization(nameof(TipSeatEmpty), () => "Vacant seat");
        }

        //==================== 可见性 ====================

        private float appear;

        private static bool HoldingUmbrella(Player p) {
            Item item = p.GetItem();
            return item != null && item.Alives()
                && item.type == ModContent.ItemType<KikasaItem>();
        }

        private static bool WantVisible(Player p)
            => HoldingUmbrella(p) || p.GetModPlayer<KikasaDomainPlayer>().AnyActive;

        public override bool Active {
            get {
                Player p = Main.LocalPlayer;
                if (p == null || !p.active || p.dead || Main.dedServ) {
                    return false;
                }
                return WantVisible(p) || appear > 0.01f;
            }
        }

        #region 左下角 HUD 队列接入
        bool IBottomLeftHud.HudStackActive => Active;
        int IBottomLeftHud.HudStackOrder => 0;
        Vector2 IBottomLeftHud.HudStackAnchor => NaturalAnchor;
        //上覆读数栈顶，下到短册尾
        float IBottomLeftHud.HudStackTopExtent => KikasaHudTheme.ChimeH * 0.5f + 34f;
        float IBottomLeftHud.HudStackBottomExtent => KikasaHudTheme.ChimeH * 0.5f + 8f;
        #endregion

        //====== 风铃内部布局（相对锚点=风铃中心，静止位） ======

        //檐钩（摆锤支点）
        private const float HookY = -51f;
        //铃身中心
        private const float BellY = -16f;
        //铃舌珠
        private const float ClapperY = 7f;
        //短册顶与尺寸
        private const float TanzakuY = 11f;
        private const float TanzakuW = 14f;
        private const float TanzakuH = 35f;
        //读数栈：铃右一列仪器读数（不随摆，读数是仪器，铃是风物）
        private const float ReadoutX = 34f;
        private const float ReadoutTopY = -44f;
        private const float ReadoutGapY = 23f;
        private const float ReadoutHitHalf = 10f;

        //====== SVG 路径（归一 [-1,1]，A 弧不可用） ======

        //檐钩短枝：一段斜出的枝子，末端下弯成钩，钩尖收在 (0, 0.5)
        private const string BranchPath =
            "M -1 -0.5 Q -0.45 -0.72 0.1 -0.45 Q 0.55 -0.28 0.62 0.0 "
            + "M 0.62 0.0 Q 0.66 0.3 0.35 0.42 Q 0.12 0.5 0 0.5";

        //铃身轮廓（闭环，供巡行亮笔与缺编回退）：球肩 + 波口唇线
        private const string BellRimPath =
            "M -0.78 0.55 Q -1.02 -0.08 -0.56 -0.60 Q 0 -0.96 0.56 -0.60 "
            + "Q 1.02 -0.08 0.78 0.55 Q 0.4 0.63 0 0.60 Q -0.4 0.57 -0.78 0.55";

        //铃顶冠结：小玻璃冠盖 + 一道系绳箍带，接住吊绳
        private const string CrownPath =
            "M -1 0.6 Q -0.9 -0.25 0 -0.45 Q 0.9 -0.25 1 0.6 "
            + "M -0.55 0.05 Q 0 -0.18 0.55 0.05";

        //短册墨字：竖排草书一线的抽象水印（非可读字）
        private const string TanzakuInkPath =
            "M 0.04 -1 Q -0.20 -0.72 0.06 -0.48 Q 0.26 -0.30 -0.06 -0.10 "
            + "Q -0.26 0.04 0.08 0.22 M -0.04 0.42 Q 0.16 0.55 -0.02 0.72 "
            + "Q -0.14 0.84 0.06 0.98";

        //犬眼：杏仁轮廓（醒/梦中睁着），睡时只留下睑一线
        private const string EyeOpenPath =
            "M -1 0 Q 0 -0.85 1 0 Q 0 0.85 -1 0";
        private const string EyeClosedPath =
            "M -1 0.1 Q 0 0.4 1 0.1";

        //焰苗：一条上收的火舌
        private const string FlamePath =
            "M 0 1 Q -0.7 0.25 -0.3 -0.25 Q -0.05 -0.6 0.05 -1 "
            + "Q 0.45 -0.45 0.3 -0.05 Q 0.55 0.4 0 1";

        //沉溺之手：一滴下坠的水珠
        private const string DropPath =
            "M 0 -1 Q 0.8 0.05 0.5 0.55 Q 0.25 1 0 1 Q -0.25 1 -0.5 0.55 Q -0.8 0.05 0 -1";

        /// <summary>自然锚点（风铃中心），未参与左下队列避让时的原始位置</summary>
        public static Vector2 NaturalAnchor => new(KikasaHudTheme.AnchorOffset.X,
            KikasaHudTheme.UIScreenH + KikasaHudTheme.AnchorOffset.Y);

        /// <summary>风铃中心锚点，经左下队列避让后的最终位</summary>
        public static Vector2 Anchor {
            get {
                KikasaHud inst = Instance;
                return inst == null ? NaturalAnchor : BottomLeftHudStack.ResolveAnchor(inst);
            }
        }

        /// <summary>风铃整体命中矩形</summary>
        public static Rectangle ChimeRect {
            get {
                Vector2 anchor = Anchor;
                return new Rectangle(
                    (int)(anchor.X - KikasaHudTheme.ChimeW * 0.5f),
                    (int)(anchor.Y - KikasaHudTheme.ChimeH * 0.5f),
                    KikasaHudTheme.ChimeW, KikasaHudTheme.ChimeH);
            }
        }

        /// <summary>铃身静止中心（引导指环也认它）</summary>
        public static Vector2 BellAnchor => Anchor + new Vector2(0f, BellY);

        /// <summary>铃身矩形（引导环与放大动画的锚）</summary>
        public static Rectangle BellRect {
            get {
                Vector2 c = BellAnchor;
                int s = KikasaHudTheme.BellSize;
                return new Rectangle((int)(c.X - s * 0.5f), (int)(c.Y - s * 0.5f), s, s);
            }
        }

        //==================== 状态 ====================

        /// <summary>悬停目标：HUD 的每个读数都有自己的说明</summary>
        private enum TipTarget { None, Bell, Dream, Wisp, Drown, Reset, Thrall, Seats }

        //事件搅一记涌浪（stir），涌浪推摆幅；读数交给图标列
        private float stir;
        private float swingT;
        private int lastVaultCount;
        private int lastMemoryType;
        private bool lastLakeReady;

        private TipTarget hoverTarget = TipTarget.None;
        private float hoverLerp;

        //本帧读数栈的实际条目与位置（Update 布局，Draw/命中/悬停共用一份）
        private readonly List<(TipTarget target, Vector2 pos)> readouts = [];

        private KikasaDomainPlayer Domain => player.GetModPlayer<KikasaDomainPlayer>();
        private KikasaVaultPlayer Vault => player.GetModPlayer<KikasaVaultPlayer>();
        private KikasaDreamPlayer Dream => player.GetModPlayer<KikasaDreamPlayer>();
        private KikasaServantPlayer Servant => player.GetModPlayer<KikasaServantPlayer>();

        //==================== 更新 ====================

        public override void Update() {
            Player p = player;
            bool want = WantVisible(p);
            appear = MathHelper.Clamp(appear + (want ? 0.06f : -0.06f), 0f, 1f);

            Vector2 anchor = Anchor;
            Size = new Vector2(KikasaHudTheme.ChimeW + 52f, KikasaHudTheme.ChimeH + 16f);
            DrawPosition = anchor - new Vector2(KikasaHudTheme.ChimeW * 0.5f + 8f,
                KikasaHudTheme.ChimeH * 0.5f + 8f);
            UIHitBox = DrawPosition.GetRectangle(Size);

            //事件只在铃上搅一记涌浪，细节反馈都在湖心景里
            KikasaVaultPlayer vault = Vault;
            int vaultCount = vault.Stored.Count;
            int memoryType = Servant.LastDrownedType;
            bool lakeReady = vault.LakeReady;
            if ((vaultCount != lastVaultCount || memoryType != lastMemoryType
                || (lakeReady && !lastLakeReady)) && appear > 0.1f) {
                stir = MathF.Max(stir, 0.6f);
            }
            lastVaultCount = vaultCount;
            lastMemoryType = memoryType;
            lastLakeReady = lakeReady;
            stir = MathHelper.Lerp(stir,
                Domain.Phase == KikasaDomainPhase.Opening
                || Domain.Phase == KikasaDomainPhase.Closing ? 0.45f : 0.12f, 0.06f);

            //摆锤相位：水一搅，铃就荡
            swingT += 0.030f + MathHelper.Clamp(stir, 0f, 1f) * 0.055f;

            LayoutReadouts(anchor);

            //悬停解析：读数 > 席位排 > 铃身；命中即占鼠标
            Vector2 mouse = KikasaHudTheme.UIMouse;
            TipTarget newTarget = TipTarget.None;
            if (appear > 0.5f) {
                foreach ((TipTarget target, Vector2 pos) in readouts) {
                    if (MathF.Abs(mouse.X - pos.X) < ReadoutHitHalf
                        && MathF.Abs(mouse.Y - pos.Y) < ReadoutHitHalf + 1f) {
                        newTarget = target;
                        break;
                    }
                }
                if (newTarget == TipTarget.None) {
                    //席位排：短册下段一小片
                    Vector2 seatRow = anchor + new Vector2(0f, TanzakuY + TanzakuH - 10.5f);
                    Rectangle seatsHit = new((int)(seatRow.X - 12f), (int)(seatRow.Y - 7f), 24, 14);
                    if (seatsHit.Contains(mouse.ToPoint())) {
                        newTarget = TipTarget.Seats;
                    }
                    else if (ChimeRect.Contains(mouse.ToPoint())) {
                        newTarget = TipTarget.Bell;
                    }
                }
            }
            hoverTarget = newTarget;
            hoverLerp = MathHelper.Lerp(hoverLerp, hoverTarget != TipTarget.None ? 1f : 0f, 0.15f);

            if (hoverTarget != TipTarget.None) {
                player.mouseInterface = true;
                if (hoverTarget == TipTarget.Bell && keyLeftPressState == KeyPressState.Pressed) {
                    KikasaPanoramaUI pano = KikasaPanoramaUI.Instance;
                    if (pano != null) {
                        if (pano.IsOpen) {
                            pano.Close();
                        }
                        else {
                            pano.Open();
                        }
                    }
                }
                //焰苗即开关：点燃/收火走统一门，点不着回一声轻拒
                if (hoverTarget == TipTarget.Wisp && keyLeftPressState == KeyPressState.Pressed
                    && !KikasaWisp.TryToggle(player)) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.5f, Volume = 0.5f });
                }
            }
        }

        /// <summary>
        /// 读数栈布局：只排本帧有话可说的条目，犬眼与焰苗（常驻两鬼）、
        /// 沉溺（冷却中）、重启与伞奴（鬼雨态）。绘制与命中共用这一份
        /// </summary>
        private void LayoutReadouts(Vector2 anchor) {
            readouts.Clear();
            KikasaDomainPlayer domain = Domain;
            float x = anchor.X + ReadoutX;
            float y = anchor.Y + ReadoutTopY;
            void Push(TipTarget target) {
                readouts.Add((target, new Vector2(x, y)));
                y += ReadoutGapY;
            }
            Push(TipTarget.Dream);
            Push(TipTarget.Wisp);
            if (KikasaDrown.LocalCooldown01 > 0.005f) {
                Push(TipTarget.Drown);
            }
            if (domain.IsRainForm) {
                Push(TipTarget.Reset);
                if (KikasaThrall.CountActive(player.whoAmI) > 0) {
                    Push(TipTarget.Thrall);
                }
            }
        }

        //==================== 绘制 ====================

        public override void Draw(SpriteBatch spriteBatch) {
            float a = appear;
            if (a < 0.01f) {
                return;
            }
            //湖心景铺开后风铃让位，免得铃与全屏同屏抢戏
            float panoOpen = KikasaPanoramaUI.Instance?.OpenProgress ?? 0f;
            a *= 1f - MathHelper.Clamp(panoOpen * 1.4f, 0f, 1f);
            if (a < 0.01f) {
                return;
            }

            KikasaDomainPlayer domain = Domain;
            float rain = KikasaHudTheme.EffectiveRain(domain);
            float rise = domain.AnyActive ? domain.RiseProgress : 0f;
            float lightGate = Vault.Stored.Count / (float)KikasaVaultPlayer.Capacity;
            float time = Main.GlobalTimeWrappedHourly;
            float stir01 = MathHelper.Clamp(stir, 0f, 1f);

            //浮现自下轻托
            Vector2 anchor = Anchor + new Vector2(0f, (1f - a) * 10f);
            Vector2 hook = anchor + new Vector2(0f, HookY);

            //摆角：铃身主摆，铃舌与短册滞后跟摆
            float amp = 0.045f + stir01 * 0.24f;
            float ang = MathF.Sin(swingT) * amp;
            float angC = MathF.Sin(swingT - 0.85f) * amp * 1.12f;
            float angT = MathF.Sin(swingT - 1.45f) * amp * 1.05f
                + MathF.Sin(time * 5.3f) * 0.012f;

            //支点旋转：静止位 y 偏移 → 摆后位置
            Vector2 Swing(float restY, float theta)
                => hook + new Vector2(0f, restY - HookY).RotatedBy(theta);
            Vector2 bellC = Swing(BellY, ang);
            Vector2 bellTop = Swing(BellY - 23f, ang);
            Vector2 clapper = Swing(ClapperY, angC);
            Vector2 tzTop = Swing(TanzakuY, angT);

            Color barCol = KikasaHudTheme.Void(rain);
            Color accent = KikasaHudTheme.Accent(rain);
            Color glow = KikasaHudTheme.Glow(rain);
            Color dim = KikasaHudTheme.TextDim(rain);
            Texture2D px = VaultAsset.placeholder2.Value;

            //1 檐钩短枝（静，不随摆）：粗笔枝身 + 一线受光
            SvgPath branch = SvgPathPen.Path(BranchPath);
            Vector2 branchC = hook + new Vector2(0f, -8f);
            SvgPathPen.Stroke(spriteBatch, branch, branchC, 16f, 0f, barCol, 2.4f, a * 0.95f);
            SvgPathPen.Stroke(spriteBatch, branch, branchC, 16f, 0f, accent, 0.8f, a * 0.35f);

            //2 吊绳与铃舌（先画，玻璃罩在上面）：钩→铃顶→舌珠
            KikasaVaultRenderer.DrawLine(spriteBatch, hook, bellTop, 1.1f, dim * (0.55f * a));
            KikasaVaultRenderer.DrawLine(spriteBatch, bellTop, clapper, 1f, dim * (0.4f * a));
            spriteBatch.Draw(px, clapper, null, barCol * a, MathHelper.PiOver4,
                px.Size() * 0.5f, new Vector2(4.5f / px.Width, 4.5f / px.Height),
                SpriteEffects.None, 0f);

            //2.5 铃顶冠结：冠盖骑在铃肩上收住吊绳，冠顶一粒系结（缘光稍后压住冠脚）
            SvgPath crown = SvgPathPen.Path(CrownPath);
            Vector2 crownC = Swing(BellY - 21.5f, ang);
            SvgPathPen.Stroke(spriteBatch, crown, crownC, 4.6f, ang, barCol, 1.8f, a * 0.9f);
            SvgPathPen.Stroke(spriteBatch, crown, crownC, 4.6f, ang, accent, 0.7f, a * 0.35f);
            spriteBatch.Draw(px, Swing(BellY - 23.8f, ang), null, barCol * (0.9f * a),
                MathHelper.PiOver4, px.Size() * 0.5f,
                new Vector2(2.6f / px.Width, 2.6f / px.Height), SpriteEffects.None, 0f);

            //3 玻璃铃身（TechChime / 缺编回退）：先垫一枚随呼吸的衬光
            float glowBreath = KikasaHudTheme.Breath(time, 0.31f, 0.9f);
            SvgPathPen.SoftDot(spriteBatch, bellC, 26f, glow, (0.05f + glowBreath * 0.03f) * a);
            float bellHover = hoverTarget == TipTarget.Bell ? hoverLerp : 0f;
            DrawBell(spriteBatch, bellC, ang, a, rain, rise, stir01, lightGate, domain, time,
                bellHover);

            //4 铃缘巡行亮笔：悬停/涌浪时一段亮笔沿铃缘走
            float runA = (0.10f + bellHover * 0.30f + MathF.Max(stir01 - 0.2f, 0f) * 0.3f) * a;
            if (runA > 0.03f) {
                SvgPath rim = SvgPathPen.Path(BellRimPath);
                SvgPathPen.StrokeRunner(spriteBatch, rim, bellC,
                    KikasaHudTheme.BellSize * 0.36f, ang, glow, 1.1f, runA,
                    time * 0.16f, 0.14f);
            }

            //5 短册纸条：纸底 + 边线 + 三席驻影小印
            DrawTanzaku(spriteBatch, tzTop, angT, a, rain, dim);

            //6 读数栈：铃右一列仪器读数
            DrawReadouts(spriteBatch, a, rain, domain, time);

            //7 摆到头一记铃缘微光
            float peak = MathF.Abs(MathF.Sin(swingT));
            float glint = MathHelper.Clamp((peak - 0.94f) / 0.06f, 0f, 1f)
                * MathHelper.Clamp(stir01 * 2f - 0.3f, 0f, 1f);
            if (glint > 0.05f) {
                SvgPathPen.SoftDot(spriteBatch, Swing(BellY + 16f, ang), 7f, glow,
                    glint * 0.5f * a);
            }
        }

        /// <summary>铃身：TechChime 吹制玻璃质感 + 常驻内景（烬萤/凝露/潮痕）；
        /// 缺编回退 SVG 轮廓 + 液面一线</summary>
        private static void DrawBell(SpriteBatch sb, Vector2 center, float ang, float a,
            float rain, float fill, float stir01, float lightGate,
            KikasaDomainPlayer domain, float time, float hover) {
            Effect effect = EffectLoader.KikasaScene?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D px = VaultAsset.placeholder2.Value;
            int size = KikasaHudTheme.BellSize;

            if (effect != null && noise != null && effect.Techniques["TechChime"] != null) {
                effect.CurrentTechnique = effect.Techniques["TechChime"];
                effect.Parameters["uTime"]?.SetValue(time);
                effect.Parameters["uAlpha"]?.SetValue(a);
                effect.Parameters["uResolution"]?.SetValue(new Vector2(size, size));
                effect.Parameters["uRain"]?.SetValue(rain);
                effect.Parameters["uStir"]?.SetValue(stir01);
                effect.Parameters["uBoil"]?.SetValue(domain.FlipBoil);
                effect.Parameters["uFlash"]?.SetValue(domain.FlipFlash);
                effect.Parameters["uLightGate"]?.SetValue(lightGate);
                effect.Parameters["uWaterY"]?.SetValue(MathHelper.Clamp(fill, 0f, 1f));
                effect.Parameters["uSwing"]?.SetValue(ang);
                effect.Parameters["uHover"]?.SetValue(hover);

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Main.UIScaleMatrix);
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
                sb.Draw(px, center, null, Color.White, ang, px.Size() * 0.5f,
                    new Vector2(size / (float)px.Width, size / (float)px.Height),
                    SpriteEffects.None, 0f);
                KikasaVaultRenderer.RestoreUIBatch(sb);
                return;
            }

            //缺编回退：铃形轮廓两笔 + 世界水平的液面一线
            SvgPath rim = SvgPathPen.Path(BellRimPath);
            float scale = size * 0.36f;
            SvgPathPen.Stroke(sb, rim, center, scale, ang, KikasaHudTheme.Void(rain), 3.2f, a * 0.95f);
            SvgPathPen.Stroke(sb, rim, center, scale, ang, KikasaHudTheme.Accent(rain), 1f, a * 0.5f);
            if (fill > 0.03f) {
                float lv = MathHelper.Lerp(scale * 0.62f, scale * -0.32f, fill);
                Vector2 lp = center + new Vector2(0f, lv);
                float half = scale * 0.6f;
                KikasaVaultRenderer.DrawLine(sb, lp - new Vector2(half, 0f),
                    lp + new Vector2(half, 0f), 1.4f, KikasaHudTheme.Glow(rain) * (0.55f * a));
            }
        }

        /// <summary>短册：湿暗纸底 + 边线 + 墨字水印与朱印 + 三席驻影小印
        /// （亲和色，亮=在场、半=候湖、沉线=收起、暗=空席）</summary>
        private void DrawTanzaku(SpriteBatch sb, Vector2 top, float ang, float a,
            float rain, Color dim) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 dir = new Vector2(0f, 1f).RotatedBy(ang);
            Vector2 side = new Vector2(1f, 0f).RotatedBy(ang);
            Vector2 center = top + dir * (TanzakuH * 0.5f);

            //纸底（材料纯色底）与下缘浸润
            Color paper = Color.Lerp(new Color(56, 38, 32), new Color(34, 40, 44), rain);
            sb.Draw(px, center, null, paper * (0.92f * a), ang, px.Size() * 0.5f,
                new Vector2(TanzakuW / px.Width, TanzakuH / px.Height), SpriteEffects.None, 0f);
            sb.Draw(px, top + dir * (TanzakuH - 3.5f), null,
                KikasaHudTheme.Accent(rain) * (0.22f * a), ang, px.Size() * 0.5f,
                new Vector2(TanzakuW / px.Width, 7f / px.Height), SpriteEffects.None, 0f);

            //边线与顶端系结
            Color edge = dim * (0.35f * a);
            Vector2 halfW = side * (TanzakuW * 0.5f);
            KikasaVaultRenderer.DrawLine(sb, top - halfW, top - halfW + dir * TanzakuH, 1f, edge);
            KikasaVaultRenderer.DrawLine(sb, top + halfW, top + halfW + dir * TanzakuH, 1f, edge);
            KikasaVaultRenderer.DrawLine(sb, top - halfW, top + halfW, 1f, edge);
            KikasaVaultRenderer.DrawLine(sb, top - halfW + dir * TanzakuH,
                top + halfW + dir * TanzakuH, 1f, edge * 0.8f);
            sb.Draw(px, top, null, dim * (0.6f * a), MathHelper.PiOver4,
                px.Size() * 0.5f, new Vector2(3f / px.Width, 3f / px.Height),
                SpriteEffects.None, 0f);

            //墨字水印与朱印：竖排草书一线沉在纸底，印落条尾略歪（手押的章不会正）
            SvgPath inkGlyph = SvgPathPen.Path(TanzakuInkPath);
            SvgPathPen.Stroke(sb, inkGlyph, top + dir * (TanzakuH * 0.42f), 12.5f, ang,
                dim, 1.1f, a * 0.20f);
            sb.Draw(px, top + dir * (TanzakuH - 16f), null,
                KikasaHudTheme.Accent(rain) * (0.40f * a), ang + 0.3f,
                px.Size() * 0.5f, new Vector2(3f / px.Width, 3f / px.Height),
                SpriteEffects.None, 0f);

            //驻影小印：册底缘三点，三席编成的缩影，亲和身份色，
            //亮呼吸=在场、稳半亮=候湖、印下沉线=收起、暗=空席
            KikasaServantPlayer servant = Servant;
            Vector2 dotRow = top + dir * (TanzakuH - 8.5f);
            bool seatsHovered = hoverTarget == TipTarget.Seats;
            for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                Vector2 pos = dotRow + side * ((i - 1) * 5.2f);
                int key = servant.SlotKeyAt(i);
                if (key == 0) {
                    SvgPathPen.SoftDot(sb, pos, 2.6f, dim, (0.20f + (seatsHovered ? 0.1f : 0f)) * a);
                    continue;
                }
                KikasaAffinity affinity = servant.SlotAffinity(i);
                Color seatCol = affinity == KikasaAffinity.None
                    ? KikasaHudTheme.TextDim(rain)
                    : KikasaEffigyBoard.AffinityColor(affinity);
                bool held = servant.SlotHeldAt(i);
                bool present = servant.FindServantOf(key) != null;
                if (held) {
                    SvgPathPen.SoftDot(sb, pos, 3.2f, seatCol, 0.28f * a);
                    KikasaVaultRenderer.DrawLine(sb, pos + side * -3f + dir * 3.4f,
                        pos + side * 3f + dir * 3.4f, 1.1f, seatCol * (0.5f * a));
                }
                else if (present) {
                    float breath = KikasaHudTheme.Breath(Main.GlobalTimeWrappedHourly, i * 2.7f, 1.6f);
                    SvgPathPen.SoftDot(sb, pos, 3.8f, seatCol, (0.55f + breath * 0.3f) * a);
                }
                else {
                    SvgPathPen.SoftDot(sb, pos, 3.2f, seatCol, 0.42f * a);
                }
            }
        }

        //==================== 读数栈 ====================

        /// <summary>铃右仪器读数列：每模块一枚图标 + 微弧/微条，悬停亮起、说明进顶层</summary>
        private void DrawReadouts(SpriteBatch sb, float a, float rain,
            KikasaDomainPlayer domain, float time) {
            foreach ((TipTarget target, Vector2 pos) in readouts) {
                float hover = hoverTarget == target ? hoverLerp : 0f;
                switch (target) {
                    case TipTarget.Dream:
                        DrawDreamReadout(sb, pos, a, rain, domain, time, hover);
                        break;
                    case TipTarget.Wisp:
                        DrawWispReadout(sb, pos, a, rain, domain, time, hover);
                        break;
                    case TipTarget.Drown:
                        DrawCooldownReadout(sb, pos, a, DropPath,
                            KikasaHudTheme.Glow(rain), KikasaDrown.LocalCooldown01, hover);
                        break;
                    case TipTarget.Reset:
                        DrawCooldownReadout(sb, pos, a, null,
                            new Color(108, 190, 198), KikasaReset.LocalCooldown01, hover);
                        break;
                    case TipTarget.Thrall:
                        DrawThrallReadout(sb, pos, a, rain, hover);
                        break;
                }
            }
        }

        /// <summary>犬眼：睡=下睑一线，醒=杏仁睁眼，梦中=烬瞳燃亮；梦中外圈唤犬冷却弧</summary>
        private void DrawDreamReadout(SpriteBatch sb, Vector2 pos, float a, float rain,
            KikasaDomainPlayer domain, float time, float hover) {
            bool dreaming = domain.Phase == KikasaDomainPhase.Dreaming;
            bool awake = domain.HoundReflection || dreaming;
            Color ember = new(230, 96, 40);
            Color lineCol = awake ? ember : KikasaHudTheme.TextDim(rain);
            float baseA = (awake ? 0.85f : 0.45f) + hover * 0.2f;
            if (awake) {
                SvgPath eye = SvgPathPen.Path(EyeOpenPath);
                SvgPathPen.Stroke(sb, eye, pos, 8f, 0f, lineCol, 1.2f, baseA * a);
                //烬瞳：醒着微光，梦中燃透
                float pupil = dreaming ? 0.9f : 0.45f + hover * 0.25f;
                SvgPathPen.SoftDot(sb, pos, dreaming ? 3.4f : 2.6f, ember, pupil * a);
            }
            else {
                SvgPath lid = SvgPathPen.Path(EyeClosedPath);
                SvgPathPen.Stroke(sb, lid, pos, 8f, 0f, lineCol, 1.2f, baseA * a);
            }
            //梦中：唤犬冷却弧绕着眼走（满=刚唤出，退尽=可再唤）
            if (dreaming) {
                float cd = Dream.HoundCooldown01;
                if (cd > 0.005f) {
                    DrawArcFraction(sb, pos, 10.5f, cd, ember * (0.7f * a));
                }
            }
        }

        /// <summary>焰苗：熄=苍金余烬轮廓，燃=金焰满描+焰芯闪，压制=失温苍金</summary>
        private static void DrawWispReadout(SpriteBatch sb, Vector2 pos, float a, float rain,
            KikasaDomainPlayer domain, float time, float hover) {
            bool burning = domain.WispFireActive && domain.WispT > 0.1f;
            float quench = MathHelper.Clamp(domain.WispQuench, 0f, 1f);
            Color body = Color.Lerp(KikasaWisp.GoldBody, KikasaWisp.PaleDying,
                burning ? quench : 0.75f);
            body = KikasaWisp.Tint(body);
            SvgPath flame = SvgPathPen.Path(FlamePath);
            float flick = burning ? 1f + MathF.Sin(time * 7.1f) * 0.06f : 1f;
            SvgPathPen.Stroke(sb, flame, pos, 8.5f * flick, 0f, body, 1.3f,
                ((burning ? 0.9f : 0.4f) + hover * 0.2f) * a);
            if (burning) {
                SvgPathPen.SoftDot(sb, pos + new Vector2(0f, 2f), 3.2f,
                    KikasaWisp.Tint(KikasaWisp.GoldCore), (0.6f * (1f - quench * 0.6f)) * a);
            }
        }

        /// <summary>冷却读数：图标（缺省画环）+ 环上冷却弧，满=刚用完、退尽=可再用</summary>
        private static void DrawCooldownReadout(SpriteBatch sb, Vector2 pos, float a,
            string iconPath, Color color, float cooldown01, float hover) {
            if (iconPath != null) {
                SvgPath icon = SvgPathPen.Path(iconPath);
                SvgPathPen.Stroke(sb, icon, pos, 7f, 0f, color, 1.2f, (0.6f + hover * 0.25f) * a);
            }
            else {
                //重启：一圈回卷环 + 逆指小针
                DrawArcFraction(sb, pos, 6.5f, 0.8f, color * ((0.55f + hover * 0.25f) * a));
                KikasaVaultRenderer.DrawLine(sb, pos + new Vector2(-2f, -6.5f),
                    pos + new Vector2(-5f, -3.5f), 1.2f, color * ((0.6f + hover * 0.25f) * a));
            }
            float cd = MathHelper.Clamp(cooldown01, 0f, 1f);
            if (cd > 0.005f) {
                DrawArcFraction(sb, pos, 10f, cd, color * (0.65f * a));
            }
        }

        /// <summary>伞奴计数：一枚小伞章 + n/cap 数字（0.8，不再眯眼）</summary>
        private void DrawThrallReadout(SpriteBatch sb, Vector2 pos, float a, float rain, float hover) {
            KikasaVaultRenderer.DrawSeal(sb, pos + new Vector2(-6f, 0f), 6.5f,
                (0.7f + hover * 0.25f) * a, Main.GlobalTimeWrappedHourly, 1f,
                KikasaHudTheme.TextDim(rain), KikasaHudTheme.Accent(rain), KikasaHudTheme.Glow(rain));
            string text = $"{KikasaThrall.CountActive(player.whoAmI)}/{KikasaEffigyBoard.ThrallCap(player)}";
            Utils.DrawBorderString(sb, text, pos + new Vector2(4f, -8f),
                KikasaHudTheme.Text(rain) * ((0.85f + hover * 0.15f) * a), 0.8f);
        }

        /// <summary>自顶顺时针的分数弧（冷却语义）</summary>
        private static void DrawArcFraction(SpriteBatch sb, Vector2 center, float radius,
            float fraction, Color color) {
            float span = MathHelper.TwoPi * MathHelper.Clamp(fraction, 0f, 1f);
            int segs = Math.Max(4, (int)(span * radius / 3.5f));
            float start = -MathHelper.PiOver2;
            Vector2 prev = center + start.ToRotationVector2() * radius;
            for (int i = 1; i <= segs; i++) {
                float t = start + span * i / segs;
                Vector2 cur = center + t.ToRotationVector2() * radius;
                KikasaVaultRenderer.DrawLine(sb, prev, cur, 1.3f, color);
                prev = cur;
            }
        }

        //==================== 顶层悬停说明 ====================

        /// <summary>
        /// 顶层悬浮说明（由 <see cref="KikasaHudTipOverlay"/> 在 Mouse_Text 层调用）：
        /// 状态 + 对应按键提示，题行 1.0、正文 0.9：每个读数都答得上"这是什么、按哪里"
        /// </summary>
        internal void DrawTooltipOverlay(SpriteBatch sb) {
            if (hoverTarget == TipTarget.None || appear < 0.5f) {
                return;
            }
            float panoOpen = KikasaPanoramaUI.Instance?.OpenProgress ?? 0f;
            float alpha = MathHelper.Clamp(hoverLerp, 0f, 1f)
                * (1f - MathHelper.Clamp(panoOpen * 1.4f, 0f, 1f));
            if (alpha < 0.05f) {
                return;
            }

            KikasaDomainPlayer domain = Domain;
            float rain = KikasaHudTheme.EffectiveRain(domain);
            Color text = KikasaHudTheme.Text(rain);
            Color dimC = KikasaHudTheme.TextDim(rain);
            Color glowC = KikasaHudTheme.Glow(rain);
            Vector2 cursor = KikasaHudTheme.UIMouse;
            string mutateKey = CWRKeySystem.Kikasa_DomainMutate
                .ToTooltipString(CWRKeySystem.Notbound.Value);

            switch (hoverTarget) {
                case TipTarget.Bell: {
                    KikasaVaultPlayer vault = Vault;
                    int waterPct = (int)MathF.Round(
                        (domain.AnyActive ? domain.RiseProgress : 0f) * 100f);
                    KikasaTipPanel.Draw(sb, cursor, ScrollName.Value, rain, alpha,
                        new KikasaTipLine($"{OpenTag.Value} \u00b7 "
                            + CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value),
                            glowC),
                        new KikasaTipLine(string.Format(TipWaterFormat.Value, waterPct), dimC),
                        new KikasaTipLine(string.Format(TipVaultFormat.Value,
                            vault.Stored.Count, KikasaVaultPlayer.Capacity), dimC));
                    return;
                }
                case TipTarget.Dream: {
                    bool dreaming = domain.Phase == KikasaDomainPhase.Dreaming;
                    List<KikasaTipLine> lines = [];
                    if (dreaming) {
                        lines.Add(new KikasaTipLine(KikasaPanoramaUI.InDreamLine.Value,
                            new Color(230, 96, 40)));
                        lines.Add(new KikasaTipLine(string.Format(TipHoundCountFormat.Value,
                            CountHounds(), KikasaDreamPlayer.MaxHoundsFor(player)), dimC));
                        lines.Add(new KikasaTipLine(string.Format(
                            KikasaPanoramaUI.DreamReturnFormat.Value, mutateKey), dimC));
                    }
                    else {
                        lines.Add(new KikasaTipLine(domain.HoundReflection
                            ? KikasaPanoramaUI.ReflectAwake.Value
                            : KikasaPanoramaUI.ReflectAsleep.Value,
                            domain.HoundReflection ? new Color(235, 150, 90) : dimC));
                        lines.Add(new KikasaTipLine(string.Format(
                            KikasaPanoramaUI.DreamEnterKeyFormat.Value, mutateKey), dimC));
                    }
                    lines.Add(new KikasaTipLine(string.Format(KikasaPanoramaUI.HoundBonusFormat.Value,
                        KikasaEffigyBoard.HoundCap(player),
                        (int)MathF.Round(KikasaEffigyBoard.HoundDamageScale(player) * 100f)),
                        KikasaEffigyBoard.NightmareCount(player) > 0
                            ? KikasaEffigyBoard.AffinityColor(KikasaAffinity.Nightmare) : dimC, 0.85f));
                    KikasaTipPanel.Draw(sb, cursor, KikasaPanoramaUI.HoundTitle.Value, rain, alpha,
                        [.. lines]);
                    return;
                }
                case TipTarget.Wisp: {
                    //状态与拒绝原因走共享口径；可操作时给点击提示，点不着时说清差哪一步
                    List<KikasaTipLine> lines = [
                        new KikasaTipLine(KikasaUIText.WispStateLine(domain), domain.WispFireActive
                            ? KikasaWisp.Tint(KikasaWisp.GoldBody) : dimC),
                    ];
                    string block = KikasaUIText.WispBlockReason(domain);
                    lines.Add(block != null
                        ? new KikasaTipLine(block, dimC)
                        : new KikasaTipLine(TipWispClick.Value, glowC));
                    lines.Add(new KikasaTipLine(string.Format(KikasaPanoramaUI.WispBonusFormat.Value,
                        (KikasaEffigyBoard.WispBurnDuration(player) / 60f).ToString("0.0"),
                        (int)KikasaEffigyBoard.WispFlameReach(player)),
                        KikasaEffigyBoard.FlameCount(player) > 0
                            ? KikasaEffigyBoard.AffinityColor(KikasaAffinity.Flame) : dimC, 0.85f));
                    KikasaTipPanel.Draw(sb, cursor, KikasaPanoramaUI.WispTitle.Value, rain, alpha,
                        [.. lines]);
                    return;
                }
                case TipTarget.Drown: {
                    int pct = (int)MathF.Round(KikasaDrown.LocalCooldown01 * 100f);
                    KikasaTipPanel.Draw(sb, cursor, TipDrownTitle.Value, rain, alpha,
                        new KikasaTipLine(pct > 0
                            ? string.Format(TipCooldownFormat.Value, pct)
                            : TipDrownReady.Value, pct > 0 ? dimC : glowC),
                        new KikasaTipLine(string.Format(TipDrownHintFormat.Value,
                            CWRKeySystem.Kikasa_Sink.ToTooltipString(CWRKeySystem.Notbound.Value)),
                            dimC));
                    return;
                }
                case TipTarget.Reset: {
                    int pct = (int)MathF.Round(KikasaReset.LocalCooldown01 * 100f);
                    KikasaTipPanel.Draw(sb, cursor, TipResetTitle.Value, rain, alpha,
                        new KikasaTipLine(string.Format(TipCooldownFormat.Value, pct),
                            pct > 0 ? dimC : glowC),
                        new KikasaTipLine(string.Format(TipResetHintFormat.Value,
                            CWRKeySystem.Legend_Restart.ToTooltipString(CWRKeySystem.Notbound.Value)),
                            dimC));
                    return;
                }
                case TipTarget.Thrall:
                    KikasaTipPanel.Draw(sb, cursor,
                        string.Format(TipThrallFormat.Value,
                            KikasaThrall.CountActive(player.whoAmI),
                            KikasaEffigyBoard.ThrallCap(player)), rain, alpha);
                    return;
                case TipTarget.Seats: {
                    KikasaServantPlayer servant = Servant;
                    List<KikasaTipLine> lines = [];
                    for (int i = 0; i < KikasaServantPlayer.SlotCount; i++) {
                        int key = servant.SlotKeyAt(i);
                        if (key == 0) {
                            lines.Add(new KikasaTipLine(TipSeatEmpty.Value, dimC * 0.8f));
                            continue;
                        }
                        bool held = servant.SlotHeldAt(i);
                        bool present = servant.FindServantOf(key) != null;
                        string state = held ? KikasaUIText.StateHeld.Value
                            : present ? KikasaUIText.StateOut.Value : KikasaUIText.StateAwait.Value;
                        KikasaAffinity affinity = servant.SlotAffinity(i);
                        Color col = affinity == KikasaAffinity.None
                            ? text : KikasaEffigyBoard.AffinityColor(affinity);
                        lines.Add(new KikasaTipLine(
                            $"{KikasaServantPlayer.KeyDisplayName(key)} \u00b7 {state}", col));
                    }
                    lines.Add(new KikasaTipLine(string.Format(TipSeatsHintFormat.Value,
                        CWRKeySystem.GetKeybindText(CWRKeySystem.RadialWheel_Key,
                            CWRKeySystem.Notbound.Value)), dimC, 0.85f));
                    KikasaTipPanel.Draw(sb, cursor, KikasaPanoramaUI.SeatsTitle.Value, rain, alpha,
                        [.. lines]);
                    return;
                }
            }
        }

        /// <summary>场上属于自己的梦犬计数（梦中读数）</summary>
        private int CountHounds() {
            int hounds = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.ModProjectile is KikasaDreamHound) {
                    hounds++;
                }
            }
            return hounds;
        }
    }
}
