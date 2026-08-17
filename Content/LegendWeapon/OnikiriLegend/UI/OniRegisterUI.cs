using CalamityOverhaul.Common;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI
{
    /// <summary>
    /// 点鬼簿全屏,卷轴名录+影绘细节板。<br/>
    /// 只读——结印仍只在结印盘上做,此处不装鬼。
    /// 与结印盘是"抽卷/归卷"的关系而非姊妹屏:展卷静默收盘,合卷静默回盘
    /// </summary>
    internal sealed class OniRegisterUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.OnikiriText";
        public static OniRegisterUI Instance => UIHandleLoader.GetUIHandleOfType<OniRegisterUI>();

        private const string FreezeReason = "OniRegister";

        #region 本地化
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText StatusFormat { get; private set; }
        public static LocalizedText RevivalFormat { get; private set; }
        public static LocalizedText AbilityCostFormat { get; private set; }
        public static LocalizedText OriginLabel { get; private set; }
        public static LocalizedText PowerLabel { get; private set; }
        public static LocalizedText StateReady { get; private set; }
        public static LocalizedText StateDanger { get; private set; }
        public static LocalizedText StateArchive { get; private set; }
        public static LocalizedText UsableSection { get; private set; }
        public static LocalizedText BrowseHint { get; private set; }
        public static LocalizedText CloseTagText { get; private set; }
        public static LocalizedText CloseHintFormat { get; private set; }
        public static LocalizedText EquippedActive { get; private set; }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "点 鬼 簿");
            StatusFormat = this.GetLocalization(nameof(StatusFormat), () => "在盘 {0} 只 · 侵蚀 {1}%");
            RevivalFormat = this.GetLocalization(nameof(RevivalFormat), () => "复苏 {0}%");
            AbilityCostFormat = this.GetLocalization(nameof(AbilityCostFormat), () => "每次役使 复苏 +{0}% · 侵蚀 +{1}%");
            OriginLabel = this.GetLocalization(nameof(OriginLabel), () => "来历");
            PowerLabel = this.GetLocalization(nameof(PowerLabel), () => "赋力");
            StateReady = this.GetLocalization(nameof(StateReady), () => "可役使");
            StateDanger = this.GetLocalization(nameof(StateDanger), () => "将醒 · 慎役");
            StateArchive = this.GetLocalization(nameof(StateArchive), () => "残卷 · 不可役使");
            UsableSection = this.GetLocalization(nameof(UsableSection), () => "役 鬼");
            BrowseHint = this.GetLocalization(nameof(BrowseHint),
                () => "点名录中的一只查看来历与赋力。此卷只载其事，结印在盘上做");
            CloseTagText = this.GetLocalization(nameof(CloseTagText), () => "合 卷");
            CloseHintFormat = this.GetLocalization(nameof(CloseHintFormat), () => "ESC · {0} · 点击卷外 归盘");
            EquippedActive = this.GetLocalization(nameof(EquippedActive), () => "结印中");
        }
        #endregion

        public override bool CloseOnEscape => true;
        public override float RenderPriority => 2f;
        public override SoundStyle? OpenSound => SoundID.MenuOpen with { Pitch = -0.45f, Volume = 0.55f };
        public override SoundStyle? CloseSound => SilentSwap ? null
            : SoundID.MenuClose with { Pitch = -0.3f, Volume = 0.5f };
        public override Vector2 MousePosition => OnikiriUITheme.UIMouse;

        /// <summary>互斥收卷时置位:抑制本屏关闭音,切换只响新屏开音一声</summary>
        internal bool SilentSwap;
        /// <summary>自结印盘抽卷进来:合卷时静默摆回盘上</summary>
        private bool returnToSigil;

        //====交互状态====
        private int selectedIndex = -1;
        private int hoverIndex = -1;
        private readonly float[] hoverEase = new float[16];
        private float selectEase;
        private Rectangle scrollRect;
        private Rectangle detailRect;
        private float usableSectionY;
        //合卷木牌,点击归盘,牌绳 Verlet
        private Rectangle closeTagRect;
        private float closeTagHover;
        private bool closeTagWasHovered;
        private Vector2 closeTagAnchor;
        private readonly OniRope closeTagRope = new(5, 22f);
        private readonly Rectangle[] entryRects = new Rectangle[16];

        //====动画状态====
        internal float SwayTimer;
        internal float ShaderTime;
        private readonly OniUIParticlePool particles = new(140);
        private int petalTimer;
        private int ashTimer;
        private int ambienceTimer = 600;

        //====细节板打字机====
        private float typeTimer;
        private int lastDetailChars = -1;
        private float detailInkAge = 60f;

        //====低频异象====
        private Vector2 lastMouse;
        private int idleTimer;
        private float glanceStrength;       //0~1,眼神转向光标的插值
        private int pupilCooldown = 1500;   //绯月竖瞳冷却
        private float pupilOpen;            //0~1

        public override void OnEnterWorld() {
            if (IsOpen) {
                Close();
            }
            SnapOpenProgress();
        }

        /// <summary>自结印盘抽卷:展卷并记得合卷后要摆回盘上</summary>
        internal static void OpenFromSigil() {
            OniRegisterUI register = Instance;
            if (register == null || register.IsOpen) {
                return;
            }
            register.returnToSigil = true;
            register.Open();
        }

        protected override void OnOpen() {
            Main.playerInventory = false;
            //抽卷即收盘:静默收,切换只响展卷这一声
            if (OniSigilUI.Instance?.IsOpen ?? false) {
                OniSigilUI.Instance.SilentSwap = true;
                OniSigilUI.Instance.Close();
                OniSigilUI.Instance.SilentSwap = false;
            }
            SwayTimer = 0f;
            petalTimer = 0;
            particles.Clear();
            selectedIndex = -1;
            int equippedIndex = FindFirstEquipped();
            if (equippedIndex >= 0) {
                SelectEntry(equippedIndex, silent: true);
            }
            idleTimer = 0;
            glanceStrength = 0f;
            pupilOpen = 0f;
            pupilCooldown = Main.rand.Next(900, 1800);
            LayoutCompute();
            closeTagRope.WarmStart(closeTagAnchor);
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Activate(FreezeReason);
            }
        }

        protected override void OnClose() {
            bool back = returnToSigil;
            returnToSigil = false;
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Deactivate(FreezeReason);
            }
            //合卷归盘:卷是从盘上抽出来的,放回去才算完
            if (back && !SilentSwap && HoldingOnikiri() && OniSigilUI.Instance is { IsOpen: false }) {
                OniSigilUI.Instance.Open();
            }
        }

        private static bool HoldingOnikiri() {
            Item item = Main.LocalPlayer?.HeldItem;
            return item != null && item.Alives() && item.type == OnikiriOverride.ID;
        }

        /// <summary>开簿时把浏览焦点落在盘上第一只，纯预览，不产生任何写入</summary>
        private static int FindFirstEquipped() {
            var entries = OniRegistry.Entries;
            for (int slot = 0; slot < OniRegistry.SlotCount; slot++) {
                string key = OniRegistry.SlotKey(slot);
                if (string.IsNullOrEmpty(key)) {
                    continue;
                }
                for (int i = 0; i < entries.Count; i++) {
                    if (entries[i].Key == key) {
                        return i;
                    }
                }
            }
            return -1;
        }

        private void SelectEntry(int index, bool silent = false) {
            var entries = OniRegistry.Entries;
            if (index < 0 || index >= entries.Count) {
                return;
            }
            if (selectedIndex != index || lastDetailChars < 0) {
                selectedIndex = index;
                selectEase = 0f;
                typeTimer = 0f;
                lastDetailChars = -1;
                detailInkAge = 60f;
                if (!silent) {
                    SoundEngine.PlaySound(CWRSound.ButtonZero with { Volume = 0.5f });
                }
            }
        }

        internal OniGhostEntry SelectedEntry {
            get {
                var entries = OniRegistry.Entries;
                return selectedIndex >= 0 && selectedIndex < entries.Count ? entries[selectedIndex] : null;
            }
        }

        /// <summary>该鬼此刻在不在结印盘上（图鉴只读这一位，不改它）</summary>
        internal static bool IsEquipped(OniGhostEntry entry)
            => entry != null && OniRegistry.IsEquipped(entry.Key);

        /// <summary>图鉴只读：手上还是鬼切就行，不需要装备请求那套实例会话</summary>
        private bool MaintainHold() {
            if (!HoldingOnikiri() || Main.LocalPlayer?.dead != false) {
                Close();
                return false;
            }
            return true;
        }

        public override void Update() {
            if (IsOpen) {
                if (!MaintainHold()) {
                    return;
                }
                player.mouseInterface = true;
            }
        }

        public override void LogicUpdate() {
            if (IsOpen) {
                if (!MaintainHold()) {
                    return;
                }
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
                if (!player.active || player.dead) {
                    Close();
                }
            }

            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }

            SwayTimer += 0.022f;
            if (SwayTimer > MathHelper.TwoPi) {
                SwayTimer -= MathHelper.TwoPi;
            }
            ShaderTime += 1f / 60f;
            particles.Update();
            detailInkAge = Math.Min(detailInkAge + 1f, 60f);
            selectEase = MathHelper.Clamp(selectEase + 0.07f, 0f, 1f);

            LayoutCompute();

            //合卷牌摆:绳受风,牌是末端配重
            closeTagRope.Update(closeTagAnchor, null, ShaderTime, 0.26f, endWeight: 0.55f);
            Vector2 tagTop = closeTagRope.End;
            closeTagRect = new Rectangle((int)(tagTop.X - 16f), (int)tagTop.Y - 2, 32, 48);

            UpdateInteraction(a);
            UpdateAmbient(a);
            UpdateAnomalies();
            UpdateDetailTypewriter();
        }

        private void LayoutCompute() {
            float sw = OnikiriUITheme.UIScreenW;
            float sh = OnikiriUITheme.UIScreenH;
            var entries = OniRegistry.Entries;

            float scrollW = Math.Min(OnikiriUITheme.ScrollMaxWidth, sw * OnikiriUITheme.ScrollWidthRatio);
            scrollW = Math.Max(scrollW, Math.Min(340f, sw * 0.5f));
            float heightEase = MathHelper.Clamp((sh - 480f) / 420f, 0f, 1f);
            float scrollRatio = MathHelper.Lerp(0.94f, 0.84f, heightEase);
            float scrollH = Math.Min(sh - 28f, sh * scrollRatio);
            Vector2 scrollCenter = new(sw * OnikiriUITheme.ScrollCenterXRatio, sh * 0.5f + 6f);
            scrollRect = new Rectangle((int)(scrollCenter.X - scrollW * 0.5f), (int)(scrollCenter.Y - scrollH * 0.5f), (int)scrollW, (int)scrollH);

            float detailX = scrollRect.Right + 38f;
            float detailW = Math.Min(430f, sw - detailX - 44f);
            float detailH = Math.Min(520f, sh - 92f);
            detailRect = new Rectangle((int)detailX, (int)(sh * 0.5f - detailH * 0.5f), (int)detailW, (int)detailH);

            //合卷木牌绳锚:顶部轴杆右端帽,牌体位置由 Verlet 绳每帧决定
            closeTagAnchor = new Vector2(scrollRect.Right + 17f, scrollRect.Y - 7f);

            Array.Clear(entryRects, 0, entryRects.Length);
            Rectangle inner = scrollRect;
            inner.Inflate(-30, -36);
            int usableCount = 0;
            foreach (OniGhostEntry entry in entries) {
                if (entry.CanEquip) {
                    usableCount++;
                }
            }

            //残卷区已删，六成品单排占满纸面
            float mainTotalW = usableCount * OnikiriUITheme.EntryColumnW
                + Math.Max(0, usableCount - 1) * OnikiriUITheme.EntryColumnGap;
            float mainX = inner.Center.X + mainTotalW * 0.5f - OnikiriUITheme.EntryColumnW;
            int mainGap = (int)MathF.Round(MathHelper.Lerp(32f, 42f, heightEase));
            int mainMaxH = (int)MathF.Round(MathHelper.Lerp(200f, 288f, heightEase));
            const int bottomReserve = 32;
            //役鬼位横条已随装备职能一并撤走，名录直接接在题头刀痕之下
            int mainTop = scrollRect.Y + 86 + mainGap;
            int mainH = Math.Max(96, Math.Min(mainMaxH, inner.Bottom - mainTop - bottomReserve));
            usableSectionY = mainTop - 27f;

            int usableIndex = 0;
            for (int i = 0; i < entries.Count && i < entryRects.Length; i++) {
                if (!entries[i].CanEquip) {
                    continue;
                }
                float x = mainX - usableIndex * (OnikiriUITheme.EntryColumnW + OnikiriUITheme.EntryColumnGap);
                entryRects[i] = new Rectangle((int)x, mainTop, (int)OnikiriUITheme.EntryColumnW, mainH);
                usableIndex++;
            }
        }

        private void UpdateInteraction(float a) {
            bool inputAvailable = IsOpen && a > 0.9f && !OniLedgerSwapFX.Running;
            Vector2 mouse = MousePosition;
            Point mp = mouse.ToPoint();
            var entries = OniRegistry.Entries;

            int newHover = -1;
            if (inputAvailable) {
                for (int i = 0; i < entries.Count && i < entryRects.Length; i++) {
                    if (entryRects[i].Contains(mp)) {
                        newHover = i;
                        break;
                    }
                }
            }
            if (newHover != hoverIndex) {
                hoverIndex = newHover;
                if (hoverIndex >= 0) {
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.25f, Volume = 0.3f });
                }
            }
            for (int i = 0; i < entries.Count && i < hoverEase.Length; i++) {
                float target = i == hoverIndex ? 1f : 0f;
                hoverEase[i] += (target - hoverEase[i]) * (target > hoverEase[i] ? 0.22f : 0.12f);
            }

            //合卷牌 hover 缓动;拂过时给绳一记横向冲量,像被手碰了一下
            bool tagHovered = inputAvailable && closeTagRect.Contains(mp);
            closeTagHover += ((tagHovered ? 1f : 0f) - closeTagHover) * 0.2f;
            if (tagHovered && !closeTagWasHovered) {
                closeTagRope.Nudge(Main.rand.NextFloat(0.8f, 1.5f) * (Main.rand.NextBool() ? 1f : -1f));
            }
            closeTagWasHovered = tagHovered;

            if (inputAvailable && keyLeftPressState == KeyPressState.Pressed) {
                if (tagHovered) {
                    Close();
                    return;
                }
                if (hoverIndex >= 0) {
                    SelectEntry(hoverIndex);
                    return;
                }
                //点击卷外压暗区合卷:卷轴(含外扩边)、细节板、合卷牌之外都算"外"
                Rectangle scrollHit = scrollRect;
                scrollHit.Inflate(OnikiriUITheme.ScrollEdgePad + 22, OnikiriUITheme.ScrollEdgePad + 22);
                if (!scrollHit.Contains(mp) && !detailRect.Contains(mp)) {
                    Close();
                }
            }
        }

        private void UpdateAmbient(float a) {
            if (!IsOpen || a < 0.6f) {
                return;
            }
            //落花只在卷轴两翼窄条飘落,不进纸面正文
            petalTimer++;
            if (petalTimer >= 42) {
                petalTimer = 0;
                bool left = Main.rand.NextBool();
                float x = left
                    ? Main.rand.NextFloat(scrollRect.X - 16f, scrollRect.X + 12f)
                    : Main.rand.NextFloat(scrollRect.Right - 12f, scrollRect.Right + 16f);
                particles.SpawnPetal(new Vector2(x, scrollRect.Y - 8f), left ? -1f : 1f);
            }

            //线香落灰
            OniGhostEntry sel = SelectedEntry;
            if (sel?.CanEquip == true) {
                ashTimer++;
                if (ashTimer >= 34) {
                    ashTimer = 0;
                    particles.SpawnAsh(IncenseEmberPos());
                }
            }

            //偶发远处风铃,簿开着的时候夜里有风
            if (--ambienceTimer <= 0) {
                ambienceTimer = Main.rand.Next(760, 1300);
                SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.14f, Pitch = 0.3f, MaxInstances = 1 });
            }
        }

        /// <summary>线香燃点,燃去比=复苏进度——香烧到底,鬼就该醒了</summary>
        internal Vector2 IncenseEmberPos() {
            OniGhostEntry sel = SelectedEntry;
            float revival = MathHelper.Clamp(sel?.Revival ?? 0f, 0f, 1f);
            Rectangle rect = IncenseRect();
            return new Vector2(rect.Center.X, rect.Y + rect.Height * revival);
        }

        /// <summary>线香立杆矩形(细节板右下,文案区右缘让位于它)</summary>
        internal Rectangle IncenseRect() => new(detailRect.Right - 56, detailRect.Bottom - 188, 4, 84);

        private void UpdateAnomalies() {
            //闲置计时:光标位移超过阈值即重置
            Vector2 mouse = MousePosition;
            if (Vector2.DistanceSquared(mouse, lastMouse) > 5f) {
                idleTimer = 0;
            }
            lastMouse = mouse;

            if (!IsOpen) {
                glanceStrength = 0f;
                pupilOpen = 0f;
                return;
            }
            idleTimer++;

            //闲置约 10s 后,鬼眼缓缓转向光标凝视约 2.2s,随后收回并重新计时
            bool glancing = idleTimer > 600 && idleTimer < 600 + 132;
            glanceStrength += ((glancing ? 1f : 0f) - glanceStrength) * 0.045f;
            if (idleTimer >= 600 + 132 + 90) {
                idleTimer = 60; //不立即重看,留一段安静
            }

            //复苏将满的役鬼只以低频竖瞳示警
            if (OniRegistry.IsEquippedInDanger) {
                pupilCooldown--;
                bool pupilActive = pupilCooldown <= 0;
                if (pupilCooldown < -96) {
                    pupilCooldown = Main.rand.Next(1500, 2600);
                }
                pupilOpen += ((pupilActive ? 1f : 0f) - pupilOpen) * 0.09f;
            }
            else {
                pupilOpen *= 0.9f;
            }
        }

        private void UpdateDetailTypewriter() {
            OniGhostEntry sel = SelectedEntry;
            if (sel == null) {
                return;
            }
            typeTimer += 1f;
            int chars = (int)(typeTimer / 1.4f);
            if (chars != lastDetailChars) {
                lastDetailChars = chars;
                detailInkAge = 0f;
            }
        }

        /// <summary>细节板正文可见字符数</summary>
        internal int DetailVisibleChars => Math.Max(0, (int)(typeTimer / 1.4f));
        /// <summary>细节板湿墨强度 0~1</summary>
        internal float DetailInkStrength => 1f - MathHelper.Clamp(detailInkAge / 16f, 0f, 1f);
        /// <summary>鬼眼转向光标的强度 0~1</summary>
        internal float GlanceStrength => glanceStrength;
        /// <summary>绯月竖瞳开度 0~1</summary>
        internal float PupilOpen => pupilOpen;

        public override void Draw(SpriteBatch spriteBatch) {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            var entries = OniRegistry.Entries;

            //====压暗世界 + 绯月(远景视差:随光标轻微反向,层次感白拿)====
            Rectangle full = new(0, 0, (int)OnikiriUITheme.UIScreenW + 2, (int)OnikiriUITheme.UIScreenH + 2);
            spriteBatch.Draw(pixel, full, src, Color.Black * (a * 0.66f));
            Vector2 parallax = (OnikiriUITheme.UIMouse - OnikiriUITheme.UIScreenSize * 0.5f) * -0.016f;
            OniRegisterRenderer.DrawMoon(spriteBatch, new Vector2(OnikiriUITheme.UIScreenW * 0.84f, 118f) + parallax, a, ShaderTime, pupilOpen);

            //====顶梁(同一夜屋的持续骨架;图鉴不占工位,两块驿牌照原样立着)====
            OniLedgerBeam.Draw(spriteBatch, a, ShaderTime, OniLedgerView.Sigil, 0f);

            //====卷轴纸体(shader / CPU 降级) + 轴杆 + 挂件====
            float reveal = a;
            OniRegisterRenderer.DrawScroll(spriteBatch, scrollRect, a, reveal, ShaderTime);
            OniRegisterRenderer.DrawRollers(spriteBatch, scrollRect, a, reveal);
            float decoAlpha = MathHelper.Clamp((a - 0.55f) / 0.45f, 0f, 1f);
            if (decoAlpha > 0.01f) {
                OniBrush.DrawShide(spriteBatch, scrollRect, decoAlpha, SwayTimer);
            }

            //====簿题 + 分隔刀痕 + 状态行====
            float contentA = MathHelper.Clamp((a - 0.45f) / 0.55f, 0f, 1f);
            if (contentA > 0.01f) {
                DrawHeader(spriteBatch, font, contentA);
                DrawEntries(spriteBatch, font, contentA, entries);
                OniRegisterRenderer.DrawDetail(spriteBatch, this, detailRect, contentA);
                OniRegisterRenderer.DrawCloseTag(spriteBatch, font, closeTagRope, contentA,
                    closeTagHover, GlobalTimer, CloseTagText.Value);
                DrawCloseHint(spriteBatch, font, contentA);
            }

            //====两翼落花====
            particles.Draw(spriteBatch, a);
        }

        private void DrawHeader(SpriteBatch sb, DynamicSpriteFont font, float a) {
            string title = TitleText.Value;
            const float TitleScale = 1.08f;
            Vector2 tSize = font.MeasureString(title) * TitleScale;
            Vector2 tPos = new(scrollRect.Center.X - tSize.X * 0.5f, scrollRect.Y + 26f);
            //小朱印压在题左
            OniBrush.DrawSealGlyph(sb, tPos + new Vector2(-24f, tSize.Y * 0.5f), 13f, a * 0.95f);
            Utils.DrawBorderString(sb, title, tPos, OnikiriUITheme.HotWhite * a, TitleScale);
            //题下一笔刀痕
            OniBrush.DrawTaperedSlash(sb,
                new Vector2(scrollRect.X + 34f, tPos.Y + tSize.Y + 7f),
                new Vector2(scrollRect.Right - 34f, tPos.Y + tSize.Y + 5f), 2.2f, 1.8f, a * 0.9f);

            DrawSectionLabel(sb, font, UsableSection.Value, usableSectionY, a);

            int erosion = (int)MathF.Round(OniRegistry.Erosion * 100f);
            string status = string.Format(StatusFormat.Value, OniRegistry.EquippedCount, erosion);
            Vector2 sSize = font.MeasureString(status) * 0.7f;
            Utils.DrawBorderString(sb, status,
                new Vector2(scrollRect.Center.X - sSize.X * 0.5f, scrollRect.Bottom - 44f),
                OnikiriUITheme.TextDim * (a * 0.85f), 0.7f);
        }

        private void DrawEntries(SpriteBatch sb, DynamicSpriteFont font, float a, System.Collections.Generic.IReadOnlyList<OniGhostEntry> entries) {
            for (int i = 0; i < entries.Count && i < entryRects.Length; i++) {
                float hover = i < hoverEase.Length ? hoverEase[i] : 0f;
                bool selected = i == selectedIndex;
                OniRegisterRenderer.DrawEntryColumn(sb, font, entries[i], entryRects[i], a, hover,
                    selected, IsEquipped(entries[i]), selected ? selectEase : 0f, GlobalTimer, i);
            }
        }

        private void DrawSectionLabel(SpriteBatch sb, DynamicSpriteFont font, string text, float y, float a) {
            const float Scale = 0.65f;
            Vector2 size = font.MeasureString(text) * Scale;
            float x = scrollRect.Center.X - size.X * 0.5f;
            Utils.DrawBorderString(sb, text, new Vector2(x, y), OnikiriUITheme.Deep * a, Scale);
            float lineY = y + size.Y * 0.5f;
            OniBrush.DrawGradientLine(sb, new Vector2(scrollRect.Center.X - 118f, lineY),
                new Vector2(x - 10f, lineY), OnikiriUITheme.Deep * 0f,
                OnikiriUITheme.Deep * (a * 0.7f), 1f);
            OniBrush.DrawGradientLine(sb, new Vector2(x + size.X + 10f, lineY),
                new Vector2(scrollRect.Center.X + 118f, lineY), OnikiriUITheme.Deep * (a * 0.7f),
                OnikiriUITheme.Deep * 0f, 1f);
        }

        /// <summary>卷底常驻关闭提示:ESC/键位/点卷外</summary>
        private void DrawCloseHint(SpriteBatch sb, DynamicSpriteFont font, float a) {
            string keyName = CWRKeySystem.Legend_UIControl.ToTooltipString(CWRKeySystem.Notbound.Value);
            string hint = string.Format(CloseHintFormat.Value, keyName);
            Vector2 size = font.MeasureString(hint) * 0.62f;
            float y = Math.Min(scrollRect.Bottom + 14f, OnikiriUITheme.UIScreenH - 24f);
            Utils.DrawBorderString(sb, hint,
                new Vector2(scrollRect.Center.X - size.X * 0.5f, y),
                OnikiriUITheme.TextDim * (a * 0.6f), 0.62f);
        }
    }

    /// <summary>鬼切界面键位开关</summary>
    internal sealed class OniRegisterKeyPlayer : ModPlayer
    {
        public override void PostUpdate() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            Item item = Player.HeldItem;
            bool holding = item.Alives()
                && (item.type == ModContent.ItemType<OnikiriItem>());
            if (!holding) {
                return;
            }
            if (OniMeiUI.Instance?.Rite.Active ?? false) {
                return;
            }
            //换乘途中不接开关键,免得旧屏刚收又被键关/目的屏被抢开
            if (OniLedgerSwapFX.Running) {
                return;
            }
            if (CWRKeySystem.Legend_UIControl.JustPressed) {
                OniTalismanHud.ToggleRememberedLedger();
            }
        }
    }
}
