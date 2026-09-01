using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Narrative
{
    internal class MusicToast : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static MusicToast Instance => UIHandleLoader.GetUIHandleOfType<MusicToast>();

        public enum MusicStyle
        {
            Vinyl,  //黑胶
            Digital,  //数字音波
            Neon,  //霓虹光谱
            RedNeon,  //红霓虹
            WetInk,  //湿墨冷青，对齐鬼湖色板
            Sakura  //夜樱朱红，对齐夜樱色板
        }

        public class MusicInfo
        {
            public Texture2D AlbumCover { get; set; }
            public string Title { get; set; }
            public string Artist { get; set; }
            public Texture2D TitleTexture { get; set; }
            public Func<float> ScreenYProvider { get; set; }
            public MusicStyle Style { get; set; } = MusicStyle.Vinyl;
            public Action OnComplete { get; set; }
            public int DisplayDuration { get; set; } = 300;  //默认5秒
            public float LayoutScale { get; set; } = 1f;
        }

        #region 数据字段
        private readonly Queue<MusicInfo> musicQueue = new();
        private MusicInfo currentMusic;

        private enum AnimationState
        {
            SlideIn,  //滑入
            Display,  //展示
            SlideOut  //滑出
        }
        private AnimationState currentState = AnimationState.SlideIn;
        private int stateTimer = 0;

        private const int SlideInDuration = 35;  //滑入帧
        private const int SlideOutDuration = 30;  //滑出帧

        private float slideProgress = 0f;
        private float alpha = 1f;
        private float pulsePhase = 0f;
        private float wavePhase = 0f;

        private const float MinPanelWidth = 320f;
        private const float MaxPanelWidth = 550f;
        internal const float PanelHeight = 90f;
        internal const float MenuLayoutScale = 0.85f;
        private const float AlbumSize = 70f;
        private const float AlbumPadding = 10f;
        private const float TextStartX = 95f;
        private const float TextPaddingRight = 15f;

        private float LayoutScale => currentMusic?.LayoutScale ?? 1f;
        private float BoxH => PanelHeight * LayoutScale;
        private float Cover => AlbumSize * LayoutScale;
        private float CoverPad => AlbumPadding * LayoutScale;
        private float TextX => TextStartX * LayoutScale;
        private float TextPadR => TextPaddingRight * LayoutScale;

        private float currentPanelWidth = MinPanelWidth;
        private float OffscreenX => -currentPanelWidth - 50f;
        private const float OnscreenX = 15f;
        private float ScreenY => Main.screenHeight - BoxH - 120f;  //左下角

        private readonly float[] audioLevels = new float[32];
        private int audioUpdateTimer = 0;

        private float vinylRotation = 0f;

        private readonly List<MusicParticle> particles = new();
        private int particleSpawnTimer = 0;

        private readonly float[] spectrumBars = new float[16];
        private int spectrumUpdateTimer = 0;

        protected static LocalizedText NowPlaying;
        #endregion

        public override bool Active => currentMusic != null || musicQueue.Count > 0 || slideProgress > 0.01f;

        public override void SetStaticDefaults() {
            NowPlaying = this.GetLocalization(nameof(NowPlaying), () => "正在播放");
        }

        #region 公共API
        public static void ShowMusic(string title, string artist = null, Texture2D albumCover = null,
            MusicStyle style = MusicStyle.Vinyl, int displayDuration = 300, Action onComplete = null,
            Texture2D titleTexture = null, Func<float> screenYProvider = null, float layoutScale = 1f) {
            var music = new MusicInfo {
                Title = title,
                Artist = artist,
                AlbumCover = albumCover,
                TitleTexture = titleTexture,
                ScreenYProvider = screenYProvider,
                Style = style,
                DisplayDuration = displayDuration,
                OnComplete = onComplete,
                LayoutScale = layoutScale > 0.01f ? layoutScale : 1f
            };
            Instance.musicQueue.Enqueue(music);
        }

        public static void ClearQueue() {
            Instance.musicQueue.Clear();
        }

        public static void Dismiss() {
            MusicToast inst = Instance;
            inst.musicQueue.Clear();
            inst.currentMusic = null;
            inst.slideProgress = 0f;
            inst.alpha = 1f;
            inst.stateTimer = 0;
            inst.currentState = AnimationState.SlideIn;
            inst.particles.Clear();
        }

        public override void OnEnterWorld() => Dismiss();
        #endregion

        #region 更新逻辑
        public override void LogicUpdate() {
            pulsePhase += 0.04f;
            wavePhase += 0.06f;
            vinylRotation += 0.05f;

            if (pulsePhase > MathHelper.TwoPi) pulsePhase -= MathHelper.TwoPi;
            if (wavePhase > MathHelper.TwoPi) wavePhase -= MathHelper.TwoPi;
            if (vinylRotation > MathHelper.TwoPi) vinylRotation -= MathHelper.TwoPi;

            audioUpdateTimer++;
            if (audioUpdateTimer >= 3) {
                audioUpdateTimer = 0;
                UpdateAudioLevels();
            }

            spectrumUpdateTimer++;
            if (spectrumUpdateTimer >= 2) {
                spectrumUpdateTimer = 0;
                UpdateSpectrum();
            }

            if (currentMusic == null && musicQueue.Count > 0) {
                StartNext();
                return;
            }

            if (currentMusic == null) {
                return;
            }

            UpdateAnimation();
            UpdateParticles();
        }

        private void StartNext() {
            currentMusic = musicQueue.Dequeue();
            currentState = AnimationState.SlideIn;
            stateTimer = 0;
            slideProgress = 0f;
            alpha = 1f;
            pulsePhase = 0f;
            wavePhase = 0f;
            vinylRotation = 0f;
            particles.Clear();
            Array.Clear(audioLevels, 0, audioLevels.Length);
            Array.Clear(spectrumBars, 0, spectrumBars.Length);

            CalculatePanelWidth();
        }

        private void CalculatePanelWidth() {
            var font = FontAssets.MouseText.Value;
            float s = LayoutScale;
            float maxTextWidth = ResolveTitleSize(MaxPanelWidth * s - TextX - TextPadR).X;

            if (!string.IsNullOrEmpty(currentMusic.Artist)) {
                Vector2 artistSize = font.MeasureString(currentMusic.Artist) * (0.65f * s);
                maxTextWidth = Math.Max(maxTextWidth, artistSize.X);
            }

            float requiredWidth = TextX + maxTextWidth + TextPadR;
            currentPanelWidth = Math.Clamp(requiredWidth, MinPanelWidth * s, MaxPanelWidth * s);
        }

        private bool HasTitleTexture =>
            currentMusic?.TitleTexture != null && !currentMusic.TitleTexture.IsDisposed;

        //标题行高度对齐 0.85 字号；贴图不超过 1x，超宽再按可用宽度压
        private Vector2 ResolveTitleSize(float maxWidth) {
            if (HasTitleTexture) {
                Texture2D tex = currentMusic.TitleTexture;
                float lineH = FontAssets.MouseText.Value.MeasureString("A").Y * (0.85f * LayoutScale);
                float scale = Math.Min(LayoutScale, lineH / Math.Max(tex.Height, 1));
                float width = tex.Width * scale;
                if (width > maxWidth && width > 0.01f) {
                    scale *= maxWidth / width;
                }
                return new Vector2(tex.Width * scale, tex.Height * scale);
            }

            string titleText = currentMusic.Title ?? "Unknown Track";
            return FontAssets.MouseText.Value.MeasureString(titleText) * (0.85f * LayoutScale);
        }

        private void UpdateAnimation() {
            stateTimer++;

            switch (currentState) {
                case AnimationState.SlideIn:
                    UpdateSlideIn();
                    break;
                case AnimationState.Display:
                    UpdateDisplay();
                    break;
                case AnimationState.SlideOut:
                    UpdateSlideOut();
                    break;
            }
        }

        private void UpdateSlideIn() {
            float t = stateTimer / (float)SlideInDuration;
            t = VaultUtils.EaseOutCubic(t);
            slideProgress = t;

            if (stateTimer >= SlideInDuration) {
                currentState = AnimationState.Display;
                stateTimer = 0;
            }
        }

        private void UpdateDisplay() {
            particleSpawnTimer++;
            if (particleSpawnTimer >= 8) {
                particleSpawnTimer = 0;
                Vector2 panelPos = GetCurrentPanelPosition();
                particles.Add(new MusicParticle(
                    new Vector2(panelPos.X + currentPanelWidth, panelPos.Y + Main.rand.NextFloat(BoxH)),
                    currentMusic.Style
                ));
            }

            if (stateTimer >= currentMusic.DisplayDuration) {
                currentState = AnimationState.SlideOut;
                stateTimer = 0;
            }
        }

        private void UpdateSlideOut() {
            float t = stateTimer / (float)SlideOutDuration;
            t = VaultUtils.EaseInCubic(t);
            slideProgress = 1f - t;
            alpha = 1f - t * 0.7f;

            if (stateTimer >= SlideOutDuration) {
                currentMusic?.OnComplete?.Invoke();
                currentMusic = null;
                currentState = AnimationState.SlideIn;
                stateTimer = 0;
            }
        }

        private void UpdateParticles() {
            for (int i = particles.Count - 1; i >= 0; i--) {
                if (particles[i].Update()) {
                    particles.RemoveAt(i);
                }
            }
        }

        private void UpdateAudioLevels() {
            for (int i = 0; i < audioLevels.Length; i++) {
                float target = (float)Math.Sin(wavePhase + i * 0.2f) * 0.5f + 0.5f;
                target *= Main.rand.NextFloat(0.6f, 1f);
                audioLevels[i] = MathHelper.Lerp(audioLevels[i], target, 0.3f);
            }
        }

        private void UpdateSpectrum() {
            for (int i = 0; i < spectrumBars.Length; i++) {
                float freq = i / (float)spectrumBars.Length;
                float target = (float)Math.Sin(wavePhase * 1.5f + freq * MathHelper.TwoPi) * 0.5f + 0.5f;
                target *= Main.rand.NextFloat(0.5f, 1f);
                spectrumBars[i] = MathHelper.Lerp(spectrumBars[i], target, 0.4f);
            }
        }

        private Vector2 GetCurrentPanelPosition() {
            float x = MathHelper.Lerp(OffscreenX, OnscreenX, slideProgress);
            float y = currentMusic?.ScreenYProvider?.Invoke() ?? ScreenY;
            return new Vector2(x, y);
        }
        #endregion

        #region 绘制逻辑
        public override void Draw(SpriteBatch spriteBatch) {
            if (currentMusic == null || slideProgress <= 0.01f) return;

            Vector2 panelPos = GetCurrentPanelPosition();
            Rectangle panelRect = new Rectangle((int)panelPos.X, (int)panelPos.Y, (int)currentPanelWidth, (int)BoxH);

            switch (currentMusic.Style) {
                case MusicStyle.Vinyl:
                    DrawVinylStyle(spriteBatch, panelRect);
                    break;
                case MusicStyle.Digital:
                    DrawDigitalStyle(spriteBatch, panelRect);
                    break;
                case MusicStyle.Neon:
                    DrawNeonStyle(spriteBatch, panelRect);
                    break;
                case MusicStyle.RedNeon:
                    DrawRedNeonStyle(spriteBatch, panelRect);
                    break;
                case MusicStyle.WetInk:
                    DrawWetInkStyle(spriteBatch, panelRect);
                    break;
                case MusicStyle.Sakura:
                    DrawSakuraStyle(spriteBatch, panelRect);
                    break;
            }

            DrawContent(spriteBatch, panelRect);

            foreach (var particle in particles) {
                particle.Draw(spriteBatch, alpha);
            }
        }

        #region 黑胶唱片风格
        private void DrawVinylStyle(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            int segments = 15;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle r = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));

                Color dark = new Color(15, 10, 20);
                Color mid = new Color(30, 20, 35);
                Color c = Color.Lerp(dark, mid, t * 0.8f);
                c *= alpha;

                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }

            Color borderColor = new Color(180, 150, 120) * alpha;
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), borderColor * 0.8f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.9f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.9f);

            DrawVinylGrooves(spriteBatch, rect);
        }

        private void DrawVinylGrooves(SpriteBatch sb, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            Vector2 center = new Vector2(rect.X + CoverPad + Cover / 2f, rect.Y + rect.Height / 2f);
            int grooveCount = 8;
            for (int i = 0; i < grooveCount; i++) {
                float radius = 25f + i * 3f;
                int segments = 32;
                Color grooveColor = new Color(80, 60, 70) * (alpha * 0.3f);

                for (int s = 0; s < segments; s++) {
                    float angle1 = s / (float)segments * MathHelper.TwoPi + vinylRotation;
                    float angle2 = (s + 1) / (float)segments * MathHelper.TwoPi + vinylRotation;

                    Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * radius;
                    Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * radius;

                    Vector2 diff = p2 - p1;
                    float len = diff.Length();
                    if (len > 0.01f) {
                        float rot = diff.ToRotation();
                        sb.Draw(px, p1, new Rectangle(0, 0, 1, 1), grooveColor, rot, Vector2.Zero, new Vector2(len, 0.5f), SpriteEffects.None, 0f);
                    }
                }
            }
        }
        #endregion

        #region 数字音波风格
        private void DrawDigitalStyle(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            Color bgDark = new Color(5, 15, 25) * alpha;
            Color bgLight = new Color(10, 25, 40) * alpha;

            int segments = 20;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle r = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));

                Color c = Color.Lerp(bgDark, bgLight, t);
                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }

            DrawDigitalGrid(spriteBatch, rect);

            Color borderColor = new Color(0, 180, 255) * alpha;
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), borderColor * 0.7f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.85f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.85f);

            DrawAudioWaveform(spriteBatch, rect, new Color(0, 200, 255) * alpha);
        }

        private void DrawDigitalGrid(SpriteBatch sb, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color gridColor = new Color(20, 80, 120) * (alpha * 0.1f);

            for (int i = 0; i < 8; i++) {
                int x = rect.X + (int)(i / 8f * rect.Width);
                sb.Draw(px, new Rectangle(x, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), gridColor);
            }

            for (int i = 0; i < 4; i++) {
                int y = rect.Y + (int)(i / 4f * rect.Height);
                sb.Draw(px, new Rectangle(rect.X, y, rect.Width, 1), new Rectangle(0, 0, 1, 1), gridColor);
            }
        }

        private void DrawAudioWaveform(SpriteBatch sb, Rectangle rect, Color waveColor) {
            Texture2D px = VaultAsset.placeholder2.Value;

            float startX = rect.X + TextX;
            float endX = rect.Right - TextPadR;
            float centerY = rect.Y + rect.Height - 15f * LayoutScale;
            float maxHeight = 10f * LayoutScale;

            for (int i = 0; i < audioLevels.Length - 1; i++) {
                float t = i / (float)(audioLevels.Length - 1);
                float x1 = MathHelper.Lerp(startX, endX, t);
                float x2 = MathHelper.Lerp(startX, endX, (i + 1) / (float)(audioLevels.Length - 1));

                float h1 = audioLevels[i] * maxHeight;
                float h2 = audioLevels[i + 1] * maxHeight;

                Vector2 p1 = new Vector2(x1, centerY - h1);
                Vector2 p2 = new Vector2(x2, centerY - h2);

                Vector2 diff = p2 - p1;
                float len = diff.Length();
                if (len > 0.01f) {
                    float rot = diff.ToRotation();
                    sb.Draw(px, p1, new Rectangle(0, 0, 1, 1), waveColor, rot, Vector2.Zero, new Vector2(len, 1.5f), SpriteEffects.None, 0f);
                }
            }
        }
        #endregion

        #region 湿墨冷青（鬼湖）
        //色值对齐 ShenyoMenuTheme：天穹底、潮雾、溺月、径流水光
        private static readonly Color WetInkDeep = new(5, 7, 9);
        private static readonly Color WetInkMurk = new(20, 26, 30);
        private static readonly Color WetInkMoon = new(196, 214, 218);
        private static readonly Color WetInkWater = new(136, 202, 216);
        private static readonly Color WetInkPale = new(222, 232, 236);
        private static readonly Color WetInkMist = new(118, 144, 152);

        private void DrawWetInkStyle(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            int segments = 16;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle r = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));
                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1),
                    Color.Lerp(WetInkDeep, WetInkMurk, t) * (alpha * 0.88f));
            }

            Color hair = WetInkMoon * (alpha * 0.38f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Rectangle(0, 0, 1, 1), hair);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Rectangle(0, 0, 1, 1), hair * 0.7f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), hair * 0.85f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), hair * 0.55f);

            DrawAudioWaveform(spriteBatch, rect, WetInkWater * (alpha * 0.42f));
        }
        #endregion

        #region 夜樱朱红
        //色值对齐 HimayoMenuTheme：夜底、灯笼朱红、樱瓣、象牙正文
        private static readonly Color SakuraDeep = new(12, 6, 8);
        private static readonly Color SakuraMurk = new(36, 14, 18);
        private static readonly Color SakuraCrimson = new(158, 32, 46);
        private static readonly Color SakuraBloom = new(255, 176, 196);
        private static readonly Color SakuraIvory = new(240, 232, 238);
        private static readonly Color SakuraPetal = new(250, 178, 194);

        private void DrawSakuraStyle(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            int segments = 16;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle r = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1));
                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1),
                    Color.Lerp(SakuraDeep, SakuraMurk, t) * (alpha * 0.88f));
            }

            Color hair = SakuraCrimson * (alpha * 0.55f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 1), new Rectangle(0, 0, 1, 1), hair);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), new Rectangle(0, 0, 1, 1), hair * 0.7f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), hair * 0.85f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), hair * 0.5f);

            DrawAudioWaveform(spriteBatch, rect, SakuraBloom * (alpha * 0.40f));
        }
        #endregion

        #region 霓虹光谱风格
        private void DrawNeonStyle(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            Color bgColor = new Color(10, 5, 15) * alpha;
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), bgColor);

            DrawSpectrumBars(spriteBatch, rect);

            float pulse = (float)Math.Sin(pulsePhase) * 0.5f + 0.5f;
            Color neonPink = Color.Lerp(new Color(255, 0, 150), new Color(255, 100, 200), pulse) * alpha;
            Color neonCyan = Color.Lerp(new Color(0, 255, 255), new Color(100, 255, 255), pulse) * alpha;

            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonPink);
            spriteBatch.Draw(px, new Rectangle(rect.X + rect.Width / 2, rect.Y, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonCyan);

            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonPink * 0.7f);
            spriteBatch.Draw(px, new Rectangle(rect.X + rect.Width / 2, rect.Bottom - 3, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonCyan * 0.7f);

            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), neonPink * 0.85f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), neonCyan * 0.85f);

            DrawNeonGlow(spriteBatch, rect, neonPink, neonCyan);
        }

        private void DrawSpectrumBars(SpriteBatch sb, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            float barWidth = 4f;
            float spacing = 2f;
            float startX = rect.X + TextX;
            float bottomY = rect.Y + rect.Height - 10f;
            float maxBarHeight = 20f;

            for (int i = 0; i < spectrumBars.Length; i++) {
                float x = startX + i * (barWidth + spacing);
                if (x + barWidth > rect.Right - TextPadR) break;

                float height = spectrumBars[i] * maxBarHeight;

                float hue = (i / (float)spectrumBars.Length + pulsePhase * 0.1f) % 1f;
                Color barColor = Main.hslToRgb(hue, 1f, 0.6f) * alpha;

                Rectangle barRect = new Rectangle((int)x, (int)(bottomY - height), (int)barWidth, (int)height);
                sb.Draw(px, barRect, new Rectangle(0, 0, 1, 1), barColor);

                sb.Draw(px, barRect, new Rectangle(0, 0, 1, 1), barColor * 0.3f);
            }
        }

        private void DrawNeonGlow(SpriteBatch sb, Rectangle rect, Color color1, Color color2) {
            Texture2D px = VaultAsset.placeholder2.Value;

            int glowSize = 6;
            for (int i = 0; i < glowSize; i++) {
                float offset = i + 1;
                float intensity = (1f - i / (float)glowSize) * 0.2f;

                Rectangle glowRect = new Rectangle(
                    rect.X - (int)offset,
                    rect.Y - (int)offset,
                    rect.Width + (int)(offset * 2),
                    rect.Height + (int)(offset * 2)
                );

                Color c1 = color1 * intensity;
                Color c2 = color2 * intensity;

                sb.Draw(px, new Rectangle(glowRect.X, glowRect.Y, glowRect.Width / 2, 1), new Rectangle(0, 0, 1, 1), c1);
                sb.Draw(px, new Rectangle(glowRect.X + glowRect.Width / 2, glowRect.Y, glowRect.Width / 2, 1), new Rectangle(0, 0, 1, 1), c2);

                sb.Draw(px, new Rectangle(glowRect.X, glowRect.Y, 1, glowRect.Height), new Rectangle(0, 0, 1, 1), c1);
                sb.Draw(px, new Rectangle(glowRect.Right, glowRect.Y, 1, glowRect.Height), new Rectangle(0, 0, 1, 1), c2);
            }
        }
        #endregion

        #region 红色霓虹光谱风格
        private void DrawRedNeonStyle(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            Color bgColor = new Color(10, 5, 15) * alpha;
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), bgColor);

            DrawRedSpectrumBars(spriteBatch, rect);

            float pulse = (float)Math.Sin(pulsePhase) * 0.5f + 0.5f;
            Color neonRed = Color.Lerp(new Color(255, 0, 0), new Color(255, 100, 100), pulse) * alpha;
            Color neonWhite = Color.Lerp(new Color(255, 255, 255), new Color(255, 255, 200), pulse) * alpha;

            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonRed);
            spriteBatch.Draw(px, new Rectangle(rect.X + rect.Width / 2, rect.Y, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonWhite);

            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonRed * 0.7f);
            spriteBatch.Draw(px, new Rectangle(rect.X + rect.Width / 2, rect.Bottom - 3, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonWhite * 0.7f);

            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), neonRed * 0.85f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), neonWhite * 0.85f);

            DrawNeonGlow(spriteBatch, rect, neonRed, neonWhite);
        }

        private void DrawRedSpectrumBars(SpriteBatch sb, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            float barWidth = 4f;
            float spacing = 2f;
            float startX = rect.X + TextX;
            float bottomY = rect.Y + rect.Height - 10f;
            float maxBarHeight = 20f;

            for (int i = 0; i < spectrumBars.Length; i++) {
                float x = startX + i * (barWidth + spacing);
                if (x + barWidth > rect.Right - TextPadR) break;

                float height = spectrumBars[i] * maxBarHeight;

                float t = (i / (float)spectrumBars.Length + pulsePhase * 0.1f) % 1f;
                Color barColor;

                if (t < 0.33f) {
                    float localT = t / 0.33f;
                    barColor = Color.Lerp(new Color(180, 0, 0), new Color(255, 0, 0), localT);
                }
                else if (t < 0.66f) {
                    float localT = (t - 0.33f) / 0.33f;
                    barColor = Color.Lerp(new Color(255, 0, 0), new Color(255, 80, 0), localT);
                }
                else {
                    float localT = (t - 0.66f) / 0.34f;
                    barColor = Color.Lerp(new Color(255, 80, 0), new Color(255, 150, 50), localT);
                }

                barColor *= alpha;

                Rectangle barRect = new Rectangle((int)x, (int)(bottomY - height), (int)barWidth, (int)height);
                sb.Draw(px, barRect, new Rectangle(0, 0, 1, 1), barColor);

                sb.Draw(px, barRect, new Rectangle(0, 0, 1, 1), barColor * 0.4f);
            }
        }
        #endregion

        private void DrawContent(SpriteBatch spriteBatch, Rectangle rect) {
            var font = FontAssets.MouseText.Value;

            Vector2 albumPos = new Vector2(rect.X + CoverPad + Cover / 2f, rect.Y + rect.Height / 2f);

            if (currentMusic.AlbumCover != null) {
                Texture2D album = currentMusic.AlbumCover;
                float albumScale = Math.Min(Cover / album.Width, Cover / album.Height);

                float rotation = currentMusic.Style == MusicStyle.Vinyl ? vinylRotation : 0f;
                Color albumTint = currentMusic.Style switch {
                    MusicStyle.WetInk => WetInkMist * alpha,
                    MusicStyle.Sakura => SakuraPetal * alpha,
                    _ => Color.White * alpha
                };
                spriteBatch.Draw(album, albumPos, null, albumTint, rotation, album.Size() / 2f, albumScale, SpriteEffects.None, 0f);
            }
            else {
                DrawDefaultMusicIcon(spriteBatch, albumPos);
            }

            float s = LayoutScale;
            Vector2 textStart = new Vector2(rect.X + TextX, rect.Y + 15f * s);

            Color textColor = currentMusic.Style switch {
                MusicStyle.Vinyl => new Color(220, 200, 180) * alpha,
                MusicStyle.Digital => new Color(0, 220, 255) * alpha,
                MusicStyle.Neon => new Color(255, 100, 255) * alpha,
                MusicStyle.RedNeon => new Color(255, 125, 75) * alpha,
                MusicStyle.WetInk => WetInkPale * alpha,
                MusicStyle.Sakura => SakuraIvory * alpha,
                _ => Color.White * alpha
            };

            string nowPlayingText = NowPlaying.Value;
            Utils.DrawBorderString(spriteBatch, nowPlayingText, textStart, textColor * 0.7f, 0.6f * s);

            Vector2 titlePos = textStart + new Vector2(0, 14f * s);
            float availableWidth = currentPanelWidth - TextX - TextPadR;
            Vector2 titleSize = ResolveTitleSize(availableWidth);

            if (HasTitleTexture) {
                Texture2D titleTex = currentMusic.TitleTexture;
                float texScale = titleSize.X / Math.Max(titleTex.Width, 1);
                Color titleTint = currentMusic.Style switch {
                    MusicStyle.WetInk => WetInkMoon * alpha,
                    //夜樱锁图自带金描，不再乘象牙色
                    MusicStyle.Sakura => Color.White * alpha,
                    _ => Color.White * alpha
                };
                spriteBatch.Draw(titleTex, titlePos, null, titleTint, 0f,
                    Vector2.Zero, texScale, SpriteEffects.None, 0f);
            }
            else {
                string titleText = currentMusic.Title ?? "Unknown Track";
                Vector2 measured = font.MeasureString(titleText) * (0.85f * s);
                if (measured.X > availableWidth) {
                    float scale = Math.Max(0.55f * s, availableWidth / measured.X * (0.85f * s));
                    Utils.DrawBorderString(spriteBatch, titleText, titlePos, textColor, scale);
                    titleSize = font.MeasureString(titleText) * scale;
                }
                else {
                    Utils.DrawBorderString(spriteBatch, titleText, titlePos, textColor, 0.85f * s);
                }
            }

            if (!string.IsNullOrEmpty(currentMusic.Artist)) {
                Vector2 artistPos = titlePos + new Vector2(0, titleSize.Y + 3);
                Vector2 artistSize = font.MeasureString(currentMusic.Artist) * (0.65f * s);

                if (artistSize.X > availableWidth) {
                    float scale = Math.Max(0.45f * s, availableWidth / artistSize.X * (0.65f * s));
                    Utils.DrawBorderString(spriteBatch, currentMusic.Artist, artistPos, textColor * 0.75f, scale);
                }
                else {
                    Utils.DrawBorderString(spriteBatch, currentMusic.Artist, artistPos, textColor * 0.75f, 0.65f * s);
                }
            }
        }

        private void DrawDefaultMusicIcon(SpriteBatch sb, Vector2 center) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color iconColor = currentMusic.Style switch {
                MusicStyle.WetInk => WetInkMoon,
                MusicStyle.Sakura => SakuraBloom,
                _ => Color.White
            } * alpha;

            float noteSize = Cover * 0.6f;

            Rectangle stem = new Rectangle(
                (int)(center.X + noteSize * 0.15f),
                (int)(center.Y - noteSize * 0.3f),
                (int)(noteSize * 0.1f),
                (int)(noteSize * 0.5f)
            );
            sb.Draw(px, stem, new Rectangle(0, 0, 1, 1), iconColor);

            sb.Draw(px, center + new Vector2(0, noteSize * 0.2f), new Rectangle(0, 0, 1, 1), iconColor,
                MathHelper.PiOver4, new Vector2(0.5f), new Vector2(noteSize * 0.25f, noteSize * 0.2f), SpriteEffects.None, 0f);
        }
        #endregion

        #region 粒子类
        private class MusicParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Size;
            public Color Color;
            public MusicStyle Style;

            public MusicParticle(Vector2 startPos, MusicStyle style) {
                Style = style;
                Position = startPos;
                Velocity = new Vector2(Main.rand.NextFloat(0.5f, 2f), Main.rand.NextFloat(-1f, 1f));
                Life = 0f;
                MaxLife = Main.rand.NextFloat(40f, 80f);
                Size = Main.rand.NextFloat(1.5f, 3.5f);

                Color = style switch {
                    MusicStyle.Vinyl => Main.rand.Next(new Color[] {
                        new Color(180, 150, 120),
                        new Color(200, 170, 140),
                        Color.Wheat
                    }),
                    MusicStyle.Digital => Main.rand.Next(new Color[] {
                        new Color(0, 180, 255),
                        new Color(0, 220, 255),
                        Color.Cyan
                    }),
                    MusicStyle.WetInk => Main.rand.Next(new Color[] {
                        WetInkMoon,
                        WetInkMist,
                        WetInkPale
                    }),
                    MusicStyle.Sakura => Main.rand.Next(new Color[] {
                        SakuraBloom,
                        SakuraPetal,
                        SakuraIvory
                    }),
                    MusicStyle.Neon => Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.6f),
                    MusicStyle.RedNeon => Main.rand.Next(new Color[] {
                        new Color(255, 0, 0),
                        new Color(255, 80, 40),
                        new Color(255, 120, 60),
                        Color.OrangeRed
                    }),
                    _ => Color.White
                };
            }

            public bool Update() {
                Life++;
                Position += Velocity;
                Velocity.X *= 0.98f;
                Velocity.Y *= 0.95f;

                if (Style == MusicStyle.Neon) {
                    float hue = Life * 0.02f % 1f;
                    Color = Main.hslToRgb(hue, 1f, 0.6f);
                }
                else if (Style == MusicStyle.RedNeon) {
                    float t = Life * 0.03f % 1f;
                    if (t < 0.5f) {
                        Color = Color.Lerp(new Color(255, 0, 0), new Color(255, 100, 0), t * 2f);
                    }
                    else {
                        Color = Color.Lerp(new Color(255, 100, 0), new Color(255, 0, 0), (t - 0.5f) * 2f);
                    }
                }

                return Life >= MaxLife;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D px = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin((1f - t) * MathHelper.Pi);
                Color drawColor = Color * (fade * alpha * 0.6f);
                sb.Draw(px, Position, new Rectangle(0, 0, 1, 1), drawColor, 0f, new Vector2(0.5f), Size, SpriteEffects.None, 0f);
            }
        }
        #endregion
    }

    /// <summary>把局内 MusicToast 接到主菜单 Mod_MenuLoad 层；标题接管帧靠 DriveMenuOverlays 驱动</summary>
    internal class MusicToastMenuHost : UIHandle
    {
        public override LayersModeEnum LayersMode => LayersModeEnum.Mod_MenuLoad;
        public override float RenderPriority => 1.2f;
        public override bool Active => Main.gameMenu && MusicToast.Instance.Active;

        public override void MenuLogicUpdate() => MusicToast.Instance.LogicUpdate();

        public override void Draw(SpriteBatch spriteBatch) => MusicToast.Instance.Draw(spriteBatch);
    }
}
