using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
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
    internal class ADVRewardPopup : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.HalibutText.ADV";

        public static ADVRewardPopup Instance => UIHandleLoader.GetUIHandleOfType<ADVRewardPopup>();

        public class RewardEntry
        {
            public int ItemId;
            public int Stack;
            public string CustomText;
            public int Timer;
            public bool Given;
            public float Appear;
            public float Hold;
            public int AppearDuration;
            public int HoldDuration;
            public int GiveDuration;
            public bool RequireClick;
            public Func<Vector2> AnchorProvider;
            public Vector2 Offset;
        }

        private readonly Queue<RewardEntry> queue = new();
        private RewardEntry current;
        private int stateTimer;
        private bool givingOut = false;
        private float panelFade = 0f;
        private float panelScale = 0f;
        private bool justOpened = false;
        //风格动画变量
        private float wavePhase = 0f;
        private float abyssPulse = 0f;
        private float panelPulse = 0f;
        private readonly List<Bubble> bubbles = new();
        private readonly List<Star> stars = new();
        private int bubbleTimer;
        private int starTimer;
        //面板区域缓存和鼠标悬停状态
        private Rectangle panelRect;
        private bool isHovering = false;
        public static int DefaultAppearDuration = 24;
        public static int DefaultHoldDuration = 50;
        public static int DefaultGiveDuration = 16;
        public static event Action<RewardEntry> OnRewardGiven;

        //位置平滑过渡相关
        private Vector2 cachedAnchorPosition; //缓存的锚点位置
        private Vector2 currentDisplayPosition; //当前实际显示位置
        private bool isDialogueClosing = false; //对话框是否正在关闭
        private float positionTransitionProgress = 0f; //位置过渡进度
        private const float PositionTransitionSpeed = 0.08f; //位置过渡速度

        //本地化文本
        protected static LocalizedText ClickToReceive;
        protected static LocalizedText ClickToContinue;
        protected static LocalizedText ClickOrWaitToContinue;

        public override bool Active => current != null || queue.Count > 0 || panelFade > 0.01f;

        public override void SetStaticDefaults() {
            ClickToReceive = this.GetLocalization(nameof(ClickToReceive), () => "点击领取");
            ClickToContinue = this.GetLocalization(nameof(ClickToContinue), () => "点击继续");
            ClickOrWaitToContinue = this.GetLocalization(nameof(ClickOrWaitToContinue), () => "点击/等待继续");
        }

        public static void ConfigureDefaults(int? appear = null, int? hold = null, int? give = null) {
            if (appear.HasValue && appear.Value > 0) {
                DefaultAppearDuration = appear.Value;
            }
            if (hold.HasValue) {
                DefaultHoldDuration = hold.Value;
            }
            if (give.HasValue && give.Value > 0) {
                DefaultGiveDuration = give.Value;
            }
        }
        public static void ShowReward(int itemId, int stack = 1, string text = null,
            int? appearDuration = null, int? holdDuration = null, int? giveDuration = null, bool requireClick = false,
            Func<Vector2> anchorProvider = null, Vector2? offset = null) {
            var inst = Instance;

            if (text == null && itemId > ItemID.None) {
                text = itemId < ItemID.Count ?
                    Language.GetText("ItemName." + ItemID.Search.GetName(itemId)).Value
                    : ItemLoader.GetItem(itemId).GetLocalization("DisplayName").Value;
            }

            inst.queue.Enqueue(new RewardEntry {
                ItemId = itemId,
                Stack = stack <= 0 ? 1 : stack,
                CustomText = text,
                AppearDuration = appearDuration.HasValue && appearDuration.Value > 0 ? appearDuration.Value : DefaultAppearDuration,
                HoldDuration = holdDuration.HasValue ? holdDuration.Value : DefaultHoldDuration,
                GiveDuration = giveDuration.HasValue && giveDuration.Value > 0 ? giveDuration.Value : DefaultGiveDuration,
                RequireClick = requireClick,
                AnchorProvider = anchorProvider,
                Offset = offset ?? Vector2.Zero
            });
        }
        public static void ShowRewards(IEnumerable<(int itemId, int stack, string text, int? appear, int? hold, int? give, bool requireClick, Func<Vector2> anchor, Vector2? offset)> rewards) {
            foreach (var r in rewards) {
                ShowReward(r.itemId, r.stack, r.text, r.appear, r.hold, r.give, r.requireClick, r.anchor, r.offset);
            }
        }
        private void StartNext() {
            if (current != null) {
                return;
            }
            if (queue.Count == 0) {
                return;
            }
            current = queue.Dequeue();
            stateTimer = 0;
            givingOut = false;
            current.Appear = 0f;
            current.Hold = 0f;
            justOpened = true;
            
            //重置位置过渡状态
            isDialogueClosing = false;
            positionTransitionProgress = 0f;
            cachedAnchorPosition = Vector2.Zero;
            currentDisplayPosition = Vector2.Zero;
            
            //初次播放音效
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.4f, Pitch = -0.2f });
        }
        public override void Update() {
            if (!givingOut && current != null) {
                float appearT = current.AppearDuration > 0 ? Math.Clamp(stateTimer / (float)current.AppearDuration, 0f, 1f) : 1f;
                if (appearT >= 1f) {
                    current.Hold++;
                    bool autoReady = false;
                    if (current.HoldDuration >= 0 && current.Hold >= current.HoldDuration) autoReady = true;
                    //只有在鼠标悬停在面板上且点击时才触发
                    bool click = isHovering && keyLeftPressState == KeyPressState.Pressed;
                    if ((autoReady && !current.RequireClick) || (click && appearT >= 0.95f)) {
                        givingOut = true;
                        stateTimer = 0;
                        SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.5f, Pitch = 0.3f });
                    }
                }
            }
        }
        public void LogicUpdate() {
            wavePhase += 0.02f;
            abyssPulse += 0.013f;
            panelPulse += 0.025f;
            if (wavePhase > MathHelper.TwoPi) wavePhase -= MathHelper.TwoPi;
            if (abyssPulse > MathHelper.TwoPi) abyssPulse -= MathHelper.TwoPi;
            if (panelPulse > MathHelper.TwoPi) panelPulse -= MathHelper.TwoPi;
            if (current == null && queue.Count > 0) {
                StartNext();
            }
            if (current == null) {
                if (panelFade > 0f) {
                    panelFade -= 0.08f;
                    if (panelFade < 0f) panelFade = 0f;
                }
                isHovering = false;
                return;
            }
            if (panelFade < 1f) {
                panelFade += 0.12f;
                if (panelFade > 1f) panelFade = 1f;
            }
            if (justOpened) {
                panelScale = 0.6f;
                justOpened = false;
            }
            panelScale = MathHelper.Lerp(panelScale, 1f, 0.18f);
            stateTimer++;
            if (!givingOut) {
                float appearT = current.AppearDuration > 0 ? Math.Clamp(stateTimer / (float)current.AppearDuration, 0f, 1f) : 1f;
                current.Appear = appearT;
                if (appearT >= 1f) {
                    current.Hold++;
                }
            }
            else {
                float t = current.GiveDuration > 0 ? Math.Clamp(stateTimer / (float)current.GiveDuration, 0f, 1f) : 1f;
                current.Appear = 1f - t;
                if (t >= 1f) {
                    GiveCurrent();
                    current = null;
                    StartNext();
                }
            }

            //检测鼠标悬停状态
            Vector2 mousePos = new Vector2(Main.mouseX, Main.mouseY);
            isHovering = panelRect.Contains(mousePos.ToPoint());

            //只有在悬停时才阻止鼠标交互穿透
            if (isHovering && Active) {
                player.mouseInterface = true;
            }

            //粒子刷新
            Vector2 basePos = ResolveBasePosition(new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f));
            bubbleTimer++;
            if (panelFade > 0.6f && bubbleTimer >= 8 && bubbles.Count < 20) {
                bubbleTimer = 0;
                bubbles.Add(new Bubble(basePos + new Vector2(Main.rand.NextFloat(-80f, 80f), 40f)));
            }
            starTimer++;
            if (panelFade > 0.6f && starTimer >= 18 && stars.Count < 12) {
                starTimer = 0;
                stars.Add(new Star(basePos + new Vector2(Main.rand.NextFloat(-100f, 100f), Main.rand.NextFloat(-60f, 20f))));
            }
            for (int i = bubbles.Count - 1; i >= 0; i--) if (bubbles[i].Update()) bubbles.RemoveAt(i);
            for (int i = stars.Count - 1; i >= 0; i--) if (stars[i].Update()) stars.RemoveAt(i);
        }
        private void GiveCurrent() {
            if (current == null || current.Given) return;
            var plr = Main.LocalPlayer;
            if (plr != null && plr.active) {
                int stack = current.Stack <= 0 ? 1 : current.Stack;
                var source = plr.GetSource_GiftOrReward();
                plr.QuickSpawnItem(source, current.ItemId, stack);
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.55f, Pitch = 0.15f });
            }
            current.Given = true;
            OnRewardGiven?.Invoke(current);
        }
        private Vector2 ResolveBasePosition(Vector2 panelCenter) {
            //优先使用自定义锚点提供器
            if (current?.AnchorProvider != null) {
                Vector2 providedPos = current.AnchorProvider() + current.Offset;
                
                //检测对话框是否正在关闭
                if (DialogueUIRegistry.Current != null) {
                    var currentBox = DialogueUIRegistry.Current;
                    
                    //如果对话框刚开始关闭，缓存当前位置
                    if (currentBox.closing && !isDialogueClosing) {
                        isDialogueClosing = true;
                        cachedAnchorPosition = providedPos;
                        positionTransitionProgress = 0f;
                    }
                    
                    //对话框关闭中，使用缓存位置并平滑过渡到屏幕中心
                    if (isDialogueClosing) {
                        positionTransitionProgress += PositionTransitionSpeed;
                        positionTransitionProgress = Math.Clamp(positionTransitionProgress, 0f, 1f);
                        
                        //使用缓动函数平滑过渡
                        float easeProgress = EaseOutCubic(positionTransitionProgress);
                        currentDisplayPosition = Vector2.Lerp(cachedAnchorPosition, panelCenter, easeProgress);
                        return currentDisplayPosition;
                    }
                    else {
                        //对话框正常显示，直接使用提供的位置
                        currentDisplayPosition = providedPos;
                        return providedPos;
                    }
                }
                
                return providedPos;
            }
            
            //如果没有自定义锚点，尝试使用对话框位置
            if (DialogueUIRegistry.Current != null) {
                var rect = DialogueUIRegistry.Current.GetPanelRect();
                if (rect != Rectangle.Empty) {
                    Vector2 dialoguePos = new Vector2(rect.Center.X, rect.Y - 60f);
                    
                    //同样处理对话框关闭的情况
                    var currentBox = DialogueUIRegistry.Current;
                    
                    if (currentBox.closing && !isDialogueClosing) {
                        isDialogueClosing = true;
                        cachedAnchorPosition = dialoguePos;
                        positionTransitionProgress = 0f;
                    }
                    
                    if (isDialogueClosing) {
                        positionTransitionProgress += PositionTransitionSpeed;
                        positionTransitionProgress = Math.Clamp(positionTransitionProgress, 0f, 1f);
                        
                        float easeProgress = EaseOutCubic(positionTransitionProgress);
                        currentDisplayPosition = Vector2.Lerp(cachedAnchorPosition, panelCenter, easeProgress);
                        return currentDisplayPosition;
                    }
                    
                    return dialoguePos;
                }
            }
            
            //默认返回屏幕中心
            return panelCenter;
        }
        
        ///<summary>
        ///平滑缓出函数
        ///</summary>
        private static float EaseOutCubic(float t) {
            return 1f - (float)Math.Pow(1f - t, 3f);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (panelFade <= 0f) return;
            if (current == null && queue.Count == 0 && panelFade <= 0.01f) return;
            Vector2 screenCenter = new(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
            Vector2 anchor = ResolveBasePosition(screenCenter);
            float alpha = panelFade;
            Texture2D px = TextureAssets.MagicPixel.Value;
            Vector2 panelSize = new(240f, 132f);
            float slideIn = 1f - (float)Math.Pow(1f - panelFade, 3f);
            Vector2 drawPos = anchor - panelSize / 2f;
            drawPos.Y -= MathHelper.Lerp(80f, 0f, slideIn);
            Rectangle rect = new((int)drawPos.X, (int)drawPos.Y, (int)panelSize.X, (int)panelSize.Y);

            //缓存面板矩形供鼠标检测使用
            panelRect = rect;

            //鼠标悬停时添加高亮效果
            float hoverGlow = isHovering ? 0.15f : 0f;

            //深海渐层背景条
            int segs = 26;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                int height = Math.Max(1, y2 - y1);
                
                Rectangle r = new(rect.X, y1, rect.Width, height);
                
                Color abyssDeep = new Color(2, 10, 18);
                Color abyssMid = new Color(6, 32, 48);
                Color bioEdge = new Color(12, 80, 110);
                float breathing = (float)Math.Sin(abyssPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(abyssDeep, abyssMid, (float)Math.Sin(panelPulse * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, bioEdge, t * 0.55f * (0.4f + breathing * 0.6f));
                c *= alpha * (0.92f + hoverGlow);
                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }
            //波浪横线
            DrawWaveLines(spriteBatch, rect, alpha * 0.65f);
            //内边微光
            Rectangle inner = rect; inner.Inflate(-6, -6);
            spriteBatch.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(30, 120, 150) * (alpha * (0.08f + hoverGlow * 0.5f) * (0.4f + (float)Math.Sin(panelPulse * 1.3f) * 0.6f)));
            //描边与角标
            DrawFrame(spriteBatch, rect, alpha, hoverGlow);
            //气泡与星光
            foreach (var b in bubbles) b.Draw(spriteBatch, alpha * 0.85f);
            foreach (var s in stars) s.Draw(spriteBatch, alpha * 0.5f);
            if (current != null) DrawRewardContent(spriteBatch, rect, alpha);
        }
        private void DrawRewardContent(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            float a = current.Appear;
            float ease = (float)Math.Sin(a * MathHelper.PiOver2);
            float iconScaleEase = MathHelper.Lerp(0.35f, 1f, ease);
            float iconAlpha = Math.Clamp(a, 0f, 1f);
            Vector2 iconCenter = new(rect.Center.X, rect.Center.Y - 12f);
            Item item = new();
            item.SetDefaults(current.ItemId);
            Main.instance.LoadItem(current.ItemId);
            Texture2D tex = TextureAssets.Item[item.type].Value;
            Rectangle frame = tex.Bounds;
            Vector2 origin = frame.Size() / 2f;
            float itemScale = 1f;
            if (frame.Width > 40 || frame.Height > 40) {
                float maxDim = Math.Max(frame.Width, frame.Height);
                itemScale = 40f / maxDim;
            }
            float bounce = (float)Math.Sin(MathHelper.Clamp(ease * 1.2f, 0f, 1f) * MathHelper.Pi) * 0.08f;
            itemScale *= (iconScaleEase + bounce) * panelScale;
            float floatOff = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.2f + a) * 4f * iconAlpha;
            Vector2 floatPos = iconCenter + new Vector2(0, floatOff);

            VaultUtils.SimpleDrawItem(spriteBatch, current.ItemId, floatPos, 140, itemScale * 6, 0, Color.White * (iconAlpha * alpha));

            string name = current.CustomText;
            if (string.IsNullOrEmpty(name)) name = item.Name;
            var font = FontAssets.MouseText.Value;
            Vector2 namePos = iconCenter + new Vector2(0, 40f);
            float nameAlpha = iconAlpha * alpha;
            Color nameGlow = new Color(140, 230, 255) * (nameAlpha * 0.6f);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Vector2 off = ang.ToRotationVector2() * 1.7f;
                Utils.DrawBorderString(spriteBatch, name, namePos + off, nameGlow * 0.55f, 0.8f);
            }
            Utils.DrawBorderString(spriteBatch, name, namePos, Color.White * nameAlpha, 0.8f);
            if (a >= 1f) {
                string hint;
                if (current.RequireClick) {
                    hint = ClickToReceive.Value;
                }
                else if (current.HoldDuration < 0) {
                    hint = ClickToContinue.Value;
                }
                else {
                    hint = ClickOrWaitToContinue.Value;
                }
                Vector2 hs = font.MeasureString(hint) * 0.6f;
                Vector2 hp = new(rect.Right - hs.X - 12f, rect.Bottom - hs.Y - 10f);
                float blink = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.5f + 0.5f;
                //悬停时提示文字更亮
                float hintAlpha = isHovering ? blink * alpha * 1.2f : blink * alpha * 0.7f;
                Utils.DrawBorderString(spriteBatch, hint, hp, new Color(140, 230, 255) * hintAlpha, 0.7f);
            }
        }
        private void DrawFrame(SpriteBatch sb, Rectangle rect, float alpha, float hoverGlow = 0f) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            Color edge = new Color(70, 180, 230) * (alpha * (0.85f + hoverGlow * 0.3f));
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            DrawCornerStar(sb, new Vector2(rect.X + 10, rect.Y + 10), alpha * (0.9f + hoverGlow * 0.4f));
            DrawCornerStar(sb, new Vector2(rect.Right - 10, rect.Y + 10), alpha * (0.9f + hoverGlow * 0.4f));
            DrawCornerStar(sb, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * (0.6f + hoverGlow * 0.3f));
            DrawCornerStar(sb, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * (0.6f + hoverGlow * 0.3f));
        }
        private void DrawWaveLines(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            int bands = 4;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 18 + t * (rect.Height - 36);
                float amp = 5f + (float)Math.Sin((wavePhase + t) * 2f) * 3.2f;
                float thickness = 2f;
                int segments = 38;
                Vector2 prev = Vector2.Zero;
                for (int s = 0; s <= segments; s++) {
                    float p = s / (float)segments;
                    float localY = y + (float)Math.Sin(wavePhase * 2f + p * MathHelper.TwoPi * 1.1f + t) * amp;
                    Vector2 point = new(rect.X + 8 + p * (rect.Width - 16), localY);
                    if (s > 0) {
                        Vector2 diff = point - prev;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color c = new Color(30, 120, 170) * (alpha * 0.06f);
                            sb.Draw(px, prev, new Rectangle(0, 0, 1, 1), c, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }
        private void DrawCornerStar(SpriteBatch sb, Vector2 pos, float a) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            float size = 5f;
            Color c = new Color(150, 230, 255) * a;
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
            sb.Draw(px, pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.26f), SpriteEffects.None, 0f);
        }
        private class Bubble
        {
            public Vector2 Pos;
            public float R;
            public float Life;
            public float MaxLife;
            public float Seed;
            public Bubble(Vector2 p) {
                Pos = p;
                R = Main.rand.NextFloat(3f, 7f);
                Life = 0f;
                MaxLife = Main.rand.NextFloat(60f, 120f);
                Seed = Main.rand.NextFloat(10f);
            }
            public bool Update() {
                Life++;
                Pos.Y -= 0.6f + (float)Math.Sin(Life * 0.05f + Seed) * 0.2f;
                Pos.X += (float)Math.Sin(Life * 0.04f + Seed) * 0.5f;
                if (Life >= MaxLife) return true;
                return false;
            }
            public void Draw(SpriteBatch sb, float a) {
                Texture2D px = TextureAssets.MagicPixel.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi);
                float scale = R * (0.8f + (float)Math.Sin((Life + Seed * 15f) * 0.08f) * 0.2f);
                Color core = new Color(140, 230, 255) * (a * 0.4f * fade);
                Color rim = new Color(30, 100, 150) * (a * 0.25f * fade);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), rim, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale * 1.6f, scale * 0.5f), SpriteEffects.None, 0f);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), core, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale, scale), SpriteEffects.None, 0f);
            }
        }
        private class Star
        {
            public Vector2 Pos;
            public float Life;
            public float MaxLife;
            public float Seed;
            public Star(Vector2 p) {
                Pos = p;
                Life = 0f;
                MaxLife = Main.rand.NextFloat(70f, 140f);
                Seed = Main.rand.NextFloat(10f);
            }
            public bool Update() {
                Life++;
                if (Life >= MaxLife) return true;
                return false;
            }
            public void Draw(SpriteBatch sb, float a) {
                Texture2D px = TextureAssets.MagicPixel.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * MathHelper.Pi) * a;
                float scale = 2.5f + (float)Math.Sin((Life + Seed * 30f) * 0.1f) * 1.2f;
                Color c = Color.Gold * (0.6f * fade);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), c, 0f, new Vector2(0.5f, 0.5f), new Vector2(scale, scale * 0.3f), SpriteEffects.None, 0f);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), c * 0.8f, MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(scale, scale * 0.3f), SpriteEffects.None, 0f);
            }
        }
    }
}
