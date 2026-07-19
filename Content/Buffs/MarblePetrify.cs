using CalamityOverhaul.Content.Items.Stones;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    //大理石石化：被巨棍砸中的目标短暂石化，行动明显迟滞并染上灰白石色；
    //Boss 只吃减速不吃染色（施加方负责持续减半）
    internal class MarblePetrify : ModBuff
    {
        //暂无专属图标：先跟随 TemporalStasis 用占位图，贴图补上后自动切换
        public override string Texture => ModContent.HasAsset(CWRConstant.Buff + "MarblePetrify")
            ? CWRConstant.Buff + "MarblePetrify" : CWRConstant.Placeholder2;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            //石之重：每帧阻尼对抗 AI 加速度，杂兵近乎钉住，Boss 只小幅迟滞
            npc.velocity *= npc.boss ? 0.93f : 0.78f;

            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
                PRTLoader.NewParticle<PRT_Smoke>(pos, new Vector2(0f, Main.rand.NextFloat(0.2f, 0.8f))
                    , GraniteMarbleVFX.MarbleDust, Main.rand.NextFloat(0.25f, 0.45f)).Configure(18, 0.5f, 0.03f);
            }
        }
    }

    //石化期间的灰白染色：把受光色拉向石灰白，Boss 不染色保持辨识度
    internal class MarblePetrifyNPC : GlobalNPC
    {
        private static readonly Color stoneTint = new Color(196, 193, 186);

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            if (npc.boss || !npc.HasBuff(ModContent.BuffType<MarblePetrify>())) {
                return;
            }
            //保留一点原色亮度层次，避免整只糊成一块灰
            byte alpha = drawColor.A;
            drawColor = Color.Lerp(drawColor, stoneTint, 0.72f);
            drawColor.A = alpha;
        }
    }
}
