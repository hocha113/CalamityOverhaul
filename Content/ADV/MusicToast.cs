using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV
{
    /// <summary>
    /// 音乐展示框系统，从屏幕左下角弹出显示
    /// </summary>
    internal class MusicToast : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";

        public static MusicToast Instance => UIHandleLoader.GetUIHandleOfType<MusicToast>();

        /// <summary>
        /// 音乐展示风格枚举
        /// </summary>
        public enum MusicStyle
        {
            Vinyl,      //黑胶唱片风格
            Digital,    //数字音波风格
            Neon,       //霓虹光谱风格
            RedNeon     //红色霓虹光谱风格
        }

        /// <summary>
        /// 音乐数据类
        /// </summary>
        public class MusicInfo
        {
            public Texture2D AlbumCover { get; set; }
            public string Title { get; set; }
            public string Artist { get; set; }
            public MusicStyle Style { get; set; } = MusicStyle.Vinyl;
            public Action OnComplete { get; set; }
            public int DisplayDuration { get; set; } = 300; //默认5秒 (60fps * 5)
        }

        #region 数据字段
        private readonly Queue<MusicInfo> musicQueue = new();
        private MusicInfo currentMusic;

        //动画状态机
        private enum AnimationState
        {
            SlideIn,    //滑入
            Display,    //展示
            SlideOut    //滑出
        }
        private AnimationState currentState = AnimationState.SlideIn;
        private int stateTimer = 0;

        //动画参数
        private const int SlideInDuration = 35;      //滑入时长
        private const int SlideOutDuration = 30;     //滑出时长

        private float slideProgress = 0f;            //滑动进度 0-1
        private float alpha = 1f;                    //透明度
        private float pulsePhase = 0f;               //脉冲相位
        private float wavePhase = 0f;                //波形相位

        //面板参数
        private const float MinPanelWidth = 320f;
        private const float MaxPanelWidth = 550f;
        private const float PanelHeight = 90f;
        private const float AlbumSize = 70f;         //专辑封面大小
        private const float AlbumPadding = 10f;      //专辑封面左边距
        private const float TextStartX = 95f;        //文字起始X
        private const float TextPaddingRight = 15f;  //文字右边距

        private float currentPanelWidth = MinPanelWidth;
        private float OffscreenX => -currentPanelWidth - 50f;
        private const float OnscreenX = 15f;
        private static float ScreenY => Main.screenHeight - PanelHeight - 120f;//左下角位置

        //音波可视化
        private readonly float[] audioLevels = new float[32];
        private int audioUpdateTimer = 0;

        //旋转效果（黑胶唱片）
        private float vinylRotation = 0f;

        //粒子系统
        private readonly List<MusicParticle> particles = new();
        private int particleSpawnTimer = 0;

        //光谱条
        private readonly float[] spectrumBars = new float[16];
        private int spectrumUpdateTimer = 0;

        //本地化文本
        protected static LocalizedText NowPlaying;
        #endregion

        public override bool Active => currentMusic != null || musicQueue.Count > 0 || slideProgress > 0.01f;

        public override void SetStaticDefaults() {
            NowPlaying = this.GetLocalization(nameof(NowPlaying), () => "正在播放");
        }

        #region 公共API
        /// <summary>
        /// 显示音乐信息
        /// </summary>
        public static void ShowMusic(string title, string artist = null, Texture2D albumCover = null,
            MusicStyle style = MusicStyle.Vinyl, int displayDuration = 300, Action onComplete = null) {
            var music = new MusicInfo {
                Title = title,
                Artist = artist,
                AlbumCover = albumCover,
                Style = style,
                DisplayDuration = displayDuration,
                OnComplete = onComplete
            };
            Instance.musicQueue.Enqueue(music);
        }

        /// <summary>
        /// 清空音乐队列
        /// </summary>
        public static void ClearQueue() {
            Instance.musicQueue.Clear();
        }
        #endregion

        #region 更新逻辑
        public override void LogicUpdate() {
            //动画计时器更新
            pulsePhase += 0.04f;
            wavePhase += 0.06f;
            vinylRotation += 0.05f;

            if (pulsePhase > MathHelper.TwoPi) pulsePhase -= MathHelper.TwoPi;
            if (wavePhase > MathHelper.TwoPi) wavePhase -= MathHelper.TwoPi;
            if (vinylRotation > MathHelper.TwoPi) vinylRotation -= MathHelper.TwoPi;

            //更新音波数据
            audioUpdateTimer++;
            if (audioUpdateTimer >= 3) {
                audioUpdateTimer = 0;
                UpdateAudioLevels();
            }

            //更新光谱
            spectrumUpdateTimer++;
            if (spectrumUpdateTimer >= 2) {
                spectrumUpdateTimer = 0;
                UpdateSpectrum();
            }

            //如果没有当前音乐但队列有，开始下一个
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

            //计算面板宽度
            CalculatePanelWidth();
        }

        private void CalculatePanelWidth() {
            var font = FontAssets.MouseText.Value;

            string titleText = currentMusic.Title ?? "Unknown Track";
            Vector2 titleSize = font.MeasureString(titleText) * 0.85f;

            float maxTextWidth = titleSize.X;

            if (!string.IsNullOrEmpty(currentMusic.Artist)) {
                Vector2 artistSize = font.MeasureString(currentMusic.Artist) * 0.65f;
                maxTextWidth = Math.Max(maxTextWidth, artistSize.X);
            }

            //总宽度 = 文字起始X + 文字宽度 + 右边距
            float requiredWidth = TextStartX + maxTextWidth + TextPaddingRight;

            //限制在最小和最大宽度之间
            currentPanelWidth = Math.Clamp(requiredWidth, MinPanelWidth, MaxPanelWidth);
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
            t = CWRUtils.EaseOutCubic(t);
            slideProgress = t;

            if (stateTimer >= SlideInDuration) {
                currentState = AnimationState.Display;
                stateTimer = 0;
            }
        }

        private void UpdateDisplay() {
            //持续生成粒子
            particleSpawnTimer++;
            if (particleSpawnTimer >= 8) {
                particleSpawnTimer = 0;
                Vector2 panelPos = GetCurrentPanelPosition();
                particles.Add(new MusicParticle(
                    new Vector2(panelPos.X + currentPanelWidth, panelPos.Y + Main.rand.NextFloat(PanelHeight)),
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
            t = CWRUtils.EaseInCubic(t);
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
            //模拟音波数据
            for (int i = 0; i < audioLevels.Length; i++) {
                float target = (float)Math.Sin(wavePhase + i * 0.2f) * 0.5f + 0.5f;
                target *= Main.rand.NextFloat(0.6f, 1f);
                audioLevels[i] = MathHelper.Lerp(audioLevels[i], target, 0.3f);
            }
        }

        private void UpdateSpectrum() {
            //模拟频谱数据
            for (int i = 0; i < spectrumBars.Length; i++) {
                float freq = i / (float)spectrumBars.Length;
                float target = (float)Math.Sin(wavePhase * 1.5f + freq * MathHelper.TwoPi) * 0.5f + 0.5f;
                target *= Main.rand.NextFloat(0.5f, 1f);
                spectrumBars[i] = MathHelper.Lerp(spectrumBars[i], target, 0.4f);
            }
        }

        private Vector2 GetCurrentPanelPosition() {
            float x = MathHelper.Lerp(OffscreenX, OnscreenX, slideProgress);
            return new Vector2(x, ScreenY);
        }
        #endregion

        #region 绘制逻辑
        public override void Draw(SpriteBatch spriteBatch) {
            if (currentMusic == null || slideProgress <= 0.01f) return;

            Vector2 panelPos = GetCurrentPanelPosition();
            Rectangle panelRect = new Rectangle((int)panelPos.X, (int)panelPos.Y, (int)currentPanelWidth, (int)PanelHeight);

            //根据风格绘制背景
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
            }

            //绘制内容
            DrawContent(spriteBatch, panelRect);

            //绘制粒子
            foreach (var particle in particles) {
                particle.Draw(spriteBatch, alpha);
            }
        }

        #region 黑胶唱片风格
        private void DrawVinylStyle(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //深色背景渐变
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

            //复古质感边框
            Color borderColor = new Color(180, 150, 120) * alpha;
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), borderColor * 0.8f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.9f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.9f);

            //音波纹理
            DrawVinylGrooves(spriteBatch, rect);
        }

        private void DrawVinylGrooves(SpriteBatch sb, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //绘制同心圆纹路
            Vector2 center = new Vector2(rect.X + AlbumPadding + AlbumSize / 2f, rect.Y + rect.Height / 2f);
            int grooveCount = 8;
            for (int i = 0; i < grooveCount; i++) {
                float radius = 25f + i * 3f;
                int segments = 32;
                Color grooveColor = new Color(80, 60, 70) * (alpha * 0.3f);

                for (int s = 0; s < segments; s++) {
                    float angle1 = (s / (float)segments) * MathHelper.TwoPi + vinylRotation;
                    float angle2 = ((s + 1) / (float)segments) * MathHelper.TwoPi + vinylRotation;

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

            //深色科技背景
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

            //网格线
            DrawDigitalGrid(spriteBatch, rect);

            //科技边框
            Color borderColor = new Color(0, 180, 255) * alpha;
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), borderColor * 0.7f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.85f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.85f);

            //音波可视化
            DrawAudioWaveform(spriteBatch, rect);
        }

        private void DrawDigitalGrid(SpriteBatch sb, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color gridColor = new Color(20, 80, 120) * (alpha * 0.1f);

            //垂直网格线
            for (int i = 0; i < 8; i++) {
                int x = rect.X + (int)((i / 8f) * rect.Width);
                sb.Draw(px, new Rectangle(x, rect.Y, 1, rect.Height), new Rectangle(0, 0, 1, 1), gridColor);
            }

            //水平网格线
            for (int i = 0; i < 4; i++) {
                int y = rect.Y + (int)((i / 4f) * rect.Height);
                sb.Draw(px, new Rectangle(rect.X, y, rect.Width, 1), new Rectangle(0, 0, 1, 1), gridColor);
            }
        }

        private void DrawAudioWaveform(SpriteBatch sb, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            float startX = rect.X + TextStartX;
            float endX = rect.Right - TextPaddingRight;
            float centerY = rect.Y + rect.Height - 15f;
            float maxHeight = 10f;

            Color waveColor = new Color(0, 200, 255) * alpha;

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

        #region 霓虹光谱风格
        private void DrawNeonStyle(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //深色背景
            Color bgColor = new Color(10, 5, 15) * alpha;
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), bgColor);

            //霓虹光谱条
            DrawSpectrumBars(spriteBatch, rect);

            //发光边框
            float pulse = (float)Math.Sin(pulsePhase) * 0.5f + 0.5f;
            Color neonPink = Color.Lerp(new Color(255, 0, 150), new Color(255, 100, 200), pulse) * alpha;
            Color neonCyan = Color.Lerp(new Color(0, 255, 255), new Color(100, 255, 255), pulse) * alpha;

            //渐变边框
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonPink);
            spriteBatch.Draw(px, new Rectangle(rect.X + rect.Width / 2, rect.Y, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonCyan);

            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonPink * 0.7f);
            spriteBatch.Draw(px, new Rectangle(rect.X + rect.Width / 2, rect.Bottom - 3, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonCyan * 0.7f);

            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), neonPink * 0.85f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), neonCyan * 0.85f);

            //外发光
            DrawNeonGlow(spriteBatch, rect, neonPink, neonCyan);
        }

        private void DrawSpectrumBars(SpriteBatch sb, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            float barWidth = 4f;
            float spacing = 2f;
            float startX = rect.X + TextStartX;
            float bottomY = rect.Y + rect.Height - 10f;
            float maxBarHeight = 20f;

            for (int i = 0; i < spectrumBars.Length; i++) {
                float x = startX + i * (barWidth + spacing);
                if (x + barWidth > rect.Right - TextPaddingRight) break;

                float height = spectrumBars[i] * maxBarHeight;

                //渐变颜色
                float hue = (i / (float)spectrumBars.Length + pulsePhase * 0.1f) % 1f;
                Color barColor = Main.hslToRgb(hue, 1f, 0.6f) * alpha;

                Rectangle barRect = new Rectangle((int)x, (int)(bottomY - height), (int)barWidth, (int)height);
                sb.Draw(px, barRect, new Rectangle(0, 0, 1, 1), barColor);

                //发光效果
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

                //上下
                sb.Draw(px, new Rectangle(glowRect.X, glowRect.Y, glowRect.Width / 2, 1), new Rectangle(0, 0, 1, 1), c1);
                sb.Draw(px, new Rectangle(glowRect.X + glowRect.Width / 2, glowRect.Y, glowRect.Width / 2, 1), new Rectangle(0, 0, 1, 1), c2);

                //左右
                sb.Draw(px, new Rectangle(glowRect.X, glowRect.Y, 1, glowRect.Height), new Rectangle(0, 0, 1, 1), c1);
                sb.Draw(px, new Rectangle(glowRect.Right, glowRect.Y, 1, glowRect.Height), new Rectangle(0, 0, 1, 1), c2);
            }
        }
        #endregion

        #region 红色霓虹光谱风格
        private void DrawRedNeonStyle(SpriteBatch spriteBatch, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            //深色背景
            Color bgColor = new Color(10, 5, 15) * alpha;
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), bgColor);

            //霓虹光谱条
            DrawRedSpectrumBars(spriteBatch, rect);

            //发光边框
            float pulse = (float)Math.Sin(pulsePhase) * 0.5f + 0.5f;
            Color neonRed = Color.Lerp(new Color(255, 0, 0), new Color(255, 100, 100), pulse) * alpha;
            Color neonWhite = Color.Lerp(new Color(255, 255, 255), new Color(255, 255, 200), pulse) * alpha;

            //渐变边框
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonRed);
            spriteBatch.Draw(px, new Rectangle(rect.X + rect.Width / 2, rect.Y, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonWhite);

            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 3, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonRed * 0.7f);
            spriteBatch.Draw(px, new Rectangle(rect.X + rect.Width / 2, rect.Bottom - 3, rect.Width / 2, 3), new Rectangle(0, 0, 1, 1), neonWhite * 0.7f);

            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), neonRed * 0.85f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), neonWhite * 0.85f);

            //外发光
            DrawNeonGlow(spriteBatch, rect, neonRed, neonWhite);
        }

        private void DrawRedSpectrumBars(SpriteBatch sb, Rectangle rect) {
            Texture2D px = VaultAsset.placeholder2.Value;

            float barWidth = 4f;
            float spacing = 2f;
            float startX = rect.X + TextStartX;
            float bottomY = rect.Y + rect.Height - 10f;
            float maxBarHeight = 20f;

            for (int i = 0; i < spectrumBars.Length; i++) {
                float x = startX + i * (barWidth + spacing);
                if (x + barWidth > rect.Right - TextPaddingRight) break;

                float height = spectrumBars[i] * maxBarHeight;

                //红色系渐变 - 从深红到亮红到橙红
                float t = (i / (float)spectrumBars.Length + pulsePhase * 0.1f) % 1f;
                Color barColor;

                if (t < 0.33f) {
                    //深红 -> 鲜红
                    float localT = t / 0.33f;
                    barColor = Color.Lerp(new Color(180, 0, 0), new Color(255, 0, 0), localT);
                }
                else if (t < 0.66f) {
                    //鲜红 -> 橙红
                    float localT = (t - 0.33f) / 0.33f;
                    barColor = Color.Lerp(new Color(255, 0, 0), new Color(255, 80, 0), localT);
                }
                else {
                    //橙红 -> 亮橙
                    float localT = (t - 0.66f) / 0.34f;
                    barColor = Color.Lerp(new Color(255, 80, 0), new Color(255, 150, 50), localT);
                }

                barColor *= alpha;

                Rectangle barRect = new Rectangle((int)x, (int)(bottomY - height), (int)barWidth, (int)height);
                sb.Draw(px, barRect, new Rectangle(0, 0, 1, 1), barColor);

                //发光效果
                sb.Draw(px, barRect, new Rectangle(0, 0, 1, 1), barColor * 0.4f);
            }
        }
        #endregion

        private void DrawContent(SpriteBatch spriteBatch, Rectangle rect) {
            var font = FontAssets.MouseText.Value;

            //绘制专辑封面或音乐图标
            Vector2 albumPos = new Vector2(rect.X + AlbumPadding + AlbumSize / 2f, rect.Y + rect.Height / 2f);

            if (currentMusic.AlbumCover != null) {
                Texture2D album = currentMusic.AlbumCover;
                float albumScale = Math.Min(AlbumSize / album.Width, AlbumSize / album.Height);

                //黑胶唱片风格：旋转封面
                float rotation = currentMusic.Style == MusicStyle.Vinyl ? vinylRotation : 0f;
                spriteBatch.Draw(album, albumPos, null, Color.White * alpha, rotation, album.Size() / 2f, albumScale, SpriteEffects.None, 0f);
            }
            else {
                //默认音符图标
                DrawDefaultMusicIcon(spriteBatch, albumPos);
            }

            //绘制文字
            Vector2 textStart = new Vector2(rect.X + TextStartX, rect.Y + 15);

            Color textColor = currentMusic.Style switch {
                MusicStyle.Vinyl => new Color(220, 200, 180) * alpha,
                MusicStyle.Digital => new Color(0, 220, 255) * alpha,
                MusicStyle.Neon => new Color(255, 100, 255) * alpha,
                MusicStyle.RedNeon => new Color(255, 125, 75) * alpha,
                _ => Color.White * alpha
            };

            //"正在播放" 标签
            string nowPlayingText = NowPlaying.Value;
            Utils.DrawBorderString(spriteBatch, nowPlayingText, textStart, textColor * 0.7f, 0.6f);

            //标题
            Vector2 titlePos = textStart + new Vector2(0, 14);
            string titleText = currentMusic.Title ?? "Unknown Track";

            float availableWidth = currentPanelWidth - TextStartX - TextPaddingRight;
            Vector2 titleSize = font.MeasureString(titleText) * 0.85f;

            if (titleSize.X > availableWidth) {
                float scale = Math.Max(0.55f, availableWidth / titleSize.X * 0.85f);
                Utils.DrawBorderString(spriteBatch, titleText, titlePos, textColor, scale);
                titleSize = font.MeasureString(titleText) * scale;
            }
            else {
                Utils.DrawBorderString(spriteBatch, titleText, titlePos, textColor, 0.85f);
            }

            //艺术家/作者
            if (!string.IsNullOrEmpty(currentMusic.Artist)) {
                Vector2 artistPos = titlePos + new Vector2(0, titleSize.Y + 3);
                Vector2 artistSize = font.MeasureString(currentMusic.Artist) * 0.65f;

                if (artistSize.X > availableWidth) {
                    float scale = Math.Max(0.45f, availableWidth / artistSize.X * 0.65f);
                    Utils.DrawBorderString(spriteBatch, currentMusic.Artist, artistPos, textColor * 0.75f, scale);
                }
                else {
                    Utils.DrawBorderString(spriteBatch, currentMusic.Artist, artistPos, textColor * 0.75f, 0.65f);
                }
            }
        }

        private void DrawDefaultMusicIcon(SpriteBatch sb, Vector2 center) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color iconColor = Color.White * alpha;

            //简单的音符图标
            float noteSize = AlbumSize * 0.6f;

            //音符柄
            Rectangle stem = new Rectangle(
                (int)(center.X + noteSize * 0.15f),
                (int)(center.Y - noteSize * 0.3f),
                (int)(noteSize * 0.1f),
                (int)(noteSize * 0.5f)
            );
            sb.Draw(px, stem, new Rectangle(0, 0, 1, 1), iconColor);

            //音符头
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

                //霓虹风格：颜色变化
                if (Style == MusicStyle.Neon) {
                    float hue = (Life * 0.02f) % 1f;
                    Color = Main.hslToRgb(hue, 1f, 0.6f);
                }
                else if (Style == MusicStyle.RedNeon) {
                    //红色霓虹：在红色范围内变化
                    float t = (Life * 0.03f) % 1f;
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
}
