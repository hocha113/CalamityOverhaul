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
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            Projectile.velocity = new Vector2(0, -6);
        }

        public override void OnKill(int timeLeft) {
            CWRWorld.MachineRebellion = true;
            //设置成1秒的锁定时间，因为服务器那边的生成是请求状态，大概会有一帧到两帧的广播延迟
            //这导致服务器生成的时候标签已经被关闭了，所以需要一个时间锁
            CWRWorld.DontCloseMachineRebellion = 60;//如果60tick还不够，那一定奸奇搞的鬼
            if (!VaultUtils.isClient) {
                VaultUtils.SpawnBossNetcoded(Main.LocalPlayer, NPCID.SkeletronPrime, false);
                VaultUtils.SpawnBossNetcoded(Main.LocalPlayer, NPCID.Retinazer, false);
                VaultUtils.SpawnBossNetcoded(Main.LocalPlayer, NPCID.Spazmatism, false);
                VaultUtils.SpawnBossNetcoded(Main.LocalPlayer, NPCID.TheDestroyer, false);
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
