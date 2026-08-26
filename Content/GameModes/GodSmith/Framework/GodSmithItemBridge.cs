using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Framework
{
    /// <summary>
    /// 神匠的 GlobalItem 桥接：只承载 InnoVault <see cref="InnoVault.GameSystem.ItemOverride"/>
    /// 钩子面缺失、而方案又确实需要的钩子。目前仅用速倍率一项；
    /// 新增桥接钩子前先确认 ItemOverride 真的没有对应入口
    /// </summary>
    internal class GodSmithItemBridge : GlobalItem
    {
        public override float UseSpeedMultiplier(Item item, Player player) {
            if (!GameModeSystem.GodSmithActive) {
                return 1f;
            }
            if (GodSmithScheme.TryGetScheme(item.type, out GodSmithScheme scheme)) {
                return scheme.GsUseSpeedMultiplier(item, player);
            }
            return 1f;
        }
    }
}
