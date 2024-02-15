using CalamityOverhaul.Common;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    internal class HellfireExplosion : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "HellfireExplosion";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
        }

        int time = 0;
        public override void Update(Player player, ref int buffIndex) {

        }

        public override void Update(NPC npc, ref int buffIndex) {
            base.Update(npc, ref buffIndex);
        }
    }
}
