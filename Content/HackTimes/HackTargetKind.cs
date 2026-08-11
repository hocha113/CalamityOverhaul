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
        //保留原灵异目标位，避免其余 Flags 改号
        ReservedWraith = 4,
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
        //Boss 部件（体节/肢体），2026-08 扩展批预留
        BossPart = 256,
        //玩家自身义体组（自我目标）
        SelfRig = 512,
        //容器（箱子等）
        Container = 1024,
        //世界状态（昼夜/天气/重力）
        World = 2048,
        //敌对玩家（PvP 骇入）。效果管线与其他种类相反：权威端不施加，
        //走服务端授予 → 防守方本机结算（PvP/PlayerHackNet 的 DefenderApply 管线）
        Player = 4096,
        //线上格式 WriteTarget/TryReadTarget 已按 ushort 收发 kind（HACK32 批），
        //超过 128 的位可以直接启用；再往上扩要先看 ushort 的 65535 上限
    }
}
