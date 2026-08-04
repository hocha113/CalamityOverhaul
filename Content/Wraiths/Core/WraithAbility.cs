using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    internal readonly struct WraithAbilityContext(
        Player player,
        Item vesselItem,
        WraithDefinition definition,
        float mastery)
    {
        public Player Player { get; } = player;
        public Item VesselItem { get; } = vesselItem;
        public WraithDefinition Definition { get; } = definition;
        public float Mastery { get; } = mastery;
    }

    /// <summary>鬼切普通五连段实际出刀时冻结的役鬼节拍</summary>
    internal readonly struct WraithComboBeatEvent(
        int beat,
        float aim,
        int facing,
        int baseWeaponDamage,
        float knockback,
        float bladeScale,
        int damageStart,
        uint actionSerial)
    {
        public int Beat { get; } = beat;
        public float Aim { get; } = aim;
        public int Facing { get; } = facing;
        public int BaseWeaponDamage { get; } = baseWeaponDamage;
        public float Knockback { get; } = knockback;
        public float BladeScale { get; } = bladeScale;
        public int DamageStart { get; } = damageStart;
        public uint ActionSerial { get; } = actionSerial;
    }

    internal abstract class WraithPassiveAbility
    {
        public WraithDefinition Definition { get; internal set; }
        public abstract void Update(in WraithAbilityContext context);

        public virtual void OnComboBeat(in WraithAbilityContext context,
            in WraithComboBeatEvent beat) { }
    }

    /// <summary>役鬼资格与资源结算的唯一入口</summary>
    internal static class WraithAbilityService
    {
        internal const float DormantThreshold = 0.20f;
        internal const float WakeThreshold = 0.65f;
        internal const int RecoveryDelayTicks = 60 * 3;
        internal const float RecoveryPerSecond = 0.025f;

        internal static bool IsOnikiriHeld(Player player)
            => player != null && player.active && player.HeldItem != null
                && !player.HeldItem.IsAir && player.HeldItem.type == OnikiriOverride.ID;

        /// <summary>只检查役鬼是否仍由当前手持鬼切维持，不检查本轮后的资源与休眠。</summary>
        internal static bool HasAbilityChannel(Player player, string requiredKey)
            => TryResolveChannel(player, requiredKey, out _, out _);

        internal static bool TryResolve(Player player, string requiredKey,
            out WraithAbilityContext context) {
            context = default;
            if (!TryResolveChannel(player, requiredKey,
                    out Runtime.WraithPlayer wraithPlayer, out WraithDefinition definition)
                || wraithPlayer.IsDormant(requiredKey)) {
                return false;
            }

            float mastery = wraithPlayer.GetMastery(requiredKey);
            if (mastery + 0.0001f < definition.MasteryCost) {
                return false;
            }
            context = new WraithAbilityContext(player, player.HeldItem, definition, mastery);
            return true;
        }

        private static bool TryResolveChannel(Player player, string requiredKey,
            out Runtime.WraithPlayer wraithPlayer, out WraithDefinition definition) {
            wraithPlayer = null;
            definition = null;
            return player != null && player.active && !player.dead && IsOnikiriHeld(player)
                && player.TryGetModPlayer(out wraithPlayer)
                && !string.IsNullOrEmpty(requiredKey)
                && wraithPlayer.EquippedWraithKey == requiredKey
                && WraithRegistry.TryGetUsable(requiredKey, out definition);
        }

        internal static bool TryCommitUse(Player player, string key) {
            if (Main.netMode == NetmodeID.MultiplayerClient
                || !TryResolve(player, key, out WraithAbilityContext context)) {
                return false;
            }
            return TryCommitUse(in context);
        }

        /// <summary>结算已经由权威端确认生效的事件，不因命中回调改变手持物而丢失代价。</summary>
        internal static bool TryCommitUse(in WraithAbilityContext context) {
            if (Main.netMode == NetmodeID.MultiplayerClient || context.Player == null
                || context.Definition == null) {
                return false;
            }
            return context.Player.GetModPlayer<Runtime.WraithPlayer>()
                .TryConsumeAuthority(context.Definition.Key,
                    context.Definition.MasteryCost, context.Definition.ErosionCost);
        }

        internal static void PublishComboBeat(Player player, in WraithComboBeatEvent beat) {
            if (Main.dedServ || player == null || player.whoAmI != Main.myPlayer
                || !player.TryGetModPlayer(out Runtime.WraithPlayer wraithPlayer)
                || string.IsNullOrEmpty(wraithPlayer.EquippedWraithKey)
                || !TryResolve(player, wraithPlayer.EquippedWraithKey,
                    out WraithAbilityContext context)) {
                return;
            }
            context.Definition.Ability?.OnComboBeat(in context, in beat);
        }
    }
}
