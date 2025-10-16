﻿using InnoVault.UIHandles;
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

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV
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

        // 移除静态事件，改为由场景主动设置预处理器
        public Action<DialoguePreProcessArgs> PreProcessor { get; set; }

        internal class DialogueSegment
        {
            public string Speaker;
            public string Content;
            public Action OnStart;
            public Action OnFinish;
        }
        public abstract string LocalizationCategory { get; }
        protected readonly Queue<DialogueSegment> queue = new();
        protected DialogueSegment current;
        protected string[] wrappedLines = Array.Empty<string>();
        protected int visibleCharCount = 0;
        protected int typeTimer = 0;
        protected const int TypeInterval = 2;
        protected bool fastMode = false;
        protected bool finishedCurrent = false;
        protected int playedCount = 0;
        protected Vector2 anchorPos;
        protected float panelHeight = 160f;
        protected virtual float MinHeight => 120f;
        protected virtual float MaxHeight => 340f;
        protected virtual int Padding => 18;
        protected virtual int LineSpacing => 6;
        protected abstract float PanelWidth { get; }
        protected float showProgress = 0f;
        protected float hideProgress = 0f;
        protected virtual float ShowDuration => 18f;
        protected virtual float HideDuration => 14f;
        protected bool closing = false;
        protected float contentFade = 0f;
        protected bool waitingForAdvance = false;
        protected int advanceBlinkTimer = 0;
        protected string lastSpeaker;
        protected float speakerSwitchProgress = 1f;
        protected float speakerSwitchSpeed = 0.14f;
        public override bool Active => current != null || queue.Count > 0 || (showProgress > 0f && !closing);
        protected static LocalizedText ContinueHint;
        protected static LocalizedText FastHint;
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
        protected virtual void BeginClose() {
            if (closing) {
                return;
            }
            closing = true;
            hideProgress = 0f;
        }
        protected virtual void StartNext() {
            if (queue.Count == 0) {
                BeginClose();
                playedCount = 0;
                return;
            }
            current = queue.Dequeue();
            
            // 在处理对话之前触发 OnStart
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
                wrappedLines = Array.Empty<string>();
                return;
            }
            string raw = current.Content.Replace("\r", string.Empty);
            List<string> allLines = new();
            string[] manual = raw.Split('\n');
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            //计算可用宽度(考虑立绘与文字缩放)
            float textScale = 0.8f; //与绘制时保持一致
            float baseWidth = PanelWidth - Padding * 2 - 24f;
            bool hasPortrait = false;
            if (!string.IsNullOrEmpty(current.Speaker) && portraits.TryGetValue(current.Speaker, out var pd) && pd.Texture != null) {
                hasPortrait = true;
            }
            if (hasPortrait) {
                baseWidth -= (PortraitWidth + 20f); //与绘制 leftOffset 增量同步
            }
            if (baseWidth < 60f) {
                baseWidth = 60f; //最低保障
            }
            int wrapWidth = (int)(baseWidth / textScale); //换算为未缩放字体测量宽度
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
            wrappedLines = allLines.ToArray();
            int textLines = wrappedLines.Length;
            int lineHeight = (int)(font.MeasureString("A").Y * 0.8f) + LineSpacing;
            int headerHeight = 36; //与绘制中 textBlockOffsetY = Padding + 36 对齐, 原28会截断导致只能显示一行
            float contentHeight = textLines * lineHeight + Padding * 2 + headerHeight;
            panelHeight = MathHelper.Clamp(contentHeight, MinHeight, MaxHeight);
        }
        public override void Update() { }//此处更新在绘制逻辑中
        public virtual void LogicUpdate() {
            anchorPos = new Vector2(Main.screenWidth / 2f, Main.screenHeight - 140f);
            HandleInput();
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
                        
                        // 淡出动画完成后，恢复默认解析器
                        // 只有当前对话框是通过解析器设置的才恢复
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
            Vector2 panelPos = anchorPos - new Vector2(PanelWidth / 2f, panelHeight);
            Vector2 panelSize = new(PanelWidth, panelHeight);
            StyleUpdate(panelPos, panelSize);
        }
        public Rectangle GetPanelRect() {
            if (!Active && showProgress <= 0f) {
                return Rectangle.Empty;
            }
            float progress = closing ? (1f - hideProgress) : showProgress;
            if (progress <= 0f) {
                return Rectangle.Empty;
            }
            float eased = closing ? EaseInCubic(progress) : EaseOutBack(progress);
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
                        current.OnFinish?.Invoke();
                        StartNext();
                    }
                }
            }

            if (keyRightPressState == KeyPressState.Pressed && hover) {
                BeginClose();
            }
            fastMode = Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
        }
        protected static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }
        protected static float EaseInCubic(float t) {
            return t * t * t;
        }
        protected static float EaseOutCubic(float t) {
            return 1f - (float)Math.Pow(1f - t, 3f);
        }
        public override void Draw(SpriteBatch spriteBatch) {
            if (showProgress <= 0.01f && !closing) {
                return;
            }
            float progress = closing ? (1f - hideProgress) : showProgress;
            if (progress <= 0f) {
                return;
            }
            float eased = closing ? EaseInCubic(progress) : EaseOutBack(progress);
            float width = PanelWidth;
            float height = panelHeight;
            Vector2 panelOrigin = new(width / 2f, height);
            Vector2 drawPos = anchorPos - panelOrigin;
            drawPos.Y += (1f - eased) * 90f;
            Rectangle panelRect = new((int)drawPos.X, (int)drawPos.Y, (int)width, (int)height);
            float alpha = progress;
            float contentAlpha = contentFade * alpha;
            DrawStyle(spriteBatch, panelRect, alpha, contentAlpha, eased);
        }
        protected abstract void DrawStyle(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha, float easedProgress);
    }
}
