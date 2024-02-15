using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Dusts;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Buffs
{
    internal class SoulBurning : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "SoulBurning";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
        }

        int time = 0;
        public override void Update(Player player, ref int buffIndex) {
            time++;
            if (time % 10 == 0)
                player.statLife -= 35;
            if (time % 2 == 0) {
                Vector2 pos = player.Center + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.Next(32);
                Dust.NewDust(pos, 1, 1, ModContent.DustType<SoulFire>());
            }
        }

        public override void Update(NPC npc, ref int buffIndex) {
            time++;
            if (time % 10 == 0 && !npc.dontTakeDamage)
                npc.life -= 5;
            if (time % 5 == 0) {
                Vector2 pos = npc.Center + Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.Next(-npc.width, npc.width);
                Dust.NewDust(pos, 1, 1, ModContent.DustType<SoulFire>());
            }
        }
    }
}
