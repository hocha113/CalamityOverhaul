using CalamityOverhaul.Content.ADV.DialogueBoxs;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.IncomingCalls
{
    /// <summary>
    /// 来电对话系统基类——类似赛博朋克2077的来电通讯UI
    /// 屏幕左侧滑入来电面板，显示联系人头像和振铃动画，
    /// 接听后展开为通话面板，动态弹出台词
    /// </summary>
    internal abstract class IncomingCallBase : UIHandle
    {
        #region 状态

        private IncomingCallState _state = IncomingCallState.Idle;
        public IncomingCallState State => _state;

        /// <summary>
        /// 来电台词队列
        /// </summary>
        internal readonly Queue<IncomingCallSegment> queue = new();

        /// <summary>
        /// 当前正在播放的台词
        /// </summary>
        internal IncomingCallSegment current;

        /// <summary>
        /// 来电者名称（振铃时显示）
        /// </summary>
        internal string callerName;

        /// <summary>
        /// 来电者立绘键
        /// </summary>
        internal string callerPortraitKey;

        #endregion

        #region 动画进度

        /// <summary>
        /// 面板滑入进度 0~1
        /// </summary>
        protected float slideProgress;

        /// <summary>
        /// 接听展开进度 0~1（Ringing面板 → Speaking面板的过渡）
        /// </summary>
        protected float expandProgress;

        /// <summary>
        /// 内容淡入进度
        /// </summary>
        protected float contentFade;

        /// <summary>
        /// 振铃动画计时器
        /// </summary>
        protected float ringTimer;

        /// <summary>
        /// 振铃脉冲波纹列表
        /// </summary>
        protected readonly List<float> ringPulses = [];

        /// <summary>
        /// 振铃脉冲生成计时
        /// </summary>
        protected int ringPulseSpawnTimer;

        #endregion

        #region 打字机

        protected string[] wrappedLines;
        protected int visibleCharCount;
        protected int typeTimer;
        protected bool finishedCurrent;
        protected int autoAdvanceTimer;

        /// <summary>
        /// 通话计时（帧），从 Connecting 进入 Speaking 后逐帧累计
        /// </summary>
        protected int callDurationFrames;

        /// <summary>
        /// 根据当前台词内容动态计算的通话面板高度
        /// </summary>
        private float computedSpeakingHeight;

        #endregion

        #region 可重写参数

        public override bool Active => _state != IncomingCallState.Idle;

        /// <summary>
        /// 振铃阶段面板宽度
        /// </summary>
        protected virtual float RingingPanelWidth => 260f;

        /// <summary>
        /// 振铃阶段面板高度
        /// </summary>
        protected virtual float RingingPanelHeight => 120f;

        /// <summary>
        /// 通话阶段面板宽度
        /// </summary>
        protected virtual float SpeakingPanelWidth => 380f;

        /// <summary>
        /// 通话阶段面板最大高度
        /// </summary>
        protected virtual float SpeakingPanelHeight => 200f;

        /// <summary>
        /// 通话阶段面板最小高度
        /// </summary>
        protected virtual float SpeakingPanelMinHeight => 100f;

        /// <summary>
        /// 通话面板头部区域高度（头像顶部边距 + 名称 + 分割线 + 间距）
        /// </summary>
        protected virtual float SpeakingHeaderHeight => 50f;

        /// <summary>
        /// 通话面板底部区域高度（提示文字 + 底部边距）
        /// </summary>
        protected virtual float SpeakingFooterHeight => 30f;

        /// <summary>
        /// 面板距离屏幕左侧的边距
        /// </summary>
        protected virtual float LeftMargin => 20f;

        /// <summary>
        /// 面板距离屏幕顶部的偏移比例 (0~1)
        /// </summary>
        protected virtual float TopRatio => 0.2f;

        /// <summary>
        /// 滑入/滑出速度
        /// </summary>
        protected virtual float SlideSpeed => 0.08f;

        /// <summary>
        /// 展开速度
        /// </summary>
        protected virtual float ExpandSpeed => 0.06f;

        /// <summary>
        /// 打字间隔帧数
        /// </summary>
        protected virtual int TypeInterval => 2;

        /// <summary>
        /// 文本缩放
        /// </summary>
        protected virtual float TextScale => 0.85f;

        /// <summary>
        /// 名称缩放
        /// </summary>
        protected virtual float NameScale => 0.95f;

        /// <summary>
        /// 头像显示尺寸
        /// </summary>
        protected virtual float PortraitSize => 64f;

        /// <summary>
        /// 自动挂断延迟（最后一条台词结束后等待N帧自动挂断，0=不自动挂断）
        /// </summary>
        protected virtual int AutoHangUpDelay => 90;

        /// <summary>
        /// 振铃超时帧数（超过后自动挂断），0=不超时
        /// </summary>
        protected virtual int RingTimeout => 0;

        /// <summary>
        /// 行间距
        /// </summary>
        protected virtual int LineSpacing => 22;

        /// <summary>
        /// 面板内边距
        /// </summary>
        protected virtual int Padding => 10;

        #endregion

        #region 计时器

        private int ringTimeoutCounter;

        #endregion

        #region 公共接口

        /// <summary>
        /// 发起来电：设置来电者并加入台词队列
        /// </summary>
        public virtual void StartCall(string caller, string portraitKey = null) {
            if (_state != IncomingCallState.Idle) {
                ForceEnd();
            }

            callerName = caller;
            callerPortraitKey = portraitKey ?? caller;
            _state = IncomingCallState.Ringing;
            slideProgress = 0f;
            expandProgress = 0f;
            contentFade = 0f;
            ringTimer = 0f;
            ringPulses.Clear();
            ringPulseSpawnTimer = 0;
            ringTimeoutCounter = 0;
            callDurationFrames = 0;
            current = null;
            wrappedLines = null;
            visibleCharCount = 0;
            typeTimer = 0;
            finishedCurrent = false;
            autoAdvanceTimer = 0;
            computedSpeakingHeight = SpeakingPanelHeight;
            OnCallStarted();
        }

        /// <summary>
        /// 加入一条台词
        /// </summary>
        public virtual void EnqueueLine(string speaker, string content, Action onFinish = null, Action onStart = null, int autoAdvanceDelay = 120) {
            queue.Enqueue(new IncomingCallSegment {
                Speaker = speaker,
                Content = content,
                OnStart = onStart,
                OnFinish = onFinish,
                AutoAdvanceDelay = autoAdvanceDelay
            });
        }

        /// <summary>
        /// 加入台词并指定立绘键
        /// </summary>
        public virtual void EnqueueLine(string speaker, string portraitKey, string content, Action onFinish = null, Action onStart = null, int autoAdvanceDelay = 120) {
            queue.Enqueue(new IncomingCallSegment {
                Speaker = speaker,
                PortraitKey = portraitKey,
                Content = content,
                OnStart = onStart,
                OnFinish = onFinish,
                AutoAdvanceDelay = autoAdvanceDelay
            });
        }

        /// <summary>
        /// 接听来电
        /// </summary>
        public virtual void Answer() {
            if (_state != IncomingCallState.Ringing) return;
            _state = IncomingCallState.Connecting;
            expandProgress = 0f;
            OnAnswered();
        }

        /// <summary>
        /// 自动接听（跳过振铃阶段直接进入通话）
        /// </summary>
        public virtual void AutoAnswer() {
            if (_state == IncomingCallState.Ringing || _state == IncomingCallState.Idle) {
                if (_state == IncomingCallState.Idle) {
                    //如果还没开始，先初始化
                    slideProgress = 1f;
                }
                _state = IncomingCallState.Connecting;
                expandProgress = 0f;
                OnAnswered();
            }
        }

        /// <summary>
        /// 挂断/结束通话
        /// </summary>
        public virtual void HangUp() {
            if (_state == IncomingCallState.Idle || _state == IncomingCallState.Ending) return;

            //触发剩余回调
            current?.OnFinish?.Invoke();
            while (queue.Count > 0) {
                var seg = queue.Dequeue();
                seg.OnFinish?.Invoke();
            }

            _state = IncomingCallState.Ending;
            OnHangUp();
        }

        /// <summary>
        /// 强制结束（跳过动画）
        /// </summary>
        public virtual void ForceEnd() {
            current?.OnFinish?.Invoke();
            while (queue.Count > 0) {
                var seg = queue.Dequeue();
                seg.OnFinish?.Invoke();
            }

            _state = IncomingCallState.Idle;
            current = null;
            queue.Clear();
            slideProgress = 0f;
            expandProgress = 0f;
            contentFade = 0f;
        }

        #endregion

        #region 生命周期钩子

        protected virtual void OnCallStarted() { }
        protected virtual void OnAnswered() { }
        protected virtual void OnHangUp() { }
        protected virtual void OnLineStarted(IncomingCallSegment segment) { }
        protected virtual void OnLineFinished(IncomingCallSegment segment) { }
        protected virtual void OnCallEnded() { }

        #endregion

        #region 更新

        public override void Update() {
            switch (_state) {
                case IncomingCallState.Ringing:
                    UpdateRinging();
                    break;
                case IncomingCallState.Connecting:
                    UpdateConnecting();
                    break;
                case IncomingCallState.Speaking:
                    UpdateSpeaking();
                    break;
                case IncomingCallState.Ending:
                    UpdateEnding();
                    break;
            }

            StyleUpdate();
        }

        private void UpdateRinging() {
            //面板滑入
            slideProgress = MathHelper.Lerp(slideProgress, 1f, SlideSpeed);
            if (slideProgress > 0.99f) slideProgress = 1f;

            //振铃动画
            ringTimer += 0.05f;
            if (ringTimer > MathHelper.TwoPi) ringTimer -= MathHelper.TwoPi;

            //振铃脉冲
            ringPulseSpawnTimer++;
            if (ringPulseSpawnTimer >= 30) {
                ringPulseSpawnTimer = 0;
                ringPulses.Add(0f);
            }
            for (int i = ringPulses.Count - 1; i >= 0; i--) {
                ringPulses[i] += 0.025f;
                if (ringPulses[i] >= 1f) ringPulses.RemoveAt(i);
            }

            //振铃超时
            if (RingTimeout > 0) {
                ringTimeoutCounter++;
                if (ringTimeoutCounter >= RingTimeout) {
                    HangUp();
                    return;
                }
            }

            //点击接听
            HandleRingingInput();
        }

        private void UpdateConnecting() {
            slideProgress = MathHelper.Lerp(slideProgress, 1f, SlideSpeed);
            expandProgress = MathHelper.Lerp(expandProgress, 1f, ExpandSpeed);
            if (expandProgress > 0.98f) {
                expandProgress = 1f;
                _state = IncomingCallState.Speaking;
                StartNextLine();
            }
        }

        private void UpdateSpeaking() {
            callDurationFrames++;
            contentFade = MathHelper.Lerp(contentFade, 1f, 0.1f);

            //打字机
            if (!finishedCurrent && wrappedLines != null) {
                typeTimer++;
                if (typeTimer >= TypeInterval) {
                    typeTimer = 0;
                    visibleCharCount++;
                    int totalChars = 0;
                    foreach (var line in wrappedLines) totalChars += line.Length;
                    if (visibleCharCount >= totalChars) {
                        finishedCurrent = true;
                    }
                }
            }

            //自动推进
            if (finishedCurrent) {
                int delay = current?.AutoAdvanceDelay ?? 0;
                if (delay > 0) {
                    autoAdvanceTimer++;
                    if (autoAdvanceTimer >= delay) {
                        AdvanceLine();
                    }
                }
            }

            HandleSpeakingInput();
        }

        private void UpdateEnding() {
            slideProgress = MathHelper.Lerp(slideProgress, 0f, SlideSpeed * 1.5f);
            expandProgress = MathHelper.Lerp(expandProgress, 0f, ExpandSpeed * 2f);
            contentFade = MathHelper.Lerp(contentFade, 0f, 0.15f);

            if (slideProgress < 0.02f) {
                slideProgress = 0f;
                _state = IncomingCallState.Idle;
                current = null;
                queue.Clear();
                OnCallEnded();
            }
        }

        #endregion

        #region 输入处理

        protected virtual void HandleRingingInput() {
            if (IsMouseOverPanel() && Main.mouseLeft && Main.mouseLeftRelease) {
                SoundEngine.PlaySound(SoundID.MenuOpen with { Pitch = 0.3f });
                Answer();
            }
        }

        protected virtual void HandleSpeakingInput() {
            if (IsMouseOverPanel() && Main.mouseLeft && Main.mouseLeftRelease) {
                if (!finishedCurrent) {
                    //加速显示
                    int totalChars = 0;
                    if (wrappedLines != null) {
                        foreach (var line in wrappedLines) totalChars += line.Length;
                    }
                    visibleCharCount = totalChars;
                    finishedCurrent = true;
                }
                else {
                    AdvanceLine();
                }
            }
        }

        protected bool IsMouseOverPanel() {
            Rectangle panel = GetCurrentPanelRect();
            Rectangle mouse = new((int)Main.MouseScreen.X, (int)Main.MouseScreen.Y, 1, 1);
            return panel.Intersects(mouse);
        }

        #endregion

        #region 台词推进

        protected virtual void StartNextLine() {
            if (queue.Count == 0) {
                //所有台词播完
                if (AutoHangUpDelay > 0) {
                    //等待一段时间后自动挂断——在Update中检测
                    finishedCurrent = true;
                    current = null;
                }
                else {
                    HangUp();
                }
                return;
            }

            current?.OnFinish?.Invoke();
            current = queue.Dequeue();
            current.OnStart?.Invoke();
            OnLineStarted(current);
            WrapCurrentLine();
            visibleCharCount = 0;
            typeTimer = 0;
            finishedCurrent = false;
            autoAdvanceTimer = 0;
        }

        protected virtual void AdvanceLine() {
            if (queue.Count > 0) {
                StartNextLine();
            }
            else {
                current?.OnFinish?.Invoke();
                current = null;
                HangUp();
            }
        }

        protected virtual void WrapCurrentLine() {
            if (current == null) {
                wrappedLines = [];
                return;
            }

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float maxWidth = SpeakingPanelWidth - Padding * 2 - PortraitSize - 20f;
            wrappedLines = CWRUtils.WrapTextArray(current.Content, font, (int)(maxWidth / TextScale), 10, out _);

            //过滤空行
            List<string> valid = [];
            foreach (var line in wrappedLines) {
                if (!string.IsNullOrEmpty(line)) valid.Add(line.TrimEnd('-', ' '));
            }
            wrappedLines = [.. valid];

            //根据有效行数动态计算面板高度
            float textHeight = valid.Count * LineSpacing;
            float totalHeight = SpeakingHeaderHeight + textHeight + SpeakingFooterHeight;
            computedSpeakingHeight = Math.Clamp(totalHeight, SpeakingPanelMinHeight, SpeakingPanelHeight);
        }

        #endregion

        #region 面板矩形

        protected Rectangle GetCurrentPanelRect() {
            float w, h;
            if (_state == IncomingCallState.Ringing) {
                w = RingingPanelWidth;
                h = RingingPanelHeight;
            }
            else {
                w = MathHelper.Lerp(RingingPanelWidth, SpeakingPanelWidth, expandProgress);
                h = MathHelper.Lerp(RingingPanelHeight, computedSpeakingHeight, expandProgress);
            }

            float offscreenX = -w - 10f;
            float onscreenX = LeftMargin;
            float x = MathHelper.Lerp(offscreenX, onscreenX, slideProgress);
            float y = Main.screenHeight * TopRatio;

            return new Rectangle((int)x, (int)y, (int)w, (int)h);
        }

        #endregion

        #region 绘制

        /// <summary>
        /// 子类实现的样式更新
        /// </summary>
        protected virtual void StyleUpdate() { }

        /// <summary>
        /// 子类实现——绘制振铃状态面板
        /// </summary>
        protected abstract void DrawRingingPanel(SpriteBatch spriteBatch, Rectangle panelRect, float alpha);

        /// <summary>
        /// 子类实现——绘制通话状态面板
        /// </summary>
        protected abstract void DrawSpeakingPanel(SpriteBatch spriteBatch, Rectangle panelRect, float alpha, float contentAlpha);

        public override void Draw(SpriteBatch spriteBatch) {
            if (_state == IncomingCallState.Idle) return;

            Rectangle panelRect = GetCurrentPanelRect();
            float alpha = MathHelper.Clamp(slideProgress, 0f, 1f);

            if (_state == IncomingCallState.Ringing) {
                DrawRingingPanel(spriteBatch, panelRect, alpha);
            }
            else {
                DrawSpeakingPanel(spriteBatch, panelRect, alpha, contentFade);
            }
        }

        /// <summary>
        /// 辅助：绘制立绘
        /// </summary>
        protected void DrawPortrait(SpriteBatch spriteBatch, Vector2 center, float size, float alpha) {
            string key = current?.PortraitKey ?? current?.Speaker ?? callerPortraitKey ?? callerName;
            if (key != null && DialogueBoxBase.TryGetPortrait(key, out var pd) && pd.Texture != null) {
                Texture2D tex = pd.Texture;
                Rectangle? src = pd.SourceRect;
                Vector2 texSize = src.HasValue ? new Vector2(src.Value.Width, src.Value.Height) : new Vector2(tex.Width, tex.Height);
                float scale = size / MathF.Max(texSize.X, texSize.Y);
                Color drawColor = pd.Silhouette ? (new Color(20, 35, 55) * 0.85f) : (pd.BaseColor * alpha);

                spriteBatch.Draw(tex, center, src, drawColor, 0f, texSize / 2f, scale, SpriteEffects.None, 0f);
            }
        }

        /// <summary>
        /// 辅助：绘制打字机文本（支持字符覆写，用于扰码/故障字形等效果）
        /// </summary>
        protected void DrawTypedText(SpriteBatch spriteBatch, Vector2 startPos, float alpha, Color textColor) {
            if (wrappedLines == null || wrappedLines.Length == 0) return;

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            int charsSoFar = 0;

            for (int i = 0; i < wrappedLines.Length; i++) {
                string line = wrappedLines[i];
                int lineStart = charsSoFar;
                charsSoFar += line.Length;

                //计算该行可见字符数
                int lineVisible = Math.Clamp(visibleCharCount - lineStart, 0, line.Length);
                if (lineVisible <= 0) break;

                //逐字符覆写（扰码/故障字形效果）
                char[] chars = line[..lineVisible].ToCharArray();
                for (int c = 0; c < chars.Length; c++) {
                    char? ov = GetCharOverride(lineStart + c);
                    if (ov.HasValue) chars[c] = ov.Value;
                }
                string visibleText = new string(chars);

                Vector2 pos = startPos + new Vector2(0, i * LineSpacing);
                pos = ApplyTextLineOffset(pos, i);

                Utils.DrawBorderString(spriteBatch, visibleText, pos, textColor * alpha, TextScale);
            }
        }

        /// <summary>
        /// 可重写：为指定全局字符索引返回覆写字符（返回 null 则显示原始字符）
        /// 用于实现扰码/故障字形等打字特效
        /// </summary>
        protected virtual char? GetCharOverride(int globalCharIndex) => null;

        /// <summary>
        /// 可重写：文本行偏移
        /// </summary>
        protected virtual Vector2 ApplyTextLineOffset(Vector2 basePos, int lineIndex) => basePos;

        #endregion
    }
}
