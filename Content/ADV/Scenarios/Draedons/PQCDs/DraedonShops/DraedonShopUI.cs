using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.Draedons.PQCDs.DraedonShops
{
    /// <summary>
    /// 嘉登商店UI
    /// </summary>
    internal class DraedonShopUI : UIHandle
    {
        public static DraedonShopUI Instance => UIHandleLoader.GetUIHandleOfType<DraedonShopUI>();

        //UI状态
        private bool _active;
        public override bool Active {
            get => _active || animation.UIAlpha > 0f;
            set => _active = value;
        }

        //UI尺寸
        private const int PanelWidth = 680;
        private const int PanelHeight = 640;

        //商店数据
        private readonly List<ShopItem> shopItems = new();

        //组件
        private readonly DraedonShopAnimation animation = new();
        private readonly DraedonShopEffects effects = new();
        private DraedonShopInteraction interaction;
        private DraedonShopRenderer renderer;

        public override void Update() {
            //更新动画进度
            animation.UpdateUIAnimation(_active);

            if (animation.UIAlpha <= 0f) {
                CleanupEffects();
                return;
            }

            //初始化商店
            InitializeShop();

            //初始化组件（延迟初始化，确保shopItems已填充）
            if (interaction == null) {
                interaction = new DraedonShopInteraction(player, shopItems);
                renderer = new DraedonShopRenderer(player, shopItems, animation, interaction);
            }

            DraedonCallUI.Instance.Active = animation.UIAlpha >= 0f;

            //更新科技动画
            animation.UpdateTechEffects();

            //计算面板位置
            Vector2 panelPosition = renderer.CalculatePanelPosition();

            //更新粒子和特效
            effects.UpdateParticles(_active, panelPosition, PanelWidth, PanelHeight);

            //更新UI交互
            if (_active && animation.PanelSlideProgress > 0.9f) {
                UpdateInteraction(panelPosition);
            }

            //更新槽位悬停动画
            animation.UpdateSlotHoverAnimations(interaction.HoveredIndex);
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
                player.CWR().DontSwitchWeaponTime = 2;

                if (keyLeftPressState != KeyPressState.None) {
                    //更新滚动条（优先处理）
                    interaction.UpdateScrollBar(panelPosition, MousePosition.ToPoint(),
                        Main.mouseLeft, Main.mouseLeftRelease);
                }

                //滚轮滚动（滚动条未拖动时才响应）
                if (!interaction.IsScrollBarDragging) {
                    interaction.HandleScroll();
                }

                //检测物品点击和悬停（滚动条未拖动时才响应）
                if (!interaction.IsScrollBarDragging) {
                    Vector2 itemListPos = panelPosition + new Vector2(30, 120);
                    interaction.UpdateItemSelection(MousePosition.ToPoint(), itemListPos, PanelWidth);
                }
            }
            else if (keyLeftPressState == KeyPressState.Pressed && animation.UIAlpha >= 1f && !DraedonCallUI.Instance.hoverInMainPage) {
                _active = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = 0.2f });
            }

            //ESC关闭
            if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)) {
                _active = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = 0.2f });
            }
        }

        private void CleanupEffects() {
            effects.Clear();
            interaction?.Reset();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (animation.UIAlpha <= 0f || renderer == null) return;

            Vector2 panelPosition = renderer.CalculatePanelPosition();
            renderer.Draw(spriteBatch, panelPosition, effects);
        }

        /// <summary>
        /// 初始化商店物品
        /// </summary>
        public void InitializeShop() {
            if (shopItems.Count > 0) return;
            //添加嘉登材料合成的物品
            ShopHandle.Handle(shopItems);
        }
    }
}
