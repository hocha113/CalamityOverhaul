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

namespace CalamityOverhaul.Content.ADV.DialogueBoxs
{
    public abstract class DialogueBoxBase : UIHandle, ILocalizedModType
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
            /// <summary>
            /// 立绘键，如果为null则使用Speaker作为立绘键
            /// 允许角色名和立绘显示分离
            /// </summary>
            public string PortraitKey;
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
        protected static readonly Dictionary<string, FullBodyPortraitBase> nameTofullBodyPortraits = new(StringComparer.Ordinal);
        protected static readonly Dictionary<Type, FullBodyPortraitBase> typeTofullBodyPortraits = new();

        //当前激活的全身立绘
        protected FullBodyPortraitBase activeFullBodyPortrait;

        /// <summary>
        /// 注册一个全身立绘实例
        /// </summary>
        /// <param name="key">立绘标识符</param>
        /// <param name="portrait">立绘实例</param>
        public static void RegisterFullBodyPortrait(FullBodyPortraitBase portrait) {
            typeTofullBodyPortraits[portrait.GetType()] = portrait;

            if (string.IsNullOrWhiteSpace(portrait.Name) || portrait == null) {
                return;
            }

            if (!nameTofullBodyPortraits.TryAdd(portrait.Name, portrait)) {
                nameTofullBodyPortraits[portrait.Name] = portrait;
            }
        }

        /// <summary>
        /// 显示全身立绘
        /// </summary>
        /// <returns>是否成功显示</returns>
        public virtual bool ShowFullBodyPortrait<T>() where T : FullBodyPortraitBase => ShowFullBodyPortrait(typeof(T));
        /// <summary>
        /// 显示全身立绘
        /// </summary>
        /// <param name="key">立绘标识符</param>
        /// <returns>是否成功显示</returns>
        public virtual bool ShowFullBodyPortrait(string key) {
            if (string.IsNullOrWhiteSpace(key)) {
                return false;
            }

            if (!nameTofullBodyPortraits.TryGetValue(key, out var portrait)) {
                return false;
            }

            StartPerformance(portrait);
            return true;
        }
        /// <summary>
        /// 显示全身立绘
        /// </summary>
        /// <param name="key">立绘标识符</param>
        /// <returns>是否成功显示</returns>
        public virtual bool ShowFullBodyPortrait(Type type) {
            if (!typeTofullBodyPortraits.TryGetValue(type, out var portrait)) {
                return false;
            }

            StartPerformance(portrait);
            return true;
        }

        /// <summary>
        /// 启动全身立绘
        /// </summary>
        /// <param name="portrait"></param>
        public void StartPerformance(FullBodyPortraitBase portrait) {
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
        }

        /// <summary>
        /// 隐藏当前全身立绘
        /// </summary>
        public virtual void HideFullBodyPortrait() {
            activeFullBodyPortrait?.EndPerformance();
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
            nameTofullBodyPortraits.Clear();
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
            /// <summary>
            /// 用于纹理裁剪的源矩形区域，如果为 null 则绘制整个纹理
            /// </summary>
            public Rectangle? SourceRect;
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
                tex = CWRUtils.GetT2DAsset(texturePath, true).Value;
            } catch {
                return;
            }
            RegisterPortrait(speaker, tex, baseColor, silhouette);
        }

        /// <summary>
        /// 注册头像
        /// </summary>
        /// <param name="speaker">说话者标识符</param>
        /// <param name="texturePath">纹理路径</param>
        /// <param name="sourceRect">用于裁剪的矩形区域，允许从帧图中截取特定部分</param>
        /// <param name="baseColor">基础颜色</param>
        /// <param name="silhouette">是否显示为剪影</param>
        public static void RegisterPortrait(string speaker, string texturePath, Rectangle sourceRect, Color? baseColor = null, bool silhouette = false) {
            if (string.IsNullOrWhiteSpace(speaker) || string.IsNullOrWhiteSpace(texturePath)) {
                return;
            }
            Texture2D tex;
            try {
                tex = CWRUtils.GetT2DAsset(texturePath, true).Value;
            } catch {
                return;
            }
            RegisterPortrait(speaker, tex, sourceRect, baseColor, silhouette);
        }

        /// <summary>
        /// 注册头像
        /// </summary>
        /// <param name="speaker">说话者标识符</param>
        /// <param name="texture">纹理对象</param>
        /// <param name="baseColor">基础颜色</param>
        /// <param name="silhouette">是否显示为剪影</param>
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
            pd.SourceRect = null; //不裁剪，使用完整纹理
        }

        /// <summary>
        /// 注册头像
        /// </summary>
        /// <param name="speaker">说话者标识符</param>
        /// <param name="texture">纹理对象</param>
        /// <param name="sourceRect">用于裁剪的矩形区域，允许从帧图中截取特定部分</param>
        /// <param name="baseColor">基础颜色</param>
        /// <param name="silhouette">是否显示为剪影</param>
        public static void RegisterPortrait(string speaker, Texture2D texture, Rectangle sourceRect, Color? baseColor = null, bool silhouette = false) {
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
            pd.SourceRect = sourceRect;
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
                OnFinish = onFinish,
                PortraitKey = null
            });
        }

        /// <summary>
        /// 入队对话(支持独立立绘键)
        /// </summary>
        /// <param name="speaker">说话者名称(显示用)</param>
        /// <param name="portraitKey">立绘键(用于查找头像，如果为null则使用speaker)</param>
        /// <param name="content">对话内容</param>
        /// <param name="onFinish">完成回调</param>
        /// <param name="onStart">开始回调</param>
        public virtual void EnqueueDialogue(string speaker, string portraitKey, string content, Action onFinish = null, Action onStart = null) {
            queue.Enqueue(new DialogueSegment {
                Speaker = speaker,
                Content = content ?? string.Empty,
                OnStart = onStart,
                OnFinish = onFinish,
                PortraitKey = portraitKey
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
                //通知全身立绘对话完成
                activeFullBodyPortrait?.OnDialogueComplete();
                BeginClose();
                playedCount = 0;
                return;
            }

            //通知全身立绘对话推进
            if (playedCount > 0) {
                activeFullBodyPortrait?.OnDialogueAdvance();
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
            //确定当前对话使用的立绘键(优先使用PortraitKey，否则使用Speaker)
            string currentPortraitKey = current?.PortraitKey ?? current?.Speaker;
            if (current != null && !string.IsNullOrEmpty(currentPortraitKey) && portraits.TryGetValue(currentPortraitKey, out var pd2)) {
                foreach (var kv in portraits) {
                    kv.Value.TargetFade = kv.Key == currentPortraitKey ? 1f : 0f;
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
            string wrapPortraitKey = current.PortraitKey ?? current.Speaker;
            if (!string.IsNullOrEmpty(wrapPortraitKey) && portraits.TryGetValue(wrapPortraitKey, out var pd) && pd.Texture != null) {
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
        #region 头像绘制辅助系统

        /// <summary>
        /// 头像尺寸信息结构体
        /// </summary>
        protected struct PortraitSizeInfo
        {
            /// <summary>
            /// 计算后的缩放值
            /// </summary>
            public float Scale;

            /// <summary>
            /// 绘制后的实际尺寸
            /// </summary>
            public Vector2 DrawSize;

            /// <summary>
            /// 绘制位置
            /// </summary>
            public Vector2 DrawPosition;

            /// <summary>
            /// 用于绘制的源矩形（考虑裁剪）
            /// </summary>
            public Rectangle? SourceRectangle;

            /// <summary>
            /// 纹理的实际尺寸（考虑裁剪）
            /// </summary>
            public Vector2 TextureSize;
        }

        /// <summary>
        /// 计算头像的绘制信息
        /// </summary>
        /// <param name="portraitData">头像数据</param>
        /// <param name="panelRect">面板矩形</param>
        /// <param name="appearScale">出现动画缩放</param>
        /// <param name="availHeight">可用高度</param>
        /// <param name="maxPortraitHeight">最大头像高度</param>
        /// <returns>头像尺寸信息</returns>
        protected virtual PortraitSizeInfo CalculatePortraitSize(
            PortraitData portraitData,
            Rectangle panelRect,
            float appearScale = 1f,
            float? availHeight = null,
            float? maxPortraitHeight = null) {
            if (portraitData?.Texture == null) {
                return default;
            }

            Texture2D ptex = portraitData.Texture;
            Rectangle? sourceRect = portraitData.SourceRect;

            //确定纹理的实际尺寸
            Vector2 textureSize = sourceRect.HasValue
                ? new Vector2(sourceRect.Value.Width, sourceRect.Value.Height)
                : ptex.Size();

            //计算可用高度和最大头像高度
            float actualAvailHeight = availHeight ?? (panelRect.Height - 60f);
            float actualMaxHeight = maxPortraitHeight ?? Math.Clamp(actualAvailHeight, 95f, 270f);

            //计算基础缩放
            float scaleBase = Math.Min(PortraitWidth / textureSize.X, actualMaxHeight / textureSize.Y);
            float finalScale = scaleBase * appearScale;

            //计算绘制后的尺寸
            Vector2 drawSize = textureSize * finalScale;

            //计算绘制位置
            Vector2 drawPosition = new Vector2(
                panelRect.X + Padding + PortraitInnerPadding,
                panelRect.Y + panelRect.Height - drawSize.Y - Padding - 12f
            );

            return new PortraitSizeInfo {
                Scale = finalScale,
                DrawSize = drawSize,
                DrawPosition = drawPosition,
                SourceRectangle = sourceRect,
                TextureSize = textureSize
            };
        }

        /// <summary>
        /// 绘制头像
        /// </summary>
        /// <param name="spriteBatch">精灵批次</param>
        /// <param name="portraitData">头像数据</param>
        /// <param name="sizeInfo">尺寸信息</param>
        /// <param name="drawColor">绘制颜色</param>
        /// <param name="rotation">旋转角度</param>
        protected virtual void DrawPortrait(
            SpriteBatch spriteBatch,
            PortraitData portraitData,
            PortraitSizeInfo sizeInfo,
            Color drawColor,
            float rotation = 0f) {
            if (portraitData?.Texture == null) {
                return;
            }

            spriteBatch.Draw(
                portraitData.Texture,
                sizeInfo.DrawPosition,
                sizeInfo.SourceRectangle,
                drawColor,
                rotation,
                Vector2.Zero,
                sizeInfo.Scale,
                SpriteEffects.None,
                0f
            );
        }

        #endregion

        #region 内容绘制模板方法系统

        /// <summary>
        /// 内容绘制上下文，包含绘制过程中的所有共享状态
        /// </summary>
        protected class ContentDrawContext
        {
            public SpriteBatch SpriteBatch;
            public Rectangle PanelRect;
            public float Alpha;
            public float ContentAlpha;
            public DynamicSpriteFont Font;

            //立绘相关
            public bool HasPortrait;
            public PortraitData PortraitData;
            public string PortraitKey;

            //动画进度
            public float SwitchEase;
            public float PortraitAppearScale;
            public float PortraitExtraAlpha;

            //布局偏移
            public float LeftOffset;
            public float TopNameOffset;
            public float TextBlockOffsetY;

            //立绘尺寸信息
            public PortraitSizeInfo PortraitSizeInfo;
        }

        /// <summary>
        /// 样式配置，子类可重写以自定义各种参数
        /// </summary>
        protected virtual float PortraitScaleMin => 0.85f;
        protected virtual float PortraitScaleMax => 1f;
        protected virtual float PortraitAvailHeightOffset => 54f;
        protected virtual float PortraitMinHeight => 90f;
        protected virtual float PortraitMaxHeight => 260f;
        protected virtual float PortraitFramePadding => 8f;
        protected virtual float PortraitGlowPadding => 4f;
        protected virtual float PortraitLeftMargin => 20f;
        protected virtual float TopNameOffsetBase => 10f;
        protected virtual float TextBlockOffsetBase => 36f;
        protected virtual float NameScale => 0.9f;
        protected virtual float TextScale => 0.8f;
        protected virtual float ContinueHintScale => 0.8f;
        protected virtual float FastHintScale => 0.7f;
        protected virtual int NameGlowCount => 4;
        protected virtual float NameGlowRadius => 1.8f;
        protected virtual float DividerLineThickness => 1.3f;
        protected virtual float DividerLineOffsetY => 26f;

        /// <summary>
        /// 绘制立绘和文本的模板方法
        /// 子类可以重写整个方法，或者只重写特定的钩子方法
        /// </summary>
        protected virtual void DrawPortraitAndText(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha) {
            //创建绘制上下文
            var ctx = CreateDrawContext(spriteBatch, panelRect, alpha, contentAlpha);
            if (ctx == null) {
                return;
            }

            //绘制立绘
            if (ctx.HasPortrait) {
                DrawPortraitSection(ctx);
            }

            //绘制说话者名字
            if (current != null && !string.IsNullOrEmpty(current.Speaker)) {
                DrawSpeakerName(ctx);
            }

            //绘制文本内容
            DrawTextContent(ctx);

            //绘制提示
            DrawHints(ctx);
        }

        /// <summary>
        /// 创建绘制上下文
        /// </summary>
        protected virtual ContentDrawContext CreateDrawContext(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha) {
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            bool hasPortrait = false;
            PortraitData speakerPortrait = null;
            string portraitKey = current?.PortraitKey ?? current?.Speaker;

            if (current != null && !string.IsNullOrEmpty(portraitKey) && portraits.TryGetValue(portraitKey, out var pd) && pd.Texture != null && pd.Fade > 0.02f) {
                hasPortrait = true;
                speakerPortrait = pd;
            }

            float switchEase = speakerSwitchProgress;
            if (switchEase < 1f) {
                switchEase = CWRUtils.EaseOutCubic(switchEase);
            }

            float portraitAppearScale = MathHelper.Lerp(PortraitScaleMin, PortraitScaleMax, switchEase);
            float portraitExtraAlpha = MathHelper.Clamp(switchEase, 0f, 1f);

            float leftOffset = Padding;
            float topNameOffset = TopNameOffsetBase;
            float textBlockOffsetY = Padding + TextBlockOffsetBase;

            var ctx = new ContentDrawContext {
                SpriteBatch = spriteBatch,
                PanelRect = panelRect,
                Alpha = alpha,
                ContentAlpha = contentAlpha,
                Font = font,
                HasPortrait = hasPortrait,
                PortraitData = speakerPortrait,
                PortraitKey = portraitKey,
                SwitchEase = switchEase,
                PortraitAppearScale = portraitAppearScale,
                PortraitExtraAlpha = portraitExtraAlpha,
                LeftOffset = leftOffset,
                TopNameOffset = topNameOffset,
                TextBlockOffsetY = textBlockOffsetY
            };

            //计算立绘尺寸
            if (hasPortrait) {
                ctx.PortraitSizeInfo = CalculatePortraitSize(
                    speakerPortrait,
                    panelRect,
                    portraitAppearScale,
                    panelRect.Height - PortraitAvailHeightOffset,
                    Math.Clamp(panelRect.Height - PortraitAvailHeightOffset, PortraitMinHeight, PortraitMaxHeight)
                );
            }

            return ctx;
        }

        /// <summary>
        /// 绘制立绘区域（包括边框、立绘本身、光效）
        /// </summary>
        protected virtual void DrawPortraitSection(ContentDrawContext ctx) {
            var sizeInfo = ctx.PortraitSizeInfo;
            var pd = ctx.PortraitData;

            //应用立绘位置偏移（子类可重写以添加扭曲效果）
            Vector2 finalPosition = ApplyPortraitOffset(ctx, sizeInfo.DrawPosition);
            sizeInfo.DrawPosition = finalPosition;
            ctx.PortraitSizeInfo = sizeInfo;

            //绘制头像边框
            Rectangle frameRect = new(
                (int)(finalPosition.X - PortraitFramePadding),
                (int)(finalPosition.Y - PortraitFramePadding),
                (int)(sizeInfo.DrawSize.X + PortraitFramePadding * 2),
                (int)(sizeInfo.DrawSize.Y + PortraitFramePadding * 2)
            );
            DrawPortraitFrame(ctx, frameRect);

            //计算绘制颜色
            Color drawColor = GetPortraitColor(ctx);

            //绘制立绘
            DrawPortrait(ctx.SpriteBatch, pd, sizeInfo, drawColor);

            //绘制立绘光效
            Rectangle glowRect = new(
                (int)(finalPosition.X - PortraitGlowPadding),
                (int)(finalPosition.Y - PortraitGlowPadding),
                (int)(sizeInfo.DrawSize.X + PortraitGlowPadding * 2),
                (int)(sizeInfo.DrawSize.Y + PortraitGlowPadding * 2)
            );
            DrawPortraitGlow(ctx, glowRect);

            //更新左偏移
            ctx.LeftOffset += PortraitWidth + PortraitLeftMargin;
        }

        /// <summary>
        /// 应用立绘位置偏移，子类可重写以添加扭曲/漂浮效果
        /// </summary>
        protected virtual Vector2 ApplyPortraitOffset(ContentDrawContext ctx, Vector2 basePosition) {
            return basePosition;
        }

        /// <summary>
        /// 绘制立绘边框，子类应重写以自定义样式
        /// </summary>
        protected virtual void DrawPortraitFrame(ContentDrawContext ctx, Rectangle frameRect) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            float alpha = ctx.Alpha * ctx.PortraitData.Fade * ctx.PortraitExtraAlpha;

            //默认深色背景
            Color back = new Color(10, 20, 30) * (alpha * 0.85f);
            ctx.SpriteBatch.Draw(vaule, frameRect, new Rectangle(0, 0, 1, 1), back);

            //默认边框
            Color edge = new Color(100, 150, 200) * (alpha * 0.6f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Y, frameRect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Bottom - 2, frameRect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.X, frameRect.Y, 2, frameRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.8f);
            ctx.SpriteBatch.Draw(vaule, new Rectangle(frameRect.Right - 2, frameRect.Y, 2, frameRect.Height), new Rectangle(0, 0, 1, 1), edge * 0.8f);
        }

        /// <summary>
        /// 获取立绘绘制颜色
        /// </summary>
        protected virtual Color GetPortraitColor(ContentDrawContext ctx) {
            var pd = ctx.PortraitData;
            Color drawColor = pd.BaseColor * ctx.ContentAlpha * pd.Fade * ctx.PortraitExtraAlpha;

            if (pd.Silhouette) {
                drawColor = GetSilhouetteColor(ctx) * (ctx.ContentAlpha * pd.Fade * ctx.PortraitExtraAlpha);
            }

            return drawColor;
        }

        /// <summary>
        /// 获取剪影颜色，子类可重写
        /// </summary>
        protected virtual Color GetSilhouetteColor(ContentDrawContext ctx) {
            return new Color(10, 30, 40) * 0.9f;
        }

        /// <summary>
        /// 绘制立绘光效，子类应重写以自定义样式
        /// </summary>
        protected virtual void DrawPortraitGlow(ContentDrawContext ctx, Rectangle glowRect) {
            var pd = ctx.PortraitData;
            Color rim = new Color(140, 200, 255) * (ctx.ContentAlpha * 0.4f * pd.Fade) * ctx.PortraitExtraAlpha;
            DrawGlowRect(ctx.SpriteBatch, glowRect, rim);
        }

        /// <summary>
        /// 绘制说话者名字
        /// </summary>
        protected virtual void DrawSpeakerName(ContentDrawContext ctx) {
            Vector2 speakerPos = GetSpeakerNamePosition(ctx);
            float nameAlpha = ctx.ContentAlpha * ctx.SwitchEase;

            //绘制名字光晕
            DrawNameGlow(ctx, speakerPos, nameAlpha);

            //绘制名字本体
            Utils.DrawBorderString(ctx.SpriteBatch, current.Speaker, speakerPos, Color.White * nameAlpha, NameScale);

            //绘制分隔线
            Vector2 divStart = speakerPos + new Vector2(0, DividerLineOffsetY);
            Vector2 divEnd = divStart + new Vector2(ctx.PanelRect.Width - ctx.LeftOffset - Padding, 0);
            DrawDividerLine(ctx, divStart, divEnd, nameAlpha);
        }

        /// <summary>
        /// 获取说话者名字位置，子类可重写以添加扭曲效果
        /// </summary>
        protected virtual Vector2 GetSpeakerNamePosition(ContentDrawContext ctx) {
            return new Vector2(
                ctx.PanelRect.X + ctx.LeftOffset,
                ctx.PanelRect.Y + ctx.TopNameOffset - (1f - ctx.SwitchEase) * 6f
            );
        }

        /// <summary>
        /// 绘制名字光晕，子类应重写以自定义样式
        /// </summary>
        protected virtual void DrawNameGlow(ContentDrawContext ctx, Vector2 position, float alpha) {
            Color nameGlow = new Color(140, 200, 255) * alpha * 0.7f;
            for (int i = 0; i < NameGlowCount; i++) {
                float a = MathHelper.TwoPi * i / NameGlowCount;
                Vector2 off = a.ToRotationVector2() * NameGlowRadius * ctx.SwitchEase;
                Utils.DrawBorderString(ctx.SpriteBatch, current.Speaker, position + off, nameGlow * 0.55f, NameScale);
            }
        }

        /// <summary>
        /// 绘制分隔线，子类应重写以自定义样式
        /// </summary>
        protected virtual void DrawDividerLine(ContentDrawContext ctx, Vector2 start, Vector2 end, float alpha) {
            DrawGradientLine(ctx.SpriteBatch, start, end,
                new Color(100, 150, 200) * (alpha * 0.85f),
                new Color(100, 150, 200) * (alpha * 0.05f),
                DividerLineThickness);
        }

        /// <summary>
        /// 绘制文本内容
        /// </summary>
        protected virtual void DrawTextContent(ContentDrawContext ctx) {
            Vector2 textStart = new(ctx.PanelRect.X + ctx.LeftOffset, ctx.PanelRect.Y + ctx.TextBlockOffsetY);
            int remaining = visibleCharCount;
            int lineHeight = (int)(ctx.Font.MeasureString("A").Y * TextScale) + LineSpacing;
            int maxLines = (int)((ctx.PanelRect.Height - (textStart.Y - ctx.PanelRect.Y) - Padding) / lineHeight);

            for (int i = 0; i < wrappedLines.Length && i < maxLines; i++) {
                string fullLine = wrappedLines[i];
                if (string.IsNullOrEmpty(fullLine)) {
                    continue;
                }

                string visLine;
                if (finishedCurrent) {
                    visLine = fullLine;
                }
                else {
                    if (remaining <= 0) {
                        break;
                    }
                    int take = Math.Min(fullLine.Length, remaining);
                    visLine = fullLine[..take];
                    remaining -= take;
                }

                Vector2 linePos = textStart + new Vector2(0, i * lineHeight);
                if (linePos.Y + lineHeight > ctx.PanelRect.Bottom - Padding) {
                    break;
                }

                //应用文本位置偏移（子类可重写以添加扭曲效果）
                Vector2 finalPos = ApplyTextLineOffset(ctx, linePos, i);

                //获取文本颜色
                Color lineColor = GetTextLineColor(ctx, i);

                //绘制文本光晕（可选）
                DrawTextLineGlow(ctx, visLine, finalPos, i);

                //绘制文本
                Utils.DrawBorderString(ctx.SpriteBatch, visLine, finalPos, lineColor, TextScale);
            }
        }

        /// <summary>
        /// 应用文本行位置偏移，子类可重写以添加扭曲效果
        /// </summary>
        protected virtual Vector2 ApplyTextLineOffset(ContentDrawContext ctx, Vector2 basePosition, int lineIndex) {
            return basePosition;
        }

        /// <summary>
        /// 获取文本行颜色，子类可重写
        /// </summary>
        protected virtual Color GetTextLineColor(ContentDrawContext ctx, int lineIndex) {
            return Color.Lerp(new Color(180, 230, 250), Color.White, 0.35f) * ctx.ContentAlpha;
        }

        /// <summary>
        /// 绘制文本行光晕，子类可重写（默认不绘制）
        /// </summary>
        protected virtual void DrawTextLineGlow(ContentDrawContext ctx, string text, Vector2 position, int lineIndex) {
            //默认不绘制光晕，子类可重写
        }

        /// <summary>
        /// 绘制提示（继续和加速）
        /// </summary>
        protected virtual void DrawHints(ContentDrawContext ctx) {
            //继续提示
            if (waitingForAdvance) {
                DrawContinueHint(ctx);
            }

            //加速提示
            if (!finishedCurrent) {
                DrawFastHint(ctx);
            }
        }

        /// <summary>
        /// 获取继续提示文本，子类可重写
        /// </summary>
        protected virtual string GetContinueHintText() {
            return $"> {ContinueHint.Value}<";
        }

        /// <summary>
        /// 绘制继续提示
        /// </summary>
        protected virtual void DrawContinueHint(ContentDrawContext ctx) {
            float blink = (float)Math.Sin(advanceBlinkTimer / 12f * MathHelper.TwoPi) * 0.5f + 0.5f;
            string hint = GetContinueHintText();
            Vector2 hintSize = ctx.Font.MeasureString(hint) * (ContinueHintScale * 0.75f);
            Vector2 hintPos = new(ctx.PanelRect.Right - Padding - hintSize.X, ctx.PanelRect.Bottom - Padding - hintSize.Y);

            Color hintColor = GetContinueHintColor(ctx, blink);
            Utils.DrawBorderString(ctx.SpriteBatch, hint, hintPos, hintColor, ContinueHintScale);
        }

        /// <summary>
        /// 获取继续提示颜色，子类可重写
        /// </summary>
        protected virtual Color GetContinueHintColor(ContentDrawContext ctx, float blink) {
            return new Color(140, 230, 255) * blink * ctx.ContentAlpha;
        }

        /// <summary>
        /// 绘制加速提示
        /// </summary>
        protected virtual void DrawFastHint(ContentDrawContext ctx) {
            string fast = FastHint.Value;
            Vector2 fastSize = ctx.Font.MeasureString(fast) * (FastHintScale * 0.85f);
            Vector2 fastPos = new(ctx.PanelRect.Right - Padding - fastSize.X, ctx.PanelRect.Bottom - Padding - fastSize.Y - 16);

            Color fastColor = GetFastHintColor(ctx);
            Utils.DrawBorderString(ctx.SpriteBatch, fast, fastPos, fastColor, FastHintScale);
        }

        /// <summary>
        /// 获取加速提示颜色，子类可重写
        /// </summary>
        protected virtual Color GetFastHintColor(ContentDrawContext ctx) {
            return new Color(120, 200, 235) * 0.4f * ctx.ContentAlpha;
        }

        /// <summary>
        /// 绘制渐变线条的辅助方法
        /// </summary>
        protected static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }

            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 11f));

            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }

        /// <summary>
        /// 绘制发光矩形的辅助方法
        /// </summary>
        protected static void DrawGlowRect(SpriteBatch sb, Rectangle rect, Color glow) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            sb.Draw(vaule, rect, new Rectangle(0, 0, 1, 1), glow * 0.15f);

            int border = 2;
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.6f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.4f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.5f);
            sb.Draw(vaule, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.5f);
        }

        #endregion
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
            activeFullBodyPortrait?.Draw(spriteBatch, alpha);

            DrawStyle(spriteBatch, panelRect, alpha, contentAlpha, eased);
        }
        protected abstract void DrawStyle(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha, float easedProgress);
    }
}
