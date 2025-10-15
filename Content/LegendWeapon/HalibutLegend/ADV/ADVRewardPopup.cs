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
    /// <summary>
    /// 简易的ADV奖励物品演出UI 显示奖励图标并逐个授予
    /// </summary>
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
            public bool RequireClick; //是否必须点击才发放
        }

        private readonly Queue<RewardEntry> queue = new();
        private RewardEntry current;
        private int stateTimer;
        private bool givingOut = false;
        private float panelFade = 0f;
        private float panelScale = 0f;
        private bool justOpened = false;

        //默认配置(可在运行时修改)
        public static int DefaultAppearDuration = 24;
        public static int DefaultHoldDuration = 50;
        public static int DefaultGiveDuration = 16;

        //外部事件
        public static event Action<RewardEntry> OnRewardGiven;

        public override bool Active => current != null || queue.Count > 0 || panelFade > 0.01f;

        public static void ConfigureDefaults(int? appear = null, int? hold = null, int? give = null) {
            if (appear.HasValue && appear.Value > 0) {
                DefaultAppearDuration = appear.Value;
            }
            if (hold.HasValue) {
                DefaultHoldDuration = hold.Value; //允许-1表示不自动发放
            }
            if (give.HasValue && give.Value > 0) {
                DefaultGiveDuration = give.Value;
            }
        }

        public static void ShowReward(int itemId, int stack = 1, string text = null,
            int? appearDuration = null, int? holdDuration = null, int? giveDuration = null, bool requireClick = false) {
            var inst = Instance;
            inst.queue.Enqueue(new RewardEntry {
                ItemId = itemId,
                Stack = stack <= 0 ? 1 : stack,
                CustomText = text,
                AppearDuration = appearDuration.HasValue && appearDuration.Value > 0 ? appearDuration.Value : DefaultAppearDuration,
                HoldDuration = holdDuration ?? DefaultHoldDuration,
                GiveDuration = giveDuration.HasValue && giveDuration.Value > 0 ? giveDuration.Value : DefaultGiveDuration,
                RequireClick = requireClick
            });
        }

        public static void ShowRewards(IEnumerable<(int itemId, int stack, string text, int? appear, int? hold, int? give, bool requireClick)> rewards) {
            foreach (var r in rewards) {
                ShowReward(r.itemId, r.stack, r.text, r.appear, r.hold, r.give, r.requireClick);
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
                SoundEngine.PlaySound(SoundID.Grab);
            }
            current.Given = true;
            OnRewardGiven?.Invoke(current);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (panelFade <= 0f) {
                return;
            }
            if (current == null && queue.Count == 0 && panelFade <= 0.01f) {
                return;
            }
            Vector2 basePos = new(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
            float alpha = panelFade;
            Texture2D px = TextureAssets.MagicPixel.Value;
            Vector2 panelSize = new(220f, 120f);
            Vector2 drawPos = basePos - panelSize / 2f;
            float scalePulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.04f + 0.96f;
            float sc = panelScale * scalePulse;
            Rectangle rect = new((int)drawPos.X, (int)drawPos.Y, (int)panelSize.X, (int)panelSize.Y);
            //背景
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), new Color(10, 25, 32) * (alpha * 0.9f));
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), new Color(70, 180, 230) * (alpha * 0.8f));
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), new Color(70, 180, 230) * (alpha * 0.6f));
            //当前奖励
            if (current != null) {
                float a = current.Appear;
                float iconScaleEase = MathHelper.Lerp(0.4f, 1f, (float)Math.Sin(a * MathHelper.PiOver2));
                float iconAlpha = Math.Clamp(a, 0f, 1f);
                Vector2 iconCenter = drawPos + panelSize / 2f + new Vector2(0, -10f);
                Item item = new();
                item.SetDefaults(current.ItemId);
                Main.instance.LoadItem(item.type);
                Texture2D tex = TextureAssets.Item[item.type].Value;
                Rectangle frame = tex.Bounds;
                Vector2 origin = frame.Size() / 2f;
                float itemScale = 1f;
                if (frame.Width > 40 || frame.Height > 40) {
                    float maxDim = Math.Max(frame.Width, frame.Height);
                    itemScale = 40f / maxDim;
                }
                itemScale *= iconScaleEase * sc;
                spriteBatch.Draw(tex, iconCenter, frame, Color.White * (iconAlpha * alpha), 0f, origin, itemScale, SpriteEffects.None, 0f);
                string name = current.CustomText ?? item.Name;
                var font = FontAssets.MouseText.Value;

                Vector2 namePos = iconCenter + new Vector2(0, 34f);
                float nameAlpha = iconAlpha * alpha;
                Color nameGlow = new Color(140, 230, 255) * (nameAlpha * 0.6f);
                for (int i = 0; i < 4; i++) {
                    float ang = MathHelper.TwoPi * i / 4f;
                    Vector2 off = ang.ToRotationVector2() * 1.5f;
                    Utils.DrawBorderString(spriteBatch, name, namePos + off, nameGlow * 0.55f, 0.8f);
                }
                Utils.DrawBorderString(spriteBatch, name, namePos, Color.White * nameAlpha, 0.8f);

                //提示
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
    }
}
