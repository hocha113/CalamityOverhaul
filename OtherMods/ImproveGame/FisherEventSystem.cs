using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.GameSystem;
using System;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.OtherMods.ImproveGame;

/// <summary>
/// 钓鱼机事件系统：拦截 ImproveGame 自动钓鱼机的钓鱼结果
/// 优先使用 Call API，不可用时回退到 MonoMod Hook
/// </summary>
internal class FisherEventSystem : ModSystem
{
    /// <summary>
    /// 用于记录是否已经钓过比目鱼
    /// </summary>
    private static bool HasCaughtHalibut;

    /// <summary>
    /// 缓存的 GiveCatchToStorage MethodInfo，用于 MonoMod Hook
    /// </summary>
    private static MethodInfo _giveCatchMethod;

    /// <summary>
    /// 原始方法的委托类型（实例方法，this + Player + int）
    /// </summary>
    private delegate void orig_GiveCatchToStorage(object self, Player player, int itemType);

    /// <summary>
    /// Hook 方法的委托类型
    /// </summary>
    private delegate void hook_GiveCatchToStorage(orig_GiveCatchToStorage orig, object self, Player player, int itemType);

    public override void PostSetupContent() {
        if (!ModLoader.TryGetMod("ImproveGame", out Mod improveGame))
            return;

        //方案一：优先尝试 Call API
        try {
            object result = improveGame.Call("RegisterFishingEvent", (Delegate)OnFishingCallback);
            if (result is true) {
                Mod.Logger.Info("Successfully registered fishing event callback via ImproveGame Call API.");
                return;
            }
        } catch (Exception) {
            //Call API 不存在或签名不匹配，静默失败
        }

        //方案二：回退到 MonoMod Hook
        Mod.Logger.Info("ImproveGame Call API unavailable, falling back to MonoMod Hook.");
        ApplyMonoModHook(improveGame);
    }

    /// <summary>
    /// 通过反射 + MonoModHooks.Add 挂钩 TEAutofisher.GiveCatchToStorage
    /// </summary>
    private void ApplyMonoModHook(Mod improveGame) {
        try {
            //通过程序集查找 TEAutofisher 类型
            Type teAutofisherType = improveGame.Code.GetType("ImproveGame.Content.Tiles.TEAutofisher");
            if (teAutofisherType is null) {
                Mod.Logger.Warn("Could not find TEAutofisher type in ImproveGame assembly.");
                return;
            }

            //获取 GiveCatchToStorage(Player, int) 方法
            _giveCatchMethod = teAutofisherType.GetMethod(
                "GiveCatchToStorage",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                [typeof(Player), typeof(int)],
                null
            );

            if (_giveCatchMethod is null) {
                Mod.Logger.Warn("Could not find GiveCatchToStorage method in TEAutofisher.");
                return;
            }

            //签名：orig(object self, Player player, int itemType)
            //其中 self 是 TEAutofisher 实例（作为 object，因为我们无法直接引用该类型）
            VaultHook.Add(_giveCatchMethod, (hook_GiveCatchToStorage)Hook_GiveCatchToStorage);

            Mod.Logger.Info("Successfully applied MonoMod Hook on TEAutofisher.GiveCatchToStorage.");
        } catch (Exception ex) {
            Mod.Logger.Error($"Failed to apply MonoMod Hook: {ex}");
        }
    }

    /// <summary>
    /// MonoMod On Hook 的实际回调
    /// 在原始方法执行前修改 itemType，然后调用原始方法
    /// </summary>
    private static void Hook_GiveCatchToStorage(orig_GiveCatchToStorage orig, object self, Player player, int itemType) {
        //在此处修改 itemType
        itemType = ModifyFishingResult(itemType);

        //调用原始方法（使用修改后的 itemType）
        orig(self, player, itemType);
    }

    /// <summary>
    /// Call API 回调（当 ImproveGame 更新支持后使用）
    /// </summary>
    private static void OnFishingCallback(
        TileEntity fisher,
        FishingAttempt fishingAttempt,
        Player player,
        ref int itemType,
        ref int itemStack,
        ref bool cancel) {
        itemType = ModifyFishingResult(itemType);
    }

    /// <summary>
    /// 核心逻辑：统一的钓鱼结果修改方法
    /// 无论走 Call 路径还是 Hook 路径都调用此方法
    /// </summary>
    private static int ModifyFishingResult(int itemType) {
        if (itemType == HalibutOverride.ID) {
            if (HasCaughtHalibut) {
                //钓鱼机在已钓过的情况下只会钓到鲈鱼
                return ItemID.Bass;
            }

            //首次钓到比目鱼，标记为已钓过
            HasCaughtHalibut = true;
        }

        return itemType;
    }

    public override void Unload() {
        _giveCatchMethod = null;
        HasCaughtHalibut = false;
    }

    public override void SaveWorldData(TagCompound tag) {
        tag[nameof(HasCaughtHalibut)] = HasCaughtHalibut;
    }

    public override void LoadWorldData(TagCompound tag) {
        HasCaughtHalibut = false;
        if (tag.TryGet(nameof(HasCaughtHalibut), out bool hasCaughtHalibut)) {
            HasCaughtHalibut = hasCaughtHalibut;
        }
    }
}