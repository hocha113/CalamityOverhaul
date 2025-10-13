using CalamityMod.Projectiles.Rogue;
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
using static CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI.HalibutUIAsset;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV
{
    /// <summary>
    /// GalGame风格对话框
    /// </summary>
    internal class DialogueBox : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.HalibutText";

        //静态访问
        public static DialogueBox Instance => UIHandleLoader.GetUIHandleOfType<DialogueBox>();

        //对话数据结构
        private class DialogueSegment
        {
            public string Speaker; //可选
            public string Content; //正文
            public Action OnFinish; //完成回调
        }

        private readonly Queue<DialogueSegment> queue = new();

        //当前显示
        private DialogueSegment current;
        private string[] wrappedLines = Array.Empty<string>();
        private int visibleCharCount = 0; //打字机
        private int typeTimer = 0;
        private const int TypeInterval = 2; //每隔多少帧出现一个字符
        private bool fastMode = false; //按下加速
        private bool finishedCurrent = false; //当前段完成

        //面板尺寸与位置
        private Vector2 anchorPos; //屏幕锚点（默认底部）
        private const float FixedWidth = 520f; //固定宽度
        private float panelHeight = 160f; //高度动态
        private const float MinHeight = 120f;
        private const float MaxHeight = 340f;
        private const int Padding = 18;
        private const int LineSpacing = 6;

        //动画
        private float showProgress = 0f; //0-1 展开
        private float hideProgress = 0f; //0-1 收起
        private const float ShowDuration = 18f;
        private const float HideDuration = 14f;
        private bool closing = false;

        //内容淡入
        private float contentFade = 0f;

        //输入控制
        private bool waitingForAdvance = false;
        private int advanceBlinkTimer = 0;

        //外部状态
        public override bool Active => current != null || queue.Count > 0 || (showProgress > 0f && !closing);

        //本地化提示
        private static LocalizedText ContinueHint;
        private static LocalizedText FastHint;

        public override void SetStaticDefaults() {
            ContinueHint = this.GetLocalization(nameof(ContinueHint), () => "继续");
            FastHint = this.GetLocalization(nameof(FastHint), () => "加速");
        }

        /// <summary>
        /// 注册并显示一组对话（立即追加）
        /// </summary>
        public void EnqueueDialogue(string speaker, string content, Action onFinish = null) {
            queue.Enqueue(new DialogueSegment {
                Speaker = speaker,
                Content = content ?? string.Empty,
                OnFinish = onFinish
            });
        }

        /// <summary>
        /// 清空现有对话并立即替换
        /// </summary>
        public void ReplaceDialogue(IEnumerable<(string speaker, string content, Action callback)> segments) {
            queue.Clear();
            current = null;
            foreach (var seg in segments) {
                EnqueueDialogue(seg.speaker, seg.content, seg.callback);
            }
        }

        private void StartNext() {
            if (queue.Count == 0) {
                BeginClose();
                return;
            }
            current = queue.Dequeue();
            WrapCurrent();
            visibleCharCount = 0;
            typeTimer = 0;
            fastMode = false;
            finishedCurrent = false;
            waitingForAdvance = false;
            contentFade = 0f;
        }

        private void WrapCurrent() {
            if (current == null) {
                wrappedLines = Array.Empty<string>();
                return;
            }
            string raw = current.Content.Replace("\r", string.Empty);
            List<string> allLines = new();
            string[] manual = raw.Split('\n');
            DynamicSpriteFont font = FontAssets.MouseText.Value; //修改类型
            int wrapWidth = (int)(FixedWidth - Padding * 2 - 24);
            foreach (var block in manual) {
                if (string.IsNullOrWhiteSpace(block)) {
                    allLines.Add(string.Empty);
                    continue;
                }
                string[] lines = Utils.WordwrapString(block, FontAssets.MouseText.Value, wrapWidth + 40, 20, out int _);
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
            float contentHeight = textLines * lineHeight + Padding * 2 + 28;
            panelHeight = MathHelper.Clamp(contentHeight, MinHeight, MaxHeight);
        }

        private static float EaseOutBack(float t) {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1, 3) + c1 * (float)Math.Pow(t - 1, 2);
        }

        private static float EaseInCubic(float t) {
            return t * t * t;
        }

        private void BeginClose() {
            closing = true;
            hideProgress = 0f;
        }

        public override void Update() {
            //此处更新在绘制线程中
        }

        public void LogicUpdate() {
            //锚点在屏幕底部中央
            anchorPos = new Vector2(Main.screenWidth / 2f, Main.screenHeight - 140f);
            if (current == null && queue.Count > 0 && !closing) {
                StartNext();
            }
            //出现动画
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
                        //完全关闭
                        current = null;
                        queue.Clear();
                        closing = false;
                        showProgress = 0f;
                    }
                }
            }
            //内容打字机
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
                        else {
                            if (visibleCharCount % 6 == 0) {
                                SoundEngine.PlaySound(SoundID.MenuTick with {
                                    Volume = 0.3f,
                                    Pitch = -0.3f
                                });
                            }
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
            //输入
            player.mouseInterface |= Active;
            HandleInput();
        }

        private void HandleInput() {
            if (current == null) {
                return;
            }
            //左键推进
            if (keyLeftPressState == KeyPressState.Pressed) {
                if (!finishedCurrent) {
                    //直接完成
                    visibleCharCount = current.Content.Length;
                    finishedCurrent = true;
                    waitingForAdvance = true;
                }
                else {
                    //下一条
                    current.OnFinish?.Invoke();
                    StartNext();
                }
            }
            //右键关闭全部
            if (Main.mouseRight && Main.mouseRightRelease) {
                BeginClose();
            }
            //加速
            fastMode = Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift) || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
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
            float width = FixedWidth;
            float height = panelHeight;
            Vector2 panelOrigin = new Vector2(width / 2f, height);
            Vector2 drawPos = anchorPos - panelOrigin;
            drawPos.Y += (1f - eased) * 80f; //从下往上滑
            Rectangle panelRect = new Rectangle((int)drawPos.X, (int)drawPos.Y, (int)width, (int)height);
            //背景参考SkillTooltipPanel风格
            float alpha = progress;
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(4, 4);
            Color shadowColor = Color.Black * (alpha * 0.4f);
            spriteBatch.Draw(TooltipPanel, shadowRect, null, shadowColor);
            Color panelColor = Color.White * alpha;
            spriteBatch.Draw(TooltipPanel, panelRect, null, panelColor);
            //轻微发光
            Color glow = Color.Gold with { A = 0 } * (alpha * 0.15f * (float)(Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.5f + 0.5f));
            Rectangle glowRect = panelRect;
            glowRect.Inflate(2, 2);
            spriteBatch.Draw(TooltipPanel, glowRect, null, glow);
            if (current == null) {
                return;
            }
            //内容绘制
            float contentAlpha = contentFade * alpha;
            if (contentAlpha <= 0.01f) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            Vector2 textStart = new Vector2(panelRect.X + Padding, panelRect.Y + Padding + 28);
            //绘制说话者
            if (!string.IsNullOrEmpty(current.Speaker)) {
                Vector2 speakerPos = new Vector2(panelRect.X + Padding, panelRect.Y + Padding - 4);
                Color speakerGlow = Color.Gold * contentAlpha * 0.6f;
                for (int i = 0; i < 4; i++) {
                    float ang = MathHelper.TwoPi * i / 4f;
                    Vector2 off = ang.ToRotationVector2() * 1.3f;
                    Utils.DrawBorderString(spriteBatch, current.Speaker, speakerPos + off, speakerGlow, 0.85f);
                }
                Utils.DrawBorderString(spriteBatch, current.Speaker, speakerPos, Color.White * contentAlpha, 0.85f);
            }
            //显示打字机内容
            int remaining = visibleCharCount;
            int lineHeight = (int)(font.MeasureString("A").Y * 0.8f) + LineSpacing;
            for (int i = 0; i < wrappedLines.Length; i++) {
                string fullLine = wrappedLines[i];
                if (string.IsNullOrEmpty(fullLine)) {
                    remaining -= 0;
                }
                string visibleLine;
                if (finishedCurrent) {
                    visibleLine = fullLine;
                }
                else {
                    if (remaining <= 0) {
                        break;
                    }
                    int take = Math.Min(fullLine.Length, remaining);
                    visibleLine = fullLine.Substring(0, take);
                    remaining -= take;
                }
                Vector2 linePos = textStart + new Vector2(0, i * lineHeight);
                if (linePos.Y + lineHeight > panelRect.Bottom - Padding) {
                    break;
                }
                Utils.DrawBorderString(spriteBatch, visibleLine, linePos, Color.White * contentAlpha, 0.78f);
            }
            //继续提示
            if (waitingForAdvance) {
                float blink = (float)Math.Sin(advanceBlinkTimer / 12f * MathHelper.TwoPi) * 0.5f + 0.5f;
                string hint = $"> {ContinueHint.Value}<";
                Vector2 hintSize = font.MeasureString(hint) * 0.6f;
                Vector2 hintPos = new Vector2(panelRect.Right - Padding - hintSize.X, panelRect.Bottom - Padding - hintSize.Y);
                Utils.DrawBorderString(spriteBatch, hint, hintPos, Color.Gold * blink * contentAlpha, 0.6f);
            }
            if (!finishedCurrent) {
                string fast = FastHint.Value;
                Vector2 fastSize = font.MeasureString(fast) * 0.5f;
                Vector2 fastPos = new Vector2(panelRect.Right - Padding - fastSize.X, panelRect.Bottom - Padding - fastSize.Y - 14);
                Utils.DrawBorderString(spriteBatch, fast, fastPos, Color.White * 0.4f * contentAlpha, 0.5f);
            }
        }
    }
}