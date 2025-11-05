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

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.StormGoddessSpearProj
{
    /// <summary>
    /// 风暴女神
    /// </summary>
    internal class StormGoddess : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "StormGoddess";

        //AI状态机
        private enum GoddessState
        {
            Appearing,      //出现
            Following,      //跟随
            PrepareStrike,  //准备攻击
            Striking,       //攻击中
            Returning,      //返回
            Idle           //待机
        }

        private GoddessState State {
            get => (GoddessState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float StateTimer => ref Projectile.ai[1];
        private ref float AnimationFrame => ref Projectile.localAI[0];
        private ref float GlowIntensity => ref Projectile.localAI[1];

        //运动系统 - Boid算法参数
        private Vector2 velocity = Vector2.Zero;
        private Vector2 acceleration = Vector2.Zero;
        private Vector2 targetPosition = Vector2.Zero;
        private Vector2 idleOffset = Vector2.Zero;

        //力场参数
        private const float MaxSpeed = 12f;
        private const float MaxForce = 0.8f;
        private const float ArriveDistance = 150f;
        private const float IdleOrbitRadius = 120f;
        private const float IdleOrbitSpeed = 0.02f;

        //动画参数
        private const int TotalFrames = 5;
        private float animationSpeed = 0.2f;
        private float scale = 1f;
        private float rotation = 0f;
        private float targetRotation = 0f;

        //攻击系统
        private NPC currentTarget = null;
        private int attackCooldown = 0;
        private const int AttackCooldownMax = 120;
        private Vector2 strikePosition = Vector2.Zero;

        //粒子效果
        private float particleTimer = 0f;
        private List<LightningPrepareEffect> lightningEffects = new List<LightningPrepareEffect>();

        //内部类：闪电蓄力特效
        private class LightningPrepareEffect
        {
            public Vector2 Position;
            public float Life;
            public float MaxLife;
            public float Rotation;
            public float Scale;
        }

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = TotalFrames;
            ProjectileID.Sets.TrailCacheLength[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 64;
            Projectile.height = 64;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2;
            Projectile.alpha = 0;
        }

        public override void AI() {
            //保持存活
            if (Owner.active && !Owner.dead && Item.type == ModContent.ItemType<Items.Melee.StormGoddessSpear>()) {
                Projectile.timeLeft = 2;
            }
            else {
                //玩家不持有武器时消失
                Projectile.Kill();
                return;
            }

            StateTimer++;
            particleTimer++;

            //状态机更新
            switch (State) {
                case GoddessState.Appearing:
                    AppearingAI();
                    break;

                case GoddessState.Following:
                    FollowingAI();
                    break;

                case GoddessState.PrepareStrike:
                    PrepareStrikeAI();
                    break;

                case GoddessState.Striking:
                    StrikingAI();
                    break;

                case GoddessState.Returning:
                    ReturningAI();
                    break;

                case GoddessState.Idle:
                    IdleAI();
                    break;
            }

            //更新运动
            UpdateMovement();

            //更新动画
            UpdateAnimation();

            //更新特效
            UpdateEffects();

            //攻击冷却
            if (attackCooldown > 0) {
                attackCooldown--;
            }
        }

        #region 状态AI

        /// <summary>
        /// 出现状态 - 优雅地从天而降
        /// </summary>
        private void AppearingAI() {
            if (StateTimer == 1) {
                //初始化位置：玩家上方
                Projectile.Center = Owner.Center + new Vector2(0, -400);
                Projectile.alpha = 255;
                scale = 0.5f;
                GlowIntensity = 0f;

                //出现音效
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.3f }, Projectile.Center);
            }

            //优雅下降
            float progress = StateTimer / 60f;
            float easeProgress = EaseOutCubic(progress);

            //淡入
            Projectile.alpha = (int)(255 * (1f - easeProgress));

            //缩放动画
            scale = MathHelper.Lerp(0.5f, 1f, easeProgress);

            //发光强度
            GlowIntensity = easeProgress;

            //下降到目标位置
            Vector2 targetPos = Owner.Center + new Vector2(80, -80);
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, easeProgress * 0.1f);

            //生成出现粒子
            if (StateTimer % 3 == 0) {
                SpawnAppearParticles();
            }

            //转换到跟随状态
            if (StateTimer >= 60) {
                State = GoddessState.Following;
                StateTimer = 0;
                velocity = Vector2.Zero;
            }
        }

        /// <summary>
        /// 跟随状态 - 使用Boid算法自然跟随
        /// </summary>
        private void FollowingAI() {
            //目标位置：玩家侧后方
            Vector2 ownerDirection = Owner.direction == 1 ? Vector2.UnitX : -Vector2.UnitX;
            targetPosition = Owner.Center + ownerDirection * 100 + new Vector2(0, -100);

            //应用跟随力
            Vector2 desired = targetPosition - Projectile.Center;
            float distance = desired.Length();

            if (distance < ArriveDistance) {
                //到达行为：减速
                float m = MathHelper.Clamp(distance / ArriveDistance, 0, 1);
                desired = desired.SafeNormalize(Vector2.Zero) * MaxSpeed * m;
            }
            else {
                //寻找行为：全速
                desired = desired.SafeNormalize(Vector2.Zero) * MaxSpeed;
            }

            Vector2 steer = desired - velocity;
            steer = ClampMagnitude(steer, MaxForce);
            ApplyForce(steer);

            //检测是否应该攻击
            if (ShouldAttack() && attackCooldown <= 0) {
                currentTarget = FindNearestEnemy();
                if (currentTarget != null) {
                    State = GoddessState.PrepareStrike;
                    StateTimer = 0;
                }
            }

            //轻微的环绕运动
            if (distance < 50f) {
                State = GoddessState.Idle;
                StateTimer = 0;
                idleOffset = new Vector2(IdleOrbitRadius, 0).RotatedBy(Main.GlobalTimeWrappedHourly * IdleOrbitSpeed);
            }
        }

        /// <summary>
        /// 待机状态 - 优雅的环绕运动
        /// </summary>
        private void IdleAI() {
            //环绕玩家
            Vector2 ownerDirection = Owner.direction == 1 ? Vector2.UnitX : -Vector2.UnitX;
            float orbitAngle = Main.GlobalTimeWrappedHourly * IdleOrbitSpeed;
            idleOffset = new Vector2(
                MathF.Cos(orbitAngle) * IdleOrbitRadius,
                MathF.Sin(orbitAngle) * 60 - 80
            );

            targetPosition = Owner.Center + idleOffset;

            //应用柔和的力
            Vector2 desired = (targetPosition - Projectile.Center).SafeNormalize(Vector2.Zero) * MaxSpeed * 0.5f;
            Vector2 steer = desired - velocity;
            steer = ClampMagnitude(steer, MaxForce * 0.3f);
            ApplyForce(steer);

            //生成待机粒子
            if (particleTimer % 15 == 0) {
                SpawnIdleParticles();
            }

            //检测攻击
            if (ShouldAttack() && attackCooldown <= 0) {
                currentTarget = FindNearestEnemy();
                if (currentTarget != null) {
                    State = GoddessState.PrepareStrike;
                    StateTimer = 0;
                }
            }

            //返回跟随
            if (Vector2.Distance(Projectile.Center, Owner.Center) > 300f) {
                State = GoddessState.Following;
                StateTimer = 0;
            }
        }

        /// <summary>
        /// 准备攻击状态 - 蓄力动画
        /// </summary>
        private void PrepareStrikeAI() {
            if (currentTarget == null || !currentTarget.active) {
                State = GoddessState.Following;
                StateTimer = 0;
                return;
            }

            //蓄力时间
            const int prepareTime = 45;

            if (StateTimer == 1) {
                //蓄力音效
                SoundEngine.PlaySound(SoundID.DD2_LightningAuraZap with { 
                    Volume = 0.6f, 
                    Pitch = -0.4f 
                }, Projectile.Center);
            }

            //移动到敌人上方
            strikePosition = currentTarget.Center + new Vector2(0, -350);
            targetPosition = strikePosition;

            //快速移动到攻击位置
            Vector2 desired = (targetPosition - Projectile.Center).SafeNormalize(Vector2.Zero) * MaxSpeed * 1.5f;
            Vector2 steer = desired - velocity;
            steer = ClampMagnitude(steer, MaxForce * 1.5f);
            ApplyForce(steer);

            //蓄力特效
            float prepareProgress = StateTimer / prepareTime;
            GlowIntensity = 1f + prepareProgress * 2f;
            scale = 1f + MathF.Sin(prepareProgress * MathHelper.TwoPi * 3f) * 0.15f;

            //生成蓄力粒子
            if (StateTimer % 3 == 0) {
                SpawnPrepareParticles();
            }

            //生成闪电预示
            if (StateTimer % 8 == 0) {
                CreateLightningPrepareEffect();
            }

            //更新闪电特效
            UpdateLightningEffects();

            //转换到攻击状态
            if (StateTimer >= prepareTime) {
                State = GoddessState.Striking;
                StateTimer = 0;
            }
        }

        /// <summary>
        /// 攻击状态 - 释放闪电
        /// </summary>
        private void StrikingAI() {
            if (currentTarget == null || !currentTarget.active) {
                State = GoddessState.Returning;
                StateTimer = 0;
                return;
            }

            const int strikeTime = 30;

            if (StateTimer == 1) {
                //释放闪电
                ReleaseStormLightning();

                //攻击音效
                SoundEngine.PlaySound(SoundID.Item122 with { 
                    Volume = 1f, 
                    Pitch = -0.3f 
                }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.DD2_LightningBugZap with { 
                    Volume = 0.8f 
                }, Projectile.Center);

                //屏幕震动
                if (Owner.whoAmI == Main.myPlayer) {
                    Owner.GetModPlayer<CWRPlayer>().ScreenShakeValue = Math.Max(
                        Owner.GetModPlayer<CWRPlayer>().ScreenShakeValue, 8f);
                }
            }

            //攻击后坐力
            velocity *= 0.8f;

            //发光脉冲
            GlowIntensity = 3f * (1f - StateTimer / strikeTime);

            //攻击动画
            animationSpeed = 0.4f;

            //生成攻击粒子
            if (StateTimer % 2 == 0) {
                SpawnStrikeParticles();
            }

            //转换到返回状态
            if (StateTimer >= strikeTime) {
                State = GoddessState.Returning;
                StateTimer = 0;
                attackCooldown = AttackCooldownMax;
                currentTarget = null;
                lightningEffects.Clear();
            }
        }

        /// <summary>
        /// 返回状态 - 回到玩家身边
        /// </summary>
        private void ReturningAI() {
            targetPosition = Owner.Center + new Vector2(Owner.direction * 80, -100);

            //快速返回
            Vector2 desired = (targetPosition - Projectile.Center).SafeNormalize(Vector2.Zero) * MaxSpeed * 1.2f;
            Vector2 steer = desired - velocity;
            steer = ClampMagnitude(steer, MaxForce * 1.2f);
            ApplyForce(steer);

            //恢复发光
            GlowIntensity = MathHelper.Lerp(GlowIntensity, 1f, 0.05f);
            scale = MathHelper.Lerp(scale, 1f, 0.05f);

            //到达后转换状态
            if (Vector2.Distance(Projectile.Center, targetPosition) < 50f) {
                State = GoddessState.Following;
                StateTimer = 0;
            }
        }

        #endregion

        #region 运动系统

        /// <summary>
        /// 应用力 - Boid算法核心
        /// </summary>
        private void ApplyForce(Vector2 force) {
            acceleration += force;
        }

        /// <summary>
        /// 更新运动
        /// </summary>
        private void UpdateMovement() {
            //更新速度
            velocity += acceleration;
            velocity = ClampMagnitude(velocity, MaxSpeed);

            //更新位置
            Projectile.Center += velocity;

            //重置加速度
            acceleration = Vector2.Zero;

            //平滑旋转朝向
            if (velocity.LengthSquared() > 0.1f) {
                targetRotation = velocity.ToRotation();
            }
            rotation = MathHelper.Lerp(rotation, targetRotation, 0.2f);
        }

        /// <summary>
        /// 限制向量大小
        /// </summary>
        private Vector2 ClampMagnitude(Vector2 vector, float maxLength) {
            float length = vector.Length();
            if (length > maxLength) {
                return vector.SafeNormalize(Vector2.Zero) * maxLength;
            }
            return vector;
        }

        #endregion

        #region 攻击系统

        /// <summary>
        /// 是否应该攻击
        /// </summary>
        private bool ShouldAttack() {
            //玩家正在使用物品
            return Owner.itemAnimation > 0 && Owner.HeldItem.type == ModContent.ItemType<Items.Melee.StormGoddessSpear>();
        }

        /// <summary>
        /// 寻找最近的敌人
        /// </summary>
        private NPC FindNearestEnemy() {
            NPC closest = null;
            float closestDist = 800f;

            foreach (NPC npc in Main.npc) {
                if (npc.active && npc.CanBeChasedBy() && !npc.friendly) {
                    float dist = Vector2.Distance(Owner.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        /// <summary>
        /// 释放风暴闪电
        /// </summary>
        private void ReleaseStormLightning() {
            if (currentTarget == null || !Projectile.IsOwnedByLocalPlayer()) return;

            //主闪电
            Vector2 lightningStart = Projectile.Center + new Vector2(0, 30);
            Vector2 direction = (currentTarget.Center - lightningStart).SafeNormalize(Vector2.UnitY);

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                lightningStart,
                direction * 25f,
                ModContent.ProjectileType<StormLightning>(),
                (int)(Owner.GetWeaponDamage(Owner.HeldItem) * 1.2f),
                8f,
                Owner.whoAmI,
                ai0: 0,
                ai1: 0,
                ai2: 1  //白蓝色
            );

            //额外的辅助闪电
            int extraLightning = Main.rand.Next(2, 4);
            for (int i = 0; i < extraLightning; i++) {
                Vector2 offset = Main.rand.NextVector2Circular(100, 50);
                Vector2 extraStart = lightningStart + offset;
                Vector2 extraDir = (currentTarget.Center + Main.rand.NextVector2Circular(80, 80) - extraStart).SafeNormalize(Vector2.UnitY);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    extraStart,
                    extraDir * Main.rand.NextFloat(20f, 28f),
                    ModContent.ProjectileType<StormLightning>(),
                    (int)(Owner.GetWeaponDamage(Owner.HeldItem) * 0.8f),
                    6f,
                    Owner.whoAmI,
                    ai0: 0,
                    ai1: 0,
                    ai2: 101  //白蓝色，不追踪
                );
            }
        }

        #endregion

        #region 粒子特效

        /// <summary>
        /// 生成出现粒子
        /// </summary>
        private void SpawnAppearParticles() {
            Vector2 particlePos = Projectile.Center + Main.rand.NextVector2Circular(32, 32);
            Vector2 particleVel = Main.rand.NextVector2Circular(3f, 3f);

            BasePRT particle = new PRT_Light(
                particlePos,
                particleVel,
                0.3f,
                new Color(180, 220, 255),
                Main.rand.Next(15, 25),
                1.2f,
                1.8f,
                hueShift: 0f
            );
            PRTLoader.AddParticle(particle);
        }

        /// <summary>
        /// 生成待机粒子
        /// </summary>
        private void SpawnIdleParticles() {
            Vector2 particlePos = Projectile.Center + Main.rand.NextVector2Circular(40, 40);
            
            BasePRT particle = new PRT_Spark(
                particlePos,
                Main.rand.NextVector2Circular(2f, 2f),
                false,
                Main.rand.Next(10, 20),
                0.8f,
                new Color(200, 230, 255),
                Owner
            );
            PRTLoader.AddParticle(particle);
        }

        /// <summary>
        /// 生成蓄力粒子
        /// </summary>
        private void SpawnPrepareParticles() {
            //环绕粒子
            for (int i = 0; i < 2; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 particlePos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(60, 100);
                Vector2 particleVel = (Projectile.Center - particlePos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(5f, 10f);

                BasePRT particle = new PRT_Light(
                    particlePos,
                    particleVel,
                    0.4f,
                    new Color(150, 200, 255),
                    Main.rand.Next(20, 30),
                    1.5f,
                    2.5f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }

        /// <summary>
        /// 生成攻击粒子
        /// </summary>
        private void SpawnStrikeParticles() {
            for (int i = 0; i < 3; i++) {
                Vector2 particlePos = Projectile.Center + Main.rand.NextVector2Circular(50, 50);
                Vector2 particleVel = Main.rand.NextVector2Circular(10f, 10f);

                BasePRT particle = new PRT_Light(
                    particlePos,
                    particleVel,
                    0.5f,
                    new Color(200, 230, 255),
                    Main.rand.Next(15, 25),
                    1.8f,
                    3f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }

        /// <summary>
        /// 创建闪电蓄力特效
        /// </summary>
        private void CreateLightningPrepareEffect() {
            if (currentTarget == null) return;

            lightningEffects.Add(new LightningPrepareEffect {
                Position = currentTarget.Center,
                Life = 0,
                MaxLife = 40,
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                Scale = Main.rand.NextFloat(0.5f, 1f)
            });
        }

        /// <summary>
        /// 更新闪电特效
        /// </summary>
        private void UpdateLightningEffects() {
            for (int i = lightningEffects.Count - 1; i >= 0; i--) {
                var effect = lightningEffects[i];
                effect.Life++;

                if (effect.Life >= effect.MaxLife) {
                    lightningEffects.RemoveAt(i);
                }
            }
        }

        #endregion

        #region 动画系统

        /// <summary>
        /// 更新动画
        /// </summary>
        private void UpdateAnimation() {
            //更新帧
            AnimationFrame += animationSpeed;
            if (AnimationFrame >= TotalFrames) {
                AnimationFrame = 0;
            }
            Projectile.frame = (int)AnimationFrame;

            //根据状态调整动画速度
            animationSpeed = State switch {
                GoddessState.PrepareStrike => 0.3f,
                GoddessState.Striking => 0.5f,
                _ => 0.2f
            };

            //发光强度衰减
            if (State != GoddessState.PrepareStrike && State != GoddessState.Striking) {
                GlowIntensity = MathHelper.Lerp(GlowIntensity, 1f, 0.05f);
            }
        }

        #endregion

        #region 渲染

        /// <summary>
        /// 更新特效
        /// </summary>
        private void UpdateEffects() {
            //光源
            float lightIntensity = GlowIntensity * 0.8f;
            Lighting.AddLight(Projectile.Center, 
                0.7f * lightIntensity, 
                0.9f * lightIntensity, 
                1f * lightIntensity);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Texture2D glowTex = CWRAsset.StarTexture.Value;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle frameRect = texture.Frame(1, TotalFrames, 0, Projectile.frame);
            Vector2 origin = frameRect.Size() / 2f;

            //绘制残影
            if (State == GoddessState.PrepareStrike || State == GoddessState.Striking) {
                DrawAfterimages(sb, texture, frameRect, origin, lightColor);
            }

            //绘制外光晕
            if (GlowIntensity > 1f) {
                Color glowColor = new Color(180, 220, 255) with { A = 0 };
                float glowScale = scale * (GlowIntensity - 0.5f) * 2f;

                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    glowColor * 0.6f,
                    rotation,
                    glowTex.Size() / 2,
                    glowScale,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制主体
            Color baseColor = Color.White * ((255f - Projectile.alpha) / 255f);
            
            sb.Draw(
                texture,
                drawPos,
                frameRect,
                baseColor,
                0,
                origin,
                scale,
                Projectile.direction == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally,
                0
            );

            //绘制闪电蓄力特效
            DrawLightningPrepareEffects(sb);

            return false;
        }

        /// <summary>
        /// 绘制残影
        /// </summary>
        private void DrawAfterimages(SpriteBatch sb, Texture2D texture, Rectangle frameRect, Vector2 origin, Color lightColor) {
            for (int i = 1; i < Projectile.oldPos.Length && i < 8; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - i / 8f;
                Vector2 afterPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color afterColor = new Color(180, 220, 255) with { A = 0 } * progress * 0.5f;

                sb.Draw(
                    texture,
                    afterPos,
                    frameRect,
                    afterColor,
                    0,
                    origin,
                    scale * progress,
                    Projectile.direction == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally,
                    0
                );
            }
        }

        /// <summary>
        /// 绘制闪电蓄力特效
        /// </summary>
        private void DrawLightningPrepareEffects(SpriteBatch sb) {
            Texture2D glowTex = CWRAsset.StarTexture.Value;

            foreach (var effect in lightningEffects) {
                float progress = effect.Life / effect.MaxLife;
                float alpha = (float)Math.Sin(progress * MathHelper.Pi);
                float effectScale = effect.Scale * (1f + progress * 2f);

                Vector2 drawPos = effect.Position - Main.screenPosition;
                Color effectColor = new Color(200, 230, 255) with { A = 0 };

                sb.Draw(
                    glowTex,
                    drawPos,
                    null,
                    effectColor * alpha * 0.6f,
                    effect.Rotation + progress * MathHelper.TwoPi,
                    glowTex.Size() / 2,
                    effectScale,
                    SpriteEffects.None,
                    0
                );
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 缓动函数 - EaseOutCubic
        /// </summary>
        private float EaseOutCubic(float t) {
            return 1f - MathF.Pow(1f - t, 3f);
        }

        #endregion

        public override void OnKill(int timeLeft) {
            //消失特效
            for (int i = 0; i < 30; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(10f, 10f);
                BasePRT particle = new PRT_Light(
                    Projectile.Center,
                    velocity,
                    0.4f,
                    new Color(180, 220, 255),
                    Main.rand.Next(20, 35),
                    1.5f,
                    2.5f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
        }
    }
}
