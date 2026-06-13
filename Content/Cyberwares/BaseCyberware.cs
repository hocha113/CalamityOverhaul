using CalamityOverhaul.Content.Cyberwares.Skills;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares
{
    /// <summary>义体槽位，对应 12 个装备位</summary>
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

    /// <summary>义体物品基类</summary>
    internal abstract class BaseCyberware : ModItem
    {
        /// <summary>可装入槽位类别</summary>
        public virtual CyberwareSlotCategory SlotCategory => CyberwareSlotCategory.OperatingSystem;

        /// <summary>占用容量</summary>
        public virtual int CapacityCost => 1;

        /// <summary>装备时回调</summary>
        public virtual void OnEquip(Player player) { }

        /// <summary>卸载时回调</summary>
        public virtual void OnUnequip(Player player) { }

        /// <summary>装备期间每帧更新</summary>
        public virtual void UpdateEquipped(Player player) { }

        /// <summary>
        /// PostUpdateEquips 同期统计加成入口
        /// <br/>防御/击退/移速等属性在此覆写
        /// </summary>
        public virtual void PostUpdateEquipped(Player player) { }

        /// <summary>
        /// 主动技能描述符，null 不参与雷达
        /// <br/>建议 static 单例；运行时状态留在 ModPlayer/ModSystem
        /// </summary>
        public virtual CyberwareSkillBase ActiveSkill => null;

        public override void SetDefaults() {
            Item.maxStack = 1;
            Item.width = 32;
            Item.height = 32;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(0, 5, 0, 0);
        }
    }
}
