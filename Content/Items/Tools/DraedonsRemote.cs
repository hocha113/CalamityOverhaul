using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using CalamityMod.NPCs.VanillaNPCAIOverrides.Bosses;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class DraedonsRemote : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/DraedonsRemote";
        public override void SetStaticDefaults() => Item.ResearchUnlockCount = 64;
        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 20;
            Item.value = Item.buyPrice(0, 8, 50, 50);
            Item.rare = ItemRarityID.Purple;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 20;
            Item.useTime = 20;
            Item.autoReuse = false;
            Item.consumable = false;//还是不要消耗的好
        }

        public override bool CanUseItem(Player player)
            => !NPC.AnyNPCs(NPCID.SkeletronPrime) && !NPC.AnyNPCs(NPCID.Retinazer) 
            && !NPC.AnyNPCs(NPCID.Spazmatism) && !NPC.AnyNPCs(NPCID.TheDestroyer);

        public override bool? UseItem(Player player) {
            HeadPrimeAI.MachineRebellion = true;
            VaultUtils.SpawnBossNetcoded(player, NPCID.SkeletronPrime);
            SpazmatismAI.MachineRebellion = true;
            VaultUtils.SpawnBossNetcoded(player, NPCID.Retinazer);
            SpazmatismAI.MachineRebellion = true;
            VaultUtils.SpawnBossNetcoded(player, NPCID.Spazmatism);
            DestroyerHeadAI.MachineRebellion = true;
            VaultUtils.SpawnBossNetcoded(player, NPCID.TheDestroyer);
            return true;
        }

        public override void AddRecipes() {
            _ = CreateRecipe()
                 .AddIngredient(ItemID.BlackThread, 2)
                 .AddIngredient(ItemID.Hay, 4)
                 .AddIngredient(ItemID.SoulofLight, 2)
                 .AddIngredient(ItemID.SoulofNight, 2)
                 .AddTile(TileID.Loom)
                 .Register();
        }
    }
}
