using CalamityOverhaul.Content.Dusts;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    internal class GodKillsFire : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "EXHellfire";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = true;
        }

        private int time = 0;
        public override void Update(NPC npc, ref int buffIndex) {
            time++;
            if (time % 10 == 0 && !npc.dontTakeDamage)
                npc.life -= 50;
            if (time % 2 == 0) {
                int dust = Dust.NewDust(npc.Center + Main.rand.NextVector2Unit() * 36, 16, 16, ModContent.DustType<SparkDust>());
                Main.dust[dust].alpha = npc.whoAmI;
            }
        }
    }
}
