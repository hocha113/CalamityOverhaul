using CalamityMod.Items.Weapons.Ranged;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills
{
    /// <summary>
    /// 叠加攻击技能 - 时空克隆体汇聚后释放大比目鱼炮齐射
    /// </summary>
    internal static class Superposition
    {
        public static int ID = 6;
        private const int ToggleCD = 30;
        private const int SuperpositionCooldown = 1800; //30秒终极技能冷却

        public static void AltUse(Item item, Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (hp.SuperpositionToggleCD > 0 || hp.SuperpositionCooldown > 0) {
                return;
            }

            Activate(player);
            hp.SuperpositionToggleCD = ToggleCD;
            hp.SuperpositionCooldown = 60; //调试用
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
    /// <summary>
    /// 时空克隆体 - 从远处环绕聚拢的过去玩家影像
    /// </summary>
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

        private const int MaxTrailLength = 28;
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

            Texture2D tex = TextureAssets.MagicPixel.Value;

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
    /// <summary>
    /// 法阵符环 - 椭圆形魔法阵特效
    /// </summary>
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

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            int segments = 120;
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

    /// <summary>
    /// 叠加攻击主控制弹幕
    /// </summary>
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
        private float chargeIntensity = 0f;
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
            float progress = stateTimer / (float)ChargeDuration;
            chargeIntensity = progress;
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

            //安全检查：确保炮阵已生成
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
            int cloneCount = 26;
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

        private void DrawTimeClone(TimeClone clone) {
            if (clone.Alpha < 0.05f) {
                return;
            }

            Player ghost = new Player();
            ghost.ResetEffects();
            ghost.CopyVisuals(Owner);
            ghost.position = clone.Position - Owner.Size * 0.5f;
            ghost.direction = Owner.direction;
            ghost.bodyFrame = Owner.bodyFrame;
            ghost.legFrame = Owner.legFrame;

            Color ghostColor = new Color(170, 130, 255) * clone.Alpha * 0.9f;
            ghost.skinColor = ghostColor;
            ghost.shirtColor = ghostColor;
            ghost.underShirtColor = ghostColor;
            ghost.pantsColor = ghostColor;
            ghost.shoeColor = ghostColor;
            ghost.hairColor = ghostColor;
            ghost.eyeColor = ghostColor;

            try {
                Main.PlayerRenderer.DrawPlayer(
                    Main.Camera,
                    ghost,
                    ghost.position,
                    0f,
                    ghost.fullRotationOrigin
                );
            } catch {
                //忽略渲染异常
            }
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
            if (timeClones != null) {
                foreach (var clone in timeClones) {
                    DrawTimeClone(clone);
                }
            }

            return false;
        }
    }

    #region 齐射炮弹幕
    /// <summary>
    /// 大比目鱼炮 - 齐射弹幕发射器
    /// </summary>
    internal class SuperpositionCannon : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private Player Owner => Main.player[Projectile.owner];

        private enum CannonState
        {
            Deploy,  //部署展开
            Charge,  //充能准备
            Volley,  //齐射发射
            Finish   //完成淡出
        }

        private CannonState state = CannonState.Deploy;
        private int timer;
        private int volleyIndex;

        private const int DeployTime = 20;
        private const int ChargeTime = 30;
        private const int VolleyCount = 4;
        private const int VolleySpacing = 12;

        private float angleOffset;
        private float pulse;

        public bool Completed => state == CannonState.Finish;

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
            }

            timer++;

            //计算位置和方向
            Vector2 direction = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.UnitX);
            Vector2 backDir = -direction;
            Vector2 perpendicular = direction.RotatedBy(MathHelper.PiOver2);

            //位置跟随玩家（保持相对队形）
            Vector2 basePosition = Owner.Center + backDir * -80f;
            Vector2 offset = backDir.RotatedBy(angleOffset) * 20f +
                            perpendicular * (float)Math.Sin(angleOffset) * 40f;
            Projectile.Center = Vector2.Lerp(Projectile.Center, basePosition + offset, 0.15f);

            //更新玩家朝向
            Owner.direction = Math.Sign(direction.X);

            //状态机
            switch (state) {
                case CannonState.Deploy:
                    UpdateDeploy();
                    break;
                case CannonState.Charge:
                    UpdateCharge();
                    break;
                case CannonState.Volley:
                    UpdateVolley(direction);
                    break;
                case CannonState.Finish:
                    UpdateFinish();
                    break;
            }
        }

        private void UpdateDeploy() {
            pulse = timer / (float)DeployTime;

            if (timer >= DeployTime) {
                state = CannonState.Charge;
                timer = 0;
                SoundEngine.PlaySound(
                    SoundID.Item34 with { Pitch = -0.5f },
                    Projectile.Center
                );
            }
        }

        private void UpdateCharge() {
            pulse = (float)Math.Sin(timer / (float)ChargeTime * MathHelper.Pi);

            //充能粒子
            if (timer % 6 == 0) {
                int dust = Dust.NewDust(
                    Projectile.Center - new Vector2(8, 8),
                    16, 16,
                    DustID.Water,
                    0, 0, 150,
                    new Color(160, 200, 255),
                    1.3f
                );
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * -2f +
                                           Main.rand.NextVector2Circular(1f, 1f);
            }

            if (timer >= ChargeTime) {
                state = CannonState.Volley;
                timer = 0;
                volleyIndex = 0;
                SoundEngine.PlaySound(SoundID.Item82, Projectile.Center);
            }
        }

        private void UpdateVolley(Vector2 forward) {
            pulse = 1f;

            if (timer == 1) {
                FireVolley(forward);
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

        private void UpdateFinish() {
            pulse *= 0.92f;

            if (pulse < 0.05f) {
                Projectile.Kill();
            }
        }

        private void FireVolley(Vector2 forward) {
            var source = Projectile.GetSource_FromThis();
            int fishPerVolley = 10;
            float spread = MathHelper.ToRadians(18f);
            ShootState shootState = Owner.GetShootState();

            //发射鱼群弹幕
            for (int i = 0; i < fishPerVolley; i++) {
                float lerpFactor = (i + 0.5f) / fishPerVolley;
                float angle = spread * (lerpFactor - 0.5f);
                Vector2 velocity = forward.RotatedBy(angle) * Main.rand.NextFloat(14f, 18f);

                int projId = Projectile.NewProjectile(
                    source,
                    Projectile.Center + forward * 30f,
                    velocity,
                    ModContent.ProjectileType<CannonFishShot>(),
                    shootState.WeaponDamage * 10,
                    shootState.WeaponKnockback,
                    Owner.whoAmI,
                    Main.rand.Next(9999)
                );

                if (projId >= 0) {
                    Main.projectile[projId].friendly = true;
                }
            }

            //环形粒子特效
            for (int k = 0; k < 14; k++) {
                float angle = MathHelper.TwoPi * k / 14f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f) + forward * 6f;
                int dust = Dust.NewDust(
                    Projectile.Center, 1, 1,
                    DustID.Water,
                    0, 0, 100,
                    new Color(180, 220, 255),
                    1.4f
                );
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = velocity;
            }

            SoundEngine.PlaySound(
                SoundID.Item11 with { Volume = 0.6f, Pitch = -0.2f },
                Projectile.Center
            );
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ModContent.ItemType<HalibutCannon>());
            Texture2D texture = TextureAssets.Item[ModContent.ItemType<HalibutCannon>()].Value;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;

            Vector2 direction = (Main.MouseWorld - Owner.Center).SafeNormalize(Vector2.UnitX);
            SpriteEffects spriteEffects = direction.X > 0
                ? SpriteEffects.None
                : SpriteEffects.FlipHorizontally;
            float rotation = direction.ToRotation() + (direction.X > 0 ? 0 : MathHelper.Pi);

            //后坐力效果
            float recoil = (state == CannonState.Volley && timer < 4) ? -6f : 0f;
            drawPosition += direction * recoil;

            float scale = 0.8f + pulse * 0.25f;

            //发光层
            Color glowColor = new Color(180, 200, 255, 0) * pulse * 0.6f;
            for (int i = 0; i < 3; i++) {
                Vector2 offset = (i * MathHelper.TwoPi / 3f).ToRotationVector2() * pulse * 4f;
                Main.spriteBatch.Draw(
                    texture,
                    drawPosition + offset,
                    null,
                    glowColor,
                    rotation,
                    origin,
                    scale,
                    spriteEffects,
                    0f
                );
            }

            //主体绘制
            Main.spriteBatch.Draw(
                texture,
                drawPosition,
                null,
                Color.White,
                rotation,
                origin,
                scale,
                spriteEffects,
                0f
            );

            return false;
        }
    }

    /// <summary>
    /// 齐射鱼群弹幕 - 带环绕小鱼和群聚行为
    /// </summary>
    internal class CannonFishShot : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private float seed;
        private bool initialized;
        private float alpha;
        private int fishType;
        private float fishScale;
        private float fishRotation;
        private int fishDirection;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.extraUpdates = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 1;
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
            }

            alpha = Math.Min(1f, alpha + 0.1f);

            //波动运动
            Projectile.velocity *= 0.998f;
            Vector2 waveOffset = new Vector2(0, (float)Math.Sin((Projectile.timeLeft + seed) * 0.2f)) * 0.06f;
            Projectile.velocity += waveOffset;

            //群聚行为
            ApplyCohesion();

            //更新朝向和旋转
            UpdateRotation();

            //生成轨迹粒子
            SpawnTrailParticles();
        }

        private void Initialize() {
            initialized = true;
            seed = Projectile.ai[0];
            alpha = 0f;
            fishType = Main.rand.Next(3); //0=Tuna, 1=Bass, 2=Trout
            fishScale = 0.6f + Main.rand.NextFloat() * 0.3f;
            fishDirection = Projectile.velocity.X > 0 ? 1 : -1;
        }

        private void ApplyCohesion() {
            Vector2 cohesion = Vector2.Zero;
            int nearbyCount = 0;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (i == Projectile.whoAmI) {
                    continue;
                }

                Projectile other = Main.projectile[i];
                if (!other.active || other.type != Projectile.type || other.owner != Projectile.owner) {
                    continue;
                }

                float distance = Vector2.Distance(Projectile.Center, other.Center);
                if (distance < 100f && distance > 0.1f) {
                    cohesion += (other.Center - Projectile.Center).SafeNormalize(Vector2.Zero) / distance;
                    nearbyCount++;
                }
            }

            if (nearbyCount > 0) {
                cohesion /= nearbyCount;
                Projectile.velocity += cohesion * 0.15f;
            }
        }

        private void UpdateRotation() {
            if (Math.Abs(Projectile.velocity.X) > 0.5f) {
                fishDirection = Projectile.velocity.X > 0 ? 1 : -1;
            }

            if (Projectile.velocity.LengthSquared() > 0.1f) {
                fishRotation = Projectile.velocity.ToRotation();
            }
        }

        private void SpawnTrailParticles() {
            if (Main.rand.NextBool(4)) {
                int dust = Dust.NewDust(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Water,
                    0, 0, 150,
                    new Color(150, 210, 255),
                    1.0f
                );
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = -Projectile.velocity * 0.3f;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 3; i++) {
                int dust = Dust.NewDust(
                    target.position,
                    target.width,
                    target.height,
                    DustID.Water,
                    0, 0, 150,
                    new Color(140, 210, 255),
                    1.2f
                );
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity *= 0.4f;
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 8; i++) {
                int dust = Dust.NewDust(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Water,
                    0, 0, 120,
                    new Color(160, 220, 255),
                    1.4f
                );
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = Main.rand.NextVector2Circular(3f, 3f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int itemType = fishType switch {
                0 => ItemID.Tuna,
                1 => ItemID.Bass,
                2 => ItemID.Trout,
                _ => ItemID.Tuna
            };

            Main.instance.LoadItem(itemType);
            Texture2D fishTexture = TextureAssets.Item[itemType].Value;

            //绘制拖尾
            DrawTrail(fishTexture);

            //绘制主体
            DrawMainFish(fishTexture);

            return false;
        }

        private void DrawTrail(Texture2D texture) {
            Rectangle rect = texture.Bounds;
            Vector2 origin = rect.Size() * 0.5f;
            SpriteEffects effects = fishDirection > 0
                ? SpriteEffects.None
                : SpriteEffects.FlipVertically;
            float rotation = fishRotation + (fishDirection > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }

                float trailProgress = i / (float)Projectile.oldPos.Length;
                float trailAlpha = alpha * (1f - trailProgress) * 0.5f;
                Vector2 trailPosition = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float trailScale = fishScale * 0.8f * (1f - trailProgress * 0.3f);

                Main.spriteBatch.Draw(
                    texture,
                    trailPosition,
                    rect,
                    new Color(140, 200, 255) * trailAlpha,
                    rotation,
                    origin,
                    trailScale,
                    effects,
                    0f
                );
            }
        }

        private void DrawMainFish(Texture2D texture) {
            Rectangle rect = texture.Bounds;
            Vector2 origin = rect.Size() * 0.5f;
            SpriteEffects effects = fishDirection > 0
                ? SpriteEffects.None
                : SpriteEffects.FlipVertically;
            float rotation = fishRotation + (fishDirection > 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.Draw(
                texture,
                drawPosition,
                rect,
                Color.White * alpha,
                rotation,
                origin,
                fishScale,
                effects,
                0f
            );
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
