using CalamityMod.Particles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    internal class EXHellfire : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "EXHellfire";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = true;
        }

        private void SpanFire(Entity entity) {
            bool LowVel = Main.rand.NextBool() ? false : true;
            FlameParticle ballFire = new FlameParticle(entity.Center + CWRUtils.randVr(entity.width / 2)
                , Main.rand.Next(13, 22), Main.rand.NextFloat(0.1f, 0.22f), Main.rand.NextFloat(0.02f, 0.07f), Color.Gold, Color.DarkRed) {
                Velocity = new Vector2(entity.velocity.X * 0.8f, -10).RotatedByRandom(0.005f)
                * (LowVel ? Main.rand.NextFloat(0.4f, 0.65f) : Main.rand.NextFloat(0.8f, 1f))
            };
            GeneralParticleHandler.SpawnParticle(ballFire);
        }

        public override void Update(Player player, ref int buffIndex) {
            player.CWR().HellfireExplosion = true;
            SpanFire(player);
        }

        public override void Update(NPC npc, ref int buffIndex) {
            npc.CWR().HellfireExplosion = true;
            SpanFire(npc);
        }
    }
}
