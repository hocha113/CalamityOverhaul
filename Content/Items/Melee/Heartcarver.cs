using CalamityMod;
using CalamityMod.Particles;
using CalamityOverhaul.Content.MeleeModify.Core;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 刻心者
    /// </summary>
    internal class Heartcarver : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "Heartcarver";

        public override void SetDefaults() {
            Item.width = 52;
            Item.height = 52;
            Item.damage = 145;
            Item.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Item.useAnimation = 14;
            Item.useTime = 14;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Rapier;
            Item.knockBack = 5.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<HeartcarverHeld>();
            Item.shootSpeed = 2.4f;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.buyPrice(0, 30, 0, 0);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position
            , Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
            return false;
        }
    }

    /// <summary>
    /// 刻心者短剑的手持弹幕
    /// </summary>
    internal class HeartcarverHeld : BaseKnife
    {
        public override string Texture => CWRConstant.Item_Melee + "Heartcarver";
        public override int TargetID => ModContent.ItemType<Heartcarver>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Heartcarver_Bar";
        private int stabCounter = 0;//刺击计数器

        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            canDrawSlashTrail = false;
            SwingData.baseSwingSpeed = 5.5f;
            Length = 45;
            unitOffsetDrawZkMode = -8;
            drawTrailTopWidth = 30;
            drawTrailBtommWidth = 10;
        }

        public override bool PreSwingAI() {
            //短剑刺击行为
            StabBehavior(
                initialLength: 35,
                lifetime: maxSwingTime,
                scaleFactorDenominator: 420f,
                minLength: 35,
                maxLength: 65,
                canDrawSlashTrail: true
            );
            return false;
        }

        public override void Shoot() {
            //每次刺击都会生成环绕匕首
            int daggerType = ModContent.ProjectileType<HeartcarverDagger>();
            //限制最多3把匕首
            if (Owner.ownedProjectileCounts[daggerType] < 3) {
                Projectile.NewProjectile(
                        Source,
                        ShootSpanPos,
                        Vector2.Zero,
                    daggerType,
                (int)(Projectile.damage * 0.85f),
         Projectile.knockBack * 0.5f,
               Owner.whoAmI,
                ai0: stabCounter //传入刺击索引用于错开动画
            );
                stabCounter++;
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Bleeding, 180);
        }
    }

    internal class HeartcarverDagger : ModProjectile
    {
        public override string Texture => CWRConstant.Item_Melee + "Heartcarver";

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        //状态机
        private enum DaggerState
        {
            Gathering,//聚集阶段
            Orbiting,//环绕阶段
            Charging,//蓄力阶段
            Launching//发射阶段
        }

        private DaggerState State {
            get => (DaggerState)Projectile.ai[1];
            set => Projectile.ai[1] = (float)value;
        }

        private ref float DaggerIndex => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[2];

        //环绕参数
        private float orbitRadius = 120f;
        private float orbitAngle = 0f;
        private float orbitSpeed = 0.04f;
        private const float MaxOrbitSpeed = 0.35f;

        //蓄力参数
        private const int GatherDuration = 20;
        private const int OrbitDuration = 60;
        private const int ChargeDuration = 40;
        private const float LaunchSpeed = 26f;

        //视觉效果
        private float glowIntensity = 0f;
        private float trailIntensity = 0f;
        private float pulseTimer = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            StateTimer++;
            pulseTimer += 0.08f;

            //状态机
            switch (State) {
                case DaggerState.Gathering:
                    GatheringPhaseAI(owner);
                    break;

                case DaggerState.Orbiting:
                    OrbitingPhaseAI(owner);
                    break;

                case DaggerState.Charging:
                    ChargingPhaseAI(owner);
                    break;

                case DaggerState.Launching:
                    LaunchingPhaseAI(owner);
                    break;
            }

            //旋转
            Projectile.rotation += MathHelper.Lerp(0.1f, 0.6f, orbitSpeed / MaxOrbitSpeed);

            //血红色照明
            float lightIntensity = glowIntensity * 0.7f;
            Lighting.AddLight(Projectile.Center,
             0.9f * lightIntensity,
                0.2f * lightIntensity,
               0.2f * lightIntensity);
        }

        //聚集阶段：匕首快速飞向玩家附近
        private void GatheringPhaseAI(Player owner) {
            float progress = StateTimer / GatherDuration;

            //计算初始环绕位置
            float targetAngle = MathHelper.TwoPi * DaggerIndex / 8f;
            Vector2 targetPos = owner.Center + targetAngle.ToRotationVector2() * orbitRadius;

            //使用缓动创造有力的冲刺感
            float easeProgress = CWRUtils.EaseOutCubic(progress);
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, easeProgress * 0.35f);

            //提前开始旋转
            orbitAngle = targetAngle;

            glowIntensity = MathHelper.Lerp(0f, 0.5f, progress);

            //聚集血色粒子
            if (Main.rand.NextBool(4)) {
                SpawnGatherParticle();
            }

            //转入环绕阶段
            if (StateTimer >= GatherDuration) {
                State = DaggerState.Orbiting;
                StateTimer = 0;

                //匕首碰撞音效
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.4f,
                    Pitch = 0.2f
                }, Projectile.Center);
            }
        }

        //环绕阶段：环绕玩家并逐渐加速
        private void OrbitingPhaseAI(Player owner) {
            float progress = StateTimer / OrbitDuration;

            //加速旋转
            float speedProgress = CWRUtils.EaseInQuad(progress);
            orbitSpeed = MathHelper.Lerp(0.04f, MaxOrbitSpeed * 0.6f, speedProgress);

            //半径脉冲营造能量聚集感
            float radiusPulse = MathF.Sin(StateTimer * 0.3f) * 10f;
            float currentRadius = orbitRadius + radiusPulse * progress;

            //更新环绕角度
            orbitAngle += orbitSpeed;

            //计算环绕位置
            Vector2 orbitOffset = orbitAngle.ToRotationVector2() * currentRadius;
            Vector2 targetPos = owner.Center + orbitOffset;

            //平滑跟随
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.3f);

            //辉光强度增加
            glowIntensity = MathHelper.Lerp(0.5f, 0.8f, progress);
            trailIntensity = progress;

            //血色粒子环绕
            if (Main.rand.NextBool(3)) {
                SpawnOrbitParticle(owner.Center, progress);
            }

            //周期性匕首切割音效
            if (StateTimer % (int)MathHelper.Lerp(25, 8, progress) == 0) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.2f * progress,
                    Pitch = -0.3f + progress * 0.4f
                }, Projectile.Center);
            }

            //转入蓄力阶段
            if (StateTimer >= OrbitDuration) {
                State = DaggerState.Charging;
                StateTimer = 0;

                //蓄力音效
                SoundEngine.PlaySound(SoundID.Item28 with {
                    Volume = 0.6f,
                    Pitch = -0.3f
                }, Projectile.Center);
            }
        }

        //蓄力阶段：最高速旋转准备发射
        private void ChargingPhaseAI(Player owner) {
            float progress = StateTimer / ChargeDuration;

            //达到最高旋转速度
            orbitSpeed = MathHelper.Lerp(MaxOrbitSpeed * 0.6f, MaxOrbitSpeed, CWRUtils.EaseInOutQuad(progress));

            //半径脉动蓄力震荡感
            float radiusOscillation = MathF.Sin(StateTimer * 0.5f) * 15f * progress;
            float currentRadius = orbitRadius - 20f * progress + radiusOscillation;

            //更新环绕
            orbitAngle += orbitSpeed;
            Vector2 orbitOffset = orbitAngle.ToRotationVector2() * currentRadius;
            Vector2 targetPos = owner.Center + orbitOffset;
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.4f);

            //最大辉光
            glowIntensity = 0.9f + MathF.Sin(StateTimer * 0.8f) * 0.1f;
            trailIntensity = 1f;

            //密集血色粒子
            if (Main.rand.NextBool()) {
                SpawnChargeParticle(owner.Center, progress);
            }

            //蓄力血色脉冲
            if (StateTimer % 10 == 0) {
                SpawnChargePulse(owner.Center);
            }

            //高频匕首音效
            if (StateTimer % 6 == 0) {
                SoundEngine.PlaySound(SoundID.Item1 with {
                    Volume = 0.15f + progress * 0.25f,
                    Pitch = 0.2f + progress * 0.4f
                }, Projectile.Center);
            }

            //转入发射阶段
            if (StateTimer >= ChargeDuration) {
                LaunchToTarget(owner);
            }
        }

        //发射向目标
        private void LaunchToTarget(Player owner) {
            NPC target = owner.Center.FindClosestNPC(1200);

            Vector2 launchDir;
            if (target != null) {
                launchDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
            }
            else {
                launchDir = (orbitAngle - MathHelper.PiOver2).ToRotationVector2();
            }

            //计算发射速度基于当前旋转速度增加动量感
            float momentumBonus = orbitSpeed / MaxOrbitSpeed;
            float finalSpeed = LaunchSpeed * (1f + momentumBonus * 0.5f);

            Projectile.velocity = launchDir * finalSpeed;
            Projectile.tileCollide = true;

            State = DaggerState.Launching;
            StateTimer = 0;

            //爆发式血色粒子
            SpawnLaunchBurst();

            //强力发射音效
            SoundEngine.PlaySound(SoundID.Item71 with {
                Volume = 0.7f,
                Pitch = 0.3f
            }, Projectile.Center);
        }

        //发射阶段：向目标飞行
        private void LaunchingPhaseAI(Player owner) {
            //速度衰减
            Projectile.velocity *= 0.99f;

            //追踪敌人
            NPC target = Projectile.Center.FindClosestNPC(800);
            if (target != null && Projectile.timeLeft < 570) {
                Projectile.SmoothHomingBehavior(target.Center, 1.01f, 0.08f);
            }

            //轨迹强度保持
            trailIntensity = 1f;
            glowIntensity = 0.9f;

            //飞行血色粒子
            if (Main.rand.NextBool(3)) {
                SpawnLaunchTrailParticle();
            }

            //超时消失
            if (StateTimer > 180) {
                Projectile.Kill();
            }
        }

        //===== 粒子效果方法 =====

        private void SpawnGatherParticle() {
            Dust gather = Dust.NewDustPerfect(
      Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                DustID.Blood,
                (Main.player[Projectile.owner].Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f),
                100,
                default,
                Main.rand.NextFloat(0.8f, 1.3f)
            );
            gather.noGravity = true;
            gather.fadeIn = 1.1f;
        }

        private void SpawnOrbitParticle(Vector2 ownerCenter, float progress) {
            Vector2 toCenter = (ownerCenter - Projectile.Center).SafeNormalize(Vector2.Zero);
            Vector2 velocity = toCenter * Main.rand.NextFloat(0.5f, 2f) * progress;

            Dust orbit = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                DustID.Blood,
                velocity,
                100,
                default,
                Main.rand.NextFloat(0.9f, 1.4f)
            );
            orbit.noGravity = true;
            orbit.fadeIn = 1.1f;
        }

        private void SpawnChargeParticle(Vector2 ownerCenter, float progress) {
            Vector2 toCenter = (ownerCenter - Projectile.Center).SafeNormalize(Vector2.Zero);
            Vector2 velocity = toCenter * Main.rand.NextFloat(2f, 5f) * progress;

            Dust charge = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.Blood,
                velocity,
                100,
                default,
                Main.rand.NextFloat(1.2f, 1.9f)
            );
            charge.noGravity = true;
            charge.fadeIn = 1.2f;
        }

        private void SpawnChargePulse(Vector2 ownerCenter) {
            //环形血色脉冲
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f;
                Vector2 velocity = angle.ToRotationVector2() * 2.5f;

                Dust pulse = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Blood,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.3f, 1.8f)
                );
                pulse.noGravity = true;
                pulse.fadeIn = 1.2f;
            }
        }

        private void SpawnLaunchBurst() {
            //爆发式血色粒子
            for (int i = 0; i < 25; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 12f);

                Dust burst = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Blood,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.4f, 2.2f)
                );
                burst.noGravity = true;
                burst.fadeIn = 1.3f;
            }

            //额外发光粒子
            for (int i = 0; i < 8; i++) {
                AltSparkParticle spark = new AltSparkParticle(
                    Projectile.Center,
                    Main.rand.NextVector2Circular(8f, 8f),
                    false,
                    Main.rand.Next(15, 25),
                    Main.rand.NextFloat(1.5f, 2.5f),
                    Color.Lerp(Color.Red, Color.DarkRed, Main.rand.NextFloat())
                );
                GeneralParticleHandler.SpawnParticle(spark);
            }
        }

        private void SpawnLaunchTrailParticle() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                DustID.Blood,
                -Projectile.velocity * Main.rand.NextFloat(0.1f, 0.25f),
                100,
                default,
                Main.rand.NextFloat(0.9f, 1.4f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1.1f;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //碰撞后反弹
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.7f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.7f;
            }

            //碰撞音效
            SoundEngine.PlaySound(SoundID.Dig with {
                Volume = 0.5f,
                Pitch = 0.3f
            }, Projectile.Center);

            //碰撞血色碎片
            for (int i = 0; i < 5; i++) {
                Dust.NewDust(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Blood,
                    Scale: Main.rand.NextFloat(1f, 1.5f)
                );
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中血色粒子效果
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust hitDust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Blood,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.3f, 2f)
                );
                hitDust.noGravity = true;
                hitDust.fadeIn = 1.2f;
            }

            //击中音效
            SoundEngine.PlaySound(SoundID.NPCHit18 with {
                Volume = 0.5f,
                Pitch = 0.2f
            }, Projectile.Center);

            //添加流血Buff
            target.AddBuff(BuffID.Bleeding, 240);
        }

        public override void OnKill(int timeLeft) {
            //消失时的血色粒子效果
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);
                Dust killDust = Dust.NewDustPerfect(
                  Projectile.Center,
                    DustID.Blood,
               velocity,
             100,
                    default,
           Main.rand.NextFloat(1.2f, 1.8f)
                );
                killDust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D daggerTex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = daggerTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;

            Color baseColor = lightColor;
            float alpha = (255f - Projectile.alpha) / 255f;

            //绘制匕首残影拖尾
            if (State == DaggerState.Orbiting || State == DaggerState.Charging || State == DaggerState.Launching) {
                DrawDaggerAfterimages(sb, daggerTex, sourceRect, origin, baseColor, alpha);
            }

            //绘制光晕
            if (glowIntensity > 0.3f && SoftGlow?.Value != null) {
                Texture2D glow = SoftGlow.Value;
                float glowScale = Projectile.scale * (0.9f + glowIntensity * 0.5f);
                float glowAlpha = (glowIntensity - 0.3f) * alpha * 0.4f;

                if (State == DaggerState.Charging) {
                    glowAlpha *= 1.5f;
                }

                sb.Draw(
                    glow,
                    drawPos,
                    null,
                    new Color(255, 100, 100, 0) * glowAlpha,
                    Projectile.rotation,
                    glow.Size() / 2f,
                    glowScale,
                    SpriteEffects.None,
                     0f
                );
            }

            //绘制主体匕首
            sb.Draw(
                daggerTex,
                drawPos,
                sourceRect,
                baseColor * alpha,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            //蓄力/发射时的血红辉光覆盖层
            if ((State == DaggerState.Charging || State == DaggerState.Launching) && glowIntensity > 0.5f) {
                float lightAlpha = (glowIntensity - 0.5f) * 2f * alpha * 0.5f;
                Color daggerLight = new Color(220, 50, 50);

                sb.Draw(
                    daggerTex,
                    drawPos,
                    sourceRect,
                    daggerLight * lightAlpha,
                    Projectile.rotation,
                    origin,
                    Projectile.scale * 1.05f,
                    SpriteEffects.None,
                    0
                );
            }

            return false;
        }

        //绘制匕首残影拖尾
        private void DrawDaggerAfterimages(SpriteBatch sb, Texture2D daggerTex, Rectangle sourceRect,
            Vector2 origin, Color baseColor, float alpha) {

            int afterimageCount = State == DaggerState.Launching ? 12 : (State == DaggerState.Charging ? 10 : 6);

            for (int i = 0; i < afterimageCount; i++) {
                if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) continue;

                float afterimageProgress = 1f - i / (float)afterimageCount;
                float afterimageAlpha = afterimageProgress * trailIntensity * alpha;

                //残影颜色：环绕时淡红蓄力时增强发射时最强
                Color afterimageColor;
                if (State == DaggerState.Launching) {
                    afterimageColor = Color.Lerp(
                    new Color(200, 50, 50),
                    new Color(255, 100, 100),
                    afterimageProgress
                ) * (afterimageAlpha * 0.7f);
                }
                else if (State == DaggerState.Charging) {
                    afterimageColor = new Color(210, 60, 60) * (afterimageAlpha * 0.6f);
                }
                else {
                    afterimageColor = baseColor * (afterimageAlpha * 0.5f);
                }

                Vector2 afterimagePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float afterimageScale = Projectile.scale * MathHelper.Lerp(0.85f, 1f, afterimageProgress);

                sb.Draw(
                    daggerTex,
                    afterimagePos,
                    sourceRect,
                    afterimageColor,
                    Projectile.rotation - i * 0.1f * (orbitSpeed / MaxOrbitSpeed),
                    origin,
                    afterimageScale,
                    SpriteEffects.None,
                    0
                );
            }
        }
    }
}
