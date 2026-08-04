using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Buffs
{
    /// <summary>
    /// 替死后遗症：12 秒内移速降至 72%。不可右键清除，与侵蚀累积独立。
    /// </summary>
    internal sealed class ScapeAfterburn : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;        //不可右键清除
            Main.pvpBuff[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.moveSpeed *= 0.72f;
            player.maxRunSpeed *= 0.72f;
        }
    }
}
