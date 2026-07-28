using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Buffs
{
    /// <summary>替死鬼保护印记；存活期间下一次致死伤害会被转移</summary>
    internal sealed class ScapeGhostMark : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
        }
    }
}