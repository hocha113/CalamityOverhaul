using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityOverhaul.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    internal class CWRTiles : GlobalTile
    {
        public override void RightClick(int i, int j, int type) {
            base.RightClick(i, j, type);

            Mod musicMod = CWRMod.Instance.musicMod;
            if (musicMod is not null) {
                if (type == musicMod.Find<ModTile>("CalamityTitleMusicBox").Type
                    && !NPC.AnyNPCs(ModContent.NPCType<SupremeCalamitas>())) {
                    CalamityPlayer modPlayer = Main.LocalPlayer.Calamity();

                    if (!CWRWorld.TitleMusicBoxEasterEgg) {
                        if (modPlayer.sCalKillCount <= 0) {
                            return;
                        }
                    }

                    if (Main.myPlayer == 1) {
                        Projectile.NewProjectile(Main.LocalPlayer.parent(), new Vector2(i, j) * 16, Vector2.Zero
                            , ModContent.ProjectileType<TitleMusicBoxEasterEggProj>(), 0, 0, 0, 0, i * 16, j * 16);
                    }
                }
            }
        }
    }
}
