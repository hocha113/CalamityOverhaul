using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    /// <summary>
    /// 治疗光环:治疗站光环内获得,提高生命再生。
    /// 各端本地挂,回血走原版 lifeRegen 本地结算,零同步
    /// </summary>
    internal class IndustrialRegenBuff : ModBuff
    {
        //无专属图时用占位
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>lifeRegen 加值,原版单位2=每秒1点,8即每秒4点</summary>
        internal const int RegenBonus = 8;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = false;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.lifeRegen += RegenBonus;
        }
    }
}
