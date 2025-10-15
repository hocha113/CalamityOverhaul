using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV
{
    internal class ADVRewardPopup : UIHandle
    {
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
        public static int DefaultAppearDuration = 24;
        public static int DefaultHoldDuration = 50;
        public static int DefaultGiveDuration = 16;
        public static event Action<RewardEntry> OnRewardGiven;
        public override bool Active => current != null || queue.Count > 0 || panelFade > 0.01f;
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
                    bool click = keyLeftPressState == KeyPressState.Pressed;
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
            player.mouseInterface |= Active;
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
            if (current?.AnchorProvider != null) return current.AnchorProvider() + current.Offset;
            if (DialogueUIRegistry.Current != null) {
                var rect = DialogueUIRegistry.Current.GetPanelRect();
                if (rect != Rectangle.Empty) return new Vector2(rect.Center.X, rect.Y - 60f);
            }
            return panelCenter;
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
            //深海渐层背景条
            int segs = 26;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / segs;
                int y1 = rect.Y + (int)(t * rect.Height);
                int y2 = rect.Y + (int)(t2 * rect.Height);
                Rectangle r = new(rect.X, y1, rect.Width, Math.Max(1, y2 - y1) * 10);
                Color abyssDeep = new Color(2, 10, 18);
                Color abyssMid = new Color(6, 32, 48);
                Color bioEdge = new Color(12, 80, 110);
                float breathing = (float)Math.Sin(abyssPulse) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(abyssDeep, abyssMid, (float)Math.Sin(panelPulse * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, bioEdge, t * 0.55f * (0.4f + breathing * 0.6f));
                c *= alpha * 0.92f;
                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }
            //波浪横线
            DrawWaveLines(spriteBatch, rect, alpha * 0.65f);
            //内边微光
            Rectangle inner = rect; inner.Inflate(-6, -6);
            spriteBatch.Draw(px, inner, new Rectangle(0, 0, 1, 1), new Color(30, 120, 150) * (alpha * 0.08f * (0.4f + (float)Math.Sin(panelPulse * 1.3f) * 0.6f)));
            //描边与角标
            DrawFrame(spriteBatch, rect, alpha);
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
            spriteBatch.Draw(tex, floatPos, frame, Color.White * (iconAlpha * alpha), 0f, origin, itemScale, SpriteEffects.None, 0f);
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
                if (current.RequireClick) hint = "点击领取"; else if (current.HoldDuration < 0) hint = "点击继续"; else hint = "点击/等待继续";
                Vector2 hs = font.MeasureString(hint) * 0.6f;
                Vector2 hp = new(rect.Right - hs.X - 12f, rect.Bottom - hs.Y - 10f);
                float blink = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.5f + 0.5f;
                Utils.DrawBorderString(spriteBatch, hint, hp, new Color(140, 230, 255) * (blink * alpha), 0.7f);
            }
        }
        private void DrawFrame(SpriteBatch sb, Rectangle rect, float alpha) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            Color edge = new Color(70, 180, 230) * (alpha * 0.85f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            DrawCornerStar(sb, new Vector2(rect.X + 10, rect.Y + 10), alpha * 0.9f);
            DrawCornerStar(sb, new Vector2(rect.Right - 10, rect.Y + 10), alpha * 0.9f);
            DrawCornerStar(sb, new Vector2(rect.X + 10, rect.Bottom - 10), alpha * 0.6f);
            DrawCornerStar(sb, new Vector2(rect.Right - 10, rect.Bottom - 10), alpha * 0.6f);
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
