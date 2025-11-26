using System.Collections.Generic;
using Terraria.ID;

namespace CalamityOverhaul.Content.Industrials.Generator
{
    internal class FuelItems
    {
        public readonly static Dictionary<int, int> FuelItemToCombustion = new Dictionary<int, int>() {
            // === 基础木材 (30-50) ===
            { ItemID.Wood, 30 },
            { ItemID.Ebonwood, 30 },
            { ItemID.RichMahogany, 35 },
            { ItemID.Pearlwood, 40 },
            { ItemID.Shadewood, 30 },
            { ItemID.SpookyWood, 45 },
            { ItemID.DynastyWood, 35 },
            { ItemID.BorealWood, 28 },
            { ItemID.PalmWood, 32 },
            { ItemID.AshWood, 25 },
            { ItemID.Acorn, 15 },
            
            // === 煤炭和高能燃料 (200-300) ===
            { ItemID.Coal, 200 },
            { ItemID.Torch, 15 },
            { ItemID.Gel, 60 },
            { ItemID.PinkGel, 120 },
            
            // === 特殊高能燃料 (500-2000+) ===
            { ItemID.LavaBucket, 800 },
            { ItemID.BottomlessLavaBucket, 2500 },
            { ItemID.MeteoriteBar, 350 },
            { ItemID.HellstoneBar, 500 },
            { ItemID.CursedFlame, 200 },
            { ItemID.CursedSapling, 800 },
            { ItemID.RedPotion, 600 },
            
            // === 基础木制工具 (60-120) ===
            { ItemID.WoodenSword, 90 },
            { ItemID.WoodenHammer, 120 },
            { ItemID.WoodenBow, 120 },
            { ItemID.WoodenBoomerang, 120 },
            { ItemID.BugNet, 60 },
            
            // === 木制平台和栅栏 (8-12) ===
            { ItemID.WoodPlatform, 8 },
            { ItemID.EbonwoodPlatform, 8 },
            { ItemID.RichMahoganyPlatform, 9 },
            { ItemID.PearlwoodPlatform, 10 },
            { ItemID.ShadewoodPlatform, 8 },
            { ItemID.SpookyPlatform, 12 },
            { ItemID.DynastyPlatform, 9 },
            { ItemID.BorealWoodPlatform, 7 },
            { ItemID.PalmWoodPlatform, 8 },
            { ItemID.AshWoodPlatform, 7 },
            { ItemID.LivingWoodPlatform, 10 },
            { ItemID.SlimePlatform, 20 },

            { ItemID.EbonwoodFence, 6 },
            { ItemID.RichMahoganyFence, 7 },
            { ItemID.PearlwoodFence, 8 },
            { ItemID.ShadewoodFence, 6 },
            { ItemID.BorealWoodFence, 6 },
            { ItemID.PalmWoodFence, 6 },
            { ItemID.AshWoodFence, 6 },
            
            // === 墙壁 (5-15) ===
            { ItemID.WoodWall, 5 },
            { ItemID.EbonwoodWall, 5 },
            { ItemID.RichMahoganyWall, 6 },
            { ItemID.PearlwoodWall, 7 },
            { ItemID.ShadewoodWall, 5 },
            { ItemID.SpookyWoodWall, 10 },
            { ItemID.BorealWoodWall, 5 },
            { ItemID.PalmWoodWall, 5 },
            { ItemID.AshWoodWall, 5 },
            { ItemID.PlankedWall, 6 },
            { ItemID.WoodenBeam, 6 },
            { ItemID.HayWall, 8 },
            { ItemID.GrassWall, 8 },
            { ItemID.JungleWall, 8 },
            { ItemID.FlowerWall, 10 },
            { ItemID.MushroomWall, 12 },
            { ItemID.SlimeBlockWall, 25 },
            
            // === 门 (90-120) ===
            { ItemID.WoodenDoor, 90 },
            { ItemID.EbonwoodDoor, 90 },
            { ItemID.RichMahoganyDoor, 95 },
            { ItemID.PearlwoodDoor, 100 },
            { ItemID.ShadewoodDoor, 90 },
            { ItemID.SpookyDoor, 120 },
            { ItemID.DynastyDoor, 95 },
            { ItemID.BorealWoodDoor, 85 },
            { ItemID.PalmWoodDoor, 90 },
            { ItemID.AshWoodDoor, 85 },
            { ItemID.LivingWoodDoor, 100 },
            { ItemID.MushroomDoor, 110 },
            { ItemID.SlimeDoor, 150 },
            
            // === 桌子 (80-100) ===
            { ItemID.WoodenTable, 80 },
            { ItemID.EbonwoodTable, 80 },
            { ItemID.RichMahoganyTable, 85 },
            { ItemID.PearlwoodTable, 90 },
            { ItemID.ShadewoodTable, 80 },
            { ItemID.SpookyTable, 110 },
            { ItemID.DynastyTable, 85 },
            { ItemID.BorealWoodTable, 75 },
            { ItemID.PalmWoodTable, 80 },
            { ItemID.AshWoodTable, 75 },
            { ItemID.LivingWoodTable, 90 },
            { ItemID.SlimeTable, 130 },
            
            // === 椅子 (70-90) ===
            { ItemID.WoodenChair, 70 },
            { ItemID.EbonwoodChair, 70 },
            { ItemID.RichMahoganyChair, 75 },
            { ItemID.PearlwoodChair, 80 },
            { ItemID.ShadewoodChair, 70 },
            { ItemID.SpookyChair, 100 },
            { ItemID.DynastyChair, 75 },
            { ItemID.BorealWoodChair, 65 },
            { ItemID.PalmWoodChair, 70 },
            { ItemID.AshWoodChair, 65 },
            { ItemID.LivingWoodChair, 80 },
            { ItemID.MushroomChair, 85 },
            { ItemID.SlimeChair, 120 },
            
            // === 工作台 (120-150) ===
            { ItemID.WorkBench, 120 },
            { ItemID.EbonwoodWorkBench, 120 },
            { ItemID.RichMahoganyWorkBench, 125 },
            { ItemID.PearlwoodWorkBench, 130 },
            { ItemID.ShadewoodWorkBench, 120 },
            { ItemID.SpookyWorkBench, 180 },
            { ItemID.DynastyWorkBench, 125 },
            { ItemID.BorealWoodWorkBench, 115 },
            { ItemID.PalmWoodWorkBench, 120 },
            { ItemID.AshWoodWorkbench, 115 },
            { ItemID.LivingWoodWorkBench, 130 },
            { ItemID.MushroomWorkBench, 135 },
            { ItemID.SlimeWorkBench, 180 },
            
            // === 箱子 (140-180) ===
            { ItemID.Chest, 140 },
            { ItemID.EbonwoodChest, 140 },
            { ItemID.RichMahoganyChest, 145 },
            { ItemID.PearlwoodChest, 150 },
            { ItemID.ShadewoodChest, 140 },
            { ItemID.DynastyChest, 145 },
            { ItemID.BorealWoodChest, 135 },
            { ItemID.PalmWoodChest, 140 },
            { ItemID.AshWoodChest, 135 },
            { ItemID.LivingWoodChest, 150 },
            { ItemID.SlimeChest, 200 },
            { ItemID.WoodenCrate, 150 },
            { ItemID.WoodenCrateHard, 150 },
            { ItemID.Fake_AshWoodChest, 135 },
            
            // === 床 (220-280) ===
            { ItemID.Bed, 220 },
            { ItemID.EbonwoodBed, 220 },
            { ItemID.RichMahoganyBed, 230 },
            { ItemID.PearlwoodBed, 240 },
            { ItemID.ShadewoodBed, 220 },
            { ItemID.SpookyBed, 320 },
            { ItemID.DynastyBed, 230 },
            { ItemID.BorealWoodBed, 210 },
            { ItemID.PalmWoodBed, 220 },
            { ItemID.AshWoodBed, 210 },
            { ItemID.LivingWoodBed, 240 },
            { ItemID.SlimeBed, 320 },
            
            // === 梳妆台 (180-220) ===
            { ItemID.Dresser, 180 },
            { ItemID.EbonwoodDresser, 180 },
            { ItemID.RichMahoganyDresser, 185 },
            { ItemID.PearlwoodDresser, 190 },
            { ItemID.ShadewoodDresser, 180 },
            { ItemID.SpookyDresser, 260 },
            { ItemID.DynastyDresser, 185 },
            { ItemID.BorealWoodDresser, 175 },
            { ItemID.PalmWoodDresser, 180 },
            { ItemID.AshWoodDresser, 175 },
            { ItemID.LivingWoodDresser, 190 },
            { ItemID.SlimeDresser, 240 },
            
            // === 钢琴 (200-240) ===
            { ItemID.Piano, 200 },
            { ItemID.EbonwoodPiano, 200 },
            { ItemID.RichMahoganyPiano, 205 },
            { ItemID.PearlwoodPiano, 210 },
            { ItemID.ShadewoodPiano, 200 },
            { ItemID.SpookyPiano, 280 },
            { ItemID.DynastyPiano, 205 },
            { ItemID.BorealWoodPiano, 195 },
            { ItemID.PalmWoodPiano, 200 },
            { ItemID.AshWoodPiano, 195 },
            { ItemID.LivingWoodPiano, 210 },
            { ItemID.SlimePiano, 260 },
            
            // === 沙发 (180-220) ===
            { ItemID.Sofa, 180 },
            { ItemID.EbonwoodSofa, 180 },
            { ItemID.RichMahoganySofa, 185 },
            { ItemID.PearlwoodSofa, 190 },
            { ItemID.ShadewoodSofa, 180 },
            { ItemID.SpookySofa, 260 },
            { ItemID.DynastySofa, 185 },
            { ItemID.PalmWoodSofa, 180 },
            { ItemID.LivingWoodSofa, 190 },
            { ItemID.SlimeSofa, 240 },
            
            // === 浴缸 (140-180) ===
            { ItemID.Bathtub, 140 },
            { ItemID.RichMahoganyBathtub, 145 },
            { ItemID.PearlwoodBathtub, 150 },
            { ItemID.SpookyBathtub, 210 },
            { ItemID.ShadewoodBathtub, 140 },
            { ItemID.AshWoodBathtub, 145 },
            { ItemID.PalmWoodBathtub, 140 },
            { ItemID.BorealWoodBathtub, 135 },
            { ItemID.SlimeBathtub, 200 },
            { ItemID.DynastyBathtub, 145 },
            
            // === 长凳 (120-160) ===
            { ItemID.Bench, 120 },
            { ItemID.PalmWoodBench, 120 },
            
            // === 时钟 (240-300) ===
            { ItemID.EbonwoodClock, 240 },
            { ItemID.LivingWoodClock, 250 },
            { ItemID.RichMahoganyClock, 245 },
            { ItemID.PalmWoodClock, 240 },
            { ItemID.PearlwoodClock, 250 },
            { ItemID.ShadewoodClock, 240 },
            { ItemID.SpookyClock, 340 },
            { ItemID.BorealWoodClock, 235 },
            { ItemID.SlimeClock, 320 },
            { ItemID.DynastyClock, 245 },
            
            // === 书架 (140-180) ===
            { ItemID.Bookcase, 140 },
            { ItemID.EbonwoodBookcase, 140 },
            { ItemID.RichMahoganyBookcase, 145 },
            { ItemID.PearlwoodBookcase, 150 },
            { ItemID.SpookyBookcase, 210 },
            { ItemID.LivingWoodBookcase, 150 },
            { ItemID.ShadewoodBookcase, 140 },
            { ItemID.BorealWoodBookcase, 135 },
            { ItemID.SlimeBookcase, 200 },
            { ItemID.DynastyBookcase, 145 },
            
            // === 灯具 (60-90) ===
            { ItemID.EbonwoodLamp, 65 },
            { ItemID.RichMahoganyLamp, 70 },
            { ItemID.PearlwoodLamp, 75 },
            { ItemID.SpookyLamp, 100 },
            { ItemID.LivingWoodLamp, 75 },
            { ItemID.ShadewoodLamp, 65 },
            { ItemID.DynastyLamp, 70 },
            { ItemID.PalmWoodLamp, 65 },
            { ItemID.BorealWoodLamp, 60 },
            { ItemID.SlimeLamp, 110 },
            
            // === 灯笼 (120-150) ===
            { ItemID.RichMahoganyLantern, 125 },
            { ItemID.PearlwoodLantern, 130 },
            { ItemID.SpookyLantern, 180 },
            { ItemID.LivingWoodLantern, 130 },
            { ItemID.ShadewoodLantern, 120 },
            { ItemID.DynastyLantern, 125 },
            { ItemID.PalmWoodLantern, 120 },
            { ItemID.BorealWoodLantern, 115 },
            { ItemID.SlimeLantern, 170 },
            
            // === 蜡烛 (35-45) ===
            { ItemID.EbonwoodCandle, 35 },
            { ItemID.RichMahoganyCandle, 38 },
            { ItemID.PearlwoodCandle, 40 },
            { ItemID.SpookyCandle, 55 },
            { ItemID.LivingWoodCandle, 40 },
            { ItemID.ShadewoodCandle, 35 },
            { ItemID.DynastyCandle, 38 },
            { ItemID.PalmWoodCandle, 35 },
            { ItemID.BorealWoodCandle, 33 },
            { ItemID.SlimeCandle, 65 },
            
            // === 烛台 (50-70) ===
            { ItemID.EbonwoodCandelabra, 50 },
            { ItemID.RichMahoganyCandelabra, 53 },
            { ItemID.PearlwoodCandelabra, 55 },
            { ItemID.SpookyCandelabra, 75 },
            { ItemID.ShadewoodCandelabra, 50 },
            { ItemID.LivingWoodCandelabra, 55 },
            { ItemID.DynastyCandelabra, 53 },
            { ItemID.PalmWoodCandelabra, 50 },
            { ItemID.BorealWoodCandelabra, 48 },
            { ItemID.SlimeCandelabra, 85 },
            
            // === 吊灯 (100-130) ===
            { ItemID.EbonwoodChandelier, 100 },
            { ItemID.RichMahoganyChandelier, 105 },
            { ItemID.PearlwoodChandelier, 110 },
            { ItemID.SpookyChandelier, 150 },
            { ItemID.LivingWoodChandelier, 110 },
            { ItemID.ShadewoodChandelier, 100 },
            { ItemID.DynastyChandelier, 105 },
            { ItemID.PalmWoodChandelier, 100 },
            { ItemID.BorealWoodChandelier, 95 },
            { ItemID.SlimeChandelier, 140 },
            
            // === 水槽 (60-80) ===
            { ItemID.WoodenSink, 60 },
            { ItemID.EbonwoodSink, 60 },
            { ItemID.RichMahoganySink, 63 },
            { ItemID.PearlwoodSink, 65 },
            { ItemID.LivingWoodSink, 65 },
            { ItemID.ShadewoodSink, 60 },
            { ItemID.SpookySink, 90 },
            { ItemID.DynastySink, 63 },
            { ItemID.PalmWoodSink, 60 },
            { ItemID.SlimeSink, 85 },
            
            // === 厕所 (70-90) ===
            { ItemID.ToiletEbonyWood, 70 },
            { ItemID.ToiletRichMahogany, 73 },
            { ItemID.ToiletPearlwood, 75 },
            { ItemID.ToiletLivingWood, 75 },
            { ItemID.ToiletShadewood, 70 },
            { ItemID.ToiletSpooky, 100 },
            { ItemID.ToiletDynasty, 73 },
            { ItemID.ToiletPalm, 70 },
            { ItemID.ToiletBoreal, 68 },
            { ItemID.ToiletSlime, 95 },
            
            // === 火把 (15-40) ===
            { ItemID.BlueTorch, 18 },
            { ItemID.RedTorch, 18 },
            { ItemID.GreenTorch, 18 },
            { ItemID.PurpleTorch, 18 },
            { ItemID.WhiteTorch, 18 },
            { ItemID.YellowTorch, 18 },
            { ItemID.DemonTorch, 20 },
            { ItemID.CursedTorch, 25 },
            { ItemID.PinkTorch, 22 },
            { ItemID.RainbowTorch, 40 },
            
            // === 火把 - 营地 (120-180) ===
            { ItemID.CursedCampfire, 140 },
            { ItemID.DemonCampfire, 140 },
            { ItemID.FrozenCampfire, 140 },
            { ItemID.IchorCampfire, 140 },
            { ItemID.RainbowCampfire, 180 },
            { ItemID.UltraBrightCampfire, 160 },
            { ItemID.DesertCampfire, 140 },
            { ItemID.CoralCampfire, 140 },
            { ItemID.CorruptCampfire, 140 },
            { ItemID.CrimsonCampfire, 140 },
            { ItemID.HallowedCampfire, 140 },
            { ItemID.JungleCampfire, 140 },
            
            // === 木制工具和武器 ===
            { ItemID.EbonwoodSword, 90 },
            { ItemID.EbonwoodHammer, 120 },
            { ItemID.EbonwoodBow, 120 },
            { ItemID.RichMahoganySword, 95 },
            { ItemID.RichMahoganyHammer, 125 },
            { ItemID.RichMahoganyBow, 125 },
            { ItemID.PearlwoodSword, 100 },
            { ItemID.PearlwoodHammer, 130 },
            { ItemID.PearlwoodBow, 130 },
            { ItemID.ShadewoodSword, 90 },
            { ItemID.ShadewoodHammer, 120 },
            { ItemID.ShadewoodBow, 120 },
            { ItemID.BorealWoodSword, 85 },
            { ItemID.BorealWoodHammer, 115 },
            { ItemID.BorealWoodBow, 115 },
            { ItemID.PalmWoodBow, 95 },
            { ItemID.PalmWoodHammer, 125 },
            { ItemID.PalmWoodSword, 95 },
            { ItemID.WoodenArrow, 5 },
            
            // === 盔甲 (120-140) ===
            { ItemID.WoodHelmet, 120 },
            { ItemID.WoodBreastplate, 130 },
            { ItemID.WoodGreaves, 125 },
            { ItemID.EbonwoodHelmet, 120 },
            { ItemID.EbonwoodBreastplate, 130 },
            { ItemID.EbonwoodGreaves, 125 },
            { ItemID.RichMahoganyHelmet, 125 },
            { ItemID.RichMahoganyBreastplate, 135 },
            { ItemID.RichMahoganyGreaves, 130 },
            { ItemID.PearlwoodHelmet, 130 },
            { ItemID.PearlwoodBreastplate, 140 },
            { ItemID.PearlwoodGreaves, 135 },
            { ItemID.ShadewoodHelmet, 120 },
            { ItemID.ShadewoodBreastplate, 130 },
            { ItemID.ShadewoodGreaves, 125 },
            { ItemID.BorealWoodHelmet, 115 },
            { ItemID.BorealWoodBreastplate, 125 },
            { ItemID.BorealWoodGreaves, 120 },
            { ItemID.PalmWoodHelmet, 120 },
            { ItemID.PalmWoodBreastplate, 130 },
            { ItemID.PalmWoodGreaves, 125 },
            
            // === 其他家具和装饰 ===
            { ItemID.Mannequin, 160 },
            { ItemID.Loom, 130 },
            { ItemID.Keg, 140 },
            { ItemID.Sawmill, 180 },
            { ItemID.CarpentryRack, 120 },
            { ItemID.SwordRack, 120 },
            { ItemID.WeaponRack, 120 },
            { ItemID.ItemFrame, 100 },
            { ItemID.HatRack, 80 },
            { ItemID.LivingLoom, 150 },
            { ItemID.WoodShelf, 40 },
            { ItemID.DynastyCup, 15 },
            { ItemID.DynastyBowl, 15 },
            
            // === 绳索和藤蔓 ===
            { ItemID.Rope, 4 },
            { ItemID.RopeCoil, 40 },
            { ItemID.Vine, 50 },
            { ItemID.VineRope, 30 },
            { ItemID.WoodenSpike, 12 },
            
            // === 布料和纤维 (25-80) ===
            { ItemID.Hay, 20 },
            { ItemID.Silk, 35 },
            { ItemID.BlackThread, 8 },
            { ItemID.GreenThread, 8 },
            { ItemID.Leather, 20 },
            { ItemID.Robe, 120 },
            { ItemID.TatteredCloth, 15 },
            { ItemID.AncientCloth, 25 },
            
            // === 有机材料 ===
            { ItemID.GlowingMushroom, 18 },
            { ItemID.Feather, 12 },
            { ItemID.OldShoe, 8 },
            
            // === 书籍和纸张 (30-50) ===
            { ItemID.Book, 35 },
            
            // === 服饰 (60-100) ===
            { ItemID.RainHat, 65 },
            { ItemID.RainCoat, 75 },
            { ItemID.UmbrellaHat, 70 },
            { ItemID.SailorHat, 55 },
            { ItemID.EyePatch, 30 },
            { ItemID.SailorShirt, 70 },
            { ItemID.SailorPants, 70 },
            
            // === 其他 ===
            { ItemID.Confetti, 8 },
            { ItemID.ExplosivePowder, 40 },
            
            // === 种植盒 (15-25) ===
            { ItemID.DayBloomPlanterBox, 18 },
            { ItemID.MoonglowPlanterBox, 18 },
            { ItemID.CorruptPlanterBox, 18 },
            { ItemID.CrimsonPlanterBox, 18 },
            { ItemID.BlinkrootPlanterBox, 18 },
            { ItemID.WaterleafPlanterBox, 18 },
            { ItemID.ShiverthornPlanterBox, 18 },
            { ItemID.FireBlossomPlanterBox, 18 },
            
            // === 绘画 (10-40) ===
            { ItemID.PaintingAcorns, 15 },
            
            // === 活化火焰方块 (80-200) ===
            { ItemID.LivingFireBlock, 80 },
            { ItemID.LivingCursedFireBlock, 180 },
            { ItemID.LivingDemonFireBlock, 80 },
            { ItemID.LivingFrostFireBlock, 80 },
            { ItemID.LivingIchorBlock, 80 },
            { ItemID.LivingUltrabrightFireBlock, 150 },
            
            // === 木桶和容器 ===
            { ItemID.Barrel, 100 },
            
            // === 更多火把品种 ===
            { ItemID.IceTorch, 20 },
            { ItemID.BoneTorch, 22 },
            
            // === 更多书籍和纸质品 ===
            { ItemID.SpellTome, 45 },
            
            // === 旗帜 (20-35) ===
            { ItemID.WorldBanner, 25 },
        };
        /// <summary>
        /// 燃料被消耗时会运行
        /// </summary>
        /// <param name="itemType"></param>
        /// <param name="generator"></param>
        public static void OnAfterFlaming(int itemType, BaseGeneratorTP generator) {
            if (itemType == ItemID.LavaBucket || itemType == ItemID.BottomlessLavaBucket) {
                if (!VaultUtils.isClient) {
                    generator.DropItem(ItemID.EmptyBucket);
                }
            }
        }
    }
}