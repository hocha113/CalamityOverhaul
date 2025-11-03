using CalamityOverhaul.Common;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
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

                    //生成岩鱼锤
                    int hammerProj = Projectile.NewProjectile(
                        shootState.Source,
                        player.Center + new Vector2(0, -120), //从玩家头顶生成
                        Vector2.Zero,
                        ModContent.ProjectileType<RockHammerFish>(),
                        (int)(shootState.WeaponDamage * (3.6f + HalibutData.GetDomainLayer() * 1.2f)),
                        shootState.WeaponKnockback * 3f,
                        player.whoAmI,
                        ai0: target.whoAmI //传递目标ID
                    );

                    if (hammerProj >= 0) {
                        //生成召唤特效
                        SpawnSummonEffect(player.Center + new Vector2(0, -120));

                        //召唤音效
                        SoundEngine.PlaySound(SoundID.Item70 with {
                            Volume = 0.7f,
                            Pitch = -0.3f
                        }, player.Center);
                    }
                }
            }
            return base.UpdateCooldown(halibutPlayer, player);
        }

        private static void SpawnSummonEffect(Vector2 position) {
            //岩石碎片环
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);

                Dust rock = Dust.NewDustPerfect(
                    position,
                    DustID.Stone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.8f, 2.8f)
                );
                rock.noGravity = true;
                rock.fadeIn = 1.4f;
            }

            //烟雾云
            for (int i = 0; i < 15; i++) {
                Dust smoke = Dust.NewDustDirect(
                    position - new Vector2(20),
                    40, 40,
                    DustID.Smoke,
                    Scale: Main.rand.NextFloat(2f, 3.5f)
                );
                smoke.velocity = Main.rand.NextVector2Circular(4f, 4f);
                smoke.noGravity = true;
                smoke.color = new Color(100, 80, 60);
            }
        }
    }

    /// <summary>
    /// 岩鱼锤弹幕
    /// </summary>
    internal class RockHammerFish : BaseHeldProj
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Rockfish;

        //状态机
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

        //贝塞尔曲线控制点
        private Vector2 bezierP0;
        private Vector2 bezierP1;
        private Vector2 bezierP2;
        private Vector2 bezierP3;

        //旋转效果
        private float hammerRotation = 0f;
        private float targetRotation = 0f;
        private float rotationSpeed = 0f;

        //视觉效果
        private float glowIntensity = 0f;
        private float scaleMultiplier = 1f;
        private float impactShake = 0f;

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
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20; //增加拖尾长度
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
            if (Projectile.numHits == 0)
            //增强冲击效果
            CreateEnhancedImpactEffect(target.Center);
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!FishSkill.GetT<FishRock>().Active(Owner)) {
                State = HammerState.Disappearing;
                StateTimer = 0;
            }

            StateTimer++;

            //更新目标预判
            UpdateTargetPrediction();

            //状态机
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

            //平滑更新旋转
            hammerRotation = MathHelper.Lerp(hammerRotation, targetRotation, 0.25f);

            //震动衰减
            impactShake *= 0.85f;

            //增强照明
            float lightIntensity = glowIntensity * 0.8f;
            Lighting.AddLight(Projectile.Center,
                1.2f * lightIntensity,
                0.9f * lightIntensity,
                0.5f * lightIntensity);
        }

        //更新目标预判
        private void UpdateTargetPrediction() {
            if (!IsTargetValid()) return;

            NPC target = Main.npc[(int)TargetNPCID];
            
            //计算目标速度
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
            }

            Projectile.Center = startPos + new Vector2(0, -30 * easeProgress);
            targetRotation = MathHelper.TwoPi * 2f * CWRUtils.EaseOutCubic(progress);
            scaleMultiplier = easeProgress;
            glowIntensity = MathHelper.Lerp(0f, 1.2f, progress); //更亮

            if (Main.rand.NextBool(2)) { //更多粒子
                SpawnAppearParticle();
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

        //初始化飞行路径
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
            float easeProgress = CWRUtils.EaseInOutCubic(progress);

            Vector2 newPos = CWRUtils.CubicBezier(easeProgress, bezierP0, bezierP1, bezierP2, bezierP3);

            Vector2 velocity = newPos - Projectile.Center;
            if (velocity.LengthSquared() > 0.1f) {
                targetRotation = velocity.ToRotation() + MathHelper.PiOver2;
            }

            Projectile.Center = newPos;

            rotationSpeed = MathHelper.Lerp(0.08f, 0.4f, easeProgress);
            targetRotation += rotationSpeed;

            glowIntensity = 1.2f;
            scaleMultiplier = 1f + (float)Math.Sin(progress * MathHelper.Pi) * 0.2f;

            if (Main.rand.NextBool(2)) {
                SpawnFlyingTrail();
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

        //准备敲击阶段 - 动态追踪
        private void PreparingPhaseAI() {
            if (!IsTargetValid()) {
                State = HammerState.Returning;
                StateTimer = 0;
                return;
            }

            float progress = StateTimer / PrepareDuration;
            NPC target = Main.npc[(int)TargetNPCID];

            //使用预测位置
            Vector2 targetPoint = predictedPos != Vector2.Zero ? predictedPos : target.Center;

            //向后拉动蓄力
            float pullBack = (float)Math.Sin(progress * MathHelper.Pi) * 60f; //拉得更远
            Vector2 pullDirection = (targetPoint - Projectile.Center).SafeNormalize(Vector2.UnitY);
            Projectile.Center = strikeStartPos - pullDirection * pullBack;

            targetRotation += 0.5f; //更快旋转
            scaleMultiplier = 1f + progress * 0.7f; //更大缩放
            glowIntensity = 1.2f + progress * 0.8f; //更亮

            if (Main.rand.NextBool(1)) { //更密集的蓄力粒子
                SpawnChargeParticle();
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

        //敲击阶段
        private void StrikingPhaseAI() {
            float progress = StateTimer / StrikeDuration;
            float easeProgress = CWRUtils.EaseInCubic(progress);

            Projectile.Center = Vector2.Lerp(strikeStartPos, strikeEndPos, easeProgress);

            targetRotation += 1.5f; //更快旋转
            scaleMultiplier = 1.8f - progress * 0.4f; //更大缩放
            glowIntensity = 2.5f; //更亮

            if (Main.rand.NextBool(1)) {
                SpawnStrikeTrail();
            }

            //扩大击中检测范围和时间窗口
            if (IsTargetValid() && StateTimer >= StrikeDuration / 3 && StateTimer <= StrikeDuration * 2 / 3) {
                NPC target = Main.npc[(int)TargetNPCID];
                float distance = Vector2.Distance(Projectile.Center, target.Center);

                if (distance < HitRadius) //使用更大的命中半径
                {
                    //造成伤害
                    target.SimpleStrikeNPC(Projectile.damage, 0, false, Projectile.knockBack * 2f, null, false, 0f, true);

                    //增强冲击效果
                    CreateEnhancedImpactEffect(target.Center);
                    impactShake = 30f; //更强震动

                    //屏幕震动
                    if (Owner.whoAmI == Main.myPlayer && CWRServerConfig.Instance.ScreenVibration) {
                        Owner.GetModPlayer<CWRPlayer>().ScreenShakeValue = Math.Max(
                            Owner.GetModPlayer<CWRPlayer>().ScreenShakeValue, 12f);
                    }

                    //增强音效
                    SoundEngine.PlaySound(SoundID.Item70 with {
                        Volume = 1.3f,
                        Pitch = -0.5f
                    }, target.Center);

                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                        Volume = 1.2f,
                        Pitch = -0.2f
                    }, target.Center);

                    SoundEngine.PlaySound(SoundID.Item14 with {
                        Volume = 1f,
                        Pitch = -0.3f
                    }, target.Center);

                    //标记已命中，避免重复
                    TargetNPCID = -1;
                }
            }

            if (StateTimer >= StrikeDuration) {
                State = HammerState.Returning;
                StateTimer = 0;
            }
        }

        //返回阶段
        private void ReturningPhaseAI() {
            float progress = StateTimer / ReturnDuration;
            float easeProgress = CWRUtils.EaseInOutQuad(progress);

            Vector2 returnTarget = Owner.Center + new Vector2(0, -120);
            Projectile.Center = Vector2.Lerp(Projectile.Center, returnTarget, easeProgress * 0.12f);

            targetRotation += MathHelper.Lerp(0.8f, 0.1f, progress);
            scaleMultiplier = MathHelper.Lerp(1.3f, 0.8f, progress);
            glowIntensity = MathHelper.Lerp(1.8f, 0.6f, progress);

            if (Main.rand.NextBool(5)) {
                SpawnReturnTrail();
            }

            if (StateTimer >= ReturnDuration || Vector2.Distance(Projectile.Center, returnTarget) < 30f) {
                State = HammerState.Disappearing;
                StateTimer = 0;
            }
        }

        //消失阶段
        private void DisappearingPhaseAI() {
            float progress = StateTimer / DisappearDuration;
            float easeProgress = CWRUtils.EaseInCubic(progress);

            targetRotation += 0.2f * (1f - progress);
            Projectile.alpha = (int)(255 * easeProgress);
            scaleMultiplier = 1f - easeProgress * 0.5f;
            glowIntensity *= 0.88f;

            if (Main.rand.NextBool(2)) {
                SpawnDisappearParticle();
            }

            if (StateTimer >= DisappearDuration) {
                SpawnFinalEffect();
                Projectile.Kill();
            }
        }

        //检查目标有效性
        private bool IsTargetValid() {
            int id = (int)TargetNPCID;
            if (id < 0 || id >= Main.maxNPCs) return false;
            NPC target = Main.npc[id];
            return target.active && target.CanBeChasedBy();
        }

        //粒子效果
        private void SpawnAppearParticle() {
            Dust appear = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                DustID.Stone,
                Main.rand.NextVector2Circular(3f, 3f),
                100,
                default,
                Main.rand.NextFloat(1.8f, 3f)
            );
            appear.noGravity = true;
            appear.fadeIn = 1.3f;
        }

        private void SpawnFlyingTrail() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.Smoke,
                -Projectile.velocity * 0.3f,
                100,
                new Color(120, 100, 80),
                Main.rand.NextFloat(1.8f, 2.5f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1.2f;
        }

        private void SpawnChargeParticle() {
            Vector2 particlePos = Projectile.Center + Main.rand.NextVector2Circular(50f, 50f);
            Vector2 toCenter = (Projectile.Center - particlePos).SafeNormalize(Vector2.Zero);

            Dust charge = Dust.NewDustPerfect(
                particlePos,
                DustID.Stone,
                toCenter * Main.rand.NextFloat(4f, 8f),
                100,
                default,
                Main.rand.NextFloat(2f, 3.5f)
            );
            charge.noGravity = true;
            charge.fadeIn = 1.4f;
        }

        private void SpawnStrikeTrail() {
            Dust strike = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                DustID.Stone,
                -Projectile.velocity * Main.rand.NextFloat(0.3f, 0.7f),
                100,
                default,
                Main.rand.NextFloat(2.5f, 4f)
            );
            strike.noGravity = true;
            strike.fadeIn = 1.5f;
        }

        private void SpawnReturnTrail() {
            Dust returnDust = Dust.NewDustPerfect(
                Projectile.Center,
                DustID.Smoke,
                Main.rand.NextVector2Circular(2f, 2f),
                100,
                new Color(100, 80, 60),
                Main.rand.NextFloat(1.5f, 2.5f)
            );
            returnDust.noGravity = true;
            returnDust.fadeIn = 1.1f;
            returnDust.alpha = 100;
        }

        private void SpawnDisappearParticle() {
            Dust disappear = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                DustID.Stone,
                Main.rand.NextVector2Circular(4f, 4f),
                100,
                default,
                Main.rand.NextFloat(1.8f, 3f)
            );
            disappear.noGravity = true;
            disappear.fadeIn = 1.3f;
        }

        //增强的冲击效果
        private static void CreateEnhancedImpactEffect(Vector2 position) {
            //冲击波环
            for (int i = 0; i < 40; i++) {
                float angle = MathHelper.TwoPi * i / 40f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 20f);

                Dust impact = Dust.NewDustPerfect(
                    position,
                    DustID.Stone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(3f, 5f)
                );
                impact.noGravity = true;
                impact.fadeIn = 1.6f;
            }

            //更多烟雾爆发
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(12f, 12f);
                Dust smoke = Dust.NewDustPerfect(
                    position,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(100, 80, 60),
                    Main.rand.NextFloat(3.5f, 5.5f)
                );
                smoke.noGravity = true;
            }

            //更多碎石飞溅
            for (int i = 0; i < 25; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(15f, 15f);
                velocity.Y -= Main.rand.NextFloat(6f, 12f);

                Dust debris = Dust.NewDustPerfect(
                    position,
                    DustID.Stone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(2.5f, 4f)
                );
                debris.noGravity = false;
            }

            //冲击波圈
            for (int i = 0; i < 3; i++) {
                int radius = 50 + i * 30;
                for (int j = 0; j < 20; j++) {
                    float angle = MathHelper.TwoPi * j / 20f;
                    Vector2 offset = angle.ToRotationVector2() * radius;
                    
                    Dust ring = Dust.NewDustPerfect(
                        position + offset,
                        DustID.Torch,
                        angle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f),
                        100,
                        new Color(255, 200, 100),
                        Main.rand.NextFloat(2f, 3f)
                    );
                    ring.noGravity = true;
                }
            }
        }

        private void SpawnFinalEffect() {
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(8f, 8f);
                Dust final = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Stone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                final.noGravity = true;
                final.fadeIn = 1.4f;
            }

            SoundEngine.PlaySound(SoundID.Item10 with {
                Volume = 0.6f,
                Pitch = -0.2f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D hammerTex = TextureAssets.Item[ItemID.Rockfish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //震动效果
            if (impactShake > 0) {
                drawPos += Main.rand.NextVector2Circular(impactShake, impactShake);
            }

            Rectangle sourceRect = hammerTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;
            Color baseColor = lightColor;
            float alpha = (255f - Projectile.alpha) / 255f;

            //拖尾残影
            if (State == HammerState.Flying || State == HammerState.Striking) {
                DrawHammerAfterimages(sb, hammerTex, sourceRect, origin, baseColor, alpha);
            }

            //增强发光层
            if (glowIntensity > 1f) {
                Color glowColor = new Color(255, 220, 120);
                float glowAlpha = (glowIntensity - 1f) * alpha * 0.7f;

                for (int i = 0; i < 3; i++) {
                    sb.Draw(
                        hammerTex,
                        drawPos + Main.rand.NextVector2Circular(2, 2),
                        sourceRect,
                        glowColor * glowAlpha * 0.5f,
                        hammerRotation,
                        origin,
                        Projectile.scale * scaleMultiplier * (1.2f + i * 0.1f),
                        SpriteEffects.None,
                        0
                    );
                }
            }

            //主体绘制
            sb.Draw(
                hammerTex,
                drawPos,
                sourceRect,
                baseColor * alpha,
                hammerRotation,
                origin,
                Projectile.scale * scaleMultiplier,
                SpriteEffects.None,
                0
            );

            //敲击高光
            if (State == HammerState.Striking) {
                Color strikeGlow = new Color(255, 255, 200, 0);
                float strikeAlpha = glowIntensity * 0.6f * alpha;

                sb.Draw(
                    hammerTex,
                    drawPos,
                    sourceRect,
                    strikeGlow * strikeAlpha,
                    hammerRotation,
                    origin,
                    Projectile.scale * scaleMultiplier * 1.15f,
                    SpriteEffects.None,
                    0
                );
            }

            return false;
        }

        private void DrawHammerAfterimages(SpriteBatch sb, Texture2D hammerTex, Rectangle sourceRect,
            Vector2 origin, Color baseColor, float alpha) {

            int afterimageCount = State == HammerState.Striking ? 20 : 15;

            for (int i = 1; i < afterimageCount; i++) {
                if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) continue;

                float afterimageProgress = 1f - i / (float)afterimageCount;
                float afterimageAlpha = afterimageProgress * alpha * 0.6f;

                Color afterimageColor = Color.Lerp(
                    new Color(80, 60, 40),
                    new Color(255, 200, 100),
                    afterimageProgress
                ) * afterimageAlpha;

                Vector2 afterimagePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float afterimageScale = Projectile.scale * scaleMultiplier * MathHelper.Lerp(0.6f, 0.95f, afterimageProgress);

                float afterimageRotation = MathHelper.Lerp(hammerRotation, hammerRotation - 0.5f, i / (float)afterimageCount);

                sb.Draw(
                    hammerTex,
                    afterimagePos,
                    sourceRect,
                    afterimageColor,
                    afterimageRotation,
                    origin,
                    afterimageScale,
                    SpriteEffects.None,
                    0
                );
            }
        }

        public override bool? CanDamage() => State == HammerState.Striking;
    }
}
