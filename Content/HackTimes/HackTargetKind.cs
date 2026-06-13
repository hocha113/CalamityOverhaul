using System;

namespace CalamityOverhaul.Content.HackTimes
{
    /// <summary>协议支持目标类型，Flags 可多选</summary>
    [Flags]
    internal enum HackTargetKind
    {
        //无目标
        None = 0,
        //NPC目标
        Npc = 1,
        //物块目标
        Tile = 2,
        //灵异 Actor
        Wraith = 4,
        //可骇入炮台 Actor
        Turret = 8,
        //信号塔 Actor
        SignalTower = 16,
        //弹幕实体
        Projectile = 32,
        //液体格子
        Water = 64,
        //掉落物实体
        Item = 128,
    }
}
