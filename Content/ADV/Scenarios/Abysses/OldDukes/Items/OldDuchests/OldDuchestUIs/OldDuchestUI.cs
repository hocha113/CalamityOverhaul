using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OldDuchests.OldDuchestUIs
{
    /// <summary>
    /// 老箱子UI
    /// </summary>
    internal class OldDuchestUI : UIHandle, ILocalizedModType
    {
        public static OldDuchestUI Instance => UIHandleLoader.GetUIHandleOfType<OldDuchestUI>();

        //UI状态
        private bool _active;
        public override bool Active {
            get => _active || animation.UIAlpha > 0f;
            set => _active = value;
        }

        public string LocalizationCategory => "UI";
        public static LocalizedText TitleText;
        public static LocalizedText StorageText;

        //UI尺寸 20x12格储物空间
        private const int PanelWidth = 760;
        private const int PanelHeight = 520;
        private const int SlotsPerRow = 20;
        private const int SlotRows = 12;
        private const int TotalSlots = SlotsPerRow * SlotRows;

        //当前绑定的箱子
        public OldDuchestTP CurrentChest { get; private set; }
        private Point16 chestPosition;

        //存储物品
        private readonly List<Item> items = new();

        //组件
        private readonly OldDuchestAnimation animation = new();
        private readonly OldDuchestEffects effects = new();
        private OldDuchestInteraction interaction;
        private OldDuchestRenderer renderer;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "老箱子");
            StorageText = this.GetLocalization(nameof(StorageText), () => "储物空间");
            
            //初始化物品列表
            for (int i = 0; i < TotalSlots; i++) {
                items.Add(new Item());
            }
        }

        /// <summary>
        /// 打开UI并绑定箱子
        /// </summary>
        public void Open(Point16 position) {
            if (!InnoVault.TileProcessors.TileProcessorLoader.ByPositionGetTP(position, out OldDuchestTP chest)) {
                return;
            }

            chestPosition = position;
            CurrentChest = chest;
            _active = true;

            //初始化组件
            if (interaction == null || renderer == null) {
                interaction = new OldDuchestInteraction(player, this);
                renderer = new OldDuchestRenderer(player, this, animation, interaction);
            }

            //加载箱子数据
            LoadItems(chest.storedItems);

            //通知箱子打开
            chest.OpenUI(player);

            SoundEngine.PlaySound(SoundID.MenuOpen with { Pitch = -0.2f });
        }

        /// <summary>
        /// 加载物品数据
        /// </summary>
        public void LoadItems(List<Item> storedItems) {
            for (int i = 0; i < TotalSlots; i++) {
                if (i < storedItems.Count) {
                    items[i] = storedItems[i].Clone();
                }
                else {
                    items[i] = new Item();
                }
            }
        }

        /// <summary>
        /// 获取存储的物品
        /// </summary>
        public List<Item> GetStoredItems() {
            List<Item> result = new();
            for (int i = 0; i < items.Count; i++) {
                if (items[i] != null && !items[i].IsAir) {
                    result.Add(items[i].Clone());
                }
            }
            return result;
        }

        /// <summary>
        /// 获取指定槽位物品
        /// </summary>
        public Item GetItem(int slot) {
            if (slot < 0 || slot >= items.Count) {
                return new Item();
            }
            return items[slot];
        }

        /// <summary>
        /// 设置指定槽位物品
        /// </summary>
        public void SetItem(int slot, Item item) {
            if (slot < 0 || slot >= items.Count) {
                return;
            }
            items[slot] = item?.Clone() ?? new Item();
        }

        /// <summary>
        /// 关闭UI
        /// </summary>
        public void Close() {
            _active = false;
            
            if (CurrentChest != null) {
                CurrentChest.SaveItemsFromUI();
                CurrentChest.CloseUI(player);
            }

            SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.3f });
        }

        public override void Update() {
            //更新动画进度
            animation.UpdateUIAnimation(_active);

            if (animation.UIAlpha <= 0f) {
                CleanupEffects();
                return;
            }

            //检查箱子是否仍然有效
            if (!ValidateChest()) {
                _active = false;
                return;
            }

            //更新动画
            animation.UpdateEffects();

            //计算面板位置
            Vector2 panelPosition = renderer.CalculatePanelPosition();

            //更新粒子和特效
            effects.UpdateParticles(_active, panelPosition, PanelWidth, PanelHeight);

            //更新UI交互
            if (_active && animation.PanelSlideProgress > 0.9f) {
                UpdateInteraction(panelPosition);
            }

            //更新槽位悬停动画
            animation.UpdateSlotHoverAnimations(interaction.HoveredSlot);
        }

        private bool ValidateChest() {
            if (CurrentChest == null) {
                return false;
            }

            if (!InnoVault.TileProcessors.TileProcessorLoader.ByPositionGetTP(chestPosition, out OldDuchestTP chest)) {
                return false;
            }

            return chest == CurrentChest && chest.Active;
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

                //优先检测关闭按钮
                if (interaction.UpdateCloseButton(MousePosition.ToPoint(), panelPosition, 
                    keyLeftPressState == KeyPressState.Pressed)) {
                    Close();
                    return;
                }

                //处理槽位交互
                Vector2 storageStartPos = panelPosition + new Vector2(20, 90);
                interaction.UpdateSlotInteraction(MousePosition.ToPoint(), storageStartPos);
            }
            else if (keyLeftPressState == KeyPressState.Pressed && animation.UIAlpha >= 1f && !player.mouseInterface) {
                Close();
            }

            //ESC关闭
            if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)) {
                Close();
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
    }
}
