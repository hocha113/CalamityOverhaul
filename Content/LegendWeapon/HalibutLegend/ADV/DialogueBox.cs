using CalamityMod.Projectiles.Rogue;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
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
                        current = null;
                        queue.Clear();
                        closing = false;
                        showProgress = 0f;
                    }
                }
            }
            //背景动画计时器（之前遗漏导致面板全静态）
            panelPulseTimer += 0.035f;
            scanTimer += 0.022f;
            if (panelPulseTimer > MathHelper.TwoPi) {
                panelPulseTimer -= MathHelper.TwoPi;
            }
            if (scanTimer > MathHelper.TwoPi) {
                scanTimer -= MathHelper.TwoPi;
            }
            //星粒子刷新（之前未调用导致列表不维护，虽然不影响文本，但统一整理）
            Vector2 panelPos = anchorPos - new Vector2(FixedWidth / 2f, panelHeight);
            Vector2 panelSize = new(FixedWidth, panelHeight);
            starSpawnTimer++;
            if (Active && starSpawnTimer >= 24 && starFx.Count < 14) {
                starSpawnTimer = 0;
                Vector2 p = panelPos + new Vector2(Main.rand.NextFloat(32f, panelSize.X - 32f), Main.rand.NextFloat(40f, panelSize.Y - 40f));
                starFx.Add(new StarFx(p));
            }
            for (int i = starFx.Count - 1; i >= 0; i--) {
                if (starFx[i].Update(panelPos, panelSize)) {
                    starFx.RemoveAt(i);
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
                        else if (visibleCharCount % 6 == 0) {
                            SoundEngine.PlaySound(SoundID.MenuTick with {
                                Volume = 0.3f,
                                Pitch = -0.3f
                            });
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
            Vector2 panelOrigin = new(width / 2f, height);
            Vector2 drawPos = anchorPos - panelOrigin;
            drawPos.Y += (1f - eased) * 90f;//更明显的上滑
            Rectangle panelRect = new((int)drawPos.X, (int)drawPos.Y, (int)width, (int)height);
            float alpha = progress;
            //背景分层: 阴影 -> 主底色渐变 -> 内部柔光 -> 边框 -> 扫描线 -> 星粒子
            Texture2D px = TextureAssets.MagicPixel.Value;
            //阴影
            Rectangle shadow = panelRect;
            shadow.Offset(5, 6);
            spriteBatch.Draw(px, shadow, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.5f));
            //主底色渐变(垂直分段绘制)
            int segs = 24;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));
                Color top = new Color(20, 32, 54);
                Color mid = new Color(30, 42, 70);
                Color bot = new Color(18, 28, 46);
                Color c = Color.Lerp(Color.Lerp(top, mid, (float)Math.Sin(panelPulseTimer * 0.6f) * 0.5f + 0.5f), bot, t);
                c *= alpha * 0.95f;
                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }
            //内层柔亮晕圈
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.3f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-4, -4);
            spriteBatch.Draw(px, inner, new Rectangle(0, 0, 1, 1), Color.CornflowerBlue * (alpha * 0.08f * pulse));
            //边框线 + 角落亮点
            DrawFrame(spriteBatch, panelRect, alpha, pulse);
            //星粒子
            foreach (var s in starFx) {
                s.Draw(spriteBatch, alpha * 0.9f);
            }
            if (current == null) {
                return;
            }
            float contentAlpha = contentFade * alpha;
            if (contentAlpha <= 0.01f) {
                return;
            }
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            //说话者
            if (!string.IsNullOrEmpty(current.Speaker)) {
                Vector2 speakerPos = new(panelRect.X + Padding, panelRect.Y + 10);
                Color nameGlow = Color.Gold * contentAlpha * 0.55f;
                for (int i = 0; i < 4; i++) {
                    float a = MathHelper.TwoPi * i / 4f;
                    Vector2 off = a.ToRotationVector2() * 1.5f;
                    Utils.DrawBorderString(spriteBatch, current.Speaker, speakerPos + off, nameGlow * 0.6f, 0.9f);
                }
                Utils.DrawBorderString(spriteBatch, current.Speaker, speakerPos, Color.White * contentAlpha, 0.9f);
                //分隔线
                Vector2 divStart = speakerPos + new Vector2(0, 26);
                Vector2 divEnd = divStart + new Vector2(panelRect.Width - Padding * 2, 0);
                DrawGradientLine(spriteBatch, divStart, divEnd, Color.Gold * (contentAlpha * 0.7f), Color.Gold * (contentAlpha * 0.05f), 1.2f);
            }
            //文本区域起点
            Vector2 textStart = new(panelRect.X + Padding, panelRect.Y + Padding + 34);
            int remaining = visibleCharCount;
            int lineHeight = (int)(font.MeasureString("A").Y * 0.8f) + LineSpacing;
            int maxLines = (int)((panelRect.Height - (textStart.Y - panelRect.Y) - Padding) / lineHeight);

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
                if (linePos.Y + lineHeight > panelRect.Bottom - Padding) {
                    break;
                }

                Utils.DrawBorderString(spriteBatch, visLine, linePos, Color.White * contentAlpha, 0.78f);
            }
            if (waitingForAdvance) {
                float blink = (float)Math.Sin(advanceBlinkTimer / 12f * MathHelper.TwoPi) * 0.5f + 0.5f;
                string hint = $"> {ContinueHint.Value}<";
                Vector2 hintSize = font.MeasureString(hint) * 0.6f;
                Vector2 hintPos = new(panelRect.Right - Padding - hintSize.X, panelRect.Bottom - Padding - hintSize.Y);
                Utils.DrawBorderString(spriteBatch, hint, hintPos, Color.Gold * blink * contentAlpha, 0.6f);
            }
            if (!finishedCurrent) {
                string fast = FastHint.Value;
                Vector2 fastSize = font.MeasureString(fast) * 0.5f;
                Vector2 fastPos = new(panelRect.Right - Padding - fastSize.X, panelRect.Bottom - Padding - fastSize.Y - 16);
                Utils.DrawBorderString(spriteBatch, fast, fastPos, Color.White * 0.4f * contentAlpha, 0.5f);
            }
        }

        private void DrawFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            //外框
            Color edge = Color.Lerp(new Color(120, 200, 255), new Color(200, 230, 255), pulse) * (alpha * 0.65f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.75f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            //内发光边
            Rectangle inner = rect;
            inner.Inflate(-4, -4);
            Color innerC = Color.Gold * (alpha * 0.15f * pulse);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            sb.Draw(px, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.7f);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.85f);
            sb.Draw(px, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.85f);
            //角星
            DrawCornerStar(sb, new Vector2(rect.X + 8, rect.Y + 8), alpha * 0.9f);
            DrawCornerStar(sb, new Vector2(rect.Right - 8, rect.Y + 8), alpha * 0.9f);
            DrawCornerStar(sb, new Vector2(rect.X + 8, rect.Bottom - 8), alpha * 0.7f);
            DrawCornerStar(sb, new Vector2(rect.Right - 8, rect.Bottom - 8), alpha * 0.7f);
        }

        private void DrawCornerStar(SpriteBatch sb, Vector2 pos, float a) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            float size = 4f;
            Color c = Color.Gold * a;
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.22f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.22f), SpriteEffects.None, 0f);
        }

        private void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }
            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 10f));
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }

        //视觉粒子
        private readonly List<StarFx> starFx = new();
        private int starSpawnTimer = 0;
        private float panelPulseTimer = 0f;
        private float scanTimer = 0f;

        private class StarFx
        {
            public Vector2 Pos;
            public float BaseRadius;
            public float Rot;
            public float Life;
            public float MaxLife;
            public float Seed;
            public StarFx(Vector2 p) {
                Pos = p;
                BaseRadius = Main.rand.NextFloat(2f, 4f);
                Rot = Main.rand.NextFloat(MathHelper.TwoPi);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(60f, 140f);
                Seed = Main.rand.NextFloat(10f);
            }
            public bool Update(Vector2 panelPos, Vector2 panelSize) {
                Life++;
                Rot += 0.02f;
                float t = Life / MaxLife;
                float amp = (float)Math.Sin((t + Seed) * Math.PI) * 0.5f + 0.5f;
                float drift = (float)Math.Sin((Life + Seed * 20f) * 0.03f) * 6f;
                Pos.X += drift * 0.02f;
                //若超出范围或生命结束
                if (Life >= MaxLife) {
                    return true;
                }
                //若离开可视边界(容差)
                if (Pos.X < panelPos.X - 40 || Pos.X > panelPos.X + panelSize.X + 40 || Pos.Y < panelPos.Y - 40 || Pos.Y > panelPos.Y + panelSize.Y + 40) {
                    return true;
                }
                return false;
            }
            public void Draw(SpriteBatch sb, float alpha) {
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi) * alpha;
                float scale = BaseRadius * (0.6f + (float)Math.Sin((Life + Seed * 33f) * 0.08f) * 0.4f);
                Color c = Color.Gold * (0.7f * fade);
                Texture2D px = TextureAssets.MagicPixel.Value;
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale, scale * 0.25f), SpriteEffects.None, 0f);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(scale, scale * 0.25f), SpriteEffects.None, 0f);
            }
        }
    }
}