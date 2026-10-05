using System;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>Runtime reader for the weapon corner animation exported by Slash Studio.</summary>
public sealed class SakuraWeaponAnimation
{
    public int Version { get; set; }
    public int Fps { get; set; }
    public int Duration { get; set; }
    public string Interpolation { get; set; } = "transform";
    public SakuraWeaponTexture Weapon { get; set; }
    public SakuraWeaponCalibration EditorCalibration { get; set; }
    public SakuraWeaponKeyframe[] Keyframes { get; set; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static SakuraWeaponAnimation Parse(string json)
    {
        var animation = JsonSerializer.Deserialize<SakuraWeaponAnimation>(json, JsonOptions)
            ?? throw new InvalidOperationException("武器动画文件为空。");
        animation.Validate();
        return animation;
    }

    public Vector2[] Evaluate(float frame)
    {
        if (!float.IsFinite(frame)) frame = 0;
        frame = MathHelper.Clamp(frame, 0, Duration);
        if (frame <= Keyframes[0].Frame) return Keyframes[0].ToVectors();
        var last = Keyframes[^1];
        if (frame >= last.Frame) return last.ToVectors();

        SakuraWeaponKeyframe left = Keyframes[0], right = last;
        for (int i = 1; i < Keyframes.Length; i++)
        {
            if (Keyframes[i].Frame >= frame)
            {
                right = Keyframes[i];
                left = Keyframes[i - 1];
                break;
            }
        }

        float t = (frame - left.Frame) / Math.Max(1f, right.Frame - left.Frame);
        var leftCorners = left.ToVectors();
        var rightCorners = right.ToVectors();
        return string.Equals(Interpolation, "corners", StringComparison.OrdinalIgnoreCase)
            ? InterpolateCornersLinear(leftCorners, rightCorners, t)
            : InterpolateCorners(leftCorners, rightCorners, t);
    }

    public bool TryGetGripPosition(float frame, out Vector2 grip, out Vector2 shoulder)
    {
        grip = shoulder = Vector2.Zero;
        if (Weapon == null || EditorCalibration?.Shoulder == null) return false;
        Vector2[] corners = Evaluate(frame);
        float u = Weapon.Grip[0] / Weapon.Size[0];
        float v = Weapon.Grip[1] / Weapon.Size[1];
        grip = corners[0] + (corners[1] - corners[0]) * u + (corners[3] - corners[0]) * v;
        shoulder = EditorCalibration.Shoulder.Vector;
        return true;
    }

    public bool TryGetArmRotation(float frame, int facingDirection, int gravityDirection, out float rotation)
    {
        rotation = 0;
        if (!TryGetGripPosition(frame, out Vector2 grip, out Vector2 shoulder)) return false;
        Vector2 arm = new((grip.X - shoulder.X) * facingDirection,
            (grip.Y - shoulder.Y) * gravityDirection);
        if (arm.LengthSquared() <= 0.000001f) return false;
        rotation = MathF.Atan2(arm.Y, arm.X) - MathHelper.PiOver2 * gravityDirection;
        return true;
    }

    private readonly struct CornerPose
    {
        public CornerPose(Vector2 center, float rotation, Vector2[] local)
        {
            Center = center;
            Rotation = rotation;
            Local = local;
        }

        public Vector2 Center { get; }
        public float Rotation { get; }
        public Vector2[] Local { get; }
    }

    // Separate translation, orientation, and the remaining affine shape before interpolation.
    private static Vector2[] InterpolateCorners(Vector2[] leftCorners, Vector2[] rightCorners, float t)
    {
        var left = DecomposeCorners(leftCorners);
        var right = DecomposeCorners(rightCorners);
        var rotation = left.Rotation + WrapAngle(right.Rotation - left.Rotation) * t;
        var center = Vector2.Lerp(left.Center, right.Center, t);
        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);
        var result = new Vector2[4];
        for (var i = 0; i < result.Length; i++)
        {
            var local = Vector2.Lerp(left.Local[i], right.Local[i], t);
            result[i] = center + new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
        }
        return result;
    }

    private static Vector2[] InterpolateCornersLinear(Vector2[] leftCorners, Vector2[] rightCorners, float t)
    {
        var result = new Vector2[4];
        for (var i = 0; i < result.Length; i++)
            result[i] = Vector2.Lerp(leftCorners[i], rightCorners[i], t);
        return result;
    }

    private static CornerPose DecomposeCorners(Vector2[] corners)
    {
        var center = (corners[0] + corners[1] + corners[2] + corners[3]) / 4f;
        var axis = corners[1] - corners[0];
        var rotation = axis.LengthSquared() > 0.000001f ? MathF.Atan2(axis.Y, axis.X) : 0f;
        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);
        var local = new Vector2[4];
        for (var i = 0; i < local.Length; i++)
        {
            var delta = corners[i] - center;
            local[i] = new Vector2(delta.X * cos + delta.Y * sin, -delta.X * sin + delta.Y * cos);
        }
        return new CornerPose(center, rotation, local);
    }

    private static float WrapAngle(float angle) => MathF.Atan2(MathF.Sin(angle), MathF.Cos(angle));

    private void Validate()
    {
        if (Version != 1 || Fps is < 1 or > 120 || Duration is < 1 or > 600 ||
            Keyframes is not { Length: > 0 })
            throw new InvalidOperationException("武器动画版本或时间轴无效。");
        if (Weapon != null && !Weapon.IsValid())
            throw new InvalidOperationException("武器贴图尺寸或握持点无效。");
        if (EditorCalibration?.Shoulder is { } shoulder &&
            (!float.IsFinite(shoulder.X) || !float.IsFinite(shoulder.Y) ||
             Math.Abs(shoulder.X) > 100000 || Math.Abs(shoulder.Y) > 100000 ||
             !float.IsFinite(shoulder.Radius) || shoulder.Radius < 1 || shoulder.Radius > 100000))
            throw new InvalidOperationException("武器动画肩膀参考圈无效。");
        int previous = -1;
        for (int i = 0; i < Keyframes.Length; i++)
        {
            var key = Keyframes[i];
            if (key == null || key.Frame < 0 || key.Frame > Duration || key.Frame <= previous ||
                key.WeaponCorners is not { Length: 4 })
                throw new InvalidOperationException("武器动画关键帧无效。");
            foreach (var point in key.WeaponCorners)
            {
                if (point == null || !float.IsFinite(point.X) || !float.IsFinite(point.Y) ||
                    Math.Abs(point.X) > 100000 || Math.Abs(point.Y) > 100000)
                    throw new InvalidOperationException("武器动画坐标无效。");
            }
            // Older exports allowed perspective quads. Keep their first, second and
            // fourth corners and derive the opposite corner as a parallelogram.
            key.NormalizeParallelogram();
            previous = key.Frame;
        }
        if (Keyframes[0].Frame != 0) throw new InvalidOperationException("武器动画必须从第 0 帧开始。");
    }
}

public sealed class SakuraWeaponTexture
{
    public string Texture { get; set; }
    public float[] Size { get; set; }
    public float[] Grip { get; set; }

    public bool IsValid() => Size is { Length: 2 } && Grip is { Length: 2 } &&
        float.IsFinite(Size[0]) && float.IsFinite(Size[1]) &&
        Size[0] > 0 && Size[1] > 0 && Size[0] <= 100000 && Size[1] <= 100000 &&
        float.IsFinite(Grip[0]) && float.IsFinite(Grip[1]) &&
        Grip[0] >= 0 && Grip[0] <= Size[0] && Grip[1] >= 0 && Grip[1] <= Size[1];
}

public sealed class SakuraWeaponCalibration
{
    public SakuraWeaponShoulder Shoulder { get; set; }
}

public sealed class SakuraWeaponShoulder
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Radius { get; set; }
    public Vector2 Vector => new(X, Y);
}

public sealed class SakuraWeaponKeyframe
{
    public int Frame { get; set; }
    public SakuraWeaponPoint[] WeaponCorners { get; set; } = [];

    public void NormalizeParallelogram()
    {
        var first = WeaponCorners[0];
        var second = WeaponCorners[1];
        var fourth = WeaponCorners[3];
        WeaponCorners[2] = new SakuraWeaponPoint
        {
            X = second.X + fourth.X - first.X,
            Y = second.Y + fourth.Y - first.Y
        };
    }

    public Vector2[] ToVectors()
    {
        var result = new Vector2[4];
        for (int i = 0; i < result.Length; i++) result[i] = WeaponCorners[i].Vector;
        return result;
    }
}

public sealed class SakuraWeaponPoint
{
    public float X { get; set; }
    public float Y { get; set; }
    public Vector2 Vector => new(X, Y);
}
