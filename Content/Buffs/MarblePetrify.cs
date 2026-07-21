using CalamityOverhaul.Content.Items.Stones;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    //Boss只减速不染色，施加方持续减半
    internal class MarblePetrify : ModBuff
    {
        //无专属图时用占位
        public override string Texture => ModContent.HasAsset(CWRConstant.Buff + "MarblePetrify")
            ? CWRConstant.Buff + "MarblePetrify" : CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            //每帧阻尼，Boss较轻
            npc.velocity *= npc.boss ? 0.93f : 0.78f;

            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
                PRTLoader.NewParticle<PRT_Smoke>(pos, new Vector2(0f, Main.rand.NextFloat(0.2f, 0.8f))
                    , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.25f, 0.45f)).Configure(18, 0.5f, 0.03f);
            }
        }
    }

    //灰白染色，Boss除外
    internal class MarblePetrifyNPC : GlobalNPC
    {
        private static readonly Color stoneTint = new Color(196, 193, 186);

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            if (npc.boss || !npc.HasBuff(ModContent.BuffType<MarblePetrify>())) {
                return;
            }
            //保留一点原色层次
            byte alpha = drawColor.A;
            drawColor = Color.Lerp(drawColor, stoneTint, 0.72f);
            drawColor.A = alpha;
        }
    }
}
