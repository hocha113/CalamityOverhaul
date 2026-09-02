using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.Scenarios.Helen;
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
/// 拦截 ImproveGame 钓鱼机结果，优先 Call，否则 MonoMod Hook
/// </summary>
internal class FisherEventSystem : ModSystem
{
    /// <summary>已钓过比目鱼</summary>
    private static bool HasCaughtHalibut;

    /// <summary>GiveCatchToStorage 反射点</summary>
    private static MethodInfo _giveCatchMethod;

    private delegate void orig_GiveCatchToStorage(object self, Player player, int itemType);
    private delegate void hook_GiveCatchToStorage(orig_GiveCatchToStorage orig, object self, Player player, int itemType);

    public override void PostSetupContent() {
        if (!ModLoader.TryGetMod("ImproveGame", out Mod improveGame))
            return;

        //优先 Call API
        try {
            object result = improveGame.Call("RegisterFishingEvent", (Delegate)OnFishingCallback);
            if (result is true) {
                Mod.Logger.Info("Successfully registered fishing event callback via ImproveGame Call API.");
                return;
            }
        } catch (Exception) {
            //Call 不可用则忽略
        }

        //回退 MonoMod Hook
        Mod.Logger.Info("ImproveGame Call API unavailable, falling back to MonoMod Hook.");
        ApplyMonoModHook(improveGame);
    }

    /// <summary>挂钩 TEAutofisher.GiveCatchToStorage</summary>
    private void ApplyMonoModHook(Mod improveGame) {
        try {
            Type teAutofisherType = improveGame.Code.GetType("ImproveGame.Content.Tiles.TEAutofisher");
            if (teAutofisherType is null) {
                Mod.Logger.Warn("Could not find TEAutofisher type in ImproveGame assembly.");
                return;
            }

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

            //self 为 TEAutofisher，无编译期类型
            VaultHook.Add(_giveCatchMethod, (hook_GiveCatchToStorage)Hook_GiveCatchToStorage);

            Mod.Logger.Info("Successfully applied MonoMod Hook on TEAutofisher.GiveCatchToStorage.");
        } catch (Exception ex) {
            Mod.Logger.Error($"Failed to apply MonoMod Hook: {ex}");
        }
    }

    private static void Hook_GiveCatchToStorage(orig_GiveCatchToStorage orig, object self, Player player, int itemType) {
        itemType = ModifyFishingResult(player, itemType);
        orig(self, player, itemType);
    }

    /// <summary>Call API 回调</summary>
    private static void OnFishingCallback(
        TileEntity fisher,
        FishingAttempt fishingAttempt,
        Player player,
        ref int itemType,
        ref int itemStack,
        ref bool cancel) {
        itemType = ModifyFishingResult(player, itemType);
    }

    private static int ModifyFishingResult(Player player, int itemType) {
        if (itemType != HalibutOverride.ID) {
            return itemType;
        }

        //世界级闩只记得钓鱼机自己给过的那条；玩家用鱼竿钓到（已捕获旗已写）或正持有比目鱼时，
        //钓鱼机照样再给一条（反馈五 #25）。与鱼竿口径对齐：玩家真值任一为真即改鲈鱼
        bool alreadyOwned = HasCaughtHalibut
            || (player?.active == true
                && (player.HasHalibut()
                    || HalibutState.Read(player, d => d.HasCaughtHalibut, d => d.HasCaughtHalibut)));
        if (alreadyOwned) {
            return ItemID.Bass;
        }

        HasCaughtHalibut = true;
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
