using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.StorageUIs;
using InnoVault.UIHandles;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.OldDuchests.OldDuchestUIs
{
    /// <summary>老箱子UI</summary>
    internal class OldDuchestUI : BaseChestUI
    {
        public static OldDuchestUI Instance => UIHandleLoader.GetUIHandleOfType<OldDuchestUI>();

        public override string LocalizationCategory => "UI";
        public static LocalizedText TitleText;
        public static LocalizedText StorageText;

        public override int PanelWidth => 760;
        public override int PanelHeight => 560;
        public override int SlotsPerRow => 20;
        public override int SlotRows => 12;

        public OldDuchestTP CurrentChest { get; private set; }
        private Point16 chestPosition;

        private readonly List<Item> items = new();

        private readonly OldDuchestAnimation _animation = new();
        private readonly OldDuchestEffects _effects = new();
        private OldDuchestRenderer _renderer;

        protected override BaseChestAnimation Animation => _animation;
        protected override IChestEffects Effects => _effects;
        protected override BaseChestRenderer Renderer => _renderer;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "老箱子");
            StorageText = this.GetLocalization(nameof(StorageText), () => "储物空间");

            for (int i = 0; i < TotalSlots; i++) {
                items.Add(new Item());
            }
        }


        public override int UsedSlotCount {
            get {
                int count = 0;
                for (int i = 0; i < items.Count; i++) {
                    if (items[i] != null && !items[i].IsAir) count++;
                }
                return count;
            }
        }

        public override Item GetItem(int slot) {
            if (slot < 0 || slot >= items.Count) return new Item();
            return items[slot];
        }

        public override void SetItem(int slot, Item item) {
            if (slot < 0 || slot >= items.Count) return;
            items[slot] = item?.Clone() ?? new Item();
        }


        public void Interactive(OldDuchestTP chest) {
            if (CurrentChest != chest) {
                //换箱先关旧
                CurrentChest?.CloseUI(player);

                CurrentChest = chest;
                chestPosition = chest.Position;
                Open();

                if (Interaction == null || _renderer == null) {
                    InitInteraction();
                    _renderer = new OldDuchestRenderer(player, this, _animation, Interaction);
                }

                LoadItems(chest.storedItems);
                chest.OpenUI(player);
                SoundEngine.PlaySound(CWRSound.OldDuchestOpen with { Volume = 0.4f, Pitch = chest.isUnderwater ? -0.4f : 0 });
            }
            else {
                if (IsOpen) {
                    Close();
                }
                else {
                    Open();
                    chest.OpenUI(player);
                    SoundEngine.PlaySound(CWRSound.OldDuchestOpen with { Volume = 0.4f, Pitch = chest.isUnderwater ? -0.4f : 0 });
                }
            }
        }

        public void LoadItems(List<Item> storedItems) {
            for (int i = 0; i < TotalSlots; i++) {
                items[i] = i < storedItems.Count ? storedItems[i].Clone() : new Item();
            }
        }

        public List<Item> GetStoredItems() {
            List<Item> result = new();
            for (int i = 0; i < items.Count; i++) {
                if (items[i] != null && !items[i].IsAir) {
                    result.Add(items[i].Clone());
                }
            }
            return result;
        }


        protected override bool ValidateSource() {
            if (CurrentChest == null) return false;
            if (!InnoVault.TileProcessors.TileProcessorLoader.ByPositionGetTP(chestPosition, out OldDuchestTP chest))
                return false;
            return chest == CurrentChest && chest.Active;
        }

        protected override void OnClose() {
            CurrentChest?.CloseUI(player);
        }

        protected override SoundStyle GetCloseSound() {
            return CWRSound.OldDuchestClose with {
                Volume = 0.6f,
                Pitch = CurrentChest?.isUnderwater == true ? -0.4f : 0
            };
        }

        protected override Vector2 GetStorageStartOffset() => new Vector2(20, 90);
    }
}
