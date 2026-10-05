using System;
using System.Collections.Generic;



using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>
/// 超必杀的玩家动作编排：蓄力、残影消失、空间斩、末段回场和收刀。
/// 玩家的实际位置始终不动，移动只存在于绘制数据中。
/// </summary>
public sealed class SakuraKiriPlayerProj : ModProjectile
{
    public override string Texture => SakuraFinaleAssets.TexturePath + "SakuraKiriPlayerProj";

    public int ChargeFrames => 60;
    public int DisappearFrames => 8;
    public int ReappearFrames => (int)MathF.Round((FinisherTimeline.FlyOutEnd - FinisherTimeline.ReappearStart) * 60f);
    public int SheatheFrames => SakuraKiriSheatheAnimation.SheathedFrame;
    public int FinaleHoldFrames => 4;

    public float DisappearDistance => 200f;

    // 末段角度、倾斜、手臂/刀尖方向统一在 SakuraKiriFinisher 顶部配置。
    public float ReappearDistance => SakuraKiriFinisher.ReappearDistance;
    public float FinaleStartSeconds => 1.8f;

    // Owner-client-only charge feedback. Keep tuning values in expression-bodied properties.
    public float ChargeShakeStartProgress => .08f;
    public float ChargeShakeMaxStrength => 4f;
    public float ChargeShakeRisePower => 1.6f;
    public int ChargeShakeIntervalFrames => 6;
    public float ChargeShakeCyclesPerSecond => 15f;

    public float SlashPunchStrength => 3.5f;

    // Applied once at the final particle explosion, never during line expansion.
    public float FinaleSlashPunchStrength => 4.5f;
    public float HeavySlashPunchMultiplier => 10f;
    public int SlashPunchFrames => 8;
    public float SlashPunchCyclesPerSecond => 12f;

    private int facing = 1;
    private int sourceItemType;
    private bool started;
    private bool slashStarted;
    private bool actionEnded;
    private bool finaleParticlesReleased;
    private Vector2 slashCenter;
    private float slashTime;
    private int slashFrames;
    private float finaleBurstTime = -1f;
    private readonly SakuraKiriRenderer renderer = new();
    private readonly List<float> finaleStartDelays = [];
    private readonly HashSet<SakuraKiriLine> punchedSlashes = [];

    private Player Owner => Projectile.owner >= 0 && Projectile.owner < Main.maxPlayers
        ? Main.player[Projectile.owner]
        : null;

    private int SlashStartFrame => ChargeFrames + DisappearFrames;

    public int Segments
    {
        get => segments;
        set => segments = Math.Clamp(value, 2, 512);
    }

    public float Length { get; set; } = 1600f;
    public SakuraKiriLayer BlackLayer { get; } = SakuraKiriLayer.CreateBlack();
    public SakuraKiriLayer RedLayer { get; } = SakuraKiriLayer.CreateRed();
    public SakuraKiriLayer WhiteLayer { get; } = SakuraKiriLayer.CreateWhite();

    public List<SakuraKiriLine> Lines { get; } =
    [
        new()
        {
            LengthScale = 1.92f,
            WidthScale = 0.6f,
            StartDelay = .05f,
            Rotation = Matrix.CreateRotationY(-0.62f) * Matrix.CreateRotationZ(0.36f)
        },
        new()
        {
            LengthScale = .62f,
            WidthScale = 0.3f,
            StartDelay = .1f,
            Offset = new Vector2(-300, -200),
            Rotation = Matrix.CreateRotationY(0.32f) * Matrix.CreateRotationZ(1.36f)
        },
        new()
        {
            LengthScale = .92f,
            StartDelay = .15f,
            WidthScale = 0.7f,
            Offset = new Vector2(-100, 400),
            Rotation = Matrix.CreateRotationY(0.92f) * Matrix.CreateRotationZ(-1.48f)
        },
        new()
        {
            LengthScale = 1.32f,
            StartDelay = .2f,
            Offset = new Vector2(-800, 200),
            Rotation = Matrix.CreateRotationY(-0.72f) * Matrix.CreateRotationZ(1.0f)
        },
        new()
        {
            LengthScale = 1.82f,
            StartDelay = .23f,
            Offset = new Vector2(800, -200),
            Rotation = Matrix.CreateRotationY(-0.72f) * Matrix.CreateRotationZ(-1.3f)
        },
        new()
        {
            LengthScale = 1.82f,
            StartDelay = .26f,
            Offset = new Vector2(400, -200),
            Rotation = Matrix.CreateRotationY(-0.32f) * Matrix.CreateRotationZ(-1.8f)
        },
        new()
        {
            LengthScale = 2.92f,
            HeavyCameraPunch = true,
            WidthScale = 1.8f,
            StartDelay = .3f,
            Offset = new Vector2(-200, 0),
            Rotation = Matrix.CreateRotationY(0.42f) * Matrix.CreateRotationZ(-0.1f),
            LengthGrowTime = 0.3f
        },
        new()
        {
            // 细粉线延长 -> 白色高亮爆发 -> 收缩为细粉线。
            LengthScale = 0.92f,
            WidthScale = 0.7f,
            StartDelay = .0f,
            Offset = new Vector2(100, -000),
            Rotation = Matrix.CreateRotationX(0.22f) * Matrix.CreateRotationY(-0.3f) * Matrix.CreateRotationZ(-0.5f)
        }
    ];

    private int segments = 300;

    /// <summary>当前空间斩演出的已播放时间，单位为秒。</summary>
    public float ElapsedTime => slashTime;

    /// <summary>收尾阶段开始时间，单位为秒。</summary>
    public float FinaleStartTime => FinaleStartSeconds;

    /// <summary>收尾开始后释放中心粒子与爆发效果的延迟，单位为秒；可调。</summary>
    public float FinaleParticleDelay => .4f;

    /// <summary>收尾阶段光晕膨胀时间，单位为秒。</summary>
    public float FinaleExpandTime => 0.35f;

    /// <summary>收尾阶段光晕收缩时间，单位为秒。</summary>
    public float FinaleShrinkTime => .08f;

    /// <summary>最后粒子爆发光晕的持续时间。</summary>
    public float FinaleHaloDuration => .45f;

    /// <summary>最后粒子爆发光晕的最大直径，单位为世界像素。</summary>
    public float FinaleHaloSize => 4000f;

    /// <summary>最后粒子爆发光晕的噪声流动速度。</summary>
    public float FinaleHaloFlowSpeed => 4.5f;

    /// <summary>最后粒子爆发光晕的强度。</summary>
    public float FinaleHaloIntensity => 5.15f;

    /// <summary>最后粒子爆发光晕颜色。</summary>
    public Color FinaleHaloColor => new(255, 48, 60, 255);

    /// <summary>收尾粒子与刀光爆发的绝对时间；由原始独立延迟决定。</summary>
    public float FinaleParticleTime => FinaleStartTime + Math.Max(0f, FinaleParticleDelay);

    private float FinaleDelayForLine(SakuraKiriLine target)
    {
        if (target == null) return 0f;
        PrepareFinaleStartDelays();
        int visibleIndex = 0;
        foreach (SakuraKiriLine line in Lines)
        {
            if (!line.Visible) continue;
            int reverseIndex = finaleStartDelays.Count - 1 - visibleIndex++;
            if (ReferenceEquals(line, target))
                return finaleStartDelays[reverseIndex] - finaleStartDelays[0];
        }

        return 0f;
    }

    internal SakuraKiriLine FinisherLine
    {
        get
        {
            SakuraKiriLine selected = null;
            foreach (SakuraKiriLine line in Lines)
            {
                if (!line.Visible || line.LengthScale <= 0f || line.WidthScale <= 0f) continue;
                if (selected == null || (line.HeavyCameraPunch && !selected.HeavyCameraPunch)
                                     || (line.HeavyCameraPunch == selected.HeavyCameraPunch
                                         && line.StartDelay + line.LengthGrowTime >
                                         selected.StartDelay + selected.LengthGrowTime))
                    selected = line;
            }

            return selected;
        }
    }

    internal SakuraKiriFinisherTimeline FinisherTimeline
    {
        get
        {
            SakuraKiriLine line = FinisherLine;
            // Player sheathing and blade-light explosion use separate tunable clocks.
            float playerStart = SakuraKiriSheatheAnimation.StartTimeSeconds;
            float playerEnd = playerStart + SakuraKiriSheatheAnimation.SheathedFrame / 60f;
            return SakuraKiriFinisher.Timeline(line?.StartDelay ?? FinaleStartTime,
                line?.LengthGrowTime ?? (FinaleExpandTime + FinaleShrinkTime),
                playerStart, playerEnd, FinaleParticleTime);
        }
    }

    public bool IsFinishingPose => DrawState is SakuraKiriDrawState.Reappear or SakuraKiriDrawState.Sheathe;
    public bool IsCenterSheathing => DrawState == SakuraKiriDrawState.Sheathe;
    public float CenterSheatheFrame => SakuraKiriSheatheAnimation.Frame(ElapsedTime, FinisherTimeline);
    public float FinisherBodyRotation => IsCenterSheathing ? 0f : SakuraKiriFinisher.BodyRotation();
    public float FinisherArmRotation => SakuraKiriFinisher.UprightArmRotation();
    public float FinisherEchoStrength => SakuraKiriFinisher.EchoStrength(ElapsedTime, FinisherTimeline);
    public float CenterRevealProgress => SakuraKiriFinisher.CenterRevealProgress(ElapsedTime, FinisherTimeline);
    public float CenterEchoOffset => SakuraKiriFinisher.CenterEchoOffset(ElapsedTime, FinisherTimeline);
    public float CenterEchoOpacity => SakuraKiriFinisher.CenterEchoOpacity(ElapsedTime, FinisherTimeline);

    /// <summary>最后一条收尾空间斩开始的时间，单位为秒。</summary>
    public float LastFinaleSlashTime
    {
        get
        {
            float firstDelay = float.MaxValue;
            float lastDelay = 0f;
            foreach (SakuraKiriLine line in Lines)
            {
                if (!line.Visible) continue;
                firstDelay = Math.Min(firstDelay, line.StartDelay * .5f);
                lastDelay = Math.Max(lastDelay, line.StartDelay * .5f);
            }

            return FinaleStartTime + (firstDelay == float.MaxValue ? 0f : lastDelay - firstDelay);
        }
    }

    /// <summary>可配置的屏幕空间回场方向，固定左下到右上，不随起手朝向、斩线或重力翻转。</summary>
    public Vector2 FinaleSlashDirection => SakuraKiriFinisher.ReturnDirection();

    public bool IsChargeVisible => Projectile.active && Projectile.ai[0] < ChargeFrames;
    public bool IsActionVisible => IsChargeVisible || DrawState != SakuraKiriDrawState.Hidden;

    public SakuraKiriDrawState DrawState
    {
        get
        {
            if (Projectile.ai[0] < ChargeFrames)
                return SakuraKiriDrawState.Charge;
            if (Projectile.ai[0] < SlashStartFrame)
            {
                return SakuraKiriDrawState.Disappear;
            }

            float elapsed = ElapsedTime;
            SakuraKiriFinisherTimeline timeline = FinisherTimeline;
            if (elapsed < timeline.ReappearStart) return SakuraKiriDrawState.Hidden;
            // 第一段只保留刀光，玩家本体/武器不再绘制掠过中场的效果.
            if (elapsed < timeline.FlyOutEnd)
                return SakuraKiriDrawState.Hidden;
            if (elapsed < timeline.SheatheStart)
                return SakuraKiriDrawState.Hidden;
            // 第二段：仍使用原来的中场收刀、停顿及爆发时间轴。
            float playerEnd = timeline.Sheathed
                              + SakuraKiriSheatheAnimation.HoldAfterSheatheFrames / 60f;
            if (elapsed < playerEnd)
                return SakuraKiriDrawState.Sheathe;
            return SakuraKiriDrawState.Complete;
        }
    }

    // 回场和收刀维持左向；最后一下收刀完成后转向右边并保持到动作结束。
    public int Facing => Projectile.ai[0] >= SlashStartFrame && ElapsedTime >= FinisherTimeline.ReappearStart
        ? SakuraKiriFinisher.FacingAtCloseout(ElapsedTime, FinisherTimeline)
        : facing;

    public float ChargeProgress => MathHelper.Clamp(Projectile.ai[0] / (ChargeFrames - 1f), 0f, 1f);

    public float DisappearProgress => MathHelper.Clamp(
        (Projectile.ai[0] - ChargeFrames) / (float)DisappearFrames, 0f, 1f);

    public float ReappearProgress
    {
        get
        {
            SakuraKiriFinisherTimeline timeline = FinisherTimeline;
            return SakuraKiriFinisher.Progress(ElapsedTime, timeline.ReappearStart, timeline.FlyOutEnd);
        }
    }

    public float SheatheProgress
    {
        get
        {
            SakuraKiriFinisherTimeline timeline = FinisherTimeline;
            return SakuraKiriFinisher.Progress(ElapsedTime, timeline.SheatheStart, timeline.Sheathed);
        }
    }

    public Vector2 ReappearDirection => FinaleSlashDirection;

    public Vector2 EchoDirection => DrawState == SakuraKiriDrawState.Disappear
        ? new Vector2(-facing, 0f)
        : -ReappearDirection;

    public Vector2 DrawOffset
    {
        get
        {
            if (DrawState == SakuraKiriDrawState.Disappear)
            {
                float remaining = 1f - DisappearProgress;
                return new Vector2(facing * DisappearDistance * (1f - remaining * remaining * remaining), 0f);
            }

            if (DrawState == SakuraKiriDrawState.Reappear)
            {
                return SakuraKiriFinisher.BodyOffset(ElapsedTime, FinisherTimeline, ReappearDirection);
            }

            return Vector2.Zero;
        }
    }

    public float BodyOpacity => DrawState switch
    {
        SakuraKiriDrawState.Disappear => 1f - SakuraKiriAnimation.Ease(DisappearProgress),
        SakuraKiriDrawState.Reappear => SakuraKiriFinisher.BodyOpacity(ElapsedTime, FinisherTimeline),
        SakuraKiriDrawState.Sheathe => SakuraKiriFinisher.CenterRevealOpacity(ElapsedTime, FinisherTimeline),
        SakuraKiriDrawState.Charge => 1f,
        _ => 0f
    };

    public float WeaponGlow => DrawState switch
    {
        SakuraKiriDrawState.Charge => SakuraReadyStanceWeaponVisual.GetGrowingGlowStrength(
            ChargeProgress, SakuraReadyStanceWeaponVisual.KiriGlowStartProgress,
            SakuraReadyStanceWeaponVisual.KiriGlowRisePower),
        SakuraKiriDrawState.Reappear or SakuraKiriDrawState.Sheathe => .7f,
        _ => 1f
    };

    private bool weaponLayer = true;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 2;
        Projectile.aiStyle = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.hide = false;
        Projectile.timeLeft = 240; //240;
        segments = 300;
        slashTime = 0f;
        slashFrames = 0;
        slashStarted = false;
        actionEnded = false;
        finaleBurstTime = -1f;
        finaleParticlesReleased = false;
        punchedSlashes.Clear();
        weaponLayer = true;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void AI()
    {
        Player player = Owner;
        if (player == null || !player.active || player.dead || player.CCed
            || (started && player.HeldItem.type != sourceItemType))
        {
            Projectile.Kill();
            return;
        }

        if (!started)
        {
            started = true;
            sourceItemType = player.HeldItem.type;
            facing = player.direction == 0 ? 1 : player.direction;
            Projectile.Center = player.MountedCenter;
            player.GetModPlayer<SakuraKiriPlayer>().Begin(this);
            if (Projectile.owner == Main.myPlayer)
                Projectile.netUpdate = true;
        }

        if (!slashStarted)
            Projectile.Center = player.MountedCenter;
        Projectile.timeLeft = Math.Max(Projectile.timeLeft, 2);

        UpdateChargeCameraShake(player);
        UpdateSlashEffects();
        if (!actionEnded)
            player.GetModPlayer<SakuraKiriPlayer>().ApplyAnimationPose(this);
        float playerEndTime = FinisherTimeline.Sheathed
                              + SakuraKiriSheatheAnimation.HoldAfterSheatheFrames / 60f;
        float effectsHoldEnd = FinaleParticleTime + FinaleHoldFrames / 60f;
        if (!actionEnded && Projectile.ai[0] >= SlashStartFrame
                         && ElapsedTime >= Math.Max(playerEndTime, effectsHoldEnd))
        {
            actionEnded = true;
            if (Owner is { } owner)
            {
                if (owner.heldProj == Projectile.whoAmI) owner.heldProj = -1;
                owner.GetModPlayer<SakuraKiriPlayer>().End(this);
            }
        }

        float effectsEnd = Math.Max(FinaleParticleTime + FinaleHaloDuration,
            LastFinaleSlashTime + FinaleExpandTime + FinaleShrinkTime);
        if (actionEnded && ElapsedTime >= effectsEnd)
        {
            Projectile.Kill();
            return;
        }

        Projectile.ai[0]++;
    }

    private void UpdateChargeCameraShake(Player player)
    {
        // Never add this modifier on a server or when observing another player's skill.
        if (Main.dedServ || Projectile.owner != Main.myPlayer || Main.gameMenu || Main.gamePaused
            || actionEnded || DrawState != SakuraKiriDrawState.Charge)
            return;

        int interval = Math.Max(2, ChargeShakeIntervalFrames);
        int frame = (int)Projectile.ai[0];
        if (frame % interval != 0) return;
        int duration = Math.Min(interval, ChargeFrames - frame);
        if (duration < 2) return;

        float start = MathHelper.Clamp(ChargeShakeStartProgress, 0f, .99f);
        float progress = MathHelper.Clamp((ChargeProgress - start) / (1f - start), 0f, 1f);
        float strength = Math.Max(0f, ChargeShakeMaxStrength)
                         * MathF.Pow(progress, Math.Max(.01f, ChargeShakeRisePower));
        if (strength <= .001f) return;

        // Rotate successive punches without consuming gameplay randomness; no packets are sent.
        Vector2 direction = (frame / interval * 2.399963f).ToRotationVector2();
        Main.instance.CameraModifiers.Add(new PunchCameraModifier(player.MountedCenter, direction,
            strength, Math.Max(.01f, ChargeShakeCyclesPerSecond), duration, distanceFalloff: -1f,
            uniqueIdentity: "CalamityOverhaul:SakuraKiri:ChargeShake"));
    }

    private void UpdateSlashEffects()
    {
        if (Projectile.ai[0] < SlashStartFrame) return;

        if (!slashStarted)
        {
            slashStarted = true;
            slashCenter = Projectile.Center;
        }

        Projectile.Center = slashCenter;

        float previousSlashTime = slashTime;
        // Derive seconds from an integer clock so a 2-frame sheathing pause does not drift.
        slashTime = ++slashFrames / 60f;
        UpdateSlashCameraPunches(previousSlashTime);
        if (finaleParticlesReleased || slashTime < FinaleParticleTime) return;

        finaleParticlesReleased = true;
        finaleBurstTime = slashTime;
        if (Main.dedServ) return;

        // The final kick belongs to the one-shot particle explosion, not the lines' finale pulses.
        if (Projectile.owner == Main.myPlayer && !Main.gameMenu && !Main.gamePaused
            && FinisherLine is { } finisherLine)
            AddSlashCameraPunch(finisherLine, Lines.IndexOf(finisherLine), true);

        SakuraFinaleFeedback.KiriBurst(Projectile.Center);
        SakuraParticleSystem.EmitBurst(Projectile.Center, Vector2.UnitX, new SakuraEmissionSettings
        {
            FineCount = 16,
            SimpleCount = 96,
            Speed = 920f,
            Spread = MathHelper.Pi,
            SpawnRadius = 4f,
            Scale = 2.9f,
            PerspectiveStrength = 3.4f,
            Lifetime = 1.9f,
            Drag = .55f,
            Gravity = 18f,
            Curl = 70f,
            SphericalBurst = true
        });
    }

    private void PrepareFinaleStartDelays()
    {
        finaleStartDelays.Clear();
        foreach (SakuraKiriLine line in Lines)
            if (line.Visible)
                finaleStartDelays.Add(line.StartDelay * .5f);
        finaleStartDelays.Sort();
    }

    private void UpdateSlashCameraPunches(float previousTime)
    {
        if (Main.dedServ || Projectile.owner != Main.myPlayer || Main.gameMenu || Main.gamePaused)
            return;

        PrepareFinaleStartDelays();
        int visibleIndex = 0;
        for (int i = 0; i < Lines.Count; i++)
        {
            SakuraKiriLine line = Lines[i];
            if (!line.Visible) continue;
            int reverseIndex = finaleStartDelays.Count - 1 - visibleIndex++;
            float finaleTime = FinaleStartTime + finaleStartDelays[reverseIndex] - finaleStartDelays[0];
            if (Length <= 0f || line.LengthScale <= 0f || line.WidthScale <= 0f) continue;

            float burstTime = Math.Max(0f, line.StartDelay + Math.Max(0f, line.LengthGrowTime));
            // The first visible step is strictly after StartDelay, as in SakuraKiriRenderer.Draw.
            if (slashTime <= line.StartDelay) continue;
            if (previousTime <= burstTime && slashTime > burstTime && slashTime < finaleTime
                && punchedSlashes.Add(line))
                AddSlashCameraPunch(line, i, false);
        }
    }

    private void AddSlashCameraPunch(SakuraKiriLine line, int index, bool finale)
    {
        float strength = Math.Max(0f, finale ? FinaleSlashPunchStrength : SlashPunchStrength);
        if (line.HeavyCameraPunch) strength *= Math.Max(0f, HeavySlashPunchMultiplier);
        if (strength <= 0f) return;
        Matrix rotation = line.Rotation;
        if (line.FollowProjectileRotation)
            rotation *= Matrix.CreateRotationZ(Projectile.rotation);
        Vector3 axis = Vector3.Transform(Vector3.UnitX, rotation);
        Vector2 direction = Vector2.TransformNormal(new Vector2(axis.X, axis.Y),
            Main.GameViewMatrix.TransformationMatrix);
        // An edge-on line has no stable screen direction; do not introduce an unrelated kick.
        if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y)
                                         || direction.LengthSquared() < .000001f) return;
        direction.Normalize();
        Main.instance.CameraModifiers.Add(new PunchCameraModifier(slashCenter + line.Offset,
            direction, strength, Math.Max(.01f, SlashPunchCyclesPerSecond), Math.Max(2, SlashPunchFrames),
            distanceFalloff: -1f,
            uniqueIdentity: $"CalamityOverhaul:SakuraKiri:{Projectile.owner}:{Projectile.identity}:{index}:{finale}"));
    }

    public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
        List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
    {
        overPlayers.Add(index);
        base.DrawBehind(index, behindNPCsAndTiles, behindNPCs, behindProjectiles, overPlayers, overWiresUI);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (Main.dedServ) return false;

        if (weaponLayer)
        {
            weaponLayer = false;
            if (DrawState == SakuraKiriDrawState.Charge)
            {
                Main.spriteBatch.End();
                try
                {
                    DrawChargeFlow(true);
                }
                finally
                {
                    SakuraFinaleAssets.RestoreWorldBatch();
                }

                return false;
            }

            if (!slashStarted) return false;

            SakuraFinaleAssets assets = ModContent.GetInstance<SakuraFinaleAssets>();
            Effect effect = assets.KiriMesh;
            Texture2D texture = assets.KiriTrail;
            GraphicsDevice device = Main.instance.GraphicsDevice;
            Matrix worldToScreen = Matrix.CreateTranslation(-Main.screenPosition.X, -Main.screenPosition.Y, 0f)
                                   * Main.GameViewMatrix.TransformationMatrix;

            Main.spriteBatch.End();
            try
            {
                PrepareFinaleStartDelays();

                int visibleLineCount = finaleStartDelays.Count;
                int visibleLineIndex = 0;
                foreach (SakuraKiriLine line in Lines)
                {
                    if (!line.Visible) continue;
                    int finalePulseIndex = visibleLineCount - 1 - visibleLineIndex++;
                    float finaleDelay = finaleStartDelays[finalePulseIndex] - finaleStartDelays[0];
                    Matrix rotation = line.Rotation;
                    if (line.FollowProjectileRotation)
                        rotation *= Matrix.CreateRotationZ(Projectile.rotation);
                    renderer.Draw(device, effect, texture, Projectile.Center + line.Offset, rotation, worldToScreen,
                        slashTime, Length * line.LengthScale, Segments, BlackLayer, RedLayer, WhiteLayer,
                        line.FocalLength, line.WidthScale, line,
                        slashTime - FinaleStartTime, finaleDelay,
                        FinaleExpandTime, FinaleShrinkTime);
                }

            }
            finally
            {
                SakuraFinaleAssets.RestoreWorldBatch();
            }
        }
        else
        {
            weaponLayer = true;
            DrawFinaleHalo();
        }

        return false;
    }

    private void DrawFinaleHalo()
    {
        SakuraFinaleAssets assets = ModContent.GetInstance<SakuraFinaleAssets>();
        Effect polarFlowEffect = assets.PolarFlow;
        Texture2D finaleHaloTexture = assets.Halo;
        float duration = Math.Max(.001f, FinaleHaloDuration);
        float age = slashTime - finaleBurstTime;
        if (finaleBurstTime < 0f || age < 0f || age >= duration || FinaleHaloSize <= 0f) return;

        float attackTime = Math.Min(.14f, duration * .35f);
        float pulse = SakuraBurstCurve.Evaluate(age, attackTime, duration, .72f, 4.2f);
        if (pulse <= 0f) return;

        float expansion = SakuraBurstCurve.Attack(age / attackTime, .7f);
        float size = FinaleHaloSize * MathHelper.Lerp(.42f, 1f, expansion);
        Vector4 color = FinaleHaloColor.ToVector4();
        color.W *= pulse * Math.Max(0f, FinaleHaloIntensity);

        polarFlowEffect.Parameters["Center"].SetValue(new Vector2(.5f, .5f));
        polarFlowEffect.Parameters["AspectRatio"].SetValue(1f);
        polarFlowEffect.Parameters["AngularTiling"].SetValue(5f);
        polarFlowEffect.Parameters["RadialTiling"].SetValue(1f);
        polarFlowEffect.Parameters["Rotation"].SetValue(0f);
        polarFlowEffect.Parameters["TextureRotation"].SetValue(0f);
        polarFlowEffect.Parameters["FlowOffset"].SetValue(age * FinaleHaloFlowSpeed);
        polarFlowEffect.Parameters["EdgeFade"].SetValue(.02f);
        polarFlowEffect.Parameters["ClipOutside"].SetValue(true);
        polarFlowEffect.Parameters["EffectColor"].SetValue(color);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
            DepthStencilState.None, RasterizerState.CullNone, polarFlowEffect,
            Main.GameViewMatrix.TransformationMatrix);
        Main.spriteBatch.Draw(finaleHaloTexture, Projectile.Center - Main.screenPosition, null, Color.White,
            0f, new Vector2(finaleHaloTexture.Width, finaleHaloTexture.Height) * .5f,
            size / finaleHaloTexture.Width, SpriteEffects.None, 0f);
        Main.spriteBatch.End();
        SakuraFinaleAssets.RestoreWorldBatch();
    }

    public override void SendExtraAI(System.IO.BinaryWriter writer)
    {
        writer.Write(facing);
        writer.Write(sourceItemType);
    }

    public override void ReceiveExtraAI(System.IO.BinaryReader reader)
    {
        facing = reader.ReadInt32();
        sourceItemType = reader.ReadInt32();
    }

    public void DrawHeldWeapon(ref PlayerDrawSet drawInfo)
    {
        Player player = drawInfo.drawPlayer;
        if (!player.active || player.dead || !IsActionVisible
            || DrawState is SakuraKiriDrawState.Hidden or SakuraKiriDrawState.Complete)
            return;

        Texture2D texture = ModContent.GetInstance<SakuraFinaleAssets>().Weapon;
        int gravity = player.gravDir < 0f ? -1 : 1;
        bool finishing = DrawState is SakuraKiriDrawState.Reappear or SakuraKiriDrawState.Sheathe;
        Vector2 center = new Vector2(MathF.Floor(player.MountedCenter.X), MathF.Floor(player.MountedCenter.Y))
            + new Vector2(0f, MathF.Floor(player.gfxOffY))
                                                      + drawInfo.Position - player.position;
        Vector2 position, origin;
        float rotation;
        SpriteEffects effects;
        if (IsCenterSheathing)
        {
            // 第二段原样复刻测试版的初始中心偏移、刀柄旋转和世界空间平移。
            // 不套用第一段的手掌锚定，否则会改变已经验证过的收刀路径。
            Vector2 sheatheCenter = player.MountedCenter + new Vector2(0f, player.gfxOffY)
                                                         + drawInfo.Position - player.position;
            SakuraKiriSheatheAnimation.WeaponPose(texture, sheatheCenter, CenterSheatheFrame, Facing, gravity,
                out position, out origin, out rotation, out effects);
        }
        else if (finishing)
        {
            // 与后伸的持刀手使用同一 composite-arm 角度，禁止把刀柄固定在身体偏移上。
            // GetFrontHandPosition 不翻转重力，因此只镜像手位的局部 Y 一次。
            Vector2 handOffset = player.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full,
                FinisherArmRotation) - player.MountedCenter;
            handOffset.Y *= gravity;
            Vector2 hand = player.MountedCenter + handOffset + new Vector2(0f, player.gfxOffY)
                + drawInfo.Position - player.position;
            SakuraKiriFinisher.WeaponPose(texture, hand, gravity,
                out position, out origin, out rotation, out effects);
        }
        else
        {
            // Charge/disappear keep the original diagonal ready stance, not the finisher pose.
            Vector2 offset = SakuraReadyStanceWeaponVisual.WeaponOffset;
            position = center + new Vector2(offset.X * Facing, offset.Y * gravity);
            origin = new Vector2(texture.Width, texture.Height) * .5f;
            rotation = SakuraReadyStanceWeaponVisual.WeaponRotation * Facing * gravity;
            effects = (Facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None)
                      | (gravity < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None);
        }

        float glow = WeaponGlow;
        float revealOpacity = IsCenterSheathing ? BodyOpacity : 1f;
        float echoOpacity = IsCenterSheathing ? CenterEchoOpacity : 0f;
        float echoOffset = IsCenterSheathing ? CenterEchoOffset : 0f;

        if (IsCenterSheathing && echoOpacity > .001f)
        {
            Color echoColor = Color.Lerp(Color.White, new Color(255, 70, 100, 255), .42f);
            AddHeldWeaponDraw(ref drawInfo, texture, position, origin, rotation, effects, glow,
                new Vector2(-echoOffset, 0f), echoColor, echoOpacity);
            AddHeldWeaponDraw(ref drawInfo, texture, position, origin, rotation, effects, glow,
                new Vector2(echoOffset, 0f), echoColor, echoOpacity);
        }

        AddHeldWeaponDraw(ref drawInfo, texture, position, origin, rotation, effects, glow,
            Vector2.Zero, Color.White, revealOpacity);
    }

    private void AddHeldWeaponDraw(ref PlayerDrawSet drawInfo, Texture2D texture,
        Vector2 position, Vector2 origin, float rotation, SpriteEffects effects, float glow,
        Vector2 shift, Color tint, float opacity)
    {
        Vector2 drawPosition = position - Main.screenPosition + shift;
        if (glow > 0f)
        {
            Texture2D weaponGlowMask = ModContent.GetInstance<SakuraFinaleAssets>().GlowMask();
            drawInfo.DrawDataCache.Add(new DrawData(weaponGlowMask, drawPosition, null,
                SakuraReadyStanceWeaponVisual.GlowColor(glow) * opacity, rotation,
                new Vector2(weaponGlowMask.Width, weaponGlowMask.Height) * .5f, 1f, effects, 0));
        }

        drawInfo.DrawDataCache.Add(new DrawData(texture, drawPosition, null,
            tint * opacity, rotation, origin, 1f, effects, 0));
    }

    internal void DrawChargeFlow(bool foreground)
    {
        Player player = Owner;
        if (Main.dedServ || DrawState != SakuraKiriDrawState.Charge || player == null
            || !player.active || player.dead)
            return;

        SakuraFinaleAssets assets = ModContent.GetInstance<SakuraFinaleAssets>();
        Texture2D weapon = assets.Weapon;
        Texture2D texture = assets.ChargeTrail;
        GraphicsDevice device = Main.instance.GraphicsDevice;
        SakuraKiriChargeFlowRenderer chargeFlowRenderer = assets.ChargeRenderer();
        int gravity = player.gravDir < 0f ? -1 : 1;
        Vector2 playerCenter = new Vector2(MathF.Floor(player.MountedCenter.X), MathF.Floor(player.MountedCenter.Y))
            + new Vector2(0f, MathF.Floor(player.gfxOffY));
        Vector2 target = SakuraKiriChargeFlow.GetSwordTarget(weapon, playerCenter, facing, gravity);
        SakuraKiriFlowSettings settings = new(SakuraKiriChargeFlow.Radius, SakuraKiriChargeFlow.Arc,
            SakuraKiriChargeFlow.Width, SakuraKiriChargeFlow.Speed, SakuraKiriChargeFlow.Brightness,
            SakuraKiriChargeFlow.Segments);
        Matrix transform = Matrix.CreateTranslation(-Main.screenPosition.X, -Main.screenPosition.Y, 0f)
                           * Main.GameViewMatrix.TransformationMatrix;
        chargeFlowRenderer.Draw(texture, TextureAssets.MagicPixel.Value, target, facing, gravity,
            Projectile.ai[0] / 60f, ChargeProgress, foreground, settings,
            (ChargeFrames - 1f) / 60f, transform);
    }

    public Texture2D GetWeaponGlowMask(Texture2D texture) =>
        ModContent.GetInstance<SakuraFinaleAssets>().GlowMask();

    public override void OnKill(int timeLeft)
    {
        if (Owner is { } player)
        {
            if (player.heldProj == Projectile.whoAmI) player.heldProj = -1;
            player.GetModPlayer<SakuraKiriPlayer>().End(this);
        }

    }
}

public enum SakuraKiriDrawState
{
    Charge,
    Disappear,
    Hidden,
    Reappear,
    Sheathe,
    Complete
}

internal static class SakuraKiriAnimation
{
    internal static Vector2 WeaponGrip => new(14f, 208f);
    internal static Vector2 WeaponBladeVector => new(58f, -218f);

    internal static float Ease(float t)
    {
        t = MathHelper.Clamp(t, 0f, 1f);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    internal static Vector2 GetMirroredOrigin(Texture2D texture, Vector2 origin, SpriteEffects effects)
    {
        if ((effects & SpriteEffects.FlipHorizontally) != 0) origin.X = texture.Width - origin.X;
        if ((effects & SpriteEffects.FlipVertically) != 0) origin.Y = texture.Height - origin.Y;
        return origin;
    }

    internal static float GetWeaponRotation(Texture2D texture, Vector2 bladeVector,
        float desiredAngle, SpriteEffects effects)
    {
        if ((effects & SpriteEffects.FlipHorizontally) != 0) bladeVector.X = -bladeVector.X;
        if ((effects & SpriteEffects.FlipVertically) != 0) bladeVector.Y = -bladeVector.Y;
        return desiredAngle - bladeVector.ToRotation();
    }
}
