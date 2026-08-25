using CalamityOverhaul.Content.Industrials.ElectricPowers.ShieldGenerators;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    /// <summary>
    /// 能量护盾:护盾发生器光环内获得,维持护盾池充能,受击时吸收部分伤害。
    /// 各端本地挂,吸收结算在受击玩家自己的端上(见 <see cref="ShieldGeneratorPlayer"/>)
    /// </summary>
    internal class IndustrialShieldBuff : ModBuff
    {
        //无专属图时用占位
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.GetModPlayer<ShieldGeneratorPlayer>().ShieldAuraActive = true;
        }
    }
}
