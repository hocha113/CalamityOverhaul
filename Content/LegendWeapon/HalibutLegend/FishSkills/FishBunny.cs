using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
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
    /// <summary>兔子鱼技能，抛射跳跃爆炸鱼</summary>
    internal class FishBunny : FishSkill
    {
        public override int UnlockFishID => ItemID.Bunnyfish;
        public override int DefaultCooldown => 60 * (15 - HalibutData.GetDomainLayer());
        public override int ResearchDuration => 60 * 12;
        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse != 2) {
                return null;
            }

            if (player.whoAmI != Main.myPlayer) {
                return false;
            }

            if (Cooldown > 0) {
                return false;
            }

            item.UseSound = null;
            Vector2 velocity = player.To(Main.MouseWorld).UnitVector() * 12f;
            Vector2 position = player.Center;
            ShootState shootState = player.GetShootState();
            var source = shootState.Source;
            int damage = shootState.WeaponDamage * 2;
            float knockback = shootState.WeaponKnockback;

            SetCooldown();

            //丢出兔子鱼的数量随领域层数增加
            int bunnyCount = 2 + HalibutData.GetDomainLayer() / 2;

            for (int i = 0; i < bunnyCount; i++) {
                //随机抛射角度和速度
                float throwAngle = velocity.ToRotation() + Main.rand.NextFloat(-0.4f, 0.4f);
                float throwSpeed = Main.rand.NextFloat(10f, 16f);
                Vector2 throwVelocity = throwAngle.ToRotationVector2() * throwSpeed;
                throwVelocity.Y -= Main.rand.NextFloat(3f, 6f);

                Projectile.NewProjectile(
                    source,
                    position,
                    throwVelocity,
                    ModContent.ProjectileType<BunnyfishHopper>(),
                    (int)(damage * (1.5f + HalibutData.GetDomainLayer() * 0.35f)),
                    knockback * 2f,
                    player.whoAmI
                );
            }

            //丢出音效
            SoundEngine.PlaySound(SoundID.Item1 with {
                Volume = 0.6f,
                Pitch = 0.3f
            }, position);

            //兔子叫声
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.5f,
                Pitch = 0.8f
            }, position);

            //生成抛掷粒子
            SpawnThrowEffect(position, velocity);
            return false;
        }

        //抛掷特效：出手方向奶粉绒毛扑 + 两粒卡通星点
        private static void SpawnThrowEffect(Vector2 position, Vector2 direction) {
            Vector2 dir = direction.SafeNormalize(Vector2.Zero);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = dir.RotatedByRandom(0.55f) * Main.rand.NextFloat(1.6f, 4.2f);
                PRTLoader.NewParticle<PRT_FishBunnyFluff>(position + Main.rand.NextVector2Circular(6f, 6f)
                    , vel, FishBunnyPalette.Fluff(), Main.rand.NextFloat(0.026f, 0.042f))
                    ?.Configure(Main.rand.Next(34, 50));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishBunnyStar>(position + dir * 14f + Main.rand.NextVector2Circular(8f, 8f)
                    , dir.RotatedByRandom(0.4f) * Main.rand.NextFloat(1f, 2.5f)
                    , Color.Lerp(FishBunnyPalette.HeartFlush, FishBunnyPalette.EmberHot, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.28f, 0.4f))?.Configure(Main.rand.Next(14, 20));
            }
            //少量尘做底噪
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(position, DustID.Smoke
                    , dir.RotatedByRandom(0.5f) * Main.rand.NextFloat(2f, 5f)
                    , 140, new Color(210, 196, 200), Main.rand.NextFloat(0.8f, 1.3f));
                dust.noGravity = true;
            }
        }
    }

    /// <summary>兔子鱼跳跃弹幕，落地/追击后爆炸</summary>
    internal class BunnyfishHopper : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Bunnyfish;

        //兔子状态
        private enum BunnyState
        {
            Airborne,   //空中
            OnGround,   //地面
            Chasing,    //追击
            Exploding   //爆炸
        }

        private BunnyState State {
            get => (BunnyState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float BunnyLife => ref Projectile.ai[1];
        private ref float TargetNPCID => ref Projectile.ai[2];

        //跳跃参数
        private int groundTime = 0;
        private const int MinGroundTime = 8;
        private const int MaxGroundTime = 25;
        private const float JumpForce = 12f;
        private const float ChaseJumpForce = 15f;

        //物理参数
        private const float Gravity = 0.4f;
        private const float GroundFriction = 0.88f;
        private const float AirResistance = 0.98f;
        private const float MaxFallSpeed = 18f;

        //追击参数
        private const float DetectionRange = 600f;
        private const float ChaseRange = 400f;

        //生物动画
        private float squashStretch = 1f;
        private float bodyRotation = 0f;
        private int idleAnimTimer = 0;
        /// <summary>本次落地的压扁深度，按坠落速度决定，越高摔越扁</summary>
        private float impactSquash = 0.7f;
        /// <summary>预定起跳帧：一次落地只掷一次骰子，末尾几帧留给下蹲预告</summary>
        private int plannedJumpTime = 0;
        private bool crouching = false;
        private bool apexFluffDone = false;

        //心跳预警
        private float beatPhase = 0f;
        /// <summary>心跳亮度包络，拍点置 1 后指数衰减</summary>
        private float beatEnvelope = 0f;
        /// <summary>lub-dub 第二次弱搏的延迟计时</summary>
        private int dubTimer = 0;
        private bool heartbeatPrimed = false;
        private int alertCooldown = 0;

        //爆炸参数
        private const int MaxLifeTime = 600;
        private const int ExplosionRadius = 100;

        public override void SetStaticDefaults() {
            //空中大弧跳的幽灵残影缓存
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = MaxLifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage /= 2f;
            }
        }

        public override void AI() {
            BunnyLife++;
            if (alertCooldown > 0) {
                alertCooldown--;
            }

            //状态机
            switch (State) {
                case BunnyState.Airborne:
                    AirbornePhaseAI();
                    break;
                case BunnyState.OnGround:
                    OnGroundPhaseAI();
                    break;
                case BunnyState.Chasing:
                    ChasingPhaseAI();
                    break;
                case BunnyState.Exploding:
                    ExplodingPhaseAI();
                    break;
            }

            //更新生物动画
            UpdateBunnyAnimation();

            //引信预热：寿命后段心跳隐约响起，进入爆炸预警后由 ExplodingPhaseAI 接管提速
            if (State != BunnyState.Exploding && Projectile.timeLeft < 150) {
                float preUrgency = MathHelper.Clamp(1f - (Projectile.timeLeft - 30f) / 120f, 0f, 1f);
                float interval = MathHelper.Lerp(30f, 16f, preUrgency);
                beatPhase += 1f / interval;
                if (beatPhase >= 1f) {
                    beatPhase -= 1f;
                    beatEnvelope = MathF.Max(beatEnvelope, 0.55f);
                    dubTimer = 6;
                    SoundEngine.PlaySound(SoundID.NPCHit1 with {
                        Volume = 0.16f,
                        Pitch = -0.72f,
                        MaxInstances = 5
                    }, Projectile.Center);
                }
                if (dubTimer > 0 && --dubTimer == 0) {
                    beatEnvelope = MathF.Max(beatEnvelope, 0.3f);
                }
                beatEnvelope *= 0.84f;
            }

            //兔子粉色照明：心跳期随包络搏动
            float lightIntensity = 0.5f + beatEnvelope * 0.6f;
            Lighting.AddLight(Projectile.Center,
                1.0f * lightIntensity,
                0.7f * lightIntensity,
                0.8f * lightIntensity);

            //接近寿命终点进入爆炸
            if (Projectile.timeLeft <= 30 && State != BunnyState.Exploding) {
                State = BunnyState.Exploding;
            }
        }

        //空中状态
        private void AirbornePhaseAI() {
            //应用重力
            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }

            //空气阻力
            Projectile.velocity.X *= AirResistance;

            //身体旋转跟随速度方向
            if (Projectile.velocity.LengthSquared() > 4f) {
                bodyRotation = MathHelper.Lerp(bodyRotation, Projectile.velocity.Y * 0.05f, 0.2f);
            }

            //弧顶拍：过顶瞬间掉一撮绒毛，标记大弧跳最高点
            if (!apexFluffDone && Projectile.velocity.Y > -0.5f) {
                apexFluffDone = true;
                if (!Main.dedServ) {
                    PRTLoader.NewParticle<PRT_FishBunnyFluff>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f)
                        , new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -0.4f)
                        , FishBunnyPalette.Fluff(), Main.rand.NextFloat(0.028f, 0.042f))
                        ?.Configure(Main.rand.Next(40, 60));
                }
            }

            //飞行途中零星掉毛，慢飘在弧线后方
            if (!Main.dedServ && BunnyLife % 9 == 0 && Main.rand.NextBool(3, 5)) {
                PRTLoader.NewParticle<PRT_FishBunnyFluff>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f)
                    , -Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.4f, 0.4f)
                    , FishBunnyPalette.Fluff(), Main.rand.NextFloat(0.022f, 0.036f))
                    ?.Configure(Main.rand.Next(36, 56));
            }
        }

        //地面状态
        private void OnGroundPhaseAI() {
            groundTime++;

            //地面摩擦
            Projectile.velocity.X *= GroundFriction;
            Projectile.velocity.Y = 0;

            //身体恢复水平
            bodyRotation = MathHelper.Lerp(bodyRotation, 0, 0.3f);

            //寻找敌人
            NPC target = Projectile.Center.FindClosestNPC(DetectionRange);

            if (target != null) {
                TargetNPCID = target.whoAmI;
                State = BunnyState.Chasing;
                groundTime = 0;
                plannedJumpTime = 0;
                crouching = false;
                //目标在追击半径边缘抖动会来回切状态，警觉提示限频防刷屏
                if (alertCooldown <= 0) {
                    alertCooldown = 45;
                    SpawnAlertMark();
                }
                return;
            }

            //落地时掷一次骰子定起跳帧，末 4 帧下蹲蓄力
            if (plannedJumpTime <= 0) {
                plannedJumpTime = Main.rand.Next(MinGroundTime, MaxGroundTime);
            }
            crouching = groundTime >= plannedJumpTime - 4;

            if (groundTime >= plannedJumpTime) {
                PerformJump(false);
                groundTime = 0;
                plannedJumpTime = 0;

                //兔子跳跃音
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.3f,
                    Pitch = 0.5f
                }, Projectile.Center);
            }
        }

        //追击状态
        private void ChasingPhaseAI() {
            groundTime++;

            //地面摩擦
            Projectile.velocity.X *= GroundFriction;
            Projectile.velocity.Y = 0;

            //验证目标
            if (!IsTargetValid()) {
                State = BunnyState.OnGround;
                groundTime = 0;
                plannedJumpTime = 0;
                return;
            }

            NPC target = Main.npc[(int)TargetNPCID];

            //目标距离判断
            float distanceToTarget = Vector2.Distance(Projectile.Center, target.Center);

            if (distanceToTarget > ChaseRange) {
                //目标太远,返回普通状态
                State = BunnyState.OnGround;
                groundTime = 0;
                plannedJumpTime = 0;
                return;
            }

            //计算跳跃方向
            Vector2 toTarget = target.Center - Projectile.Center;

            //追击节奏更急：短窗掷骰，末 3 帧下蹲
            if (plannedJumpTime <= 0) {
                plannedJumpTime = Main.rand.Next(5, 15);
            }
            crouching = groundTime >= plannedJumpTime - 3;

            //朝向目标跳跃
            if (groundTime >= plannedJumpTime) {
                //计算跳跃速度
                float horizontalSpeed = Math.Abs(toTarget.X) < 100f ? 6f : 9f;
                Projectile.velocity.X = Math.Sign(toTarget.X) * horizontalSpeed;
                Projectile.velocity.Y = -ChaseJumpForce;

                //根据高度差调整跳跃力度
                if (toTarget.Y < -100f) {
                    Projectile.velocity.Y -= 2f;
                }

                State = BunnyState.Airborne;
                groundTime = 0;
                plannedJumpTime = 0;
                crouching = false;
                apexFluffDone = false;
                squashStretch = 1.38f;

                //追击跳音
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.4f,
                    Pitch = 0.7f
                }, Projectile.Center);

                //跳跃粒子
                SpawnJumpParticle();
            }
        }

        //爆炸状态：粉红心跳，拍点随倒计时逼近越跳越快
        private void ExplodingPhaseAI() {
            //停止移动
            Projectile.velocity *= 0.85f;

            //进入预警的第一帧立刻给一拍
            if (!heartbeatPrimed) {
                heartbeatPrimed = true;
                beatPhase = 1f;
            }

            //拍点间隔从 15 帧收紧到 5.5 帧
            float urgency = 1f - Projectile.timeLeft / 30f;
            float interval = MathHelper.Lerp(15f, 5.5f, MathHelper.Clamp(urgency, 0f, 1f));
            beatPhase += 1f / interval;

            if (beatPhase >= 1f) {
                beatPhase -= 1f;
                beatEnvelope = 1f;
                dubTimer = 5;

                //闷响心跳声，节奏加速全靠拍点密度
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.3f,
                    Pitch = -0.72f,
                    MaxInstances = 5
                }, Projectile.Center);

                if (!Main.dedServ) {
                    //每拍一圈粉色脉搏环，越急越大越亮
                    PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(Projectile.Center, Vector2.Zero
                        , FishBunnyPalette.HeartFlush * (0.30f + 0.30f * urgency), 0.04f)
                        ?.Configure(0.04f, 0.15f + 0.10f * urgency, 11);

                    //急拍时毛被心跳挤出来
                    if (interval < 10f) {
                        for (int i = 0; i < 2; i++) {
                            PRTLoader.NewParticle<PRT_FishBunnyFluff>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f)
                                , Main.rand.NextVector2Circular(1.4f, 1f) - Vector2.UnitY * 0.5f
                                , FishBunnyPalette.Fluff(), Main.rand.NextFloat(0.024f, 0.038f))
                                ?.Configure(Main.rand.Next(30, 46));
                        }
                    }
                }
            }

            //lub-dub 第二弱搏
            if (dubTimer > 0 && --dubTimer == 0) {
                beatEnvelope = MathF.Max(beatEnvelope, 0.55f);
            }
            beatEnvelope *= 0.84f;

            //心跳物理挤压替代旧的正弦抖动
            squashStretch = 1f + beatEnvelope * 0.14f;
        }

        //执行跳跃
        private void PerformJump(bool isChase) {
            float jumpPower = isChase ? ChaseJumpForce : JumpForce;

            //随机水平速度
            float horizontalSpeed = Main.rand.NextFloat(3f, 7f) * (Main.rand.NextBool() ? 1 : -1);

            Projectile.velocity.X = horizontalSpeed;
            Projectile.velocity.Y = -jumpPower;

            State = BunnyState.Airborne;
            crouching = false;
            apexFluffDone = false;
            //蹬地瞬间过冲拉伸
            squashStretch = 1.38f;

            //跳跃粒子
            SpawnJumpParticle();
        }

        //锁定目标提示：一粒警觉星点弹出 + 短促尖叫
        private void SpawnAlertMark() {
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.32f,
                Pitch = 0.92f,
                MaxInstances = 4
            }, Projectile.Center);

            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_FishBunnyStar>(Projectile.Top - Vector2.UnitY * 8f
                , -Vector2.UnitY * 1.2f, FishBunnyPalette.HeartFlush, 0.42f)?.Configure(16);
        }

        //验证目标有效性
        private bool IsTargetValid() {
            int targetID = (int)TargetNPCID;
            if (targetID < 0 || targetID >= Main.maxNPCs) return false;

            NPC target = Main.npc[targetID];
            return target.active && target.CanBeChasedBy();
        }

        //更新兔子动画
        private void UpdateBunnyAnimation() {
            idleAnimTimer++;

            //空中拉伸
            if (State == BunnyState.Airborne) {
                float speedRatio = Math.Abs(Projectile.velocity.Y) / MaxFallSpeed;
                float targetSquash = MathHelper.Lerp(1f, 1.3f, speedRatio);
                squashStretch = MathHelper.Lerp(squashStretch, targetSquash, 0.2f);
            }
            //地面压扁
            else if (State == BunnyState.OnGround || State == BunnyState.Chasing) {
                //起跳前下蹲蓄力
                if (crouching) {
                    squashStretch = MathHelper.Lerp(squashStretch, 0.68f, 0.35f);
                }
                //着地瞬间按坠速压扁
                else if (groundTime < 5) {
                    squashStretch = MathHelper.Lerp(squashStretch, impactSquash, 0.3f);
                }
                else if (State == BunnyState.Chasing) {
                    //追击时紧张
                    float tension = (float)Math.Sin(idleAnimTimer * 0.15f) * 0.08f;
                    squashStretch = MathHelper.Lerp(squashStretch, 1f + tension, 0.15f);
                }
                else {
                    //呼吸效果
                    float breathe = (float)Math.Sin(idleAnimTimer * 0.1f) * 0.05f;
                    squashStretch = MathHelper.Lerp(squashStretch, 1f + breathe, 0.1f);
                }
            }

            //记录到弹幕旋转，让残影缓存拿到同样的倾角
            Projectile.rotation = bodyRotation;
        }

        //跳跃粒子：蹬地尘 + 掉毛
        private void SpawnJumpParticle() {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishBunnySmoke>(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-8f, 8f), -2f)
                    , new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-0.3f, 0.1f))
                    , Color.White, Main.rand.NextFloat(0.05f, 0.08f))
                    ?.Configure(Main.rand.Next(20, 30), new Color(206, 192, 184), new Color(128, 120, 116), 1.012f, 0f);
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishBunnyFluff>(Projectile.Bottom - Vector2.UnitY * 6f
                    , new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), Main.rand.NextFloat(-1.2f, -0.3f))
                    , FishBunnyPalette.Fluff(), Main.rand.NextFloat(0.024f, 0.038f))
                    ?.Configure(Main.rand.Next(34, 52));
            }
            //尘底噪
            for (int i = 0; i < 3; i++) {
                Dust jump = Dust.NewDustPerfect(Projectile.Bottom
                    , DustID.Smoke
                    , new Vector2(Main.rand.NextFloat(-2.4f, 2.4f), Main.rand.NextFloat(0.5f, 2f))
                    , 140, new Color(190, 182, 186), Main.rand.NextFloat(0.8f, 1.3f));
                jump.noGravity = false;
            }
        }

        //地面碰撞
        public override bool OnTileCollide(Vector2 oldVelocity) {
            //着地判断
            if (State == BunnyState.Airborne && Projectile.velocity.Y == 0) {
                State = BunnyState.OnGround;
                groundTime = 0;
                plannedJumpTime = 0;

                //坠速越大摔得越扁，落地尘环也越大
                float impact = MathHelper.Clamp(Math.Abs(oldVelocity.Y) / MaxFallSpeed, 0f, 1f);
                impactSquash = MathHelper.Lerp(0.78f, 0.52f, impact);

                //着地音效：摔得越重越沉
                SoundEngine.PlaySound(SoundID.Dig with {
                    Volume = 0.24f + 0.18f * impact,
                    Pitch = 0.5f - 0.35f * impact
                }, Projectile.Center);

                SpawnLandingRing(impact);
                return false;
            }

            //墙壁碰撞反弹
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.6f;

                //够快的撞墙才配一记软弹尘与闷响，慢速蹭墙不出声
                if (Math.Abs(oldVelocity.X) > 2.5f && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Dig with {
                        Volume = 0.22f,
                        Pitch = -0.1f,
                        MaxInstances = 4
                    }, Projectile.Center);

                    float dir = Math.Sign(Projectile.velocity.X);
                    for (int i = 0; i < 2; i++) {
                        PRTLoader.NewParticle<PRT_FishBunnySmoke>(Projectile.Center + new Vector2(-dir * 10f, Main.rand.NextFloat(-6f, 6f))
                            , new Vector2(dir * Main.rand.NextFloat(0.8f, 1.6f), Main.rand.NextFloat(-0.5f, 0.2f))
                            , Color.White, Main.rand.NextFloat(0.045f, 0.07f))
                            ?.Configure(Main.rand.Next(18, 26), new Color(206, 192, 184), new Color(126, 118, 114), 1.012f, 0f);
                    }
                    PRTLoader.NewParticle<PRT_FishBunnyFluff>(Projectile.Center - new Vector2(dir * 8f, 0f)
                        , new Vector2(dir * 0.6f, -0.8f), FishBunnyPalette.Fluff(), Main.rand.NextFloat(0.024f, 0.036f))
                        ?.Configure(Main.rand.Next(32, 48));
                }
            }

            return false;
        }

        //落地尘环：贴地横扫的左右哑光尘团 + 顶起的绒毛
        private void SpawnLandingRing(float impact) {
            if (Main.dedServ) {
                return;
            }
            int puffs = 3 + (int)(impact * 3f);
            for (int i = 0; i < puffs; i++) {
                //左右对开、贴地压平的尘
                float side = i % 2 == 0 ? 1f : -1f;
                PRTLoader.NewParticle<PRT_FishBunnySmoke>(Projectile.Bottom + new Vector2(side * Main.rand.NextFloat(4f, 14f), -3f)
                    , new Vector2(side * Main.rand.NextFloat(1.4f, 3f + impact * 2f), Main.rand.NextFloat(-0.25f, 0f))
                    , Color.White, Main.rand.NextFloat(0.05f, 0.09f) * (0.8f + impact * 0.5f))
                    ?.Configure(Main.rand.Next(22, 34), new Color(206, 192, 184), new Color(126, 118, 114), 1.014f, 0f);
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishBunnyFluff>(Projectile.Top + Main.rand.NextVector2Circular(8f, 4f)
                    , new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(-1.6f, -0.6f))
                    , FishBunnyPalette.Fluff(), Main.rand.NextFloat(0.024f, 0.04f))
                    ?.Configure(Main.rand.Next(36, 54));
            }
            //尘底噪
            for (int i = 0; i < 4; i++) {
                Dust land = Dust.NewDustDirect(Projectile.Bottom - new Vector2(Projectile.width * 0.5f, 5f)
                    , Projectile.width, 5, DustID.Smoke
                    , Scale: Main.rand.NextFloat(0.9f, 1.4f));
                land.velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1f, 0.2f));
                land.color = new Color(196, 186, 184);
                land.alpha = 140;
            }
        }

        //击中敌人
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //标记进入爆炸
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, 3);
            State = BunnyState.Exploding;
        }

        //死亡爆炸
        public override void OnKill(int timeLeft) {
            CreateBunnyExplosion();

            //爆炸音效
            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.7f,
                Pitch = 0.2f
            }, Projectile.Center);

            SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                Volume = 0.6f,
                Pitch = 0.5f
            }, Projectile.Center);
        }

        //创建兔子爆炸：暖橙火心 + 哑光烟圈 + 绒毛纷飞 + 少量卡通星点
        private void CreateBunnyExplosion() {
            Projectile.Explode(ExplosionRadius, default, false);

            if (VaultUtils.isServer) {
                return;
            }

            Vector2 center = Projectile.Center;
            int layer = HalibutData.GetDomainLayer();

            //两帧白闪过曝爆点，随即塌向暖橙
            PRTLoader.NewParticle<PRT_FishBunnyStar>(center, Vector2.Zero
                , new Color(255, 244, 230), 1.05f)?.Configure(8, true);

            //卡通星点：绕爆心弹出少量四芒星
            for (int i = 0; i < 4; i++) {
                float ang = MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(0.7f);
                PRTLoader.NewParticle<PRT_FishBunnyStar>(center + ang.ToRotationVector2() * 10f
                    , ang.ToRotationVector2() * Main.rand.NextFloat(2f, 4.5f)
                    , Color.Lerp(FishBunnyPalette.EmberHot, FishBunnyPalette.HeartFlush, Main.rand.NextFloat())
                    , Main.rand.NextFloat(0.34f, 0.55f))?.Configure(Main.rand.Next(16, 26));
            }

            //暖橙火心：拉丝余烬径向迸出，微重力下坠
            int emberCount = Math.Min(10 + layer, 12);
            for (int i = 0; i < emberCount; i++) {
                Vector2 vel = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(2.5f, 7.5f);
                vel.Y -= Main.rand.NextFloat(0f, 1.5f);
                PRTLoader.NewParticle<PRT_PallbearerEmber>(center + Main.rand.NextVector2Circular(6f, 6f), vel
                    , Main.rand.NextBool(3) ? FishBunnyPalette.EmberDeep : FishBunnyPalette.EmberHot
                    , Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(18, 30));
            }

            //哑光烟圈：环形外涌，压住加色亮部
            for (int i = 0; i < 8; i++) {
                float ang = MathHelper.TwoPi * i / 8f + Main.rand.NextFloat(0.35f);
                Vector2 dir = ang.ToRotationVector2();
                PRTLoader.NewParticle<PRT_FishBunnySmoke>(center + dir * 22f
                    , dir * Main.rand.NextFloat(2.6f, 4.2f) + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-0.8f, 0.8f)
                    , Color.White, Main.rand.NextFloat(0.07f, 0.11f))
                    ?.Configure(Main.rand.Next(28, 42), new Color(216, 188, 172), new Color(122, 114, 112), 1.012f, 0.004f);
            }
            //中心两团上浮浓烟收尾
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishBunnySmoke>(center + Main.rand.NextVector2Circular(8f, 8f)
                    , new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.8f, 1.6f))
                    , Color.White, Main.rand.NextFloat(0.1f, 0.14f))
                    ?.Configure(Main.rand.Next(36, 50), new Color(198, 176, 164), new Color(112, 106, 104), 1.010f, 0.012f);
            }

            //绒毛纷飞：玩偶填充物炸开，比弹体活得久
            int fluffCount = Math.Min(12 + layer, 13);
            for (int i = 0; i < fluffCount; i++) {
                Vector2 vel = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(1.5f, 5.5f);
                vel.Y -= Main.rand.NextFloat(0f, 2.5f);
                PRTLoader.NewParticle<PRT_FishBunnyFluff>(center + Main.rand.NextVector2Circular(10f, 10f), vel
                    , FishBunnyPalette.Fluff(), Main.rand.NextFloat(0.028f, 0.05f))
                    ?.Configure(Main.rand.Next(50, 80));
            }

            //尘底噪
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(center, DustID.Smoke
                    , Main.rand.NextVector2Circular(9f, 9f), 130
                    , new Color(212, 190, 180), Main.rand.NextFloat(1.2f, 2f));
                d.noGravity = Main.rand.NextBool();
            }

            //暖橙光斑一闪
            Lighting.AddLight(center, 1.4f, 0.9f, 0.5f);

            //克制的落点小震：多只兔子可能同帧炸，单发幅度压低
            if (CWRServerConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(center
                    , Main.rand.NextVector2Unit(), 2f, 5f, 6, 620f, FullName));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D bunnyTex = TextureAssets.Item[ItemID.Bunnyfish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //计算翻转方向
            SpriteEffects effects = SpriteEffects.None;
            if (Projectile.velocity.X < 0) {
                effects = SpriteEffects.FlipHorizontally;
            }

            //形变缩放：预热期心跳给一点整体膨缩，爆炸期由 squashStretch 自己搏动
            float beatSwell = State == BunnyState.Exploding ? 1f : 1f + beatEnvelope * 0.05f;
            Vector2 scale = new Vector2(Projectile.scale / squashStretch, Projectile.scale * squashStretch) * beatSwell;

            //基础颜色
            Color drawColor = Projectile.GetAlpha(lightColor);

            //追击警觉潮红，心跳按包络瞬时泛粉：预热浅、爆炸期深
            if (State == BunnyState.Chasing) {
                drawColor = Color.Lerp(drawColor, new Color(255, 190, 195), 0.25f);
            }
            float flush = State == BunnyState.Exploding ? 0.75f : 0.3f;
            drawColor = Color.Lerp(drawColor, FishBunnyPalette.HeartFlush, beatEnvelope * flush);

            //心跳底光：夹在残影与本体之下的暖粉泛光，仅在拍点亮起
            if (State == BunnyState.Exploding && beatEnvelope > 0.05f && CWRAsset.SoftGlow?.Value is Texture2D glowTex) {
                Color glowCol = FishBunnyPalette.HeartFlush with { A = 0 } * (beatEnvelope * 0.45f);
                float glowScale = 0.95f + beatEnvelope * 0.45f;
                sb.Draw(glowTex, drawPos, null, glowCol, 0f, glowTex.Size() / 2f, glowScale, SpriteEffects.None, 0);
            }

            //空中快速移动时的幽灵残影链，读出整条跳弧
            if (State == BunnyState.Airborne && Projectile.velocity.LengthSquared() > 30f) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    float fade = 0.26f * (1f - i / (float)Projectile.oldPos.Length);
                    Color ghostColor = drawColor * fade;
                    sb.Draw(bunnyTex, ghostPos, null, ghostColor, Projectile.oldRot[i]
                        , bunnyTex.Size() / 2f, scale * (1f - i * 0.04f), effects, 0);
                }
            }

            //绘制阴影层
            for (int i = 0; i < 3; i++) {
                float shadowOffset = (3 - i) * 2f;
                Vector2 shadowPos = drawPos + new Vector2(0, shadowOffset);
                Color shadowColor = new Color(0, 0, 0, 80) * (1f - i * 0.3f);

                sb.Draw(
                    bunnyTex,
                    shadowPos,
                    null,
                    shadowColor,
                    bodyRotation,
                    bunnyTex.Size() / 2f,
                    scale * 0.95f,
                    effects,
                    0
                );
            }

            //绘制主体
            sb.Draw(
                bunnyTex,
                drawPos,
                null,
                drawColor,
                bodyRotation,
                bunnyTex.Size() / 2f,
                scale,
                effects,
                0
            );

            return false;
        }
    }
}
