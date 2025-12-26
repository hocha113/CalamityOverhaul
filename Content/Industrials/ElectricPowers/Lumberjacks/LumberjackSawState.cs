namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Lumberjacks
{
    /// <summary>
    /// 伐木锯机械臂状态枚举
    /// </summary>
    internal enum LumberjackSawState : byte
    {
        Idle = 0,           //待机
        Searching = 1,      //搜索树木
        MovingToTree = 2,   //移动到树木
        Cutting = 3,        //砍伐中
        Returning = 4       //返回待机位置
    }
}
