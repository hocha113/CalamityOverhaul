using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.SupCal.End.EternalBlazingNow.Enchants
{
    /// <summary>
    /// Ebn 炼铸逻辑：附魔选择、进度、应用
    /// </summary>
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

        /// <summary>每帧推进炼铸进度</summary>
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

            //索引 clamp
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

        /// <summary>开始炼铸</summary>
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

            //验证附魔有效性
            if (!CanEnchant(CurrentItem, SelectedEnchantment.Value)) {
                return false;
            }

            IsEnchanting = true;
            EnchantProgress = 0f;

            //播放开始音效
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = -0.3f }, player.Center);

            //触发开始回调
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

            //应用附魔
            ApplyEnchantment(CurrentItem, SelectedEnchantment.Value);

            //重置状态
            IsEnchanting = false;
            EnchantProgress = 0f;
            SelectedEnchantmentIndex = 0;

            //播放完成音效
            Player player = Main.LocalPlayer;
            if (player != null) {
                SoundStyle enchantSound = "CalamityMod/Sounds/Custom/WeaponEnchant".GetSound();
                SoundEngine.PlaySound(enchantSound with { Volume = 0.8f }, player.Center);
            }

            //触发完成回调
            OnEnchantComplete?.Invoke(CurrentItem, SelectedEnchantment.Value);
        }

        public bool CanEnchant(Item item, CWRRef.EnchantmentWrapper enchantment) {
            if (item == null || item.IsAir) {
                return false;
            }

            //获取可用附魔列表并检查目标附魔是否在其中
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
