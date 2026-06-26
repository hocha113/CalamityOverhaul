using InnoVault.GameSystem;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.MainMenuOverUIs
{
    /// <summary>
    /// 模组致谢 ED：编排式片尾——入场标题揭示 → 分节滚动名单 → 谢幕定格卡。
    /// 全屏背景与谢幕辉光走着色器（AckBackdrop / AckFinale），缺失时 CPU 回退；
    /// 版式参考明日方舟片尾：近黑底、单一暖琥珀强调、克制留白与缓动
    /// </summary>
    internal class AcknowledgmentUI : UIHandle, IUpdateAudio, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static LocalizedText ArtistRole { get; private set; }
        public static LocalizedText CodeAssistanceRole { get; private set; }
        public static LocalizedText MusicianRole { get; private set; }
        public static LocalizedText DonorRole { get; private set; }
        public static LocalizedText BalanceTesterRole { get; private set; }
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText SubtitleText { get; private set; }
        public static LocalizedText FinaleText { get; private set; }
        public static LocalizedText ExitHintText { get; private set; }

        internal static AcknowledgmentUI Instance;
        /// <summary>外部入口（致谢按钮）置真以开启 ED</summary>
        internal bool _active;

        [VaultLoaden("CalamityOverhaul/IntactLogo")]
        private static Asset<Texture2D> Logo = null;

        private enum Phase { Title, Roll, Finale }
        private Phase phase;
        private float phaseTime;     //当前阶段已运行秒数
        private float fade;          //主透明度 0-1
        private float globalTime;    //动画总时钟（秒），驱动呼吸/微光
        private float scrollPx;      //名单滚动量（UI空间像素）
        private float rollDistance;  //名单滚完所需的滚动量
        private float frameReveal;   //取景框入场 0-1
        private float holdProgress;  //长按退出读条 0-1
        private int musicFade50;     //ED 音乐与其它音轨的交叉淡入计数

        private const float TitleDuration = 6f;     //标题阶段总时长
        private const float TitleExitTime = 1.3f;   //标题退场淡出时长
        private const float FinaleRiseTime = 2.4f;  //谢幕辉光涨起时长
        private const float RollPxPerFrame = 0.85f; //名单每帧滚动像素

        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;
        public override bool Active => CWRLoad.OnLoadContentBool;
        public override float RenderPriority => 1.2f;
        public override bool CanLoad() => true;

        public static bool OnActive() {
            if (Instance == null) {
                return false;
            }
            return Instance._active || Instance.fade > 0f;
        }

        public override void SetStaticDefaults() {
            ArtistRole = this.GetLocalization(nameof(ArtistRole), () => "画师");
            CodeAssistanceRole = this.GetLocalization(nameof(CodeAssistanceRole), () => "代码援助");
            MusicianRole = this.GetLocalization(nameof(MusicianRole), () => "音乐制作");
            DonorRole = this.GetLocalization(nameof(DonorRole), () => "捐赠者");
            BalanceTesterRole = this.GetLocalization(nameof(BalanceTesterRole), () => "平衡测试");
            TitleText = this.GetLocalization(nameof(TitleText), () => "鸣 谢");
            SubtitleText = this.GetLocalization(nameof(SubtitleText), () => "灾厄大修 · 全体贡献者");
            FinaleText = this.GetLocalization(nameof(FinaleText), () => "感谢一路同行");
            ExitHintText = this.GetLocalization(nameof(ExitHintText), () => "点击或按 ESC 退出");
            Instance = UIHandleLoader.GetUIHandleOfType<AcknowledgmentUI>();
            ResetTimeline();
        }

        public override void UnLoad() {
            Instance = null;
            fade = 0f;
        }

        private void ResetTimeline() {
            phase = Phase.Title;
            phaseTime = 0f;
            scrollPx = 0f;
            frameReveal = 0f;
            holdProgress = 0f;
        }

        private static string RoleHeader(CreditRole role) => role switch {
            CreditRole.Artist => ArtistRole.Value,
            CreditRole.CodeAssistance => CodeAssistanceRole.Value,
            CreditRole.Musician => MusicianRole.Value,
            CreditRole.BalanceTester => BalanceTesterRole.Value,
            _ => DonorRole.Value,
        };

        void IUpdateAudio.DecideMusic() {
            if (!Main.gameMenu || !OnActive()) {
                return;
            }
            int targetID = MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/ED_WEH");
            for (int i = 0; i < Main.musicFade.Length; i++) {
                if (i == targetID) {
                    continue;
                }
                Main.musicFade[i] = musicFade50 / 120f;
            }
            Main.newMusic = targetID;
        }

        public override void Update() {
            globalTime += 1f / 60f;

            if (!OnActive()) {
                if (musicFade50 < 120) {
                    musicFade50++;
                }
                return;
            }
            if (musicFade50 > 0) {
                musicFade50--;
            }

            if (_active) {
                fade = MathF.Min(1f, fade + 0.035f);
            }
            else if (fade > 0f) {
                fade = MathF.Max(0f, fade - 0.045f);
                if (fade <= 0f) {
                    ResetTimeline();
                    return;
                }
            }

            //淡入到一定程度后才推进编排，避免黑屏瞬间就开始滚动
            if (fade > 0.25f) {
                phaseTime += 1f / 60f;
                AdvancePhase();
            }

            HandleInput();
        }

        private void AdvancePhase() {
            switch (phase) {
                case Phase.Title:
                    frameReveal = MathF.Min(1f, frameReveal + 1f / 90f);
                    if (phaseTime >= TitleDuration) {
                        phase = Phase.Roll;
                        phaseTime = 0f;
                        scrollPx = 0f;
                        rollDistance = MeasureRollDistance(AckTheme.UIScreenW, AckTheme.UIScreenH);
                    }
                    break;
                case Phase.Roll:
                    scrollPx += RollPxPerFrame;
                    if (scrollPx >= rollDistance) {
                        phase = Phase.Finale;
                        phaseTime = 0f;
                    }
                    break;
                case Phase.Finale:
                    break;
            }
        }

        private void HandleInput() {
            //长按任意键（含鼠标）读条退出，松开即回落，避免误触瞬退
            if (fade < 0.85f) {
                holdProgress = MathF.Max(0f, holdProgress - 0.05f);
                return;
            }
            if (AnyKeyHeld()) {
                holdProgress = MathF.Min(1f, holdProgress + 1f / 78f);
                if (holdProgress >= 1f) {
                    SoundEngine.PlaySound(SoundID.MenuClose);
                    _active = false;
                }
            }
            else {
                holdProgress = MathF.Max(0f, holdProgress - 0.045f);
            }
        }

        private static bool AnyKeyHeld() {
            if (Main.mouseLeft || Main.mouseRight || Main.mouseMiddle) {
                return true;
            }
            return Main.keyState.GetPressedKeys().Length > 0;
        }

        #region 名单布局测算
        private static int DonorColumns(float contentWidth) => Math.Max(2, (int)(contentWidth / AckTheme.DonorColWidth));

        private static bool IsGridSection(in CreditSection sec)
            => sec.Role == CreditRole.Donor || sec.Names.Length > AckCredits.MultiColumnThreshold;

        /// <summary>名单从底部入场滚到完全离顶所需的滚动距离</summary>
        private static float MeasureRollDistance(float screenW, float screenH) {
            float contentWidth = screenW * (1f - AckTheme.SideMarginRatio * 2f);
            int cols = DonorColumns(contentWidth);
            float y = 0f;
            foreach (CreditSection sec in AckCredits.Sections) {
                y += AckTheme.SectionGap + AckTheme.HeaderHeight;
                if (IsGridSection(sec)) {
                    int rows = (sec.Names.Length + cols - 1) / cols;
                    y += rows * AckTheme.DonorRowHeight;
                }
                else {
                    y += sec.Names.Length * AckTheme.NameRowHeight;
                }
            }
            return y + AckTheme.SectionGap + screenH * 0.5f;
        }
        #endregion

        public override void Draw(SpriteBatch spriteBatch) {
            if (!OnActive()) {
                return;
            }
            //资源在卸载模组时可能已被释放，绘制前确认占位纹理仍可用
            if (CWRAsset.Placeholder_White == null || CWRAsset.Placeholder_White.IsDisposed) {
                _active = false;
                return;
            }

            float screenW = AckTheme.UIScreenW;
            float screenH = AckTheme.UIScreenH;

            //背景情绪随阶段过渡：入场偏冷暗 → 名单中段 → 谢幕暖亮
            float progress = phase switch {
                Phase.Finale => 1f,
                Phase.Roll => 0.5f,
                _ => 0.15f,
            };
            AckRenderer.DrawBackdrop(spriteBatch, new Rectangle(0, 0, (int)screenW, (int)screenH),
                fade, progress, AckTheme.Accent);

            AckRenderer.DrawScreenFrame(spriteBatch, screenW, screenH, fade, frameReveal, globalTime);

            switch (phase) {
                case Phase.Title:
                    DrawTitle(spriteBatch, screenW, screenH);
                    break;
                case Phase.Roll:
                    DrawRoll(spriteBatch, screenW, screenH);
                    break;
                case Phase.Finale:
                    DrawFinale(spriteBatch, screenW, screenH);
                    break;
            }

            DrawExitHold(spriteBatch, screenW, screenH);
        }

        #region 标题阶段
        private void DrawTitle(SpriteBatch sb, float screenW, float screenH) {
            float t = phaseTime;
            float exit = AckTheme.EaseInOutCubic((t - (TitleDuration - TitleExitTime)) / TitleExitTime);
            float block = fade * (1f - exit);
            if (block < 0.01f) {
                return;
            }
            float riseOut = -38f * exit;
            float cx = screenW * 0.5f;

            //标志：缓出回弹浮入（回弹会越过 1，位移用其过冲，透明度须钳制避免 Color*scale 溢出回绕）
            float logoAppear = AckTheme.EaseOutBack((t - 0.5f) / 1.7f);
            Vector2 logoCenter = new(cx, screenH * 0.40f + (1f - logoAppear) * 26f + riseOut);
            AckRenderer.DrawLogo(sb, Logo?.Value, logoCenter, 0.92f, block * AckTheme.Saturate(logoAppear), AckTheme.Accent);

            //主标题
            float titleAppear = AckTheme.EaseOutCubic((t - 1.7f) / 1.4f);
            float titleScale = 1.95f;
            Vector2 titleCenter = new(cx, screenH * 0.54f + (1f - titleAppear) * 16f + riseOut);
            Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(TitleText.Value) * titleScale;
            AckRenderer.DrawDisplayText(sb, TitleText.Value, titleCenter,
                AckTheme.Text * (block * titleAppear), AckTheme.Accent, titleScale, block * titleAppear * 0.4f);

            //标题两侧取景括号
            float bracketGap = titleSize.X * 0.5f + 30f;
            float bh = titleSize.Y * 0.40f;
            Color brc = AckTheme.Accent * (block * titleAppear * 0.85f);
            AckRenderer.DrawBracket(sb, new Vector2(titleCenter.X - bracketGap, titleCenter.Y - bh), 13f, 2f, 1, 1, brc);
            AckRenderer.DrawBracket(sb, new Vector2(titleCenter.X - bracketGap, titleCenter.Y + bh), 13f, 2f, 1, -1, brc);
            AckRenderer.DrawBracket(sb, new Vector2(titleCenter.X + bracketGap, titleCenter.Y - bh), 13f, 2f, -1, 1, brc);
            AckRenderer.DrawBracket(sb, new Vector2(titleCenter.X + bracketGap, titleCenter.Y + bh), 13f, 2f, -1, -1, brc);

            //标题下的对称生长强调线 + 中点菱形
            float ulHalf = (titleSize.X * 0.5f + 12f) * AckTheme.EaseOutQuint(titleAppear);
            float ulY = titleCenter.Y + titleSize.Y * 0.5f + 9f;
            AckRenderer.DrawGradientLine(sb, new Vector2(cx, ulY), new Vector2(cx - ulHalf, ulY),
                AckTheme.Accent * (block * 0.7f), AckTheme.Accent * 0.02f, 1.6f);
            AckRenderer.DrawGradientLine(sb, new Vector2(cx, ulY), new Vector2(cx + ulHalf, ulY),
                AckTheme.Accent * (block * 0.7f), AckTheme.Accent * 0.02f, 1.6f);
            AckRenderer.DrawDiamond(sb, new Vector2(cx, ulY), 4f, AckTheme.AccentHi * (block * titleAppear));

            //副标题：字距拉开，居中
            float subAppear = AckTheme.EaseOutCubic((t - 2.7f) / 1.3f);
            string sub = SubtitleText.Value;
            float subScale = 0.78f;
            float subTrack = 2.6f;
            float subW = AckRenderer.MeasureTracked(sub, subScale, subTrack);
            AckRenderer.DrawTrackedText(sb, sub, new Vector2(cx - subW * 0.5f, ulY + 18f),
                AckTheme.TextDim * (block * subAppear), subScale, subTrack);
        }
        #endregion

        #region 名单阶段
        private void DrawRoll(SpriteBatch sb, float screenW, float screenH) {
            float contentLeft = screenW * AckTheme.SideMarginRatio;
            float contentRight = screenW * (1f - AckTheme.SideMarginRatio);
            float contentWidth = contentRight - contentLeft;
            int cols = DonorColumns(contentWidth);
            float baseY = screenH;
            float y = 0f;
            int sectionIndex = 0;
            int total = AckCredits.Sections.Length;

            foreach (CreditSection sec in AckCredits.Sections) {
                y += AckTheme.SectionGap;
                float headerTop = baseY - scrollPx + y;
                if (headerTop > -AckTheme.HeaderHeight && headerTop < screenH + 40f) {
                    float a = RowAlpha(headerTop + AckTheme.HeaderHeight * 0.5f, screenH);
                    float reveal = AckTheme.Saturate((screenH * 0.92f - headerTop) / (screenH * 0.32f));
                    AckRenderer.DrawSectionHeader(sb, sectionIndex, total, sec.Role, RoleHeader(sec.Role),
                        contentLeft, headerTop, AckTheme.HeaderHeight, contentRight, a, reveal, globalTime);
                }
                y += AckTheme.HeaderHeight;

                if (IsGridSection(sec)) {
                    int rows = (sec.Names.Length + cols - 1) / cols;
                    float colW = contentWidth / cols;
                    for (int n = 0; n < sec.Names.Length; n++) {
                        int col = n % cols;
                        int row = n / cols;
                        float rowY = baseY - scrollPx + y + row * AckTheme.DonorRowHeight;
                        if (rowY < -20f || rowY > screenH + 20f) {
                            continue;
                        }
                        float a = RowAlpha(rowY + AckTheme.DonorRowHeight * 0.5f, screenH);
                        if (a < 0.01f) {
                            continue;
                        }
                        Vector2 cellCenter = new(contentLeft + colW * (col + 0.5f), rowY + AckTheme.DonorRowHeight * 0.5f);
                        AckRenderer.DrawNameCentered(sb, sec.Names[n], cellCenter, AckTheme.TextDim, a, 0.82f, colW - 18f);
                    }
                    y += rows * AckTheme.DonorRowHeight;
                }
                else {
                    Color nameCol = Color.Lerp(AckTheme.Text, AckTheme.RoleColor(sec.Role), 0.18f);
                    for (int n = 0; n < sec.Names.Length; n++) {
                        float rowY = baseY - scrollPx + y + n * AckTheme.NameRowHeight;
                        if (rowY < -20f || rowY > screenH + 20f) {
                            continue;
                        }
                        float a = RowAlpha(rowY + AckTheme.NameRowHeight * 0.5f, screenH);
                        if (a < 0.01f) {
                            continue;
                        }
                        AckRenderer.DrawName(sb, sec.Names[n], new Vector2(contentLeft + 28f, rowY), nameCol, a, 0.95f);
                    }
                    y += sec.Names.Length * AckTheme.NameRowHeight;
                }
                sectionIndex++;
            }
        }

        /// <summary>名字进出视野时上下渐隐，乘以主透明度</summary>
        private float RowAlpha(float screenY, float screenH) {
            float band = AckTheme.FadeBand;
            float topFactor = AckTheme.Saturate(screenY / band);
            float bottomFactor = AckTheme.Saturate((screenH - screenY) / band);
            return topFactor * bottomFactor * fade;
        }
        #endregion

        #region 谢幕阶段
        private void DrawFinale(SpriteBatch sb, float screenW, float screenH) {
            float cx = screenW * 0.5f;
            float cy = screenH * 0.46f;
            float intensity = AckTheme.EaseOutCubic(phaseTime / FinaleRiseTime);
            float auraR = MathF.Min(screenW, screenH) * 0.42f;

            AckRenderer.DrawFinaleAura(sb, new Vector2(cx, cy), auraR, fade, intensity, AckTheme.Accent);

            float breath = AckTheme.Breath(globalTime, 0f, 1.2f);
            AckRenderer.DrawLogo(sb, Logo?.Value, new Vector2(cx, cy), 1f + breath * 0.02f,
                fade * intensity, AckTheme.AccentHi);

            float textAppear = AckTheme.EaseOutCubic((phaseTime - 0.8f) / 1.6f);
            float fScale = 1.15f;
            Vector2 textCenter = new(cx, cy + auraR * 0.55f + 24f);
            Vector2 fSize = FontAssets.MouseText.Value.MeasureString(FinaleText.Value) * fScale;
            AckRenderer.DrawDisplayText(sb, FinaleText.Value, textCenter,
                AckTheme.Text * (fade * textAppear), AckTheme.Accent, fScale, fade * textAppear * 0.4f);

            float ulY = textCenter.Y + fSize.Y * 0.5f + 10f;
            float ulHalf = (fSize.X * 0.5f + 26f) * AckTheme.EaseOutQuint(textAppear);
            AckRenderer.DrawGradientLine(sb, new Vector2(cx, ulY), new Vector2(cx - ulHalf, ulY),
                AckTheme.Accent * (fade * 0.6f), AckTheme.Accent * 0.02f, 1.4f);
            AckRenderer.DrawGradientLine(sb, new Vector2(cx, ulY), new Vector2(cx + ulHalf, ulY),
                AckTheme.Accent * (fade * 0.6f), AckTheme.Accent * 0.02f, 1.4f);
            AckRenderer.DrawDiamond(sb, new Vector2(cx, ulY), 4f, AckTheme.AccentHi * (fade * textAppear));
        }
        #endregion

        private void DrawExitHold(SpriteBatch sb, float screenW, float screenH) {
            float baseA = AckTheme.Saturate((fade - 0.5f) * 2f);
            if (baseA < 0.01f) {
                return;
            }
            float cx = screenW * 0.5f;
            float y = screenH - 48f;

            //提示文字：按住时由暗转亮
            string hint = ExitHintText.Value;
            const float hintScale = 0.66f;
            const float hintTrack = 1.8f;
            float hintW = AckRenderer.MeasureTracked(hint, hintScale, hintTrack);
            Color hintCol = Color.Lerp(AckTheme.TextFaint, AckTheme.Accent, holdProgress);
            AckRenderer.DrawTrackedText(sb, hint, new Vector2(cx - hintW * 0.5f, y - 22f),
                hintCol * (baseA * (0.45f + holdProgress * 0.5f)), hintScale, hintTrack);

            //读条：细轨 + 生长辉光填充 + 端点菱形
            const float barW = 210f;
            float left = cx - barW * 0.5f;
            AckRenderer.DrawLine(sb, new Vector2(left, y), new Vector2(left + barW, y), 2f,
                AckTheme.TextFaint * (baseA * 0.35f));
            if (holdProgress > 0.001f) {
                float fillX = left + barW * holdProgress;
                AckRenderer.DrawGlowLine(sb, new Vector2(left, y), new Vector2(fillX, y), 2.2f, AckTheme.Accent * baseA);
                AckRenderer.DrawDiamond(sb, new Vector2(fillX, y), 4.5f, AckTheme.AccentHi * baseA);
            }
        }
    }
}
