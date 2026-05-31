using CalamityOverhaul.Content.HackTimes;
using System;
using Terraria;

namespace CalamityOverhaul.Content.RAMSystems
{
    /// <summary>
    /// CWR 全局 RAM 资源系统
    /// <br/>从原 <c>HackTimeRAM</c> 升级而来，定位为独立的资源管理框架而非骇客时间附属机制
    /// <br/>提供"永久基础值（持久化） + 动态修饰器（运行时聚合）"双层架构：
    /// <list type="bullet">
    ///   <item><see cref="BaseMaxRam"/>/<see cref="BaseRecoveryRate"/>：通过物品/进度永久提升，跨存档保留</item>
    ///   <item><see cref="IRamModifierProvider"/>：义体、Buff、Cyberware 等动态来源每帧聚合</item>
    /// </list>
    /// 每帧得出生效值 <see cref="MaxRam"/>/<see cref="RecoveryRate"/> 供外部读取
    /// </summary>
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

        /// <summary>
        /// 默认基础 RAM 上限
        /// </summary>
        public const int DefaultBaseMaxRam = 8;
        /// <summary>
        /// 默认基础每秒恢复量
        /// </summary>
        public const float DefaultBaseRecoveryRate = 0.1f;
        /// <summary>
        /// 基础上限的最小值（防极端配置）
        /// </summary>
        public const int MinBaseMaxRam = 1;
        /// <summary>
        /// 基础上限的软上限（与 HUD 弧条最大跨度联动，防止绕一整圈）
        /// </summary>
        public const int SoftMaxBaseMaxRam = 64;
        /// <summary>
        /// RAM 上限芯片最多可使用次数
        /// </summary>
        public const int MaxCapacityUpgradeChips = 42;
        /// <summary>
        /// RAM 恢复速度芯片最多可使用次数
        /// </summary>
        public const int MaxRecoveryUpgradeChips = 30;
        /// <summary>
        /// 单个 RAM 上限芯片提供的基础上限
        /// </summary>
        public const int CapacityUpgradeChipBonus = 1;
        /// <summary>
        /// 单个 RAM 恢复速度芯片提供的基础每秒恢复量
        /// </summary>
        public const float RecoveryUpgradeChipBonus = 0.05f;
        /// <summary>
        /// 消耗 RAM 后到开始恢复的延迟（秒）
        /// </summary>
        public const float RecoveryDelay = 1.5f;
        /// <summary>
        /// RAM 不足闪烁提示持续帧数（玩家试图使用却 RAM 不够时的故障警示时长）
        /// </summary>
        public const int InsufficientFlashFrames = 30;
        //tModLoader 固定每秒 60 tick
        private const float TickSeconds = 1f / 60f;

        //系统锁定计时（帧）：>0 时 RAM 被强制锁定为 0，期间禁止消耗与恢复
        private static int lockTimer;
        private static int lockTotalFrames;
        //RAM 不足闪烁计时（帧）：>0 时 HUD 应进行红色故障闪烁
        private static int flashTimer;

        #endregion

        #region 锁定与故障反馈

        /// <summary>
        /// 当前是否处于系统锁定（重启演出后等冷却期）
        /// <br/>锁定期间 RAM 持续为 0、不消耗、不恢复，HUD 应整体显示为红色故障样式
        /// </summary>
        public static bool IsLocked => lockTimer > 0;

        /// <summary>
        /// 系统锁定剩余帧数
        /// </summary>
        public static int LockRemain => lockTimer;

        /// <summary>
        /// 系统锁定总帧数（演出/HUD 可用于推算进度）
        /// </summary>
        public static int LockTotal => lockTotalFrames;

        /// <summary>
        /// 系统锁定剩余比例（1=刚进入锁定，0=锁定结束），供 HUD 绘制倒计时填充
        /// </summary>
        public static float LockRemainRatio {
            get {
                if (lockTimer <= 0 || lockTotalFrames <= 0) {
                    return 0f;
                }
                return MathHelper.Clamp(lockTimer / (float)lockTotalFrames, 0f, 1f);
            }
        }

        /// <summary>
        /// 当前是否处于 RAM 不足故障闪烁
        /// </summary>
        public static bool IsFlashing => flashTimer > 0;

        /// <summary>
        /// 取得 HUD 用警告强度 (0..1)，已组合锁定与故障闪烁
        /// <br/>锁定状态恒定为 1，故障闪烁随计时衰减
        /// </summary>
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

        /// <summary>
        /// 触发系统锁定：立即榨干 RAM 并锁定为 0，持续指定帧数
        /// <br/>由 CyberRestart 等需要硬冷却的技能在生效后调用
        /// </summary>
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

        /// <summary>
        /// 触发 RAM 不足故障闪烁（HUD 短暂红色闪烁提示玩家 RAM 不够用）
        /// </summary>
        public static void NotifyInsufficient() {
            flashTimer = InsufficientFlashFrames;
        }

        /// <summary>
        /// 立即解除系统锁定（仅用于读档/卸载等极端情形）
        /// </summary>
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

        /// <summary>
        /// 当前已注册的修饰器数量（仅供调试/UI 展示）
        /// </summary>
        public static int ProviderCount => Local?.Providers.Count ?? 0;

        #endregion

        #region 永久升级 API

        /// <summary>
        /// 永久增加 RAM 基础上限（差值会写入持久化基础值）
        /// </summary>
        public static void IncreaseBaseMaxRamBy(int delta) => BaseMaxRam = BaseMaxRam + delta;
        /// <summary>
        /// 永久增加每秒基础恢复量（差值会写入持久化基础值）
        /// </summary>
        public static void IncreaseBaseRecoveryRateBy(float delta) => BaseRecoveryRate = BaseRecoveryRate + delta;
        /// <summary>
        /// 当前是否还能使用 RAM 上限芯片
        /// </summary>
        public static bool CanUseCapacityUpgradeChip => UsedCapacityUpgradeChips < MaxCapacityUpgradeChips;
        /// <summary>
        /// 当前是否还能使用 RAM 恢复速度芯片
        /// </summary>
        public static bool CanUseRecoveryUpgradeChip => UsedRecoveryUpgradeChips < MaxRecoveryUpgradeChips;

        /// <summary>
        /// 使用一枚 RAM 上限芯片，并同步永久基础值
        /// </summary>
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
            //数据生命周期由 ModPlayer 实例管理，无需手动清理
        }

        #endregion
    }
}
