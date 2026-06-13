using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    //引力坍缩：被诅咒的目标化作局部引力井，持续失血并把周围的敌人拖向自己
    internal class GravitationalCollapse : ModBuff
    {
        public override string Texture => CWRConstant.Placeholder2;
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
            npc.lifeRegen -= 150;

            //把周围的敌人拖向坍缩中心
            float pullRadius = 320f;
            foreach (var other in Main.ActiveNPCs) {
                if (other.whoAmI == npc.whoAmI || other.friendly || other.dontTakeDamage || other.boss) {
                    continue;
                }
                float dist = Vector2.Distance(other.Center, npc.Center);
                if (dist < pullRadius && dist > 24f) {
                    Vector2 dir = (npc.Center - other.Center).SafeNormalize(Vector2.Zero);
                    other.velocity += dir * 0.9f * (1f - dist / pullRadius);
                }
            }

            //被坍缩者自身行动迟滞
            npc.velocity *= 0.96f;

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                //外缘向内坠落粒子
                Vector2 offset = Main.rand.NextVector2CircularEdge(npc.width, npc.height) * 1.5f;
                Vector2 vel = -offset.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 4f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center + offset, vel, Color.MediumPurple, Main.rand.NextFloat(0.5f, 1f)).Configure(false, 12);
            }
        }
    }
}
