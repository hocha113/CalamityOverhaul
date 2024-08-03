using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityOverhaul.Content.Items.Materials;
using CalamityOverhaul.Content.Projectiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content
{
    internal class CWRTiles : GlobalTile
    {
        public override void RightClick(int i, int j, int type) {
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

                    if (Main.myPlayer == 0) {
                        Projectile.NewProjectile(Main.LocalPlayer.parent(), new Vector2(i, j) * 16, Vector2.Zero
                            , ModContent.ProjectileType<TitleMusicBoxEasterEggProj>(), 0, 0, 0, 0, i * 16, j * 16);
                    }
                }
            }
        }

        public override void Drop(int i, int j, int type) {
            if (type == 1) {
                if (Main.rand.NextBool(23)) {
                    Item.NewItem(new EntitySource_WorldEvent(), new Vector2(i, j) * 16, new Item(ModContent.ItemType<Pebble>()));
                }
                if (Main.rand.NextBool(293)) {
                    Item.NewItem(new EntitySource_WorldEvent(), new Vector2(i, j) * 16, new Item(ModContent.ItemType<Flint>()));
                }
            }
            if (type == 123) {
                if (Main.rand.NextBool(6)) {
                    Item.NewItem(new EntitySource_WorldEvent(), new Vector2(i, j) * 16, new Item(ModContent.ItemType<Flint>()));
                }
            }
        }
    }
}
