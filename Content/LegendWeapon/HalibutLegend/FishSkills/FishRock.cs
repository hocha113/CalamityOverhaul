using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>岩鱼锤专属 shader 资源（域内加载器，不动 EffectLoader）</summary>
    internal class FishRockAssets
    {
        /// <summary>嵌地裂纹 decal（砸点地面短命残迹）</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishRockCrack { get; private set; }
    }

    internal class FishRock : FishSkill
    {
        public override int UnlockFishID => ItemID.Rockfish;
        public override int DefaultCooldown => 180 - HalibutData.GetDomainLayer() * 9;
        public override int ResearchDuration => 60 * 16;
        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            if (!Active(player)) {
                return false;
            }

            if (Cooldown <= 0) {
                //查找最近的敌人
                NPC target = player.Center.FindClosestNPC(800f);
                ShootState shootState = player.GetShootState();

                if (target != null) {
                    SetCooldown();

                    //生成岩鱼锤（召唤音与凝聚尘由锤体出现帧自播，各端可见）
                    Projectile.NewProjectile(
                        shootState.Source,
                        player.Center + new Vector2(0, -120), //从玩家头顶生成
                        Vector2.Zero,
                        ModContent.ProjectileType<RockHammerFish>(),
                        (int)(shootState.WeaponDamage * (3.6f + HalibutData.GetDomainLayer() * 1.2f)),
                        shootState.WeaponKnockback * 3f,
                        player.whoAmI,
                        ai0: target.whoAmI //传递目标ID
                    );
                }
            }
            return base.UpdateCooldown(halibutPlayer, player);
        }
    }

    /// <summary>
    /// 岩鱼锤弹幕，整块花岗岩的重量感呈现<br/>
    /// 悬停蓄力 = 石屑反重力上浮 + 锤体微震颤 + 缓慢上抬；下砸 = 加速度曲线 + 速度拉伸；
    /// 砸点 = 定帧 + 尘环波前 + 瓦砾抛物 + 嵌地裂纹 decal + 克制震屏，哑光零发光
    /// </summary>
    internal class RockHammerFish : BaseHeldProj, IPrimitiveDrawable
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Rockfish;

        private enum HammerState
        {
            Appearing,    //出现阶段
            Flying,       //飞行阶段
            Preparing,    //准备敲击
            Striking,     //敲击
            Returning,    //返回
            Disappearing  //消失
        }

        private HammerState State {
            get => (HammerState)Projectile.ai[1];
            set => Projectile.ai[1] = (float)value;
        }

        private ref float TargetNPCID => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[2];

        //运动参数
        private Vector2 startPos;
        private Vector2 strikeStartPos;
        private Vector2 strikeEndPos;
        private Vector2 lastCenter;
        private Vector2 moveDelta;

        //贝塞尔曲线控制点
        private Vector2 bezierP0;
        private Vector2 bezierP1;
        private Vector2 bezierP2;
        private Vector2 bezierP3;

        //旋转效果
        private float hammerRotation = 0f;
        private float targetRotation = 0f;

        //视觉效果
        private float scaleMultiplier = 1f;
        private float trembleAmp = 0f;

        //砸击反馈
        private bool impactTriggered;
        private int impactHoldFrames;
        private Vector2 impactHoldPos;

        //嵌地裂纹 decal
        private bool decalActive;
        private int decalTimer;
        private Vector2 decalCenter;
        private float decalSeed;
        /// <summary>裂纹残迹寿命（帧），砸击后独立于锤体运动计时</summary>
        private const int DecalLife = 50;
        /// <summary>砸点定帧数</summary>
        private const int ImpactHoldFrames = 4;

        //预判系统
        private Vector2 lastTargetPos = Vector2.Zero;
        private Vector2 targetVelocity = Vector2.Zero;
        private Vector2 predictedPos = Vector2.Zero;

        //各阶段持续时间
        private const int AppearDuration = 30;
        private const int FlyDuration = 40; //缩短飞行时间，更快到达
        private const int PrepareDuration = 15; //缩短准备时间
        private const int StrikeDuration = 12; //缩短敲击时间，更快速
        private const int ReturnDuration = 40;
        private const int DisappearDuration = 25;

        //命中判定半径（更大）
        private const float HitRadius = 180f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10086;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.7f;
            }
            //对Boss造成额外伤害
            if (target.boss) {
                modifiers.FinalDamage *= 1.5f;
            }
            //增加击退
            modifiers.Knockback *= 1.5f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                TriggerImpact(target.Center);
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!FishSkill.GetT<FishRock>().Active(Owner) && State != HammerState.Disappearing) {
                State = HammerState.Disappearing;
                StateTimer = 0;
            }

            StateTimer++;

            //更新目标预判
            UpdateTargetPrediction();

            switch (State) {
                case HammerState.Appearing:
                    AppearingPhaseAI();
                    break;

                case HammerState.Flying:
                    FlyingPhaseAI();
                    break;

                case HammerState.Preparing:
                    PreparingPhaseAI();
                    break;

                case HammerState.Striking:
                    StrikingPhaseAI();
                    break;

                case HammerState.Returning:
                    ReturningPhaseAI();
                    break;

                case HammerState.Disappearing:
                    DisappearingPhaseAI();
                    break;
            }

            //平滑更新旋转（WrapAngle 走短弧，避免蓄力转向时甩长弧）
            hammerRotation += MathHelper.WrapAngle(targetRotation - hammerRotation) * 0.25f;
            Projectile.rotation = hammerRotation;

            //裂纹残迹独立计时
            if (decalActive && decalTimer < DecalLife) {
                decalTimer++;
            }

            //帧位移缓存，速度拉伸与剥落尾迹的量源
            moveDelta = lastCenter == Vector2.Zero ? Vector2.Zero : Projectile.Center - lastCenter;
            lastCenter = Projectile.Center;
        }

        //更新目标预判
        private void UpdateTargetPrediction() {
            if (!IsTargetValid()) return;

            NPC target = Main.npc[(int)TargetNPCID];

            if (lastTargetPos != Vector2.Zero) {
                targetVelocity = target.Center - lastTargetPos;
            }
            lastTargetPos = target.Center;

            //预测目标位置（根据当前速度预测0.5秒后的位置）
            float predictionTime = 0.5f;
            predictedPos = target.Center + targetVelocity * predictionTime * 60f;
        }

        //出现阶段
        private void AppearingPhaseAI() {
            float progress = StateTimer / AppearDuration;
            float easeProgress = CWRUtils.EaseOutElastic(progress);

            if (StateTimer == 1) {
                startPos = Projectile.Center;
                SpawnCondenseBurst();
            }

            Projectile.Center = startPos + new Vector2(0, -30 * easeProgress);
            //一整圈定向落定
            targetRotation = MathHelper.TwoPi * VaultUtils.EaseOutCubic(progress);
            scaleMultiplier = easeProgress;

            //凝聚尾声的零星上浮石屑
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_FishRockMote>(Projectile.Center + Main.rand.NextVector2Circular(26f, 26f)
                    , new Vector2(0f, -0.4f), default, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(Main.rand.Next(12, 18));
            }

            if (StateTimer >= AppearDuration) {
                State = HammerState.Flying;
                StateTimer = 0;
                InitializeFlight();

                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                    Volume = 0.7f,
                    Pitch = -0.2f
                }, Projectile.Center);
            }
        }

        /// <summary>出现帧的凝聚演出，向心收拢尘团 + 石尘底噪 + 召唤音（各端 AI 自播）</summary>
        private void SpawnCondenseBurst() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item70 with {
                Volume = 0.7f,
                Pitch = -0.3f
            }, Projectile.Center);

            for (int i = 0; i < 6; i++) {
                float angle = MathHelper.TwoPi * i / 6f + Main.rand.NextFloat(0.5f);
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(34f, 60f);
                PRTLoader.NewParticle<PRT_FishRockDust>(Projectile.Center + offset, -offset * 0.055f
                    , default, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(18, 26), 0.42f, false, 0.006f);
            }
            for (int i = 0; i < 6; i++) {
                Dust rock = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(30f, 30f)
                    , DustID.Stone, Main.rand.NextVector2Circular(1.5f, 1.5f), 100, default, Main.rand.NextFloat(1.2f, 1.9f));
                rock.noGravity = true;
            }
        }

        private void InitializeFlight() {
            if (!IsTargetValid()) {
                State = HammerState.Returning;
                StateTimer = 0;
                return;
            }

            NPC target = Main.npc[(int)TargetNPCID];

            bezierP0 = Projectile.Center;

            //使用预测位置作为终点
            Vector2 targetPoint = predictedPos != Vector2.Zero ? predictedPos : target.Center;
            bezierP3 = targetPoint + new Vector2(0, -120); //降低高度，更容易命中

            Vector2 toTarget = bezierP3 - bezierP0;
            float distance = toTarget.Length();

            //更激进的弧线
            bezierP1 = bezierP0 + new Vector2(toTarget.X * 0.25f, -distance * 0.3f);
            float arcDirection = Math.Sign(toTarget.X) * -1;
            bezierP2 = bezierP3 + new Vector2(arcDirection * distance * 0.2f, -distance * 0.15f);
        }

        //飞行阶段
        private void FlyingPhaseAI() {
            float progress = StateTimer / FlyDuration;
            float easeProgress = VaultUtils.EaseInOutCubic(progress);

            Vector2 newPos = VaultUtils.CubicBezier(easeProgress, bezierP0, bezierP1, bezierP2, bezierP3);

            Vector2 velocity = newPos - Projectile.Center;
            if (velocity.LengthSquared() > 0.1f) {
                //朝向锁运动方向，叠加游动摆尾（接近目标时收摆）
                float sway = MathF.Sin(StateTimer * 0.45f) * 0.16f * (1f - easeProgress * 0.5f);
                targetRotation = velocity.ToRotation() + MathHelper.PiOver2 + sway;
            }

            Projectile.Center = newPos;
            scaleMultiplier = 1f;

            if (!VaultUtils.isServer) {
                //掠行剥落
                if (Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_FishRockDust>(Projectile.Center - velocity * 1.5f + Main.rand.NextVector2Circular(8f, 8f)
                        , -velocity * 0.08f, default, Main.rand.NextFloat(0.35f, 0.55f))
                        ?.Configure(Main.rand.Next(16, 24), 0.34f);
                }
                if (Main.rand.NextBool(5)) {
                    PRTLoader.NewParticle<PRT_FishRockRubble>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f)
                        , -velocity * 0.15f + new Vector2(0f, 0.6f), default, Main.rand.NextFloat(0.28f, 0.42f))
                        ?.Configure(Main.rand.Next(20, 28));
                }
            }

            if (StateTimer % 10 == 0) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.4f,
                    Pitch = 0.3f + progress * 0.4f
                }, Projectile.Center);
            }

            if (StateTimer >= FlyDuration) {
                State = HammerState.Preparing;
                StateTimer = 0;
                strikeStartPos = Projectile.Center;
            }
        }

        //准备敲击阶段，缓慢上抬悬停
        private void PreparingPhaseAI() {
            if (!IsTargetValid()) {
                State = HammerState.Returning;
                StateTimer = 0;
                trembleAmp = 0f;
                return;
            }

            float progress = StateTimer / PrepareDuration;
            NPC target = Main.npc[(int)TargetNPCID];

            //使用预测位置
            Vector2 targetPoint = predictedPos != Vector2.Zero ? predictedPos : target.Center;

            //单调上抬，easeOut 减速趋停
            float lift = VaultUtils.EaseOutCubic(progress) * 58f;
            Projectile.Center = strikeStartPos + new Vector2(0f, -lift);

            //锤头缓缓压向目标方向（下砸预指向）
            targetRotation = (targetPoint - Projectile.Center).ToRotation() + MathHelper.PiOver2;
            scaleMultiplier = 1f;
            //震颤幅度随蓄力加深（仅作绘制，不动判定）
            trembleAmp = progress;

            //反重力预告
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_FishRockMote>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-34f, 34f), Main.rand.NextFloat(26f, 78f))
                        , new Vector2(0f, Main.rand.NextFloat(-0.6f, -0.2f)), default, Main.rand.NextFloat(0.6f, 1.1f))
                        ?.Configure(Main.rand.Next(12, 18));
                }
            }

            if (StateTimer % 4 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                    Volume = 0.4f * progress,
                    Pitch = -0.3f + progress * 0.7f
                }, Projectile.Center);
            }

            if (StateTimer >= PrepareDuration) {
                State = HammerState.Striking;
                StateTimer = 0;
                strikeEndPos = targetPoint; //最终使用预测位置
                strikeStartPos = Projectile.Center; //从悬停位出发，位移连续
                trembleAmp = 0f;

                //强化重击音效
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                    Volume = 1.2f,
                    Pitch = -0.6f
                }, Projectile.Center);

                SoundEngine.PlaySound(SoundID.Item14 with {
                    Volume = 0.8f,
                    Pitch = -0.4f
                }, Projectile.Center);
            }
        }

        //敲击阶段，加速度曲线下砸，命中即定帧
        private void StrikingPhaseAI() {
            float progress = StateTimer / StrikeDuration;

            if (impactHoldFrames > 0) {
                //定帧，锤钉在命中点，挤压回弹交给绘制
                impactHoldFrames--;
                Projectile.Center = impactHoldPos;
            }
            else if (!impactTriggered) {
                float easeProgress = VaultUtils.EaseInCubic(progress);
                Projectile.Center = Vector2.Lerp(strikeStartPos, strikeEndPos, easeProgress);
                targetRotation = (strikeEndPos - strikeStartPos).ToRotation() + MathHelper.PiOver2;
            }

            scaleMultiplier = 1f;

            //下砸剥落，尾部拖出石尘
            if (!VaultUtils.isServer && !impactTriggered && moveDelta.LengthSquared() > 4f) {
                PRTLoader.NewParticle<PRT_FishRockDust>(Projectile.Center - moveDelta * 1.2f + Main.rand.NextVector2Circular(6f, 6f)
                    , -moveDelta * 0.06f, default, Main.rand.NextFloat(0.4f, 0.6f))
                    ?.Configure(Main.rand.Next(14, 20), 0.4f);
            }

            //扩大击中检测范围和时间窗口
            if (IsTargetValid() && StateTimer >= StrikeDuration / 3 && StateTimer <= StrikeDuration * 2 / 3) {
                NPC target = Main.npc[(int)TargetNPCID];
                float distance = Vector2.Distance(Projectile.Center, target.Center);

                if (distance < HitRadius) //使用更大的命中半径
                {
                    //造成伤害
                    target.SimpleStrikeNPC(Projectile.damage, 0, false, Projectile.knockBack * 2f, null, false, 0f, true);

                    TriggerImpact(target.Center);

                    //标记已命中，避免重复
                    TargetNPCID = -1;
                }
            }

            if (StateTimer >= StrikeDuration) {
                //落空轻反馈
                if (!impactTriggered) {
                    SpawnMissPuff();
                }
                impactHoldFrames = 0;
                State = HammerState.Returning;
                StateTimer = 0;
            }
        }

        /// <summary>
        /// 砸击结算演出（幂等，命中路径与碰撞路径共用）
        /// 定帧 + 克制震屏 + 三层音 + 瓦砾抛物 + 尘环波前 + 嵌地裂纹锚定
        /// </summary>
        private void TriggerImpact(Vector2 hitPos) {
            if (impactTriggered) {
                return;
            }
            impactTriggered = true;
            impactHoldFrames = ImpactHoldFrames;
            impactHoldPos = Projectile.Center;

            //嵌地裂纹与波前只在近地砸击时出现（≤8格，太远读作脱节）
            Vector2? ground = FindGroundBelow(hitPos, 8);
            if (ground.HasValue) {
                decalActive = true;
                decalTimer = 0;
                decalCenter = ground.Value + new Vector2(0f, -2f);
                decalSeed = Projectile.whoAmI * 0.61f % 1f;
            }

            //克制的定向震屏，沿下砸方向一记短震
            if (CWRServerConfig.Instance.ScreenVibration && !Main.dedServ) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(hitPos, Vector2.UnitY
                    , 6f, 5f, 10, 900f, FullName));
            }

            if (VaultUtils.isServer) {
                return;
            }

            //三层重击音
            SoundEngine.PlaySound(SoundID.Item70 with {
                Volume = 1.3f,
                Pitch = -0.5f
            }, hitPos);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                Volume = 1.2f,
                Pitch = -0.2f
            }, hitPos);
            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 1f,
                Pitch = -0.3f
            }, hitPos);

            //瓦砾抛物
            for (int i = 0; i < 12; i++) {
                float size = i < 3 ? Main.rand.NextFloat(1.1f, 1.6f)
                    : i < 8 ? Main.rand.NextFloat(0.6f, 1f)
                    : Main.rand.NextFloat(0.3f, 0.55f);
                Vector2 vel = new(Main.rand.NextFloat(-7f, 7f), Main.rand.NextFloat(-11f, -4f));
                PRTLoader.NewParticle<PRT_FishRockRubble>(hitPos + new Vector2(Main.rand.NextFloat(-26f, 26f), -6f)
                    , vel, default, size)?.Configure(Main.rand.Next(34, 55));
            }

            //尘环冲击波
            if (ground.HasValue) {
                for (int dir = -1; dir <= 1; dir += 2) {
                    for (int i = 0; i < 5; i++) {
                        float speed = MathHelper.Lerp(13f, 5f, i / 4f);
                        PRTLoader.NewParticle<PRT_FishRockDust>(
                            ground.Value + new Vector2(dir * (10f + i * 8f), -8f - i * 2f)
                            , new Vector2(dir * speed, -0.4f), default, Main.rand.NextFloat(0.5f, 0.75f))
                            ?.Configure(Main.rand.Next(22, 32), 0.55f, front: true, growth: 0.02f);
                    }
                }
            }

            //中央尘柱
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_FishRockDust>(hitPos + new Vector2(Main.rand.NextFloat(-20f, 20f), -8f)
                    , new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-4.5f, -1.5f))
                    , default, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(Main.rand.Next(30, 44), 0.5f, false, 0.018f);
            }

            //vanilla 石尘底噪
            for (int i = 0; i < 10; i++) {
                Dust debris = Dust.NewDustPerfect(hitPos + Main.rand.NextVector2Circular(24f, 10f)
                    , DustID.Stone, new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, -1f))
                    , 80, default, Main.rand.NextFloat(1f, 1.7f));
                debris.noGravity = false;
            }
        }

        /// <summary>落空反馈，几缕散尘泄掉下砸动能</summary>
        private void SpawnMissPuff() {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_FishRockDust>(Projectile.Center + Main.rand.NextVector2Circular(18f, 18f)
                    , new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-1f, 0.5f))
                    , default, Main.rand.NextFloat(0.45f, 0.7f))
                    ?.Configure(Main.rand.Next(16, 24), 0.38f);
            }
        }

        /// <summary>从指定点向下找地表，返回贴地锚点</summary>
        private static Vector2? FindGroundBelow(Vector2 from, int maxTiles) {
            Point tile = from.ToTileCoordinates();
            for (int i = 0; i < maxTiles; i++) {
                int ty = tile.Y + i;
                if (ty >= Main.maxTilesY - 10) {
                    break;
                }
                if (WorldGen.SolidTile(tile.X, ty)) {
                    return new Vector2(from.X, ty * 16f - 8f);
                }
            }
            return null;
        }

        //返回阶段
        private void ReturningPhaseAI() {
            float progress = StateTimer / ReturnDuration;
            float easeProgress = VaultUtils.EaseInOutQuad(progress);

            Vector2 returnTarget = Owner.Center + new Vector2(0, -120);
            Projectile.Center = Vector2.Lerp(Projectile.Center, returnTarget, easeProgress * 0.12f);

            //回程朝向运动方向，慢慢转正
            if (moveDelta.LengthSquared() > 1f) {
                targetRotation = moveDelta.ToRotation() + MathHelper.PiOver2;
            }
            scaleMultiplier = MathHelper.Lerp(1f, 0.9f, progress);

            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_FishRockDust>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , Main.rand.NextVector2Circular(0.5f, 0.5f), default, Main.rand.NextFloat(0.35f, 0.5f))
                    ?.Configure(Main.rand.Next(16, 22), 0.3f);
            }

            if (StateTimer >= ReturnDuration || Vector2.Distance(Projectile.Center, returnTarget) < 30f) {
                State = HammerState.Disappearing;
                StateTimer = 0;
            }
        }

        //消失阶段，崩解回石尘
        private void DisappearingPhaseAI() {
            float progress = StateTimer / DisappearDuration;
            float easeProgress = VaultUtils.EaseInCubic(progress);

            targetRotation += 0.2f * (1f - progress);
            Projectile.alpha = (int)(255 * easeProgress);
            scaleMultiplier = 1f - easeProgress * 0.5f;

            //剥蚀，锤体化回下坠石尘
            if (!VaultUtils.isServer && Main.rand.NextBool(2) && progress < 0.95f) {
                PRTLoader.NewParticle<PRT_FishRockDust>(Projectile.Center + Main.rand.NextVector2Circular(16f, 16f)
                    , new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), Main.rand.NextFloat(0.2f, 0.9f))
                    , default, Main.rand.NextFloat(0.4f, 0.65f))
                    ?.Configure(Main.rand.Next(16, 26), 0.4f);
            }

            if (StateTimer >= DisappearDuration) {
                if ((int)StateTimer == DisappearDuration) {
                    SpawnFinalEffect();
                }
                //裂纹残迹未走完则隐形滞留，画完再死
                if (!decalActive || decalTimer >= DecalLife) {
                    Projectile.Kill();
                }
            }
        }

        private bool IsTargetValid() {
            int id = (int)TargetNPCID;
            if (id < 0 || id >= Main.maxNPCs) return false;
            NPC target = Main.npc[id];
            return target.active && target.CanBeChasedBy();
        }

        /// <summary>终帧崩解，尘团散开 + 小屑坠落</summary>
        private void SpawnFinalEffect() {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_FishRockDust>(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f)
                    , Main.rand.NextVector2Circular(1.2f, 1.2f), default, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(20, 30), 0.42f);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_FishRockRubble>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-2f, 0f))
                    , default, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Main.rand.Next(24, 36));
            }

            SoundEngine.PlaySound(SoundID.Item10 with {
                Volume = 0.6f,
                Pitch = -0.2f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            //隐形滞留期
            if (Projectile.alpha >= 250) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            Texture2D hammerTex = TextureAssets.Item[ItemID.Rockfish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Rectangle sourceRect = hammerTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;
            float alpha = (255f - Projectile.alpha) / 255f;

            //蓄力微震颤，高频小位移只作用在绘制上
            if (State == HammerState.Preparing && trembleAmp > 0f) {
                float t = StateTimer * 2.9f;
                drawPos += new Vector2(MathF.Sin(t) * 1.7f, MathF.Cos(t * 1.31f) * 1.2f) * trembleAmp
                    + Main.rand.NextVector2Circular(0.7f, 0.7f) * trembleAmp;
            }

            //挤压拉伸
            Vector2 stretch = Vector2.One;
            if (State == HammerState.Striking) {
                if (impactHoldFrames > 0) {
                    float k = impactHoldFrames / (float)ImpactHoldFrames;
                    stretch = new Vector2(1f + 0.24f * k, 1f - 0.2f * k);
                }
                else if (!impactTriggered) {
                    float speedK = MathHelper.Clamp(moveDelta.Length() / 34f, 0f, 1f);
                    stretch = new Vector2(1f - 0.14f * speedK, 1f + 0.32f * speedK);
                }
            }

            //拖影，全哑光暗剪影
            if (State == HammerState.Flying || State == HammerState.Striking) {
                bool striking = State == HammerState.Striking;
                int ghostCount = striking ? 5 : 3;
                int step = striking ? 1 : 2;
                for (int i = ghostCount; i >= 1; i--) {
                    int idx = i * step;
                    if (idx >= Projectile.oldPos.Length || Projectile.oldPos[idx] == Vector2.Zero) {
                        continue;
                    }
                    float k = i / (float)(ghostCount + 1);
                    Color ghost = new Color(38, 35, 33) * (alpha * 0.42f * (1f - k));
                    Vector2 ghostPos = Projectile.oldPos[idx] + Projectile.Size / 2f - Main.screenPosition;
                    float ghostRot = idx < Projectile.oldRot.Length ? Projectile.oldRot[idx] : hammerRotation;
                    sb.Draw(hammerTex, ghostPos, sourceRect, ghost, ghostRot, origin
                        , Projectile.scale * scaleMultiplier * stretch * (1f - k * 0.18f), SpriteEffects.None, 0);
                }
            }

            //主体，哑光受环境光，零发光层
            sb.Draw(hammerTex, drawPos, sourceRect, lightColor * alpha, hammerRotation, origin
                , Projectile.scale * scaleMultiplier * stretch, SpriteEffects.None, 0);

            return false;
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !decalActive || decalTimer >= DecalLife) {
                return;
            }
            Effect fx = FishRockAssets.FishRockCrack;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                return;
            }

            float life = decalTimer / (float)DecalLife;
            Vector2 c = decalCenter + new Vector2(0f, 6f);
            const float halfX = 132f;
            const float halfY = 46f;

            var verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((c + new Vector2(-halfX, -halfY)).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((c + new Vector2(halfX, -halfY)).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((c + new Vector2(-halfX, halfY)).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((c + new Vector2(halfX, halfY)).ToVector3(), Color.White, new Vector2(1f, 1f));

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState prevBlend = device.BlendState;
            RasterizerState prevRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uLife"]?.SetValue(life);
            fx.Parameters["uSeed"]?.SetValue(decalSeed);
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }

            device.BlendState = prevBlend;
            device.RasterizerState = prevRaster;
        }

        public override bool? CanDamage() => State == HammerState.Striking;
    }
}
