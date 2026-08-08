using CalamityOverhaul.Content.HackTimes;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.RAMSystems
{
    /// <summary>RAM 权威入口与本地 HUD 门面</summary>
    internal sealed class RamSystem : ICWRLoader
    {
        private static readonly List<IRamModifierProvider> providers = [];
        private static readonly HashSet<Type> failedProviderTypes = [];
        private static ulong lastAuthorityUpdateFrame = ulong.MaxValue;

        public const int DefaultBaseMaxRam = 8;
        public const float DefaultBaseRecoveryRate = 0.1f;
        public const int MinBaseMaxRam = 1;
        public const int SoftMaxBaseMaxRam = 64;
        public const int MaxCapacityUpgradeChips = 42;
        public const int MaxRecoveryUpgradeChips = 30;
        public const int CapacityUpgradeChipBonus = 1;
        public const float RecoveryUpgradeChipBonus = 0.05f;
        public const float MaxBaseRecoveryRate = DefaultBaseRecoveryRate
            + MaxRecoveryUpgradeChips * RecoveryUpgradeChipBonus;
        public const float MaxEffectiveRecoveryRate = 16f;
        public const float RecoveryDelay = 1.5f;
        public const float MaxRecoveryDelay = 60f;
        public const float MaxMutationAmount = 1024f;
        public const float MaxMutationPerSecond = 1024f;
        public const int MaxLockFrames = 60 * 60 * 10;
        public const int MaxRecentRequestResults = 64;
        public const int InsufficientFlashFrames = 30;

        private static RAMPlayer Local {
            get {
                if (Main.netMode == NetmodeID.Server || Main.myPlayer < 0
                    || Main.myPlayer >= Main.maxPlayers) {
                    return null;
                }
                Player player = Main.player[Main.myPlayer];
                return player?.active == true ? player.GetModPlayer<RAMPlayer>() : null;
            }
        }

        private static Player LocalOwner => Local?.Player;

        internal static IReadOnlyList<IRamModifierProvider> ModifierProviders => providers;

        public static int UsedCapacityUpgradeChips => Local?.UsedCapacityUpgradeChips ?? 0;
        public static int UsedRecoveryUpgradeChips => Local?.UsedRecoveryUpgradeChips ?? 0;
        public static int BaseMaxRam => Local?.BaseMaxRam ?? DefaultBaseMaxRam;
        public static float BaseRecoveryRate => Local?.BaseRecoveryRate ?? DefaultBaseRecoveryRate;
        public static int MaxRam => Local?.MaxRam ?? DefaultBaseMaxRam;
        public static float RecoveryRate => Local?.RecoveryRate ?? DefaultBaseRecoveryRate;
        public static float CurrentRam => Local?.CurrentRam ?? 0f;
        public static int DisplayCurrent => Local?.DisplayCurrent ?? 0;
        public static float Ratio => Local?.Ratio ?? 0f;
        public static bool IsLocked => Local?.IsLocked ?? false;
        public static int LockRemain => Local?.LockRemain ?? 0;
        public static int LockTotal => Local?.LockTotal ?? 0;
        public static float LockRemainRatio => Local?.LockRemainRatio ?? 0f;
        public static bool IsFlashing => Local?.IsFlashing ?? false;
        public static bool ProfileInitialized => Local?.ProfileInitialized ?? false;
        public static uint SessionId => Local?.SessionId ?? 0;
        public static uint Revision => Local?.Revision ?? 0;
        public static int ProviderCount => providers.Count;
        public static float RecoveryRateRatio => MaxBaseRecoveryRate > 0f
            ? MathHelper.Clamp(RecoveryRate / MaxBaseRecoveryRate, 0f, 1f)
            : 0f;

        void ICWRLoader.UnLoadData() => UnloadReset();

        public static float GetWarningPulse() => Local?.GetWarningPulse() ?? 0f;

        public static bool CanAfford(int cost) {
            if (cost < 0) {
                return false;
            }
            if (HackTime.InfiniteHack) {
                return true;
            }
            return CanAfford(LocalOwner, cost);
        }

        public static bool CanAfford(Player player, float amount) {
            RAMPlayer state = GetState(player);
            return state?.ProfileInitialized == true && state.CanAfford(amount);
        }

        public static bool TryConsume(int cost) {
            if (Main.netMode == NetmodeID.SinglePlayer && HackTime.InfiniteHack) {
                return cost >= 0;
            }
            return TryConsume(LocalOwner, cost, out _);
        }

        public static bool TryConsume(Player player, float amount)
            => TryConsume(player, amount, out _);

        public static bool TryConsume(Player player, float amount, out float paid) {
            paid = 0f;
            return TryGetAuthorityState(player, out RAMPlayer state)
                && state.TryConsumeAuthority(amount, out paid);
        }

        public static bool ConsumeOverTime(float ramPerSecond) {
            if (Main.netMode == NetmodeID.SinglePlayer && HackTime.InfiniteHack) {
                return ramPerSecond >= 0f && float.IsFinite(ramPerSecond);
            }
            return TryConsumeOverTime(LocalOwner, ramPerSecond, out _);
        }

        public static bool TryConsumeOverTime(Player player, float ramPerSecond,
            out float paid) {
            paid = 0f;
            return TryGetAuthorityState(player, out RAMPlayer state)
                && state.TryConsumeOverTimeAuthority(ramPerSecond, out paid);
        }

        public static bool Restore(float amount) => Restore(LocalOwner, amount, out _);

        public static bool Restore(Player player, float amount)
            => Restore(player, amount, out _);

        public static bool Restore(Player player, float amount, out float restored) {
            restored = 0f;
            return TryGetAuthorityState(player, out RAMPlayer state)
                && state.RestoreAuthority(amount, out restored);
        }

        public static bool Refill() => Refill(LocalOwner);

        public static bool Refill(Player player) {
            return TryGetAuthorityState(player, out RAMPlayer state)
                && state.RefillAuthority();
        }

        public static bool SystemLock(int frames) => SystemLock(LocalOwner, frames);

        public static bool SystemLock(Player player, int frames) {
            return TryGetAuthorityState(player, out RAMPlayer state)
                && state.SetLockAuthority(frames);
        }

        public static bool ClearLock() => ClearLock(LocalOwner);

        public static bool ClearLock(Player player) {
            return TryGetAuthorityState(player, out RAMPlayer state)
                && state.ClearLockAuthority();
        }

        public static bool CanUseCapacityUpgradeChip
            => Local?.CanUseUpgrade(RamUpgradeKind.Capacity) ?? false;

        public static bool CanUseRecoveryUpgradeChip
            => Local?.CanUseUpgrade(RamUpgradeKind.Recovery) ?? false;

        public static bool CanUseUpgrade(Player player, RamUpgradeKind kind)
            => GetState(player)?.CanUseUpgrade(kind) ?? false;

        /// <summary>本机是否还有等回执兑现的芯片扣除</summary>
        public static bool HasPendingUpgrade(Player player)
            => GetState(player)?.HasPendingUpgrade ?? false;

        public static bool TryUseCapacityUpgradeChip()
            => TryUseUpgrade(LocalOwner, RamUpgradeKind.Capacity);

        public static bool TryUseCapacityUpgradeChip(Player player)
            => TryUseUpgrade(player, RamUpgradeKind.Capacity);

        public static bool TryUseRecoveryUpgradeChip()
            => TryUseUpgrade(LocalOwner, RamUpgradeKind.Recovery);

        public static bool TryUseRecoveryUpgradeChip(Player player)
            => TryUseUpgrade(player, RamUpgradeKind.Recovery);

        public static bool TryUseUpgrade(Player player, RamUpgradeKind kind) {
            return TryGetAuthorityState(player, out RAMPlayer state)
                && state.TryUseUpgradeAuthority(kind);
        }

        public static bool TryAllocateRequest(Player player, out RamRequestToken token) {
            token = default;
            RAMPlayer state = GetState(player);
            return state?.TryAllocateRequest(out token) == true;
        }

        public static RamRequestDisposition ClassifyRequest(Player player, uint sessionId,
            uint requestId, ushort operationId, out RamRequestResult previous) {
            previous = default;
            if (!TryGetAuthorityState(player, out RAMPlayer state)) {
                return RamRequestDisposition.Invalid;
            }
            return state.ClassifyRequest(sessionId, requestId, operationId, out previous);
        }

        public static bool CompleteRequest(Player player, in RamRequestToken token,
            ushort operationId, byte resultCode, float appliedAmount,
            out RamRequestResult result) {
            result = default;
            if (!TryGetAuthorityState(player, out RAMPlayer state)
                || token.SessionId != state.SessionId || token.RequestId == 0
                || operationId == 0 || !float.IsFinite(appliedAmount)
                || MathF.Abs(appliedAmount) > MaxMutationAmount) {
                return false;
            }
            if (state.ClassifyRequest(token.SessionId, token.RequestId,
                operationId, out _) != RamRequestDisposition.New) {
                return false;
            }

            result = new RamRequestResult(token.SessionId, token.RequestId,
                operationId, resultCode, appliedAmount, state.Revision);
            state.StoreRequestResult(result);
            return true;
        }

        public static bool TryGetRequestResult(Player player, uint requestId,
            out RamRequestResult result) {
            result = default;
            return GetState(player)?.TryGetRequestResult(requestId, out result) == true;
        }

        public static void NotifyInsufficient() => Local?.NotifyInsufficient();

        public static void RegisterProvider(IRamModifierProvider provider) {
            if (provider == null) {
                return;
            }
            Type providerType = provider.GetType();
            for (int i = 0; i < providers.Count; i++) {
                if (providers[i]?.GetType() == providerType) {
                    return;
                }
            }
            providers.Add(provider);
        }

        public static void UnregisterProvider(IRamModifierProvider provider) {
            if (provider == null) {
                return;
            }
            Type providerType = provider.GetType();
            providers.RemoveAll(entry => entry?.GetType() == providerType);
            failedProviderTypes.Remove(providerType);
        }

        internal static bool TryGetProviderBonuses(IRamModifierProvider provider,
            Player player, out int maxBonus, out float recoveryBonus) {
            maxBonus = 0;
            recoveryBonus = 0f;
            if (provider == null || player?.active != true
                || failedProviderTypes.Contains(provider.GetType())) {
                return false;
            }
            try {
                if (!provider.IsActive(player)) {
                    return false;
                }
                maxBonus = Math.Clamp(provider.MaxRamBonus,
                    -SoftMaxBaseMaxRam, SoftMaxBaseMaxRam);
                float value = provider.RecoveryRateBonus;
                recoveryBonus = float.IsFinite(value)
                    ? MathHelper.Clamp(value, -MaxEffectiveRecoveryRate,
                        MaxEffectiveRecoveryRate)
                    : 0f;
                return true;
            } catch (Exception exception) {
                Type type = provider.GetType();
                if (failedProviderTypes.Add(type)) {
                    CWRMod.Instance?.Logger.Warn($"[RAM] Provider {type.FullName} failed: {exception.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// 权威循环：驱动全部玩家的恢复/锁定/快照发送。
        /// 由 <see cref="HackTime.PostUpdateEverything"/> 每帧调用，
        /// 不挂在 ModPlayer.PostUpdate 上——死亡玩家的 Player.Update
        /// 提前返回，会把恢复与锁倒计时一并冻住。
        /// </summary>
        public static void Update() {
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            ulong frame = Main.GameUpdateCount;
            if (lastAuthorityUpdateFrame == frame) {
                return;
            }
            lastAuthorityUpdateFrame = frame;

            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (player?.active != true) {
                    continue;
                }
                player.GetModPlayer<RAMPlayer>().UpdateAuthorityTick();
            }
        }

        public static void Reset() {
            lastAuthorityUpdateFrame = ulong.MaxValue;
        }

        public static void UnloadReset() {
            providers.Clear();
            failedProviderTypes.Clear();
            lastAuthorityUpdateFrame = ulong.MaxValue;
            RamNet.Reset();
        }

        private static RAMPlayer GetState(Player player) {
            if (player == null || !player.active || player.whoAmI < 0
                || player.whoAmI >= Main.maxPlayers) {
                return null;
            }
            return player.GetModPlayer<RAMPlayer>();
        }

        private static bool TryGetAuthorityState(Player player, out RAMPlayer state) {
            state = null;
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return false;
            }
            state = GetState(player);
            if (state == null) {
                return false;
            }
            if (!state.ProfileInitialized && Main.netMode == NetmodeID.SinglePlayer) {
                state.InitializeAuthorityProfile(state.UsedCapacityUpgradeChips,
                    state.UsedRecoveryUpgradeChips, RamNet.AllocateSessionId());
            }
            return state.ProfileInitialized;
        }
    }
}
