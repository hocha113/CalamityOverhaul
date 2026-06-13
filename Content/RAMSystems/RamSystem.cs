using CalamityOverhaul.Content.HackTimes;
using System;
using Terraria;

namespace CalamityOverhaul.Content.RAMSystems
{
    /// <summary>CWR 全局 RAM 资源系统，永久基础值加动态修饰器双层架构</summary>
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

        /// <summary>默认基础 RAM 上限</summary>
        public const int DefaultBaseMaxRam = 8;
        /// <summary>默认基础每秒恢复量</summary>
        public const float DefaultBaseRecoveryRate = 0.1f;
        /// <summary>基础上限最小值</summary>
        public const int MinBaseMaxRam = 1;
        /// <summary>基础上限软上限，与 HUD 弧条最大跨度联动</summary>
        public const int SoftMaxBaseMaxRam = 64;
        /// <summary>RAM 上限芯片最多可用次数</summary>
        public const int MaxCapacityUpgradeChips = 42;
        /// <summary>RAM 恢复芯片最多可用次数</summary>
        public const int MaxRecoveryUpgradeChips = 30;
        /// <summary>单枚上限芯片提供基础上限</summary>
        public const int CapacityUpgradeChipBonus = 1;
        /// <summary>单枚恢复芯片提供基础每秒恢复量</summary>
        public const float RecoveryUpgradeChipBonus = 0.05f;
        /// <summary>基础恢复速度上限，不含运行时加成</summary>
        public const float MaxBaseRecoveryRate = DefaultBaseRecoveryRate
            + MaxRecoveryUpgradeChips * RecoveryUpgradeChipBonus;
        /// <summary>消耗后到开始恢复的延迟（秒）</summary>
        public const float RecoveryDelay = 1.5f;
        /// <summary>RAM 不足闪烁持续帧数</summary>
        public const int InsufficientFlashFrames = 30;
        //tModLoader 固定每秒 60 tick
        private const float TickSeconds = 1f / 60f;

        //系统锁定计时（帧）：>0 时 RAM 锁定为 0，禁止消耗与恢复
        private static int lockTimer;
        private static int lockTotalFrames;
        //RAM 不足闪烁计时（帧）：>0 时 HUD 红色故障闪烁
        private static int flashTimer;

        #endregion

        #region 锁定与故障反馈

        /// <summary>是否处于系统锁定</summary>
        public static bool IsLocked => lockTimer > 0;

        /// <summary>系统锁定剩余帧数</summary>
        public static int LockRemain => lockTimer;

        /// <summary>系统锁定总帧数，供 HUD 推算进度</summary>
        public static int LockTotal => lockTotalFrames;

        /// <summary>系统锁定剩余比例，供 HUD 倒计时填充</summary>
        public static float LockRemainRatio {
            get {
                if (lockTimer <= 0 || lockTotalFrames <= 0) {
                    return 0f;
                }
                return MathHelper.Clamp(lockTimer / (float)lockTotalFrames, 0f, 1f);
            }
        }

        /// <summary>是否处于 RAM 不足故障闪烁</summary>
        public static bool IsFlashing => flashTimer > 0;

        /// <summary>HUD 警告强度 0..1，锁定恒 1，闪烁随计时衰减</summary>
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

        /// <summary>触发系统锁定，榨干 RAM 并锁定指定帧数</summary>
        public static void SystemLock(int frames) {
            if (frames <= 0) {
                return;
            }
            lockTimer = frames;
            lockTotalFrames = frames;
            var local = Local;
            if (local == null) {
                return;
            }
            local.CurrentRam = 0f;
            local.RecoveryCooldown = 0f;
            local.InvokeOnDepleted();
        }

        /// <summary>触发 RAM 不足故障闪烁</summary>
        public static void NotifyInsufficient() {
            flashTimer = InsufficientFlashFrames;
        }

        /// <summary>立即解除系统锁定，仅读档/卸载等极端情形</summary>
        public static void ClearLock() {
            lockTimer = 0;
            lockTotalFrames = 0;
            flashTimer = 0;
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

        /// <summary>已注册修饰器数量，仅供调试/UI</summary>
        public static int ProviderCount => Local?.Providers.Count ?? 0;

        #endregion

        #region 永久升级 API

        /// <summary>永久增加 RAM 基础上限</summary>
        public static void IncreaseBaseMaxRamBy(int delta) => BaseMaxRam = BaseMaxRam + delta;
        /// <summary>永久增加基础每秒恢复量</summary>
        public static void IncreaseBaseRecoveryRateBy(float delta) => BaseRecoveryRate = BaseRecoveryRate + delta;
        /// <summary>是否还能使用 RAM 上限芯片</summary>
        public static bool CanUseCapacityUpgradeChip => UsedCapacityUpgradeChips < MaxCapacityUpgradeChips;
        /// <summary>是否还能使用 RAM 恢复芯片</summary>
        public static bool CanUseRecoveryUpgradeChip => UsedRecoveryUpgradeChips < MaxRecoveryUpgradeChips;

        /// <summary>使用一枚 RAM 上限芯片并同步永久基础值</summary>
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
            //锁定期一律视为不足
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
            //锁定中拒绝消耗
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
            //锁定期间不再额外扣 RAM
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

            //故障闪烁计时独立推进
            if (flashTimer > 0) {
                flashTimer--;
            }

            //系统锁定：强制 RAM 为 0、阻断本帧恢复
            if (lockTimer > 0) {
                lockTimer--;
                local.CurrentRam = 0f;
                local.RecoveryCooldown = 0f;
                if (lockTimer == 0) {
                    lockTotalFrames = 0;
                }
                return;
            }

            if (HackTime.Active) {
                return;
            }
            if (local.RecoveryCooldown > 0f) {
                local.RecoveryCooldown -= TickSeconds;
                return;
            }
            if (local.CurrentRam < local.MaxRam) {
                local.CurrentRam += local.RecoveryRate * TickSeconds;
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
            flashTimer = 0;
            if (!Main.LocalPlayer.active) return;
            var local = Local;
            local.RecoveryCooldown = 0f;
            local.RecomputeEffective();
            local.CurrentRam = local.MaxRam;
        }

        public static void UnloadReset() {
            //数据生命周期由 ModPlayer 管理
        }

        #endregion
    }
}
