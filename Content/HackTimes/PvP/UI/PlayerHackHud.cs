using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Chips;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.PvP.UI
{
    /// <summary>
    /// PvP 骇入的表现主题：防守方视角的入侵语汇<b>恒定敌对红族</b>，
    /// 不走 HackTheme.HostileBlend 插值，你是受害者，没有中立态。
    /// 色值与 HackTheme 的敌对态一致（那边是 private，这里是 PvP 侧的公开镜像）
    /// </summary>
    internal static class PvPTheme
    {
        internal static readonly Color Hostile = new(230, 56, 68);
        internal static readonly Color HostileAlt = new(255, 122, 92);
        internal static readonly Color HostileGlow = new(235, 70, 82);
        internal static readonly Color HostileBorder = new(126, 46, 56);
        /// <summary>回溯反向追踪的亮青（防守方翻转主动时的唯一冷色）</summary>
        internal static readonly Color TraceCyan = new(60, 220, 230);
        /// <summary>上传期琥珀（沿用 HackTheme.Uploading 族）</summary>
        internal static readonly Color Amber = new(200, 170, 40);
    }

    /// <summary>PvP HUD 本地化文本</summary>
    internal class PvPHudText : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static LocalizedText BeingHackedBy { get; private set; }
        public static LocalizedText MoreIntrusions { get; private set; }
        public static LocalizedText UploadCanceled { get; private set; }
        public static LocalizedText UploadLanded { get; private set; }
        public static LocalizedText PositionExposed { get; private set; }
        public static LocalizedText GaugeUntrusted { get; private set; }
        public static LocalizedText GaugeSyncTag { get; private set; }
        public static LocalizedText GaugeLifeTag { get; private set; }
        public static LocalizedText GaugeManaTag { get; private set; }
        public static LocalizedText TracebackFlip { get; private set; }
        public static LocalizedText AlertTraced { get; private set; }
        public static LocalizedText AlertTargetLost { get; private set; }
        public static LocalizedText AlertRejectedFormat { get; private set; }
        public static LocalizedText ImplantPanelTitle { get; private set; }
        public static LocalizedText ImplantUninstalled { get; private set; }
        public static LocalizedText ImplantExpired { get; private set; }
        public static LocalizedText SignalLost { get; private set; }
        public static LocalizedText RemainSecondsFormat { get; private set; }
        public static LocalizedText CasterFormat { get; private set; }
        public static LocalizedText DistanceFormat { get; private set; }

        public override void SetStaticDefaults() {
            BeingHackedBy = this.GetLocalization(nameof(BeingHackedBy),
                () => "BEING HACKED BY {0}");
            MoreIntrusions = this.GetLocalization(nameof(MoreIntrusions),
                () => "+{0} INTRUSIONS");
            UploadCanceled = this.GetLocalization(nameof(UploadCanceled),
                () => "LINK SEVERED");
            UploadLanded = this.GetLocalization(nameof(UploadLanded),
                () => ">> BREACHED <<");
            PositionExposed = this.GetLocalization(nameof(PositionExposed),
                () => "POSITION EXPOSED");
            GaugeUntrusted = this.GetLocalization(nameof(GaugeUntrusted),
                () => "READOUT UNRELIABLE");
            GaugeSyncTag = this.GetLocalization(nameof(GaugeSyncTag), () => "SYNC");
            GaugeLifeTag = this.GetLocalization(nameof(GaugeLifeTag), () => "HP");
            GaugeManaTag = this.GetLocalization(nameof(GaugeManaTag), () => "MP");
            TracebackFlip = this.GetLocalization(nameof(TracebackFlip),
                () => "TRACEBACK ACTIVE");
            AlertTraced = this.GetLocalization(nameof(AlertTraced),
                () => "LINK TRACED BACK");
            AlertTargetLost = this.GetLocalization(nameof(AlertTargetLost),
                () => "TARGET LOST · RAM REFUNDED");
            AlertRejectedFormat = this.GetLocalization(nameof(AlertRejectedFormat),
                () => "REJECTED · CODE {0}");
            ImplantPanelTitle = this.GetLocalization(nameof(ImplantPanelTitle),
                () => "IMPLANTS");
            ImplantUninstalled = this.GetLocalization(nameof(ImplantUninstalled),
                () => "UNINSTALLED");
            ImplantExpired = this.GetLocalization(nameof(ImplantExpired),
                () => "EXPIRED");
            SignalLost = this.GetLocalization(nameof(SignalLost),
                () => "SIGNAL LOST");
            RemainSecondsFormat = this.GetLocalization(nameof(RemainSecondsFormat),
                () => "{0}s");
            CasterFormat = this.GetLocalization(nameof(CasterFormat), () => "BY {0}");
            DistanceFormat = this.GetLocalization(nameof(DistanceFormat), () => "{0}m");
        }
    }

    /// <summary>
    /// 帐本/镜像/网络层向 HUD 投递表现事件的槽。全部客户端本地视觉状态；
    /// 服务端调进来直接空转。声音一律记 beat 防重放（7.5 律）
    /// </summary>
    internal static class PlayerHackHudFeed
    {
        /// <summary>UI 空间碎片粒子（卡片碎裂/横幅打断用，纯本机）</summary>
        internal struct UiDebris
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public int Life;
            public int MaxLife;
            public Color Color;
            public float Size;
        }

        internal sealed class AlertLabel
        {
            public string Text;
            public Color Color;
            public int FramesLeft;
            public int MaxFrames;
        }

        internal static readonly List<UiDebris> Debris = [];
        internal static readonly List<AlertLabel> Alerts = [];
        /// <summary>屏幕边缘红闪剩余帧（链路被回溯）</summary>
        internal static int EdgeFlashFrames;
        /// <summary>横幅→效果条交棒白闪剩余帧</summary>
        internal static int HandoffFlashFrames;

        internal static void NotifyNotice(PlayerHackNotice entry) {
            if (Main.dedServ || entry == null) return;
            //首达播一声低位警示；每攻击方每次上传只播一次
            if (!entry.PlayedCue && !entry.Terminal) {
                entry.PlayedCue = true;
                SoundEngine.PlaySound(CWRSound.Hacker with {
                    Volume = 0.6f,
                    Pitch = -0.3f
                });
            }
            else if (entry.State == 3) {
                //落地故障重音 + 交棒白闪
                SoundEngine.PlaySound(CWRSound.Hacker with {
                    Volume = 0.85f,
                    Pitch = 0.15f
                });
                HandoffFlashFrames = Math.Max(HandoffFlashFrames, 10);
            }
        }

        internal static void NotifyEffectApplied(PlayerHackEffect effect) {
            if (Main.dedServ) return;
            HandoffFlashFrames = Math.Max(HandoffFlashFrames, 10);
        }

        internal static void NotifyEffectRemoved(PlayerHackEffect effect,
            PlayerHackRemoveReason reason) {
            if (Main.dedServ) return;
            //被拔碎裂有声，自然到期安静淡出
            if (reason is PlayerHackRemoveReason.Uninstalled) {
                SoundEngine.PlaySound(CWRSound.Hacker with {
                    Volume = 0.7f,
                    Pitch = -0.5f
                });
                PlayerHackHud.RequestShatter(effect.ActivationId);
            }
        }

        internal static void NotifyMirrorLanded(long activationId, int defenderIndex,
            int casterIndex, int slotIndex) {
            if (Main.dedServ || casterIndex != Main.myPlayer) return;
            //自己的植入物落地：轻确认音（攻击方对称反馈）
            SoundEngine.PlaySound(CWRSound.Hacker with {
                Volume = 0.5f,
                Pitch = 0.35f
            });
        }

        internal static void NotifyMirrorRemoved(PlayerHackMirror.MirrorEffect fx,
            PlayerHackRemoveReason reason) {
            if (Main.dedServ || fx.CasterIndex != Main.myPlayer) return;
            if (reason == PlayerHackRemoveReason.Uninstalled) {
                SoundEngine.PlaySound(CWRSound.Hacker with {
                    Volume = 0.6f,
                    Pitch = -0.2f
                });
            }
        }

        internal static void NotifyTracebackFired(int tracedCount) {
            if (Main.dedServ) return;
            PushAlert(PvPHudText.TracebackFlip.Value, PvPTheme.TraceCyan, 90);
        }

        internal static void NotifyAlert(PlayerHackAlert kind, byte detail) {
            if (Main.dedServ) return;
            switch (kind) {
                case PlayerHackAlert.Traced:
                    EdgeFlashFrames = 24;
                    SoundEngine.PlaySound(CWRSound.Hacker with {
                        Volume = 0.9f,
                        Pitch = 0.5f
                    });
                    PushAlert(PvPHudText.AlertTraced.Value, PvPTheme.Hostile, 90);
                    break;
                case PlayerHackAlert.TargetLost:
                    PushAlert(PvPHudText.AlertTargetLost.Value, HackTheme.TextDim, 120);
                    break;
                case PlayerHackAlert.Rejected:
                    //拒绝必须可读（№2.8 的 HUD 版）：带上拒绝码
                    PushAlert(PvPHudText.AlertRejectedFormat.Format(detail),
                        PvPTheme.Amber, 120);
                    break;
            }
        }

        private static void PushAlert(string text, Color color, int frames) {
            Alerts.Add(new AlertLabel {
                Text = text,
                Color = color,
                FramesLeft = frames,
                MaxFrames = frames,
            });
            if (Alerts.Count > 4) Alerts.RemoveAt(0);
        }

        internal static void SpawnDebris(Vector2 pos, Color color, int count) {
            for (int i = 0; i < count; i++) {
                Debris.Add(new UiDebris {
                    Pos = pos + new Vector2(Main.rand.NextFloat(-16f, 16f),
                        Main.rand.NextFloat(-8f, 8f)),
                    Vel = new Vector2(Main.rand.NextFloat(-2.4f, 2.4f),
                        Main.rand.NextFloat(-2.8f, 0.6f)),
                    Life = 0,
                    MaxLife = Main.rand.Next(20, 42),
                    Color = color,
                    Size = Main.rand.NextFloat(1.5f, 4f),
                });
            }
        }

        internal static void Tick() {
            for (int i = Debris.Count - 1; i >= 0; i--) {
                UiDebris d = Debris[i];
                d.Pos += d.Vel;
                d.Vel.Y += 0.16f;
                d.Life++;
                if (d.Life >= d.MaxLife) Debris.RemoveAt(i);
                else Debris[i] = d;
            }
            for (int i = Alerts.Count - 1; i >= 0; i--) {
                if (--Alerts[i].FramesLeft <= 0) Alerts.RemoveAt(i);
            }
            if (EdgeFlashFrames > 0) EdgeFlashFrames--;
            if (HandoffFlashFrames > 0) HandoffFlashFrames--;
        }

        internal static void Reset() {
            Debris.Clear();
            Alerts.Clear();
            EdgeFlashFrames = 0;
            HandoffFlashFrames = 0;
        }
    }

    /// <summary>
    /// 被骇者 HUD 三件套 + 攻击方对称反馈（UI 空间层）。<br/>
    /// ① 顶部效果条（数据源 = 本机 <see cref="PlayerHackLedger"/>，真值零延迟）；<br/>
    /// ② 其下被骇横幅（数据源 = DefenderNotice，45f TTL 自清）；<br/>
    /// ③ 攻击方植入物面板（右上小地图下方，数据源 = PlayerEffectState 镜像）；<br/>
    /// ④ 警报标签、UI 碎片、屏幕边缘红闪、读数污染等协议自绘覆盖层。<br/>
    /// 红线与世界标记在 <c>PlayerHackLinkRender</c>（世界坐标层）。<br/>
    /// 布局全走 UI 空间坐标（HackTheme.UIScreenW/H），横幅底板实底 BgPanel + 1px 边框，
    /// 发光只用亮色多 pass：禁 magic-pixel 暗羽化
    /// </summary>
    internal class PlayerHackHud : UIHandle
    {
        public static PlayerHackHud Instance
            => UIHandleLoader.GetUIHandleOfType<PlayerHackHud>();

        //效果卡几何（132×40 复用 HackStatusDisplay 卡片语法放大版）
        private const float CardW = 132f;
        private const float CardH = 40f;
        private const float CardGap = 8f;
        private const float EffectRowY = 10f;
        private const int MaxVisibleCards = 4;
        private const int MaxVisibleBanners = 3;

        //条目飞入进度（activationId → 0..1）
        private static readonly Dictionary<long, float> cardFlyIn = [];
        //待碎裂条目（叠加在移除帧上）
        private static readonly HashSet<long> shatterRequests = [];
        //横幅滑入进度（attacker+request 键 → 0..1）
        private static readonly Dictionary<ulong, float> bannerSlide = [];
        private static readonly List<PlayerHackMirror.MirrorEffect> ownImplantCache = [];

        //悬停 tooltip：本帧命中的条目（Draw 末尾统一画，单 overlay 律）
        private PlayerHackEffect hoveredEffect;

        public override bool Active {
            get {
                if (Main.gameMenu || Main.dedServ) return false;
                Player player = Main.LocalPlayer;
                if (player?.active != true) return false;
                var ledger = player.GetModPlayer<PlayerHackLedger>();
                if (ledger.ActiveEffects.Count > 0
                    || ledger.IncomingUploads.Count > 0
                    || ledger.TracebackMarkers.Count > 0) {
                    return true;
                }
                PlayerHackMirror.CollectOwnImplants(ownImplantCache);
                return ownImplantCache.Count > 0
                    || PlayerHackHudFeed.Alerts.Count > 0
                    || PlayerHackHudFeed.Debris.Count > 0
                    || PlayerHackHudFeed.EdgeFlashFrames > 0;
            }
        }

        internal static void RequestShatter(long activationId) {
            shatterRequests.Add(activationId);
        }

        public override void Update() {
            PlayerHackHudFeed.Tick();

            var ledger = Main.LocalPlayer.GetModPlayer<PlayerHackLedger>();
            //飞入推进 + 已消失条目的键回收
            for (int i = 0; i < ledger.ActiveEffects.Count; i++) {
                long id = ledger.ActiveEffects[i].ActivationId;
                cardFlyIn.TryGetValue(id, out float t);
                cardFlyIn[id] = Math.Min(t + 1f / 12f, 1f);
            }
            PruneKeys(ledger);
            //横幅滑入键随横幅清空整批回收（键 = 攻击方+请求号，条目走 TTL 自清）
            if (ledger.IncomingUploads.Count == 0 && bannerSlide.Count > 0) {
                bannerSlide.Clear();
            }

            //碎裂请求消费：按最后已知卡位炸 UI 碎片
            if (shatterRequests.Count > 0) {
                foreach (long id in shatterRequests) {
                    PlayerHackHudFeed.SpawnDebris(LastCardCenter(id),
                        PvPTheme.Hostile, 18);
                }
                shatterRequests.Clear();
            }
        }

        private static readonly Dictionary<long, Vector2> lastCardCenters = [];

        private static Vector2 LastCardCenter(long activationId)
            => lastCardCenters.TryGetValue(activationId, out Vector2 pos)
                ? pos : new Vector2(HackTheme.UIScreenW * 0.5f, EffectRowY + CardH * 0.5f);

        private static void PruneKeys(PlayerHackLedger ledger) {
            if (cardFlyIn.Count == 0) return;
            List<long> stale = null;
            foreach (long id in cardFlyIn.Keys) {
                if (ledger.FindEffect(id) == null) (stale ??= []).Add(id);
            }
            if (stale == null) return;
            for (int i = 0; i < stale.Count; i++) {
                cardFlyIn.Remove(stale[i]);
                lastCardCenters.Remove(stale[i]);
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Player player = Main.LocalPlayer;
            var ledger = player.GetModPlayer<PlayerHackLedger>();
            hoveredEffect = null;

            float effectRowBottom = DrawEffectRow(spriteBatch, ledger);
            DrawBanners(spriteBatch, ledger, effectRowBottom);
            DrawImplantPanel(spriteBatch);
            DrawAlerts(spriteBatch, effectRowBottom);
            DrawEdgeFlash(spriteBatch);
            DrawDebris(spriteBatch);

            //协议自绘的防守方覆盖层（读数污染等），按帐本条目分发
            for (int i = 0; i < ledger.ActiveEffects.Count; i++) {
                PlayerHackEffect effect = ledger.ActiveEffects[i];
                effect.Hack?.DrawDefenderOverlay(spriteBatch, player, effect);
            }

            //单 overlay 律：悬停说明最后画、全边裁剪
            if (hoveredEffect != null) {
                DrawEffectTooltip(spriteBatch, hoveredEffect);
                player.mouseInterface = true;
            }
        }

        #region ③ 已生效协议列表（屏幕正上方，本机帐本真值）

        private float DrawEffectRow(SpriteBatch sb, PlayerHackLedger ledger) {
            int count = ledger.ActiveEffects.Count;
            if (count == 0) return EffectRowY;

            int visible = Math.Min(count, MaxVisibleCards);
            float totalW = visible * CardW + (visible - 1) * CardGap
                + (count > visible ? 34f : 0f);
            float x = (HackTheme.UIScreenW - totalW) * 0.5f;
            //UIHandle 层的 Main.mouseX/Y 已是 UI 空间（口径同 HackPanelRenderer.ContainsMouse）
            float mouseX = Main.mouseX;
            float mouseY = Main.mouseY;

            for (int i = 0; i < visible; i++) {
                PlayerHackEffect effect = ledger.ActiveEffects[i];
                cardFlyIn.TryGetValue(effect.ActivationId, out float flyIn);
                float ease = HackTheme.EaseOutCubic(flyIn);
                //新条目从横幅位置飞入落位
                float y = MathHelper.Lerp(EffectRowY + 52f, EffectRowY, ease);
                var rect = new Rectangle((int)x, (int)y, (int)CardW, (int)CardH);
                lastCardCenters[effect.ActivationId] = rect.Center.ToVector2();
                DrawEffectCard(sb, rect, effect, ease);

                if (rect.Contains((int)mouseX, (int)mouseY)) {
                    hoveredEffect = effect;
                }
                x += CardW + CardGap;
            }
            if (count > visible) {
                HackTheme.DrawRawText(sb, $"+{count - visible}",
                    new Vector2(x + 4f, EffectRowY + 12f), PvPTheme.Hostile, 0.7f);
            }
            return EffectRowY + CardH;
        }

        private static void DrawEffectCard(SpriteBatch sb, Rectangle rect,
            PlayerHackEffect effect, float alphaEase) {
            Texture2D pixel = HackTheme.Pixel;
            if (pixel == null) return;
            float alpha = 0.35f + 0.65f * alphaEase;

            //实底 + 1px 敌对边框（不做假投影）
            sb.Draw(pixel, rect, HackTheme.SrcPixel, HackTheme.BgPanel * (0.92f * alpha));
            DrawBorder(sb, pixel, rect, PvPTheme.HostileBorder * alpha);
            //顶缘 1px 亮线（受光，不是羽化）
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1),
                HackTheme.SrcPixel, PvPTheme.HostileGlow * (0.55f * alpha));

            //左侧 28px：协议芯片纹图标（HackChipGlyph 管线，无芯片协议走自带 die 或 fallback）
            var glyphCenter = new Vector2(rect.X + 16f, rect.Y + rect.Height * 0.5f);
            HackChipGlyph.Draw(sb, effect.Hack?.GetType().Name, glyphCenter, 9.5f,
                alpha, PvPTheme.Hostile, 0f, Main.GameUpdateCount * 0.02f);

            //右侧：协议名 + 施加者名
            string name = effect.Hack?.DisplayName.Value ?? "?";
            HackTheme.DrawRawText(sb, name,
                new Vector2(rect.X + 32f, rect.Y + 4f), PvPTheme.HostileAlt * alpha, 0.62f);
            string caster = string.IsNullOrEmpty(effect.CasterName)
                ? PvPHudText.SignalLost.Value
                : PvPHudText.CasterFormat.Format(effect.CasterName);
            HackTheme.DrawRawText(sb, caster,
                new Vector2(rect.X + 32f, rect.Y + 20f),
                new Color(140, 46, 52) * alpha, 0.45f);

            //底部 2px 剩余时长条（红，随时间缩短）
            var barBg = new Rectangle(rect.X + 2, rect.Bottom - 4, rect.Width - 4, 2);
            sb.Draw(pixel, barBg, HackTheme.SrcPixel, HackTheme.ProgressBg * alpha);
            int fill = (int)(barBg.Width * effect.RemainingRatio);
            if (fill > 0) {
                sb.Draw(pixel, new Rectangle(barBg.X, barBg.Y, fill, 2),
                    HackTheme.SrcPixel, PvPTheme.Hostile * alpha);
            }
        }

        #endregion

        #region ① 被骇横幅（DefenderNotice 数据源）

        private static void DrawBanners(SpriteBatch sb, PlayerHackLedger ledger,
            float effectRowBottom) {
            IReadOnlyList<PlayerHackNotice> notices = ledger.IncomingUploads;
            if (notices.Count == 0) return;
            Texture2D pixel = HackTheme.Pixel;
            if (pixel == null) return;

            float y = ledger.ActiveEffects.Count > 0 ? effectRowBottom + 8f : 14f;
            int drawn = 0;
            var font = FontAssets.MouseText.Value;

            for (int i = 0; i < notices.Count && drawn < MaxVisibleBanners; i++) {
                PlayerHackNotice notice = notices[i];
                ulong key = ((ulong)(uint)notice.AttackerIndex << 32) | notice.RequestId;
                bannerSlide.TryGetValue(key, out float slide);
                slide = Math.Min(slide + 1f / 8f, 1f);
                bannerSlide[key] = slide;
                float ease = HackTheme.EaseOutCubic(slide);

                string text = PvPHudText.BeingHackedBy.Format(notice.AttackerName);
                float textW = font.MeasureString(text).X * 0.85f;
                float w = Math.Max(360f, textW + 120f);
                const float h = 34f;
                //入场故障滑入：轻微 x 抖动帧
                float jitter = slide < 0.6f && Main.GameUpdateCount % 3 == 0
                    ? Main.rand.NextFloat(-2f, 2f) : 0f;
                float x = (HackTheme.UIScreenW - w) * 0.5f + jitter;
                var rect = new Rectangle((int)x, (int)(y - (1f - ease) * 18f),
                    (int)w, (int)h);
                float alpha = ease * (notice.Terminal
                    ? MathHelper.Clamp(notice.Ttl / 30f, 0f, 1f) : 1f);

                DrawBannerBody(sb, pixel, font, rect, notice, text, alpha);
                y += h + 4f;
                drawn++;
            }
            if (notices.Count > drawn) {
                HackTheme.DrawRawText(sb,
                    PvPHudText.MoreIntrusions.Format(notices.Count - drawn),
                    new Vector2(HackTheme.UIScreenW * 0.5f - 40f, y),
                    PvPTheme.Hostile, 0.6f);
            }
        }

        private static void DrawBannerBody(SpriteBatch sb, Texture2D pixel,
            DynamicSpriteFont font, Rectangle rect, PlayerHackNotice notice,
            string text, float alpha) {
            //实底 BgPanel + 1px 敌对边框；驻留期 CRT 扫描线微闪
            sb.Draw(pixel, rect, HackTheme.SrcPixel, HackTheme.BgPanel * (0.94f * alpha));
            DrawBorder(sb, pixel, rect, PvPTheme.HostileBorder * alpha);
            HackTheme.DrawCRTOverlay(sb, rect, 0.16f * alpha);

            //落地白闪 → 翻红
            if (notice.State == 3 && PlayerHackHudFeed.HandoffFlashFrames > 6) {
                sb.Draw(pixel, rect, HackTheme.SrcPixel, Color.White * (0.5f * alpha));
            }

            //主文案（恒定敌对红族）
            HackTheme.DrawRawText(sb, text,
                new Vector2(rect.X + 14f, rect.Y + 5f), PvPTheme.HostileAlt * alpha, 0.85f);

            //协议名不显示：条目位画 6 个乱码故障字形滚动（信息不对称是博弈的一部分）
            string garbage = BuildGarbageGlyphs(notice.RequestId);
            HackTheme.DrawRawText(sb, garbage,
                new Vector2(rect.Right - 84f, rect.Y + 8f),
                PvPTheme.Hostile * (0.75f * alpha), 0.6f);

            //底部 4px 进度条：琥珀填充 + 15f 间隔本机补间；终止态按状态换色
            var barBg = new Rectangle(rect.X + 2, rect.Bottom - 6, rect.Width - 4, 4);
            sb.Draw(pixel, barBg, HackTheme.SrcPixel, HackTheme.ProgressBg * alpha);
            float progress = notice.DisplayProgress;
            Color fillColor = notice.State switch {
                3 => PvPTheme.Hostile,
                1 or 2 => HackTheme.TextDim,
                _ => PvPTheme.Amber,
            };
            int fill = (int)(barBg.Width * progress);
            if (fill > 0) {
                sb.Draw(pixel, new Rectangle(barBg.X, barBg.Y, fill, 4),
                    HackTheme.SrcPixel, fillColor * alpha);
                //推进沿的亮头
                sb.Draw(pixel, new Rectangle(barBg.X + fill - 2, barBg.Y, 2, 4),
                    HackTheme.SrcPixel,
                    Color.Lerp(fillColor, Color.White, 0.6f) * alpha);
            }

            //终止态标签
            if (notice.Terminal) {
                string tag = notice.State == 3
                    ? PvPHudText.UploadLanded.Value
                    : PvPHudText.UploadCanceled.Value;
                HackTheme.DrawRawText(sb, tag,
                    new Vector2(rect.X + 14f, rect.Bottom - 20f),
                    (notice.State == 3 ? PvPTheme.HostileGlow : HackTheme.TextDim)
                        * alpha, 0.55f);
            }
        }

        /// <summary>6 个乱码故障字形，按时间片滚动（协议名的遮蔽物）</summary>
        private static string BuildGarbageGlyphs(uint seed) {
            const string glyphs = "▓▒░◧◨◩◪@#$%&";
            Span<char> chars = stackalloc char[6];
            ulong t = Main.GameUpdateCount / 6;
            for (int i = 0; i < 6; i++) {
                ulong h = (seed * 2654435761UL + (ulong)i * 40503UL + t * 69069UL);
                chars[i] = glyphs[(int)(h % (ulong)glyphs.Length)];
            }
            return new string(chars);
        }

        #endregion

        #region ④ 攻击方植入物面板（右上小地图下方，PlayerEffectState 镜像数据源）

        private static void DrawImplantPanel(SpriteBatch sb) {
            PlayerHackMirror.CollectOwnImplants(ownImplantCache);
            if (ownImplantCache.Count == 0) return;
            Texture2D pixel = HackTheme.Pixel;
            if (pixel == null) return;

            const float cardW = 88f;
            const float cardH = 26f;
            float x = HackTheme.UIScreenW - HackTheme.SideMargin - cardW;
            float y = 272f;

            HackTheme.DrawRawText(sb, PvPHudText.ImplantPanelTitle.Value,
                new Vector2(x, y - 16f), HackTheme.TextDim, 0.55f);

            for (int i = 0; i < ownImplantCache.Count && i < 6; i++) {
                PlayerHackMirror.MirrorEffect fx = ownImplantCache[i];
                var rect = new Rectangle((int)x, (int)y, (int)cardW, (int)cardH);
                bool removed = fx.RemovedReason != null;
                float alpha = removed
                    ? MathHelper.Clamp(fx.RemoveFxFrames / 40f, 0f, 1f) : 1f;

                sb.Draw(pixel, rect, HackTheme.SrcPixel,
                    HackTheme.BgPanel * (0.9f * alpha));
                DrawBorder(sb, pixel, rect,
                    (removed ? HackTheme.TextDim : PvPTheme.HostileBorder) * alpha);

                //左侧协议芯片纹（缩小版）
                QuickHackDef hack = QuickHackDef.GetByIndex(fx.SlotIndex);
                HackChipGlyph.Draw(sb, hack?.GetType().Name,
                    new Vector2(rect.X + 12f, rect.Y + cardH * 0.5f), 6.5f,
                    alpha, PvPTheme.Hostile, 0f, Main.GameUpdateCount * 0.02f);

                //目标名缩写
                Player defender = fx.DefenderIndex >= 0
                    && fx.DefenderIndex < Main.maxPlayers
                    ? Main.player[fx.DefenderIndex] : null;
                string name = defender?.name ?? "?";
                if (name.Length > 6) name = name[..6];
                HackTheme.DrawRawText(sb, name,
                    new Vector2(rect.X + 24f, rect.Y + 3f),
                    HackTheme.TextBright * alpha, 0.52f);

                if (removed) {
                    //爆裂标签：被卸载红 / 到期灰（两种退场区分明确）
                    bool uninstalled = fx.RemovedReason
                        == PlayerHackRemoveReason.Uninstalled;
                    HackTheme.DrawRawText(sb,
                        uninstalled ? PvPHudText.ImplantUninstalled.Value
                            : PvPHudText.ImplantExpired.Value,
                        new Vector2(rect.X + 24f, rect.Y + 13f),
                        (uninstalled ? PvPTheme.Hostile : HackTheme.TextDim) * alpha,
                        0.45f);
                    if (uninstalled && fx.RemoveFxFrames == 39) {
                        PlayerHackHudFeed.SpawnDebris(rect.Center.ToVector2(),
                            PvPTheme.Hostile, 10);
                    }
                }
                else {
                    //剩余时长条（影子时钟 60f 刷新 + 本机补间，读秒粒度足够）
                    var bar = new Rectangle(rect.X + 24, rect.Bottom - 6,
                        rect.Width - 30, 2);
                    sb.Draw(pixel, bar, HackTheme.SrcPixel, HackTheme.ProgressBg);
                    int fill = (int)(bar.Width * fx.RemainingRatio);
                    if (fill > 0) {
                        sb.Draw(pixel, new Rectangle(bar.X, bar.Y, fill, 2),
                            HackTheme.SrcPixel, PvPTheme.Hostile * alpha);
                    }
                }
                y += cardH + 4f;
            }
        }

        #endregion

        #region 警报 / 边缘红闪 / UI 碎片 / 悬停说明

        private static void DrawAlerts(SpriteBatch sb, float effectRowBottom) {
            if (PlayerHackHudFeed.Alerts.Count == 0) return;
            float y = effectRowBottom + 64f;
            var font = FontAssets.MouseText.Value;
            for (int i = 0; i < PlayerHackHudFeed.Alerts.Count; i++) {
                PlayerHackHudFeed.AlertLabel alert = PlayerHackHudFeed.Alerts[i];
                float alpha = MathHelper.Clamp(alert.FramesLeft / 30f, 0f, 1f);
                //前 12f 快闪（警报感）
                if (alert.MaxFrames - alert.FramesLeft < 12
                    && Main.GameUpdateCount % 4 < 2) {
                    alpha *= 0.35f;
                }
                float w = font.MeasureString(alert.Text).X * 0.7f;
                HackTheme.DrawRawText(sb, alert.Text,
                    new Vector2((HackTheme.UIScreenW - w) * 0.5f, y),
                    alert.Color * alpha, 0.7f);
                y += 20f;
            }
        }

        private static void DrawEdgeFlash(SpriteBatch sb) {
            if (PlayerHackHudFeed.EdgeFlashFrames <= 0) return;
            Texture2D pixel = HackTheme.Pixel;
            if (pixel == null) return;
            float alpha = PlayerHackHudFeed.EdgeFlashFrames / 24f * 0.4f;
            int w = (int)HackTheme.UIScreenW;
            int h = (int)HackTheme.UIScreenH;
            const int t = 4;
            Color c = PvPTheme.Hostile * alpha;
            sb.Draw(pixel, new Rectangle(0, 0, w, t), HackTheme.SrcPixel, c);
            sb.Draw(pixel, new Rectangle(0, h - t, w, t), HackTheme.SrcPixel, c);
            sb.Draw(pixel, new Rectangle(0, 0, t, h), HackTheme.SrcPixel, c);
            sb.Draw(pixel, new Rectangle(w - t, 0, t, h), HackTheme.SrcPixel, c);
        }

        private static void DrawDebris(SpriteBatch sb) {
            Texture2D pixel = HackTheme.Pixel;
            if (pixel == null) return;
            for (int i = 0; i < PlayerHackHudFeed.Debris.Count; i++) {
                PlayerHackHudFeed.UiDebris d = PlayerHackHudFeed.Debris[i];
                float alpha = 1f - d.Life / (float)d.MaxLife;
                sb.Draw(pixel, d.Pos, HackTheme.SrcPixel, d.Color * alpha, 0.6f,
                    new Vector2(0.5f), d.Size, SpriteEffects.None, 0f);
            }
        }

        private static void DrawEffectTooltip(SpriteBatch sb, PlayerHackEffect effect) {
            Texture2D pixel = HackTheme.Pixel;
            if (pixel == null || effect.Hack == null) return;
            var font = FontAssets.MouseText.Value;

            string title = effect.Hack.DisplayName.Value;
            string desc = effect.Hack.Description.Value;
            string caster = PvPHudText.CasterFormat.Format(
                string.IsNullOrEmpty(effect.CasterName)
                    ? PvPHudText.SignalLost.Value : effect.CasterName);
            string remain = PvPHudText.RemainSecondsFormat.Format(
                (effect.RemainingFrames + 59) / 60);

            //按内容实测定宽高（面板尺寸随文本走，不押常数）
            float w = Math.Max(Math.Max(font.MeasureString(title).X * 0.72f,
                font.MeasureString(desc).X * 0.58f),
                font.MeasureString(caster).X * 0.52f + 60f) + 20f;
            const float h = 64f;
            float x = Main.mouseX + 14f;
            float y = Main.mouseY + 14f;
            //四边裁剪
            x = Math.Min(x, HackTheme.UIScreenW - w - 6f);
            y = Math.Min(y, HackTheme.UIScreenH - h - 6f);

            var rect = new Rectangle((int)x, (int)y, (int)w, (int)h);
            sb.Draw(pixel, rect, HackTheme.SrcPixel, HackTheme.BgPanel * 0.96f);
            DrawBorder(sb, pixel, rect, PvPTheme.HostileBorder);
            HackTheme.DrawRawText(sb, title, new Vector2(x + 10f, y + 5f),
                PvPTheme.HostileAlt, 0.72f);
            HackTheme.DrawRawText(sb, desc, new Vector2(x + 10f, y + 24f),
                HackTheme.TextNormal, 0.58f);
            HackTheme.DrawRawText(sb, caster, new Vector2(x + 10f, y + 44f),
                new Color(140, 46, 52), 0.52f);
            float remainW = font.MeasureString(remain).X * 0.52f;
            HackTheme.DrawRawText(sb, remain,
                new Vector2(rect.Right - remainW - 10f, y + 44f),
                PvPTheme.Hostile, 0.52f);
        }

        private static void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect,
            Color color) {
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1),
                HackTheme.SrcPixel, color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1),
                HackTheme.SrcPixel, color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height),
                HackTheme.SrcPixel, color);
            sb.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height),
                HackTheme.SrcPixel, color);
        }

        #endregion

        public override void UnLoad() {
            cardFlyIn.Clear();
            bannerSlide.Clear();
            lastCardCenters.Clear();
            shatterRequests.Clear();
            ownImplantCache.Clear();
            PlayerHackHudFeed.Reset();
        }
    }
}
