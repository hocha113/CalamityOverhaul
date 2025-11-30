using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.SupertableUIs.Inventory;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.UIs.SupertableUIs.UIContent
{
    /// <summary>
    /// 快捷操作管理器 - 一键放置、一键取出、高亮显示等
    /// </summary>
    internal class QuickActionsManager
    {
        private readonly SupertableUI _mainUI;
        private readonly SupertableController _controller;

        private QuickPlaceButton _quickPlaceButton;
        private QuickTakeButton _quickTakeButton;
        private HighlightButton _highlightButton;

        public QuickActionsManager(SupertableUI mainUI, SupertableController controller) {
            _mainUI = mainUI;
            _controller = controller;
            _quickPlaceButton = new QuickPlaceButton(mainUI, controller);
            _quickTakeButton = new QuickTakeButton(mainUI, controller);
            _highlightButton = new HighlightButton(mainUI, controller);
        }

        public void Update() {
            _quickPlaceButton.Update();
            _quickTakeButton.Update();
            _highlightButton.Update();
        }

        public void Draw(SpriteBatch spriteBatch, float alpha) {
            _quickPlaceButton.Draw(spriteBatch, alpha);
            _quickTakeButton.Draw(spriteBatch, alpha);
            _highlightButton.Draw(spriteBatch, alpha);
        }
    }

    /// <summary>
    /// 一键放置按钮
    /// </summary>
    internal class QuickPlaceButton
    {
        private readonly SupertableUI _mainUI;
        private readonly SupertableController _controller;
        private Rectangle _hitbox;
        private bool _isHovered;
        private int _cooldown;
        private int _clickSpeedMultiplier = 30;

        public QuickPlaceButton(SupertableUI mainUI, SupertableController controller) {
            _mainUI = mainUI;
            _controller = controller;
        }

        public void Update() {
            Vector2 pos = _mainUI.DrawPosition + SupertableConstants.ORGANIZER_OFFSET;
            _hitbox = new Rectangle((int)pos.X, (int)pos.Y, 30, 30);
            _isHovered = _hitbox.Intersects(_mainUI.MouseHitBox);

            if (_cooldown > 0) _cooldown--;

            int mouseState = (int)_mainUI.keyLeftPressState;
            if (mouseState != 1 && mouseState != 3) {
                _clickSpeedMultiplier = 30;
            }

            if (_isHovered && (mouseState == 1 || mouseState == 3)) {
                if (_cooldown <= 0 || mouseState == 1) {
                    PlaceRecipe();
                    _cooldown = _clickSpeedMultiplier;
                    AdjustClickSpeed();
                }
                DragController.SetGlobalDontDragTime(2);
            }
        }

        private void PlaceRecipe() {
            if (ItemInteractionHandler.TryQuickPlaceRecipe(
                _controller.SlotManager.Slots,
                _controller.SlotManager.PreviewSlots,
                ref Main.mouseItem,
                UIHandle.player)) {
                _controller.UpdateRecipeMatching();
            }
        }

        private void AdjustClickSpeed() {
            if (_clickSpeedMultiplier == 30) {
                _clickSpeedMultiplier = 12;
            }
            else {
                _clickSpeedMultiplier = Math.Max(_clickSpeedMultiplier - 1, 1);
            }
        }

        public void Draw(SpriteBatch spriteBatch, float alpha) {
            Texture2D texture = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/OneClick");
            Color color = _isHovered ? Color.Gold : Color.White;
            spriteBatch.Draw(texture, _mainUI.DrawPosition + SupertableConstants.ORGANIZER_OFFSET, null, color * alpha, 0, Vector2.Zero, 1, SpriteEffects.None, 0);

            if (_isHovered) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value,
                    CWRLocText.GetTextValue("SupMUI_OneClick_Text1"),
                    _hitbox.X - 30, _hitbox.Y + 30,
                    Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.8f);
            }
        }
    }

    /// <summary>
    /// 一键取出按钮
    /// </summary>
    internal class QuickTakeButton
    {
        private readonly SupertableUI _mainUI;
        private readonly SupertableController _controller;
        private Rectangle _hitbox;
        private bool _isHovered;

        public QuickTakeButton(SupertableUI mainUI, SupertableController controller) {
            _mainUI = mainUI;
            _controller = controller;
        }

        public void Update() {
            Vector2 pos = _mainUI.DrawPosition + SupertableConstants.ORGANIZER_LEFT_OFFSET;
            _hitbox = new Rectangle((int)pos.X, (int)pos.Y, 30, 30);
            _isHovered = _hitbox.Intersects(_mainUI.MouseHitBox);

            if (_isHovered) {
                bool hasItems = _controller.SlotManager.HasAnyItems();
                if (hasItems && (_mainUI.keyLeftPressState == KeyPressState.Pressed || _mainUI.keyLeftPressState == KeyPressState.Held)) {
                    TakeAllItems();
                }
                DragController.SetGlobalDontDragTime(2);
            }
        }

        private void TakeAllItems() {
            SoundEngine.PlaySound(SoundID.Grab);

            foreach (var (index, item) in _controller.SlotManager.GetNonEmptySlots()) {
                Item itemClone = item.Clone();
                UIHandle.player.QuickSpawnItem(UIHandle.player.FromObjectGetParent(), itemClone, itemClone.stack);
                _controller.SlotManager.ClearSlot(index);
            }

            _controller.UpdateRecipeMatching();
        }

        public void Draw(SpriteBatch spriteBatch, float alpha) {
            Texture2D texture = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/TwoClick");
            Color color = _isHovered ? Color.Gold : Color.White;
            spriteBatch.Draw(texture, _mainUI.DrawPosition + SupertableConstants.ORGANIZER_LEFT_OFFSET, null, color * alpha, 0, Vector2.Zero, 1, SpriteEffects.None, 0);

            if (_isHovered) {
                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value,
                    CWRLocText.GetTextValue("SupMUI_OneClick_Text2"),
                    _hitbox.X - 30, _hitbox.Y + 30,
                    Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.8f);
            }
        }
    }

    /// <summary>
    /// 高亮显示按钮
    /// </summary>
    internal class HighlightButton
    {
        [VaultLoaden("CalamityOverhaul/Assets/UIs/SupertableUIs/Eye")]
        private static Asset<Texture2D> _eyeAsset = null;

        private readonly SupertableUI _mainUI;
        private readonly SupertableController _controller;
        private Rectangle _hitbox;
        private bool _isHovered;
        private bool _highlightEnabled;

        public HighlightButton(SupertableUI mainUI, SupertableController controller) {
            _mainUI = mainUI;
            _controller = controller;
        }

        public void Update() {
            Vector2 pos = _mainUI.DrawPosition + SupertableConstants.HIGHLIGHTER_OFFSET;
            _hitbox = new Rectangle((int)pos.X, (int)pos.Y, 30, 30);
            _isHovered = _hitbox.Intersects(_mainUI.MouseHitBox);

            if (_isHovered && _mainUI.keyLeftPressState == KeyPressState.Pressed) {
                _highlightEnabled = !_highlightEnabled;
                SoundEngine.PlaySound(SoundID.Unlock with { Pitch = _highlightEnabled ? 0.5f : -0.5f });
            }
        }

        public void Draw(SpriteBatch spriteBatch, float alpha) {
            if (_eyeAsset?.Value != null) {
                Rectangle sourceRect = _eyeAsset.Value.GetRectangle(_highlightEnabled ? 1 : 0, 2);
                spriteBatch.Draw(_eyeAsset.Value, _mainUI.DrawPosition + SupertableConstants.HIGHLIGHTER_OFFSET,
                    sourceRect, Color.White * alpha, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
            }

            if (_highlightEnabled) {
                DrawHighlights(spriteBatch, alpha);
            }

            if (_isHovered) {
                string text = _highlightEnabled
                    ? CWRLocText.GetTextValue("SupertableUI_Text4")
                    : CWRLocText.GetTextValue("SupertableUI_Text5");

                Utils.DrawBorderStringFourWay(spriteBatch, FontAssets.MouseText.Value, text,
                    _hitbox.X - 30, _hitbox.Y + 30,
                    Color.White * alpha, Color.Black * alpha, new Vector2(0.3f), 0.8f);
            }
        }

        private void DrawHighlights(SpriteBatch spriteBatch, float alpha) {
            Texture2D highlightTexture = CWRUtils.GetT2DValue("CalamityOverhaul/Assets/UIs/SupertableUIs/CallFull");

            for (int i = 0; i < SupertableConstants.TOTAL_SLOTS; i++) {
                Item slotItem = _controller.SlotManager.GetSlot(i);
                Item previewItem = _controller.SlotManager.GetPreviewSlot(i);

                if (slotItem?.type != ItemID.None && previewItem?.type != ItemID.None &&
                    slotItem.type != previewItem.type) {
                    Vector2 pos = _mainUI.ArcCellPos(i) + new Vector2(-1, 0);
                    spriteBatch.Draw(highlightTexture, pos, null, Color.White * 0.6f * alpha, 0, Vector2.Zero, 1, SpriteEffects.None, 0);
                }
            }
        }
    }
}
