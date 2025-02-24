using CalamityMod;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    internal class FrenziedMachineSoul : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "FrenziedMachineSoul";
        public override void SetStaticDefaults() {
            Main.buffNoTimeDisplay[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.Calamity().adrenalineModeActive) {
                player.buffTime[buffIndex] = 10086;
            }
            else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
}
