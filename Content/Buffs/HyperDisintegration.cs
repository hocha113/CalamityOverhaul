using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    internal class HyperDisintegration : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            if (npc.lifeRegen > 0) {
                npc.lifeRegen = 0;
            }
            npc.lifeRegen -= 200;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.lifeRegen > 0) {
                player.lifeRegen = 0;
            }
            player.lifeRegen -= 40;
        }
    }
}
