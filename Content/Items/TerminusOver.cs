using CalamityMod;
using CalamityMod.Enums;
using CalamityMod.Events;
using CalamityMod.Items;
using CalamityMod.Items.SummonItems;
using CalamityMod.NPCs.ExoMechs;
using CalamityMod.Projectiles.Typeless;
using CalamityMod.Systems;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Tools;
using CalamityOverhaul.Content.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items
{
    internal class TerminusOver : ModItem
    {
        public new string LocalizationCategory => "Items.SummonItems";
        public override string Texture => "CalamityMod/Items/SummonItems/Terminus_GFB";
        public override void SetDefaults() {
            Item.rare = ItemRarityID.Blue;
            Item.width = Main.zenithWorld ? 54 : 28;
            Item.height = Main.zenithWorld ? 78 : 28;
            Item.useAnimation = 45;
            Item.useTime = 45;
            Item.channel = true;
            Item.noUseGraphic = true;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
            ItemID.Sets.ShimmerTransformToItem[Type] = ModContent.ItemType<Terminus>();
        }

        public override bool? UseItem(Player player) {
            BossRushEvent.SyncStartTimer(BossRushEvent.StartEffectTotalTime);
            for (int doom = 0; doom < Main.maxNPCs; doom++) {
                NPC n = Main.npc[doom];
                if (!n.active)
                    continue;

                bool shouldDespawn = n.boss || n.type == NPCID.EaterofWorldsHead || n.type == NPCID.EaterofWorldsBody || n.type == NPCID.EaterofWorldsTail || n.type == ModContent.NPCType<Draedon>();
                if (shouldDespawn) {
                    n.active = false;
                    n.netUpdate = true;
                }
            }

            BossRushEvent.BossRushStage = 0;
            BossRushEvent.BossRushActive = !BossRushEvent.BossRushActive;
            if (!BossRushEvent.BossRushActive) {
                PlayerDeathReason pd = PlayerDeathReason.ByCustomReason(player.name + CWRLocText.GetTextValue("BloodAltar_Text3"));
                player.Hurt(pd, player.statLifeMax2 / 2, 0);
            }

            BossRushDialogueSystem.StartDialogue(DownedBossSystem.startedBossRushAtLeastOnce ? BossRushDialoguePhase.StartRepeat : BossRushDialoguePhase.Start);

            CalamityNetcode.SyncWorld();
            if (Main.netMode == NetmodeID.Server) {
                var netMessage = Mod.GetPacket();
                netMessage.Write((byte)CalamityModMessageType.BossRushStage);
                netMessage.Write(BossRushEvent.BossRushStage);
                netMessage.Send();
            }

            return true;
        }

        public static string GenerateRandomString(int length) {
            const string characters = "!@#$%^&*()-_=+[]{}|;:'\",.<>/?`~0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            char[] result = new char[length];

            for (int i = 0; i < length; i++) {
                result[i] = characters[Main.rand.Next(characters.Length)];
            }

            return new string(result);
        }

        public override void UpdateInventory(Player player) {
            if (BossRushEvent.BossRushActive) {
                Item.SetNameOverride(CalamityUtils.ColorMessage(GenerateRandomString(Main.rand.Next(16, 23))
                    , CWRUtils.MultiStepColorLerp(Main.rand.NextFloat(), Color.Red, Color.Black, Color.Purple, Color.Plum)));
            }
            else {
                Item.SetNameOverride(Language.GetText($"Mods.CalamityOverhaul.Items.TerminusOver.DisplayName").Value);
            }
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frameI, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            Texture2D texture = ModContent.Request<Texture2D>("CalamityMod/Items/SummonItems/Terminus_GFB").Value;
            Color overlay = Color.White;
            spriteBatch.Draw(texture, position, null, overlay, 0f, origin, scale, 0, 0);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            Texture2D texture = ModContent.Request<Texture2D>("CalamityMod/Items/SummonItems/Terminus_GFB").Value;
            spriteBatch.Draw(texture, Item.position - Main.screenPosition, null, lightColor, 0f, Vector2.Zero, 1f, 0, 0);
            return false;
        }

        public override void ModifyResearchSorting(ref ContentSamples.CreativeHelper.ItemGroup itemGroup) {
            itemGroup = ContentSamples.CreativeHelper.ItemGroup.EventItem;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient<DarkMatterBall>(3)
                .AddIngredient<Terminus>()
                .AddTile(ModContent.TileType<DarkMatterCompressor>())
                .Register();
        }
    }
}
