using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.UIs.StorageUIs
{
    /// <summary>
    /// 通用箱子UI基类，封装所有箱子UI的通用逻辑：
    /// 动画驱动、交互、开关管理、绘制流程
    /// </summary>
    internal abstract class BaseChestUI : UIHandle, ILocalizedModType, IChestStorage
    {
        //UI开关状态由基类生命周期 IsOpen 驱动；淡出动画期间(UIAlpha>0)仍保持 Active 以播放收尾动画
        public override bool Active => IsOpen || Animation.UIAlpha > 0f;

        public abstract string LocalizationCategory { get; }

        //--- 尺寸配置 (子类实现) ---
        public abstract int PanelWidth { get; }
        public abstract int PanelHeight { get; }
        public abstract int SlotsPerRow { get; }
        public abstract int SlotRows { get; }
        public int TotalSlots => SlotsPerRow * SlotRows;

        //--- 组件 (子类提供具体实现) ---
        protected abstract BaseChestAnimation Animation { get; }
        protected abstract IChestEffects Effects { get; }
        protected ChestInteraction Interaction { get; private set; }
        protected abstract BaseChestRenderer Renderer { get; }

        //--- 存储接口 (子类实现) ---
        public abstract int UsedSlotCount { get; }
        public abstract Item GetItem(int slot);
        public abstract void SetItem(int slot, Item item);

        //--- 子类覆写的行为 ---

        /// <summary>验证关联的机器/箱子是否仍然有效</summary>
        protected abstract bool ValidateSource();

        /// <summary>当UI关闭时的回调，接入基类 <see cref="UIHandle.Close"/> 生命周期</summary>
        protected abstract override void OnClose();

        /// <summary>获取关闭音效</summary>
        protected abstract SoundStyle GetCloseSound();

        //接入基类生命周期：关闭音效与 ESC 关闭交由基类统一处理
        public override SoundStyle? CloseSound => GetCloseSound();
        public override bool CloseOnEscape => true;

        /// <summary>获取存储区域起始偏移</summary>
        protected abstract Vector2 GetStorageStartOffset();

        /// <summary>更新主题特有的动画效果（在基础动画之后调用）</summary>
        protected virtual void UpdateThemeAnimations() {
            Animation.UpdateThemeEffects();
        }

        /// <summary>
        /// 初始化交互组件，子类在首次打开时调用
        /// </summary>
        protected void InitInteraction() {
            Interaction = new ChestInteraction(player, this);
        }

        //--- 通用Update逻辑 ---

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

        //--- 通用Draw逻辑 ---

        public override void Draw(SpriteBatch spriteBatch) {
            if (Animation.UIAlpha <= 0f || Renderer == null) return;

            Vector2 panelPosition = Renderer.CalculatePanelPosition();
            Renderer.Draw(spriteBatch, panelPosition, Effects);
        }
    }
}
