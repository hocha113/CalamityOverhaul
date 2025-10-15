using InnoVault.UIHandles;
using Microsoft.Xna.Framework;
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
            public Func<Vector2> AnchorProvider; //返回锚点中心位置 可为空
            public Vector2 Offset; //在锚点基础上的偏移
        }
        private readonly Queue<RewardEntry> queue = new();
        private RewardEntry current;
        private int stateTimer;
        private bool givingOut = false;
        private float panelFade = 0f;
        private float panelScale = 0f;
        private bool justOpened = false;
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
        }
        public void LogicUpdate() {
            if (current == null && queue.Count > 0) {
                StartNext();
            }
            if (current == null) {
                if (panelFade > 0f) {
                    panelFade -= 0.08f;
                    if (panelFade < 0f) {
                        panelFade = 0f;
                    }
                }
                return;
            }
            if (panelFade < 1f) {
                panelFade += 0.12f;
                if (panelFade > 1f) {
                    panelFade = 1f;
                }
            }
            if (justOpened) {
                panelScale = 0.6f;
                justOpened = false;
            }
            panelScale = MathHelper.Lerp(panelScale, 1f, 0.18f);
            stateTimer++;
            if (!givingOut) {
                float appearT = 0f;
                if (current.AppearDuration > 0) {
                    appearT = Math.Clamp(stateTimer / (float)current.AppearDuration, 0f, 1f);
                } else {
                    appearT = 1f;
                }
                current.Appear = appearT;
                if (appearT >= 1f) {
                    current.Hold++;
                    bool autoReady = false;
                    if (current.HoldDuration >= 0) {
                        if (current.Hold >= current.HoldDuration) {
                            autoReady = true;
                        }
                    }
                    bool click = keyLeftPressState == KeyPressState.Pressed;
                    if ((autoReady && !current.RequireClick) || (click && appearT >= 0.95f)) {
                        givingOut = true;
                        stateTimer = 0;
                        SoundEngine.PlaySound(SoundID.MenuOpen with { Volume = 0.5f, Pitch = 0.3f });
                    }
                }
            } else {
                float t = 0f;
                if (current.GiveDuration > 0) {
                    t = Math.Clamp(stateTimer / (float)current.GiveDuration, 0f, 1f);
                } else {
                    t = 1f;
                }
                current.Appear = 1f - t;
                if (t >= 1f) {
                    GiveCurrent();
                    current = null;
                    StartNext();
                }
            }
            player.mouseInterface |= Active;
        }
        private void GiveCurrent() {
            if (current == null) {
                return;
            }
            if (current.Given) {
                return;
            }
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
            if (current?.AnchorProvider != null) {
                return current.AnchorProvider() + current.Offset;
            }
            if (DialogueUIRegistry.Current != null) {
                var rect = DialogueUIRegistry.Current.GetPanelRect();
                if (rect != Rectangle.Empty) {
                    Vector2 above = new(rect.Center.X, rect.Y - 60f);
                    return above;
                }
            }
            return panelCenter;
        }
        public override void Draw(SpriteBatch spriteBatch) {
            if (panelFade <= 0f) {
                return;
            }
            if (current == null && queue.Count == 0 && panelFade <= 0.01f) {
                return;
            }
            Vector2 screenCenter = new(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
            Vector2 anchor = ResolveBasePosition(screenCenter);
            float alpha = panelFade;
            Texture2D px = TextureAssets.MagicPixel.Value;
            Vector2 panelSize = new(240f, 132f);
            float slideIn = 1f - (float)Math.Pow(1f - panelFade, 3f);
            Vector2 drawPos = anchor - panelSize / 2f;
            drawPos.Y -= MathHelper.Lerp(80f, 0f, slideIn);
            float scalePulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.04f + 0.96f;
            float sc = panelScale * scalePulse;
            Rectangle rect = new((int)drawPos.X, (int)drawPos.Y, (int)panelSize.X, (int)panelSize.Y);
            //背景分层
            for (int i = 0; i < 18; i++) {
                float t = i / 17f;
                Rectangle r = rect;
                int inset = (int)(t * 12f);
                r.Inflate(-inset, -inset / 2);
                if (r.Width <= 4 || r.Height <= 4) {
                    continue;
                }
                float bandA = alpha * (0.06f * (1f - t) + 0.04f);
                Color c = Color.Lerp(new Color(5, 25, 32), new Color(18, 90, 120), t) * bandA;
                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), c);
            }
            //外框光晕
            DrawGlowRect(spriteBatch, rect, new Color(70, 180, 230) * (alpha * 0.6f));
            //涟漪环
            float ringTime = (float)(Main.GlobalTimeWrappedHourly * 1.4f);
            for (int ri = 0; ri < 3; ri++) {
                float rt = (ringTime + ri * 0.33f) % 1f;
                float ringAlpha = (1f - rt) * 0.45f * alpha;
                float ringScale = MathHelper.Lerp(0.65f, 1.15f, rt);
                Rectangle rr = rect;
                int add = (int)(rect.Width * (ringScale - 1f) / 2f);
                rr.Inflate(add, add / 3);
                Color rc = new Color(90, 210, 255) * ringAlpha;
                spriteBatch.Draw(px, rr, new Rectangle(0, 0, 1, 1), rc);
            }
            //当前奖励
            if (current != null) {
                float a = current.Appear;
                float ease = (float)Math.Sin(a * MathHelper.PiOver2);
                float iconScaleEase = MathHelper.Lerp(0.35f, 1f, ease);
                float iconAlpha = Math.Clamp(a, 0f, 1f);
                Vector2 iconCenter = drawPos + panelSize / 2f + new Vector2(0, -12f);
                Item item = new();
                item.SetDefaults(current.ItemId);
                Main.instance.LoadItem(current.ItemId);//这里要加载，不然有些时候看不见物品
                Texture2D tex = TextureAssets.Item[item.type].Value;
                Rectangle frame = tex.Bounds;
                Vector2 origin = frame.Size() / 2f;
                float itemScale = 1f;
                if (frame.Width > 40 || frame.Height > 40) {
                    float maxDim = Math.Max(frame.Width, frame.Height);
                    itemScale = 40f / maxDim;
                }
                //弹性缩放
                float bounce = (float)Math.Sin(MathHelper.Clamp(ease * 1.2f, 0f, 1f) * MathHelper.Pi) * 0.08f;
                itemScale *= (iconScaleEase + bounce) * sc;
                //漂浮
                float floatOff = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.2f + a) * 4f * iconAlpha;
                Vector2 floatPos = iconCenter + new Vector2(0, floatOff);
                spriteBatch.Draw(tex, floatPos, frame, Color.White * (iconAlpha * alpha), 0f, origin, itemScale, SpriteEffects.None, 0f);
                string name = current.CustomText;
                if (string.IsNullOrEmpty(name)) {
                    name = item.Name;
                }
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
                        hint = "点击领取";
                    } else if (current.HoldDuration < 0) {
                        hint = "点击继续";
                    } else {
                        hint = "点击/等待继续";
                    }
                    Vector2 hs = font.MeasureString(hint) * 0.6f;
                    Vector2 hp = drawPos + new Vector2(panelSize.X - hs.X - 12f, panelSize.Y - hs.Y - 10f);
                    float blink = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.5f + 0.5f;
                    Utils.DrawBorderString(spriteBatch, hint, hp, new Color(140, 230, 255) * (blink * alpha), 0.7f);
                }
            }
        }
        private static void DrawGlowRect(SpriteBatch sb, Rectangle rect, Color glow) {
            Texture2D px = TextureAssets.MagicPixel.Value;
            sb.Draw(px, rect, new Rectangle(0, 0, 1, 1), glow * 0.15f);
            int border = 2;
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.6f);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - border, rect.Width, border), new Rectangle(0, 0, 1, 1), glow * 0.4f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.5f);
            sb.Draw(px, new Rectangle(rect.Right - border, rect.Y, border, rect.Height), new Rectangle(0, 0, 1, 1), glow * 0.5f);
        }
    }
}
