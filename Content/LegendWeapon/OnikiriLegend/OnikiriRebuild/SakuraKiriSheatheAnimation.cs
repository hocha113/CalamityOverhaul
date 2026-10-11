using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>
/// 第二段中场收刀：仅复刻 StandaloneFinisherTestProjectile 的玩家/武器动作。
/// 不复用测试弹幕的生成、输入、特效或生命周期。游戏与离线预览共享本文件。
/// </summary>
internal static class SakuraKiriSheatheAnimation
{
    // 玩家自己的绝对收刀开始时间，与刀光 FinaleStartTime 独立。
    internal static float StartTimeSeconds => 1.1f;
    // 收刀完成后玩家保持最终姿态的时间；不控制刀光爆炸。
    internal static int HoldAfterSheatheFrames => 4;
    // 第二段现身的聚集残影参数；不改变收刀本身的动作曲线。
    internal static int CenterRevealFrames => 28;
    internal static float CenterEchoDistance => 182f;
    internal static float CenterEchoOpacity => .62f;
    // 与测试动画保持一致；frame 从第二段中场现身开始计时。
    internal static int HoldFrames => 18;
    internal static int RotationFrames => 30;
    internal static int RotationHoldFrames => 15;
    internal static int RetractionFrames => 2;
    internal static float SheatheAngleLift => MathHelper.ToRadians(-10f);
    // 正重力、面朝右时的 composite-arm 绝对角度，不是刀尖角度。
    internal static float ArmInitialAngle => MathHelper.ToRadians(-60.5f);
    internal static float ArmRotationEndAngle => MathHelper.ToRadians(-93.5f);
    internal static float ArmRetractionEndAngle => MathHelper.ToRadians(10.5f);
    internal static Vector2 InitialWeaponOffset => new(-75f, 50f);
    internal static float InitialWeaponRotation => 3.9f;
    // 忠实保留测试版：两段位移都是世界/屏幕空间向量，不随朝向、重力再次镜像。
    internal static Vector2 SheatheTravel => new(-15f, 13f);
    internal static float SlowTravelFraction => .19f;
    internal static Vector2 RotationTravel => new(10f, -10f);

    internal static int SlowStartFrame => Math.Max(0, HoldFrames);
    internal static int RotationEndFrame => SlowStartFrame + Math.Max(1, RotationFrames);
    internal static int SnapStartFrame => RotationEndFrame + Math.Max(0, RotationHoldFrames);
    internal static int SheathedFrame => SnapStartFrame + Math.Max(1, RetractionFrames);

    internal static float Frame(float time, SakuraKiriFinisherTimeline timeline)
    {
        float actualDuration = Math.Max(.00001f, timeline.Sheathed - timeline.SheatheStart);
        float normalized = MathHelper.Clamp((time - timeline.SheatheStart) / actualDuration, 0f, 1f);
        return normalized * SheathedFrame;
    }

    internal static float SlowProgress(float frame) => Smooth(MathHelper.Clamp(
        (frame - SlowStartFrame) / Math.Max(1, RotationFrames), 0f, 1f));

    internal static float SnapProgress(float frame) => Smooth(MathHelper.Clamp(
        (frame - SnapStartFrame) / Math.Max(1, RetractionFrames), 0f, 1f));

    internal static float TravelProgress(float frame) => frame < SnapStartFrame
        ? SlowTravelFraction * SlowProgress(frame)
        : MathHelper.Lerp(SlowTravelFraction, 1f, SnapProgress(frame));

    internal static float ArmRotation(float frame, int facing, int gravity)
    {
        float localAngle = frame < SnapStartFrame
            ? MathHelper.Lerp(ArmInitialAngle, ArmRotationEndAngle, SlowProgress(frame))
            : MathHelper.Lerp(ArmRotationEndAngle, ArmRetractionEndAngle, SnapProgress(frame));
        return localAngle * facing * gravity;
    }

    internal static void WeaponPose(Texture2D texture, Vector2 center, float frame, int facing, int gravity,
        out Vector2 position, out Vector2 origin, out float rotation, out SpriteEffects effects)
    {
        effects = (facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None)
            | (gravity < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None);
        origin = new Vector2(texture.Width, texture.Height) * .5f;
        rotation = InitialWeaponRotation * facing * gravity
            + SheatheAngleLift * facing * gravity * SlowProgress(frame);
        Vector2 grip = new(14f, 208f);
        if ((effects & SpriteEffects.FlipHorizontally) != 0) grip.X = texture.Width - grip.X;
        if ((effects & SpriteEffects.FlipVertically) != 0) grip.Y = texture.Height - grip.Y;
        Vector2 readyPosition = center + new Vector2(
            InitialWeaponOffset.X * facing, InitialWeaponOffset.Y * gravity);
        Vector2 gripWorld = readyPosition
            + Vector2.Transform(grip - origin, Matrix.CreateRotationZ(InitialWeaponRotation * facing * gravity));
        Vector2 movement = frame < SnapStartFrame
            ? SheatheTravel * (SlowTravelFraction * SlowProgress(frame)) + RotationTravel * SlowProgress(frame)
            : SheatheTravel * MathHelper.Lerp(SlowTravelFraction, 1f, SnapProgress(frame)) + RotationTravel;
        position = gripWorld + movement - Vector2.Transform(grip - origin, Matrix.CreateRotationZ(rotation));
    }

    private static float Smooth(float value) => value * value * (3f - 2f * value);
}
