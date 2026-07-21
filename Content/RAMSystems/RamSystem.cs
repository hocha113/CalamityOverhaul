using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using Terraria;

namespace CalamityOverhaul.Content.RAMSystems
{
    /// <summary>永久基础 + 动态修饰器</summary>
    internal class RamSystem : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => UnloadReset();

        private static RAMPlayer Local {
            get {
                if (Main.LocalPlayer == null || !Main.LocalPlayer.active) return null;
                return Main.LocalPlayer.GetModPlayer<RAMPlayer>();
            }
        }

        #region 默认值与边界

        public const int DefaultBaseMaxRam = 8;
        public const float DefaultBaseRecoveryRate = 0.1f;
        public const int MinBaseMaxRam = 1;
        /// <summary>基础上限软顶，HUD 弧条联动</summary>
        public const int SoftMaxBaseMaxRam = 64;
        public const int MaxCapacityUpgradeChips = 42;
        public const int MaxRecoveryUpgradeChips = 30;
        /// <summary>单枚上限芯片+基值</summary>
        public const int CapacityUpgradeChipBonus = 1;
        /// <summary>单枚恢复芯片+基值/秒</summary>
        public const float RecoveryUpgradeChipBonus = 0.05f;
        /// <summary>基础恢复上限，不含运行时</summary>
        public const float MaxBaseRecoveryRate = DefaultBaseRecoveryRate
            + MaxRecoveryUpgradeChips * RecoveryUpgradeChipBonus;
        /// <summary>消耗后恢复延迟(秒)</summary>
        public const float RecoveryDelay = 1.5f;
        /// <summary>不足闪烁帧数</summary>
        public const int InsufficientFlashFrames = 30;
        //tModLoader 固定每秒 60 tick
        private const float TickSeconds = 1f / 60f;

        //锁定计时(帧)，>0 则 RAM=0
        private static int lockTimer;
        private static int lockTotalFrames;
        private static float lockTimerCarry;
        //不足闪烁计时(帧)
        private static int flashTimer;
        private static float flashTimerCarry;

        #endregion

        #region 锁定与故障反馈

        public static bool IsLocked => lockTimer > 0;

        public static int LockRemain => lockTimer;

        /// <summary>锁定总帧，HUD 进度</summary>
        public static int LockTotal => lockTotalFrames;

        /// <summary>锁定剩余比，HUD 填充</summary>
        public static float LockRemainRatio {
            get {
                if (lockTimer <= 0 || lockTotalFrames <= 0) {
                    return 0f;
                }
                return MathHelper.Clamp(lockTimer / (float)lockTotalFrames, 0f, 1f);
            }
        }

        public static bool IsFlashing => flashTimer > 0;

        /// <summary>HUD 警告 0..1，锁定恒1</summary>
        public static float GetWarningPulse() {
            if (lockTimer > 0) {
                return 1f;
            }
            if (flashTimer > 0) {
                float k = flashTimer / (float)InsufficientFlashFrames;
                return MathHelper.Clamp(k, 0f, 1f);
            }
            return 0f;
        }

        /// <summary>榨干并锁定指定帧</summary>
        public static void SystemLock(int frames) {
            if (frames <= 0) {
                return;
            }
            lockTimer = frames;
            lockTotalFrames = frames;
            lockTimerCarry = 0f;
            var local = Local;
            if (local == null) {
                return;
            }
            local.CurrentRam = 0f;
            local.RecoveryCooldown = 0f;
            local.InvokeOnDepleted();
        }

        public static void NotifyInsufficient() {
            flashTimer = InsufficientFlashFrames;
            flashTimerCarry = 0f;
        }

        /// <summary>立即解锁定，读档/卸载用</summary>
        public static void ClearLock() {
            lockTimer = 0;
            lockTotalFrames = 0;
            lockTimerCarry = 0f;
            flashTimer = 0;
            flashTimerCarry = 0f;
        }

        #endregion

        #region 永久基础值（委托至 RAMPlayer 实例）

        public static int UsedCapacityUpgradeChips => Local?.UsedCapacityUpgradeChips ?? 0;
        public static int UsedRecoveryUpgradeChips => Local?.UsedRecoveryUpgradeChips ?? 0;

        public static int BaseMaxRam {
            get => Local?.BaseMaxRam ?? DefaultBaseMaxRam;
            set { var l = Local; if (l != null) l.BaseMaxRam = value; }
        }

        public static float BaseRecoveryRate {
            get => Local?.BaseRecoveryRate ?? DefaultBaseRecoveryRate;
            set { var l = Local; if (l != null) l.BaseRecoveryRate = value; }
        }

        #endregion

        #region 生效值（委托至 RAMPlayer 实例）

        public static int MaxRam => Local?.MaxRam ?? DefaultBaseMaxRam;
        public static float RecoveryRate => Local?.RecoveryRate ?? DefaultBaseRecoveryRate;
        public static float RecoveryRateRatio => MaxBaseRecoveryRate > 0f
            ? MathHelper.Clamp(RecoveryRate / MaxBaseRecoveryRate, 0f, 1f)
            : 0f;

        public static float CurrentRam {
            get => Local?.CurrentRam ?? 0f;
            set { var l = Local; if (l != null) l.CurrentRam = value; }
        }

        public static int DisplayCurrent => Local?.DisplayCurrent ?? 0;
        public static float Ratio => Local?.Ratio ?? 0f;

        #endregion

        #region 动态修饰器注册表

        public static void RegisterProvider(IRamModifierProvider provider) {
            if (provider == null) {
                return;
            }
            var local = Local;
            if (local == null) {
                return;
            }
            if (!local.Providers.Contains(provider)) {
                local.Providers.Add(provider);
            }
        }

        public static void UnregisterProvider(IRamModifierProvider provider) {
            if (provider == null) {
                return;
            }
            Local?.Providers.Remove(provider);
        }

        /// <summary>修饰器数，调试/UI</summary>
        public static int ProviderCount => Local?.Providers.Count ?? 0;

        #endregion

        #region 永久升级 API

        public static void IncreaseBaseMaxRamBy(int delta) => BaseMaxRam = BaseMaxRam + delta;
        public static void IncreaseBaseRecoveryRateBy(float delta) => BaseRecoveryRate = BaseRecoveryRate + delta;
        public static bool CanUseCapacityUpgradeChip => UsedCapacityUpgradeChips < MaxCapacityUpgradeChips;
        public static bool CanUseRecoveryUpgradeChip => UsedRecoveryUpgradeChips < MaxRecoveryUpgradeChips;

        public static bool TryUseCapacityUpgradeChip() {
            if (!CanUseCapacityUpgradeChip) {
                return false;
            }
            var local = Local;
            if (local == null) {
                return false;
            }
            local.UsedCapacityUpgradeChips++;
            local.BaseMaxRam = DefaultBaseMaxRam + local.UsedCapacityUpgradeChips * CapacityUpgradeChipBonus;
            local.RecomputeEffective();
            Restore(CapacityUpgradeChipBonus);
            return true;
        }

        public static bool TryUseRecoveryUpgradeChip() {
            if (!CanUseRecoveryUpgradeChip) {
                return false;
            }
            var local = Local;
            if (local == null) {
                return false;
            }
            local.UsedRecoveryUpgradeChips++;
            local.BaseRecoveryRate = DefaultBaseRecoveryRate + local.UsedRecoveryUpgradeChips * RecoveryUpgradeChipBonus;
            local.RecomputeEffective();
            return true;
        }

        #endregion

        #region 消耗与恢复

        public static bool CanAfford(int cost) {
            if (HackTime.InfiniteHack) {
                return true;
            }
            if (lockTimer > 0) {
                return false;
            }
            var local = Local;
            if (local == null) {
                return false;
            }
            return local.CurrentRam >= cost;
        }

        public static bool TryConsume(int cost) {
            if (HackTime.InfiniteHack) {
                return true;
            }
            if (lockTimer > 0) {
                return false;
            }
            var local = Local;
            if (local == null) {
                return false;
            }
            if (local.CurrentRam < cost) {
                return false;
            }
            float prev = local.CurrentRam;
            local.CurrentRam -= cost;
            if (local.CurrentRam < 0f) {
                local.CurrentRam = 0f;
            }
            local.RecoveryCooldown = RecoveryDelay;
            if (prev > 0f && local.CurrentRam <= 0f) {
                local.InvokeOnDepleted();
            }
            return true;
        }

        public static void ConsumeOverTime(float ramPerSecond) {
            if (HackTime.InfiniteHack) {
                return;
            }
            if (ramPerSecond <= 0f) {
                return;
            }
            if (lockTimer > 0) {
                return;
            }
            var local = Local;
            if (local == null) {
                return;
            }
            float prev = local.CurrentRam;
            local.CurrentRam -= ramPerSecond * TickSeconds;
            if (local.CurrentRam < 0f) {
                local.CurrentRam = 0f;
            }
            if (prev > 0f && local.CurrentRam <= 0f) {
                local.InvokeOnDepleted();
            }
        }

        public static void Restore(float amount) {
            if (amount <= 0f) {
                return;
            }
            var local = Local;
            if (local == null) {
                return;
            }
            local.CurrentRam = Math.Min(local.CurrentRam + amount, local.MaxRam);
        }

        public static void Refill() => Local?.Refill();

        #endregion

        #region 每帧更新

        public static void Update() {
            var local = Local;
            if (local == null) {
                return;
            }
            local.RecomputeEffective();

            TimeGear.ConsumeFrames(ref flashTimer, ref flashTimerCarry);

            //锁定中 RAM=0，不恢复
            if (lockTimer > 0) {
                TimeGear.ConsumeFrames(ref lockTimer, ref lockTimerCarry);
                local.CurrentRam = 0f;
                local.RecoveryCooldown = 0f;
                if (lockTimer == 0) {
                    lockTotalFrames = 0;
                    lockTimerCarry = 0f;
                }
                return;
            }

            if (HackTime.Active) {
                return;
            }
            if (local.RecoveryCooldown > 0f) {
                local.RecoveryCooldown -= TickSeconds * TimeGear.TimeScale;
                return;
            }
            if (local.CurrentRam < local.MaxRam && TimeGear.TimeScale > 0f) {
                local.CurrentRam += local.RecoveryRate * TickSeconds * TimeGear.TimeScale;
                if (local.CurrentRam > local.MaxRam) {
                    local.CurrentRam = local.MaxRam;
                }
            }
        }

        #endregion

        #region 重置

        public static void Reset() {
            lockTimer = 0;
            lockTotalFrames = 0;
            lockTimerCarry = 0f;
            flashTimer = 0;
            flashTimerCarry = 0f;
            if (!Main.LocalPlayer.active) return;
            var local = Local;
            local.RecoveryCooldown = 0f;
            local.RecomputeEffective();
            local.CurrentRam = local.MaxRam;
        }

        public static void UnloadReset() {
        }

        #endregion
    }
}
