using CalamityMod;
using CalamityMod.UI.CalamitasEnchants;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows.Enchants
{
    /// <summary>
    /// 炼铸系统核心逻辑处理器
    /// 负责管理附魔流程、验证、应用等核心功能
    /// </summary>
    [CWRJITEnabled]
    internal class EnchantmentHandler
    {
        /// <summary>
        /// 当前待炼铸的物品
        /// </summary>
        public Item CurrentItem { get; set; }

        /// <summary>
        /// 当前选择的附魔索引
        /// </summary>
        public int SelectedEnchantmentIndex { get; set; }

        /// <summary>
        /// 当前选择的附魔
        /// </summary>
        public Enchantment? SelectedEnchantment { get; private set; }

        /// <summary>
        /// 是否正在进行炼铸
        /// </summary>
        public bool IsEnchanting { get; private set; }

        /// <summary>
        /// 炼铸进度 (0-1)
        /// </summary>
        public float EnchantProgress { get; private set; }

        /// <summary>
        /// 炼铸所需时间（帧数）
        /// </summary>
        public float EnchantDuration { get; set; } = 180f;

        /// <summary>
        /// 炼铸完成时的回调
        /// </summary>
        public event Action<Item, Enchantment> OnEnchantComplete;

        /// <summary>
        /// 炼铸开始时的回调
        /// </summary>
        public event Action<Item, Enchantment> OnEnchantStart;

        /// <summary>
        /// 炼铸进度更新时的回调
        /// </summary>
        public event Action<float> OnProgressUpdate;

        public EnchantmentHandler() {
            CurrentItem = new Item();
            SelectedEnchantmentIndex = 0;
        }

        /// <summary>
        /// 更新炼铸逻辑，每帧调用
        /// </summary>
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

        /// <summary>
        /// 获取当前物品可用的附魔列表
        /// </summary>
        /// <returns>可用的附魔集合</returns>
        public IEnumerable<Enchantment> GetAvailableEnchantments() {
            if (CurrentItem == null || CurrentItem.IsAir) {
                return [];
            }
            IEnumerable<Enchantment> validEnchantments = EnchantmentManager.GetValidEnchantmentsForItem(CurrentItem);
            return validEnchantments;
        }

        /// <summary>
        /// 更新当前选择的附魔
        /// </summary>
        public void UpdateSelectedEnchantment() {
            IEnumerable<Enchantment> availableEnchantments = GetAvailableEnchantments();

            if (!availableEnchantments.Any()) {
                SelectedEnchantment = null;
                return;
            }

            //确保索引在有效范围内
            if (SelectedEnchantmentIndex < 0) {
                SelectedEnchantmentIndex = 0;
            }
            else if (SelectedEnchantmentIndex >= availableEnchantments.Count()) {
                SelectedEnchantmentIndex = availableEnchantments.Count() - 1;
            }

            SelectedEnchantment = availableEnchantments.ElementAt(SelectedEnchantmentIndex);
        }

        /// <summary>
        /// 选择上一个附魔
        /// </summary>
        /// <returns>是否成功切换</returns>
        public bool SelectPreviousEnchantment() {
            if (IsEnchanting) {
                return false;
            }

            IEnumerable<Enchantment> enchantments = GetAvailableEnchantments();
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

        /// <summary>
        /// 选择下一个附魔
        /// </summary>
        /// <returns>是否成功切换</returns>
        public bool SelectNextEnchantment() {
            if (IsEnchanting) {
                return false;
            }

            IEnumerable<Enchantment> enchantments = GetAvailableEnchantments();
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

        /// <summary>
        /// 开始炼铸流程
        /// </summary>
        /// <param name="player">发起炼铸的玩家</param>
        /// <returns>是否成功开始炼铸</returns>
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

        /// <summary>
        /// 取消当前炼铸
        /// </summary>
        public void CancelEnchanting() {
            if (!IsEnchanting) {
                return;
            }

            IsEnchanting = false;
            EnchantProgress = 0f;
        }

        /// <summary>
        /// 完成炼铸流程
        /// </summary>
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
                SoundStyle enchantSound = new("CalamityMod/Sounds/Custom/WeaponEnchant");
                SoundEngine.PlaySound(enchantSound with { Volume = 0.8f }, player.Center);
            }

            //触发完成回调
            OnEnchantComplete?.Invoke(CurrentItem, SelectedEnchantment.Value);
        }

        /// <summary>
        /// 验证是否可以对物品应用附魔
        /// </summary>
        /// <param name="item">目标物品</param>
        /// <param name="enchantment">目标附魔</param>
        /// <returns>是否可以附魔</returns>
        public bool CanEnchant(Item item, Enchantment enchantment) {
            if (item == null || item.IsAir) {
                return false;
            }

            //获取可用附魔列表并检查目标附魔是否在其中
            IEnumerable<Enchantment> validEnchantments = EnchantmentManager.GetValidEnchantmentsForItem(item);
            return validEnchantments.Contains(enchantment);
        }

        /// <summary>
        /// 应用附魔到物品
        /// </summary>
        /// <param name="item">目标物品</param>
        /// <param name="enchantment">要应用的附魔</param>
        public void ApplyEnchantment(Item item, Enchantment enchantment) {
            if (item == null || item.IsAir) {
                return;
            }

            //保存物品的词缀
            int oldPrefix = item.prefix;

            //重置物品
            item.SetDefaults(item.type);
            item.Prefix(oldPrefix);

            //应用或清除附魔
            if (enchantment.Equals(EnchantmentManager.ClearEnchantment)) {
                item.Calamity().AppliedEnchantment = null;
                item.Prefix(oldPrefix);
            }
            else {
                item.Calamity().AppliedEnchantment = enchantment;
                enchantment.CreationEffect?.Invoke(item);

                if (EnchantmentManager.ItemUpgradeRelationship.TryGetValue(item.type, out var newID)) {
                    //重置为升级后的物品
                    item.SetDefaults(newID);
                    item.Prefix(oldPrefix);
                }
            }
        }

        /// <summary>
        /// 重置处理器状态
        /// </summary>
        public void Reset() {
            CurrentItem = new Item();
            SelectedEnchantmentIndex = 0;
            SelectedEnchantment = null;
            IsEnchanting = false;
            EnchantProgress = 0f;
        }

        /// <summary>
        /// 设置当前物品并重置选择状态
        /// </summary>
        /// <param name="item">新的目标物品</param>
        public void SetCurrentItem(Item item) {
            if (IsEnchanting) {
                return;
            }

            CurrentItem = item ?? new Item();
            SelectedEnchantmentIndex = 0;
            UpdateSelectedEnchantment();
        }

        /// <summary>
        /// 交换当前物品（用于物品槽交互）
        /// </summary>
        /// <param name="otherItem">要交换的物品</param>
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
