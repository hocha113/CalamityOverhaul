using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
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

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen
{
    /// <summary>
    /// 提供普通鱼以换取一瓶鱼油的任务窗口
    /// </summary>
    internal class FishoilQuestUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public static FishoilQuestUI Instance => UIHandleLoader.GetUIHandleOfType<FishoilQuestUI>();

        //本地化
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText DescText { get; private set; }
        public static LocalizedText SubmitHint { get; private set; }
        public static LocalizedText DeclineText { get; private set; }
        public static LocalizedText CompletedText { get; private set; }
        public static LocalizedText RewardText { get; private set; }

        //需求配置(复用)
        private readonly List<QuestMaterialSlot> RequiredItems = new();

        //状态
        private float showProgress;
        private float contentFade;
        private float pulseTimer;
        private Rectangle declineButtonRect;
        private bool hoverDecline;
        private bool rewarded; //是否已经发放奖励

        //布局常量
        private const int PanelWidth = 260;
        private const int PanelHeight = 230;
        private const int Padding = 12;
        private const int SlotSize = 40;
        private const int SlotSpacing = 10;

        public override bool Active {
            get {
                if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var hp)) return false;
                if (hp.ADCSave.FishoilQuestDeclined) return false;
                if (!hp.ADCSave.FishoilQuestAccepted) return false;
                if (hp.ADCSave.FishoilQuestCompleted && showProgress <= 0f && rewarded) return false;
                return true;
            }
        }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "采集:鲈鱼");
            DescText = this.GetLocalization(nameof(DescText), () => "放入足够数量的鲈鱼，我就可以提炼出一瓶鱼油");
            SubmitHint = this.GetLocalization(nameof(SubmitHint), () => "点击放置/取出");
            DeclineText = this.GetLocalization(nameof(DeclineText), () => "拒绝");
            CompletedText = this.GetLocalization(nameof(CompletedText), () => "完成");
            RewardText = this.GetLocalization(nameof(RewardText), () => "领取奖励");
        }

        public void OpenPersistent() {
            if (RequiredItems.Count == 0) InitDefaultRequirement();
            showProgress = Math.Max(showProgress, 0.01f);
        }

        private void InitDefaultRequirement() {
            //默认需求:鲈鱼 30(可调整)
            RequiredItems.Clear();
            RequiredItems.Add(new QuestMaterialSlot(ItemID.Bass, 30));
        }

        public override void Update() {
            if (RequiredItems.Count == 0) InitDefaultRequirement();

            float target = Active ? 1f : 0f;
            showProgress = MathHelper.Lerp(showProgress, target, 0.15f);
            if (Math.Abs(showProgress - target) < 0.01f) showProgress = target;
            if (showProgress <= 0f) return;

            if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var hp)) return;

            if (showProgress > 0.3f) contentFade = Math.Min(1f, contentFade + 0.08f);
            else contentFade = Math.Max(0f, contentFade - 0.15f);

            pulseTimer += 0.03f;

            Size = new Vector2(PanelWidth, PanelHeight);
            DrawPosition = new Vector2(Main.screenWidth - PanelWidth - 20, Main.screenHeight * 0.35f);
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);
            if (hoverInMainPage) player.mouseInterface = true;

            declineButtonRect = new Rectangle((int)(DrawPosition.X + PanelWidth - Padding - 64), (int)(DrawPosition.Y + PanelHeight - Padding - 28), 64, 24);
            hoverDecline = declineButtonRect.Contains(MouseHitBox);

            Vector2 slotsStart = DrawPosition + new Vector2(Padding, 76);
            for (int i = 0; i < RequiredItems.Count; i++) {
                var slot = RequiredItems[i];
                slot.Update(slotsStart + new Vector2(i * (SlotSize + SlotSpacing), 0), SlotSize, player, hoverInMainPage);
            }

            //拒绝任务(未完成时可拒绝,完成后隐藏按钮)
            if (keyLeftPressState == KeyPressState.Pressed && hoverDecline && !hp.ADCSave.FishoilQuestCompleted) {
                hp.ADCSave.FishoilQuestDeclined = true;
                SoundEngine.PlaySound(SoundID.MenuClose);
            }

            //完成检测
            if (!hp.ADCSave.FishoilQuestCompleted) {
                bool allDone = true;
                foreach (var s in RequiredItems) if (!s.IsSatisfied) { allDone = false; break; }
                if (allDone) {
                    hp.ADCSave.FishoilQuestCompleted = true;
                    SoundEngine.PlaySound(SoundID.Research);
                }
            }

            //发放奖励(只发一次)
            if (hp.ADCSave.FishoilQuestCompleted && !rewarded) {
                //点击奖励按钮领取,使用槽位置文本做提示
                Rectangle rewardRect = new Rectangle((int)(DrawPosition.X + Padding), (int)(DrawPosition.Y + PanelHeight - 58), 120, 20);
                if (rewardRect.Contains(MouseHitBox)) {
                    if (keyLeftPressState == KeyPressState.Pressed) {
                        rewarded = true;
                        Item reward = new Item(ModContent.ItemType<Fishoil>());
                        reward.stack = 1;
                        Main.LocalPlayer.QuickSpawnItem(Main.LocalPlayer.GetSource_Misc("FishoilQuestReward"), reward, reward.stack);
                        SoundEngine.PlaySound(SoundID.Item4);
                    }
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (showProgress <= 0.01f) return;
            float alpha = Math.Min(showProgress * 1.6f, 1f);
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle panelRect = UIHitBox;

            Rectangle shadow = panelRect; shadow.Offset(4, 5);
            spriteBatch.Draw(px, shadow, new Rectangle(0,0,1,1), Color.Black * (alpha * 0.55f));

            int segs = 20;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i+1)/(float)segs;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new Rectangle(panelRect.X, y1, panelRect.Width, Math.Max(1,y2 - y1));
                Color deep = new Color(4,18,30);
                Color mid = new Color(10,42,60);
                Color edge = new Color(20,90,120);
                float osc = (float)Math.Sin(pulseTimer * 1.2f + t * 3f) * 0.5f + 0.5f;
                Color c = Color.Lerp(Color.Lerp(deep, mid, osc), edge, t * 0.55f);
                spriteBatch.Draw(px, r, new Rectangle(0,0,1,1), c * alpha);
            }

            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.5f + 0.5f;
            spriteBatch.Draw(px, panelRect, new Rectangle(0,0,1,1), new Color(30,120,150) * (alpha * 0.08f * pulse));

            DrawFrameOcean(spriteBatch, panelRect, alpha, pulse);

            if (contentFade <= 0.01f) return;
            float ca = contentFade;

            Vector2 titlePos = DrawPosition + new Vector2(Padding, Padding);
            string title = TitleText.Value;
            Color glow = new Color(140,230,255) * (ca * 0.7f);
            for(int i=0;i<4;i++){ float ang = MathHelper.TwoPi * i / 4f; Vector2 off = ang.ToRotationVector2() * 1.6f; Utils.DrawBorderString(spriteBatch, title, titlePos + off, glow * 0.55f, 0.9f); }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * ca, 0.9f);

            Utils.DrawBorderString(spriteBatch, DescText.Value, titlePos + new Vector2(0, 30), new Color(180,230,250) * ca, 0.65f);

            Vector2 divStart = titlePos + new Vector2(0, 52);
            Vector2 divEnd = divStart + new Vector2(PanelWidth - Padding * 2, 0);
            DrawGradientLine(spriteBatch, divStart, divEnd, new Color(70,180,230)*(ca * 0.85f), new Color(70,180,230)*(ca * 0.05f), 1.2f);

            foreach(var slot in RequiredItems) slot.Draw(spriteBatch, ca);

            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var hp)) {
                if (!hp.ADCSave.FishoilQuestCompleted) {
                    Utils.DrawBorderString(spriteBatch, SubmitHint.Value, DrawPosition + new Vector2(Padding, PanelHeight - 40), new Color(120,200,235)*(ca*0.7f), 0.7f);
                } else {
                    Utils.DrawBorderString(spriteBatch, CompletedText.Value, DrawPosition + new Vector2(Padding, PanelHeight - 40), new Color(150,230,255)* ca, 0.7f);
                    //奖励按钮提示
                    if (!rewarded) {
                        Rectangle rewardRect = new Rectangle((int)(DrawPosition.X + Padding), (int)(DrawPosition.Y + PanelHeight - 58), 120, 20);
                        Color rrC = rewardRect.Contains(MouseHitBox) ? new Color(30,120,170) : new Color(12,60,90);
                        spriteBatch.Draw(px, rewardRect, new Rectangle(0,0,1,1), rrC * (ca * 0.9f));
                        Utils.DrawBorderString(spriteBatch, RewardText.Value, new Vector2(rewardRect.X + 6, rewardRect.Y + 2), Color.White * ca, 0.65f);
                    }
                }
            }

            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var hp2) && !hp2.ADCSave.FishoilQuestCompleted) {
                Color btnBase = hoverDecline ? new Color(100,40,40) : new Color(70,30,30);
                spriteBatch.Draw(px, declineButtonRect, new Rectangle(0,0,1,1), btnBase * (ca * 0.9f));
                Utils.DrawBorderString(spriteBatch, DeclineText.Value, new Vector2(declineButtonRect.X + 6, declineButtonRect.Y + 4), Color.White * ca, 0.7f);
            }
        }

        private static void DrawFrameOcean(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color edge = Color.Lerp(new Color(30,140,190), new Color(90,210,255), pulse) * (alpha * 0.85f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0,0,1,1), edge);
            sb.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0,0,1,1), edge * 0.7f);
            sb.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0,0,1,1), edge * 0.85f);
            sb.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0,0,1,1), edge * 0.85f);
            Rectangle inner = rect; inner.Inflate(-5,-5);
            Color innerC = new Color(120,220,255) * (alpha * 0.18f * pulse);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0,0,1,1), innerC);
            sb.Draw(px, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0,0,1,1), innerC * 0.65f);
            sb.Draw(px, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0,0,1,1), innerC * 0.85f);
            sb.Draw(px, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0,0,1,1), innerC * 0.85f);
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value; Vector2 edge = end - start; float length = edge.Length(); if (length < 1f) return; edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X); int segments = Math.Max(1, (int)(length / 11f));
            for(int i=0;i<segments;i++){ float t = i/(float)segments; Vector2 segPos = start + edge * (length * t); float segLength = length / segments; Color c = Color.Lerp(startColor, endColor, t); spriteBatch.Draw(pixel, segPos, new Rectangle(0,0,1,1), c, rotation, new Vector2(0,0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0); }
        }

        private class QuestMaterialSlot {
            public int ItemType; public int Need; public int Current; public Vector2 Pos; public Rectangle Hit; public bool Hover; public bool IsSatisfied => Current >= Need;
            public QuestMaterialSlot(int itemType, int need) { ItemType = itemType; Need = need; }
            public void Update(Vector2 drawPos, int size, Player player, bool uiHover) {
                Pos = drawPos; Hit = new Rectangle((int)Pos.X, (int)Pos.Y, size, size); Hover = uiHover && Hit.Contains(Main.mouseX, Main.mouseY);
                if (Hover) player.mouseInterface = true;
                if (Hover && Main.mouseLeft && Main.mouseLeftRelease) {
                    if (Main.mouseItem.type == ItemType) {
                        if (IsSatisfied) return; Current += 1; if (Current > Need) Current = Need; Main.mouseItem.stack -= 1; if (Main.mouseItem.stack <= 0) Main.mouseItem.TurnToAir(); SoundEngine.PlaySound(SoundID.Grab);
                    }
                    else if (Main.mouseItem.IsAir && Current > 0) {
                        Item give = new Item(ItemType); give.stack = 1; Main.mouseItem = give; Current -= 1; SoundEngine.PlaySound(SoundID.Grab);
                    }
                }
            }
            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D px = VaultAsset.placeholder2.Value; Rectangle r = new Rectangle((int)Pos.X, (int)Pos.Y, Hit.Width, Hit.Height); Color back = new Color(6,32,48) * (alpha * 0.9f); if (Hover) back *= 1.15f; sb.Draw(px, r, new Rectangle(0,0,1,1), back);
                Color edge = IsSatisfied ? new Color(70,180,230) : new Color(40,100,140); sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 2), new Rectangle(0,0,1,1), edge); sb.Draw(px, new Rectangle(r.X, r.Bottom -2, r.Width, 2), new Rectangle(0,0,1,1), edge * 0.7f); sb.Draw(px, new Rectangle(r.X, r.Y, 2, r.Height), new Rectangle(0,0,1,1), edge * 0.85f); sb.Draw(px, new Rectangle(r.Right -2, r.Y, 2, r.Height), new Rectangle(0,0,1,1), edge * 0.85f);
                if (ItemType > ItemID.None) { Main.instance.LoadItem(ItemType); Texture2D tex = TextureAssets.Item[ItemType].Value; Rectangle frame = tex.Frame(); Vector2 center = new Vector2(r.X + r.Width/2f, r.Y + r.Height/2f); float scale = Math.Min((r.Width - 10f)/frame.Width, (r.Height - 10f)/frame.Height); Color itemColor = IsSatisfied ? Color.White : Color.White * 0.9f; sb.Draw(tex, center, frame, itemColor * alpha, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f); }
                string text = $"{Current}/{Need}"; Utils.DrawBorderString(sb, text, new Vector2(r.X + 4, r.Bottom - 18), IsSatisfied ? Color.Cyan * alpha : Color.White * alpha, 0.65f);
            }
        }
    }
}
