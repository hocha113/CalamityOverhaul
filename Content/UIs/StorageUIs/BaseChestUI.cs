using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.StorageUIs
{
    /// <summary>箱子UI基类；淡出中(UIAlpha>0)仍 Active</summary>
    internal abstract class BaseChestUI : UIHandle, ILocalizedModType, IChestStorage
    {
        public override bool Active => IsOpen || Animation.UIAlpha > 0f;

        public abstract string LocalizationCategory { get; }

        public abstract int PanelWidth { get; }
        public abstract int PanelHeight { get; }
        public abstract int SlotsPerRow { get; }
        public abstract int SlotRows { get; }
        public int TotalSlots => SlotsPerRow * SlotRows;

        protected abstract BaseChestAnimation Animation { get; }
        protected abstract IChestEffects Effects { get; }
        protected ChestInteraction Interaction { get; private set; }
        protected abstract BaseChestRenderer Renderer { get; }

        public abstract int UsedSlotCount { get; }
        public abstract Item GetItem(int slot);
        public abstract void SetItem(int slot, Item item);

        /// <summary>关联机器/箱是否仍有效</summary>
        protected abstract bool ValidateSource();

        /// <summary>关闭回调，接 <see cref="UIHandle.Close"/></summary>
        protected abstract override void OnClose();

        protected abstract SoundStyle GetCloseSound();

        //关闭音效与 ESC 交基类
        public override SoundStyle? CloseSound => GetCloseSound();
        public override bool CloseOnEscape => true;

        protected abstract Vector2 GetStorageStartOffset();

        /// <summary>主题动画，基础动画之后</summary>
        protected virtual void UpdateThemeAnimations() {
            Animation.UpdateThemeEffects();
        }

        /// <summary>首次打开时 Init 交互</summary>
        protected void InitInteraction() {
            Interaction = new ChestInteraction(player, this);
        }

        public override void Update() {
            Animation.UpdateUIAnimation(IsOpen);

            if (Animation.UIAlpha <= 0f) {
                CleanupEffects();
                return;
            }

            if (!ValidateSource()) {
                Close();
                return;
            }

            UpdateThemeAnimations();

            Vector2 panelPosition = Renderer.CalculatePanelPosition();

            Effects.UpdateParticles(IsOpen, panelPosition, PanelWidth, PanelHeight);

            if (IsOpen && Animation.PanelSlideProgress > 0.9f) {
                UpdateInteraction(panelPosition);
            }

            Animation.UpdateSlotHoverAnimations(Interaction.HoveredSlot);
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

                if (Interaction.UpdateCloseButton(MousePosition.ToPoint(), panelPosition, PanelWidth,
                    keyLeftPressState == KeyPressState.Pressed)) {
                    Close();
                    return;
                }

                Vector2 storageStartPos = panelPosition + GetStorageStartOffset();
                Interaction.UpdateSlotInteraction(
                    MousePosition.ToPoint(),
                    storageStartPos,
                    keyLeftPressState == KeyPressState.Pressed,
                    keyLeftPressState == KeyPressState.Held || Main.mouseLeft,
                    keyRightPressState == KeyPressState.Pressed,
                    keyRightPressState == KeyPressState.Held || Main.mouseRight
                );
            }
            else if (keyLeftPressState == KeyPressState.Pressed && Animation.UIAlpha >= 1f && !player.mouseInterface) {
                Close();
            }
        }

        private void CleanupEffects() {
            Effects.Clear();
            Interaction?.Reset();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (Animation.UIAlpha <= 0f || Renderer == null) return;

            Vector2 panelPosition = Renderer.CalculatePanelPosition();
            Renderer.Draw(spriteBatch, panelPosition, Effects);
        }
    }
}
