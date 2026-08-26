using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    //裂渊钳杀的渊压控制。Boss只受轻度拖拽且不染色
    internal class AbyssalPressure : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            //每帧深水阻尼，Boss较轻
            npc.velocity *= npc.boss ? 0.96f : 0.82f;

            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
                PRTLoader.NewParticle<PRT_AbyssGlob>(pos, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f))
                    , Color.Lerp(AbyssrendFX.Deep, AbyssrendFX.Body, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.28f, 0.45f))
                    .Configure(Main.rand.Next(14, 22));
            }
        }
    }

    //深水染色，Boss除外
    internal class AbyssalPressureNPC : GlobalNPC
    {
        private static readonly Color deepTint = new Color(52, 84, 132);

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            if (npc.boss || !npc.HasBuff(ModContent.BuffType<AbyssalPressure>())) {
                return;
            }
            //保留一点原色层次
            byte alpha = drawColor.A;
            drawColor = Color.Lerp(drawColor, deepTint, 0.55f);
            drawColor.A = alpha;
        }
    }
}
