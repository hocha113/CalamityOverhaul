using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletronPrime;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class DraedonsRemoteSpawn : ModProjectile
    {
        public override string Texture => CWRConstant.Item + "Tools/DraedonsRemote";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 64;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
        }

        public override void AI() {
            Projectile.velocity = new Vector2(0, -6);
            Projectile.timeLeft = 2;

            if (++Projectile.ai[0] == 90 && !VaultUtils.isClient) {
                VaultUtils.SpawnBossNetcoded(Main.LocalPlayer, NPCID.SkeletronPrime, false);
                VaultUtils.SpawnBossNetcoded(Main.LocalPlayer, NPCID.Retinazer, false);
                VaultUtils.SpawnBossNetcoded(Main.LocalPlayer, NPCID.Spazmatism, false);
                VaultUtils.SpawnBossNetcoded(Main.LocalPlayer, NPCID.TheDestroyer, false);
                SetMachineRebellion();
                Projectile.Kill();
                Projectile.netUpdate = true;
            }
        }

        public static void SetMachineRebellion() {
            foreach (var npc in Main.ActiveNPCs) {
                if (npc.type == NPCID.SkeletronPrime && npc.CWR().NPCOverride is HeadPrimeAI head) {
                    HeadPrimeAI.MachineRebellion = true;
                    HeadPrimeAI.SetMachineRebellion(npc);
                    head.machineRebellion_ByNPC = true;
                }
                if (npc.type == NPCID.Retinazer && npc.CWR().NPCOverride is RetinazerAI retinazer) {
                    RetinazerAI.MachineRebellion = true;
                    RetinazerAI.SetMachineRebellion(npc);
                    retinazer.machineRebellion_ByNPC = true;
                }
                if (npc.type == NPCID.Spazmatism && npc.CWR().NPCOverride is SpazmatismAI spazmatism) {
                    SpazmatismAI.MachineRebellion = true;
                    SpazmatismAI.SetMachineRebellion(npc);
                    spazmatism.machineRebellion_ByNPC = true;
                }
                if (npc.type == NPCID.TheDestroyer && npc.CWR().NPCOverride is DestroyerHeadAI destroyer) {
                    DestroyerHeadAI.MachineRebellion = true;
                    DestroyerHeadAI.SetMachineRebellion(npc);
                    destroyer.machineRebellion_ByNPC = true;
                }
            }

            HeadPrimeAI.MachineRebellion = false;
            SpazmatismAI.MachineRebellion = false;
            DestroyerHeadAI.MachineRebellion = false;

            if (VaultUtils.isServer) {
                ModPacket modPacket = CWRMod.Instance.GetPacket();
                modPacket.Write((byte)CWRMessageType.MachineRebellion);
                modPacket.Send();
            }
        }
    }

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
            Item.UseSound = SoundID.Roar;
            Item.useAnimation = 20;
            Item.useTime = 20;
            Item.autoReuse = false;
            Item.consumable = false;//还是不要消耗的好
            Item.shoot = ModContent.ProjectileType<DraedonsRemoteSpawn>();
            Item.CWR().IsShootCountCorlUse = true;
        }

        public override bool CanUseItem(Player player)
            => !NPC.AnyNPCs(NPCID.SkeletronPrime) && !NPC.AnyNPCs(NPCID.Retinazer) 
            && !NPC.AnyNPCs(NPCID.Spazmatism) && !NPC.AnyNPCs(NPCID.TheDestroyer) && !Main.dayTime;

        public override void AddRecipes() {
            _ = CreateRecipe()
                 .AddIngredient(ItemID.LunarBar, 2)
                 .AddIngredient(ItemID.Wire, 4)
                 .AddTile(TileID.LunarCraftingStation)
                 .Register();
        }
    }
}
