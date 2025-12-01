namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Collectors
{
    /// <summary>
    /// 机械臂状态枚举
    /// </summary>
    internal enum ArmState : byte
    {
        Idle = 0,           //待机
        Searching = 1,      //搜索目标
        MovingToItem = 2,   //移动到物品
        Grasping = 3,       //抓取物品
        MovingToChest = 4,  //移动到箱子
        Depositing = 5      //存放物品
    }
}
