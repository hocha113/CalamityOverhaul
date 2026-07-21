using CalamityOverhaul.Content.Narrative.Presentation.Skins.Sulfsea;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.OldDukeShops
{
    /// <summary>老公爵商店UI</summary>
    internal class OldDukeShopUI : UIHandle, ILocalizedModType
    {
        public static OldDukeShopUI Instance => UIHandleLoader.GetUIHandleOfType<OldDukeShopUI>();

        private bool _active;
        public override bool Active {
            get => _active || animation.UIAlpha > 0f;
            set => _active = value;
        }

        public string LocalizationCategory => "UI";
        public static LocalizedText TitleText;
        public static LocalizedText CurrencyName;
        public static LocalizedText HintTooltip;

        //UI尺寸
        private const int PanelWidth = 580;
        private const int PanelHeight = 720;

        //商店数据
        private readonly List<OldDukeShopItem> shopItems = new();

        private readonly OldDukeShopAnimation animation = new();
        private readonly SulfseaPanelState sulfseaState = new();
        private OldDukeShopInteraction interaction;
        private OldDukeShopRenderer renderer;
        private bool escWasDown;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "老公爵的店铺");
            CurrencyName = this.GetLocalization(nameof(CurrencyName), () => "海洋残片");
            HintTooltip = this.GetLocalization(nameof(HintTooltip), () => "滚动/拖动条");
        }

        public override void Update() {
            animation.UpdateUIAnimation(_active);

            if (animation.UIAlpha <= 0f) {
                CleanupEffects();
                return;
            }

            //延迟初始化，等shopItems
            if (interaction == null) {
                interaction = new OldDukeShopInteraction(player, shopItems, animation);
                renderer = new OldDukeShopRenderer(player, shopItems, animation, interaction);
            }

            Vector2 panelPosition = renderer.CalculatePanelPosition();
            Rectangle panelRect = new((int)panelPosition.X, (int)panelPosition.Y, PanelWidth, PanelHeight);
            sulfseaState.Update(panelRect, _active);

            if (_active && animation.PanelSlideProgress > 0.82f) {
                UpdateInteraction(panelPosition);
            }

            animation.UpdateSlotHoverAnimations(interaction.HoveredIndex, interaction.ScrollOffset);
        }

        private void UpdateInteraction(Vector2 panelPosition) {
            UIHitBox = new Rectangle(
                (int)panelPosition.X,
                (int)panelPosition.Y,
                PanelWidth,
                PanelHeight
            );

            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                player.mouseInterface = true;
                UIInputGuard.SuppressWeaponSwitch();

                //优先检测关闭按钮
                if (interaction.UpdateCloseButton(MousePosition.ToPoint(), panelPosition, keyLeftPressState == KeyPressState.Pressed)) {
                    _active = false;
                    return;
                }

                if (keyLeftPressState != KeyPressState.None) {
                    interaction.UpdateScrollBar(panelPosition, MousePosition.ToPoint(),
                        Main.mouseLeft, Main.mouseLeftRelease);
                }

                //滚轮滚动（滚动条未拖动时才响应）
                if (!interaction.IsScrollBarDragging) {
                    interaction.HandleScroll();
                }

                //检测物品点击和悬停（滚动条未拖动时才响应）
                if (!interaction.IsScrollBarDragging) {
                    Vector2 itemListPos = panelPosition + new Vector2(35, 140);
                    interaction.UpdateItemSelection(MousePosition.ToPoint(), itemListPos, PanelWidth, keyLeftPressState);
                }
            }
            else if (keyLeftPressState == KeyPressState.Pressed && animation.UIAlpha >= 1f && !player.mouseInterface) {
                _active = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.3f });
            }

            bool escDown = Main.keyState.IsKeyDown(Keys.Escape);
            if (escDown && !escWasDown) {
                _active = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.3f });
            }
            escWasDown = escDown;
        }

        private void CleanupEffects() {
            sulfseaState.Reset();
            animation.Reset();
            escWasDown = false;
            interaction?.Reset();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (animation.UIAlpha <= 0f || renderer == null) return;

            Vector2 panelPosition = renderer.CalculatePanelPosition();
            renderer.Draw(spriteBatch, panelPosition, sulfseaState);
        }

        public void InitializeShop() {
            shopItems.Clear();
            OldDukeShopHandle.Handle(shopItems);
        }
    }
}
