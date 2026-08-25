using CalamityOverhaul.Content.Industrials.ElectricPowers.GridSwitches;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Sensors;
using CalamityOverhaul.Content.Industrials.ElectricPowers.WireInterfaces;
using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    //=========================================================================
    // 自动化控制层的任务书挂点,挂 ThrowerQuest(物流线尖端)下游:
    //   接口器(1800,-150) → 传感器(1950,-150) → 总闸(2100,-150),沿 y=-150 向东横排。
    // 三节点全部常驻无门控,150px 步进;南侧农业链尾(1800,0)与灾厄侧翼带保持 ≥150px
    //=========================================================================

    public class WireInterfaceQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "机关搭桥");
            Description = this.GetLocalization(nameof(Description), () => "制作机关接口器");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<WireInterface>();
            Position = new Vector2(150, 0);
            AddParent<ThrowerQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.Wire, 50);
            AddReward(CWRID.Item_MysteriousCircuitry, 10);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<WireInterface>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class SensorQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "电子哨眼");
            Description = this.GetLocalization(nameof(Description), () => "制作一台多模式传感器");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<Sensor>();
            Position = new Vector2(150, 0);
            AddParent<WireInterfaceQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.Lens, 5);
            AddReward(CWRID.Item_DubiousPlating, 10);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<Sensor>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    public class GridSwitchQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "电网调度");
            Description = this.GetLocalization(nameof(Description), () => "制作一台电网总闸");

            IconType = QuestIconType.Item;
            IconItemType = ModContent.ItemType<GridSwitch>();
            Position = new Vector2(150, 0);
            AddParent<SensorQuest>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            AddObtainObjective();

            AddReward(ItemID.Actuator, 20);
            AddReward(CWRID.Item_MysteriousCircuitry, 10);
        }

        public override void UpdateByPlayer() {
            bool hasItem = Main.LocalPlayer.InquireItem(ModContent.ItemType<GridSwitch>()) > 0;
            Objectives[0].CurrentProgress = hasItem ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
