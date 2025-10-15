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
            public string CustomText; //可选自定义文本
            public int Timer; //内部计时
            public bool Given; //是否已发放
            public float Appear; //出现进度 0-1
            public float Hold; //停留计时
        }

        private readonly Queue<RewardEntry> queue = new();
        private RewardEntry current;
        private int stateTimer; //状态计时
        private const int AppearDuration = 24;
        private const int HoldDuration = 50;
        private const int GiveDuration = 16; //给出淡出
        private bool givingOut = false;
        private float panelFade = 0f;
        private float panelScale = 0f;
        private bool justOpened = false;

        //外部事件
        public static event Action<RewardEntry> OnRewardGiven;

        public override bool Active => current != null || queue.Count > 0 || panelFade > 0.01f;

        public static void ShowReward(int itemId, int stack = 1, string text = null) {
            var inst = Instance;
            inst.queue.Enqueue(new RewardEntry {
                ItemId = itemId,
                Stack = stack <= 0 ? 1 : stack,
                CustomText = text
            });
        }

        public static void ShowRewards(IEnumerable<(int itemId, int stack, string text)> rewards) {
            foreach (var r in rewards) {
                ShowReward(r.itemId, r.stack, r.text);
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

        public override void Update() { }

        public void LogicUpdate() {
            if (current == null && queue.Count > 0) {
                StartNext();
            }
            if (current == null) {
                //面板淡出
                if (panelFade > 0f) {
                    panelFade -= 0.08f;
                    if (panelFade < 0f) {
                        panelFade = 0f;
                    }
                }
                return;
            }
            //面板淡入
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
                //出现阶段
                float appearT = Math.Clamp(stateTimer / (float)AppearDuration, 0f, 1f);
                current.Appear = appearT;
                if (appearT >= 1f) {
                    current.Hold++;
                    if (current.Hold >= HoldDuration || keyLeftPressState == KeyPressState.Pressed) {
                        givingOut = true;
                        stateTimer = 0;
                    }
                }
            } else {
                //淡出并发放
                float t = Math.Clamp(stateTimer / (float)GiveDuration, 0f, 1f);
                current.Appear = 1f - t;
                if (t >= 1f) {
                    GiveCurrent();
                    current = null;
                    StartNext();
                    if (current == null && queue.Count == 0) {
                        //全部结束
                    }
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
                //使用快速生成给玩家
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
                Vector2 size = font.MeasureString(name);
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
                    string hint = DialogueBoxBase.ContinueHint.Value;
                    Vector2 hs = font.MeasureString(hint) * 0.6f;
                    Vector2 hp = drawPos + new Vector2(panelSize.X - hs.X - 12f, panelSize.Y - hs.Y - 10f);
                    float blink = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.5f + 0.5f;
                    Utils.DrawBorderString(spriteBatch, hint, hp, new Color(140, 230, 255) * (blink * alpha), 0.7f);
                }
            }
        }
    }
}
