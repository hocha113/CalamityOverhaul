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
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV.Scenarios.Helen.Quest.FishoilQuest
{
    /// <summary>
    /// 提供普通鱼以换取一瓶鱼油的任务窗口
    /// </summary>
    internal class FishoilQuestUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";
        public static FishoilQuestUI Instance => UIHandleLoader.GetUIHandleOfType<FishoilQuestUI>();

        //本地化
        public static LocalizedText TitleText { get; private set; }
        public static LocalizedText DescText { get; private set; }
        public static LocalizedText SubmitHint { get; private set; }
        public static LocalizedText DeclineText { get; private set; }
        public static LocalizedText CompletedText { get; private set; }
        public static LocalizedText QuickPutText { get; private set; }

        //需求槽位
        private readonly List<QuestMaterialSlot> RequiredItems = new();

        //面板状态
        private float showProgress;//展开进度(0-1)
        private float contentFade;//内容淡入(0-1)
        private float pulseTimer;//背景脉冲计时
        private bool rewarded;//是否已发放奖励
        private bool closing;//完成后关闭动画中
        private float hideProgress;//关闭进度(0-1)
        private bool collapsed;//是否收起
        private float collapseProgress;//收起进度(0-1)
        private bool dragging;//拖拽中
        private Vector2 dragOffset;//拖拽偏移

        //交互标记
        private bool hoverDecline;
        private bool hoverQuickPut;

        //布局常量
        private const int BasePanelWidth = 240;
        private const int BasePanelHeight = 190;
        private const int HeaderHeight = 36;
        private const int Padding = 10;
        private const int SlotSize = 36;
        private const int SlotSpacing = 8;

        //动态尺寸
        private int currentPanelHeight;
        private float descUsedHeight;

        //矩形区域
        private Rectangle headerRect;
        private Rectangle collapseButtonRect;
        private Rectangle declineButtonRect;
        private Rectangle quickPutButtonRect;

        //换行缓存
        private string[] wrappedDescLines;
        private const float DescScale = 0.65f;

        //内部元素动画辅助
        private float elementAlpha; //内部元素整体Alpha
        private float elementYOffset; //内部元素整体Y偏移

        public override bool Active {
            get {
                if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var hp)) {
                    return false;
                }
                //已拒绝直接不显示
                if (hp.ADVSave.FishoilQuestDeclined) {
                    return false;
                }
                //未接受不显示
                if (!hp.ADVSave.FishoilQuestAccepted) {
                    return false;
                }
                //已完成后直接隐藏
                if (hp.ADVSave.FishoilQuestCompleted) {
                    closing = true;//强行设置开始关闭
                    return hideProgress < 1f;
                }
                return true;
            }
        }

        public override void LoadUIData(TagCompound tag) {
            hideProgress = 1f;//设置一下隐藏，防止进世界时会有一瞬间看到UI
        }

        public override void SaveUIData(TagCompound tag) {
            hideProgress = 1f;//设置一下隐藏，防止进世界时会有一瞬间看到UI
        }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "采集:鲈鱼");
            DescText = this.GetLocalization(nameof(DescText), () => "放入足够数量的鲈鱼,我会立刻提炼一瓶鱼油返还给你,过程很快,不需要你等待");
            SubmitHint = this.GetLocalization(nameof(SubmitHint), () => "左键放入/右键取出,Shift批量,Ctrl满仓");
            DeclineText = this.GetLocalization(nameof(DeclineText), () => "拒绝");
            CompletedText = this.GetLocalization(nameof(CompletedText), () => "完成");
            QuickPutText = this.GetLocalization(nameof(QuickPutText), () => "快速放入");
        }

        public void OpenPersistent() {
            //已经完成或已拒绝不再打开，防止多人模式或重新加载后无意义显示
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var hp) && (hp.ADVSave.FishoilQuestCompleted || hp.ADVSave.FishoilQuestDeclined)) {
                return;
            }
            if (RequiredItems.Count == 0) {
                InitDefaultRequirement();
            }
            ResetVisualState();
            if (showProgress <= 0f) {
                showProgress = 0.01f;
            }
        }

        private void ResetVisualState() {
            contentFade = 0f;
            hideProgress = 0f;
            closing = false;
            rewarded = false;
            collapsed = false;
            collapseProgress = 0f;
            elementAlpha = 1f;
            elementYOffset = 0f;
            foreach (var slot in RequiredItems) {
                slot.ResetCount();
            }
        }

        private void InitDefaultRequirement() {
            RequiredItems.Clear();
            RequiredItems.Add(new QuestMaterialSlot(ItemID.Bass, 300));
        }

        public override void Update() {
            if (RequiredItems.Count == 0) {
                InitDefaultRequirement();
            }

            //展开或关闭动画
            float targetShow = Active && !closing ? 1f : 0f;
            showProgress = MathHelper.Lerp(showProgress, targetShow, 0.12f);
            if (Math.Abs(showProgress - targetShow) < 0.01f) {
                showProgress = targetShow;
            }

            if (closing) {
                hideProgress += 0.08f;
                if (hideProgress > 1f) {
                    hideProgress = 1f;
                }
            }
            //Active 已在完成后直接返回 false；但关闭动画期间 Active 也为 false，showProgress 会回收，这里保持逻辑不变
            if (showProgress <= 0f && !closing) {
                return;
            }
            if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var hp)) {
                return;
            }

            //收起插值
            float targetCollapse = collapsed ? 1f : 0f;
            collapseProgress = MathHelper.Lerp(collapseProgress, targetCollapse, 0.18f);
            if (Math.Abs(collapseProgress - targetCollapse) < 0.01f) {
                collapseProgress = targetCollapse;
            }

            //元素透明度与偏移(使用缓动让过渡更自然)
            elementAlpha = 1f - CWRUtils.EaseInQuad(collapseProgress);
            elementYOffset = CWRUtils.EaseOutQuad(collapseProgress) * 30f; //内容整体上移

            if (!closing) {
                if (showProgress > 0.25f && contentFade < 1f) {
                    contentFade = Math.Min(1f, contentFade + 0.08f);
                }
                else if (showProgress <= 0.25f && contentFade > 0f) {
                    contentFade = Math.Max(0f, contentFade - 0.15f);
                }
            }
            else {
                contentFade = 1f;
            }

            pulseTimer += 0.03f;

            //包装描述获取高度(每帧更新便于语言切换)
            WrapDescriptionIfNeed();
            descUsedHeight = 0f;
            foreach (var line in wrappedDescLines) {
                descUsedHeight += FontAssets.MouseText.Value.MeasureString("A").Y * DescScale + 2f;
            }
            if (wrappedDescLines.Length > 0) {
                descUsedHeight -= 2f; //最后一行去掉多余间隙
            }

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
            headerRect = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, (int)Size.X, HeaderHeight);
            collapseButtonRect = new Rectangle(headerRect.Right - 22, headerRect.Y + 10, 14, 14);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);
            if (hoverInMainPage) {
                player.mouseInterface = true;
            }

            //拖拽逻辑: 在标题栏内按住左键开始拖拽
            if (UIHitBox.Contains(MouseHitBox)) {
                if (keyLeftPressState == KeyPressState.Pressed) {
                    dragging = true;
                    dragOffset = DrawPosition - MousePosition;
                    SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.1f });
                }
            }
            if (dragging) {
                DrawPosition = MousePosition + dragOffset;
                DrawPosition.X = MathHelper.Clamp(DrawPosition.X, 0, Main.screenWidth - Size.X);
                DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 0, Main.screenHeight - HeaderHeight); //限制在屏幕内(收起时也安全)
                UIHitBox = DrawPosition.GetRectangle(Size);
                headerRect = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, (int)Size.X, HeaderHeight);
                collapseButtonRect = new Rectangle(headerRect.Right - 22, headerRect.Y + 10, 14, 14);
                if (keyLeftPressState == KeyPressState.Released) {
                    dragging = false;
                }
            }

            if (collapseButtonRect.Contains(MouseHitBox) && keyLeftPressState == KeyPressState.Pressed) {
                collapsed = !collapsed;
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = collapsed ? -0.3f : 0.3f });
            }
            bool interactive = collapseProgress < 0.95f;

            //按钮区域布局
            int buttonHeight = 22;
            int buttonWidth = 74;
            quickPutButtonRect = new Rectangle((int)(DrawPosition.X + Padding), (int)(DrawPosition.Y + currentPanelHeight - Padding - buttonHeight), buttonWidth, buttonHeight);
            declineButtonRect = new Rectangle((int)(DrawPosition.X + BasePanelWidth - Padding - buttonWidth), (int)(DrawPosition.Y + currentPanelHeight - Padding - buttonHeight), buttonWidth, buttonHeight);
            hoverQuickPut = interactive && quickPutButtonRect.Contains(MouseHitBox);
            hoverDecline = interactive && declineButtonRect.Contains(MouseHitBox);

            //槽位区域(根据描述高度调整位置)
            Vector2 slotsStart = DrawPosition + new Vector2(Padding, Padding + 26 + descUsedHeight + 8 + elementYOffset);
            if (interactive) {
                for (int i = 0; i < RequiredItems.Count; i++) {
                    var slot = RequiredItems[i];
                    slot.Update(slotsStart + new Vector2(i * (SlotSize + SlotSpacing), 0), SlotSize, player, hoverInMainPage && interactive);
                }
            }

            //快速放入逻辑
            if (!hp.ADVSave.FishoilQuestCompleted && !closing && interactive && hoverQuickPut && keyLeftPressState == KeyPressState.Pressed) {
                SoundEngine.PlaySound(SoundID.Grab);
                QuickDepositInventoryFish();
            }

            //拒绝任务
            if (!hp.ADVSave.FishoilQuestCompleted && interactive && hoverDecline && keyLeftPressState == KeyPressState.Pressed) {
                hp.ADVSave.FishoilQuestDeclined = true;
                SoundEngine.PlaySound(SoundID.MenuClose);
            }

            //完成检测
            if (!hp.ADVSave.FishoilQuestCompleted) {
                bool allDone = true;
                foreach (var s in RequiredItems) {
                    if (!s.IsSatisfied) {
                        allDone = false;
                        break;
                    }
                }
                if (allDone) {
                    hp.ADVSave.FishoilQuestCompleted = true;
                    GiveReward();
                }
            }
        }

        private void QuickDepositInventoryFish() {
            Player p = Main.LocalPlayer;
            foreach (var slot in RequiredItems) {
                if (slot.IsSatisfied) {
                    continue;
                }
                //先处理鼠标物品
                if (Main.mouseItem.type == slot.ItemType && Main.mouseItem.stack > 0) {
                    int need = slot.Need - slot.Current;
                    int move = Math.Min(need, Main.mouseItem.stack);
                    slot.Current += move;
                    Main.mouseItem.stack -= move;
                    if (Main.mouseItem.stack <= 0) {
                        Main.mouseItem.TurnToAir();
                    }
                }
                //再处理背包
                for (int i = 0; i < p.inventory.Length && !slot.IsSatisfied; i++) {
                    Item inv = p.inventory[i];
                    if (inv.type != slot.ItemType || inv.stack <= 0) {
                        continue;
                    }
                    int need = slot.Need - slot.Current;
                    int move = Math.Min(need, inv.stack);
                    slot.Current += move;
                    inv.stack -= move;
                    if (inv.stack <= 0) {
                        inv.TurnToAir();
                    }
                }
            }
        }

        private void GiveReward() {
            if (rewarded) {
                return;
            }
            rewarded = true;
            SoundEngine.PlaySound(SoundID.Research);
            Item reward = new Item(ModContent.ItemType<Fishoil>());
            reward.stack = 5; //奖励数量
            Main.LocalPlayer.QuickSpawnItem(Main.LocalPlayer.GetSource_Misc("FishoilQuestReward"), reward, reward.stack);
            closing = true;
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (showProgress <= 0.01f && !closing) {
                return;
            }
            //展开/关闭复合插值
            float openAlpha = closing ? 1f - hideProgress : showProgress;
            float alpha = Math.Min(openAlpha * 1.6f, 1f);
            Texture2D px = VaultAsset.placeholder2.Value;
            Rectangle panelRect = new Rectangle((int)DrawPosition.X, (int)DrawPosition.Y, BasePanelWidth, currentPanelHeight);
            if (closing) {
                panelRect.Y += (int)(hideProgress * 40f);
            }
            Rectangle shadow = panelRect;
            shadow.Offset(4, 5);
            spriteBatch.Draw(px, shadow, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.55f));

            int segs = 16;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new Rectangle(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));
                Color deep = new Color(4, 18, 30);
                Color mid = new Color(10, 42, 60);
                Color edge = new Color(20, 90, 120);
                float osc = (float)Math.Sin(pulseTimer * 1.2f + t * 3f) * 0.5f + 0.5f;
                Color c = Color.Lerp(Color.Lerp(deep, mid, osc), edge, t * 0.55f);
                spriteBatch.Draw(px, r, new Rectangle(0, 0, 1, 1), c * alpha);
            }
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.5f + 0.5f;
            spriteBatch.Draw(px, panelRect, new Rectangle(0, 0, 1, 1), new Color(30, 120, 150) * (alpha * 0.08f * pulse));
            DrawFrameOcean(spriteBatch, panelRect, alpha, pulse);

            if (contentFade <= 0.01f && !closing) {
                return;
            }
            float ca = contentFade;

            DrawTitleAndCollapse(spriteBatch, panelRect, ca);
            DrawContent(spriteBatch, panelRect, ca);
        }

        private void DrawTitleAndCollapse(SpriteBatch sb, Rectangle panelRect, float ca) {
            Vector2 titlePos = new Vector2(panelRect.X + Padding, panelRect.Y + Padding);
            string title = TitleText.Value;
            Color glow = new Color(140, 230, 255) * (ca * 0.7f);
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f;
                Vector2 off = ang.ToRotationVector2() * 1.3f;
                Utils.DrawBorderString(sb, title, titlePos + off, glow * 0.55f, 0.9f);
            }
            Utils.DrawBorderString(sb, title, titlePos, Color.White * ca, 0.9f);

            char arrow = collapseProgress < 0.5f ? '\u25B2' : '\u25BC';
            float arrowScale = 0.6f + 0.2f * (1f - collapseProgress);
            Color arrowColor = Color.Lerp(new Color(120, 200, 235), new Color(200, 240, 255), 0.5f) * ca;
            Utils.DrawBorderString(sb, arrow.ToString(), new Vector2(collapseButtonRect.X, collapseButtonRect.Y), arrowColor * (0.9f - collapseProgress * 0.4f), arrowScale);
        }

        private void DrawContent(SpriteBatch sb, Rectangle panelRect, float ca) {
            if (collapseProgress >= 0.99f) {
                return;
            }
            float slotEase = CWRUtils.EaseOutQuad(1f - collapseProgress); //槽位单独缓动

            //描述
            float lineY = panelRect.Y + Padding + 26f + elementYOffset;
            foreach (var line in wrappedDescLines) {
                Utils.DrawBorderString(sb, line, new Vector2(panelRect.X + Padding, lineY), new Color(180, 230, 250) * ca * elementAlpha, DescScale);
                lineY += FontAssets.MouseText.Value.MeasureString("A").Y * DescScale + 2f;
                if (lineY > panelRect.Bottom - 70) {
                    break;
                }
            }

            //分隔线
            Vector2 divStart = new Vector2(panelRect.X + Padding, panelRect.Y + Padding + 26 + descUsedHeight + elementYOffset);
            Vector2 divEnd = divStart + new Vector2(BasePanelWidth - Padding * 2, 0);
            DrawGradientLine(sb, divStart, divEnd, new Color(70, 180, 230) * (ca * 0.85f * elementAlpha), new Color(70, 180, 230) * (ca * 0.05f * elementAlpha), 1.2f);

            //槽位绘制
            Vector2 slotsStart = new Vector2(panelRect.X + Padding, divStart.Y + 8);
            for (int i = 0; i < RequiredItems.Count; i++) {
                QuestMaterialSlot slot = RequiredItems[i];
                float slotScale = MathHelper.Lerp(0.6f, 1f, slotEase);
                float slotAlpha = MathHelper.Lerp(0f, 1f, slotEase) * elementAlpha;
                float slotYOffset = (1f - slotEase) * 20f;
                if (slotAlpha < 0.75f) {
                    continue;
                }
                slot.DrawCustom(sb, slotsStart + new Vector2(i * (SlotSize + SlotSpacing), slotYOffset), SlotSize, slotScale, slotAlpha);
            }

            //底部提示/完成
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var hp)) {
                Vector2 tipPos = new Vector2(panelRect.X + Padding, panelRect.Bottom - 54);
                if (!hp.ADVSave.FishoilQuestCompleted) {
                    Utils.DrawBorderString(sb, SubmitHint.Value, tipPos, new Color(120, 200, 235) * (ca * 0.7f * elementAlpha), 0.6f);
                }
                else {
                    float donePulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f) * 0.5f + 0.5f;
                    Utils.DrawBorderString(sb, CompletedText.Value, tipPos, Color.Lerp(new Color(150, 230, 255), new Color(200, 240, 255), donePulse) * ca * elementAlpha, 0.7f);
                }
            }

            //按钮
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var hp2) && !hp2.ADVSave.FishoilQuestCompleted && !closing) {
                DrawButton(sb, quickPutButtonRect, QuickPutText.Value, hoverQuickPut, ca * elementAlpha, new Color(30, 90, 120));
                DrawButton(sb, declineButtonRect, DeclineText.Value, hoverDecline, ca * elementAlpha, new Color(100, 40, 40));
            }
        }

        private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, string text, bool hover, float alpha, Color baseColor) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Color bg = baseColor * (alpha * (hover ? 0.95f : 0.75f));
            spriteBatch.Draw(px, rect, new Rectangle(0, 0, 1, 1), bg);
            Color edge = Color.Lerp(baseColor, Color.White, 0.35f) * alpha;
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.6f);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.8f);
            spriteBatch.Draw(px, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.8f);
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
                }
                else {
                    current = test;
                }
            }
            if (current.Length > 0) {
                lines.Add(current);
            }
            wrappedDescLines = [.. lines];
        }

        private static void DrawFrameOcean(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D vaule = VaultAsset.placeholder2.Value;
            Color edge = Color.Lerp(new Color(30, 140, 190), new Color(90, 210, 255), pulse) * (alpha * 0.85f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
            sb.Draw(vaule, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            sb.Draw(vaule, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
            Rectangle inner = rect;
            inner.Inflate(-5, -5);
            Color innerC = new Color(120, 220, 255) * (alpha * 0.18f * pulse);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerC * 0.65f);
            sb.Draw(vaule, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.85f);
            sb.Draw(vaule, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerC * 0.85f);
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }
            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 11f));
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color c = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), c, rotation
                    , new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }

        private class QuestMaterialSlot(int itemType, int need)
        {
            public int ItemType = itemType;
            public int Need = need;
            public int Current;
            public Vector2 Pos;
            public Rectangle Hit;
            public bool Hover;
            public bool IsSatisfied => Current >= Need;

            public void ResetCount() => Current = 0;

            public void Update(Vector2 drawPos, int size, Player player, bool uiHover) {
                Pos = drawPos;
                Hit = new Rectangle((int)Pos.X, (int)Pos.Y, size, size);
                Hover = uiHover && Hit.Contains(Main.mouseX, Main.mouseY);
                if (Hover) {
                    player.mouseInterface = true;
                }
                if (Hover) {
                    bool leftClick = Main.mouseLeft && Main.mouseLeftRelease;
                    if (leftClick && Main.mouseItem.type == ItemType && !IsSatisfied) {
                        int add = 1;
                        if (Main.keyState.PressingShift()) {
                            add = Math.Min(Main.mouseItem.stack, Need - Current);
                        }
                        if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl)) {
                            add = Need - Current;
                        }
                        add = Math.Min(add, Main.mouseItem.stack);
                        Current += add;
                        Main.mouseItem.stack -= add;
                        if (Main.mouseItem.stack <= 0) {
                            Main.mouseItem.TurnToAir();
                        }
                        SoundEngine.PlaySound(SoundID.Grab);
                    }
                    bool rightClick = Main.mouseRight && Main.mouseRightRelease;
                    if (rightClick && Current > 0 && Main.mouseItem.IsAir) {
                        Current -= 1;
                        Item give = new Item(ItemType);
                        give.stack = 1;
                        Main.mouseItem = give;
                        SoundEngine.PlaySound(SoundID.Grab);
                    }
                }
            }

            public void DrawCustom(SpriteBatch sb, Vector2 drawPos, int size, float scale, float alpha) {
                Texture2D vaule = VaultAsset.placeholder2.Value;
                Rectangle r = new Rectangle((int)drawPos.X, (int)drawPos.Y, size, size);
                Color back = new Color(6, 32, 48) * (alpha * 0.9f);
                if (Hover) {
                    back *= 1.15f;
                }
                sb.Draw(vaule, r, new Rectangle(0, 0, 1, 1), back);
                Color edge = IsSatisfied ? new Color(70, 180, 230) : new Color(40, 100, 140);
                sb.Draw(vaule, new Rectangle(r.X, r.Y, r.Width, 2), new Rectangle(0, 0, 1, 1), edge);
                sb.Draw(vaule, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), new Rectangle(0, 0, 1, 1), edge * 0.7f);
                sb.Draw(vaule, new Rectangle(r.X, r.Y, 2, r.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
                sb.Draw(vaule, new Rectangle(r.Right - 2, r.Y, 2, r.Height), new Rectangle(0, 0, 1, 1), edge * 0.85f);
                if (ItemType > ItemID.None) {
                    Main.instance.LoadItem(ItemType);
                    Texture2D tex = TextureAssets.Item[ItemType].Value;
                    Rectangle frame = tex.Frame();
                    Vector2 center = new Vector2(r.X + r.Width / 2f, r.Y + r.Height / 2f);
                    float fitScale = Math.Min((r.Width - 8f) / frame.Width, (r.Height - 8f) / frame.Height) * scale;
                    Color itemColor = IsSatisfied ? Color.White : Color.White * 0.9f;
                    sb.Draw(tex, center, frame, itemColor * alpha, 0f, frame.Size() / 2f, fitScale, SpriteEffects.None, 0f);
                }
                string text = $"{Current}/{Need}";
                Utils.DrawBorderString(sb, text, new Vector2(r.X + 4, r.Bottom - 16), IsSatisfied ? Color.Cyan * alpha : Color.White * alpha, 0.6f);
            }
        }
    }
}
