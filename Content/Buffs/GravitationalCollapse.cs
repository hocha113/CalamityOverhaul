using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    internal class GravitationalCollapse : ModBuff
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
            npc.lifeRegen -= 150;

            //拖周围敌人
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

            npc.velocity *= 0.96f;

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Vector2 offset = Main.rand.NextVector2CircularEdge(npc.width, npc.height) * 1.5f;
                Vector2 vel = -offset.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 4f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center + offset, vel, Color.MediumPurple, Main.rand.NextFloat(0.5f, 1f)).Configure(false, 12);
            }
        }
    }
}
