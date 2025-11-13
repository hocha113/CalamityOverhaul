using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV
{
    internal abstract class DialogueBoxBase : UIHandle, ILocalizedModType
    {
        public class DialoguePreProcessArgs
        {
            public string Speaker;
            public string Content;
            public int Index;
            public int Total;
        }

        //移除静态事件，改为由场景主动设置预处理器
        public Action<DialoguePreProcessArgs> PreProcessor { get; set; }

        internal class DialogueSegment
        {
            public string Speaker;
            public string Content;
            public Action OnStart;
            public Action OnFinish;
        }
        public abstract string LocalizationCategory { get; }
        internal readonly Queue<DialogueSegment> queue = new();
        internal DialogueSegment current;
        protected string[] wrappedLines = Array.Empty<string>();
        protected int visibleCharCount = 0;
        protected int typeTimer = 0;
        protected const int TypeInterval = 2;
        protected bool fastMode = false;
        protected bool finishedCurrent = false;
        internal int playedCount = 0;
        protected Vector2 anchorPos;
        internal float panelHeight = 160f;
        protected virtual float MinHeight => 120f;
        protected virtual float MaxHeight => 480f;
        protected virtual int Padding => 18;
        protected virtual int LineSpacing => 6;
        protected abstract float PanelWidth { get; }
        internal float showProgress = 0f;
        internal float hideProgress = 0f;
        protected virtual float ShowDuration => 18f;
        protected virtual float HideDuration => 14f;
        internal bool closing = false;
        internal float contentFade = 0f;
        protected bool waitingForAdvance = false;
        protected int advanceBlinkTimer = 0;
        internal string lastSpeaker;
        internal float speakerSwitchProgress = 1f;
        protected float speakerSwitchSpeed = 0.14f;
        public override bool Active => current != null || queue.Count > 0 || showProgress > 0f && !closing;
        protected static LocalizedText ContinueHint;
        protected static LocalizedText FastHint;

        #region 全身立绘系统

        //全身立绘注册表
        protected static readonly Dictionary<string, FullBodyPortraitBase> fullBodyPortraits = new(StringComparer.Ordinal);

        //当前激活的全身立绘
        protected FullBodyPortraitBase activeFullBodyPortrait;

        /// <summary>
        /// 注册一个全身立绘实例
        /// </summary>
        /// <param name="key">立绘标识符</param>
        /// <param name="portrait">立绘实例</param>
        public static void RegisterFullBodyPortrait(string key, FullBodyPortraitBase portrait) {
            if (string.IsNullOrWhiteSpace(key) || portrait == null) {
                return;
            }

            if (fullBodyPortraits.ContainsKey(key)) {
                fullBodyPortraits[key] = portrait;
            }
            else {
                fullBodyPortraits.Add(key, portrait);
            }
        }

        /// <summary>
        /// 移除注册的全身立绘
        /// </summary>
        /// <param name="key">立绘标识符</param>
        public static void UnregisterFullBodyPortrait(string key) {
            if (fullBodyPortraits.ContainsKey(key)) {
                fullBodyPortraits.Remove(key);
            }
        }

        /// <summary>
        /// 显示全身立绘
        /// </summary>
        /// <param name="key">立绘标识符</param>
        /// <returns>是否成功显示</returns>
        public virtual bool ShowFullBodyPortrait(string key) {
            if (string.IsNullOrWhiteSpace(key)) {
                return false;
            }

            if (!fullBodyPortraits.TryGetValue(key, out var portrait)) {
                return false;
            }

            //停止当前立绘
            if (activeFullBodyPortrait != null && activeFullBodyPortrait != portrait) {
                activeFullBodyPortrait.EndPerformance();
            }

            //启动新立绘
            activeFullBodyPortrait = portrait;
            if (!portrait.Active) {
                portrait.Initialize(this);
            }
            portrait.StartPerformance();

            return true;
        }

        /// <summary>
        /// 隐藏当前全身立绘
        /// </summary>
        public virtual void HideFullBodyPortrait() {
            if (activeFullBodyPortrait != null) {
                activeFullBodyPortrait.EndPerformance();
            }
        }

        /// <summary>
        /// 获取当前激活的全身立绘
        /// </summary>
        public FullBodyPortraitBase GetActiveFullBodyPortrait() {
            return activeFullBodyPortrait;
        }

        /// <summary>
        /// 清理所有全身立绘
        /// </summary>
        public static void ClearAllFullBodyPortraits() {
            fullBodyPortraits.Clear();
        }

        #endregion

        public override void SetStaticDefaults() {
            ContinueHint = this.GetLocalization(nameof(ContinueHint), () => "继续");
            FastHint = this.GetLocalization(nameof(FastHint), () => "加速");
        }
        protected class PortraitData
        {
            public Texture2D Texture;
            public Color BaseColor = Color.White;
            public bool Silhouette;
            public float Fade;
            public float TargetFade;
        }
        protected static readonly Dictionary<string, PortraitData> portraits = new(StringComparer.Ordinal);
        protected float portraitFadeSpeed = 0.15f;
        protected const float PortraitWidth = 120f;
        protected const float PortraitInnerPadding = 8f;
        public static void RegisterPortrait(string speaker, string texturePath, Color? baseColor = null, bool silhouette = false) {
            if (string.IsNullOrWhiteSpace(speaker) || string.IsNullOrWhiteSpace(texturePath)) {
                return;
            }
            Texture2D tex;
            try {
                tex = ModContent.Request<Texture2D>(texturePath, ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
            } catch {
                return;
            }
            RegisterPortrait(speaker, tex, baseColor, silhouette);
        }
        public static void RegisterPortrait(string speaker, Texture2D texture, Color? baseColor = null, bool silhouette = false) {
            if (string.IsNullOrWhiteSpace(speaker)) {
                return;
            }
            if (!portraits.TryGetValue(speaker, out var pd)) {
                pd = new PortraitData();
                portraits.Add(speaker, pd);
            }
            pd.Texture = texture;
            pd.BaseColor = baseColor ?? Color.White;
            pd.Silhouette = silhouette;
            pd.Fade = 0f;
            pd.TargetFade = 0f;
        }
        public static void SetPortraitStyle(string speaker, Color? baseColor = null, bool? silhouette = null) {
            if (!portraits.TryGetValue(speaker, out var pd)) {
                return;
            }
            if (baseColor.HasValue) {
                pd.BaseColor = baseColor.Value;
            }
            if (silhouette.HasValue) {
                pd.Silhouette = silhouette.Value;
            }
        }
        public static void SetPortrait(string speaker, Texture2D texture, Color? baseColor = null, bool? silhouette = null) {
            RegisterPortrait(speaker, texture, baseColor, silhouette ?? false);
            SetPortraitStyle(speaker, baseColor, silhouette);
        }
        public virtual void EnqueueDialogue(string speaker, string content, Action onFinish = null, Action onStart = null) {
            queue.Enqueue(new DialogueSegment {
                Speaker = speaker,
                Content = content ?? string.Empty,
                OnStart = onStart,
                OnFinish = onFinish
            });
        }
        public virtual void ReplaceDialogue(IEnumerable<(string speaker, string content, Action callback)> segments) {
            queue.Clear();
            current = null;
            foreach (var seg in segments) {
                EnqueueDialogue(seg.speaker, seg.content, seg.callback);
            }
        }
        public virtual void BeginClose() {
            if (closing) {
                return;
            }

            //检查全身立绘是否阻止关闭
            if (activeFullBodyPortrait != null && activeFullBodyPortrait.BlockDialogueClose) {
                return;
            }

            closing = true;
            hideProgress = 0f;

            //关闭对话框时隐藏全身立绘
            HideFullBodyPortrait();
        }
        public virtual void StartNext() {
            if (queue.Count == 0) {
                BeginClose();
                playedCount = 0;
                return;
            }
            current = queue.Dequeue();

            //在处理对话之前触发 OnStart
            current?.OnStart?.Invoke();

            playedCount++;
            int index = playedCount - 1;
            int total = playedCount + queue.Count;
            if (current != null) {
                var args = new DialoguePreProcessArgs {
                    Speaker = current.Speaker,
                    Content = current.Content,
                    Index = index,
                    Total = total
                };
                //只调用当前设置的预处理器
                PreProcessor?.Invoke(args);
                current.Speaker = args.Speaker;
                current.Content = args.Content;
            }
            if (current != null) {
                if (current.Speaker != lastSpeaker) {
                    lastSpeaker = current.Speaker;
                    speakerSwitchProgress = 0f;
                }
            }
            WrapCurrent();
            visibleCharCount = 0;
            typeTimer = 0;
            fastMode = false;
            finishedCurrent = false;
            waitingForAdvance = false;
            if (playedCount <= 1 || showProgress < 1f) {
                contentFade = 0f;
            }
            else {
                contentFade = 1f;
            }
            if (current != null && !string.IsNullOrEmpty(current.Speaker) && portraits.TryGetValue(current.Speaker, out var pd2)) {
                foreach (var kv in portraits) {
                    kv.Value.TargetFade = kv.Key == current.Speaker ? 1f : 0f;
                }
            }
        }
        protected virtual void WrapCurrent() {
            if (current == null) {
                wrappedLines = [];
                return;
            }
            string raw = current.Content.Replace("\r", string.Empty);
            List<string> allLines = new();
            string[] manual = raw.Split('\n');
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            //计算可用宽度(考虑立绘与文字缩放)
            float textScale = 0.8f;
            float baseWidth = PanelWidth - Padding * 2 - 24f;
            bool hasPortrait = false;
            if (!string.IsNullOrEmpty(current.Speaker) && portraits.TryGetValue(current.Speaker, out var pd) && pd.Texture != null) {
                hasPortrait = true;
            }
            if (hasPortrait) {
                baseWidth -= PortraitWidth + 20f;
            }
            if (baseWidth < 60f) {
                baseWidth = 60f;
            }
            int wrapWidth = (int)(baseWidth / textScale);
            foreach (var block in manual) {
                if (string.IsNullOrWhiteSpace(block)) {
                    allLines.Add(string.Empty);
                    continue;
                }
                string[] lines = Utils.WordwrapString(block, font, wrapWidth, 9999, out int _);
                foreach (var l in lines) {
                    if (l == null) {
                        continue;
                    }
                    allLines.Add(l.TrimEnd('-', ' '));
                }
            }
            wrappedLines = [.. allLines];
            int textLines = wrappedLines.Length;
            int lineHeight = (int)(font.MeasureString("A").Y * 0.8f) + LineSpacing;

            int headerHeight = 38;

            float contentHeight = textLines * lineHeight + Padding * 2 + headerHeight;
            panelHeight = MathHelper.Clamp(contentHeight, MinHeight, MaxHeight);
        }
        public override void Update() { HandleInput(); }
        public new void LogicUpdate() {
            anchorPos = new Vector2(Main.screenWidth / 2f, Main.screenHeight - 140f);
            if (current == null && queue.Count > 0 && !closing) {
                StartNext();
            }
            if (!closing) {
                if (showProgress < 1f && (current != null || queue.Count > 0)) {
                    showProgress += 1f / ShowDuration;
                    showProgress = Math.Clamp(showProgress, 0f, 1f);
                }
            }
            else {
                if (hideProgress < 1f) {
                    hideProgress += 1f / HideDuration;
                    hideProgress = Math.Clamp(hideProgress, 0f, 1f);
                    if (hideProgress >= 1f) {
                        current = null;
                        queue.Clear();
                        closing = false;
                        showProgress = 0f;
                        lastSpeaker = null;
                        speakerSwitchProgress = 1f;

                        if (DialogueUIRegistry.Current == this) {
                            DialogueUIRegistry.SetResolver(null);
                        }
                    }
                }
            }
            if (current != null && !closing) {
                if (!finishedCurrent) {
                    typeTimer++;
                    int interval = fastMode ? 1 : TypeInterval;
                    if (typeTimer >= interval) {
                        typeTimer = 0;
                        visibleCharCount++;
                        int totalChars = current.Content.Length;
                        if (visibleCharCount >= totalChars) {
                            visibleCharCount = totalChars;
                            finishedCurrent = true;
                            waitingForAdvance = true;
                        }
                        else if (visibleCharCount % 4 == 0) {
                            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.45f });
                        }
                    }
                }
                else {
                    advanceBlinkTimer++;
                }
                if (contentFade < 1f) {
                    contentFade += 0.12f;
                }
            }
            if (speakerSwitchProgress < 1f) {
                speakerSwitchProgress += speakerSwitchSpeed;
                if (speakerSwitchProgress > 1f) {
                    speakerSwitchProgress = 1f;
                }
            }
            foreach (var kv in portraits) {
                var p = kv.Value;
                if (p.Texture == null) {
                    continue;
                }
                p.Fade = MathHelper.Lerp(p.Fade, p.TargetFade, portraitFadeSpeed);
                if (p.Fade < 0.01f && p.TargetFade == 0f) {
                    p.Fade = 0f;
                }
            }

            //更新全身立绘
            if (activeFullBodyPortrait != null) {
                activeFullBodyPortrait.Update();

                //如果立绘不再激活，清空引用
                if (!activeFullBodyPortrait.Active) {
                    activeFullBodyPortrait = null;
                }
            }

            Vector2 panelPos = anchorPos - new Vector2(PanelWidth / 2f, panelHeight);
            Vector2 panelSize = new(PanelWidth, panelHeight);
            StyleUpdate(panelPos, panelSize);
        }
        public Rectangle GetPanelRect() {
            if (!Active && showProgress <= 0f) {
                return Rectangle.Empty;
            }
            float progress = closing ? 1f - hideProgress : showProgress;
            if (progress <= 0f) {
                return Rectangle.Empty;
            }
            float eased = closing ? CWRUtils.EaseInCubic(progress) : CWRUtils.EaseOutBack(progress);
            float width = PanelWidth;
            float height = panelHeight;
            Vector2 panelOrigin = new(width / 2f, height);
            Vector2 drawPos = anchorPos - panelOrigin;
            drawPos.Y += (1f - eased) * 90f;
            Rectangle panelRect = new((int)drawPos.X, (int)drawPos.Y, (int)width, (int)height);
            return panelRect;
        }
        protected virtual void StyleUpdate(Vector2 panelPos, Vector2 panelSize) { }
        protected virtual void HandleInput() {
            if (current == null) {
                return;
            }
            if (closing) {
                return;
            }
            Rectangle panelRect = GetPanelRect();
            Point mouse = new Point(Main.mouseX, Main.mouseY);
            bool hover = panelRect.Contains(mouse);
            if (hover) {
                player.mouseInterface |= Active;
                if (keyLeftPressState == KeyPressState.Pressed) {
                    if (!finishedCurrent) {
                        visibleCharCount = current.Content.Length;
                        finishedCurrent = true;
                        waitingForAdvance = true;
                    }
                    else {
                        //检查全身立绘是否阻止推进
                        if (activeFullBodyPortrait != null && activeFullBodyPortrait.BlockDialogueAdvance) {
                            return;
                        }

                        current.OnFinish?.Invoke();
                        StartNext();
                    }
                }
            }

            if (keyRightPressState == KeyPressState.Pressed && hover) {
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = 0.2f });
            }
            fastMode = Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)
                || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift)
                || keyRightPressState == KeyPressState.Held;
        }
        public override void Draw(SpriteBatch spriteBatch) {
            if (showProgress <= 0.01f && !closing) {
                return;
            }
            float progress = closing ? 1f - hideProgress : showProgress;
            if (progress <= 0f) {
                return;
            }
            float eased = closing ? CWRUtils.EaseInCubic(progress) : CWRUtils.EaseOutBack(progress);
            float width = PanelWidth;
            float height = panelHeight;
            Vector2 panelOrigin = new(width / 2f, height);
            Vector2 drawPos = anchorPos - panelOrigin;
            drawPos.Y += (1f - eased) * 90f;
            Rectangle panelRect = new((int)drawPos.X, (int)drawPos.Y, (int)width, (int)height);
            float alpha = progress;
            float contentAlpha = contentFade * alpha;

            //绘制全身立绘(在对话框之前绘制，作为背景层)
            if (activeFullBodyPortrait != null) {
                activeFullBodyPortrait.Draw(spriteBatch, alpha);
            }

            DrawStyle(spriteBatch, panelRect, alpha, contentAlpha, eased);
        }
        protected abstract void DrawStyle(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha, float easedProgress);
    }
}
