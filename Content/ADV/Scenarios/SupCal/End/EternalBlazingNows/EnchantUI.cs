using CalamityMod;
using CalamityMod.UI.CalamitasEnchants;
using InnoVault.GameSystem;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
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
using Terraria.ModLoader.IO;
using Terraria.UI.Chat;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows
{
    /// <summary>
    /// 永恒燃烧的如今
    /// 提供免费无限制的武器附魔，但需要等待附魔时间
    /// </summary>
    [VaultLoaden("@CalamityMod/UI/CalamitasEnchantments")]
    internal class EnchantUI : UIHandle, ILocalizedModType
    {
        public static Asset<Texture2D> CalamitasCurseItemSlot = null;
        public static Asset<Texture2D> CalamitasCurseUI_Button = null;
        public static Asset<Texture2D> CalamitasCurseUI_ButtonHovered = null;
        public static Asset<Texture2D> CalamitasCurseUI_ButtonClicked = null;
        public static Asset<Texture2D> CalamitasCurseUI_ArrowUp = null;
        public static Asset<Texture2D> CalamitasCurseUI_ArrowDown = null;
        public static Asset<Texture2D> CalamitasCurseUI_ArrowUpHovered = null;
        public static Asset<Texture2D> CalamitasCurseUI_ArrowDownHovered = null;
        public static Asset<Texture2D> CalamitasCurseUI_ArrowUpClicked = null;
        public static Asset<Texture2D> CalamitasCurseUI_ArrowDownClicked = null;
        public static EnchantUI Instance => UIHandleLoader.GetUIHandleOfType<EnchantUI>();

        private static bool DrogBool = false;
        private static Vector2 DrogOffset;

        //UI布局参数
        public static Vector2 UITopLeft => Instance.DrawPosition;
        public static float UIScale => 0.8f;

        //展开/收起状态
        public static bool IsCollapsed = false;
        public static float CollapseProgress = 0f;// 0 = 完全展开, 1 = 完全折叠
        public static float CollapseAnimSpeed = 0.12f;
        public static float CollapsedWidth = 60f; //折叠后的宽度
        public static float CollapsedHeight = 80f; //折叠后的高度

        //当前状态
        public static Item CurrentlyHeldItem = new Item();
        public static int EnchantIndex = 0;
        public static Enchantment? SelectedEnchantment = null;

        //附魔等待时间相关
        public static bool IsEnchanting = false;
        public static float EnchantProgress = 0f;
        public static float EnchantDuration = 180f; //3秒附魔时间

        //按钮点击冷却
        public static float TopButtonClickCountdown = 0f;
        public static float BottomButtonClickCountdown = 0f;
        public static float EnchantButtonClickCountdown = 0f;
        public static float ToggleButtonClickCountdown = 0f;

        //硫磺火视觉效果参数
        private float flameTimer = 0f;
        private float emberGlowTimer = 0f;
        private float heatWavePhase = 0f;
        private float infernoPulse = 0f;

        private float lerpProgress;

        //粒子系统
        private readonly List<EmberParticle> embers = new();
        private int emberSpawnTimer = 0;
        private readonly List<AshParticle> ashes = new();
        private int ashSpawnTimer = 0;
        private readonly List<FlameWisp> flameWisps = new();
        private int wispSpawnTimer = 0;

        public override bool Active => Main.playerInventory && player.chest == -1 && player.talkNPC == -1 && !Main.InGuideCraftMenu && EbnPlayer.OnEbn(player);

        public string LocalizationCategory => "UI";

        public static LocalizedText ExpandHint;
        public static LocalizedText CollapseHint;
        public static LocalizedText EnchantTitle;

        public new void LoadUIData(TagCompound tag) {
            tag.TryGet(nameof(DrogOffset), out DrogOffset);
            tag.TryGet(nameof(IsCollapsed), out IsCollapsed);
            if (tag.TryGet(nameof(CurrentlyHeldItem), out TagCompound itemTag)) {
                CurrentlyHeldItem = ItemIO.Load(itemTag);
            }
            else {
                CurrentlyHeldItem = new Item();
            }
        }

        public new void SaveUIData(TagCompound tag) {
            tag[nameof(DrogOffset)] = DrogOffset;
            tag[nameof(IsCollapsed)] = IsCollapsed;
            CurrentlyHeldItem ??= new Item();
            tag[nameof(CurrentlyHeldItem)] = ItemIO.Save(CurrentlyHeldItem);
        }

        public override void SetStaticDefaults() {
            ExpandHint = this.GetLocalization(nameof(ExpandHint), () => "展开炼铸界面");
            CollapseHint = this.GetLocalization(nameof(CollapseHint), () => "收起炼铸界面");
            EnchantTitle = this.GetLocalization(nameof(EnchantTitle), () => "炼铸");
        }

        public override void Update() {
            if (!Active) {
                if (!CurrentlyHeldItem.IsAir) {
                    player.QuickSpawnItem(player.GetSource_Misc(CurrentlyHeldItem.Name), CurrentlyHeldItem, CurrentlyHeldItem.stack);
                    CurrentlyHeldItem.TurnToAir();
                }

                EnchantIndex = 0;
                IsEnchanting = false;
                EnchantProgress = 0f;
                return;
            }

            Vector2 backgroundScale = Vector2.One * UIScale;
            float currentWidth = MathHelper.Lerp(392 * backgroundScale.X, CollapsedWidth, lerpProgress);
            float currentHeight = MathHelper.Lerp(324 * backgroundScale.Y, CollapsedHeight, lerpProgress);

            UIHitBox = new Rectangle(
                (int)UITopLeft.X,
                (int)UITopLeft.Y,
                (int)currentWidth,
                (int)currentHeight
            );

            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);
            if (hoverInMainPage) {
                if (keyLeftPressState == KeyPressState.Held) {
                    if (!DrogBool) {
                        DrogOffset = MousePosition.To(DrawPosition);
                    }
                    DrogBool = true;
                }
            }
            if (DrogBool) {
                DrawPosition = MousePosition + DrogOffset;
                if (keyLeftPressState == KeyPressState.Released) {
                    DrogBool = false;
                    DrogOffset = MousePosition.To(DrawPosition);
                }
            }
        }

        public override void LogicUpdate() {
            if (DrawPosition == Vector2.Zero || DrawPosition == default) {
                DrawPosition = new Vector2(168f, 320f);
            }

            //根据折叠状态计算面板大小
            lerpProgress = MathHelper.SmoothStep(0f, 1f, CollapseProgress);

            //递减点击冷却
            if (TopButtonClickCountdown > 0f)
                TopButtonClickCountdown--;
            if (BottomButtonClickCountdown > 0f)
                BottomButtonClickCountdown--;
            if (EnchantButtonClickCountdown > 0f)
                EnchantButtonClickCountdown--;
            if (ToggleButtonClickCountdown > 0f)
                ToggleButtonClickCountdown--;

            //更新折叠动画
            float targetProgress = IsCollapsed ? 1f : 0f;
            if (CollapseProgress < targetProgress) {
                CollapseProgress = Math.Min(1f, CollapseProgress + CollapseAnimSpeed);
            }
            else if (CollapseProgress > targetProgress) {
                CollapseProgress = Math.Max(0f, CollapseProgress - CollapseAnimSpeed);
            }

            //更新火焰动画计时器
            flameTimer += 0.045f;
            emberGlowTimer += 0.038f;
            heatWavePhase += 0.025f;
            infernoPulse += 0.012f;

            if (flameTimer > MathHelper.TwoPi) flameTimer -= MathHelper.TwoPi;
            if (emberGlowTimer > MathHelper.TwoPi) emberGlowTimer -= MathHelper.TwoPi;
            if (heatWavePhase > MathHelper.TwoPi) heatWavePhase -= MathHelper.TwoPi;
            if (infernoPulse > MathHelper.TwoPi) infernoPulse -= MathHelper.TwoPi;

            //更新附魔进度
            if (IsEnchanting) {
                EnchantProgress += 1f;

                //附魔完成
                if (EnchantProgress >= EnchantDuration) {
                    CompleteEnchantment();
                }
            }

            //更新粒子
            UpdateParticles();
        }

        private void UpdateParticles() {
            //折叠状态下减少粒子效果
            if (CollapseProgress > 0.5f)
                return;

            Vector2 uiCenter = UITopLeft + new Vector2(200f, 150f) * UIScale;
            Vector2 uiSize = new Vector2(400f, 300f) * UIScale;

            //生成余烬粒子
            emberSpawnTimer++;
            if (emberSpawnTimer >= 8 && embers.Count < 35) {
                emberSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(uiCenter.X - uiSize.X / 2 + 30f, uiCenter.X + uiSize.X / 2 - 30f);
                Vector2 startPos = new(xPos, uiCenter.Y + uiSize.Y / 2);
                embers.Add(new EmberParticle(startPos));
            }

            for (int i = embers.Count - 1; i >= 0; i--) {
                if (embers[i].Update(uiCenter, uiSize)) {
                    embers.RemoveAt(i);
                }
            }

            //生成灰烬粒子
            ashSpawnTimer++;
            if (ashSpawnTimer >= 12 && ashes.Count < 25) {
                ashSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(uiCenter.X - uiSize.X / 2 + 30f, uiCenter.X + uiSize.X / 2 - 30f);
                Vector2 startPos = new(xPos, uiCenter.Y + uiSize.Y / 2);
                ashes.Add(new AshParticle(startPos));
            }

            for (int i = ashes.Count - 1; i >= 0; i--) {
                if (ashes[i].Update(uiCenter, uiSize)) {
                    ashes.RemoveAt(i);
                }
            }

            //生成火焰精灵
            wispSpawnTimer++;
            if (wispSpawnTimer >= 45 && flameWisps.Count < 8) {
                wispSpawnTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(uiCenter.X - uiSize.X / 2 + 40f, uiCenter.X + uiSize.X / 2 - 40f),
                    Main.rand.NextFloat(uiCenter.Y - uiSize.Y / 2 + 60f, uiCenter.Y + uiSize.Y / 2 - 60f)
                );
                flameWisps.Add(new FlameWisp(startPos));
            }

            for (int i = flameWisps.Count - 1; i >= 0; i--) {
                if (flameWisps[i].Update(uiCenter, uiSize)) {
                    flameWisps.RemoveAt(i);
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            Vector2 backgroundScale = Vector2.One * UIScale;
            float currentWidth = MathHelper.Lerp(392 * backgroundScale.X, CollapsedWidth, lerpProgress);
            float currentHeight = MathHelper.Lerp(324 * backgroundScale.Y, CollapsedHeight, lerpProgress);
            UIHitBox = new Rectangle(
                (int)UITopLeft.X,
                (int)UITopLeft.Y,
                (int)currentWidth,
                (int)currentHeight
            );

            //绘制硫磺火风格背景
            DrawBrimstoneBackground(spriteBatch, UIHitBox);

            //禁用鼠标交互
            DisableMouseWhenOverUI(UIHitBox);

            //绘制展开/收起按钮
            DrawToggleButton(spriteBatch, UIHitBox);

            //如果正在折叠或已折叠，只显示简化内容
            if (CollapseProgress > 0.01f) {
                DrawCollapsedContent(spriteBatch, UIHitBox, lerpProgress);
                return;
            }

            //选择附魔
            IEnumerable<Enchantment> possibleEnchantments = SelectEnchantment();

            //物品槽位置
            Vector2 itemSlotDrawPosition = UITopLeft + new Vector2(36f, 46f) * backgroundScale;
            //附魔按钮位置
            Vector2 enchantIconDrawPosition = UITopLeft + new Vector2(52f, 126f) * backgroundScale;

            DrawItemIcon(spriteBatch, itemSlotDrawPosition, enchantIconDrawPosition, backgroundScale, out bool isHoveringOverItemIcon, out bool isHoveringOverEnchantIcon);

            if (isHoveringOverItemIcon)
                InteractWithItemSlot();

            //调整按钮位置
            Vector2 topButtonPos = UITopLeft + new Vector2(240f, 42f) * backgroundScale;
            Vector2 bottomButtonPos = UITopLeft + new Vector2(240f, 110f) * backgroundScale;
            DrawAndInteractWithButtons(spriteBatch, possibleEnchantments, topButtonPos, bottomButtonPos, backgroundScale);

            //绘制附魔信息
            if (SelectedEnchantment.HasValue) {
                //绘制附魔名称
                DrawEnchantmentName(spriteBatch, UITopLeft + new Vector2(300f, 70f) * backgroundScale);

                //绘制附魔描述
                Point descriptionDrawPositionTopLeft = (UITopLeft + new Vector2(40f, 180f) * backgroundScale).ToPoint();
                DrawEnchantmentDescription(spriteBatch, descriptionDrawPositionTopLeft);

                //绘制附魔图标
                if (!string.IsNullOrEmpty(SelectedEnchantment.Value.IconTexturePath)) {
                    Vector2 iconDrawPositionTopLeft = UITopLeft + new Vector2(226f, 56f) * UIScale;
                    Texture2D iconTexture = ModContent.Request<Texture2D>(SelectedEnchantment.Value.IconTexturePath).Value;
                    DrawIcon(spriteBatch, iconDrawPositionTopLeft, iconTexture);
                }
            }

            //处理附魔按钮点击
            if (isHoveringOverEnchantIcon && !IsEnchanting) {
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    InteractWithEnchantIcon();
                    EnchantButtonClickCountdown = 15f;
                }
            }

            //绘制附魔进度
            if (IsEnchanting) {
                DrawEnchantProgress(spriteBatch, UIHitBox);
            }
        }

        #region 绘制函数

        private void DrawToggleButton(SpriteBatch spriteBatch, Rectangle panelRect) {
            //按钮位置在面板右上角
            int buttonSize = 24;
            Rectangle buttonRect = new Rectangle(
                panelRect.Right - buttonSize - 8,
                panelRect.Y + 8,
                buttonSize,
                buttonSize
            );

            bool isHovering = MouseHitBox.Intersects(buttonRect);

            //绘制按钮背景
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color buttonBg = isHovering ? new Color(255, 140, 70) * 0.6f : new Color(180, 60, 30) * 0.5f;

            if (ToggleButtonClickCountdown > 0f) {
                buttonBg = new Color(255, 180, 100) * 0.7f;
            }

            spriteBatch.Draw(pixel, buttonRect, buttonBg);

            //绘制按钮边框
            int borderWidth = 2;
            Color borderColor = new Color(255, 200, 120) * 0.8f;
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, buttonRect.Width, borderWidth), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Bottom - borderWidth, buttonRect.Width, borderWidth), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, borderWidth, buttonRect.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.Right - borderWidth, buttonRect.Y, borderWidth, buttonRect.Height), borderColor);

            //绘制箭头图标
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string arrowText = IsCollapsed ? "►" : "◄";
            Vector2 textSize = font.MeasureString(arrowText);
            Vector2 textPos = buttonRect.Center.ToVector2() - textSize / 2;

            Utils.DrawBorderString(spriteBatch, arrowText, textPos, Color.White, 1f);

            //处理点击
            if (isHovering && Main.mouseLeft && Main.mouseLeftRelease && ToggleButtonClickCountdown <= 0f) {
                IsCollapsed = !IsCollapsed;
                ToggleButtonClickCountdown = 15f;
                SoundEngine.PlaySound(SoundID.MenuTick);
            }

            //显示悬停提示
            if (isHovering) {
                Main.instance.MouseText(IsCollapsed ? ExpandHint.Value : CollapseHint.Value);
            }
        }

        private void DrawCollapsedContent(SpriteBatch spriteBatch, Rectangle panelRect, float lerpProgress) {
            //折叠状态下显示简化的火焰效果和提示文字
            float alpha = 1f - lerpProgress * 0.5f;

            //绘制简化粒子
            foreach (var ember in embers.Take(5)) {
                ember.Draw(spriteBatch, alpha * 0.5f);
            }

            //显示标题
            if (lerpProgress < 0.8f) {
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                string text = EnchantTitle.Value;
                Vector2 textSize = font.MeasureString(text);
                Vector2 textPos = new Vector2(
                    panelRect.Center.X - textSize.X / 2,
                    panelRect.Center.Y - textSize.Y / 2
                );

                Color textColor = new Color(255, 200, 120) * (1f - lerpProgress);
                Utils.DrawBorderString(spriteBatch, text, textPos, textColor, 1f);
            }
        }

        private void DrawBrimstoneBackground(SpriteBatch spriteBatch, Rectangle panelRect) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //阴影效果
            Rectangle shadow = panelRect;
            shadow.Offset(7, 9);
            spriteBatch.Draw(pixel, shadow, new Rectangle(0, 0, 1, 1), new Color(20, 0, 0) * 0.65f);

            //渐变背景
            int segments = 35;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = panelRect.Y + (int)(t * panelRect.Height);
                int y2 = panelRect.Y + (int)(t2 * panelRect.Height);
                Rectangle r = new(panelRect.X, y1, panelRect.Width, Math.Max(1, y2 - y1));

                Color brimstoneDeep = new Color(25, 5, 5);
                Color brimstoneMid = new Color(80, 15, 10);
                Color brimstoneHot = new Color(140, 35, 20);

                float breathing = (float)Math.Sin(infernoPulse * 1.5f) * 0.5f + 0.5f;
                float flameWave = (float)Math.Sin(flameTimer * 0.6f + t * 2.2f) * 0.5f + 0.5f;

                Color baseColor = Color.Lerp(brimstoneDeep, brimstoneMid, flameWave);
                Color finalColor = Color.Lerp(baseColor, brimstoneHot, t * 0.5f * (0.3f + breathing * 0.7f));
                finalColor *= 0.92f;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), finalColor);
            }

            //火焰脉冲叠加层
            float pulseBrightness = (float)Math.Sin(infernoPulse * 1.8f) * 0.5f + 0.5f;
            Color pulseOverlay = new Color(120, 25, 15) * (0.25f * pulseBrightness);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), pulseOverlay);

            //内发光
            float glowPulse = (float)Math.Sin(emberGlowTimer * 1.5f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-7, -7);
            spriteBatch.Draw(pixel, inner, new Rectangle(0, 0, 1, 1), new Color(180, 60, 30) * (0.12f * (0.5f + glowPulse * 0.5f)));

            //绘制火焰边框
            DrawBrimstoneFrame(spriteBatch, panelRect, glowPulse);

            //只在展开状态绘制完整粒子
            if (CollapseProgress < 0.5f) {
                foreach (var ash in ashes) {
                    ash.Draw(spriteBatch, 0.7f * (1f - CollapseProgress * 2f));
                }
                foreach (var wisp in flameWisps) {
                    wisp.Draw(spriteBatch, 0.8f * (1f - CollapseProgress * 2f));
                }
                foreach (var ember in embers) {
                    ember.Draw(spriteBatch, 0.95f * (1f - CollapseProgress * 2f));
                }
            }
        }

        private static void DrawBrimstoneFrame(SpriteBatch sb, Rectangle rect, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * 0.85f;
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(220, 100, 50) * (0.22f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
        }

        private void DrawEnchantProgress(SpriteBatch spriteBatch, Rectangle panelRect) {
            //绘制进度条
            float progress = EnchantProgress / EnchantDuration;
            Rectangle progressBarBg = new Rectangle(
                panelRect.X + 50,
                panelRect.Bottom - 40,
                panelRect.Width - 100,
                20
            );

            //进度条背景
            spriteBatch.Draw(VaultAsset.placeholder2.Value, progressBarBg, new Color(30, 10, 5) * 0.8f);

            //进度条填充
            Rectangle progressBarFill = progressBarBg;
            progressBarFill.Width = (int)((int)(progressBarFill.Width * progress) * 5.25f);

            //绘制火焰渐变进度条
            int segments = 10;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Rectangle segment = new Rectangle(
                    progressBarFill.X + (int)(progressBarFill.Width * t / segments),
                    progressBarFill.Y,
                    Math.Max(1, progressBarFill.Width / segments),
                    progressBarFill.Height
                );

                Color fillColor = Color.Lerp(
                    new Color(180, 60, 30),
                    new Color(255, 140, 70),
                    (float)Math.Sin((Main.GlobalTimeWrappedHourly + t) * 3f) * 0.5f + 0.5f
                );

                spriteBatch.Draw(VaultAsset.placeholder2.Value, segment, fillColor);
            }

            //绘制进度文本
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string progressText = $"{(int)(progress * 100)}%";
            Vector2 textSize = font.MeasureString(progressText);
            Vector2 textPos = new Vector2(
                progressBarBg.Center.X - textSize.X / 2,
                progressBarBg.Center.Y - textSize.Y / 2
            );

            Utils.DrawBorderString(spriteBatch, progressText, textPos, Color.White * 0.9f, 1.0f);
        }

        private void DrawItemIcon(SpriteBatch spriteBatch, Vector2 itemSlotDrawPosition, Vector2 enchantIconDrawPosition, Vector2 scale, out bool isHoveringOverItemIcon, out bool isHoveringOverEnchantIcon) {
            isHoveringOverEnchantIcon = false;
            Texture2D itemSlotTexture = CalamitasCurseItemSlot.Value;
            Texture2D enchantIconTexture = CalamitasCurseUI_Button.Value;

            //增大物品槽和按钮的缩放
            Vector2 itemSlotScale = scale * 1.5f;
            Vector2 enchantButtonScale = scale * 1.5f;

            Rectangle enchantIconArea = new Rectangle(
                (int)enchantIconDrawPosition.X,
                (int)enchantIconDrawPosition.Y,
                (int)(enchantIconTexture.Width * enchantButtonScale.X),
                (int)(enchantIconTexture.Height * enchantButtonScale.Y)
            );

            //检测鼠标悬停
            if (MouseHitBox.Intersects(enchantIconArea) && !IsEnchanting) {
                enchantIconTexture = CalamitasCurseUI_ButtonHovered.Value;
                isHoveringOverEnchantIcon = true;
            }

            if (EnchantButtonClickCountdown > 0f)
                enchantIconTexture = CalamitasCurseUI_ButtonClicked.Value;

            isHoveringOverItemIcon = MouseHitBox.Intersects(new Rectangle(
                (int)itemSlotDrawPosition.X,
                (int)itemSlotDrawPosition.Y,
                (int)(itemSlotTexture.Width * itemSlotScale.X),
                (int)(itemSlotTexture.Height * itemSlotScale.Y)
            ));

            spriteBatch.Draw(itemSlotTexture, itemSlotDrawPosition, null, Color.White, 0f, Vector2.Zero, itemSlotScale, SpriteEffects.None, 0f);

            //绘制物品
            if (!CurrentlyHeldItem.IsAir) {
                float inventoryScale = Main.inventoryScale;
                Texture2D itemTexture = TextureAssets.Item[CurrentlyHeldItem.type].Value;
                Rectangle itemFrame = itemTexture.Frame(1, 1, 0, 0);
                bool hasMultipleFrames = Main.itemAnimations[CurrentlyHeldItem.type] != null;
                if (hasMultipleFrames)
                    itemFrame = Main.itemAnimations[CurrentlyHeldItem.type].GetFrame(itemTexture);

                float baseScale = UIScale * 1.5f; //增大物品显示

                float itemScale = 1f;
                if (itemFrame.Width > 36 || itemFrame.Height > 36)
                    itemScale = 36f / MathHelper.Max(itemFrame.Width, itemFrame.Height);

                itemScale *= inventoryScale * baseScale;
                Vector2 itemDrawPos = itemSlotDrawPosition + Vector2.One * 24f * baseScale;

                spriteBatch.Draw(itemTexture, itemDrawPos, itemFrame, CurrentlyHeldItem.GetAlpha(Color.White), 0f, itemFrame.Size() * 0.5f, itemScale, SpriteEffects.None, 0f);
                spriteBatch.Draw(itemTexture, itemDrawPos, itemFrame, CurrentlyHeldItem.GetColor(Color.White), 0f, itemFrame.Size() * 0.5f, itemScale, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(enchantIconTexture, enchantIconDrawPosition, null, Color.White, 0f, Vector2.Zero, enchantButtonScale, SpriteEffects.None, 0f);
        }

        private void DrawAndInteractWithButtons(SpriteBatch spriteBatch, IEnumerable<Enchantment> possibleEnchantments, Vector2 topButtonTopLeft, Vector2 bottomButtonTopLeft, Vector2 scale) {
            if (!possibleEnchantments.Any())
                return;

            Texture2D topArrowTexture = CalamitasCurseUI_ArrowUp.Value;
            Texture2D bottomArrowTexture = CalamitasCurseUI_ArrowDown.Value;

            if (TopButtonClickCountdown > 0f)
                topArrowTexture = CalamitasCurseUI_ArrowUpClicked.Value;
            if (BottomButtonClickCountdown > 0f)
                bottomArrowTexture = CalamitasCurseUI_ArrowDownClicked.Value;

            //增大箭头按钮缩放
            Vector2 arrowScale = scale * 1.5f;

            Rectangle topButtonArea = new Rectangle((int)topButtonTopLeft.X, (int)topButtonTopLeft.Y, (int)(topArrowTexture.Width * arrowScale.X), (int)(topArrowTexture.Height * arrowScale.Y));
            Rectangle bottomButtonArea = new Rectangle((int)bottomButtonTopLeft.X, (int)bottomButtonTopLeft.Y, (int)(bottomArrowTexture.Width * arrowScale.X), (int)(bottomArrowTexture.Height * arrowScale.Y));

            bool hoveringOverTopArrow = MouseHitBox.Intersects(topButtonArea);
            bool hoveringOverBottomArrow = MouseHitBox.Intersects(bottomButtonArea);

            if (hoveringOverTopArrow)
                topArrowTexture = CalamitasCurseUI_ArrowUpHovered.Value;
            if (hoveringOverBottomArrow)
                bottomArrowTexture = CalamitasCurseUI_ArrowDownHovered.Value;

            if (EnchantIndex > 0)
                spriteBatch.Draw(topArrowTexture, topButtonTopLeft, null, Color.White, 0f, Vector2.Zero, arrowScale, SpriteEffects.None, 0f);
            if (EnchantIndex < possibleEnchantments.Count() - 1)
                spriteBatch.Draw(bottomArrowTexture, bottomButtonTopLeft, null, Color.White, 0f, Vector2.Zero, arrowScale, SpriteEffects.None, 0f);

            if (Main.mouseLeft && Main.mouseLeftRelease && !IsEnchanting) {
                if (hoveringOverTopArrow && EnchantIndex > 0) {
                    EnchantIndex--;
                    TopButtonClickCountdown = 15f;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }

                if (hoveringOverBottomArrow && EnchantIndex < possibleEnchantments.Count() - 1) {
                    EnchantIndex++;
                    BottomButtonClickCountdown = 15f;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }
        }

        private void DrawEnchantmentName(SpriteBatch spriteBatch, Vector2 nameDrawCenter) {
            //增大附魔名称字体
            Vector2 scale = new Vector2(1.0f, 0.95f) * UIScale;
            string enchName = SelectedEnchantment.Value.Name.ToString();
            float textWidth = FontAssets.MouseText.Value.MeasureString(enchName).X * scale.X;
            Color drawColor = SelectedEnchantment.Value.Equals(EnchantmentManager.ClearEnchantment) ? Color.White : Color.Orange;
            nameDrawCenter.X -= textWidth * 0.5f;
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value, enchName, nameDrawCenter, drawColor, 0f, Vector2.Zero, scale);
        }

        private void DrawEnchantmentDescription(SpriteBatch spriteBatch, Point descriptionDrawPositionTopLeft) {
            Vector2 vectorDrawPosition = descriptionDrawPositionTopLeft.ToVector2();
            //增大描述文字大小
            Vector2 scale = new Vector2(0.95f, 0.95f) * MathHelper.Clamp(UIScale, 0.85f, 1f) * UIScale;

            string unifiedDescription = SelectedEnchantment.Value.Description.ToString().Replace("\n", " ");
            foreach (string line in Utils.WordwrapString(unifiedDescription, FontAssets.MouseText.Value, 400, 16, out _)) {
                if (string.IsNullOrEmpty(line))
                    continue;

                ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value, line, vectorDrawPosition, Color.Orange, 0f, Vector2.Zero, scale);
                //增加行间距
                vectorDrawPosition.Y += UIScale * 20f;
            }
        }

        private static void DrawIcon(SpriteBatch spriteBatch, Vector2 drawPositionTopLeft, Texture2D texture) {
            //增大图标缩放
            spriteBatch.Draw(texture, drawPositionTopLeft, null, Color.White, 0f, Vector2.Zero, UIScale * 1.3f, SpriteEffects.None, 0f);
        }

        #endregion

        #region 交互函数

        public static void DisableMouseWhenOverUI(Rectangle backgroundArea) {
            if (Instance.MouseHitBox.Intersects(backgroundArea)) {
                player.mouseInterface = false;
                Main.blockMouse = true;
            }
        }

        public static IEnumerable<Enchantment> SelectEnchantment() {
            //获取所有可用附魔
            IEnumerable<Enchantment> possibleEnchantments = EnchantmentManager.GetValidEnchantmentsForItem(CurrentlyHeldItem);

            SelectedEnchantment = null;
            if (possibleEnchantments.Any())
                SelectedEnchantment = possibleEnchantments.ElementAt(EnchantIndex);

            return possibleEnchantments;
        }

        public static void InteractWithItemSlot() {
            if (!CurrentlyHeldItem.IsAir) {
                Main.HoverItem = CurrentlyHeldItem.Clone();
                Main.instance.MouseTextHackZoom(string.Empty);
            }

            if (Main.mouseLeftRelease && Main.mouseLeft && !IsEnchanting) {
                EnchantIndex = 0;
                Utils.Swap(ref Main.mouseItem, ref CurrentlyHeldItem);
                SoundEngine.PlaySound(SoundID.Grab);
            }
        }

        public static void InteractWithEnchantIcon() {
            if (CurrentlyHeldItem.IsAir)
                return;

            if (!SelectedEnchantment.HasValue)
                return;

            //开始附魔等待
            IsEnchanting = true;
            EnchantProgress = 0f;

            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = -0.3f }, player.Center);
        }

        private static void CompleteEnchantment() {
            if (!SelectedEnchantment.HasValue || CurrentlyHeldItem.IsAir)
                return;

            int oldPrefix = CurrentlyHeldItem.prefix;
            CurrentlyHeldItem.SetDefaults(CurrentlyHeldItem.type);
            CurrentlyHeldItem.Prefix(oldPrefix);
            CurrentlyHeldItem = CurrentlyHeldItem.Clone();

            if (SelectedEnchantment.Value.Equals(EnchantmentManager.ClearEnchantment)) {
                CurrentlyHeldItem.Calamity().AppliedEnchantment = null;
            }
            else {
                CurrentlyHeldItem.Calamity().AppliedEnchantment = SelectedEnchantment.Value;
            }


            EnchantIndex = 0;
            IsEnchanting = false;
            EnchantProgress = 0f;

            //播放完成音效
            SoundStyle enchantSound = new("CalamityMod/Sounds/Custom/WeaponEnchant");
            SoundEngine.PlaySound(enchantSound with { Volume = 0.8f }, player.Center);
        }

        #endregion

        #region 粒子类

        private class EmberParticle(Vector2 start)
        {
            public Vector2 Pos = start;
            public float Size = Main.rand.NextFloat(2.5f, 5.5f);
            public float RiseSpeed = Main.rand.NextFloat(0.4f, 1.1f);
            public float Drift = Main.rand.NextFloat(-0.25f, 0.25f);
            public float Life = 0f;
            public float MaxLife = Main.rand.NextFloat(70f, 130f);
            public float Seed = Main.rand.NextFloat(10f);
            public float RotationSpeed = Main.rand.NextFloat(-0.05f, 0.05f);
            public float Rotation = Main.rand.NextFloat(MathHelper.TwoPi);

            public bool Update(Vector2 uiCenter, Vector2 uiSize) {
                Life++;
                float t = Life / MaxLife;
                Pos.Y -= RiseSpeed * (1f - t * 0.3f);
                Pos.X += (float)Math.Sin(Life * 0.06f + Seed) * Drift;
                Rotation += RotationSpeed;

                if (Life >= MaxLife || Pos.Y < uiCenter.Y - uiSize.Y / 2) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float scale = Size * (1f + (float)Math.Sin((Life + Seed * 20f) * 0.12f) * 0.15f);

                Color emberCore = Color.Lerp(new Color(255, 180, 80), new Color(255, 80, 40), t) * (alpha * 0.85f * fade);
                Color emberGlow = Color.Lerp(new Color(255, 140, 60), new Color(180, 40, 20), t) * (alpha * 0.5f * fade);

                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), emberGlow, 0f, new Vector2(0.5f, 0.5f), scale * 2.2f, SpriteEffects.None, 0f);
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), emberCore, Rotation, new Vector2(0.5f, 0.5f), scale, SpriteEffects.None, 0f);
            }
        }

        private class AshParticle(Vector2 start)
        {
            public Vector2 Pos = start;
            public float Size = Main.rand.NextFloat(1.5f, 3.5f);
            public float RiseSpeed = Main.rand.NextFloat(0.15f, 0.45f);
            public float Drift = Main.rand.NextFloat(-0.35f, 0.35f);
            public float Life = 0f;
            public float MaxLife = Main.rand.NextFloat(100f, 180f);
            public float Seed = Main.rand.NextFloat(10f);
            public float Rotation = Main.rand.NextFloat(MathHelper.TwoPi);

            public bool Update(Vector2 uiCenter, Vector2 uiSize) {
                Life++;
                float t = Life / MaxLife;
                Pos.Y -= RiseSpeed * (0.7f + (float)Math.Sin(t * Math.PI) * 0.3f);
                Pos.X += (float)Math.Sin(Life * 0.04f + Seed) * Drift * 1.5f;

                if (Life >= MaxLife || Pos.Y < uiCenter.Y - uiSize.Y / 2) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D px = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI) * (1f - t * 0.4f);

                Color ashColor = Color.Lerp(new Color(60, 50, 45), new Color(30, 20, 15), t) * (alpha * 0.65f * fade);
                sb.Draw(px, Pos, new Rectangle(0, 0, 1, 1), ashColor, Rotation, new Vector2(0.5f, 0.5f), Size, SpriteEffects.None, 0f);
            }
        }

        private class FlameWisp(Vector2 start)
        {
            public Vector2 Pos = start;
            public Vector2 Velocity = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(0.3f, 0.8f);
            public float Size = Main.rand.NextFloat(8f, 16f);
            public float Life = 0f;
            public float MaxLife = Main.rand.NextFloat(120f, 200f);
            public float Seed = Main.rand.NextFloat(10f);
            public float Phase = Main.rand.NextFloat(MathHelper.TwoPi);

            public bool Update(Vector2 uiCenter, Vector2 uiSize) {
                Life++;

                Phase += 0.08f;
                Vector2 drift = new Vector2(
                    (float)Math.Sin(Phase + Seed) * 0.5f,
                    (float)Math.Cos(Phase * 1.3f + Seed * 1.5f) * 0.3f
                );
                Pos += Velocity + drift;

                if (Pos.X < uiCenter.X - uiSize.X / 2 + 20f || Pos.X > uiCenter.X + uiSize.X / 2 - 20f) {
                    Velocity.X *= -0.8f;
                }
                if (Pos.Y < uiCenter.Y - uiSize.Y / 2 + 40f || Pos.Y > uiCenter.Y + uiSize.Y / 2 - 40f) {
                    Velocity.Y *= -0.8f;
                }

                if (Life >= MaxLife) {
                    return true;
                }
                return false;
            }

            public void Draw(SpriteBatch sb, float alpha) {
                Texture2D pixel = VaultAsset.placeholder2.Value;
                float t = Life / MaxLife;
                float fade = (float)Math.Sin(t * Math.PI);
                float pulse = (float)Math.Sin(Life * 0.15f + Seed) * 0.5f + 0.5f;

                float scale = Size * (0.8f + pulse * 0.4f);

                Color wispCore = new Color(255, 200, 120) * (alpha * 0.6f * fade);
                Color wispGlow = new Color(255, 120, 60) * (alpha * 0.3f * fade);

                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), wispGlow, 0f, new Vector2(0.5f, 0.5f), scale * 3f, SpriteEffects.None, 0f);
                sb.Draw(pixel, Pos, new Rectangle(0, 0, 1, 1), wispCore, 0f, new Vector2(0.5f, 0.5f), scale * 1.2f, SpriteEffects.None, 0f);
            }
        }

        #endregion
    }
}