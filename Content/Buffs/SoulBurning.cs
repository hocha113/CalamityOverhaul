using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    internal class SoulBurning : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "SoulBurning";
        private int time = 0;
        public override void SetStaticDefaults() => Main.debuff[Type] = true;
        public override void Update(Player player, ref int buffIndex) {
            player.CWR().SoulfireExplosion = true;
            if (++time % 2 == 0) {
                Vector2 pos = player.Center + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.Next(32);
                PRTLoader.NewParticle<PRT_SoulFire>(pos, new Vector2(0, -Main.rand.NextFloat(0.8f, 1.6f)));
            }
        }

        public override void Update(NPC npc, ref int buffIndex) {
            npc.CWR().SoulfireExplosion = true;
            if (++time % 5 == 0) {
                Vector2 pos = npc.Center + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.Next(-npc.width, npc.width);
                PRTLoader.NewParticle<PRT_SoulFire>(pos, new Vector2(0, -Main.rand.NextFloat(0.8f, 1.6f)));
            }
        }
    }
}
