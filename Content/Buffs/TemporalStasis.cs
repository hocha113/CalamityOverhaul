using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    //时停：被禁忌诅咒命中的目标在时间上被钉死，停止一切行动
    internal class TemporalStasis : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            //持续刷新单体冻结计时，由 WorldFreezeOverNPC 统一拦截AI
            npc.CWR().TimeFrozenTick = 2;

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.6f, npc.height * 0.6f);
                PRTLoader.NewParticle<PRT_Spark>(pos, Vector2.Zero, Color.Cyan, Main.rand.NextFloat(0.4f, 0.8f)).Configure(false, 8);
            }
        }
    }
}
