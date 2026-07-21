using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow.Enchants
{
    /// <summary>Ebn炼铸，选择/进度/应用</summary>
    internal class EnchantmentHandler
    {
        public Item CurrentItem { get; set; }

        public int SelectedEnchantmentIndex { get; set; }

        public CWRRef.EnchantmentWrapper? SelectedEnchantment { get; private set; }

        public bool IsEnchanting { get; private set; }

        /// <summary>炼铸进度 0~1</summary>
        public float EnchantProgress { get; private set; }

        /// <summary>炼铸时长，帧</summary>
        public float EnchantDuration { get; set; } = 180f;

        public event Action<Item, CWRRef.EnchantmentWrapper> OnEnchantComplete;

        public event Action<Item, CWRRef.EnchantmentWrapper> OnEnchantStart;

        public event Action<float> OnProgressUpdate;

        public EnchantmentHandler() {
            CurrentItem = new Item();
            SelectedEnchantmentIndex = 0;
        }

        public void Update() {
            if (!IsEnchanting) {
                return;
            }

            EnchantProgress += 1f;
            OnProgressUpdate?.Invoke(EnchantProgress / EnchantDuration);

            if (EnchantProgress >= EnchantDuration) {
                CompleteEnchantment();
            }
        }

        public IEnumerable<CWRRef.EnchantmentWrapper> GetAvailableEnchantments() {
            if (CurrentItem == null || CurrentItem.IsAir) {
                return [];
            }
            IEnumerable<CWRRef.EnchantmentWrapper> validEnchantments = CWRRef.GetValidEnchantmentsForItem(CurrentItem);
            return validEnchantments;
        }

        public void UpdateSelectedEnchantment() {
            IEnumerable<CWRRef.EnchantmentWrapper> availableEnchantments = GetAvailableEnchantments();

            if (!availableEnchantments.Any()) {
                SelectedEnchantment = null;
                return;
            }

            //索引clamp
            if (SelectedEnchantmentIndex < 0) {
                SelectedEnchantmentIndex = 0;
            }
            else if (SelectedEnchantmentIndex >= availableEnchantments.Count()) {
                SelectedEnchantmentIndex = availableEnchantments.Count() - 1;
            }

            SelectedEnchantment = availableEnchantments.ElementAt(SelectedEnchantmentIndex);
        }

        public bool SelectPreviousEnchantment() {
            if (IsEnchanting) {
                return false;
            }

            IEnumerable<CWRRef.EnchantmentWrapper> enchantments = GetAvailableEnchantments();
            if (!enchantments.Any()) {
                return false;
            }

            if (SelectedEnchantmentIndex > 0) {
                SelectedEnchantmentIndex--;
                UpdateSelectedEnchantment();
                return true;
            }

            return false;
        }

        public bool SelectNextEnchantment() {
            if (IsEnchanting) {
                return false;
            }

            IEnumerable<CWRRef.EnchantmentWrapper> enchantments = GetAvailableEnchantments();
            if (!enchantments.Any()) {
                return false;
            }

            if (SelectedEnchantmentIndex < enchantments.Count() - 1) {
                SelectedEnchantmentIndex++;
                UpdateSelectedEnchantment();
                return true;
            }

            return false;
        }

        public bool StartEnchanting(Player player) {
            if (IsEnchanting) {
                return false;
            }

            if (CurrentItem == null || CurrentItem.IsAir) {
                return false;
            }

            if (!SelectedEnchantment.HasValue) {
                return false;
            }

            if (!CanEnchant(CurrentItem, SelectedEnchantment.Value)) {
                return false;
            }

            IsEnchanting = true;
            EnchantProgress = 0f;

            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = -0.3f }, player.Center);

            OnEnchantStart?.Invoke(CurrentItem, SelectedEnchantment.Value);

            return true;
        }

        public void CancelEnchanting() {
            if (!IsEnchanting) {
                return;
            }

            IsEnchanting = false;
            EnchantProgress = 0f;
        }

        private void CompleteEnchantment() {
            if (!SelectedEnchantment.HasValue || CurrentItem == null || CurrentItem.IsAir) {
                IsEnchanting = false;
                EnchantProgress = 0f;
                return;
            }

            ApplyEnchantment(CurrentItem, SelectedEnchantment.Value);

            IsEnchanting = false;
            EnchantProgress = 0f;
            SelectedEnchantmentIndex = 0;

            Player player = Main.LocalPlayer;
            if (player != null) {
                SoundStyle enchantSound = "CalamityMod/Sounds/Custom/WeaponEnchant".GetSound();
                SoundEngine.PlaySound(enchantSound with { Volume = 0.8f }, player.Center);
            }

            OnEnchantComplete?.Invoke(CurrentItem, SelectedEnchantment.Value);
        }

        public bool CanEnchant(Item item, CWRRef.EnchantmentWrapper enchantment) {
            if (item == null || item.IsAir) {
                return false;
            }

            IEnumerable<CWRRef.EnchantmentWrapper> validEnchantments = CWRRef.GetValidEnchantmentsForItem(item);
            return validEnchantments.Contains(enchantment);
        }

        public void ApplyEnchantment(Item item, CWRRef.EnchantmentWrapper enchantment) {
            if (item == null || item.IsAir) {
                return;
            }

            CWRRef.ApplyEnchantmentToItem(item, enchantment);
        }

        public void Reset() {
            CurrentItem = new Item();
            SelectedEnchantmentIndex = 0;
            SelectedEnchantment = null;
            IsEnchanting = false;
            EnchantProgress = 0f;
        }

        public void SetCurrentItem(Item item) {
            if (IsEnchanting) {
                return;
            }

            CurrentItem = item ?? new Item();
            SelectedEnchantmentIndex = 0;
            UpdateSelectedEnchantment();
        }

        public void SwapItem(ref Item otherItem) {
            if (IsEnchanting) {
                return;
            }

            (otherItem, CurrentItem) = (CurrentItem, otherItem);
            SelectedEnchantmentIndex = 0;
            UpdateSelectedEnchantment();
        }
    }
}
