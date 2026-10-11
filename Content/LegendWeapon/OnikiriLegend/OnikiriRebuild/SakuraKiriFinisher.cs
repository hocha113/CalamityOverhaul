using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

internal readonly record struct SakuraKiriFinisherTimeline(
    float ReappearStart, float Arrival, float FlyOutFadeStart, float FlyOutEnd,
    float SheatheStart, float SnapStart, float Sheathed, float Explosion);

// Pure animation data shared by the game and FNA preview; no movement of the actual player.
internal static class SakuraKiriFinisher
{
    // 0 = begin returning with the selected line's initial length growth, not its finale pulse.
    internal static float ReappearAtSlashProgress => 0.8f;
    internal static float ReappearDistance => 450f;
    // 第一段穿过中场后，沿同方向继续飞出的距离（像素），不是最终落点。
    internal static float FlyPastDistance => 650f;
    // 整段掠过共用一条先快后慢的曲线：1 = 匀速，2 = 平滑减速，3 = 更明显的减速。
    // 限制在 1–8；穿过中场的时点不变，增强减速会延长后半段，但不推迟第二段。
    internal static float FlyThroughEasePower => 3f;
    // 飞出末尾的整体淡出时长；不超过穿过中场后的飞行时间。
    internal static int FlyOutFadeFrames => 10;
    // 第二段动作参数已独立到 SakuraKiriSheatheAnimation，避免与第一段掠过混用。
    internal static int ExplosionDelayFrames => 2;
    internal static int EchoFadeFrames => 6;
    internal static float EchoSpacing => 28f;
    // ===== 末段现身姿态配置（角度单位：度；屏幕 X 向右、Y 向下） =====
    // 回场和收刀期间固定面朝左，不跟随起手方向或鼠标。
    internal const int Facing = -1;
    internal const int CloseoutFacing = 1;

    // The fly-through is left-facing; the entire center sheathe is right-facing.
    internal static int FacingAtCloseout(float time, SakuraKiriFinisherTimeline timeline) =>
        time + .00001f >= timeline.SheatheStart ? CloseoutFacing : Facing;
    // 相对水平向右的上升角；限制在 1–89 度，保证始终从左下到右上。
    internal static float ReappearRiseDegrees => 5f;
    // 负值让头部向左倾；第二段中场收刀改用直立姿态。只旋转绘制，不改变实际位置。
    internal static float ReappearBodyTiltDegrees => -12f;
    // 手臂朝向：0 = 右，90 = 下。面朝左时，右侧就是身体后方。
    internal static float ReappearArmDegrees => 25f;
    // 刀尖朝向，使用相同角度约定；与手臂角度独立，刀柄始终贴住后伸的持刀手。
    internal static float ReappearBladeDegrees => 15f;
    private static Vector2 WeaponGrip => new(14f, 208f);
    private static Vector2 WeaponBladeVector => new(58f, -218f);

    internal static SakuraKiriFinisherTimeline Timeline(float slashStart, float growTime,
        float sheatheStart = 0f, float sheatheEnd = 0f, float explosionTime = 0f)
    {
        float grow = Math.Max(.001f, growTime);
        float arrival = slashStart + grow;
        float start = slashStart + grow * MathHelper.Clamp(ReappearAtSlashProgress, 0f, .99f);
        sheatheStart = Math.Max(arrival, sheatheStart);
        // Arrival 现在只表示「穿过中场」。第一段不能推迟第二段原有收刀/爆发时点。
        float available = Math.Max(0f, sheatheStart - arrival - 1f / 60f);
        float remainingFlight = Math.Max(0f, FlightDuration(start, arrival) - (arrival - start));
        float flyOutEnd = arrival + Math.Min(available, remainingFlight);
        float fadeStart = Math.Max(arrival, flyOutEnd - Math.Max(1, FlyOutFadeFrames) / 60f);
        bool syncedSheathe = sheatheEnd > sheatheStart + .00001f;
        float sheatheDuration = syncedSheathe
            ? sheatheEnd - sheatheStart
            : SakuraKiriSheatheAnimation.SheathedFrame / 60f;
        float phaseScale = sheatheDuration / (SakuraKiriSheatheAnimation.SheathedFrame / 60f);
        float snap = sheatheStart + SakuraKiriSheatheAnimation.SnapStartFrame / 60f * phaseScale;
        float sheathed = sheatheStart + sheatheDuration;
        // An explicit explosion time belongs to the blade-light timeline and may be
        // before or after the independent player sheathe completes.
        float explosion = explosionTime > 0f
            ? explosionTime
            : sheathed + Math.Max(0, ExplosionDelayFrames) / 60f;
        return new(start, arrival, fadeStart, flyOutEnd, sheatheStart, snap, sheathed, explosion);
    }

    internal static float Progress(float time, float start, float end) =>
        time + .00001f >= end ? 1f
            : MathHelper.Clamp((time - start) / Math.Max(.00001f, end - start), 0f, 1f);

    internal static Vector2 ReturnDirection()
    {
        float angle = MathHelper.ToRadians(MathHelper.Clamp(ReappearRiseDegrees, 1f, 89f));
        return new Vector2(MathF.Cos(angle), -MathF.Sin(angle));
    }

    private static float FlightEasePower => MathHelper.Clamp(FlyThroughEasePower, 1f, 8f);

    private static float FlightDuration(float start, float arrival)
    {
        float entranceDuration = Math.Max(.00001f, arrival - start);
        double incoming = Math.Max(0f, ReappearDistance);
        double outgoing = Math.Max(0f, FlyPastDistance);
        if (incoming <= 0d) return entranceDuration;
        // 反解 EaseOut(t) = 入场距离 / 总距离，固定原来的过中场时点。
        // 前后段绝不能各做一次 EaseOut，否则过中场时会先停住、再突然加速。
        double crossingProgress = 1d - Math.Pow(outgoing / (incoming + outgoing), 1d / FlightEasePower);
        return (float)(entranceDuration / Math.Max(.00000001d, crossingProgress));
    }

    internal static Vector2 BodyOffset(float time, SakuraKiriFinisherTimeline timeline, Vector2 direction)
    {
        // 消失以后重置绘制偏移，第二段在原来的中场位置现身，不移动玩家实体。
        if (time >= timeline.FlyOutEnd) return Vector2.Zero;
        double incoming = Math.Max(0f, ReappearDistance);
        if (incoming <= 0d) return Vector2.Zero;
        double duration = FlightDuration(timeline.ReappearStart, timeline.Arrival);
        double progress = Math.Clamp((time - timeline.ReappearStart) / duration, 0d, 1d);
        double eased = 1d - Math.Pow(1d - progress, FlightEasePower);
        double distance = -incoming + (incoming + Math.Max(0f, FlyPastDistance)) * eased;
        // 淡出期间仍沿这条曲线前进，只在完全不可见后重置偏移。
        return direction * (float)distance;
    }

    internal static float BodyOpacity(float time, SakuraKiriFinisherTimeline timeline) =>
        time < timeline.SheatheStart ? 0f : CenterRevealOpacity(time, timeline);

    internal static float CenterRevealProgress(float time, SakuraKiriFinisherTimeline timeline) =>
        Progress(time, timeline.SheatheStart,
            timeline.SheatheStart + Math.Max(1, SakuraKiriSheatheAnimation.CenterRevealFrames) / 60f);

    internal static float CenterRevealOpacity(float time, SakuraKiriFinisherTimeline timeline) =>
        Smooth(CenterRevealProgress(time, timeline));

    internal static float CenterEchoOffset(float time, SakuraKiriFinisherTimeline timeline) =>
        Math.Max(0f, SakuraKiriSheatheAnimation.CenterEchoDistance)
        * (1f - CenterRevealProgress(time, timeline));

    internal static float CenterEchoOpacity(float time, SakuraKiriFinisherTimeline timeline) =>
        Math.Max(0f, SakuraKiriSheatheAnimation.CenterEchoOpacity)
        * (1f - Smooth(CenterRevealProgress(time, timeline)));

    internal static float EchoStrength(float time, SakuraKiriFinisherTimeline timeline)
    {
        if (time < timeline.ReappearStart || time >= timeline.FlyOutEnd) return 0f;
        // 残影跟随第一段一起飞出，再收拢淡出，不留在中场，不带入第二段。
        return 1f - Smooth(Progress(time, timeline.FlyOutFadeStart,
            Math.Min(timeline.FlyOutEnd, timeline.FlyOutFadeStart + Math.Max(1, EchoFadeFrames) / 60f)));
    }

    // 用于时间轴检查/显示；第二段的手臂与武器直接使用独立动作模型，不混合飞行姿态。
    internal static float SheathePoseProgress(float time, SakuraKiriFinisherTimeline timeline) =>
        SakuraKiriSheatheAnimation.TravelProgress(SakuraKiriSheatheAnimation.Frame(time, timeline));

    internal static float BodyRotation() => MathHelper.ToRadians(ReappearBodyTiltDegrees);
    internal static float UprightArmRotation() => MathHelper.ToRadians(ReappearArmDegrees) - MathHelper.PiOver2;

    internal static void WeaponPose(Texture2D texture, Vector2 gripPosition, int gravity,
        out Vector2 position, out Vector2 origin, out float rotation, out SpriteEffects effects)
    {
        int facing = Facing;
        float bladeAngle = MathHelper.ToRadians(ReappearBladeDegrees);
        Vector2 grip = WeaponGrip;
        Vector2 blade = WeaponBladeVector;
        if (facing < 0) { grip.X = texture.Width - grip.X; blade.X = -blade.X; }
        if (gravity < 0) { grip.Y = texture.Height - grip.Y; blade.Y = -blade.Y; }
        Vector2 desiredBlade = new(MathF.Cos(bladeAngle), MathF.Sin(bladeAngle) * gravity);
        rotation = MathF.Atan2(desiredBlade.Y, desiredBlade.X) - MathF.Atan2(blade.Y, blade.X);
        origin = new Vector2(texture.Width, texture.Height) * .5f;
        // Return a center-origin pose so the padded glow mask stays exactly on the sword.
        position = gripPosition - Vector2.Transform(grip - origin, Matrix.CreateRotationZ(rotation));
        effects = (facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None)
            | (gravity < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None);
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);
}
