using CalamityMod.Events;
using CalamityMod.Items.Materials;
using CalamityMod.Items.Potions.Alcohol;
using CalamityMod.Items.SummonItems;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.NPCs.TownNPCs;
using CalamityMod.Projectiles.Boss;
using CalamityMod;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Content.Tiles.Core;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace CalamityOverhaul.Content.TileModify
{
    internal class ModifyCalamityTitleMusicBox : TileOverride
    {
        public override int TargetID {
            get {
                if (CWRMod.Instance.musicMod == null) {
                    return -1;//加入这个判断以防万一，虽然几乎不可能没有开启
                }
                return CWRMod.Instance.musicMod.Find<ModTile>("CalamityTitleMusicBox").Type;
            }
        }
        public override bool CanLoad() => ModLoader.HasMod("CalamityModMusic");
        public override bool? RightClick(int i, int j, Tile tile) {
            if (!CWRUtils.IsAprilFoolsDay) {
                return null;
            }
            //愚人节快乐
            //尝试召唤至尊灾厄而不是开启音乐
            bool vodka = Main.LocalPlayer.GetItem().type == ModContent.ItemType<FabsolsVodka>() && Main.zenithWorld;

            if (NPC.AnyNPCs(ModContent.NPCType<SupremeCalamitas>()) || BossRushEvent.BossRushActive) {
                return false;
            }

            if (CalamityUtils.CountProjectiles(ModContent.ProjectileType<SCalRitualDrama>()) > 0) {
                return false;
            }

            if (!VaultUtils.SafeGetTopLeft(i, j, out var point)) {
                return false;
            }

            Vector2 spawnPos = point.ToWorldCoordinates() + new Vector2(8f, -16f);

            SoundEngine.PlaySound(SCalAltar.SummonSound, spawnPos);
            Projectile.NewProjectile(new EntitySource_WorldEvent(), spawnPos, Vector2.Zero
                , ModContent.ProjectileType<SCalRitualDrama>(), 0, 0f, Main.myPlayer, 0, vodka.ToInt());

            WorldGen.KillTile(i, j);//防止开启的音乐盒影响Boss音乐，所以直接破坏掉音乐盒
            if (!VaultUtils.isSinglePlayer) {
                NetMessage.SendTileSquare(Main.myPlayer, i, j);
            }

            return false;
        }
    }
}
