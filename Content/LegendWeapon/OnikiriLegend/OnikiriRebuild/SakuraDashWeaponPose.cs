using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>固定斩后姿态；握点和刃轴沿用 SakuraSlashProj 的 本地测试刀标定。</summary>
public static class SakuraDashWeaponPose
{
    public static Vector2 Grip => new(14f, 208f);
    public static Vector2 BladeVector => new Vector2(58f, -218f);
    // 相对人物后方略向下。只调此角度即可同时改变手臂与刀身，不受鼠标影响。
    public static float BackDownAngle => .75f;
    public static float WeaponScale => 1f;

    // 玩家图像已按 facing 镜像，向左的水平基准为 Pi，不能直接再次旋转 Pi。
    // 重力镜像由玩家图层/视图统一处理；此处不额外乘 gravDir。
    public static float PlayerRotation(float projectileRotation, int facing) =>
        MathHelper.WrapAngle(projectileRotation - (facing < 0 ? MathHelper.Pi : 0f));

    public static Vector2 BodyDirection(int facing) =>
        new(-facing * MathF.Cos(BackDownAngle), MathF.Sin(BackDownAngle));

    public static float UprightArmRotation(int facing)
    {
        Vector2 direction = BodyDirection(facing);
        return MathF.Atan2(direction.Y, direction.X) - MathHelper.PiOver2;
    }

    public static float ArmRotation(int facing, int gravity) => UprightArmRotation(facing) * gravity;

    public static void GetSpritePose(Vector2 textureSize, int facing, int gravity,
        out Vector2 origin, out float rotation, out SpriteEffects effects)
    {
        effects = (facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None)
            | (gravity < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None);
        origin = Grip;
        Vector2 blade = BladeVector;
        if (facing < 0) { origin.X = textureSize.X - origin.X; blade.X = -blade.X; }
        if (gravity < 0) { origin.Y = textureSize.Y - origin.Y; blade.Y = -blade.Y; }
        Vector2 direction = BodyDirection(facing);
        direction.Y *= gravity;
        rotation = MathF.Atan2(direction.Y, direction.X) - MathF.Atan2(blade.Y, blade.X);
    }
}
