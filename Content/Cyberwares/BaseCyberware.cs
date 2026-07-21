using CalamityOverhaul.Content.Cyberwares.Skills;
using CalamityOverhaul.Content.TimeFreezes;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares
{
    /// <summary>义体槽位，对应 12 个装备位</summary>
    internal enum CyberwareSlotCategory
    {
        FrontalCortex,    //0 额叶皮层
        OcularSystem,     //1 光学系统
        LeftArm,          //2 左臂
        Hands,            //3 手部
        LeftLeg,          //4 左腿
        Feet,             //5 足部
        OperatingSystem,  //6 操作系统
        NervousSystem,    //7 神经系统
        RightArm,         //8 右臂
        CirculatorySystem,//9 循环系统
        Skeleton,         //10 骨骼
        RightLeg,         //11 右腿
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

        /// <summary>PostUpdateEquips 同期属性加成</summary>
        public virtual void PostUpdateEquipped(Player player) { }

        /// <summary>主动技能，null 不进雷达；建议 static 单例，状态放 ModPlayer</summary>
        public virtual CyberwareSkillBase ActiveSkill => null;

        /// <summary>按 <see cref="TimeGear"/> 推进倒计时整帧</summary>
        public static void TickFrameDown(ref int frames, ref float carry, float scale = -1f)
            => TimeGear.ConsumeFrames(ref frames, ref carry, scale);

        /// <summary>按 <see cref="TimeGear"/> 本帧正计时整帧数</summary>
        public static int TickFrameUp(ref float carry, float scale = -1f)
            => TimeGear.PullFrameAdvance(ref carry, scale);

        public override void SetDefaults() {
            Item.maxStack = 1;
            Item.width = 32;
            Item.height = 32;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(0, 5, 0, 0);
        }
    }
}
