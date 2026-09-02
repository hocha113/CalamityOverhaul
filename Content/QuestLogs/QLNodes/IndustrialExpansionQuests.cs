using CalamityOverhaul.Content.Industrials.ElectricPowers.Apiaries;
using CalamityOverhaul.Content.Industrials.ElectricPowers.AutoCrafters;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Crushers;
using CalamityOverhaul.Content.Industrials.ElectricPowers.FluidPumps;
using CalamityOverhaul.Content.Industrials.ElectricPowers.HealingStations;
using CalamityOverhaul.Content.Industrials.ElectricPowers.MushroomFarmers;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Recyclers;
using CalamityOverhaul.Content.Industrials.ElectricPowers.ShieldGenerators;
using CalamityOverhaul.Content.Industrials.ElectricPowers.ShimmerTransmuters;
using CalamityOverhaul.Content.Industrials.ElectricPowers.SlimeVats;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Sundials;
using CalamityOverhaul.Content.Industrials.ElectricPowers.TeleportStations;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.CryoTurrets;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.FlameTurrets;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.LaserTurrets;
using CalamityOverhaul.Content.Industrials.ElectricPowers.WeatherControllers;
using CalamityOverhaul.Content.Industrials.Generator.Biomass;
using CalamityOverhaul.Content.Industrials.Generator.MagmaThermal;
using CalamityOverhaul.Content.Industrials.Generator.SolarPanels;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Batterys;
using CalamityOverhaul.Content.Industrials.MaterialFlow.Fluids;
using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    //=========================================================================
    // 工业扩展批的任务书挂点,五条链共 22 节点,全部常驻无门控:
    //   液体链  泵→管道→储罐→岩浆发电机→微光转化槽,挂 BatteryQuest 上方带
    //   加工链  粉碎机→回收机→自动合成台,挂 IndustrialStartQuest 西侧沿 y=-150 带走
    //   农业链  蘑菇农场→养蜂箱→史莱姆槽→生物质发电机,沿 y=0 带向东一字排开
    //   防御链  火焰→冰冻→激光,护盾/治疗侧翼,挂 TeslaTowerQuest
    //   服务链  传送站→日晷→太阳能→电容矩阵→天气机,挂 BatteryQuest 左侧带
    // 坐标一律父级相对 150px 步进,双场景(有/无灾厄)间距审计最小 150px。
    // 工业节点一律不得落到讨伐篇 y≥150 领空:y=150 是灾厄材料带,y=300 主线两套配置
    // 全程满格,任何跨线连线必穿主线节点;无灾厄时主线更短,y=450/600 的原版侧翼与
    // 事件带会前移到 x=750 附近,与"让位下移"的工业节点正面相撞
    // 灾厄件奖励走 AddReward(CWRID.*) 的 ID≤0 守卫,缺席时静默不加
    //=========================================================================

    #region 液体链
    public class FluidPumpQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "液体工程");
            Description = this.GetLocalization(nameof(Description), () => "制作一台抽液泵");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<FluidPump>();
            Position = new Vector2(0, -150);
            AddParent<BatteryQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.Glass, 20);
            AddReward(CWRID.Item_DubiousPlating, 10);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<FluidPump>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class FluidPipelineQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "液脉纵横");
            Description = this.GetLocalization(nameof(Description), () => "制作液体管道");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<FluidPipeline>();
            Position = new Vector2(150, 0);
            AddParent<FluidPumpQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ModContent.ItemType<FluidPipeline>(), 30);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<FluidPipeline>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class FluidTankQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "储液之器");
            Description = this.GetLocalization(nameof(Description), () => "制作一座液体储罐");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<FluidTank>();
            Position = new Vector2(150, 0);
            AddParent<FluidPipelineQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.Glass, 30);
            AddReward(CWRID.Item_MysteriousCircuitry, 10);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<FluidTank>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class MagmaGeneratorQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "岩浆热能");
            Description = this.GetLocalization(nameof(Description), () => "制作一台岩浆热能发电机");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<MagmaThermalGenerator>();
            Position = new Vector2(150, 0);
            AddParent<FluidTankQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.LavaBucket, 3);
            AddReward(CWRID.Item_DubiousPlating, 15);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<MagmaThermalGenerator>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class ShimmerTransmuterQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "微光炼金");
            Description = this.GetLocalization(nameof(Description), () => "制作一台微光转化槽");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<ShimmerTransmuter>();
            Position = new Vector2(150, 0);
            AddParent<MagmaGeneratorQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddObtainObjective();

            AddReward(ItemID.SoulofLight, 10);
            AddReward(CWRID.Item_MysteriousCircuitry, 15);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<ShimmerTransmuter>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
    #endregion

    #region 加工链
    public class CrusherQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "碎石取矿");
            Description = this.GetLocalization(nameof(Description), () => "制作一台矿石粉碎机");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<Crusher>();
            //改挂工业起步西邻 (600,-150):原挂焚化炉 (900,0) 八邻全满,唯一空格 (900,150) 是孤格塞不下三节点链;
            //先前"下移让位"到 (900,450) 后,连线纵穿主线 (900,300)(无灾厄=世纪之花/有灾厄=骷髅王),
            //且无灾厄时回收机/装配台分别与史莱姆皇后、光之女皇同格。
            //粉碎机配方只要铁锡石与电路板,与风力发电机同档,挂起步节点门控不失序
            Position = new Vector2(-150, 0);
            AddParent<IndustrialStartQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.IronOre, 50);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<Crusher>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class RecyclerQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "变废为宝");
            Description = this.GetLocalization(nameof(Description), () => "制作一台回收机");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<Recycler>();
            //加工链沿 y=-150 带向西走,落 (450,-150):与矿工线 y=0 平行 150px,同工业主街东段的排布方式
            Position = new Vector2(-150, 0);
            AddParent<CrusherQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.GoldBar, 10);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<Recycler>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class AutoCrafterQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "自动装配");
            Description = this.GetLocalization(nameof(Description), () => "制作一台自动合成台");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<AutoCrafter>();
            //链尾落 (300,-150):无灾厄时 Omiga 武器列在 x=150(y=-100/-200),斜距 158px,过 150px 线;再往西不可
            Position = new Vector2(-150, 0);
            AddParent<RecyclerQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddObtainObjective();

            AddReward(ItemID.SoulofMight, 10);
            AddReward(CWRID.Item_DubiousPlating, 15);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<AutoCrafter>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
    #endregion

    #region 农业链
    public class MushroomFarmerQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "菌菇培育");
            Description = this.GetLocalization(nameof(Description), () => "制作一台蘑菇农场机");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<MushroomFarmer>();
            //农业链沿 y=0 带向东走:正下方 y=150 带是讨伐篇灾厄侧翼领空
            Position = new Vector2(150, 0);
            AddParent<LifeWeaverQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.MushroomGrassSeeds, 10);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<MushroomFarmer>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class ApiaryQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "甜蜜产业");
            Description = this.GetLocalization(nameof(Description), () => "制作一台养蜂箱");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<Apiary>();
            Position = new Vector2(150, 0);
            AddParent<MushroomFarmerQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.BottledHoney, 15);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<Apiary>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class SlimeVatQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "凝胶量产");
            Description = this.GetLocalization(nameof(Description), () => "制作一台史莱姆培养槽");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<SlimeVat>();
            Position = new Vector2(150, 0);
            AddParent<ApiaryQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.Gel, 99);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<SlimeVat>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class BiomassGeneratorQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "生态闭环");
            Description = this.GetLocalization(nameof(Description), () => "制作一台生物质发电机");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<BiomassGenerator>();
            //挂史莱姆槽东邻 (1950,0),农业链一字排到底:先前"下移让位"到 (1800,450) 后,
            //有灾厄时纵线穿过生命合金 (1800,150) 与拜月教邪教徒 (1800,300),斜线擦过瘟疫使者/石巨人。
            //原三父收束(蘑菇+蜂箱+史莱姆槽)只留史莱姆槽:门控等价——史莱姆槽须先解锁才能完成,
            //而解锁又要求蜂箱完成、蜂箱要求蘑菇完成;穷举周边落点,三父连线在双场景下必与农业链重叠或穿点
            Position = new Vector2(150, 0);
            AddParent<SlimeVatQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.Wood, 100);
            AddReward(CWRID.Item_DubiousPlating, 10);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<BiomassGenerator>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
    #endregion

    #region 防御链
    public class FlameTurretQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "烈焰防线");
            Description = this.GetLocalization(nameof(Description), () => "制作一座火焰喷射塔");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<FlameTurret>();
            //右移让位：灾厄在场时 (150,0) 落点与德雷东动力电池/熔炉任务同格（反馈十三·#58 同族叠点）
            Position = new Vector2(450, 0);
            AddParent<TeslaTowerQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.Gel, 50);
            AddReward(CWRID.Item_DubiousPlating, 10);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<FlameTurret>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class CryoTurretQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "寒霜封锁");
            Description = this.GetLocalization(nameof(Description), () => "制作一座冰冻塔");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<CryoTurret>();
            Position = new Vector2(150, 0);
            AddParent<FlameTurretQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddObtainObjective();

            AddReward(ItemID.FrostCore, 1);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<CryoTurret>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class LaserTurretQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "光速狙击");
            Description = this.GetLocalization(nameof(Description), () => "制作一座激光塔");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<LaserTurret>();
            Position = new Vector2(150, 0);
            AddParent<CryoTurretQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddObtainObjective();

            AddReward(ItemID.CrystalShard, 20);
            AddReward(CWRID.Item_MysteriousCircuitry, 15);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<LaserTurret>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class ShieldGeneratorQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "能量壁垒");
            Description = this.GetLocalization(nameof(Description), () => "制作一座护盾发生器");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<ShieldGenerator>();
            Position = new Vector2(0, -150);
            AddParent<FlameTurretQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddObtainObjective();

            AddReward(ItemID.SoulofLight, 8);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<ShieldGenerator>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class HealStationQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "战地医疗");
            Description = this.GetLocalization(nameof(Description), () => "制作一座治疗站");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<HealStation>();
            Position = new Vector2(150, 0);
            AddParent<ShieldGeneratorQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.HealingPotion, 10);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<HealStation>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
    #endregion

    #region 服务链
    public class TeleportStationQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "传送网络");
            Description = this.GetLocalization(nameof(Description), () => "制作一座传送站");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<TeleportStation>();
            Position = new Vector2(-150, 0);
            AddParent<BatteryQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            //传送站成对才有意义:做一座,奖一座
            AddReward(ModContent.ItemType<TeleportStation>(), 1);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<TeleportStation>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class ElectricSundialQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "拨快日晷");
            Description = this.GetLocalization(nameof(Description), () => "制作一台电动日晷");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<ElectricSundial>();
            Position = new Vector2(-150, 0);
            AddParent<TeleportStationQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.SunplateBlock, 20);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<ElectricSundial>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class SolarPanelQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "向阳而生");
            Description = this.GetLocalization(nameof(Description), () => "制作一块太阳能板");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<SolarPanel>();
            Position = new Vector2(-150, 0);
            AddParent<ElectricSundialQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddObtainObjective();

            AddReward(ItemID.CrystalShard, 15);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<SolarPanel>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class CapacitorMatrixQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "巨量储能");
            Description = this.GetLocalization(nameof(Description), () => "制作一座电容矩阵");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<CapacitorMatrix>();
            Position = new Vector2(0, -150);
            AddParent<SolarPanelQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddObtainObjective();

            AddReward(ItemID.HallowedBar, 8);
            AddReward(CWRID.Item_DubiousPlating, 15);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<CapacitorMatrix>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class WeatherControllerQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "呼风唤雨");
            Description = this.GetLocalization(nameof(Description), () => "制作一台天气控制机");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<WeatherController>();
            Position = new Vector2(150, 0);
            AddParent<CapacitorMatrixQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddObtainObjective();

            AddReward(ItemID.Cloud, 30);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<WeatherController>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
    #endregion
}
