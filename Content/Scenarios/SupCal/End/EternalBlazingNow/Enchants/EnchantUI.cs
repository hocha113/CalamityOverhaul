using CalamityOverhaul.Content.UIs.UIEffect;
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

namespace CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow.Enchants
{
    /// <summary>
    /// Ebn ���������ħ UI��������ȴ�����
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

        //UI���ֲ���
        public static Vector2 UITopLeft => Instance.DrawPosition;
        public static float UIScale => 0.8f;

        //չ��/����״̬
        public static bool IsCollapsed = false;
        public static float CollapseProgress = 0f;//0=չ�� 1=�۵�
        public static float CollapseAnimSpeed = 0.12f;
        public static float CollapsedWidth = 60f; //�۵���Ŀ���
        public static float CollapsedHeight = 80f; //�۵���ĸ߶�

        private readonly static EnchantmentHandler EnchantmentHandler = new();

        //��ť�����ȴ
        public static float TopButtonClickCountdown = 0f;
        public static float BottomButtonClickCountdown = 0f;
        public static float EnchantButtonClickCountdown = 0f;
        public static float ToggleButtonClickCountdown = 0f;

        //��ǻ��Ӿ�Ч������
        private float flameTimer = 0f;
        private float emberGlowTimer = 0f;
        private float heatWavePhase = 0f;
        private float infernoPulse = 0f;

        private float lerpProgress;

        //����ϵͳ
        private readonly List<EmberPRT> embers = new();
        private int emberSpawnTimer = 0;
        private readonly List<AshPRT> ashes = new();
        private int ashSpawnTimer = 0;
        private readonly List<FlameWispPRT> flameWisps = new();
        private int wispSpawnTimer = 0;

        public override bool Active => Main.playerInventory && player.chest == -1 && player.talkNPC == -1 && !Main.InGuideCraftMenu && EbnState.OnEbn(player);

        public string LocalizationCategory => "UI";

        public static LocalizedText ExpandHint;
        public static LocalizedText CollapseHint;
        public static LocalizedText EnchantTitle;

        public new void LoadUIData(TagCompound tag) {
            tag.TryGet(Name + ":" + nameof(DrawPosition), out DrawPosition);
            if (DrawPosition == Vector2.Zero || DrawPosition == default) {
                DrawPosition = new Vector2(168f, 320f);
            }

            tag.TryGet(Name + ":" + nameof(IsCollapsed), out IsCollapsed);
            if (tag.TryGet(Name + ":" + "CurrentlyHeldItem", out TagCompound itemTag)) {
                EnchantmentHandler.CurrentItem = ItemIO.Load(itemTag);
            }
            else {
                EnchantmentHandler.CurrentItem = new Item();
            }
        }

        public new void SaveUIData(TagCompound tag) {
            tag[Name + ":" + nameof(DrawPosition)] = DrawPosition;
            tag[Name + ":" + nameof(IsCollapsed)] = IsCollapsed;
            EnchantmentHandler.CurrentItem ??= new Item();
            tag[Name + ":" + "CurrentlyHeldItem"] = ItemIO.Save(EnchantmentHandler.CurrentItem);
        }

        public override void SetStaticDefaults() {
            ExpandHint = this.GetLocalization(nameof(ExpandHint), () => "չ����������");
            CollapseHint = this.GetLocalization(nameof(CollapseHint), () => "������������");
            EnchantTitle = this.GetLocalization(nameof(EnchantTitle), () => "����");

            //�����¼�
            EnchantmentHandler.OnEnchantStart += OnEnchantStart;
            EnchantmentHandler.OnEnchantComplete += OnEnchantComplete;
        }

        public override void Update() {
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

            if (DrawPosition == Vector2.Zero || DrawPosition == default) {
                DrawPosition = new Vector2(168f, 320f);
            }
            DrawPosition.X = MathHelper.Clamp(DrawPosition.X, 0, Main.screenWidth - CollapsedWidth);
            DrawPosition.Y = MathHelper.Clamp(DrawPosition.Y, 0, Main.screenHeight - CollapsedHeight);
        }

        public override void LogicUpdate() {
            //�����۵�״̬��������С
            lerpProgress = MathHelper.SmoothStep(0f, 1f, CollapseProgress);

            //�ݼ������ȴ
            if (TopButtonClickCountdown > 0f)
                TopButtonClickCountdown--;
            if (BottomButtonClickCountdown > 0f)
                BottomButtonClickCountdown--;
            if (EnchantButtonClickCountdown > 0f)
                EnchantButtonClickCountdown--;
            if (ToggleButtonClickCountdown > 0f)
                ToggleButtonClickCountdown--;

            //�����۵�����
            float targetProgress = IsCollapsed ? 1f : 0f;
            if (CollapseProgress < targetProgress) {
                CollapseProgress = Math.Min(1f, CollapseProgress + CollapseAnimSpeed);
            }
            else if (CollapseProgress > targetProgress) {
                CollapseProgress = Math.Max(0f, CollapseProgress - CollapseAnimSpeed);
            }

            //���»��涯����ʱ��
            flameTimer += 0.045f;
            emberGlowTimer += 0.038f;
            heatWavePhase += 0.025f;
            infernoPulse += 0.012f;

            if (flameTimer > MathHelper.TwoPi) flameTimer -= MathHelper.TwoPi;
            if (emberGlowTimer > MathHelper.TwoPi) emberGlowTimer -= MathHelper.TwoPi;
            if (heatWavePhase > MathHelper.TwoPi) heatWavePhase -= MathHelper.TwoPi;
            if (infernoPulse > MathHelper.TwoPi) infernoPulse -= MathHelper.TwoPi;

            //���������߼�
            EnchantmentHandler.Update();
            EnchantmentHandler.UpdateSelectedEnchantment();

            //��������
            UpdateParticles();
        }

        private void UpdateParticles() {
            //�۵�״̬�¼�������Ч��
            if (CollapseProgress > 0.5f)
                return;

            Vector2 uiCenter = UITopLeft + new Vector2(200f, 150f) * UIScale;
            Vector2 uiSize = new Vector2(400f, 300f) * UIScale;

            //�����������
            emberSpawnTimer++;
            if (emberSpawnTimer >= 8 && embers.Count < 35) {
                emberSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(uiCenter.X - uiSize.X / 2 + 30f, uiCenter.X + uiSize.X / 2 - 30f);
                Vector2 startPos = new(xPos, uiCenter.Y + uiSize.Y / 2);
                embers.Add(new EmberPRT(startPos));
            }

            for (int i = embers.Count - 1; i >= 0; i--) {
                if (embers[i].Update(uiCenter, uiSize)) {
                    embers.RemoveAt(i);
                }
            }

            //���ɻҽ�����
            ashSpawnTimer++;
            if (ashSpawnTimer >= 12 && ashes.Count < 25) {
                ashSpawnTimer = 0;
                float xPos = Main.rand.NextFloat(uiCenter.X - uiSize.X / 2 + 30f, uiCenter.X + uiSize.X / 2 - 30f);
                Vector2 startPos = new(xPos, uiCenter.Y + uiSize.Y / 2);
                ashes.Add(new AshPRT(startPos));
            }

            for (int i = ashes.Count - 1; i >= 0; i--) {
                if (ashes[i].Update(uiCenter, uiSize)) {
                    ashes.RemoveAt(i);
                }
            }

            //���ɻ��澫��
            wispSpawnTimer++;
            if (wispSpawnTimer >= 45 && flameWisps.Count < 8) {
                wispSpawnTimer = 0;
                Vector2 startPos = new(
                    Main.rand.NextFloat(uiCenter.X - uiSize.X / 2 + 40f, uiCenter.X + uiSize.X / 2 - 40f),
                    Main.rand.NextFloat(uiCenter.Y - uiSize.Y / 2 + 60f, uiCenter.Y + uiSize.Y / 2 - 60f)
                );
                flameWisps.Add(new FlameWispPRT(startPos));
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

            //������ǻ��񱳾�
            DrawBrimstoneBackground(spriteBatch, UIHitBox);

            //������꽻��
            DisableMouseWhenOverUI(UIHitBox);

            //����չ��/����ť
            DrawToggleButton(spriteBatch, UIHitBox);

            //��������۵������۵���ֻ��ʾ������
            if (CollapseProgress > 0.01f) {
                DrawCollapsedContent(spriteBatch, UIHitBox, lerpProgress);
                return;
            }

            //��ȡ���ø�ħ
            IEnumerable<CWRRef.EnchantmentWrapper> possibleEnchantments = EnchantmentHandler.GetAvailableEnchantments();

            //��Ʒ��λ��
            Vector2 itemSlotDrawPosition = UITopLeft + new Vector2(36f, 46f) * backgroundScale;
            //��ħ��ťλ��
            Vector2 enchantIconDrawPosition = UITopLeft + new Vector2(52f, 126f) * backgroundScale;

            DrawItemIcon(spriteBatch, itemSlotDrawPosition, enchantIconDrawPosition, backgroundScale, out bool isHoveringOverItemIcon, out bool isHoveringOverEnchantIcon);

            if (isHoveringOverItemIcon)
                InteractWithItemSlot();

            //������ťλ��
            Vector2 topButtonPos = UITopLeft + new Vector2(240f, 42f) * backgroundScale;
            Vector2 bottomButtonPos = UITopLeft + new Vector2(240f, 110f) * backgroundScale;
            DrawAndInteractWithButtons(spriteBatch, possibleEnchantments, topButtonPos, bottomButtonPos, backgroundScale);

            //���Ƹ�ħ��Ϣ
            if (EnchantmentHandler.SelectedEnchantment.HasValue) {
                //���Ƹ�ħ����
                DrawEnchantmentName(spriteBatch, UITopLeft + new Vector2(300f, 70f) * backgroundScale);

                //���Ƹ�ħ����
                Point descriptionDrawPositionTopLeft = (UITopLeft + new Vector2(40f, 180f) * backgroundScale).ToPoint();
                DrawEnchantmentDescription(spriteBatch, descriptionDrawPositionTopLeft);

                //���Ƹ�ħͼ��
                if (!string.IsNullOrEmpty(EnchantmentHandler.SelectedEnchantment.Value.IconTexturePath)) {
                    Vector2 iconDrawPositionTopLeft = UITopLeft + new Vector2(226f, 56f) * UIScale;
                    Texture2D iconTexture = CWRUtils.GetT2DAsset(EnchantmentHandler.SelectedEnchantment.Value.IconTexturePath).Value;
                    DrawIcon(spriteBatch, iconDrawPositionTopLeft, iconTexture);
                }
            }

            //��ħ��ť
            if (isHoveringOverEnchantIcon && !EnchantmentHandler.IsEnchanting) {
                if (Main.mouseLeft && Main.mouseLeftRelease) {
                    InteractWithEnchantIcon();
                    EnchantButtonClickCountdown = 15f;
                }
            }

            //���Ƹ�ħ����
            if (EnchantmentHandler.IsEnchanting) {
                DrawEnchantProgress(spriteBatch, UIHitBox);
            }
        }

        #region ���ƺ���

        private void DrawToggleButton(SpriteBatch spriteBatch, Rectangle panelRect) {
            //��ťλ����������Ͻ�
            int buttonSize = 24;
            Rectangle buttonRect = new Rectangle(
                panelRect.Right - buttonSize - 8,
                panelRect.Y + 8,
                buttonSize,
                buttonSize
            );

            bool isHovering = MouseHitBox.Intersects(buttonRect);

            //���ư�ť����
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color buttonBg = isHovering ? new Color(255, 140, 70) * 0.6f : new Color(180, 60, 30) * 0.5f;

            if (ToggleButtonClickCountdown > 0f) {
                buttonBg = new Color(255, 180, 100) * 0.7f;
            }

            spriteBatch.Draw(pixel, buttonRect, buttonBg);

            //���ư�ť�߿�
            int borderWidth = 2;
            Color borderColor = new Color(255, 200, 120) * 0.8f;
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, buttonRect.Width, borderWidth), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Bottom - borderWidth, buttonRect.Width, borderWidth), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.X, buttonRect.Y, borderWidth, buttonRect.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(buttonRect.Right - borderWidth, buttonRect.Y, borderWidth, buttonRect.Height), borderColor);

            //���Ƽ�ͷͼ��
            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string arrowText = IsCollapsed ? "?" : "?";
            Vector2 textSize = font.MeasureString(arrowText);
            Vector2 textPos = buttonRect.Center.ToVector2() - textSize / 2;

            Utils.DrawBorderString(spriteBatch, arrowText, textPos, Color.White, 1f);

            //�۵���ť
            if (isHovering && Main.mouseLeft && Main.mouseLeftRelease && ToggleButtonClickCountdown <= 0f) {
                IsCollapsed = !IsCollapsed;
                ToggleButtonClickCountdown = 15f;
                SoundEngine.PlaySound(SoundID.MenuTick);
            }

            //��ʾ��ͣ��ʾ
            if (isHovering) {
                Main.instance.MouseText(IsCollapsed ? ExpandHint.Value : CollapseHint.Value);
            }
        }

        private void DrawCollapsedContent(SpriteBatch spriteBatch, Rectangle panelRect, float lerpProgress) {
            //�۵�״̬����ʾ�򻯵Ļ���Ч������ʾ����
            float alpha = 1f - lerpProgress * 0.5f;

            //���Ƽ�����
            foreach (var ember in embers.Take(5)) {
                ember.Draw(spriteBatch, alpha * 0.5f);
            }

            //��ʾ����
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

            //��ӰЧ��
            Rectangle shadow = panelRect;
            shadow.Offset(7, 9);
            spriteBatch.Draw(pixel, shadow, new Rectangle(0, 0, 1, 1), new Color(20, 0, 0) * 0.65f);

            //���䱳��
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

            //����������Ӳ�
            float pulseBrightness = (float)Math.Sin(infernoPulse * 1.8f) * 0.5f + 0.5f;
            Color pulseOverlay = new Color(120, 25, 15) * (0.25f * pulseBrightness);
            spriteBatch.Draw(pixel, panelRect, new Rectangle(0, 0, 1, 1), pulseOverlay);

            //�ڷ���
            float glowPulse = (float)Math.Sin(emberGlowTimer * 1.5f) * 0.5f + 0.5f;
            Rectangle inner = panelRect;
            inner.Inflate(-7, -7);
            spriteBatch.Draw(pixel, inner, new Rectangle(0, 0, 1, 1), new Color(180, 60, 30) * (0.12f * (0.5f + glowPulse * 0.5f)));

            //���ƻ���߿�
            DrawBrimstoneFrame(spriteBatch, panelRect, glowPulse);

            //ֻ��չ��״̬������������
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
            //���ƽ�����
            float progress = EnchantmentHandler.EnchantProgress / EnchantmentHandler.EnchantDuration;
            Rectangle progressBarBg = new Rectangle(
                panelRect.X + 50,
                panelRect.Bottom - 40,
                panelRect.Width - 100,
                20
            );

            //����������
            spriteBatch.Draw(VaultAsset.placeholder2.Value, progressBarBg, new Color(30, 10, 5) * 0.8f);

            //���������
            Rectangle progressBarFill = progressBarBg;
            progressBarFill.Width = (int)((int)(progressBarFill.Width * progress) * 5.25f);

            //���ƻ��潥�������
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

            //���ƽ����ı�
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

            //������Ʒ�ۺͰ�ť������
            Vector2 itemSlotScale = scale * 1.5f;
            Vector2 enchantButtonScale = scale * 1.5f;

            Rectangle enchantIconArea = new Rectangle(
                (int)enchantIconDrawPosition.X,
                (int)enchantIconDrawPosition.Y,
                (int)(enchantIconTexture.Width * enchantButtonScale.X),
                (int)(enchantIconTexture.Height * enchantButtonScale.Y)
            );

            //��������ͣ
            if (MouseHitBox.Intersects(enchantIconArea) && !EnchantmentHandler.IsEnchanting) {
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

            //������Ʒ
            if (!EnchantmentHandler.CurrentItem.IsAir) {
                float inventoryScale = Main.inventoryScale;
                Texture2D itemTexture = TextureAssets.Item[EnchantmentHandler.CurrentItem.type].Value;
                Rectangle itemFrame = itemTexture.Frame(1, 1, 0, 0);
                bool hasMultipleFrames = Main.itemAnimations[EnchantmentHandler.CurrentItem.type] != null;
                if (hasMultipleFrames)
                    itemFrame = Main.itemAnimations[EnchantmentHandler.CurrentItem.type].GetFrame(itemTexture);

                float baseScale = UIScale * 1.5f; //������Ʒ��ʾ

                float itemScale = 1f;
                if (itemFrame.Width > 36 || itemFrame.Height > 36)
                    itemScale = 36f / MathHelper.Max(itemFrame.Width, itemFrame.Height);

                itemScale *= inventoryScale * baseScale;
                Vector2 itemDrawPos = itemSlotDrawPosition + Vector2.One * 24f * baseScale;

                spriteBatch.Draw(itemTexture, itemDrawPos, itemFrame, EnchantmentHandler.CurrentItem.GetAlpha(Color.White), 0f, itemFrame.Size() * 0.5f, itemScale, SpriteEffects.None, 0f);
                spriteBatch.Draw(itemTexture, itemDrawPos, itemFrame, EnchantmentHandler.CurrentItem.GetColor(Color.White), 0f, itemFrame.Size() * 0.5f, itemScale, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(enchantIconTexture, enchantIconDrawPosition, null, Color.White, 0f, Vector2.Zero, enchantButtonScale, SpriteEffects.None, 0f);
        }

        private void DrawAndInteractWithButtons(SpriteBatch spriteBatch, IEnumerable<CWRRef.EnchantmentWrapper> possibleEnchantments, Vector2 topButtonTopLeft, Vector2 bottomButtonTopLeft, Vector2 scale) {
            if (!possibleEnchantments.Any())
                return;

            Texture2D topArrowTexture = CalamitasCurseUI_ArrowUp.Value;
            Texture2D bottomArrowTexture = CalamitasCurseUI_ArrowDown.Value;

            if (TopButtonClickCountdown > 0f)
                topArrowTexture = CalamitasCurseUI_ArrowUpClicked.Value;
            if (BottomButtonClickCountdown > 0f)
                bottomArrowTexture = CalamitasCurseUI_ArrowDownClicked.Value;

            //�����ͷ��ť����
            Vector2 arrowScale = scale * 1.5f;

            Rectangle topButtonArea = new Rectangle((int)topButtonTopLeft.X, (int)topButtonTopLeft.Y, (int)(topArrowTexture.Width * arrowScale.X), (int)(topArrowTexture.Height * arrowScale.Y));
            Rectangle bottomButtonArea = new Rectangle((int)bottomButtonTopLeft.X, (int)bottomButtonTopLeft.Y, (int)(bottomArrowTexture.Width * arrowScale.X), (int)(bottomArrowTexture.Height * arrowScale.Y));

            bool hoveringOverTopArrow = MouseHitBox.Intersects(topButtonArea);
            bool hoveringOverBottomArrow = MouseHitBox.Intersects(bottomButtonArea);

            if (hoveringOverTopArrow)
                topArrowTexture = CalamitasCurseUI_ArrowUpHovered.Value;
            if (hoveringOverBottomArrow)
                bottomArrowTexture = CalamitasCurseUI_ArrowDownHovered.Value;

            if (EnchantmentHandler.SelectedEnchantmentIndex > 0)
                spriteBatch.Draw(topArrowTexture, topButtonTopLeft, null, Color.White, 0f, Vector2.Zero, arrowScale, SpriteEffects.None, 0f);
            if (EnchantmentHandler.SelectedEnchantmentIndex < possibleEnchantments.Count() - 1)
                spriteBatch.Draw(bottomArrowTexture, bottomButtonTopLeft, null, Color.White, 0f, Vector2.Zero, arrowScale, SpriteEffects.None, 0f);

            if (Main.mouseLeft && Main.mouseLeftRelease && !EnchantmentHandler.IsEnchanting) {
                if (hoveringOverTopArrow && EnchantmentHandler.SelectPreviousEnchantment()) {
                    TopButtonClickCountdown = 15f;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }

                if (hoveringOverBottomArrow && EnchantmentHandler.SelectNextEnchantment()) {
                    BottomButtonClickCountdown = 15f;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }
        }

        private void DrawEnchantmentName(SpriteBatch spriteBatch, Vector2 nameDrawCenter) {
            if (!EnchantmentHandler.SelectedEnchantment.HasValue)
                return;

            //����ħ��������
            Vector2 scale = new Vector2(1.0f, 0.95f) * UIScale;
            string enchName = EnchantmentHandler.SelectedEnchantment.Value.Name.ToString();
            float textWidth = FontAssets.MouseText.Value.MeasureString(enchName).X * scale.X;
            Color drawColor = EnchantmentHandler.SelectedEnchantment.Value.IsClearEnchantment ? Color.White : Color.Orange;
            nameDrawCenter.X -= textWidth * 0.5f;
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value, enchName, nameDrawCenter, drawColor, 0f, Vector2.Zero, scale);
        }

        private void DrawEnchantmentDescription(SpriteBatch spriteBatch, Point descriptionDrawPositionTopLeft) {
            if (!EnchantmentHandler.SelectedEnchantment.HasValue)
                return;

            Vector2 vectorDrawPosition = descriptionDrawPositionTopLeft.ToVector2();
            //�����������ִ�С
            Vector2 scale = new Vector2(0.95f, 0.95f) * MathHelper.Clamp(UIScale, 0.85f, 1f) * UIScale;

            string unifiedDescription = EnchantmentHandler.SelectedEnchantment.Value.Description.ToString().Replace("\n", " ");
            foreach (string line in CWRUtils.WrapTextArray(unifiedDescription, FontAssets.MouseText.Value, 400, 16, out _)) {
                if (string.IsNullOrEmpty(line))
                    continue;

                ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value, line, vectorDrawPosition, Color.Orange, 0f, Vector2.Zero, scale);
                //�����м��
                vectorDrawPosition.Y += UIScale * 20f;
            }
        }

        private static void DrawIcon(SpriteBatch spriteBatch, Vector2 drawPositionTopLeft, Texture2D texture) {
            //����ͼ������
            spriteBatch.Draw(texture, drawPositionTopLeft, null, Color.White, 0f, Vector2.Zero, UIScale * 1.3f, SpriteEffects.None, 0f);
        }

        #endregion

        #region ��������

        public static void DisableMouseWhenOverUI(Rectangle backgroundArea) {
            if (Instance.MouseHitBox.Intersects(backgroundArea)) {
                player.mouseInterface = false;
                Main.blockMouse = true;
            }
        }

        public static void InteractWithItemSlot() {
            if (!EnchantmentHandler.CurrentItem.IsAir) {
                Main.HoverItem = EnchantmentHandler.CurrentItem.Clone();
                Main.instance.MouseTextHackZoom(string.Empty);
            }

            if (Main.mouseLeftRelease && Main.mouseLeft && !EnchantmentHandler.IsEnchanting) {
                EnchantmentHandler.SwapItem(ref Main.mouseItem);
                SoundEngine.PlaySound(SoundID.Grab);
            }
        }

        public static void InteractWithEnchantIcon() {
            if (EnchantmentHandler.CurrentItem.IsAir)
                return;

            if (!EnchantmentHandler.SelectedEnchantment.HasValue)
                return;

            //��ʼ����
            EnchantmentHandler.StartEnchanting(player);
        }

        private static void OnEnchantStart(Item item, CWRRef.EnchantmentWrapper enchantment) {
            //������ʼʱ�Ķ����߼�
            //����ʱ�벻����ʲô��Ҫ����
        }

        private static void OnEnchantComplete(Item item, CWRRef.EnchantmentWrapper enchantment) {
            //�������ʱ�Ķ����߼�
            //����ʱ�벻����ʲô��Ҫ����
        }

        #endregion
    }
}