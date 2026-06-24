using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills
{
    /// <summary>叠加攻击：克隆体汇聚后齐射比目鱼炮</summary>
    internal static class Superposition
    {
        public static int ID = 6;
        private const int ToggleCD = 30;
        private static int SuperpositionCooldown => 60 * (30 - (HalibutData.GetDomainLayer() - 7) * 5); //30s基础冷却
        public static void AltUse(Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (hp.SuperpositionToggleCD > 0 || hp.SuperpositionCooldown > 0) {
                return;
            }

            Activate(player);
            hp.SuperpositionToggleCD = ToggleCD;
            hp.SuperpositionCooldown = SuperpositionCooldown;
        }

        public static void Activate(Player player) {
            if (Main.myPlayer == player.whoAmI) {
                SpawnSuperpositionEffect(player);
            }
        }

        internal static void SpawnSuperpositionEffect(Player player) {
            var source = player.GetSource_Misc("SuperpositionSkill");
            Projectile.NewProjectile(
                source,
                player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<SuperpositionProj>(),
                0,
                0,
                player.whoAmI
            );
        }
    }

    #region 时空克隆体
    /// <summary>汇聚阶段的克隆体弹幕</summary>
    internal class TimeClone
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 SpawnPos;
        public float Alpha;
        public float Life;
        public float MaxLife;
        public float Scale;
        public PlayerSnapshot Snapshot;
        public readonly List<Vector2> TrailPositions = new();

        private const int MaxTrailLength = 14;
        private float spiralAngle;
        private readonly float timeWarpFactor;
        private float orbitRadius;
        private bool converging;

        public TimeClone(Vector2 spawnPos, PlayerSnapshot snapshot, float startOrbitRadius) {
            Position = spawnPos;
            SpawnPos = spawnPos;
            Snapshot = snapshot;
            Velocity = Vector2.Zero;
            Life = 0f;
            MaxLife = 140f;
            Alpha = 0f;
            Scale = 0.75f;
            spiralAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            timeWarpFactor = Main.rand.NextFloat(0.8f, 1.25f);
            orbitRadius = startOrbitRadius;
        }

        public void SetConverging() => converging = true;

        public void Update(Vector2 center, float gatherProgress, float convergeProgress) {
            Life++;

            //计算目标半径
            float targetRadius = converging
                ? MathHelper.Lerp(orbitRadius, 0f, MathHelper.SmoothStep(0f, 1f, convergeProgress))
                : MathHelper.Lerp(
                    orbitRadius * 1.15f,
                    orbitRadius * 0.85f,
                    (float)Math.Sin(gatherProgress * MathHelper.Pi)
                );

            orbitRadius = MathHelper.Lerp(orbitRadius, targetRadius, converging ? 0.18f : 0.05f);

            //螺旋角度更新
            spiralAngle += 0.07f * timeWarpFactor + (converging ? 0.12f : 0f);

            //计算目标位置并应用速度
            Vector2 targetPos = center + spiralAngle.ToRotationVector2() * orbitRadius;
            Vector2 toTarget = targetPos - Position;
            Velocity = Vector2.Lerp(Velocity, toTarget * (converging ? 0.25f : 0.18f), 0.4f);
            Position += Velocity;

            //更新透明度
            if (!converging) {
                Alpha = MathHelper.Clamp(gatherProgress * 1.6f, 0f, 1f);
            }
            else {
                Alpha = (float)Math.Pow(1f - convergeProgress, 0.6f);
            }

            //记录拖尾位置
            TrailPositions.Insert(0, Position);
            if (TrailPositions.Count > MaxTrailLength) {
                TrailPositions.RemoveAt(TrailPositions.Count - 1);
            }
        }

        public bool ShouldRemove() {
            return Life >= MaxLife || (converging && orbitRadius < 4f && Alpha < 0.05f);
        }

        public void DrawTrail(float globalAlpha) {
            if (TrailPositions.Count < 3) {
                return;
            }

            Texture2D tex = VaultAsset.placeholder2.Value;

            for (int i = 0; i < TrailPositions.Count - 1; i++) {
                float progress = i / (float)TrailPositions.Count;
                float trailAlpha = (1f - progress) * Alpha * globalAlpha * 0.55f;

                Vector2 start = TrailPositions[i];
                Vector2 end = TrailPositions[i + 1];
                Vector2 diff = end - start;
                float length = diff.Length();

                if (length < 0.01f) {
                    continue;
                }

                float rotation = diff.ToRotation();
                Color color = new Color(170, 120, 255, 0) * trailAlpha;

                Main.spriteBatch.Draw(
                    tex,
                    start - Main.screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    color,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, 6f - progress * 4f),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
    #endregion

    #region 法阵符环
    /// <summary>椭圆形符环 VFX</summary>
    internal class RuneCircle
    {
        public float Life;
        public float MaxLife;
        public float StartRadius;
        public float EndRadius;
        public float Rotation;
        public float RotSpeed;
        public float EllipseFactor;
        public Color ColorA;
        public Color ColorB;
        public bool Shrink;

        public RuneCircle(float startR, float endR, int life, bool shrink, Color a, Color b) {
            StartRadius = startR;
            EndRadius = endR;
            MaxLife = life;
            Life = 0;
            Shrink = shrink;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            RotSpeed = Main.rand.NextFloat(-0.05f, 0.05f);
            EllipseFactor = Main.rand.NextFloat(0.6f, 1.15f);
            ColorA = a;
            ColorB = b;
        }

        public void Update() {
            Life++;
            Rotation += RotSpeed;
        }

        public bool Dead => Life >= MaxLife;

        public void Draw(Vector2 center, float alpha) {
            float progress = Life / MaxLife;
            float radius = Shrink
                ? MathHelper.Lerp(StartRadius, EndRadius, progress)
                : MathHelper.Lerp(StartRadius, EndRadius, (float)Math.Sin(progress * MathHelper.Pi));
            float fade = (float)Math.Sin(progress * MathHelper.Pi) * alpha;

            if (fade <= 0.01f) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            int segments = 56;
            float angleStep = MathHelper.TwoPi / segments;

            for (int i = 0; i < segments; i++) {
                float angle1 = Rotation + i * angleStep;
                float angle2 = Rotation + (i + 1) * angleStep;

                Vector2 p1 = center + new Vector2(
                    (float)Math.Cos(angle1) * radius,
                    (float)Math.Sin(angle1) * radius * EllipseFactor
                );
                Vector2 p2 = center + new Vector2(
                    (float)Math.Cos(angle2) * radius,
                    (float)Math.Sin(angle2) * radius * EllipseFactor
                );

                Vector2 diff = p2 - p1;
                float length = diff.Length();

                if (length < 0.0001f) {
                    continue;
                }

                float rotation = diff.ToRotation();
                float wave = (float)Math.Sin(angle1 * 6f + Main.GlobalTimeWrappedHourly * 8f) * 0.5f + 0.5f;
                Color color = Color.Lerp(ColorA, ColorB, wave) * fade * 0.6f;

                Main.spriteBatch.Draw(
                    pixel,
                    p1 - Main.screenPosition,
                    new Rectangle(0, 0, 1, 1),
                    color,
                    rotation,
                    Vector2.Zero,
                    new Vector2(length, 2f),
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
    #endregion

    /// <summary>叠加攻击主控弹幕</summary>
    internal class SuperpositionProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private List<TimeClone> timeClones;
        private List<RuneCircle> runeCircles = new();
        private List<int> cannonProjIds = new();
        private bool cannonsSpawned;

        private enum SuperpositionState
        {
            Gathering,  //时空克隆体聚集
            Converging, //克隆体收拢
            Charging,   //炮阵充能
            Launching,  //齐射发射
            Exploding   //爆炸收尾
        }

        private SuperpositionState currentState = SuperpositionState.Gathering;
        private int stateTimer = 0;

        //阶段时长常量
        private const int GatherDuration = 60;
        private const int ConvergeDuration = 45;
        private const int ChargeDuration = 36;
        private const int LaunchDuration = 180;
        private const int ExplodeDuration = 40;

        private float effectAlpha = 0f;
        private Vector2 attackDirection = Vector2.UnitX;

        public override void SetDefaults() {
            Projectile.width = 900;
            Projectile.height = 900;
            Projectile.timeLeft = GatherDuration + ConvergeDuration + ChargeDuration +
                                  LaunchDuration + ExplodeDuration + 30;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = false;
        }

        public override void AI() {
            if (!Owner.active) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = Owner.Center;
            stateTimer++;

            //状态机更新
            switch (currentState) {
                case SuperpositionState.Gathering:
                    UpdateGathering();
                    break;
                case SuperpositionState.Converging:
                    UpdateConverging();
                    break;
                case SuperpositionState.Charging:
                    UpdateCharging();
                    break;
                case SuperpositionState.Launching:
                    UpdateLaunching();
                    break;
                case SuperpositionState.Exploding:
                    UpdateExploding();
                    break;
            }

            UpdateLists();
        }

        private void UpdateLists() {
            //更新时空克隆体
            if (timeClones != null) {
                float gatherProgress = currentState == SuperpositionState.Gathering
                    ? stateTimer / (float)GatherDuration
                    : 1f;
                float convergeProgress = currentState == SuperpositionState.Converging
                    ? stateTimer / (float)ConvergeDuration
                    : (currentState > SuperpositionState.Converging ? 1f : 0f);

                foreach (var clone in timeClones) {
                    if (currentState == SuperpositionState.Converging) {
                        clone.SetConverging();
                    }
                    clone.Update(Owner.Center, gatherProgress, convergeProgress);
                }

                timeClones.RemoveAll(c => c.ShouldRemove());
            }

            //更新符环
            foreach (var rune in runeCircles) {
                rune.Update();
            }
            runeCircles.RemoveAll(r => r.Dead);
        }

        private void UpdateGathering() {
            float progress = stateTimer / (float)GatherDuration;
            effectAlpha = MathHelper.Clamp(progress * 1.3f, 0f, 1f);

            if (stateTimer == 1) {
                InitializeTimeClones();
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen, Owner.Center);
            }

            //生成符环
            if (stateTimer % 12 == 0) {
                runeCircles.Add(new RuneCircle(
                    260, 300, 50, false,
                    new Color(120, 90, 210),
                    new Color(200, 150, 255)
                ));
            }

            if (stateTimer >= GatherDuration) {
                currentState = SuperpositionState.Converging;
                stateTimer = 0;
            }
        }

        private void UpdateConverging() {
            if (stateTimer % 10 == 0) {
                runeCircles.Add(new RuneCircle(
                    220, 120, 40, true,
                    new Color(160, 110, 240),
                    new Color(230, 200, 255)
                ));
            }

            if (stateTimer >= ConvergeDuration) {
                currentState = SuperpositionState.Charging;
                stateTimer = 0;
                attackDirection = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.UnitX);
            }
        }

        private void UpdateCharging() {
            effectAlpha = 1f;

            if (stateTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item72 with { Volume = 1.1f }, Owner.Center);
            }

            //生成充能符环
            if (stateTimer % 6 == 0) {
                runeCircles.Add(new RuneCircle(
                    140, 210, 32, false,
                    new Color(180, 130, 255),
                    new Color(255, 255, 255)
                ));
            }

            if (stateTimer >= ChargeDuration) {
                currentState = SuperpositionState.Launching;
                stateTimer = 0;
                SpawnCannons();
            }
        }

        private void UpdateLaunching() {
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full
                , (MathHelper.PiOver2 * SafeGravDir - ToMouseA) * -SafeGravDir);

            //炮阵未生成就跳过
            if (!cannonsSpawned) {
                SpawnCannons();
            }

            //监控炮阵完成状态
            bool allCompleted = true;
            for (int i = cannonProjIds.Count - 1; i >= 0; i--) {
                int id = cannonProjIds[i];

                if (id < 0 || id >= Main.maxProjectiles || !Main.projectile[id].active) {
                    continue;
                }

                if (Main.projectile[id].ModProjectile is SuperpositionCannon cannon) {
                    if (!cannon.Completed) {
                        allCompleted = false;
                    }
                }
            }

            //所有炮完成或超时进入爆炸阶段
            if (allCompleted || stateTimer >= LaunchDuration) {
                currentState = SuperpositionState.Exploding;
                stateTimer = 0;
                SoundEngine.PlaySound(SoundID.Item14, Owner.Center);
            }
        }

        private void UpdateExploding() {
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full
                , (MathHelper.PiOver2 * SafeGravDir - ToMouseA) * -SafeGravDir);

            float progress = stateTimer / (float)ExplodeDuration;
            effectAlpha = 1f - progress;

            if (stateTimer == 1) {
                runeCircles.Add(new RuneCircle(
                    200, 30, 40, true,
                    new Color(200, 150, 255),
                    new Color(255, 255, 255)
                ));
            }

            if (stateTimer >= ExplodeDuration) {
                Projectile.Kill();
            }
        }

        private void InitializeTimeClones() {
            timeClones = new List<TimeClone>();
            int cloneCount = 12;
            float outerRing = 420f;

            for (int i = 0; i < cloneCount; i++) {
                float edge = Main.rand.NextFloat(4f);
                Vector2 spawn;

                //从四个方向随机生成
                if (edge < 1f) {
                    spawn = Owner.Center + new Vector2(Main.rand.NextFloat(-600, 600), -800);
                }
                else if (edge < 2f) {
                    spawn = Owner.Center + new Vector2(Main.rand.NextFloat(-600, 600), 800);
                }
                else if (edge < 3f) {
                    spawn = Owner.Center + new Vector2(-800, Main.rand.NextFloat(-600, 600));
                }
                else {
                    spawn = Owner.Center + new Vector2(800, Main.rand.NextFloat(-600, 600));
                }

                timeClones.Add(new TimeClone(spawn, new PlayerSnapshot(Owner), outerRing));
            }
        }

        private void SpawnCannons() {
            cannonsSpawned = true;
            cannonProjIds.Clear();

            var source = Owner.GetSource_Misc("SuperpositionCannons");
            int cannonCount = 7;
            Vector2 backDir = -attackDirection;
            Vector2 perp = attackDirection.RotatedBy(MathHelper.PiOver2);
            float arc = MathHelper.ToRadians(70f);

            for (int i = 0; i < cannonCount; i++) {
                float lerpFactor = (cannonCount == 1) ? 0.5f : i / (float)(cannonCount - 1);
                float angleOffset = (lerpFactor - 0.5f) * arc;
                Vector2 offsetDir = backDir.RotatedBy(angleOffset);
                Vector2 position = Owner.Center + backDir * 180f +
                                  perp * (float)Math.Sin(angleOffset) * 40f +
                                  offsetDir * 20f;

                int id = Projectile.NewProjectile(
                    source,
                    position,
                    Vector2.Zero,
                    ModContent.ProjectileType<SuperpositionCannon>(),
                    Owner.HeldItem.damage,
                    4f,
                    Owner.whoAmI,
                    angleOffset,
                    0
                );

                if (id >= 0) {
                    cannonProjIds.Add(id);
                }
            }
        }

        //干净克隆体绘制：仅身体本体，不改动真实玩家，也不重放其 buff/特效绘制钩子
        private void DrawTimeClone(TimeClone clone) {
            if (clone.Alpha < 0.05f) {
                return;
            }

            Color ghostColor = new Color(170, 130, 255, 255) * clone.Alpha * 0.9f;
            Vector2 topLeft = clone.Position - Owner.Size * 0.5f;
            PlayerCloneRenderer.Draw(Owner, topLeft, ghostColor, Owner.direction,
                Owner.bodyFrame, Owner.legFrame, Owner.fullRotation, Owner.fullRotationOrigin);
        }

        private static void BeginWorldAlphaBatch() {
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);
        }

        public override bool PreDraw(ref Color lightColor) {
            //绘制符环
            foreach (var rune in runeCircles) {
                rune.Draw(Owner.Center, effectAlpha);
            }

            //绘制克隆体拖尾
            if (timeClones != null) {
                foreach (var clone in timeClones) {
                    clone.DrawTrail(effectAlpha);
                }
            }

            //绘制克隆体
            if (timeClones != null && timeClones.Count > 0) {
                Main.spriteBatch.End();
                BeginWorldAlphaBatch();
                foreach (var clone in timeClones) {
                    DrawTimeClone(clone);
                }
                Main.spriteBatch.End();
                BeginWorldAlphaBatch();
            }

            return false;
        }
    }

    #region 齐射炮弹幕
    /// <summary>
    /// 大比目鱼炮 - 叠加态齐射发射器：长蓄力→塌缩沉默→重锤齐射，炮口随领域层数叠加重影
    /// </summary>
    internal class SuperpositionCannon : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private Player Owner => Main.player[Projectile.owner];

        //owner 同步后的瞄准点：远程端读近似鼠标，避免误用本地视角鼠标
        private Vector2 AimWorld {
            get {
                if (Owner.TryGetOverride<HalibutPlayer>(out var hp)) {
                    return hp.MouseWorld;
                }
                return Main.MouseWorld;
            }
        }

        private enum CannonState
        {
            Deploy,
            Charge,
            Volley,
            Finish
        }

        private CannonState state = CannonState.Deploy;
        private int timer;
        private int volleyIndex;

        private const int DeployTime = 18;
        private const int ChargeTime = 34;
        private const int VolleyCount = 4;
        private const int VolleySpacing = 9;

        private float angleOffset;
        private float pulse;        //整体强度，驱动本体缩放与发光
        private float barrelGlow;   //炮口蓄力辉光 0..1
        private float recoil;       //后坐位移，沿 -瞄准方向逐帧回弹
        private float deployScale;  //部署弹出缩放
        private int echoLayers;     //叠加重影层数（随领域层数 7→10 提升）
        private bool infinite;      //满层（第十眼）白金配色

        public bool Completed => state == CannonState.Finish;

        //配色：青蓝水流 + 紫电叠加；满层转白金。A=255 以便加算/PRT 正常显色
        private Color GlowColor => infinite ? new Color(255, 226, 150) : new Color(150, 120, 255);
        private Color FlashColor => infinite ? new Color(255, 244, 206) : new Color(190, 170, 255);

        public override void SetDefaults() {
            Projectile.width = 80;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 400;
        }

        public override void AI() {
            if (!Owner.active) {
                Projectile.Kill();
                return;
            }

            if (timer == 0) {
                angleOffset = Projectile.ai[0];
                int layer = HalibutData.GetDomainLayer();
                echoLayers = (int)MathHelper.Clamp(layer - 6, 1, 4);
                infinite = layer >= 10;
            }

            timer++;

            Vector2 direction = (AimWorld - Owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 backDir = -direction;
            Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);

            Vector2 basePosition = Owner.Center + backDir * -80f;
            Vector2 offset = backDir.RotatedBy(angleOffset) * 20f +
                            perpendicular * (float)Math.Sin(angleOffset) * 40f;
            Projectile.Center = Vector2.Lerp(Projectile.Center, basePosition + offset, 0.15f);

            Owner.direction = Math.Sign(direction.X);

            recoil = MathHelper.Lerp(recoil, 0f, 0.25f);
            Lighting.AddLight(Projectile.Center, GlowColor.R / 255f * pulse, GlowColor.G / 255f * pulse, GlowColor.B / 255f * pulse);

            switch (state) {
                case CannonState.Deploy:
                    UpdateDeploy();
                    break;
                case CannonState.Charge:
                    UpdateCharge(direction);
                    break;
                case CannonState.Volley:
                    UpdateVolley(direction);
                    break;
                case CannonState.Finish:
                    UpdateFinish(direction);
                    break;
            }
        }

        private void UpdateDeploy() {
            float p = timer / (float)DeployTime;
            deployScale = VaultUtils.EaseOutBack(p);
            pulse = p * 0.45f;

            if (timer == 1) {
                SoundEngine.PlaySound(SoundID.Item34 with { Pitch = 0.3f, Volume = 0.45f }, Projectile.Center);
            }

            if (timer >= DeployTime) {
                state = CannonState.Charge;
                timer = 0;
                deployScale = 1f;
                SoundEngine.PlaySound(SoundID.Item72 with { Pitch = -0.35f, Volume = 0.7f }, Projectile.Center);
            }
        }

        private void UpdateCharge(Vector2 direction) {
            deployScale = 1f;
            float p = timer / (float)ChargeTime;
            barrelGlow = MathHelper.Clamp(p * 1.15f, 0f, 1f);
            pulse = 0.45f + barrelGlow * 0.55f;

            //蓄力期递增的镜头微震，临射前最紧绷
            Owner.GetModPlayer<CWRPlayer>().GetScreenShake(0.5f + p * 2.2f);

            //汇聚的蓄力光点旋入炮口
            if (!Main.dedServ) {
                Vector2 muzzle = Projectile.Center + direction * 36f;
                int rate = p > 0.6f ? 1 : 2;
                if (timer % rate == 0) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 radial = ang.ToRotationVector2();
                    float dist = Main.rand.NextFloat(70f, 130f) * (1f - p * 0.4f);
                    Vector2 sp = muzzle + radial * dist;
                    Vector2 vel = (muzzle - sp) * 0.08f + radial.RotatedBy(MathHelper.PiOver2) * 1.5f;
                    PRTLoader.NewParticle<PRT_Light>(sp, vel, FlashColor, 0.4f + p * 0.45f).Configure(16);
                }
            }

            //临射前的"塌缩"沉默：辉光回落，蓄势待发（爆发的留白）
            if (timer >= ChargeTime - 5) {
                barrelGlow = MathHelper.Lerp(1f, 0.5f, (timer - (ChargeTime - 5)) / 5f);
            }

            if (timer >= ChargeTime) {
                state = CannonState.Volley;
                timer = 0;
                volleyIndex = 0;
            }
        }

        private void UpdateVolley(Vector2 forward) {
            pulse = 1f;
            barrelGlow = 1f;

            if (timer == 1) {
                FireVolley(forward);
                FireFlash(forward);
                volleyIndex++;
            }

            if (volleyIndex >= VolleyCount) {
                state = CannonState.Finish;
                timer = 0;
            }
            else if (timer >= VolleySpacing) {
                timer = 0;
            }
        }

        private void UpdateFinish(Vector2 direction) {
            pulse *= 0.9f;
            barrelGlow *= 0.84f;

            //收尾炮口余烬
            if (timer == 1 && !Main.dedServ) {
                Vector2 muzzle = Projectile.Center + direction * 34f;
                for (int i = 0; i < 5; i++) {
                    Vector2 vel = direction * Main.rand.NextFloat(1f, 3f) + Main.rand.NextVector2Circular(1.5f, 1.5f);
                    PRTLoader.NewParticle<PRT_Light>(muzzle, vel, GlowColor, Main.rand.NextFloat(0.4f, 0.7f)).Configure(22);
                }
            }

            if (pulse < 0.04f) {
                Projectile.Kill();
            }
        }

        //齐射的"重锤"反馈：大后坐 + 镜头冲击 + 炮口星芒/火花（首发最猛）
        private void FireFlash(Vector2 forward) {
            recoil = 26f - volleyIndex * 3f;
            Owner.GetModPlayer<CWRPlayer>().GetScreenShake(volleyIndex == 0 ? 6.5f : 4f);

            if (!Main.dedServ) {
                Vector2 muzzle = Projectile.Center + forward * 40f;
                PRTLoader.NewParticle<PRT_StarPulseRing>(muzzle, Vector2.Zero, FlashColor, 0.5f).Configure(0.5f, 2.4f, 18);
                PRTLoader.NewParticle<PRT_Light>(muzzle, forward * 2f, FlashColor, 1.15f).Configure(16);
                for (int i = 0; i < 7; i++) {
                    Vector2 vel = forward.RotatedByRandom(0.5f) * Main.rand.NextFloat(7f, 17f);
                    PRTLoader.NewParticle<PRT_Spark>(muzzle, vel, FlashColor, Main.rand.NextFloat(0.8f, 1.4f)).Configure(false, Main.rand.Next(10, 18));
                }
            }

            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.55f, Pitch = 0.15f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item82 with { Volume = 0.85f }, Projectile.Center);
        }

        private void FireVolley(Vector2 forward) {
            var source = Projectile.GetSource_FromThis();
            int fishPerVolley = 9;
            float spread = MathHelper.ToRadians(20f);
            ShootState shootState = Owner.GetShootState();
            Vector2 muzzle = Projectile.Center + forward * 30f;
            Vector2 perp = forward.RotatedBy(MathHelper.PiOver2);

            //生成统一伤害判定弹幕
            int projId = Projectile.NewProjectile(
                source,
                muzzle,
                forward,
                ModContent.ProjectileType<CannonFishSwarmHitbox>(),
                shootState.WeaponDamage * (HalibutData.GetDomainLayer() - 6) * 2,
                shootState.WeaponKnockback,
                Owner.whoAmI,
                Projectile.whoAmI,  //传递炮的ID
                volleyIndex         //传递齐射索引
            );

            //把鱼沿炮口横向铺开成"有厚度的洪流"，再交给鱼自行向轴心汇拢
            if (projId >= 0 && Main.projectile[projId].ModProjectile is CannonFishSwarmHitbox hitbox) {
                hitbox.Infinite = infinite;
                for (int i = 0; i < fishPerVolley; i++) {
                    float lerpFactor = (i + 0.5f) / fishPerVolley;
                    float angle = spread * (lerpFactor - 0.5f);
                    Vector2 velocity = forward.RotatedBy(angle) * Main.rand.NextFloat(17f, 23f);
                    Vector2 spawn = muzzle + perp * ((lerpFactor - 0.5f) * 70f) + forward * Main.rand.NextFloat(-6f, 10f);

                    hitbox.AddFish(new FishEntity(
                        spawn,
                        velocity,
                        muzzle,
                        forward,
                        Main.rand.Next(9999),
                        echoLayers,
                        infinite
                    ));
                }
            }
        }

        private static void BeginAdditiveBatch() {
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);
        }

        private static void BeginWorldAlphaBatch() {
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(HalibutOverride.ID);
            Texture2D texture = TextureAssets.Item[HalibutOverride.ID].Value;
            Vector2 origin = texture.Size() / 2f;

            Vector2 direction = (AimWorld - Owner.Center).SafeNormalize(Vector2.UnitX);
            SpriteEffects spriteEffects = direction.X > 0
                ? SpriteEffects.None
                : SpriteEffects.FlipHorizontally;
            float rotation = direction.ToRotation() + (direction.X > 0 ? 0 : MathHelper.Pi);

            //后坐力沿 -瞄准方向把炮身踢回，部署时从小弹出
            float scale = (0.8f + pulse * 0.3f) * HalibutOverride.ItemScale * (0.25f + 0.75f * deployScale);
            Vector2 drawPosition = Projectile.Center - Main.screenPosition - direction * recoil;
            Vector2 muzzle = drawPosition + direction * 40f * scale;

            //加算辉光层：炮身底光 + 炮口蓄力核心 + 齐射星芒
            Main.spriteBatch.End();
            BeginAdditiveBatch();

            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 glowOrigin = glow.Size() / 2f;

            Main.spriteBatch.Draw(glow, drawPosition, null, GlowColor * (0.45f + pulse * 0.55f), 0f, glowOrigin, scale * 2.4f, SpriteEffects.None, 0f);
            if (barrelGlow > 0.02f) {
                Main.spriteBatch.Draw(glow, muzzle, null, FlashColor * barrelGlow, 0f, glowOrigin, scale * (1f + barrelGlow * 1.6f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(star, muzzle, null, FlashColor * barrelGlow, rotation, star.Size() / 2f, scale * barrelGlow * 0.7f, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.End();
            BeginWorldAlphaBatch();

            //本体
            Main.spriteBatch.Draw(texture, drawPosition, null, Color.White, rotation, origin, scale, spriteEffects, 0f);

            return false;
        }
    }

    /// <summary>鱼群轻量实体：沿炮口轴线高速突进、向轴心汇拢并叠加重影，表现"叠加"的洪流冲击</summary>
    internal class FishEntity
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Seed;
        public float Alpha;
        public int FishType;
        public float FishScale;
        public float FishRotation;
        public int FishDirection;
        public readonly List<Vector2> TrailPositions = new();
        private const int MaxTrailLength = 9;

        //轴线与叠加表现
        private readonly Vector2 axisOrigin;   //炮口原点
        private readonly Vector2 axisDir;      //突进方向（单位向量）
        private readonly float laneOffset;     //初始横向偏移（带符号），向轴心汇拢的起点
        private readonly float wavePhase;      //摆动相位
        private readonly float waveFreq;       //摆动频率
        private readonly float baseSpeed;      //巡航速度
        private readonly int echoLayers;       //叠加重影层数（随领域层数提升）
        private readonly Color coreTint;       //本体色（青蓝）
        private readonly Color glowTint;       //辉光色（紫电）

        public FishEntity(Vector2 position, Vector2 velocity, Vector2 axisOrigin, Vector2 axisDir
            , float seed, int echoLayers, bool infinite) {
            Position = position;
            this.axisOrigin = axisOrigin;
            this.axisDir = axisDir.SafeNormalize(Vector2.UnitX);
            Velocity = velocity;
            Seed = seed;
            Life = 0f;
            MaxLife = 78f;
            Alpha = 0f;
            FishType = Main.rand.Next(3);
            FishScale = 0.62f + Main.rand.NextFloat() * 0.34f;
            FishDirection = this.axisDir.X >= 0 ? 1 : -1;
            baseSpeed = MathHelper.Max(velocity.Length(), 6f);
            //以鱼相对轴线的初始投影作为车道偏移，使其从所在位置自然汇拢
            laneOffset = Vector2.Dot(position - axisOrigin, this.axisDir.RotatedBy(MathHelper.PiOver2));
            wavePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            waveFreq = Main.rand.NextFloat(0.16f, 0.26f);
            this.echoLayers = echoLayers;
            coreTint = infinite ? new Color(255, 244, 206) : new Color(140, 214, 255);
            glowTint = infinite ? new Color(255, 226, 150) : new Color(150, 120, 255);
        }

        public void Update(List<FishEntity> swarm) {
            Life++;
            float t = Life / MaxLife;

            float fadeIn = MathHelper.Clamp(Life / 6f, 0f, 1f);
            float fadeOut = 1f - MathHelper.Clamp((t - 0.74f) / 0.26f, 0f, 1f);
            Alpha = fadeIn * fadeOut;

            //炮口爆发：初段极速冲出，随后回落巡航（速度即冲击感）
            float burst = MathHelper.Lerp(1.7f, 1f, VaultUtils.EaseOutCubic(MathHelper.Clamp(t / 0.22f, 0f, 1f)));
            float speed = baseSpeed * burst;

            //沿轴线分解当前位置，计算汇拢与摆动
            Vector2 perp = axisDir.RotatedBy(MathHelper.PiOver2);
            float along = Vector2.Dot(Position - axisOrigin, axisDir);
            Vector2 axisPoint = axisOrigin + axisDir * along;
            float curPerp = Vector2.Dot(Position - axisPoint, perp);

            //目标横向：车道随时间收拢，叠加正弦摆动（摆动随汇拢减弱）——越走越聚成一束
            float laneNow = laneOffset * MathHelper.Lerp(1f, 0.55f, VaultUtils.EaseOutCubic(t));
            float wave = (float)Math.Sin(Life * waveFreq + wavePhase) * 26f * (1f - t * 0.7f);
            float targetPerp = laneNow + wave;

            Vector2 desired = axisDir * speed + perp * (targetPerp - curPerp) * 0.16f;
            Velocity = Vector2.Lerp(Velocity, desired, 0.22f);
            Position += Velocity;

            if (Math.Abs(Velocity.X) > 0.4f) {
                FishDirection = Velocity.X > 0 ? 1 : -1;
            }
            if (Velocity.LengthSquared() > 0.1f) {
                FishRotation = Velocity.ToRotation();
            }

            TrailPositions.Insert(0, Position);
            if (TrailPositions.Count > MaxTrailLength) {
                TrailPositions.RemoveAt(TrailPositions.Count - 1);
            }
        }

        public bool ShouldRemove() => Life >= MaxLife;

        public Rectangle GetHitbox() {
            return new Rectangle((int)(Position.X - 14), (int)(Position.Y - 14), 28, 28);
        }

        private static Texture2D GetFishTexture(int fishType) {
            int itemType = fishType switch {
                0 => ItemID.Tuna,
                1 => ItemID.Bass,
                2 => ItemID.Trout,
                _ => ItemID.Tuna
            };
            Main.instance.LoadItem(itemType);
            return TextureAssets.Item[itemType].Value;
        }

        /// <summary>加算光层（须在 Additive 批次中调用）：拖尾洪流 + 叠加重影 + 炮口辉光</summary>
        public void DrawGlow(float globalAlpha) {
            if (Alpha < 0.04f) {
                return;
            }

            float a = Alpha * globalAlpha;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            float speedT = MathHelper.Clamp(Velocity.Length() / 26f, 0f, 1f);

            //拖尾：沿历史位置铺柔光，越尾越细越淡——水之洪流
            for (int i = 1; i < TrailPositions.Count; i++) {
                float p = i / (float)TrailPositions.Count;
                Vector2 pos = TrailPositions[i] - Main.screenPosition;
                float ta = a * (1f - p) * 0.5f;
                float ts = FishScale * (0.55f - p * 0.34f);
                Main.spriteBatch.Draw(glow, pos, null, glowTint * ta, 0f, glowOrigin, ts, SpriteEffects.None, 0f);
            }

            //叠加重影：把同一条鱼相位错移再画若干层，越叠越亮——"叠加态"的视觉本体
            Texture2D fishTex = GetFishTexture(FishType);
            Rectangle rect = fishTex.Bounds;
            Vector2 fishOrigin = rect.Size() * 0.5f;
            SpriteEffects effects = FishDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float rot = FishRotation + (FishDirection > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
            Vector2 back = Velocity.SafeNormalize(Vector2.Zero);

            for (int i = 1; i <= echoLayers; i++) {
                float echo = i / (float)(echoLayers + 1);
                Vector2 offset = -back * (i * 5f + Velocity.Length() * 0.5f * echo);
                float ea = a * (1f - echo) * 0.4f;
                Main.spriteBatch.Draw(fishTex, Position - Main.screenPosition + offset, rect
                    , glowTint * ea, rot, fishOrigin, FishScale * (1f + echo * 0.12f), effects, 0f);
            }

            //炮口辉光本体（速度越快越亮）
            Main.spriteBatch.Draw(glow, Position - Main.screenPosition, null
                , glowTint * (a * (0.5f + speedT * 0.5f)), 0f, glowOrigin, FishScale * (0.7f + speedT * 0.7f), SpriteEffects.None, 0f);
        }

        /// <summary>本体绘制（须在 AlphaBlend 批次中调用）：清晰鱼身 + 高速白热锋面</summary>
        public void DrawBody(float globalAlpha) {
            if (Alpha < 0.04f) {
                return;
            }

            float a = Alpha * globalAlpha;
            Texture2D fishTex = GetFishTexture(FishType);
            Rectangle rect = fishTex.Bounds;
            Vector2 origin = rect.Size() * 0.5f;
            SpriteEffects effects = FishDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            float rot = FishRotation + (FishDirection > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
            Vector2 drawPos = Position - Main.screenPosition;

            Main.spriteBatch.Draw(fishTex, drawPos, rect, coreTint * a, rot, origin, FishScale, effects, 0f);

            //高速时叠一层偏白描边，强调突进锋面
            float speedT = MathHelper.Clamp(Velocity.Length() / 24f, 0f, 1f);
            if (speedT > 0.2f) {
                Color hot = Color.Lerp(coreTint, Color.White, 0.7f);
                Main.spriteBatch.Draw(fishTex, drawPos, rect, hot * (a * speedT * 0.6f), rot, origin, FishScale * 1.08f, effects, 0f);
            }
        }
    }

    /// <summary>叠加齐射的统一伤害判定弹幕：承载一束鱼群洪流</summary>
    internal class CannonFishSwarmHitbox : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private readonly List<FishEntity> fishSwarm = new();
        private int particleSpawnTimer;

        /// <summary>满层（第十眼）白金配色，由发射炮设置</summary>
        public bool Infinite { get; set; }

        public override void SetDefaults() {
            Projectile.width = 800;
            Projectile.height = 800;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 110;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 1;
            Projectile.extraUpdates = 1;
        }

        public void AddFish(FishEntity fish) {
            fishSwarm.Add(fish);
        }

        public override void AI() {
            if (Projectile.DamageType != EndlessDamageClass.Instance
                && Projectile.owner.TryGetPlayer(out var player)
                && player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                if (halibutPlayer.SeaDomainActive && halibutPlayer.SeaDomainLayers == 10) {
                    Projectile.DamageType = EndlessDamageClass.Instance;//无限叠加下的弹幕使用无限伤害类型
                }
            }

            //更新所有鱼实体
            foreach (var fish in fishSwarm) {
                fish.Update(fishSwarm);
            }

            fishSwarm.RemoveAll(f => f.ShouldRemove());

            //计算弹幕中心位置为所有鱼的平均位置，并沿洪流投光
            if (fishSwarm.Count > 0) {
                Vector2 center = Vector2.Zero;
                foreach (var fish in fishSwarm) {
                    center += fish.Position;
                }
                Projectile.Center = center / fishSwarm.Count;
                Lighting.AddLight(Projectile.Center, Infinite ? new Vector3(1f, 0.85f, 0.5f) : new Vector3(0.5f, 0.55f, 1f));
            }

            //洪流中点缀加算光点（客户端）
            particleSpawnTimer++;
            if (!Main.dedServ && particleSpawnTimer >= 5 && fishSwarm.Count > 0) {
                particleSpawnTimer = 0;
                var fish = fishSwarm[Main.rand.Next(fishSwarm.Count)];
                Color tint = Infinite ? new Color(255, 226, 150) : new Color(150, 190, 255);
                PRTLoader.NewParticle<PRT_Light>(fish.Position, -fish.Velocity * 0.15f, tint, 0.45f).Configure(14);
            }

            //如果所有鱼都消失，移除弹幕
            if (fishSwarm.Count == 0) {
                Projectile.Kill();
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //检测任意一条鱼是否与目标碰撞
            foreach (var fish in fishSwarm) {
                Rectangle fishHitbox = fish.GetHitbox();
                if (fishHitbox.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //命中冲击：加算光爆 + 偶发脉冲环
            Color tint = Infinite ? new Color(255, 232, 170) : new Color(150, 200, 255);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                PRTLoader.NewParticle<PRT_Light>(target.Center, vel, tint, Main.rand.NextFloat(0.5f, 0.9f)).Configure(20, hueShift: 0.01f);
            }
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, tint, 0.4f).Configure(0.4f, 1.4f, 14);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || fishSwarm.Count == 0) {
                return;
            }
            //消散：洪流尽头的余光
            Color tint = Infinite ? new Color(255, 226, 150) : new Color(150, 200, 255);
            for (int i = 0; i < 8; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, vel, tint, Main.rand.NextFloat(0.5f, 0.8f)).Configure(24);
            }
        }

        private static void BeginAdditiveBatch() {
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);
        }

        private static void BeginWorldAlphaBatch() {
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (fishSwarm.Count == 0) {
                return false;
            }

            //加算层：辉光本体 + 拖尾洪流 + 叠加重影（重叠的炮束自然在此处叠亮成核）
            Main.spriteBatch.End();
            BeginAdditiveBatch();
            foreach (var fish in fishSwarm) {
                fish.DrawGlow(1f);
            }
            Main.spriteBatch.End();

            //常规层：清晰鱼身
            BeginWorldAlphaBatch();
            foreach (var fish in fishSwarm) {
                fish.DrawBody(1f);
            }

            return false;
        }
    }
    #endregion

    #region 时空裂隙
    internal class TimeRift
    {
        public Vector2 Position;
        public float Life;
        public float MaxLife;
        public float Rotation;
        public float Scale;

        public TimeRift(Vector2 pos) {
            Position = pos;
            Life = 0;
            MaxLife = Main.rand.NextFloat(50f, 90f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Scale = Main.rand.NextFloat(0.6f, 1.3f);
        }

        public void Update() {
            Life++;
            Rotation += 0.04f;
        }

        public bool ShouldRemove() => Life >= MaxLife;
    }
    #endregion

    #region 能量球
    internal class EnergyOrb
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Scale;

        public EnergyOrb(Vector2 pos) {
            Position = pos;
            Velocity = Vector2.Zero;
            Life = 0;
            MaxLife = 70f;
            Scale = Main.rand.NextFloat(0.4f, 0.9f);
        }

        public void Update(Vector2 target) {
            Life++;
            Vector2 toTarget = (target - Position).SafeNormalize(Vector2.Zero);
            Velocity = Vector2.Lerp(Velocity, toTarget * 14f, 0.12f);
            Position += Velocity;
        }

        public bool ShouldRemove() => Life >= MaxLife;
    }
    #endregion
}
