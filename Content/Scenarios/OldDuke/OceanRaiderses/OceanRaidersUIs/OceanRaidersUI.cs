using CalamityOverhaul.Common;
using CalamityOverhaul.Content.UIs.StorageUIs;
using InnoVault.UIHandles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.OceanRaiderses.OceanRaidersUIs
{
    /// <summary>海洋吞噬者箱子UI</summary>
    internal class OceanRaidersUI : BaseChestUI
    {
        public static OceanRaidersUI Instance => UIHandleLoader.GetUIHandleOfType<OceanRaidersUI>();

        public override string LocalizationCategory => "UI";
        public static LocalizedText TitleText;
        public static LocalizedText StorageText;

        public override int PanelWidth => 760;
        public override int PanelHeight => 780;
        public override int SlotsPerRow => 20;
        public override int SlotRows => 18;

        private OceanRaidersTP currentMachine;

        private readonly OceanRaidersAnimation _animation = new();
        private readonly OceanRaidersEffects _effects = new();
        private OceanRaidersRenderer _renderer;

        protected override BaseChestAnimation Animation => _animation;
        protected override IChestEffects Effects => _effects;
        protected override BaseChestRenderer Renderer => _renderer;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "海洋吞噬者存储");
            StorageText = this.GetLocalization(nameof(StorageText), () => "存储空间");
        }


        public override int UsedSlotCount {
            get {
                if (currentMachine?.storedItems == null) return 0;
                int count = 0;
                foreach (var item in currentMachine.storedItems) {
                    if (item != null && !item.IsAir) count++;
                }
                return count;
            }
        }

        public override Item GetItem(int slot) {
            if (currentMachine == null || slot < 0 || slot >= currentMachine.storedItems.Count)
                return new Item();
            return currentMachine.storedItems[slot];
        }

        public override void SetItem(int slot, Item item) {
            if (currentMachine == null) return;

            while (currentMachine.storedItems.Count <= slot) {
                currentMachine.storedItems.Add(new Item());
            }

            currentMachine.storedItems[slot] = item;
            CleanEmptySlots();
            currentMachine.SendData();
        }

        private void CleanEmptySlots() {
            for (int i = currentMachine.storedItems.Count - 1; i >= 0; i--) {
                Item item = currentMachine.storedItems[i];
                if (item == null || item.type <= ItemID.None || item.stack <= 0) {
                    currentMachine.storedItems.RemoveAt(i);
                }
                else {
                    break;
                }
            }
        }

        public void Interactive(OceanRaidersTP machine) {
            if (machine == null) return;

            if (currentMachine != machine) {
                currentMachine = machine;

                if (Interaction == null || _renderer == null) {
                    InitInteraction();
                    _renderer = new OceanRaidersRenderer(player, this, machine, _animation, Interaction);
                }
                else {
                    _renderer.UpdateMachine(machine);
                }

                Open();
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.2f });
            }
            else if (IsOpen) {
                //关箱音效走基类
                Close();
            }
            else {
                Open();
                SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.2f });
            }
        }


        protected override bool ValidateSource() {
            return currentMachine != null
                && currentMachine.Active
                && currentMachine.CenterInWorld.To(player.Center).Length() <= 220;
        }

        protected override void OnClose() {
            //OceanRaiders没有CloseUI回调
        }

        protected override SoundStyle GetCloseSound() {
            return SoundID.MenuClose with { Pitch = -0.3f };
        }

        protected override Vector2 GetStorageStartOffset() => new Vector2(20, 90);
    }
}
