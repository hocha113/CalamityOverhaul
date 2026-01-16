using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI
{
    /// <summary>
    /// 技能库界面，用于存放暂时不需要在主列表中显示的技能
    /// </summary>
    internal class SkillLibraryUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.HalibutText";
        public static SkillLibraryUI Instance => UIHandleLoader.GetUIHandleOfType<SkillLibraryUI>();
        public override LayersModeEnum LayersMode => LayersModeEnum.None;

        //本地化文本
        public static LocalizedText TitleText;
        public static LocalizedText EmptyText;
        public static LocalizedText HintAddToList;
        public static LocalizedText HintRemoveFromList;

        //数据访问
        public static List<SkillSlot> LibrarySlots => player.GetModPlayer<HalibutSave>().skillLibrarySlots;

        //面板尺寸
        private const float PanelWidth = 210f;
        private const float PanelHeight = 160f;
        private const float Padding = 20f;
        private const float TitleHeight = 28f;
        private const float IconSize = 36f;
        private const float IconSpacing = 6f;
        private const int IconsPerRow = 4;

        //展开动画
        public float Sengs;
        private float contentFade;

        //滚动相关
        private int scrollOffset;
        private float currentScrollOffset;
        private float scrollVelocity;
        private const float ScrollStiffness = 0.3f;
        private const float ScrollDamping = 0.7f;

        //视觉效果
        private float wavePhase;
        private float pulseTimer;
        private float shimmerPhase;

        //气泡粒子
        private readonly List<LibraryBubble> bubbles = [];
        private int bubbleSpawnTimer;

        //悬停状态
        private SkillSlot hoveredSlot;
        private int hoverTimer;
        private const int HoverDelay = 15;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "技能库");
            EmptyText = this.GetLocalization(nameof(EmptyText), () => "技能库为空\n在技能上按 [W] 可收纳技能");
            HintAddToList = this.GetLocalization(nameof(HintAddToList), () => "[W] 加入列表");
            HintRemoveFromList = this.GetLocalization(nameof(HintRemoveFromList), () => "[W] 收入库中");
        }

        public override void Update() {
            //展开动画逻辑
            bool shouldOpen = HalibutUIHead.Instance.Open && HalibutUIPanel.Instance.Sengs >= 0.8f;
            if (shouldOpen) {
                if (Sengs < 1f) {
                    Sengs += 0.08f;
                }
            }
            else {
                if (Sengs > 0f) {
                    Sengs -= 0.1f;
                }
            }
            Sengs = Math.Clamp(Sengs, 0f, 1f);

            if (Sengs <= 0.01f) {
                return;
            }

            //内容淡入
            if (Sengs > 0.5f) {
                contentFade += (1f - contentFade) * 0.12f;
            }
            else {
                contentFade *= 0.85f;
            }
            contentFade = Math.Clamp(contentFade, 0f, 1f);

            //动画计时器
            wavePhase += 0.02f;
            pulseTimer += 0.03f;
            shimmerPhase += 0.015f;
            if (wavePhase > MathHelper.TwoPi) wavePhase -= MathHelper.TwoPi;
            if (pulseTimer > MathHelper.TwoPi) pulseTimer -= MathHelper.TwoPi;
            if (shimmerPhase > MathHelper.TwoPi) shimmerPhase -= MathHelper.TwoPi;

            //计算位置（在主面板上方向上滑出）
            Vector2 panelPos = HalibutUIPanel.Instance.DrawPosition;
            float slideOffset = MathHelper.Max((1f - CWRUtils.EaseOutBack(Sengs)), 0) * PanelHeight;
            DrawPosition = new Vector2(panelPos.X, panelPos.Y - PanelHeight - 10 + slideOffset);
            Size = new Vector2(PanelWidth, PanelHeight + 40);
            UIHitBox = DrawPosition.GetRectangle(Size);
            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                player.mouseInterface = true;
                player.CWR().DontSwitchWeaponTime = 2;
            }

            //滚动处理
            UpdateScroll();

            //更新技能槽位
            UpdateSkillSlots();

            //更新气泡粒子
            UpdateBubbles();
        }

        private void UpdateScroll() {
            if (!hoverInMainPage) {
                return;
            }

            int maxRows = (int)Math.Ceiling(LibrarySlots.Count / (float)IconsPerRow);
            int visibleRows = (int)((PanelHeight - TitleHeight - Padding * 2) / (IconSize + IconSpacing));
            int maxScrollOffset = Math.Max(0, maxRows - visibleRows);

            int delta = PlayerInput.ScrollWheelDeltaForUI;
            if (delta != 0) {
                player.CWR().DontSwitchWeaponTime = 5;
                int steps = delta > 0 ? -1 : 1;
                scrollOffset = Math.Clamp(scrollOffset + steps, 0, maxScrollOffset);
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.3f, Pitch = steps > 0 ? 0.1f : -0.1f });
            }

            //平滑滚动
            float targetScroll = scrollOffset;
            float delta2 = targetScroll - currentScrollOffset;
            float springForce = delta2 * ScrollStiffness;
            float dampingForce = scrollVelocity * ScrollDamping;
            scrollVelocity += springForce - dampingForce;
            currentScrollOffset += scrollVelocity;
            if (Math.Abs(delta2) < 0.01f && Math.Abs(scrollVelocity) < 0.01f) {
                currentScrollOffset = targetScroll;
                scrollVelocity = 0f;
            }
        }

        private void UpdateSkillSlots() {
            hoveredSlot = null;
            float contentStartY = DrawPosition.Y + TitleHeight + Padding;
            float contentStartX = DrawPosition.X + Padding;

            for (int i = 0; i < LibrarySlots.Count; i++) {
                var slot = LibrarySlots[i];
                int row = i / IconsPerRow;
                int col = i % IconsPerRow;

                float visualRow = row - currentScrollOffset;
                float x = contentStartX + col * (IconSize + IconSpacing);
                float y = contentStartY + visualRow * (IconSize + IconSpacing);

                slot.DrawPosition = new Vector2(x, y);
                slot.Size = new Vector2(IconSize, IconSize);

                //检测悬停
                Rectangle slotRect = new Rectangle((int)x, (int)y, (int)IconSize, (int)IconSize);
                bool isVisible = visualRow >= -0.5f && visualRow < ((PanelHeight - TitleHeight - Padding * 2) / (IconSize + IconSpacing));

                if (isVisible && slotRect.Contains(MouseHitBox) && contentFade > 0.5f) {
                    hoveredSlot = slot;
                    hoverTimer++;

                    //检测W键按下，将技能加回主列表
                    if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.W) &&
                        !Main.oldKeyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.W)) {
                        MoveToMainList(slot);
                    }
                }
            }

            if (hoveredSlot == null) {
                hoverTimer = 0;
            }
        }

        private void UpdateBubbles() {
            bubbleSpawnTimer++;
            if (Sengs > 0.5f && bubbleSpawnTimer >= 20 && bubbles.Count < 12) {
                bubbleSpawnTimer = 0;
                float x = DrawPosition.X + Main.rand.NextFloat(20f, Size.X - 20f);
                float y = DrawPosition.Y + Size.Y - 10f;
                bubbles.Add(new LibraryBubble(new Vector2(x, y)));
            }

            for (int i = bubbles.Count - 1; i >= 0; i--) {
                if (bubbles[i].Update(DrawPosition, Size)) {
                    bubbles.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 将技能从主列表移动到技能库
        /// </summary>
        public void MoveToLibrary(SkillSlot slot) {
            if (slot?.FishSkill == null) {
                return;
            }

            var mainSlots = player.GetModPlayer<HalibutSave>().halibutUISkillSlots;
            if (!mainSlots.Contains(slot)) {
                return;
            }

            mainSlots.Remove(slot);
            LibrarySlots.Add(slot);

            //播放音效
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = 0.3f });

            //如果当前选中的技能被移走了，清空选择
            if (HalibutUIHead.FishSkill == slot.FishSkill) {
                HalibutUIHead.FishSkill = null;
            }
        }

        /// <summary>
        /// 将技能从技能库移动回主列表
        /// </summary>
        public void MoveToMainList(SkillSlot slot) {
            if (slot?.FishSkill == null) {
                return;
            }

            if (!LibrarySlots.Contains(slot)) {
                return;
            }

            LibrarySlots.Remove(slot);
            var mainSlots = player.GetModPlayer<HalibutSave>().halibutUISkillSlots;
            mainSlots.Add(slot);

            //播放音效
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.2f });
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (Sengs <= 0.01f) {
                return;
            }

            float alpha = Math.Min(Sengs * 1.5f, 1f);

            //绘制面板
            DrawPanel(spriteBatch, alpha);

            //绘制气泡（在内容后面）
            foreach (var bubble in bubbles) {
                bubble.Draw(spriteBatch, alpha * 0.6f);
            }

            //绘制内容
            if (contentFade > 0.01f) {
                DrawContent(spriteBatch, alpha * contentFade);
            }
        }

        private void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle panelRect = UIHitBox;

            //阴影
            Rectangle shadowRect = panelRect;
            shadowRect.Offset(4, 6);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.45f));

            //背景渐变（深海风格）
            int segs = 20;
            for (int i = 0; i < segs; i++) {
                float t = i / (float)segs;
                float t2 = (i + 1) / (float)segs;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color abyssDeep = new Color(4, 14, 24);
                Color abyssMid = new Color(8, 38, 58);
                Color bioEdge = new Color(15, 70, 100);

                float breathing = (float)Math.Sin(pulseTimer) * 0.5f + 0.5f;
                Color blendBase = Color.Lerp(abyssDeep, abyssMid, (float)Math.Sin(pulseTimer * 0.5f + t * 1.4f) * 0.5f + 0.5f);
                Color c = Color.Lerp(blendBase, bioEdge, t * 0.5f * (0.4f + breathing * 0.6f));
                c *= alpha * 0.95f;
                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }

            //波浪纹理叠加
            DrawWaveOverlay(spriteBatch, panelRect, alpha * 0.7f);

            //边框
            float pulse = (float)Math.Sin(pulseTimer * 1.5f) * 0.5f + 0.5f;
            Color edgeColor = Color.Lerp(new Color(40, 140, 180), new Color(80, 200, 240), pulse) * (alpha * 0.75f);
            DrawPanelBorder(spriteBatch, panelRect, edgeColor);

            //流光效果
            DrawShimmerEffect(spriteBatch, panelRect, alpha);
        }

        private void DrawWaveOverlay(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            int bands = 4;
            for (int i = 0; i < bands; i++) {
                float t = i / (float)bands;
                float y = rect.Y + 12 + t * (rect.Height - 24);
                float amp = 4f + (float)Math.Sin((wavePhase + t) * 2f) * 2.5f;
                float thickness = 1.5f;
                int segments = 30;
                Vector2 prev = Vector2.Zero;

                for (int s = 0; s <= segments; s++) {
                    float p = s / (float)segments;
                    float localY = y + (float)Math.Sin(wavePhase * 2.5f + p * MathHelper.TwoPi * 1.1f + t) * amp;
                    Vector2 point = new(rect.X + 6 + p * (rect.Width - 12), localY);

                    if (s > 0) {
                        Vector2 diff = point - prev;
                        float len = diff.Length();
                        if (len > 0.01f) {
                            float rot = diff.ToRotation();
                            Color c = new Color(35, 120, 160) * (alpha * 0.06f);
                            spriteBatch.Draw(pixel, prev, new Rectangle(0, 0, 1, 1), c, rot, Vector2.Zero, new Vector2(len, thickness), SpriteEffects.None, 0f);
                        }
                    }
                    prev = point;
                }
            }
        }

        private static void DrawPanelBorder(SpriteBatch spriteBatch, Rectangle rect, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            //上下边
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Rectangle(0, 0, 1, 1), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Rectangle(0, 0, 1, 1), color * 0.7f);
            //左右边
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), color * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Rectangle(0, 0, 1, 1), color * 0.85f);

            //角落装饰
            DrawCornerStar(spriteBatch, new Vector2(rect.X + 8, rect.Y + 8), color * 0.8f);
            DrawCornerStar(spriteBatch, new Vector2(rect.Right - 8, rect.Y + 8), color * 0.8f);
        }

        private static void DrawCornerStar(SpriteBatch spriteBatch, Vector2 pos, Color color) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float size = 4f;
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color, 0f, new Vector2(0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, pos, new Rectangle(0, 0, 1, 1), color * 0.8f, MathHelper.PiOver2, new Vector2(0.5f), new Vector2(size, size * 0.25f), SpriteEffects.None, 0f);
        }

        private void DrawShimmerEffect(SpriteBatch spriteBatch, Rectangle rect, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float shimmerT = (shimmerPhase % 1f);

            //顶部流光
            float shimmerX = rect.X + shimmerT * rect.Width;
            float intensity = (float)Math.Sin(shimmerT * MathHelper.Pi) * 0.6f;
            Color shimmerColor = new Color(120, 200, 240) * (alpha * intensity);
            spriteBatch.Draw(pixel, new Vector2(shimmerX, rect.Y), new Rectangle(0, 0, 1, 1), shimmerColor, 0f, new Vector2(0.5f), new Vector2(20f, 3f), SpriteEffects.None, 0f);
        }

        private void DrawContent(SpriteBatch spriteBatch, float alpha) {
            //标题
            Vector2 titlePos = DrawPosition + new Vector2(Padding, 8);
            string title = TitleText.Value;

            //标题光晕
            Color titleGlow = new Color(100, 200, 240) * (alpha * 0.4f);
            for (int i = 0; i < 4; i++) {
                float angle = MathHelper.TwoPi * i / 4f + pulseTimer * 0.3f;
                Vector2 offset = angle.ToRotationVector2() * 1.5f;
                Utils.DrawBorderString(spriteBatch, title, titlePos + offset, titleGlow, 0.85f);
            }
            Utils.DrawBorderString(spriteBatch, title, titlePos, Color.White * alpha, 0.85f);

            //分割线
            Vector2 divStart = titlePos + new Vector2(0, 22);
            Vector2 divEnd = divStart + new Vector2(Size.X - Padding * 2, 0);
            DrawGradientLine(spriteBatch, divStart, divEnd, new Color(80, 180, 220) * (alpha * 0.7f), new Color(80, 180, 220) * (alpha * 0.1f), 1.2f);

            //内容区域裁剪
            float contentStartY = DrawPosition.Y + TitleHeight + Padding;
            float contentHeight = PanelHeight - TitleHeight - Padding * 2;

            if (LibrarySlots.Count == 0) {
                //显示空提示
                string emptyText = EmptyText.Value;
                string[] lines = emptyText.Split('\n');
                float lineHeight = 18f;
                Vector2 emptyPos = new Vector2(DrawPosition.X + Size.X / 2, contentStartY + contentHeight / 2 - lines.Length * lineHeight / 2);

                for (int i = 0; i < lines.Length; i++) {
                    Vector2 lineSize = FontAssets.MouseText.Value.MeasureString(lines[i]) * 0.7f;
                    Vector2 linePos = emptyPos + new Vector2(-lineSize.X / 2, i * lineHeight);
                    Utils.DrawBorderString(spriteBatch, lines[i], linePos, new Color(150, 200, 220) * (alpha * 0.6f), 0.7f);
                }
                return;
            }

            //绘制技能图标
            for (int i = 0; i < LibrarySlots.Count; i++) {
                var slot = LibrarySlots[i];
                if (slot?.FishSkill?.Icon == null) {
                    continue;
                }

                int row = i / IconsPerRow;
                float visualRow = row - currentScrollOffset;

                //可见性检测
                if (visualRow < -0.5f || visualRow >= (contentHeight / (IconSize + IconSpacing))) {
                    continue;
                }

                //计算透明度（边缘淡出）
                float slotAlpha = 1f;
                if (visualRow < 0.3f) {
                    slotAlpha = Math.Max(0, (visualRow + 0.5f) / 0.8f);
                }
                else if (visualRow > (contentHeight / (IconSize + IconSpacing)) - 1.3f) {
                    slotAlpha = Math.Max(0, ((contentHeight / (IconSize + IconSpacing)) - visualRow) / 1.3f);
                }

                Vector2 iconCenter = slot.DrawPosition + new Vector2(IconSize / 2);
                float finalAlpha = alpha * slotAlpha;
                bool isHovered = slot == hoveredSlot;

                //悬停发光
                if (isHovered) {
                    float hoverGlow = (float)Math.Sin(pulseTimer * 3f) * 0.3f + 0.7f;
                    Color glowColor = new Color(120, 200, 240) * (finalAlpha * hoverGlow * 0.5f);
                    spriteBatch.Draw(slot.FishSkill.Icon, iconCenter, null, glowColor, 0f, slot.FishSkill.Icon.Size() / 2, 1.3f, SpriteEffects.None, 0f);
                }

                //图标主体
                Color iconColor = Color.White * finalAlpha;
                if (isHovered) {
                    iconColor = Color.Lerp(iconColor, Color.Gold, 0.2f);
                }
                spriteBatch.Draw(slot.FishSkill.Icon, iconCenter, null, iconColor, 0f, slot.FishSkill.Icon.Size() / 2, 1f, SpriteEffects.None, 0f);
            }

            //悬停提示
            if (hoveredSlot != null && hoverTimer >= HoverDelay) {
                DrawHoverTooltip(spriteBatch, alpha);
            }
        }

        private void DrawHoverTooltip(SpriteBatch spriteBatch, float alpha) {
            if (hoveredSlot?.FishSkill == null) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            string name = hoveredSlot.FishSkill.DisplayName?.Value ?? "未知技能";
            string hint = HintAddToList.Value;

            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(name) * 0.8f;
            Vector2 hintSize = FontAssets.MouseText.Value.MeasureString(hint) * 0.7f;
            float width = Math.Max(nameSize.X, hintSize.X) + 16f;
            float height = nameSize.Y + hintSize.Y + 12f;

            Vector2 tooltipPos = MousePosition + new Vector2(16, -height - 8);
            if (tooltipPos.X + width > Main.screenWidth - 10) {
                tooltipPos.X = Main.screenWidth - width - 10;
            }
            if (tooltipPos.Y < 10) {
                tooltipPos.Y = MousePosition.Y + 20;
            }

            Rectangle tooltipRect = new Rectangle((int)tooltipPos.X, (int)tooltipPos.Y, (int)width, (int)height);

            //背景
            spriteBatch.Draw(pixel, tooltipRect, new Rectangle(0, 0, 1, 1), new Color(15, 35, 50) * (alpha * 0.95f));

            //边框
            Color borderColor = new Color(80, 180, 220) * (alpha * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Y, tooltipRect.Width, 1), new Rectangle(0, 0, 1, 1), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Bottom - 1, tooltipRect.Width, 1), new Rectangle(0, 0, 1, 1), borderColor * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.X, tooltipRect.Y, 1, tooltipRect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(tooltipRect.Right - 1, tooltipRect.Y, 1, tooltipRect.Height), new Rectangle(0, 0, 1, 1), borderColor * 0.85f);

            //文字
            Vector2 textPos = tooltipPos + new Vector2(8, 6);
            Utils.DrawBorderString(spriteBatch, name, textPos, Color.White * alpha, 0.8f);
            Utils.DrawBorderString(spriteBatch, hint, textPos + new Vector2(0, nameSize.Y + 4), new Color(150, 220, 255) * alpha, 0.7f);
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

            int segments = Math.Max(1, (int)(length / 8f));
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
    }

    /// <summary>
    /// 技能库气泡粒子
    /// </summary>
    internal class LibraryBubble
    {
        public Vector2 Position;
        public float Size;
        public float Alpha;
        public float Speed;
        public float WobblePhase;
        public float WobbleSpeed;

        public LibraryBubble(Vector2 pos) {
            Position = pos;
            Size = Main.rand.NextFloat(3f, 7f);
            Alpha = 1f;
            Speed = Main.rand.NextFloat(0.4f, 0.9f);
            WobblePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            WobbleSpeed = Main.rand.NextFloat(0.03f, 0.06f);
        }

        public bool Update(Vector2 panelPos, Vector2 panelSize) {
            Position.Y -= Speed;
            WobblePhase += WobbleSpeed;
            Position.X += (float)Math.Sin(WobblePhase) * 0.3f;

            //淡出
            if (Position.Y < panelPos.Y + 20) {
                Alpha -= 0.05f;
            }

            return Alpha <= 0f || Position.Y < panelPos.Y - 10;
        }

        public void Draw(SpriteBatch spriteBatch, float panelAlpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float finalAlpha = Alpha * panelAlpha;

            //气泡主体
            Color bubbleColor = new Color(100, 180, 220) * (finalAlpha * 0.4f);
            spriteBatch.Draw(pixel, Position, new Rectangle(0, 0, 1, 1), bubbleColor, 0f, new Vector2(0.5f), Size, SpriteEffects.None, 0f);

            //高光
            Color highlightColor = new Color(180, 230, 255) * (finalAlpha * 0.3f);
            Vector2 highlightOffset = new Vector2(-Size * 0.2f, -Size * 0.2f);
            spriteBatch.Draw(pixel, Position + highlightOffset, new Rectangle(0, 0, 1, 1), highlightColor, 0f, new Vector2(0.5f), Size * 0.3f, SpriteEffects.None, 0f);
        }
    }
}
