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
    /// <summary>点鬼簿全屏,卷轴名录+影绘细节板</summary>
    internal sealed class OniRegisterUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.OnikiriText";
        public static OniRegisterUI Instance => UIHandleLoader.GetUIHandleOfType<OniRegisterUI>();

        private const string FreezeReason = "OniRegister";

        #region 本地化
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText StatusFormat { get; private set; }
        public static LocalizedText MasteryFormat { get; private set; }
        public static LocalizedText OriginLabel { get; private set; }
        public static LocalizedText PowerLabel { get; private set; }
        public static LocalizedText StateEngraved { get; private set; }
        public static LocalizedText StateRestless { get; private set; }
        public static LocalizedText StateSealed { get; private set; }
        public static LocalizedText StateUnknown { get; private set; }
        public static LocalizedText UnknownName { get; private set; }
        public static LocalizedText UnknownHint { get; private set; }
        public static LocalizedText SealedOriginHint { get; private set; }
        public static LocalizedText SealedPowerHint { get; private set; }
        public static LocalizedText UnrenewedOriginHint { get; private set; }
        public static LocalizedText UnrenewedPowerHint { get; private set; }
        public static LocalizedText CloseTagText { get; private set; }
        public static LocalizedText CloseHintFormat { get; private set; }
        public static LocalizedText MeiTabText { get; private set; }
        public static LocalizedText MeiTabHint { get; private set; }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "点 鬼 簿");
            StatusFormat = this.GetLocalization(nameof(StatusFormat), () => "铭鬼 {0} 缕 · 总驾驭 {1}%");
            MasteryFormat = this.GetLocalization(nameof(MasteryFormat), () => "驾驭 {0}%");
            OriginLabel = this.GetLocalization(nameof(OriginLabel), () => "来历");
            PowerLabel = this.GetLocalization(nameof(PowerLabel), () => "赋力");
            StateEngraved = this.GetLocalization(nameof(StateEngraved), () => "铭刻 · 稳固");
            StateRestless = this.GetLocalization(nameof(StateRestless), () => "铭刻 · 躁动");
            StateSealed = this.GetLocalization(nameof(StateSealed), () => "封印中");
            StateUnknown = this.GetLocalization(nameof(StateUnknown), () => "铭位空悬");
            UnknownName = this.GetLocalization(nameof(UnknownName), () => "（留白）");
            UnknownHint = this.GetLocalization(nameof(UnknownHint), () => "簿上留白。夜还长，总会有名字自己走上来");
            SealedOriginHint = this.GetLocalization(nameof(SealedOriginHint), () => "封印未解。莫揭，莫应，莫回头");
            SealedPowerHint = this.GetLocalization(nameof(SealedPowerHint), () => "———");
            //残页门控文案键(现不挡簿面)
            UnrenewedOriginHint = this.GetLocalization(nameof(UnrenewedOriginHint), () => "旧契认刀，不认这只手。去它被收伏之地，重续名字");
            UnrenewedPowerHint = this.GetLocalization(nameof(UnrenewedPowerHint), () => "———");
            CloseTagText = this.GetLocalization(nameof(CloseTagText), () => "收卷");
            CloseHintFormat = this.GetLocalization(nameof(CloseHintFormat), () => "ESC · {0} · 点击卷外 收卷");
            MeiTabText = this.GetLocalization(nameof(MeiTabText), () => "改铭台");
            MeiTabHint = this.GetLocalization(nameof(MeiTabHint), () => "点击 移步");
        }
        #endregion

        public override bool CloseOnEscape => true;
        public override float RenderPriority => 2f;
        public override SoundStyle? OpenSound => SoundID.MenuOpen with { Pitch = -0.45f, Volume = 0.55f };
        public override SoundStyle? CloseSound => SoundID.MenuClose with { Pitch = -0.3f, Volume = 0.5f };

        //====交互状态====
        private int selectedIndex;
        private int hoverIndex = -1;
        private readonly float[] hoverEase = new float[16];
        private float selectEase;
        private Rectangle scrollRect;
        private Rectangle detailRect;
        //收卷木牌,点击关闭,牌绳 Verlet
        private Rectangle closeTagRect;
        private float closeTagHover;
        private bool closeTagWasHovered;
        private Vector2 closeTagAnchor;
        private readonly OniRope closeTagRope = new(5, 22f);
        //吊挂太刀:去改铭台的门(对面器物的微缩,挂在卷轴左肩的梁下)
        private readonly OniHangingSwitch meiSwitch = new(SoundID.Unlock with { Pitch = 0.3f, Volume = 0.35f });
        private Vector2 meiSwitchAnchor;
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

        protected override void OnOpen() {
            Main.playerInventory = false;
            //姊妹屏互斥:一卷开另一台收
            if (OniMeiUI.Instance?.IsOpen ?? false) {
                OniMeiUI.Instance.Close();
            }
            meiSwitch.Reset();
            SwayTimer = 0f;
            petalTimer = 0;
            particles.Clear();
            SelectEntry(FindFirstVisible(), silent: true);
            idleTimer = 0;
            glanceStrength = 0f;
            pupilOpen = 0f;
            pupilCooldown = Main.rand.Next(900, 1800);
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Activate(FreezeReason);
            }
        }

        protected override void OnClose() {
            if (VaultUtils.isSinglePlayer) {
                WorldFreezeSystem.Deactivate(FreezeReason);
            }
        }

        private static int FindFirstVisible() {
            var entries = OniRegistry.Entries;
            for (int i = 0; i < entries.Count; i++) {
                if (entries[i].State != OniGhostState.Unknown) {
                    return i;
                }
            }
            return entries.Count > 0 ? 0 : -1;
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

        public override void Update() {
            float a = OpenProgress;
            if (a < 0.01f) {
                return;
            }

            if (IsOpen) {
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();
                if (!player.active || player.dead) {
                    Close();
                }
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

            //收卷牌摆:绳受风,牌是末端配重
            closeTagRope.Update(closeTagAnchor, null, GlobalTimer, 0.26f, endWeight: 0.55f);
            Vector2 tagTop = closeTagRope.End;
            closeTagRect = new Rectangle((int)(tagTop.X - 16f), (int)tagTop.Y - 2, 32, 48);

            //吊挂太刀:点击预演到帧即移步改铭台
            bool meiRiteBusy = OniEngraveRiteUI.Instance?.Active ?? false;
            if (meiSwitch.Update(meiSwitchAnchor, MousePosition, IsOpen && a > 0.9f && !meiRiteBusy,
                GlobalTimer, new Vector2(30f, 100f), keyLeftPressState)) {
                OniMeiUI.Instance?.Open();
            }

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
            //列数决定的最小宽度,防止小屏挤压
            float needW = entries.Count * (OnikiriUITheme.EntryColumnW + OnikiriUITheme.EntryColumnGap) + 70f;
            scrollW = Math.Max(scrollW, Math.Min(needW, sw * 0.5f));
            float scrollH = sh * 0.84f;
            Vector2 scrollCenter = new(sw * OnikiriUITheme.ScrollCenterXRatio, sh * 0.5f + 6f);
            scrollRect = new Rectangle((int)(scrollCenter.X - scrollW * 0.5f), (int)(scrollCenter.Y - scrollH * 0.5f), (int)scrollW, (int)scrollH);

            float detailX = scrollRect.Right + 38f;
            float detailW = Math.Min(430f, sw - detailX - 44f);
            detailRect = new Rectangle((int)detailX, (int)(sh * 0.5f - 216f), (int)detailW, 432);

            //收卷木牌绳锚:顶部轴杆右端帽,牌体位置由 Verlet 绳每帧决定
            closeTagAnchor = new Vector2(scrollRect.Right + 17f, scrollRect.Y - 7f);

            //吊挂太刀锚:卷轴左肩外的梁下,与右肩收卷牌对称成"一梁两挂"
            meiSwitchAnchor = new Vector2(scrollRect.X - 48f, scrollRect.Y - 7f);

            //名录竖列,右起左行(旧式名册自右向左)
            Rectangle inner = scrollRect;
            inner.Inflate(-30, -36);
            float colX = inner.Right - OnikiriUITheme.EntryColumnW;
            int colTop = inner.Y + 58;
            int colH = Math.Min(inner.Height - 96, 300);
            for (int i = 0; i < entries.Count && i < entryRects.Length; i++) {
                entryRects[i] = new Rectangle((int)colX, colTop, (int)OnikiriUITheme.EntryColumnW, colH);
                colX -= OnikiriUITheme.EntryColumnW + OnikiriUITheme.EntryColumnGap;
            }
        }

        private void UpdateInteraction(float a) {
            //铭刻仪式压在簿面之上时,簿面不受理点击
            bool riteActive = OniEngraveRiteUI.Instance?.Active ?? false;
            bool inputAvailable = IsOpen && a > 0.9f && !riteActive;
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

            //收卷牌 hover 缓动;拂过时给绳一记横向冲量,像被手碰了一下
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
                //点击卷外压暗区收卷:卷轴(含外扩边)、细节板、收卷牌、吊挂太刀之外都算"外"
                Rectangle scrollHit = scrollRect;
                scrollHit.Inflate(OnikiriUITheme.ScrollEdgePad + 22, OnikiriUITheme.ScrollEdgePad + 22);
                if (!scrollHit.Contains(mp) && !detailRect.Contains(mp) && !meiSwitch.Hovering) {
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
            if (sel != null && sel.HasName && sel.State != OniGhostState.Sealed) {
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

        /// <summary>线香燃点,燃去比=驾驭度</summary>
        internal Vector2 IncenseEmberPos() {
            OniGhostEntry sel = SelectedEntry;
            float mastery = MathHelper.Clamp(sel?.Mastery ?? 0f, 0f, 1f);
            Rectangle rect = IncenseRect();
            return new Vector2(rect.Center.X, rect.Y + rect.Height * mastery);
        }

        /// <summary>线香立杆矩形(细节板右下,文案区右缘让位于它)</summary>
        internal Rectangle IncenseRect() => new(detailRect.Right - 56, detailRect.Bottom - 132, 4, 84);

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

            //绯月竖瞳,危态低频,睁约 1.6s
            if (OniRegistry.InDanger) {
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
                OniRegisterRenderer.DrawCloseTag(spriteBatch, font, closeTagRope, contentA, closeTagHover, GlobalTimer);
                DrawCloseHint(spriteBatch, font, contentA);
                //吊挂太刀:荷札上书今名(铭位数据是每刀一份,展示缓存直读)
                string bladeName = Inscriptions.OniMeiRegistry.CurrentBladeName(
                    Inscriptions.OniMeiRegistry.DisplayStore)?.DisplayName.Value ?? "";
                OniRegisterRenderer.DrawHangingTachi(spriteBatch, font, meiSwitch, contentA, GlobalTimer, bladeName);
            }

            //====两翼落花====
            particles.Draw(spriteBatch, a);

            //吊挂太刀的悬浮说明(最后画,压在一切之上)
            if (meiSwitch.HoverEase > 0.05f) {
                OniMeiRenderer.DrawSwitchHoverTag(spriteBatch, font, MousePosition,
                    MeiTabText.Value, MeiTabHint.Value, a * meiSwitch.HoverEase);
            }
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

            //状态行:铭鬼数 + 总驾驭,置于卷底绫带上方
            int engraved = 0;
            foreach (OniGhostEntry e in OniRegistry.Entries) {
                if (e.State == OniGhostState.Engraved || e.State == OniGhostState.Restless) {
                    engraved++;
                }
            }
            string status = string.Format(StatusFormat.Value, engraved, (int)(OniRegistry.TotalMastery * 100f));
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
                    selected, selected ? selectEase : 0f, GlobalTimer, i);
            }
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

    /// <summary>点鬼簿键位开关,持刀时传奇 UI 键开阖</summary>
    internal sealed class OniRegisterKeyPlayer : ModPlayer
    {
        public override void PostUpdate() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }
            Item item = Player.GetItem();
            bool holding = item.Alives()
                && (item.type == ModContent.ItemType<OnikiriItem>());
            if (!holding) {
                return;
            }
            //仪式演出中不响应开阖,避免两层演出叠打(铭刻窗与鏨仪式同理)
            if ((OniEngraveRiteUI.Instance?.Active ?? false) || (OniMeiUI.Instance?.Rite.Active ?? false)) {
                return;
            }
            if (CWRKeySystem.Legend_UIControl.JustPressed) {
                //改铭台开着:键先收台;否则开阖点鬼簿
                if (OniMeiUI.Instance?.IsOpen ?? false) {
                    OniMeiUI.Instance.Close();
                }
                else {
                    OniRegisterUI.Instance?.Toggle();
                }
            }
        }
    }
}
