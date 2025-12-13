using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    internal class HellburnBuff : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "HellburnBuff";
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.CWR().HellfireExplosion = true;
            CWRRef.SpanFire(player);
        }

        public override void Update(NPC npc, ref int buffIndex) {
            npc.CWR().HellfireExplosion = true;
            CWRRef.SpanFire(npc);
        }
    }
}
