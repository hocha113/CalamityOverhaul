using System.Collections.Generic;

using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

public sealed partial class SakuraRangedSlashProj
{
    /// <summary>
    /// 手工编排的乱斩，不使用随机数。每个 new() 是一刀，可自由增删、调整顺序。
    /// StartDelay 为原速秒数，默认 2 倍速时实际等待时间减半；Rotation 使用弧度。
    /// XYZ 先决定每刀的空间倾斜，之后再叠加 Spawn 的整体 rotation。
    /// Offset 为整组局部坐标，随整体 rotation/scale 变换；Scale 为每刀局部 XY 缩放。
    /// 首次 AI 时按 StartDelay 稳定排序并快照；客户端和服务端必须使用相同配置。
    /// </summary>
    public List<SakuraRangedSlashCut> Cuts { get; } =
    [
        // 开场：宽斜切。
        new()
        {
            StartDelay = 0f,
            Rotation = Matrix.CreateRotationX(.38f) * Matrix.CreateRotationY(-.52f)
                * Matrix.CreateRotationZ(-.62f),
            Scale = new Vector2(1.15f, 1f)*totalScale,
        },
        // 稍停后以较窄的反向立斩打破对称。
        new()
        {
            StartDelay = .21f,
            Rotation = Matrix.CreateRotationX(-.83f) * Matrix.CreateRotationY(.34f)
                * Matrix.CreateRotationZ(1.18f),
            Offset = new Vector2(-14f, 10f),
            Scale = new Vector2(.9f, 1.12f)*totalScale,
        },
        new()
        {
            StartDelay = .31f,
            Rotation = Matrix.CreateRotationX(.26f) * Matrix.CreateRotationY(1.03f)
                * Matrix.CreateRotationZ(-1.94f),
            Offset = new Vector2(18f, -12f),
            Scale = new Vector2(1.2f, .92f)*totalScale,
        },
        // 第二次停顿，然后短促双切。
        new()
        {
            StartDelay = .53f,
            Rotation = Matrix.CreateRotationX(1.04f) * Matrix.CreateRotationY(-.4f)
                * Matrix.CreateRotationZ(.22f),
            Offset = new Vector2(-9f, -16f),
            Scale = new Vector2(1.08f, .95f)*totalScale,
        },
        new()
        {
            StartDelay = .59f,
            Rotation = Matrix.CreateRotationX(-.57f) * Matrix.CreateRotationY(-.76f)
                * Matrix.CreateRotationZ(2.38f),
            Offset = new Vector2(12f, 16f),
            Scale = new Vector2(.94f, 1.18f)*totalScale,
        },
        // 大斜切收尾，不回到开场角度。
        new()
        {
            StartDelay = .70f,
            Rotation = Matrix.CreateRotationX(-.3f) * Matrix.CreateRotationY(.58f)
                * Matrix.CreateRotationZ(-.96f),
            Scale = new Vector2(1.35f, 1.16f)*totalScale,
        }
    ];

    public static float totalScale => 2;
    
}
