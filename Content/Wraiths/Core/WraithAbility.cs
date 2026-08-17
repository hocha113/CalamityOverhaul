using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Core
{
    /// <summary>同场役鬼掩码：让一只鬼能直接问"我旁边站着谁"，不必回头查 ModPlayer。</summary>
    [System.Flags]
    internal enum WraithCoven : byte
    {
        None = 0,
        ScapeGhost = 1 << 0,
        HeadlessShade = 1 << 1,
        GhostHand = 1 << 2,
        LanternBoy = 1 << 3,
        CrimsonBride = 1 << 4,
        GhostRain = 1 << 5,
    }

    internal readonly struct WraithAbilityContext(
        Player player,
        Item vesselItem,
        WraithDefinition definition,
        float revival,
        WraithCoven coven)
    {
        public Player Player { get; } = player;
        public Item VesselItem { get; } = vesselItem;
        public WraithDefinition Definition { get; } = definition;
        /// <summary>该鬼当前复苏值 0..1；越接近复苏，能力越凶。</summary>
        public float Revival { get; } = revival;
        /// <summary>本次结印盘上的全部役鬼（含自己）。</summary>
        public WraithCoven Coven { get; } = coven;

        /// <summary>同场是否有这只鬼（自己也算）。</summary>
        public bool HasCoven(WraithCoven other) => (Coven & other) != 0;
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
        internal static bool IsOnikiriHeld(Player player)
            => player != null && player.active && player.HeldItem != null
                && !player.HeldItem.IsAir && player.HeldItem.type == OnikiriOverride.ID;

        /// <summary>只检查役鬼是否仍由当前手持鬼切维持，不检查夺身状态。</summary>
        internal static bool HasAbilityChannel(Player player, string requiredKey)
            => TryResolveChannel(player, requiredKey, out _, out _);

        internal static bool TryResolve(Player player, string requiredKey,
            out WraithAbilityContext context) {
            context = default;
            if (!TryResolveChannel(player, requiredKey,
                    out Runtime.WraithPlayer wraithPlayer, out WraithDefinition definition)
                || Deaths.WraithRevivalDeath.IsSeized(player)) {
                return false;
            }
            context = new WraithAbilityContext(player, player.HeldItem, definition,
                wraithPlayer.GetRevival(requiredKey), ResolveCoven(wraithPlayer));
            return true;
        }

        /// <summary>资格闸门：只问这只鬼在不在结印槽里，不问它是不是唯一那只。</summary>
        private static bool TryResolveChannel(Player player, string requiredKey,
            out Runtime.WraithPlayer wraithPlayer, out WraithDefinition definition) {
            wraithPlayer = null;
            definition = null;
            return player != null && player.active && !player.dead && IsOnikiriHeld(player)
                && player.TryGetModPlayer(out wraithPlayer)
                && !string.IsNullOrEmpty(requiredKey)
                && wraithPlayer.IsEquipped(requiredKey)
                && WraithRegistry.TryGetUsable(requiredKey, out definition);
        }

        internal static WraithCoven ResolveCoven(Runtime.WraithPlayer wraithPlayer) {
            WraithCoven coven = WraithCoven.None;
            if (wraithPlayer == null) {
                return coven;
            }
            foreach (string key in wraithPlayer.EquippedKeys) {
                if (WraithRegistry.TryGetUsable(key, out WraithDefinition definition)) {
                    coven |= CovenOf(definition.AbilityKind);
                }
            }
            return coven;
        }

        /// <summary>玩家当前的同场役鬼掩码；不检查手持与夺身，只报盘上有谁。</summary>
        internal static WraithCoven CovenOf(Player player)
            => player != null && player.TryGetModPlayer(out Runtime.WraithPlayer wraithPlayer)
                ? ResolveCoven(wraithPlayer) : WraithCoven.None;

        internal static WraithCoven CovenOf(WraithAbilityKind kind) => kind switch {
            WraithAbilityKind.ScapeGhost => WraithCoven.ScapeGhost,
            WraithAbilityKind.HeadlessShade => WraithCoven.HeadlessShade,
            WraithAbilityKind.GhostHand => WraithCoven.GhostHand,
            WraithAbilityKind.LanternBoy => WraithCoven.LanternBoy,
            WraithAbilityKind.CrimsonBride => WraithCoven.CrimsonBride,
            WraithAbilityKind.GhostRain => WraithCoven.GhostRain,
            _ => WraithCoven.None,
        };

        internal static bool TryCommitUse(Player player, string key) {
            if (Main.netMode == NetmodeID.MultiplayerClient
                || !TryResolve(player, key, out WraithAbilityContext context)) {
                return false;
            }
            return TryCommitUse(in context);
        }

        /// <summary>结算已经由权威端确认生效的事件：涨该鬼复苏并加侵蚀；满格触发夺身。</summary>
        internal static bool TryCommitUse(in WraithAbilityContext context) {
            if (Main.netMode == NetmodeID.MultiplayerClient || context.Player == null
                || context.Definition == null) {
                return false;
            }
            return context.Player.GetModPlayer<Runtime.WraithPlayer>()
                .TryChargeAuthority(context.Definition.Key,
                    context.Definition.RevivalCost, context.Definition.ErosionCost);
        }

        internal static void PublishComboBeat(Player player, in WraithComboBeatEvent beat) {
            if (Main.dedServ || player == null || player.whoAmI != Main.myPlayer
                || !player.TryGetModPlayer(out Runtime.WraithPlayer wraithPlayer)) {
                return;
            }
            //节拍派给盘上每一只鬼，谁认得就谁接
            for (int slot = 0; slot < Runtime.WraithPlayer.SlotCount; slot++) {
                string key = wraithPlayer.SlotKey(slot);
                if (!string.IsNullOrEmpty(key)
                    && TryResolve(player, key, out WraithAbilityContext context)) {
                    context.Definition.Ability?.OnComboBeat(in context, in beat);
                }
            }
        }
    }
}
