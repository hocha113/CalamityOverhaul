using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 兔子鱼技能-丢出跳跃爆炸兔子鱼
    /// </summary>
    internal class FishBunny : FishSkill
    {
        public override int UnlockFishID => ItemID.Bunnyfish;
        public override int DefaultCooldown => 240 - HalibutData.GetDomainLayer() * 15;

        public override bool? AltFunctionUse(Item item, Player player) => true;

        public override bool? CanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2 && Cooldown <= 0) {
                item.UseSound = null;
                Vector2 velocity = player.To(Main.MouseWorld).UnitVector() * 12f;
                Vector2 position = player.Center;
                ShootState shootState = player.GetShootState();
                var source = shootState.Source;
                int damage = shootState.WeaponDamage * 2;
                float knockback = shootState.WeaponKnockback;

                SetCooldown();

                //丢出兔子鱼的数量随领域层数增加
                int bunnyCount = 3 + HalibutData.GetDomainLayer();

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
                        (int)(damage * (2.5f + HalibutData.GetDomainLayer() * 0.7f)),
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
            }
            return null;
        }

        //抛掷特效
        private static void SpawnThrowEffect(Vector2 position, Vector2 direction) {
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = direction.SafeNormalize(Vector2.Zero).RotatedByRandom(0.5f) * Main.rand.NextFloat(3f, 8f);
                Dust dust = Dust.NewDustPerfect(
                    position,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(180, 180, 200),
                    Main.rand.NextFloat(1f, 1.8f)
                );
                dust.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 兔子鱼跳跃弹幕-模拟兔子行为
    /// </summary>
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
        private int chaseFailCount = 0;

        //生物动画
        private float squashStretch = 1f;
        private float bodyRotation = 0f;
        private int idleAnimTimer = 0;

        //爆炸参数
        private const int MaxLifeTime = 600;
        private const int ExplosionRadius = 140;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = MaxLifeTime;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
        }

        public override void AI() {
            BunnyLife++;

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

            //兔子粉色照明
            float lightIntensity = 0.6f;
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

            //空中粒子
            if (BunnyLife % 5 == 0) {
                SpawnAirborneParticle();
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
            NPC target = FindNearestEnemy();

            if (target != null) {
                TargetNPCID = target.whoAmI;
                State = BunnyState.Chasing;
                groundTime = 0;
                return;
            }

            //随机跳跃
            int jumpTime = Main.rand.Next(MinGroundTime, MaxGroundTime);
            if (groundTime >= jumpTime) {
                PerformJump(false);
                groundTime = 0;

                //兔子跳跃音
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.3f,
                    Pitch = 0.5f
                }, Projectile.Center);
            }

            //地面待机动画
            if (BunnyLife % 15 == 0) {
                SpawnIdleParticle();
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
                chaseFailCount++;
                groundTime = 0;
                return;
            }

            NPC target = Main.npc[(int)TargetNPCID];

            //目标距离判断
            float distanceToTarget = Vector2.Distance(Projectile.Center, target.Center);

            if (distanceToTarget > ChaseRange) {
                //目标太远,返回普通状态
                State = BunnyState.OnGround;
                groundTime = 0;
                return;
            }

            //计算跳跃方向
            Vector2 toTarget = target.Center - Projectile.Center;
            float targetAngle = toTarget.ToRotation();

            //朝向目标跳跃
            int chaseJumpTime = Main.rand.Next(5, 15);
            if (groundTime >= chaseJumpTime) {
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

                //追击跳音
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.4f,
                    Pitch = 0.7f
                }, Projectile.Center);

                //跳跃粒子
                SpawnJumpParticle();
            }
        }

        //爆炸状态
        private void ExplodingPhaseAI() {
            //停止移动
            Projectile.velocity *= 0.85f;

            //震动效果
            squashStretch = 1f + (float)Math.Sin(BunnyLife * 0.8f) * 0.3f;

            //密集粒子预警
            if (Projectile.timeLeft % 3 == 0) {
                SpawnExplosionWarning();
            }
        }

        //执行跳跃
        private void PerformJump(bool isChase) {
            float jumpPower = isChase ? ChaseJumpForce : JumpForce;

            //随机水平速度
            float horizontalSpeed = Main.rand.NextFloat(3f, 7f) * (Main.rand.NextBool() ? 1 : -1);

            Projectile.velocity.X = horizontalSpeed;
            Projectile.velocity.Y = -jumpPower;

            State = BunnyState.Airborne;

            //跳跃粒子
            SpawnJumpParticle();
        }

        //寻找最近的敌人
        private NPC FindNearestEnemy() {
            NPC closest = null;
            float closestDist = DetectionRange;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
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
            else if (State == BunnyState.OnGround) {
                //着地瞬间压扁
                if (groundTime < 5) {
                    squashStretch = MathHelper.Lerp(squashStretch, 0.7f, 0.3f);
                }
                else {
                    //呼吸效果
                    float breathe = (float)Math.Sin(idleAnimTimer * 0.1f) * 0.05f;
                    squashStretch = MathHelper.Lerp(squashStretch, 1f + breathe, 0.1f);
                }
            }
            //追击时紧张
            else if (State == BunnyState.Chasing) {
                float tension = (float)Math.Sin(idleAnimTimer * 0.15f) * 0.08f;
                squashStretch = MathHelper.Lerp(squashStretch, 1f + tension, 0.15f);
            }
        }

        //空中粒子
        private void SpawnAirborneParticle() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.Smoke,
                -Projectile.velocity * 0.2f,
                100,
                new Color(200, 200, 220),
                Main.rand.NextFloat(0.8f, 1.2f)
            );
            trail.noGravity = true;
        }

        //待机粒子
        private void SpawnIdleParticle() {
            Dust idle = Dust.NewDustPerfect(
                Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f), 10f),
                DustID.Smoke,
                new Vector2(0, -0.5f),
                100,
                new Color(220, 220, 240),
                Main.rand.NextFloat(0.6f, 1f)
            );
            idle.noGravity = true;
            idle.fadeIn = 0.8f;
        }

        //跳跃粒子
        private void SpawnJumpParticle() {
            for (int i = 0; i < 8; i++) {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(1f, 4f));
                Dust jump = Dust.NewDustPerfect(
                    Projectile.Bottom,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(180, 180, 200),
                    Main.rand.NextFloat(1f, 1.8f)
                );
                jump.noGravity = false;
            }
        }

        //爆炸预警粒子
        private void SpawnExplosionWarning() {
            for (int i = 0; i < 3; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f);
                Dust warning = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                    DustID.Torch,
                    velocity,
                    100,
                    new Color(255, 150, 150),
                    Main.rand.NextFloat(1.2f, 2f)
                );
                warning.noGravity = true;
                warning.fadeIn = 1.3f;
            }
        }

        //地面碰撞
        public override bool OnTileCollide(Vector2 oldVelocity) {
            //着地判断
            if (State == BunnyState.Airborne && Projectile.velocity.Y == 0) {
                State = BunnyState.OnGround;
                groundTime = 0;

                //着地音效
                SoundEngine.PlaySound(SoundID.Dig with {
                    Volume = 0.3f,
                    Pitch = 0.5f
                }, Projectile.Center);

                //着地粒子
                for (int i = 0; i < 5; i++) {
                    Dust land = Dust.NewDustDirect(
                        Projectile.Bottom - new Vector2(0, 5),
                        Projectile.width,
                        5,
                        DustID.Smoke,
                        Scale: Main.rand.NextFloat(1f, 1.5f)
                    );
                    land.velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1f, 1f));
                }

                return false;
            }

            //墙壁碰撞反弹
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.6f;
            }

            return false;
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

        //创建兔子爆炸
        private void CreateBunnyExplosion() {
            //范围伤害
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < ExplosionRadius) {
                        float damageRatio = 1f - dist / ExplosionRadius;
                        int explosionDamage = (int)(Projectile.damage * (0.6f + damageRatio * 0.4f));

                        npc.SimpleStrikeNPC(explosionDamage, 0, false, 8f, null, false, 0f, true);
                    }
                }
            }

            //爆炸粒子
            int particleCount = 45 + HalibutData.GetDomainLayer() * 5;
            for (int i = 0; i < particleCount; i++) {
                float angle = MathHelper.TwoPi * i / particleCount;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 20f);

                Dust explosion = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Torch,
                    velocity,
                    100,
                    new Color(255, 180, 200),
                    Main.rand.NextFloat(2f, 3.5f)
                );
                explosion.noGravity = Main.rand.NextBool();
                explosion.fadeIn = 1.4f;
            }

            //烟雾环
            for (int i = 0; i < 3; i++) {
                for (int j = 0; j < 20; j++) {
                    float angle = MathHelper.TwoPi * j / 20f;
                    float radius = 25f + i * 30f;
                    Vector2 spawnPos = Projectile.Center + angle.ToRotationVector2() * radius;

                    Dust smoke = Dust.NewDustPerfect(
                        spawnPos,
                        DustID.Smoke,
                        Vector2.Zero,
                        100,
                        new Color(150, 150, 150),
                        Main.rand.NextFloat(2f, 3.5f)
                    );
                    smoke.velocity = angle.ToRotationVector2() * 6f;
                    smoke.noGravity = true;
                }
            }

            //肉块碎片
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(15f, 15f);
                Dust gore = Dust.NewDustDirect(
                    Projectile.Center,
                    4, 4,
                    DustID.Blood,
                    0, 0, 100,
                    new Color(255, 150, 180),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                gore.velocity = velocity;
                gore.noGravity = false;
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

            //形变缩放
            Vector2 scale = new Vector2(Projectile.scale / squashStretch, Projectile.scale * squashStretch);

            //基础颜色
            Color drawColor = Projectile.GetAlpha(lightColor);

            //爆炸前闪烁
            if (State == BunnyState.Exploding) {
                float flash = (float)Math.Sin(BunnyLife * 1.5f) * 0.5f + 0.5f;
                drawColor = Color.Lerp(drawColor, new Color(255, 100, 100), flash * 0.7f);
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

            //追击时眼睛发光
            if (State == BunnyState.Chasing || State == BunnyState.Exploding) {
                Color glowColor = State == BunnyState.Exploding
                    ? new Color(255, 50, 50) * 0.8f
                    : new Color(255, 200, 200) * 0.6f;

                sb.Draw(
                    bunnyTex,
                    drawPos,
                    null,
                    glowColor,
                    bodyRotation,
                    bunnyTex.Size() / 2f,
                    scale * 1.05f,
                    effects,
                    0
                );
            }

            return false;
        }
    }
}
