using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares
{
    /// <summary>
    /// 义体槽位分类，对应12个槽位
    /// </summary>
    internal enum CyberwareSlotCategory
    {
        FrontalCortex,    // 0 额叶皮层
        OcularSystem,     // 1 光学系统
        LeftArm,          // 2 左臂
        Hands,            // 3 手部
        LeftLeg,          // 4 左腿
        Feet,             // 5 足部
        OperatingSystem,  // 6 操作系统
        NervousSystem,    // 7 神经系统
        RightArm,         // 8 右臂
        CirculatorySystem,// 9 循环系统
        Skeleton,         // 10 骨骼
        RightLeg,         // 11 右腿
    }

    /// <summary>
    /// 所有义体物品的基类
    /// </summary>
    internal abstract class BaseCyberware : ModItem
    {
        /// <summary>
        /// 该义体可装入的槽位类别
        /// </summary>
        public virtual CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.OperatingSystem;

        /// <summary>
        /// 该义体占用的容量
        /// </summary>
        public virtual int CapacityCost => 1;

        /// <summary>
        /// 义体装备时触发的效果（子类可覆写）
        /// </summary>
        public virtual void OnEquip(Player player) { }

        /// <summary>
        /// 义体卸载时触发的效果（子类可覆写）
        /// </summary>
        public virtual void OnUnequip(Player player) { }

        /// <summary>
        /// 义体装备期间每帧更新（子类可覆写）
        /// </summary>
        public virtual void UpdateEquipped(Player player) { }

        /// <summary>
        /// 与原版 PostUpdateEquips 同期触发的统计加成入口
        /// <br/>所有需要在装备阶段写入的属性（防御、击退抗性、移速等）请在此覆写
        /// </summary>
        public virtual void PostUpdateEquipped(Player player) { }

        public override void SetDefaults() {
            Item.maxStack = 1;
            Item.width = 32;
            Item.height = 32;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(0, 5, 0, 0);
        }
    }
}
