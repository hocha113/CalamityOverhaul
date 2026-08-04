using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains;
using CalamityOverhaul.Content.UIs.HudStack;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    internal enum OniLedgerView
    {
        Mei = 0,
        Register = 1,
    }

    /// <summary>
    /// 封印札 HUD,左下角,挂在鬼域之眼下
    /// 墨批读取当前役鬼驾驭,休眠时显焦边;点札开簿;眼控领域见 <see cref="OniDomainEye"/>
    /// </summary>
    internal sealed class OniTalismanHud : UIHandle, ILocalizedModType, IBottomLeftHud
    {
        public string LocalizationCategory => "Legend.OnikiriText";
        public static OniTalismanHud Instance => UIHandleLoader.GetUIHandleOfType<OniTalismanHud>();

        public static LocalizedText HudTitle { get; private set; }
        public static LocalizedText HudHintFormat { get; private set; }
        public static LocalizedText HudMeiName { get; private set; }
        public static LocalizedText HudRegisterName { get; private set; }
        public static LocalizedText HudDangerLine { get; private set; }
        public static LocalizedText HudWraithFormat { get; private set; }
        public static LocalizedText VigorTitle { get; private set; }
        public static LocalizedText VigorValueFormat { get; private set; }
        public static LocalizedText StanceTitle { get; private set; }
        public static LocalizedText StanceValueFormat { get; private set; }
        public static LocalizedText StanceReadyLine { get; private set; }
        public static LocalizedText StanceHalfLine { get; private set; }
        public static LocalizedText DomainTitle { get; private set; }
        public static LocalizedText DomainStateClosed { get; private set; }
        public static LocalizedText DomainStateOmote { get; private set; }
        public static LocalizedText DomainStateUra { get; private set; }
        public static LocalizedText DomainStateShifting { get; private set; }
        public static LocalizedText DomainToggleHintFormat { get; private set; }
        public static LocalizedText DomainFlipHintFormat { get; private set; }

        public override void SetStaticDefaults() {
            HudTitle = this.GetLocalization(nameof(HudTitle), () => "封印札");
            HudHintFormat = this.GetLocalization(nameof(HudHintFormat), () => "{0} 开阖{1} · 点击札打开");
            HudMeiName = this.GetLocalization(nameof(HudMeiName), () => "改铭台");
            HudRegisterName = this.GetLocalization(nameof(HudRegisterName), () => "点鬼簿");
            HudDangerLine = this.GetLocalization(nameof(HudDangerLine), () => "驾驭耗竭，役鬼能力正在休眠");
            HudWraithFormat = this.GetLocalization(nameof(HudWraithFormat), () => "役鬼 {0} · 驾驭 {1}%");
            VigorTitle = this.GetLocalization(nameof(VigorTitle), () => "气力");
            VigorValueFormat = this.GetLocalization(nameof(VigorValueFormat), () => "{0} / {1}");
            StanceTitle = this.GetLocalization(nameof(StanceTitle), () => "架势");
            StanceValueFormat = this.GetLocalization(nameof(StanceValueFormat), () => "{0} / {1}");
            StanceReadyLine = this.GetLocalization(nameof(StanceReadyLine), () => "锋已离鞘——只欠一拔");
            StanceHalfLine = this.GetLocalization(nameof(StanceHalfLine), () => "势已过半——足以一记灭世一闪");
            DomainTitle = this.GetLocalization(nameof(DomainTitle), () => "鬼域之眼");
            DomainStateClosed = this.GetLocalization(nameof(DomainStateClosed), () => "阖目——领域未展");
            DomainStateOmote = this.GetLocalization(nameof(DomainStateOmote), () => "表世界——泛黄和纸");
            DomainStateUra = this.GetLocalization(nameof(DomainStateUra), () => "里世界——水墨阴间");
            DomainStateShifting = this.GetLocalization(nameof(DomainStateShifting), () => "变相中——莫扰");
            DomainToggleHintFormat = this.GetLocalization(nameof(DomainToggleHintFormat), () => "{0} 或左键 展开/收阖领域");
            DomainFlipHintFormat = this.GetLocalization(nameof(DomainFlipHintFormat), () => "{0} 或右键 翻转表里(阖时先展)");
        }

        /// <summary>气力不足反馈:墨痕干笔一颤(玩法层调用,本地客户端)</summary>
        public static void NotifyVigorDenied() => Instance?.vigor.NotifyDenied();
        /// <summary>架势不足反馈:刀在鞘中一顿(玩法层调用,本地客户端)</summary>
        public static void NotifyStanceDenied() => Instance?.stance.NotifyDenied();
        /// <summary>不动护发动反馈:鞘刀沉重归座+金铁裂响(玩法层调用,本地客户端)</summary>
        public static void NotifyStanceGuard() => Instance?.stance.NotifyGuard();
        /// <summary>双疾走连携窗开启</summary>
        public static void NotifyExecutionChainOpen() => Instance?.stance.NotifyExecutionChainOpen();
        /// <summary>双疾走第二段已受理</summary>
        public static void NotifyExecutionDashQueued() => Instance?.stance.NotifyExecutionDashQueued();
        /// <summary>专用处决已锁定目标</summary>
        public static void NotifyExecutionLocked() => Instance?.stance.NotifyExecutionLocked();
        /// <summary>专用处决已受理空放路线</summary>
        public static void NotifyExecutionWhiffQueued() => Instance?.stance.NotifyExecutionWhiffQueued();
        /// <summary>领域命令被拒反馈:鬼眼急促眨动(玩法层调用,本地客户端)</summary>
        public static void NotifyDomainDenied() => Instance?.domainEye.NotifyDenied();

        #region 左下角 HUD 队列接入
        bool IBottomLeftHud.HudStackActive => Active;
        int IBottomLeftHud.HudStackOrder => 0;
        Vector2 IBottomLeftHud.HudStackAnchor => NaturalAnchor;
        //上缘顶到鬼域之眼的辉光边
        float IBottomLeftHud.HudStackTopExtent => OnikiriUITheme.HudEyeTopExtent;
        //簇的下缘取纸札(绳+札+余量)与架势鞘刀(刀轴+刃辉下摆)中更低者
        float IBottomLeftHud.HudStackBottomExtent => MathF.Max(
            OnikiriUITheme.HudRopeLen + OnikiriUITheme.HudTalismanH + 14f,
            OnikiriUITheme.HudStanceOffset.Y + OnikiriUITheme.HudStanceBladeH * 0.5f + 2f);
        #endregion

        private enum TooltipOwner
        {
            None,
            Vigor,
            Stance,
            Domain,
        }

        private float appear;
        private OniLedgerView rememberedLedger;
        private TooltipOwner tooltipOwner;
        private const float RestingBurn = 0.09f;
        private const float MaximumBurn = 0.24f;
        private float burnVisual = RestingBurn;
        private bool hover;
        private bool wasHovered;
        private readonly OniUIParticlePool particles = new(40);
        //气力墨脉:札旁横书一笔墨痕作气力计,共用本 HUD 锚点(数据层见 OniVigorData)
        private readonly OniVigorStroke vigor = new();
        //架势鞘刀:墨脉之下横悬的鞘中刀,拔刀进度=架势(数据层见 OniStanceData)
        private readonly OniStanceSheath stance = new();
        //鬼域之眼:整簇的挂点兼领域控制面(状态直读 OniDomain.Local)
        private readonly OniDomainEye domainEye = new();
        private int emberTimer;
        private float logicTime;
        //挂绳 Verlet:锚点随 HUD 队列避让移动时绳会带着滞后甩摆
        private readonly OniRope rope = new(5, OnikiriUITheme.HudRopeLen + 5f);
        //本帧札体姿态(绳末位置+摆角弹簧),Update 算好供 Draw/粒子/命中共用
        private Vector2 stripTopNow;
        private float stripRotNow;
        //札体摆角弹簧状态:长纸条的转动惯性来自这两个量,而非绳末段的瞬时方向
        private float stripRot;
        private float stripRotVel;
        //捏点的札面局部坐标(x=横向,y=沿札向下),悬停进入时记录
        private Vector2 gripLocal;
        //失去悬停后的帧计数:边缘打滑造成的快速重捏不重复响纸声
        private int hoverOffTicks = 60;

        /// <summary>绳结自然锚点(未避让)</summary>
        public static Vector2 NaturalAnchor => new(OnikiriUITheme.HudAnchorOffset.X,
            OnikiriUITheme.UIScreenH + OnikiriUITheme.HudAnchorOffset.Y);

        /// <summary>绳结锚点(经左下角 HUD 队列避让)</summary>
        public static Vector2 Anchor {
            get {
                OniTalismanHud inst = Instance;
                return inst == null ? NaturalAnchor : BottomLeftHudStack.ResolveAnchor(inst);
            }
        }

        public override Vector2 MousePosition => OnikiriUITheme.UIMouse;

        internal static void RememberLedger(OniLedgerView view) {
            if (Instance != null) {
                Instance.rememberedLedger = view;
            }
        }

        internal static void ToggleRememberedLedger() {
            if (OniMeiUI.Instance?.IsOpen ?? false) {
                OniMeiUI.Instance.Close();
                return;
            }
            if (OniRegisterUI.Instance?.IsOpen ?? false) {
                OniRegisterUI.Instance.Close();
                return;
            }

            OniLedgerView view = Instance?.rememberedLedger ?? OniLedgerView.Mei;
            if (view == OniLedgerView.Register) {
                OniRegisterUI.Instance?.Open();
            }
            else {
                OniMeiUI.Instance?.Open();
            }
        }

        public override void SaveUIData(TagCompound tag)
            => tag[Name + ":rememberedLedger"] = (int)rememberedLedger;

        public override void LoadUIData(TagCompound tag) {
            rememberedLedger = tag.TryGet(Name + ":rememberedLedger", out int value)
                && value == (int)OniLedgerView.Register
                ? OniLedgerView.Register
                : OniLedgerView.Mei;
        }

        private static bool LocalHolding() {
            if (Main.gameMenu || Main.dedServ) {
                return false;
            }
            Item item = Main.LocalPlayer?.HeldItem;
            return item != null && item.Alives()
                && (item.type == ModContent.ItemType<OnikiriItem>());
        }

        private static bool LocalKeepAlive()
            => LocalHolding();

        public override bool Active => LocalKeepAlive() || appear > 0.01f;

        public override void Update() {
            if (hover || vigor.Hovering || stance.Hovering || domainEye.Hovering) {
                player.mouseInterface = true;
            }
        }

        public override void LogicUpdate() {
            bool keepAlive = LocalKeepAlive();
            appear = keepAlive ? MathHelper.Clamp(appear + 0.07f, 0f, 1f) : 0f;
            logicTime += 1f / 60f;
            if (appear <= 0.01f) {
                hover = wasHovered = false;
                hoverOffTicks = Math.Min(hoverOffTicks + 1, 600);
                burnVisual = RestingBurn;
                emberTimer = 0;
                vigor.Reset();
                stance.Reset();
                domainEye.Reset();
                return;
            }
            particles.Update();

            bool danger = OniRegistry.IsEquippedDormant;
            float targetBurn = ResolveBurnTarget(OniRegistry.EquippedEntry);
            burnVisual += (targetBurn - burnVisual) * 0.06f;
            float burnStrength = MathHelper.Clamp(
                (burnVisual - RestingBurn) / (MaximumBurn - RestingBurn), 0f, 1f);

            float registerOpen = OniRegisterUI.Instance?.OpenProgress ?? 0f;
            float meiOpen = OniMeiUI.Instance?.OpenProgress ?? 0f;
            bool uiCovered = registerOpen > 0.4f || meiOpen > 0.4f;

            //鬼域之眼:先推进眼(它是整簇的挂点),左键开阖、右键/中键翻转
            Vector2 knot = Anchor;
            domainEye.Update(player, knot, !uiCovered && appear > 0.5f, MousePosition, logicTime,
                keyLeftPressState == KeyPressState.Pressed,
                keyRightPressState == KeyPressState.Pressed,
                keyMiddlePressState == KeyPressState.Pressed);

            //挂绳推进:常态保持风息,烧蚀越重风势越强;悬停视为被手捏住,风息、阻尼加重
            //大幅甩摆只留给悬停初捏与休眠拽动
            //绳结随眼呼吸微移
            float windAmp = MathHelper.Lerp(0.08f, 0.13f, burnStrength);
            if (hover) {
                windAmp *= 0.2f;
            }
            rope.Update(knot + domainEye.HangSway, null, logicTime, windAmp, endWeight: 0.5f, damping: hover ? 0.78f : 0.84f);
            if (danger && !hover && Main.rand.NextBool(180)) {
                rope.Nudge(Main.rand.NextFloat(0.45f, 0.95f) * (Main.rand.NextBool() ? 1f : -1f), Main.rand.NextFloat(0.35f));
            }
            //悬停牵引:绳末被拉向"让捏点贴住光标"的位置,绳长约束自然给出被拽住的弹性
            if (hover) {
                Vector2 gripSide = stripRot.ToRotationVector2();
                Vector2 gripDown = (MathHelper.PiOver2 + stripRot).ToRotationVector2();
                rope.PullEnd(MousePosition - gripSide * gripLocal.X - gripDown * gripLocal.Y, 0.22f);
            }
            stripTopNow = rope.End;

            //札体摆角:平时以整绳方向为基线做弹簧跟随(末段仅数像素,瞬时方向的噪声
            //会被 112px 札身放大成大幅甩摆);悬停时目标归零,入捏的一下"扶正"就是持握感
            float targetRot, stiffness, rotDamp;
            if (hover) {
                targetRot = 0f;
                stiffness = 0.16f;
                rotDamp = 0.70f;
            }
            else {
                //放大系数 <1:绳向噪声压着用,札身只承接大势不追细碎
                targetRot = ((rope.End - knot).SafeNormalize(Vector2.UnitY).ToRotation() - MathHelper.PiOver2) * 0.72f;
                stiffness = 0.085f;
                rotDamp = 0.80f;
            }
            stripRotVel += (targetRot - stripRot) * stiffness;
            stripRotVel *= rotDamp;
            stripRot = MathHelper.Clamp(stripRot + stripRotVel, -0.32f, 0.32f);
            stripRotNow = stripRot;
            if (danger) {
                stripRotNow += (float)Math.Sin(logicTime * 11f) * 0.010f;
            }

            //命中:光标变换进札面局部空间,与绘制同一姿态的 OBB(旧的轴对齐外包在摆角大时会漏判)
            float w = OnikiriUITheme.HudTalismanW;
            float h = OnikiriUITheme.HudTalismanH;
            Vector2 side = stripRotNow.ToRotationVector2();
            Vector2 down = (MathHelper.PiOver2 + stripRotNow).ToRotationVector2();
            Vector2 rel = MousePosition - stripTopNow;
            float localX = Vector2.Dot(rel, side);
            float localY = Vector2.Dot(rel, down);
            bool inStrip = localX >= -w * 0.5f - 5f && localX <= w * 0.5f + 5f
                && localY >= -3f && localY <= h + 5f;

            //框架命中盒/绘制位:旋转矩形的轴对齐外包
            Vector2 cornerA = stripTopNow - side * (w * 0.5f);
            Vector2 cornerB = stripTopNow + side * (w * 0.5f);
            Vector2 cornerC = cornerA + down * h;
            Vector2 cornerD = cornerB + down * h;
            float minX = MathF.Min(MathF.Min(cornerA.X, cornerB.X), MathF.Min(cornerC.X, cornerD.X));
            float maxX = MathF.Max(MathF.Max(cornerA.X, cornerB.X), MathF.Max(cornerC.X, cornerD.X));
            float minY = MathF.Min(MathF.Min(cornerA.Y, cornerB.Y), MathF.Min(cornerC.Y, cornerD.Y));
            float maxY = MathF.Max(MathF.Max(cornerA.Y, cornerB.Y), MathF.Max(cornerC.Y, cornerD.Y));
            DrawPosition = new Vector2(minX, minY);
            Size = new Vector2(maxX - minX, maxY - minY);
            UIHitBox = new Rectangle((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY));
            if (Tutorial.OnikiriTutorialLead.IsActive)
                Tutorial.OnikiriTutorialTargets.Publish(Tutorial.OnikiriTutorialTargets.Tag_TalismanStrip, UIHitBox);

            //气力墨脉与架势鞘刀推进:点鬼簿开卷时也继续呼吸,只是不受理悬浮
            vigor.Update(player, knot, !uiCovered && appear > 0.5f, MousePosition);
            stance.Update(player, knot, !uiCovered && appear > 0.5f, MousePosition);
            if (uiCovered) {
                hover = wasHovered = false;
                hoverOffTicks = Math.Min(hoverOffTicks + 1, 600);
                return;
            }

            hover = inStrip;
            //初捏:记下捏点并给一声很轻的纸响,不再推绳(推开会破坏"拿住"的感觉)
            if (hover && !wasHovered) {
                gripLocal = new Vector2(MathHelper.Clamp(localX, -w * 0.5f, w * 0.5f), MathHelper.Clamp(localY, 0f, h));
                if (hoverOffTicks > 8) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f, Pitch = 0.35f });
                }
            }
            hoverOffTicks = hover ? 0 : Math.Min(hoverOffTicks + 1, 600);
            wasHovered = hover;
            if (hover) {
                player.mouseInterface = true;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.6f });
                    ToggleRememberedLedger();
                }
            }

            //札脚常有零星余烬,驾驭耗竭时密度随烧蚀升高
            emberTimer++;
            int emberInterval = (int)MathHelper.Lerp(58f, 24f, burnStrength);
            if (emberTimer >= emberInterval) {
                emberTimer = 0;
                Vector2 stripBottom = stripTopNow + (MathHelper.PiOver2 + stripRotNow).ToRotationVector2()
                    * OnikiriUITheme.HudTalismanH;
                particles.SpawnEmber(stripBottom + Main.rand.NextVector2Circular(OnikiriUITheme.HudTalismanW * 0.4f, 2f));
            }
        }

        public override void Draw(SpriteBatch sb) {
            if (appear <= 0.01f) {
                return;
            }
            float registerOpen = OniRegisterUI.Instance?.OpenProgress ?? 0f;
            float meiOpen = OniMeiUI.Instance?.OpenProgress ?? 0f;
            float a = appear * (1f - Math.Max(registerOpen, meiOpen) * 0.7f);
            if (a <= 0.01f) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            Vector2 knot = Anchor;

            float rot = stripRotNow;
            //纸札顶部中点与姿态由 Verlet 绳末段决定
            Vector2 stripTop = stripTopNow;
            Vector2 down = (MathHelper.PiOver2 + rot).ToRotationVector2();
            float W = OnikiriUITheme.HudTalismanW;
            float H = OnikiriUITheme.HudTalismanH;

            //挂绳 Verlet,眼底垂至绳结
            Vector2 knotDraw = knot + domainEye.HangSway;
            OniBrush.DrawGradientLine(sb, domainEye.TieTop, knotDraw, OnikiriUITheme.Deep * (a * 0.30f), OnikiriUITheme.Deep * (a * 0.85f), 1.2f);
            rope.Draw(sb, OnikiriUITheme.Deep * 0.88f, OnikiriUITheme.Deep * 0.62f, 1.3f, a);
            sb.Draw(pixel, knotDraw, src, OnikiriUITheme.Seal * a, MathHelper.PiOver4 + rot * 0.4f, new Vector2(0.5f), new Vector2(4.6f), SpriteEffects.None, 0f);

            //鬼域之眼(画在系带之上,盖住带头)
            domainEye.Draw(sb, a, GlobalTimer);

            //札体:焚烧 shader 是常态和纸材质,驾驭状态只调节下缘烧蚀强度
            Vector2 side = rot.ToRotationVector2();
            OniGhostEntry equipped = OniRegistry.EquippedEntry;
            float mastery = equipped == null ? 0f : MathHelper.Clamp(equipped.Mastery, 0f, 1f);
            float burnStrength = MathHelper.Clamp(
                (burnVisual - RestingBurn) / (MaximumBurn - RestingBurn), 0f, 1f);
            bool paperByShader = OniPaperBurnDraw.Available;
            if (paperByShader) {
                OniPaperBurnDraw.Draw(sb, stripTop, rot, new Vector2(W, H),
                    a * (hover ? 1.05f : 0.96f), burnVisual, GlobalTimer);
            }
            else {
                OniBrush.DrawPaperStrip(sb, stripTop, rot, new Vector2(W, H), a * (hover ? 1.05f : 0.96f), GlobalTimer * 0.11f);
                DrawCharredHem(sb, stripTop, down, side, W, H, a, burnStrength);
            }

            //方章与完整淡墨笔势属于封印札本体,役鬼只负责写实驾驭进度
            DrawTalismanSeal(sb, stripTop + down * 16f, rot, equipped?.Key, a);
            Vector2 strokeStart = stripTop + down * 29f;
            Vector2 strokeFullEnd = stripTop + down * (H - 13f);
            float guideAlpha = equipped == null ? 0.30f : 0.18f;
            OniBrush.DrawTaperedSlash(sb, strokeStart, strokeFullEnd, 3.2f, 0.9f, a * guideAlpha);

            if (equipped != null) {
                //墨批:自印下垂书一笔,长度=当前役鬼驾驭度
                if (mastery > 0.02f) {
                    Vector2 strokeEnd = stripTop + down * (29f + (H - 42f) * mastery);
                    OniBrush.DrawTaperedSlash(sb, strokeStart, strokeEnd, 3.8f, 0.9f, a * 0.92f);
                }
            }

            //气力墨脉:定在锚侧不随札摆,墨丝自札边垂下把两者缝在一起
            Vector2 vigorAttach = stripTop + down * (H * 0.62f) + side * (W * 0.5f - 2f);
            vigor.Draw(sb, a, vigorAttach, GlobalTimer);

            //架势鞘刀:横悬在墨脉之下,拔刀进度=架势
            stance.Draw(sb, a, GlobalTimer);

            particles.Draw(sb, a);
        }

        private static void DrawTalismanSeal(SpriteBatch sb, Vector2 center, float rot, string key, float alpha) {
            float seed = string.IsNullOrEmpty(key) ? 0.5f : OniGhostShadowDraw.SeedFromKey(key);
            OniBrush.DrawSealGlyph(sb, center, 9.5f, alpha * 0.95f, rot + (seed - 0.5f) * 0.18f);
            Vector2 side = rot.ToRotationVector2();
            Vector2 down = (rot + MathHelper.PiOver2).ToRotationVector2();
            float offset = (seed - 0.5f) * 3f;
            OniBrush.DrawGradientLine(sb, center - side * 5f + down * offset,
                center + side * 5f - down * offset, OnikiriUITheme.Bright * (alpha * 0.76f),
                OnikiriUITheme.Deep * (alpha * 0.34f), 1f);
        }

        private static float ResolveBurnTarget(OniGhostEntry equipped) {
            if (equipped == null) {
                return RestingBurn;
            }

            float mastery = MathHelper.Clamp(equipped.Mastery, 0f, 1f);
            float depletion = (1f - mastery) * 0.04f;
            float dormantHeat = equipped.IsDormant ? 0.10f : 0f;
            return MathHelper.Clamp(RestingBurn + depletion + dormantHeat, RestingBurn, MaximumBurn);
        }

        internal void DrawTooltipOverlay(SpriteBatch sb) {
            if (appear <= 0.01f) {
                tooltipOwner = TooltipOwner.None;
                return;
            }
            float registerOpen = OniRegisterUI.Instance?.OpenProgress ?? 0f;
            float meiOpen = OniMeiUI.Instance?.OpenProgress ?? 0f;
            float a = appear * (1f - Math.Max(registerOpen, meiOpen) * 0.7f);
            if (a <= 0.01f) {
                tooltipOwner = TooltipOwner.None;
                return;
            }

            if (hover) {
                tooltipOwner = TooltipOwner.None;
                DrawHoverPanel(sb, a);
            }
            else if (vigor.Hovering) {
                tooltipOwner = TooltipOwner.Vigor;
                vigor.DrawTooltip(sb, a);
            }
            else if (stance.Hovering) {
                tooltipOwner = TooltipOwner.Stance;
                stance.DrawTooltip(sb, a);
            }
            else if (domainEye.Hovering) {
                tooltipOwner = TooltipOwner.Domain;
                domainEye.DrawTooltip(sb);
            }
            else {
                switch (tooltipOwner) {
                    case TooltipOwner.Vigor when vigor.TooltipVisible:
                        vigor.DrawTooltip(sb, a);
                        break;
                    case TooltipOwner.Stance when stance.TooltipVisible:
                        stance.DrawTooltip(sb, a);
                        break;
                    case TooltipOwner.Domain when domainEye.TooltipVisible:
                        domainEye.DrawTooltip(sb);
                        break;
                    default:
                        tooltipOwner = TooltipOwner.None;
                        break;
                }
            }
        }

        /// <summary>札脚焦边:shader 缺席时按烧蚀强度退回手绘炭痕与暖焰</summary>
        private void DrawCharredHem(SpriteBatch sb, Vector2 stripTop, Vector2 down, Vector2 side,
            float w, float h, float a, float burnStrength) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            float rot = stripRotNow;
            burnStrength = MathHelper.Clamp(burnStrength, 0f, 1f);
            const int Cols = 10;
            float step = w / Cols;
            for (int i = 0; i < Cols; i++) {
                float x = -w * 0.5f + (i + 0.5f) * step;
                float hash = OniBrush.Hash01(i * 97 + 11);
                float charH = MathHelper.Lerp(2f + hash * 2f, 3f + hash * 5f, burnStrength);
                Vector2 pos = stripTop + down * (h - charH * 0.5f) + side * x;
                sb.Draw(pixel, pos, src, OnikiriUITheme.Ink * (a * 0.92f), rot, new Vector2(0.5f), new Vector2(step + 0.6f, charH), SpriteEffects.None, 0f);
                float scorchH = MathHelper.Lerp(1.2f, 2f, burnStrength);
                sb.Draw(pixel, pos - down * (charH * 0.5f + scorchH * 0.5f), src,
                    OnikiriUITheme.Dark * (a * MathHelper.Lerp(0.48f, 0.7f, burnStrength)), rot,
                    new Vector2(0.5f), new Vector2(step + 0.6f, scorchH), SpriteEffects.None, 0f);

                if (hash > MathHelper.Lerp(0.72f, 0.52f, burnStrength)) {
                    float flick = (float)Math.Sin(GlobalTimer * (3.1f + hash * 2.6f) + i * 1.9f) * 0.5f + 0.5f;
                    float flameH = MathHelper.Lerp(2f + hash * 2.5f, 3f + hash * 4.5f, burnStrength)
                        * (0.4f + flick * 0.6f);
                    float flameAlpha = MathHelper.Lerp(0.52f, 1f, burnStrength);
                    Vector2 flamePos = stripTop + down * (h - charH) + side * x;
                    sb.Draw(pixel, flamePos, src, OnikiriUITheme.BurnDim * (a * 0.55f * flick * flameAlpha), rot,
                        new Vector2(0.5f, 1f), new Vector2(step - 0.5f, flameH), SpriteEffects.None, 0f);
                    sb.Draw(pixel, flamePos, src, OnikiriUITheme.BurnHot * (a * 0.72f * flick * flameAlpha), rot,
                        new Vector2(0.5f, 1f), new Vector2(1.4f, flameH * 0.6f), SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>悬浮说明:小裱墨牌,题名/开簿键位/危态告警</summary>
        private void DrawHoverPanel(SpriteBatch sb, float a) {
            string keyName = CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value);
            string title = HudTitle.Value;
            string ledgerName = rememberedLedger == OniLedgerView.Register
                ? HudRegisterName.Value
                : HudMeiName.Value;
            string hint = string.Format(HudHintFormat.Value, keyName, ledgerName);
            OniGhostEntry equipped = OniRegistry.EquippedEntry;
            if (equipped == null) {
                OniTooltipPanel.Draw(sb, MousePosition, title, 0.82f, a,
                    new OniTooltipLine(hint, OnikiriUITheme.TextDim));
                return;
            }
            bool danger = equipped.IsDormant;
            string wraithLine = HudWraithFormat.Format(equipped.Name?.Invoke() ?? equipped.Key,
                (int)MathF.Round(equipped.Mastery * 100f));
            string dangerLine = danger ? HudDangerLine.Value : null;
            OniTooltipPanel.Draw(sb, MousePosition, title, 0.82f, a,
                new OniTooltipLine(hint, OnikiriUITheme.TextDim),
                new OniTooltipLine(wraithLine, OnikiriUITheme.Paper),
                new OniTooltipLine(dangerLine, OnikiriUITheme.GhostFire * 0.9f));
        }
    }
}
