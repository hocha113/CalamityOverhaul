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
        public static LocalizedText QuickPutText { get; private set; }

        //需求配置(复用)
        private readonly List<QuestMaterialSlot> RequiredItems = new();

        //状态
        private float showProgress; //展开进度
        private float contentFade; //内容淡入
        private float pulseTimer; //背景脉冲
        private bool hoverDecline;
        private bool hoverQuickPut;
        private bool rewarded; //是否已发放奖励
        private bool closing; //完成后关闭动画
        private float hideProgress; //关闭插值
        private bool collapsed; //是否收起内容
        private float collapseProgress; //收起动画插值(0展开 1收起)
        private bool dragging; //拖拽中
        private Vector2 dragOffset; //拖拽偏移
        private Rectangle headerRect; //标题栏矩形
        private Rectangle collapseButtonRect; //收起按钮矩形

        //布局常量(缩小尺寸)
        private const int BasePanelWidth = 240;
        private const int BasePanelHeight = 190; //最大高度(展开)
        private const int HeaderHeight = 36; //收起后的高度
        private const int Padding = 10;
        private const int SlotSize = 36;
        private const int SlotSpacing = 8;

        //实时尺寸(受收起影响)
        private int currentPanelHeight;

        //按钮区域
        private Rectangle declineButtonRect;
        private Rectangle quickPutButtonRect;

        //换行缓存
        private string[] wrappedDescLines;
        private const float DescScale = 0.65f;
        private float descUsedHeight; //描述实际高度

        public override bool Active {
            get {
                if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var hp)) return false;
                if (hp.ADCSave.FishoilQuestDeclined) return false;
                if (!hp.ADCSave.FishoilQuestAccepted) return false;
                if (hp.ADCSave.FishoilQuestCompleted && rewarded && hideProgress >= 1f) return false; //完全关闭后隐藏
                return true;
            }
        }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "采集:鲈鱼");
            DescText = this.GetLocalization(nameof(DescText), () => "放入足够数量的鲈鱼,我会立刻提炼一瓶鱼油返还给你,过程很快,不需要你等待");
            SubmitHint = this.GetLocalization(nameof(SubmitHint), () => "左键放入/右键取出,Shift批量");
            DeclineText = this.GetLocalization(nameof(DeclineText), () => "拒绝");
            CompletedText = this.GetLocalization(nameof(CompletedText), () => "提炼完成");
            QuickPutText = this.GetLocalization(nameof(QuickPutText), () => "快速放入");
        }

        public void OpenPersistent() {
            if (RequiredItems.Count == 0) InitDefaultRequirement();
            ResetVisualState();
            showProgress = Math.Max(showProgress, 0.01f);
        }

        private void ResetVisualState() {
            contentFade = 0f; hideProgress = 0f; closing = false; rewarded = false; collapsed = false; collapseProgress = 0f;
            foreach (var slot in RequiredItems) slot.ResetCount();
        }

        private void InitDefaultRequirement() {
            //默认需求:鲈鱼 30
            RequiredItems.Clear();
            RequiredItems.Add(new QuestMaterialSlot(ItemID.Bass, 30));
        }

        public override void Update() {
            if (RequiredItems.Count == 0) InitDefaultRequirement();

            //展开或关闭动画
            float target = (Active && !closing) ? 1f : 0f;
            showProgress = MathHelper.Lerp(showProgress, target, 0.12f);
            if (Math.Abs(showProgress - target) < 0.01f) showProgress = target;

            if (closing) {
                hideProgress += 0.08f;
                if (hideProgress > 1f) hideProgress = 1f;
            }

            if (showProgress <= 0f && !closing) return;

            if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var hp)) return;

            //收起插值
            float collapseTarget = collapsed ? 1f : 0f;
            collapseProgress = MathHelper.Lerp(collapseProgress, collapseTarget, 0.18f);
            if (Math.Abs(collapseProgress - collapseTarget) < 0.01f) collapseProgress = collapseTarget;

            //内容淡入
            if (!closing) {
                if (showProgress > 0.25f && contentFade < 1f) contentFade = Math.Min(1f, contentFade + 0.08f);
                else if (showProgress <= 0.25f && contentFade > 0f) contentFade = Math.Max(0f, contentFade - 0.15f);
            }
            else contentFade = 1f;

            pulseTimer += 0.03f;

            //包装描述获取高度(每帧更新便于语言切换)
            WrapDescriptionIfNeed();
            descUsedHeight = 0f;
            foreach (var line in wrappedDescLines) {
                descUsedHeight += FontAssets.MouseText.Value.MeasureString("A").Y * DescScale + 2f;
            }
            if (wrappedDescLines.Length > 0) descUsedHeight += -2f; //最后一行去掉多余间隙

            //根据描述动态计算面板高度(不超过基准高度)
            int dynamicContentBottom = Padding + 26 + (int)descUsedHeight + 8 + SlotSize + 60; //标题+描述+间距+槽位+底部按钮与提示空间
            int targetHeight = Math.Clamp(dynamicContentBottom, HeaderHeight, BasePanelHeight);
            //应用收起动画(线性): collapseProgress 1 时只保留 HeaderHeight
            currentPanelHeight = (int)MathHelper.Lerp(targetHeight, HeaderHeight, collapseProgress);

            Size = new Vector2(BasePanelWidth, currentPanelHeight);
            if (!dragging && DrawPosition == default) {
                //初始位置保持右侧不改变
                DrawPosition = new Vector2(Main.screenWidth - BasePanelWidth - 14, Main.screenHeight * 0.34f);
            }
            UIHitBox = DrawPosition.GetRectangle(Size);
            headerRect = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, (int)Size.X, (int)Size.Y);
            collapseButtonRect = new Rectangle(headerRect.Right - 22, headerRect.Y + 10, 14, 14);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);
            if (hoverInMainPage) player.mouseInterface = true;

            //拖拽逻辑: 在标题栏内按住左键开始拖拽
            if (headerRect.Contains(MouseHitBox)) {
                if (keyLeftPressState == KeyPressState.Pressed) {
                    dragging = true; 
                    dragOffset = DrawPosition - MousePosition; 
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.1f });
                }
            }
            if (dragging) {
                DrawPosition = MousePosition + dragOffset;
                UIHitBox = DrawPosition.GetRectangle(Size);
                headerRect = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, (int)Size.X, HeaderHeight);
                collapseButtonRect = new Rectangle(headerRect.Right - 22, headerRect.Y + 10, 14, 14);
                if (keyLeftPressState == KeyPressState.Released) dragging = false;
            }

            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, 0, Main.screenWidth - Size.X);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 0, Main.screenHeight - Size.Y);

            //收起按钮点击
            if (collapseButtonRect.Contains(MouseHitBox) && keyLeftPressState == KeyPressState.Pressed) {
                collapsed = !collapsed; SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = collapsed ? -0.3f : 0.3f });
            }

            bool fullyCollapsed = collapseProgress >= 0.99f;

            //按钮布局(仅展开时才显示内容按钮)
            int buttonHeight = 22; int buttonWidth = 74;
            quickPutButtonRect = new Rectangle((int)(DrawPosition.X + Padding), (int)(DrawPosition.Y + currentPanelHeight - Padding - buttonHeight), buttonWidth, buttonHeight);
            declineButtonRect = new Rectangle((int)(DrawPosition.X + BasePanelWidth - Padding - buttonWidth), (int)(DrawPosition.Y + currentPanelHeight - Padding - buttonHeight), buttonWidth, buttonHeight);
            hoverQuickPut = !fullyCollapsed && quickPutButtonRect.Contains(MouseHitBox);
            hoverDecline = !fullyCollapsed && declineButtonRect.Contains(MouseHitBox);

            //槽位区域(根据描述高度调整位置)
            Vector2 slotsStart = DrawPosition + new Vector2(Padding, Padding + 26 + descUsedHeight + 8);
            if (!fullyCollapsed) {
                for (int i = 0; i < RequiredItems.Count; i++) {
                    var slot = RequiredItems[i];
                    slot.Update(slotsStart + new Vector2(i * (SlotSize + SlotSpacing), 0), SlotSize, player, hoverInMainPage && !fullyCollapsed);
                }
            }

            //快速放入逻辑
            if (!hp.ADCSave.FishoilQuestCompleted && !closing && !fullyCollapsed && hoverQuickPut && keyLeftPressState == KeyPressState.Pressed) {
                SoundEngine.PlaySound(SoundID.Grab);
                QuickDepositInventoryFish();
            }

            //拒绝任务
            if (!hp.ADCSave.FishoilQuestCompleted && !fullyCollapsed && hoverDecline && keyLeftPressState == KeyPressState.Pressed) {
                hp.ADCSave.FishoilQuestDeclined = true;
                SoundEngine.PlaySound(SoundID.MenuClose);
            }

            //完成检测
            if (!hp.ADCSave.FishoilQuestCompleted) {
                bool allDone = true;
                foreach (var s in RequiredItems) if (!s.IsSatisfied) { allDone = false; break; }
                if (allDone) {
                    hp.ADCSave.FishoilQuestCompleted = true;
                    GiveReward();
                }
            }
        }

        private void QuickDepositInventoryFish() {
            Player p = Main.LocalPlayer;
            foreach (var slot in RequiredItems) {
                if (slot.IsSatisfied) continue;
                //先处理鼠标物品
                if (Main.mouseItem.type == slot.ItemType && Main.mouseItem.stack > 0) {
                    int need = slot.Need - slot.Current;
                    int move = Math.Min(need, Main.mouseItem.stack);
                    slot.Current += move;
                    Main.mouseItem.stack -= move;
                    if (Main.mouseItem.stack <= 0) Main.mouseItem.TurnToAir();
                }
                //再处理背包
                for (int i = 0; i < p.inventory.Length && !slot.IsSatisfied; i++) {
                    Item inv = p.inventory[i];
                    if (inv.type != slot.ItemType || inv.stack <= 0) continue;
                    int need = slot.Need - slot.Current;
                    int move = Math.Min(need, inv.stack);
                    slot.Current += move;
                    inv.stack -= move;
                    if (inv.stack <= 0) inv.TurnToAir();
                }
            }
        }

        private void GiveReward() {
            if (rewarded) return;
            rewarded = true;
            SoundEngine.PlaySound(SoundID.Research);
            Item reward = new Item(ModContent.ItemType<Fishoil>()); reward.stack = 1;
            Main.LocalPlayer.QuickSpawnItem(Main.LocalPlayer.GetSource_Misc("FishoilQuestReward"), reward, reward.stack);
            closing = true; //启动关闭动画
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (showProgress <= 0.01f && !closing) return;
            //展开/关闭复合插值
            float openAlpha = closing ? (1f - hideProgress) : showProgress;
            float alpha = Math.Min(openAlpha * 1.6f, 1f);
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle panelRect = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, BasePanelWidth, currentPanelHeight);
            float closeOffsetY = closing ? hideProgress * 40f : 0f;
            panelRect.Y += (int)closeOffsetY;

            Rectangle shadow = panelRect; shadow.Offset(4, 5);
            spriteBatch.Draw(px, shadow, new Rectangle(0,0,1,1), Color.Black * (alpha * 0.55f));

            int segs = 16;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs; float t2 = (i+1)/(float)segs;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new Rectangle(panelRect.X, y1, panelRect.Width, Math.Max(1,y2 - y1));
                Color deep = new Color(4,18,30);
                Color mid = new Color(10,42,60);
                Color edge = new Color(20,90,120);
                float osc = (float)System.Math.Sin(pulseTimer * 1.2f + t * 3f) * 0.5f + 0.5f;
                Color c = Color.Lerp(Color.Lerp(deep, mid, osc), edge, t * 0.55f);
                spriteBatch.Draw(px, r, new Rectangle(0,0,1,1), c * alpha);
            }
            float pulse = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.5f + 0.5f;
            spriteBatch.Draw(px, panelRect, new Rectangle(0,0,1,1), new Color(30,120,150) * (alpha * 0.08f * pulse));
            DrawFrameOcean(spriteBatch, panelRect, alpha, pulse);

            if (contentFade <= 0.01f && !closing) return;
            float ca = contentFade;

            //标题区
            Vector2 titlePos = new Vector2(panelRect.X + Padding, panelRect.Y + Padding);
            string title = TitleText.Value;
            Color glow = new Color(140,230,255) * (ca * 0.7f);
            for(int i=0;i<4;i++){ float ang = MathHelper.TwoPi * i / 4f; Vector2 off = ang.ToRotationVector2() * 1.3f; Utils.DrawBorderString(spriteBatch, title, titlePos + off, glow * 0.55f, 0.9f); }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * ca, 0.9f);

            //收起按钮绘制(简单箭头)
            char arrow = collapseProgress < 0.5f ? '\u25B2' : '\u25BC'; //▲/▼
            Color arrowColor = Color.Lerp(new Color(120,200,235), Color.White, 0.5f) * ca;
            Utils.DrawBorderString(spriteBatch, arrow.ToString(), new Vector2(collapseButtonRect.X, collapseButtonRect.Y), arrowColor, 0.6f);

            bool fullyCollapsed = collapseProgress >= 0.99f;
            if (!fullyCollapsed) {
                //描述换行
                WrapDescriptionIfNeed();
                float lineY = titlePos.Y + 26f;
                foreach (var line in wrappedDescLines) {
                    Utils.DrawBorderString(spriteBatch, line, new Vector2(titlePos.X, lineY), new Color(180,230,250) * ca, DescScale);
                    lineY += FontAssets.MouseText.Value.MeasureString("A").Y * DescScale + 2f;
                    if (lineY > panelRect.Bottom - 70) break;
                }
                //分隔线
                Vector2 divStart = new Vector2(titlePos.X, titlePos.Y + 26 + descUsedHeight);
                Vector2 divEnd = divStart + new Vector2(BasePanelWidth - Padding * 2, 0);
                DrawGradientLine(spriteBatch, divStart, divEnd, new Color(70,180,230)*(ca * 0.85f), new Color(70,180,230)*(ca * 0.05f), 1.2f);

                //槽位绘制
                Vector2 slotsStart = new Vector2(panelRect.X + Padding, divStart.Y + 8);
                foreach(var slot in RequiredItems) slot.Draw(spriteBatch, ca);

                //底部提示或完成
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var hp)) {
                    if (!hp.ADCSave.FishoilQuestCompleted) {
                        Utils.DrawBorderString(spriteBatch, SubmitHint.Value, new Vector2(panelRect.X + Padding, panelRect.Bottom - 54), new Color(120,200,235)*(ca*0.7f), 0.6f);
                    }
                    else {
                        float donePulse = (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 5f) * 0.5f + 0.5f;
                        Utils.DrawBorderString(spriteBatch, CompletedText.Value, new Vector2(panelRect.X + Padding, panelRect.Bottom - 54), Color.Lerp(new Color(150,230,255), new Color(200,240,255), donePulse) * ca, 0.7f);
                    }
                }

                //按钮
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var hp2) && !hp2.ADCSave.FishoilQuestCompleted && !closing) {
                    DrawButton(spriteBatch, quickPutButtonRect, QuickPutText.Value, hoverQuickPut, ca, new Color(30,90,120));
                    DrawButton(spriteBatch, declineButtonRect, DeclineText.Value, hoverDecline, ca, new Color(100,40,40));
                }
            }
        }

        private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, string text, bool hover, float alpha, Color baseColor) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color bg = baseColor * (alpha * (hover ? 0.95f : 0.75f));
            spriteBatch.Draw(px, rect, new Rectangle(0,0,1,1), bg);
            Color edge = Color.Lerp(baseColor, Color.White, 0.35f) * alpha;
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0,0,1,1), edge);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0,0,1,1), edge * 0.6f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0,0,1,1), edge * 0.8f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0,0,1,1), edge * 0.8f);
            Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text) * 0.7f;
            Vector2 textPos = new Vector2(rect.X + rect.Width / 2f - textSize.X / 2f, rect.Y + rect.Height / 2f - textSize.Y / 2f);
            Utils.DrawBorderString(spriteBatch, text, textPos, Color.White * alpha, 0.7f);
        }

        private void WrapDescriptionIfNeed() {
            string raw = DescText.Value;
            float maxWidth = BasePanelWidth - Padding * 2f;
            List<string> lines = new List<string>();
            string current = string.Empty;
            foreach (char ch in raw) {
                string test = current + ch;
                float w = FontAssets.MouseText.Value.MeasureString(test).X * DescScale;
                if (w > maxWidth && current.Length > 0) {
                    lines.Add(current);
                    current = ch.ToString();
                } else current = test;
            }
            if (current.Length > 0) lines.Add(current);
            wrappedDescLines = lines.ToArray();
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
            float rotation = (float)System.Math.Atan2(edge.Y, edge.X); int segments = System.Math.Max(1, (int)(length / 11f));
            for(int i=0;i<segments;i++){ float t = i/(float)segments; Vector2 segPos = start + edge * (length * t); float segLength = length / segments; Color c = Color.Lerp(startColor, endColor, t); spriteBatch.Draw(pixel, segPos, new Rectangle(0,0,1,1), c, rotation, new Vector2(0,0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0); }
        }

        private class QuestMaterialSlot {
            public int ItemType; public int Need; public int Current; public Vector2 Pos; public Rectangle Hit; public bool Hover; public bool IsSatisfied => Current >= Need;
            public QuestMaterialSlot(int itemType, int need) { ItemType = itemType; Need = need; }
            public void ResetCount() { Current = 0; }
            public void Update(Vector2 drawPos, int size, Player player, bool uiHover) {
                Pos = drawPos; Hit = new Rectangle((int)Pos.X, (int)Pos.Y, size, size); Hover = uiHover && Hit.Contains(Main.mouseX, Main.mouseY);
                if (Hover) player.mouseInterface = true;
                if (Hover) {
                    //左键放入一份, Shift左键放入尽量多, Ctrl左键放入背包所有该物品
                    bool leftClick = Main.mouseLeft && Main.mouseLeftRelease;
                    if (leftClick && Main.mouseItem.type == ItemType && !IsSatisfied) {
                        int add = 1;
                        if (Main.keyState.PressingShift()) add = System.Math.Min(Main.mouseItem.stack, Need - Current);
                        if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl)) add = Need - Current;
                        add = System.Math.Min(add, Main.mouseItem.stack);
                        Current += add; Main.mouseItem.stack -= add; if (Main.mouseItem.stack <= 0) Main.mouseItem.TurnToAir(); SoundEngine.PlaySound(SoundID.Grab);
                    }
                    //右键取出一份
                    bool rightClick = Main.mouseRight && Main.mouseRightRelease;
                    if (rightClick && Current > 0 && Main.mouseItem.IsAir) {
                        Current -= 1; Item give = new Item(ItemType); give.stack = 1; Main.mouseItem = give; SoundEngine.PlaySound(SoundID.Grab);
                    }
                }
            }
            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D px = VaultAsset.placeholder2.Value; Rectangle r = new Rectangle((int)Pos.X, (int)Pos.Y, Hit.Width, Hit.Height); Color back = new Color(6,32,48) * (alpha * 0.9f); if (Hover) back *= 1.15f; sb.Draw(px, r, new Rectangle(0,0,1,1), back);
                Color edge = IsSatisfied ? new Color(70,180,230) : new Color(40,100,140); sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 2), new Rectangle(0,0,1,1), edge); sb.Draw(px, new Rectangle(r.X, r.Bottom -2, r.Width, 2), new Rectangle(0,0,1,1), edge * 0.7f); sb.Draw(px, new Rectangle(r.X, r.Y, 2, r.Height), new Rectangle(0,0,1,1), edge * 0.85f); sb.Draw(px, new Rectangle(r.Right -2, r.Y, 2, r.Height), new Rectangle(0,0,1,1), edge * 0.85f);
                if (ItemType > ItemID.None) { Main.instance.LoadItem(ItemType); Texture2D tex = TextureAssets.Item[ItemType].Value; Rectangle frame = tex.Frame(); Vector2 center = new Vector2(r.X + r.Width/2f, r.Y + r.Height/2f); float scale = System.Math.Min((r.Width - 8f)/frame.Width, (r.Height - 8f)/frame.Height); Color itemColor = IsSatisfied ? Color.White : Color.White * 0.9f; sb.Draw(tex, center, frame, itemColor * alpha, 0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f); }
                string text = $"{Current}/{Need}"; Utils.DrawBorderString(sb, text, new Vector2(r.X + 4, r.Bottom - 16), IsSatisfied ? Color.Cyan * alpha : Color.White * alpha, 0.6f);
            }
        }
    }
}
