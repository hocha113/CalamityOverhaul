using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod;
using CalamityOverhaul.Common;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.Projectiles
{
    internal class TitleMusicBoxEasterEggProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.timeLeft = 30;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            if (Projectile.ai[0] == 0) {
                CalamityUtils.DisplayLocalizedText(
                    CWRUtils.Translation(
                        "要来一场音乐的狂欢吗？",
                        "Want to have a musical orgy?")
                    , Color.Pink);

                int npcindex = 0;
                if (!CWRUtils.isClient) {
                    npcindex = CWRUtils.NewNPCEasy(null, new Vector2(Projectile.ai[1], Projectile.ai[2]) * 16 + new Vector2(0, -32)
                    , ModContent.NPCType<SupremeCalamitas>());
                }

                if (Main.npc[npcindex].type == ModContent.NPCType<SupremeCalamitas>()) {
                    Main.npc[npcindex].CWR().SprBoss = true;
                    Main.npc[npcindex].life = Main.npc[npcindex].lifeMax = 66666666;
                    Main.npc[npcindex].damage *= 2;
                    Main.npc[npcindex].netUpdate = true;
                    Main.npc[npcindex].netUpdate2 = true;
                }
                else {
                    foreach (NPC n in Main.npc) {
                        if (n.type == ModContent.NPCType<SupremeCalamitas>()) {
                            n.CWR().SprBoss = true;
                            n.life = Main.npc[npcindex].lifeMax = 66666666;
                            n.damage *= 2;
                            n.netUpdate = true;
                            n.netUpdate2 = true;
                        }
                    }
                }

                CWRWorld.TitleMusicBoxEasterEgg = false;
                CalamityNetcode.SyncWorld();
            }
            Projectile.ai[0]++;
        }
    }
}
