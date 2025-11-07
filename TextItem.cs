using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.ADV.Scenarios.Draedons.Quest.DeploySignaltower;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    //internal class TestProj : ModProjectile
    //{
    //    public override string Texture => "CalamityOverhaul/icon";
    //    public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<TextItem>()).DisplayName;
    //    public override bool IsLoadingEnabled(Mod mod) {
    //        return false;
    //    }
    //    public override void SetDefaults() {
    //        Projectile.width = Projectile.height = 66;
    //        Projectile.timeLeft = 400;
    //    }

    //    public override void AI() {
    //        Projectile.ai[0]++;
    //        if (Projectile.ai[0] == 300) {
    //            EbnSkyEffect.IsActive = true;
    //        }

    //        if (Projectile.ai[0] == 360) {
    //            ScenarioManager.Start<EternalBlazingNow>();
    //        }
    //    }

    //    public override bool PreDraw(ref Color lightColor) {
    //        return false;
    //    }
    //}

    internal class TextItem : ModItem
    {
        public override string Texture => "CalamityOverhaul/icon";

        //private bool old;
        public override bool IsLoadingEnabled(Mod mod) {
            return true;
        }

        //public override void SetStaticDefaults() {
        //   VaultUtils.LoadenNPCStaticImmunityData(NPCID.TheDestroyer, [NPCID.TheDestroyerBody, NPCID.TheDestroyerTail], 10);
        //}

        public override void SetDefaults() {
            Item.width = 80;
            Item.height = 80;
            Item.damage = 9999;
            Item.DamageType = DamageClass.Default;
            Item.useAnimation = Item.useTime = 13;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 2.25f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shootSpeed = 8f;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.value = 7;
            Item.rare = ItemRarityID.Yellow;
        }

        public override void UpdateInventory(Player player) {
            player.GetDamage(DamageClass.Generic) *= 100;
            //player.velocity.Domp();
            //bool news = player.PressKey();
            //if (news && !old) {
            //   if (Main.HoverItem.type < ItemID.Count) {
            //       Main.HoverItem.type.Domp();
            //   }
            //   else {
            //       ItemLoader.GetItem(Main.HoverItem.type).FullName.Domp();
            //   }
            //}
            //old = news;
            //int num = 0;
            //foreach (var proj in Main.ActiveProjectiles) {
            //   if (proj.type == ProjectileID.PinkLaser || proj.type == ProjectileID.DeathLaser || proj.type == ModContent.ProjectileType<DestroyerCursedLaser>() || proj.type == ModContent.ProjectileType<DestroyerElectricLaser>()) {
            //       num++;
            //   }
            //}
            //num.Domp();
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            return false;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override void HoldItem(Player player) {
        }
        //int tpIndex = 0;
        public override bool? UseItem(Player player) {
            //Projectile.NewProjectile(player.FromObjectGetParent(), player.Center, Vector2.Zero, ModContent.ProjectileType<TestProj>(), 0, 0, player.whoAmI);
            //player.QuickSpawnItem(player.FromObjectGetParent(), new Item(ItemID.Catfish));
            //ScenarioManager.Start<EternalBlazingNow>();
            //EbnSkyEffect.IsActive = true;
            //显示成就提示 - 硫磺火风格
            //AchievementToast.ShowAchievement(
            //    ItemID.None,
            //    "BE结局：永恒燃烧的现在",
            //    "往日被烈火所吞噬，以异类之躯触及永恒",
            //    AchievementToast.ToastStyle.Brimstone
            //);
            //if (CWRMod.Instance.coralite == null) {
            //   return true;
            //}
            ScenarioManager.Reset<DeploySignaltowerScenario>();
            ScenarioManager.Start<DeploySignaltowerScenario>();
            //Point16 point = Main.MouseWorld.ToTileCoordinates16();

            //MagikeCrossed.GetData(point).Domp();

            //for (int i = 0; i < 66; i++) {
            //    Vector2 spawnPos = player.position + new Vector2(Main.rand.Next(229), Main.rand.Next(229));
            //    PRTLoader.NewParticle<PRT_Nutritional>(spawnPos, Vector2.Zero);
            //}

            //Main.MouseWorld.ToTileCoordinates16().Domp();
            //if (player.altFunctionUse == 2) {
            //   MySaveStructure.DoSave<MySaveStructure>();
            //}
            //else {
            //   MySaveStructure.DoLoad<MySaveStructure>();
            //}
            //RocketHut.DoLoad<RocketHut>();
            /*
            //VaultUtils.TryKillChest(Main.MouseWorld.ToTileCoordinates16(), out var items);
            //foreach (var item in items) {
            //   VaultUtils.SpwanItem(player.FromObjectGetParent(), Main.MouseWorld.GetRectangle(32), item);
            //}
            //if (Main.MouseWorld.ToTileCoordinates16().TryFindClosestChest(out var c)) {
            //   Item item = new Item(ItemID.Mushroom);
            //   if (c.CanItemBeAddedToChest(item)) {
            //       c.AddItem(item, true);
            //   }
            //}
            //List<TileProcessor> tps = [];
            //foreach (var p in TileProcessorLoader.TP_InWorld) {
            //   if (!p.Active || p.ID != TileProcessorLoader.GetModuleID<WGGCollectorTP>()) {
            //       continue;
            //   }
            //   tps.Add(p);
            //}

            //if (tpIndex < tps.Count) {
            //   player.Center = tps[tpIndex].CenterInWorld;
            //   tpIndex++;
            //}
            //else {
            //   tpIndex = 0;
            //}
            //int num = 0;
            //foreach (var p in Main.ActiveProjectiles) {
            //   if (p.type != ModContent.ProjectileType<WGGCollectorArm>()) {
            //       continue;
            //   }
            //   num++;
            //}
            //num.Domp();
            //IndustrializationGen.SpawnWGGCollectorTile();
            //if (player.ownedProjectileCounts[ModContent.ProjectileType<RegionProj>()] == 0)
            //Projectile.NewProjectile(player.FromObjectGetParent(), player.Center, Vector2.Zero, ModContent.ProjectileType<RegionProj>(), 0, 0, player.whoAmI);
            //(Main.MouseWorld / 16).ToPoint().Domp();
            //bool copy = false;
            //if (copy) {
            //   JunkmanBase.InitializeData();
            //   Point startPoint = new Point(4202, 985);
            //   Point endPoint = new Point(4392, 1024);
            //   int heiget = Math.Abs(startPoint.Y - endPoint.Y);
            //   int wid = Math.Abs(startPoint.X - endPoint.X);
            //   using (BinaryWriter writer = new BinaryWriter(File.Open("D:\\TileWorldData\\structure.dat", FileMode.Create))) {
            //       writer.Write(wid * heiget);
            //       for (int x = 0; x < wid; x++) {
            //           for (int y = 0; y < heiget; y++) {
            //               Point offsetPoint = new Point(x, y);
            //               JunkmanBase.WriteTile(writer, Main.tile[startPoint.X + x, startPoint.Y + y], offsetPoint);
            //           }
            //       }
            //   }
            //}
            //else {
            //   using (BinaryReader reader = new BinaryReader(File.Open("D:\\TileWorldData\\structure.dat", FileMode.Open))) {
            //       int count = reader.ReadInt32();
            //       for (int x = 0; x < count; x++) {
            //           JunkmanBase.ReadTile(reader, new Point((int)Main.MouseWorld.X / 16, (int)Main.MouseWorld.Y / 16));
            //       }
            //   }
            //}
            //JunkmanBase.Spawn();*/
            return true;
        }
    }

    //internal class MySaveStructure : SaveStructure
    //{
    //   public override string SavePath => Path.Combine(VaultSave.RootPath, "Structure", Mod.Name, $"JunkmanBase.nbt");
    //   public override void SaveData(TagCompound tag) {
    //       SaveRegion(tag, new Point16(4202, 989).GetRectangleFromPoints(new Point16(4392, 1024)));
    //   }

    //   public override void LoadData(TagCompound tag) {
    //       LoadRegion(tag, Main.MouseWorld.ToTileCoordinates16());
    //       TagCache.Invalidate(SavePath);
    //   }
    //}
}
