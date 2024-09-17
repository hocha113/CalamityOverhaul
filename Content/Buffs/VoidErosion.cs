using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    internal class VoidErosion : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "VoidErosion";
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex) => npc.CWR().VoidErosionBool = true;

        public static void SpanStar(NPC nPC, Vector2 offset) {
            for (int i = 0; i < 4; i++) {
                float rot1 = MathHelper.PiOver2 * i;
                Vector2 vr = rot1.ToRotationVector2();
                for (int j = 0; j < 3; j++) {
                    BasePRT spark = new PRT_HeavenfallStar(nPC.Center + offset
                        , vr * (0.1f + j * 0.1f), false, 3, 0.8f, Color.CadetBlue);
                    PRTLoader.AddParticle(spark);
                }
            }
        }
    }
}
