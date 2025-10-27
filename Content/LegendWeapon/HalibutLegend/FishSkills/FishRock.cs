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
        private Vector2 targetPos;
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

        //各阶段持续时间
        private const int AppearDuration = 30;
        private const int FlyDuration = 45;
        private const int PrepareDuration = 20;
        private const int StrikeDuration = 15;
        private const int ReturnDuration = 40;
        private const int DisappearDuration = 25;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 15;
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
            hammerRotation = MathHelper.Lerp(hammerRotation, targetRotation, 0.2f);

            //震动衰减
            impactShake *= 0.9f;

            //照明（暖色调的岩石光）
            float lightIntensity = glowIntensity * 0.6f;
            Lighting.AddLight(Projectile.Center,
                0.9f * lightIntensity,
                0.7f * lightIntensity,
                0.4f * lightIntensity);
        }

        //出现阶段，从上方升起并展示
        private void AppearingPhaseAI() {
            float progress = StateTimer / AppearDuration;
            float easeProgress = CWRUtils.EaseOutElastic(progress);

            //初始化位置
            if (StateTimer == 1) {
                startPos = Projectile.Center;
            }

            //弹性上升动画
            Projectile.Center = startPos + new Vector2(0, -30 * easeProgress);

            //旋转展示
            targetRotation = MathHelper.TwoPi * 2f * CWRUtils.EaseOutCubic(progress);

            //缩放渐入
            scaleMultiplier = easeProgress;
            glowIntensity = MathHelper.Lerp(0f, 1f, progress);

            //出现粒子
            if (Main.rand.NextBool(3)) {
                SpawnAppearParticle();
            }

            //转入飞行阶段
            if (StateTimer >= AppearDuration) {
                State = HammerState.Flying;
                StateTimer = 0;
                InitializeFlight();

                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                    Volume = 0.6f,
                    Pitch = -0.2f
                }, Projectile.Center);
            }
        }

        //初始化飞行路径
        private void InitializeFlight() {
            //检查目标
            if (!IsTargetValid()) {
                State = HammerState.Returning;
                StateTimer = 0;
                return;
            }

            NPC target = Main.npc[(int)TargetNPCID];

            //设置三次贝塞尔曲线的四个控制点
            bezierP0 = Projectile.Center; //起点
            bezierP3 = target.Center + new Vector2(0, -150); //终点（目标上方）

            //计算中间控制点 - 创造优雅的弧线
            Vector2 toTarget = bezierP3 - bezierP0;
            float distance = toTarget.Length();

            //控制点1 - 向上偏移
            bezierP1 = bezierP0 + new Vector2(toTarget.X * 0.3f, -distance * 0.4f);

            //控制点2 - 侧向弧线
            float arcDirection = Math.Sign(toTarget.X) * -1; //反向弧线
            bezierP2 = bezierP3 + new Vector2(arcDirection * distance * 0.3f, -distance * 0.2f);
        }

        //飞行阶段，沿贝塞尔曲线飞行
        private void FlyingPhaseAI() {
            float progress = StateTimer / FlyDuration;
            float easeProgress = CWRUtils.EaseInOutCubic(progress);

            //沿三次贝塞尔曲线移动
            Vector2 newPos = CWRUtils.CubicBezier(easeProgress, bezierP0, bezierP1, bezierP2, bezierP3);

            //计算朝向 - 沿运动方向
            Vector2 velocity = newPos - Projectile.Center;
            if (velocity.LengthSquared() > 0.1f) {
                targetRotation = velocity.ToRotation() + MathHelper.PiOver2;
            }

            Projectile.Center = newPos;

            //加速旋转效果
            rotationSpeed = MathHelper.Lerp(0.05f, 0.3f, easeProgress);
            targetRotation += rotationSpeed;

            glowIntensity = 1f;
            scaleMultiplier = 1f + (float)Math.Sin(progress * MathHelper.Pi) * 0.15f;

            //飞行拖尾
            if (Main.rand.NextBool(4)) {
                SpawnFlyingTrail();
            }

            //音效
            if (StateTimer % 15 == 0) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.3f,
                    Pitch = 0.2f + progress * 0.3f
                }, Projectile.Center);
            }

            //转入准备阶段
            if (StateTimer >= FlyDuration) {
                State = HammerState.Preparing;
                StateTimer = 0;
                strikeStartPos = Projectile.Center;
            }
        }

        //准备敲击阶段，蓄力动作
        private void PreparingPhaseAI() {
            if (!IsTargetValid()) {
                State = HammerState.Returning;
                StateTimer = 0;
                return;
            }

            float progress = StateTimer / PrepareDuration;
            NPC target = Main.npc[(int)TargetNPCID];

            //向后拉动蓄力
            float pullBack = (float)Math.Sin(progress * MathHelper.Pi) * 50f;
            Vector2 pullDirection = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
            Projectile.Center = strikeStartPos - pullDirection * pullBack;

            //蓄力旋转
            targetRotation += 0.4f;

            //缩放蓄力效果
            scaleMultiplier = 1f + progress * 0.5f;
            glowIntensity = 1f + progress * 0.5f;

            //蓄力粒子
            if (Main.rand.NextBool(2)) {
                SpawnChargeParticle();
            }

            //蓄力音效
            if (StateTimer % 6 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                    Volume = 0.3f * progress,
                    Pitch = -0.3f + progress * 0.6f
                }, Projectile.Center);
            }

            //转入敲击阶段
            if (StateTimer >= PrepareDuration) {
                State = HammerState.Striking;
                StateTimer = 0;
                strikeEndPos = target.Center;

                //重击音效
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 1f,
                    Pitch = -0.5f
                }, Projectile.Center);
            }
        }

        //敲击阶段，快速重击
        private void StrikingPhaseAI() {
            float progress = StateTimer / StrikeDuration;
            float easeProgress = CWRUtils.EaseInCubic(progress);

            //快速冲向目标
            Projectile.Center = Vector2.Lerp(strikeStartPos, strikeEndPos, easeProgress);

            //快速旋转
            targetRotation += 1.2f;

            //冲击视觉
            scaleMultiplier = 1.5f - progress * 0.3f;
            glowIntensity = 2f;

            //冲击拖尾
            if (Main.rand.NextBool(2)) {
                SpawnStrikeTrail();
            }

            //击中检测
            if (IsTargetValid() && StateTimer == StrikeDuration / 2) {
                NPC target = Main.npc[(int)TargetNPCID];
                float distance = Vector2.Distance(Projectile.Center, target.Center);

                if (distance < 100f) {
                    //造成伤害
                    target.SimpleStrikeNPC(Projectile.damage, 0, false, Projectile.knockBack, null, false, 0f, true);

                    //创建冲击效果
                    CreateImpactEffect(target.Center);
                    impactShake = 20f;

                    //重击音效
                    SoundEngine.PlaySound(SoundID.Item70 with {
                        Volume = 1f,
                        Pitch = -0.4f
                    }, target.Center);

                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with {
                        Volume = 0.9f
                    }, target.Center);
                }
            }

            //转入返回阶段
            if (StateTimer >= StrikeDuration) {
                State = HammerState.Returning;
                StateTimer = 0;
            }
        }

        //返回阶段，飞回玩家
        private void ReturningPhaseAI() {
            float progress = StateTimer / ReturnDuration;
            float easeProgress = CWRUtils.EaseInOutQuad(progress);

            //计算返回路径
            Vector2 returnTarget = Owner.Center + new Vector2(0, -120);
            Projectile.Center = Vector2.Lerp(Projectile.Center, returnTarget, easeProgress * 0.1f);

            //减速旋转
            targetRotation += MathHelper.Lerp(0.8f, 0.1f, progress);

            //缩小
            scaleMultiplier = MathHelper.Lerp(1.2f, 0.8f, progress);
            glowIntensity = MathHelper.Lerp(1.5f, 0.6f, progress);

            //返回拖尾
            if (Main.rand.NextBool(6)) {
                SpawnReturnTrail();
            }

            //转入消失阶段
            if (StateTimer >= ReturnDuration || Vector2.Distance(Projectile.Center, returnTarget) < 30f) {
                State = HammerState.Disappearing;
                StateTimer = 0;
            }
        }

        //消失阶段，淡出消失
        private void DisappearingPhaseAI() {
            float progress = StateTimer / DisappearDuration;
            float easeProgress = CWRUtils.EaseInCubic(progress);

            //减速旋转
            targetRotation += 0.2f * (1f - progress);

            //淡出
            Projectile.alpha = (int)(255 * easeProgress);
            scaleMultiplier = 1f - easeProgress * 0.5f;
            glowIntensity *= 0.9f;

            //消失粒子
            if (Main.rand.NextBool(3)) {
                SpawnDisappearParticle();
            }

            //完全消失
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

        //各种粒子效果
        private void SpawnAppearParticle() {
            Dust appear = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                DustID.Stone,
                Main.rand.NextVector2Circular(3f, 3f),
                100,
                default,
                Main.rand.NextFloat(1.5f, 2.5f)
            );
            appear.noGravity = true;
            appear.fadeIn = 1.2f;
        }

        private void SpawnFlyingTrail() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.Smoke,
                -Projectile.velocity * 0.3f,
                100,
                new Color(120, 100, 80),
                Main.rand.NextFloat(1.5f, 2.2f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1.1f;
        }

        private void SpawnChargeParticle() {
            Vector2 particlePos = Projectile.Center + Main.rand.NextVector2Circular(40f, 40f);
            Vector2 toCenter = (Projectile.Center - particlePos).SafeNormalize(Vector2.Zero);

            Dust charge = Dust.NewDustPerfect(
                particlePos,
                DustID.Stone,
                toCenter * Main.rand.NextFloat(3f, 6f),
                100,
                default,
                Main.rand.NextFloat(1.8f, 2.8f)
            );
            charge.noGravity = true;
            charge.fadeIn = 1.3f;
        }

        private void SpawnStrikeTrail() {
            Dust strike = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                DustID.Stone,
                -Projectile.velocity * Main.rand.NextFloat(0.2f, 0.5f),
                100,
                default,
                Main.rand.NextFloat(2f, 3.5f)
            );
            strike.noGravity = true;
            strike.fadeIn = 1.4f;
        }

        private void SpawnReturnTrail() {
            Dust returnDust = Dust.NewDustPerfect(
                Projectile.Center,
                DustID.Smoke,
                Main.rand.NextVector2Circular(2f, 2f),
                100,
                new Color(100, 80, 60),
                Main.rand.NextFloat(1.2f, 2f)
            );
            returnDust.noGravity = true;
            returnDust.fadeIn = 1f;
            returnDust.alpha = 120;
        }

        private void SpawnDisappearParticle() {
            Dust disappear = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                DustID.Stone,
                Main.rand.NextVector2Circular(4f, 4f),
                100,
                default,
                Main.rand.NextFloat(1.5f, 2.5f)
            );
            disappear.noGravity = true;
            disappear.fadeIn = 1.2f;
        }

        //创建冲击效果
        private static void CreateImpactEffect(Vector2 position) {
            //冲击波环
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 15f);

                Dust impact = Dust.NewDustPerfect(
                    position,
                    DustID.Stone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(2.5f, 4f)
                );
                impact.noGravity = true;
                impact.fadeIn = 1.5f;
            }

            //烟雾爆发
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(10f, 10f);
                Dust smoke = Dust.NewDustPerfect(
                    position,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(100, 80, 60),
                    Main.rand.NextFloat(3f, 4.5f)
                );
                smoke.noGravity = true;
            }

            //碎石飞溅
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(12f, 12f);
                velocity.Y -= Main.rand.NextFloat(5f, 10f);

                Dust debris = Dust.NewDustPerfect(
                    position,
                    DustID.Stone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(2f, 3.5f)
                );
                debris.noGravity = false;
            }
        }

        //最终消失效果
        private void SpawnFinalEffect() {
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust final = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Stone,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.8f, 2.8f)
                );
                final.noGravity = true;
                final.fadeIn = 1.3f;
            }

            SoundEngine.PlaySound(SoundID.Item10 with {
                Volume = 0.5f,
                Pitch = -0.2f
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D hammerTex = TextureAssets.Item[ItemID.Rockfish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //应用震动偏移
            if (impactShake > 0) {
                drawPos += Main.rand.NextVector2Circular(impactShake, impactShake);
            }

            Rectangle sourceRect = hammerTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;
            Color baseColor = lightColor;
            float alpha = (255f - Projectile.alpha) / 255f;

            //绘制拖尾残影
            if (State == HammerState.Flying || State == HammerState.Striking) {
                DrawHammerAfterimages(sb, hammerTex, sourceRect, origin, baseColor, alpha);
            }

            //发光层
            if (glowIntensity > 1f) {
                Color glowColor = new Color(255, 200, 100); //暖色调辉光
                float glowAlpha = (glowIntensity - 1f) * alpha * 0.5f;

                sb.Draw(
                    hammerTex,
                    drawPos,
                    sourceRect,
                    glowColor * glowAlpha,
                    hammerRotation,
                    origin,
                    Projectile.scale * scaleMultiplier * 1.2f,
                    SpriteEffects.None,
                    0
                );
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

            //额外高光（冲击时）
            if (State == HammerState.Striking) {
                Color strikeGlow = new Color(255, 240, 200, 0);
                float strikeAlpha = glowIntensity * 0.4f * alpha;

                sb.Draw(
                    hammerTex,
                    drawPos,
                    sourceRect,
                    strikeGlow * strikeAlpha,
                    hammerRotation,
                    origin,
                    Projectile.scale * scaleMultiplier * 1.1f,
                    SpriteEffects.None,
                    0
                );
            }

            return false;
        }

        private void DrawHammerAfterimages(SpriteBatch sb, Texture2D hammerTex, Rectangle sourceRect,
            Vector2 origin, Color baseColor, float alpha) {

            int afterimageCount = State == HammerState.Striking ? 15 : 10;

            for (int i = 1; i < afterimageCount; i++) {
                if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) continue;

                float afterimageProgress = 1f - i / (float)afterimageCount;
                float afterimageAlpha = afterimageProgress * alpha * 0.5f;

                Color afterimageColor = Color.Lerp(
                    new Color(80, 60, 40),
                    new Color(200, 160, 100),
                    afterimageProgress
                ) * afterimageAlpha;

                Vector2 afterimagePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float afterimageScale = Projectile.scale * scaleMultiplier * MathHelper.Lerp(0.7f, 0.95f, afterimageProgress);

                //旋转插值
                float afterimageRotation = MathHelper.Lerp(hammerRotation, hammerRotation - 0.4f, i / (float)afterimageCount);

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
