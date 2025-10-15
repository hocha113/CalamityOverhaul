using InnoVault.UIHandles;
using Microsoft.Xna.Framework;
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
    /// <summary>
    /// 通用对话框基础逻辑(队列/打字机/预处理钩子)样式相关内容放在子类中实现
    /// 继承者只需提供 PanelWidth 以及完整的绘制实现 <see cref="DrawStyle"/>，以及可选覆写 <see cref="StyleUpdate"/>
    /// </summary>
    internal abstract class DialogueBoxBase : UIHandle, ILocalizedModType
    {
        #region 事件与数据结构
        public class DialoguePreProcessArgs
        {
            public string Speaker;
            public string Content;
            public int Index;
            public int Total;
        }
        /// <summary>
        /// 在一段文本真正开始显示前触发，可修改内容/说话者
        /// </summary>
        public static event Action<DialoguePreProcessArgs> OnPreProcessSegment;

        protected class DialogueSegment
        {
            public string Speaker;
            public string Content;
            public Action OnFinish;
        }
        #endregion

        public abstract string LocalizationCategory { get; }

        //队列与当前状态
        protected readonly Queue<DialogueSegment> queue = new();
        protected DialogueSegment current;

        //打字机
        protected string[] wrappedLines = Array.Empty<string>();
        protected int visibleCharCount = 0;
        protected int typeTimer = 0;
        protected const int TypeInterval = 2;
        protected bool fastMode = false;
        protected bool finishedCurrent = false;

        //计数
        protected int playedCount = 0;

        //面板尺寸
        protected Vector2 anchorPos;
        protected float panelHeight = 160f;
        protected virtual float MinHeight => 120f;
        protected virtual float MaxHeight => 340f;
        protected virtual int Padding => 18;
        protected virtual int LineSpacing => 6;
        protected abstract float PanelWidth { get; }

        //展开/收起动画
        protected float showProgress = 0f;
        protected float hideProgress = 0f;
        protected virtual float ShowDuration => 18f;
        protected virtual float HideDuration => 14f;
        protected bool closing = false;

        //内容淡入
        protected float contentFade = 0f;
        protected bool waitingForAdvance = false;
        protected int advanceBlinkTimer = 0;

        //头像切换过渡
        protected string lastSpeaker;
        protected float speakerSwitchProgress = 1f; //0-1 新头像出现动画
        protected float speakerSwitchSpeed = 0.14f;

        //外部状态
        public override bool Active => current != null || queue.Count > 0 || (showProgress > 0f && !closing);

        //本地化提示
        public static LocalizedText ContinueHint;
        public static LocalizedText FastHint;

        public override void SetStaticDefaults() {
            ContinueHint = this.GetLocalization(nameof(ContinueHint), () => "继续");
            FastHint = this.GetLocalization(nameof(FastHint), () => "加速");
        }

        #region 立绘注册（通用）
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
            if (string.IsNullOrWhiteSpace(speaker) || string.IsNullOrWhiteSpace(texturePath)) return;
            Texture2D tex;
            try {
                tex = ModContent.Request<Texture2D>(texturePath, ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
            } catch { return; }
            RegisterPortrait(speaker, tex, baseColor, silhouette);
        }
        public static void RegisterPortrait(string speaker, Texture2D texture, Color? baseColor = null, bool silhouette = false) {
            if (string.IsNullOrWhiteSpace(speaker)) return;
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
            if (!portraits.TryGetValue(speaker, out var pd)) return;
            if (baseColor.HasValue) pd.BaseColor = baseColor.Value;
            if (silhouette.HasValue) pd.Silhouette = silhouette.Value;
        }
        public static void SetPortrait(string speaker, Texture2D texture, Color? baseColor = null, bool? silhouette = null) {
            RegisterPortrait(speaker, texture, baseColor, silhouette ?? false);
            SetPortraitStyle(speaker, baseColor, silhouette);
        }
        #endregion

        #region API
        public virtual void EnqueueDialogue(string speaker, string content, Action onFinish = null) {
            queue.Enqueue(new DialogueSegment { Speaker = speaker, Content = content ?? string.Empty, OnFinish = onFinish });
        }
        public virtual void ReplaceDialogue(IEnumerable<(string speaker, string content, Action callback)> segments) {
            queue.Clear();
            current = null;
            foreach (var seg in segments) {
                EnqueueDialogue(seg.speaker, seg.content, seg.callback);
            }
        }
        #endregion

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
                OnPreProcessSegment?.Invoke(args);
                current.Speaker = args.Speaker;
                current.Content = args.Content;
            }
            //头像切换检测
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
            //内容淡入策略:第一段或面板刚出现时才做淡入, 其余段保持为1避免闪烁
            if (playedCount <= 1 || showProgress < 1f) {
                contentFade = 0f;
            } else {
                contentFade = 1f;
            }
            if (current != null && !string.IsNullOrEmpty(current.Speaker) && portraits.TryGetValue(current.Speaker, out var pd)) {
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
            int wrapWidth = (int)(PanelWidth - Padding * 2 - 24);
            foreach (var block in manual) {
                if (string.IsNullOrWhiteSpace(block)) {
                    allLines.Add(string.Empty);
                    continue;
                }
                string[] lines = Utils.WordwrapString(block, FontAssets.MouseText.Value, wrapWidth + 40, 20, out int _);
                foreach (var l in lines) {
                    if (l == null) continue;
                    allLines.Add(l.TrimEnd('-', ' '));
                }
            }
            wrappedLines = allLines.ToArray();
            int textLines = wrappedLines.Length;
            int lineHeight = (int)(font.MeasureString("A").Y * 0.8f) + LineSpacing;
            float contentHeight = textLines * lineHeight + Padding * 2 + 28;
            panelHeight = MathHelper.Clamp(contentHeight, MinHeight, MaxHeight);
        }

        public override void Update() { }

        public virtual void LogicUpdate() {
            anchorPos = new Vector2(Main.screenWidth / 2f, Main.screenHeight - 140f);
            if (current == null && queue.Count > 0 && !closing) {
                StartNext();
            }
            if (!closing) {
                if (showProgress < 1f && (current != null || queue.Count > 0)) {
                    showProgress += 1f / ShowDuration;
                    showProgress = Math.Clamp(showProgress, 0f, 1f);
                }
            } else {
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
                        } else if (visibleCharCount % 6 == 0) {
                            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.2f, Pitch = -0.45f });
                        }
                    }
                } else {
                    advanceBlinkTimer++;
                }
                if (contentFade < 1f) {
                    contentFade += 0.12f;
                }
            }
            //头像过渡进度
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

            player.mouseInterface |= Active;
            HandleInput();
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
                return; //关闭动画中屏蔽输入避免回弹
            }
            if (keyLeftPressState == KeyPressState.Pressed) {
                if (!finishedCurrent) {
                    visibleCharCount = current.Content.Length;
                    finishedCurrent = true;
                    waitingForAdvance = true;
                } else {
                    current.OnFinish?.Invoke();
                    StartNext();
                }
            }
            if (Main.mouseRight && Main.mouseRightRelease) {
                BeginClose();
            }
            fastMode = Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
        }

        protected static float EaseOutBack(float t) {
            const float c1 = 1.70158f; const float c3 = c1 + 1f; return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }
        protected static float EaseInCubic(float t) => t * t * t;
        protected static float EaseOutCubic(float t) { return 1f - (float)Math.Pow(1f - t, 3f); }

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

        ///<summary>
        ///子类实现完整的风格绘制（背景/特效/文字）<br/>
        ///alpha: 面板整体透明度 (0-1)；contentAlpha: 内容淡入乘积；eased: 展开动画插值(0-1)
        ///</summary>
        protected abstract void DrawStyle(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha, float easedProgress);
    }
}
