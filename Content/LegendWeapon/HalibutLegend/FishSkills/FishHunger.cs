using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishHunger : FishSkill
    {
        public override int UnlockFishID => ItemID.Hungerfish;
        public override int DefaultCooldown => 60 - +HalibutData.GetDomainLayer() * 3;
        public override int ResearchDuration => 60 * 18;
        //恶鬼管理系统
        private static readonly List<int> ActiveHungries = new();
        private static int MaxHungries => (1 + HalibutData.GetDomainLayer() / 3); //最多1-4个恶鬼

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                SetCooldown();

                //清理失效恶鬼
                CleanupInactiveHungries();

                //如果恶鬼数量未满，生成新恶鬼
                if (ActiveHungries.Count < MaxHungries) {
                    //在玩家附近生成恶鬼
                    Vector2 spawnPos = player.Center + Main.rand.NextVector2Circular(80f, 80f);

                    int hungryProj = Projectile.NewProjectile(
                        source,
                        spawnPos,
                        Vector2.Zero,
                        ModContent.ProjectileType<HungryCompanionProjectile>(),
                        (int)(damage * 1.6f + HalibutData.GetDomainLayer() * 0.4f),
                        knockback * 0.1f,
                        player.whoAmI,
                        ai0: ActiveHungries.Count //传递索引
                    );

                    if (hungryProj >= 0 && hungryProj < Main.maxProjectiles) {
                        ActiveHungries.Add(hungryProj);

                        //生成召唤特效
                        SpawnSummonEffect(spawnPos);

                        //恶鬼召唤音效
                        SoundEngine.PlaySound(SoundID.NPCHit1 with {
                            Volume = 0.6f,
                            Pitch = -0.3f
                        }, spawnPos);
                    }
                }
                else {
                    //恶鬼已满，命令现有恶鬼攻击
                    CommandHungriesToAttack(player);
                }
            }

            return null;
        }

        private static void CleanupInactiveHungries() {
            ActiveHungries.RemoveAll(id => id < 0 || id >= Main.maxProjectiles || !Main.projectile[id].active);
        }

        private void CommandHungriesToAttack(Player player) {
            //命令所有恶鬼向鼠标方向发起攻击
            for (int i = 0; i < ActiveHungries.Count; i++) {
                int id = ActiveHungries[i];
                if (id >= 0 && id < Main.maxProjectiles && Main.projectile[id].active) {
                    if (Main.projectile[id].ModProjectile is HungryCompanionProjectile hungry) {
                        hungry.CommandAttack(Main.MouseWorld);
                    }
                }
            }

            //攻击命令音效
            SoundEngine.PlaySound(SoundID.Roar with {
                Volume = 0.5f,
                Pitch = -0.4f
            }, player.Center);
        }

        private void SpawnSummonEffect(Vector2 position) {
            //召唤时的血肉粒子效果
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 6f);

                Dust blood = Dust.NewDustPerfect(
                    position,
                    DustID.Blood,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 2f)
                );
                blood.noGravity = true;
            }

            //额外的肉质粒子
            for (int i = 0; i < 10; i++) {
                Dust flesh = Dust.NewDustDirect(
                    position - new Vector2(15),
                    30, 30,
                    DustID.Blood,
                    Scale: Main.rand.NextFloat(1.5f, 2.5f)
                );
                flesh.velocity = Main.rand.NextVector2Circular(3f, 3f);
                flesh.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 恶鬼伴随弹幕
    /// </summary>
    internal class HungryCompanionProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.TheHungryII;

        //状态机
        private enum HungryState
        {
            Idle,           //待机：围绕玩家漂浮
            FollowPlayer,   //跟随：跟随玩家移动
            Attacking,      //攻击：冲向目标
            Returning       //返回：攻击后返回玩家附近
        }

        private HungryState State {
            get => (HungryState)Projectile.ai[2];
            set => Projectile.ai[2] = (float)value;
        }

        private ref float HungryIndex => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[1];

        //攻击目标
        private Vector2 attackTarget = Vector2.Zero;
        private bool hasAttackTarget = false;

        //漂浮参数
        private float idleAngle = 0f;
        private float idleRadius = 100f;
        private float breathingPhase = 0f;

        //视觉效果
        private int currentFrame = 0;
        private int frameCounter = 0;
        private const int FrameSpeed = 5;
        private float squashStretch = 1f; //挤压拉伸效果

        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 6; //6帧动画
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60 * (10 + HalibutData.GetDomainLayer() * 2); //10-30秒生命期
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;

            //初始化随机相位
            breathingPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            idleAngle = MathHelper.TwoPi * HungryIndex / 3f;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];

            if (!owner.active || owner.dead) {
                //玩家死亡，恶鬼消散
                DespawnEffect();
                Projectile.Kill();
                return;
            }

            if (!FishSkill.GetT<FishHunger>().Active(owner)) {
                Projectile.Kill();
                return;
            }

            StateTimer++;

            //更新动画帧
            UpdateAnimation();

            //状态机
            switch (State) {
                case HungryState.Idle:
                    IdleAI(owner);
                    break;

                case HungryState.FollowPlayer:
                    FollowPlayerAI(owner);
                    break;

                case HungryState.Attacking:
                    AttackingAI(owner);
                    break;

                case HungryState.Returning:
                    ReturningAI(owner);
                    break;
            }

            //生物呼吸效果
            UpdateBreathing();

            //照明
            Lighting.AddLight(Projectile.Center, 0.5f, 0.2f, 0.2f);

            //定期发出生物音效
            if (StateTimer % 120 == 0) {
                SoundEngine.PlaySound(SoundID.NPCHit8 with {
                    Volume = 0.3f,
                    Pitch = -0.5f
                }, Projectile.Center);
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
        }

        /// <summary>
        /// 命令恶鬼攻击目标
        /// </summary>
        public void CommandAttack(Vector2 target) {
            attackTarget = target;
            hasAttackTarget = true;
            State = HungryState.Attacking;
            StateTimer = 0;

            //攻击前的蓄力效果
            SpawnAttackChargeEffect();
        }

        /// <summary>
        /// 待机状态：围绕玩家漂浮
        /// </summary>
        private void IdleAI(Player owner) {
            //计算理想位置
            idleAngle += 0.02f;
            Vector2 idleOffset = idleAngle.ToRotationVector2() * idleRadius;
            Vector2 targetPos = owner.Center + idleOffset;

            //平滑移动
            Vector2 toTarget = targetPos - Projectile.Center;
            float distance = toTarget.Length();

            if (distance > 5f) {
                Projectile.velocity = toTarget * 0.08f;
            }
            else {
                Projectile.velocity *= 0.9f;
            }

            //面向玩家
            FaceDirection(owner.Center);

            //定期切换到跟随状态
            if (distance > 200f) {
                State = HungryState.FollowPlayer;
                StateTimer = 0;
            }

            //待机粒子
            if (Main.rand.NextBool(20)) {
                SpawnIdleParticle();
            }
        }

        /// <summary>
        /// 跟随状态：快速跟上玩家
        /// </summary>
        private void FollowPlayerAI(Player owner) {
            Vector2 toOwner = owner.Center - Projectile.Center;
            float distance = toOwner.Length();

            //加速追赶
            Vector2 targetVelocity = toOwner.SafeNormalize(Vector2.Zero) * Math.Min(distance * 0.1f, 15f);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity, 0.15f);

            //面向移动方向
            if (Projectile.velocity.LengthSquared() > 1f) {
                FaceDirection(Projectile.Center + Projectile.velocity);
            }

            //靠近玩家后返回待机
            if (distance < 150f) {
                State = HungryState.Idle;
                StateTimer = 0;
            }

            //跟随粒子
            if (Main.rand.NextBool(5)) {
                SpawnFollowParticle();
            }
        }

        /// <summary>
        /// 攻击状态：冲向目标
        /// </summary>
        private void AttackingAI(Player owner) {
            if (!hasAttackTarget) {
                State = HungryState.Returning;
                StateTimer = 0;
                return;
            }

            float attackProgress = StateTimer / 60f;

            //前20帧：蓄力
            if (StateTimer < 20) {
                //后退蓄力
                Vector2 toTarget = (attackTarget - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = -toTarget * (1f - attackProgress * 3f) * 2f;

                //面向目标
                FaceDirection(attackTarget);

                //蓄力粒子
                if (Main.rand.NextBool(2)) {
                    SpawnChargeParticle();
                }
            }
            //20-40帧：快速冲刺
            else if (StateTimer < 40) {
                //计算冲刺速度
                Vector2 toTarget = (attackTarget - Projectile.Center).SafeNormalize(Vector2.Zero);
                float rushSpeed = 25f;
                Projectile.velocity = toTarget * rushSpeed;

                //拉伸效果
                squashStretch = 1.5f;

                //冲刺粒子
                if (Main.rand.NextBool(2)) {
                    SpawnRushParticle();
                }

                //冲刺音效
                if (StateTimer == 20) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with {
                        Volume = 0.6f,
                        Pitch = 0.2f
                    }, Projectile.Center);
                }
            }
            //40帧后：减速并返回
            else {
                Projectile.velocity *= 0.95f;

                if (StateTimer > 60) {
                    State = HungryState.Returning;
                    StateTimer = 0;
                    hasAttackTarget = false;
                }
            }
        }

        /// <summary>
        /// 返回状态：攻击后返回玩家附近
        /// </summary>
        private void ReturningAI(Player owner) {
            Vector2 returnPos = owner.Center + idleAngle.ToRotationVector2() * idleRadius;
            Vector2 toReturn = returnPos - Projectile.Center;
            float distance = toReturn.Length();

            //平滑返回
            Vector2 targetVelocity = toReturn.SafeNormalize(Vector2.Zero) * Math.Min(distance * 0.12f, 12f);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity, 0.1f);

            //面向移动方向
            if (Projectile.velocity.LengthSquared() > 1f) {
                FaceDirection(Projectile.Center + Projectile.velocity);
            }

            //返回待机位置
            if (distance < 50f) {
                State = HungryState.Idle;
                StateTimer = 0;
            }
        }

        /// <summary>
        /// 更新动画帧
        /// </summary>
        private void UpdateAnimation() {
            frameCounter++;
            if (frameCounter >= FrameSpeed) {
                frameCounter = 0;
                currentFrame++;
                if (currentFrame >= 6) {
                    currentFrame = 0;
                }
            }
            Projectile.frame = currentFrame;
        }

        /// <summary>
        /// 更新呼吸效果
        /// </summary>
        private void UpdateBreathing() {
            breathingPhase += 0.05f;

            //呼吸缩放
            float breathScale = (float)Math.Sin(breathingPhase) * 0.05f;
            squashStretch = MathHelper.Lerp(squashStretch, 1f + breathScale, 0.2f);
        }

        /// <summary>
        /// 面向目标方向
        /// </summary>
        private void FaceDirection(Vector2 target) {
            Vector2 direction = target - Projectile.Center;
            if (Math.Abs(direction.X) > 10f) {
                Projectile.spriteDirection = direction.X > 0 ? -1 : 1; //纹理朝左，所以反转
            }
        }

        //===== 粒子效果方法 =====

        private void SpawnIdleParticle() {
            Dust idle = Dust.NewDustDirect(
                Projectile.position,
                Projectile.width,
                Projectile.height,
                DustID.Blood,
                Scale: Main.rand.NextFloat(0.8f, 1.2f)
            );
            idle.velocity = Main.rand.NextVector2Circular(1f, 1f);
            idle.noGravity = true;
            idle.alpha = 150;
        }

        private void SpawnFollowParticle() {
            Dust follow = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.Blood,
                -Projectile.velocity * Main.rand.NextFloat(0.1f, 0.3f),
                100,
                default,
                Main.rand.NextFloat(1f, 1.5f)
            );
            follow.noGravity = true;
        }

        private void SpawnAttackChargeEffect() {
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 5f);

                Dust charge = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Blood,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.2f)
                );
                charge.noGravity = true;
            }
        }

        private void SpawnChargeParticle() {
            Vector2 toTarget = (attackTarget - Projectile.Center).SafeNormalize(Vector2.Zero);
            Dust charge = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                DustID.Blood,
                toTarget * Main.rand.NextFloat(1f, 3f),
                100,
                default,
                Main.rand.NextFloat(1.2f, 1.8f)
            );
            charge.noGravity = true;
        }

        private void SpawnRushParticle() {
            Dust rush = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.Blood,
                -Projectile.velocity * Main.rand.NextFloat(0.2f, 0.4f),
                100,
                default,
                Main.rand.NextFloat(1.5f, 2.5f)
            );
            rush.noGravity = true;
        }

        private void DespawnEffect() {
            //消散特效
            for (int i = 0; i < 20; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);

                Dust despawn = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Blood,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                despawn.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.NPCDeath1 with {
                Volume = 0.5f,
                Pitch = -0.4f
            }, Projectile.Center);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中血肉粒子
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);

                Dust hitBlood = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Blood,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                hitBlood.noGravity = true;
            }

            //击中音效
            SoundEngine.PlaySound(SoundID.NPCHit13 with {
                Volume = 0.5f,
                Pitch = 0f
            }, Projectile.Center);

            //击中后的挤压效果
            squashStretch = 0.7f;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D hungryTex = TextureAssets.Npc[NPCID.TheHungryII].Value;

            int frameHeight = hungryTex.Height / 6;
            Rectangle sourceRect = new Rectangle(0, frameHeight * currentFrame, hungryTex.Width, frameHeight);
            Vector2 origin = sourceRect.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float alpha = (255f - Projectile.alpha) / 255f;

            //===== 绘制运动残影 =====
            if (State == HungryState.Attacking && StateTimer >= 20 && StateTimer < 40) {
                DrawAttackTrail(sb, hungryTex, sourceRect, origin, lightColor, alpha);
            }

            //===== 绘制外层血肉辉光 =====
            if (SoftGlow?.Value != null) {
                Texture2D glow = SoftGlow.Value;
                float glowScale = Projectile.scale * 1.2f * squashStretch;
                float glowAlpha = alpha * 0.4f;

                sb.Draw(
                    glow,
                    drawPos,
                    null,
                    new Color(150, 50, 50, 0) * glowAlpha,
                    Projectile.rotation,
                    glow.Size() / 2f,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );
            }

            //===== 绘制主体恶鬼 =====
            SpriteEffects effects = Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //应用挤压拉伸
            Vector2 scale = new Vector2(
                Projectile.scale * squashStretch,
                Projectile.scale / squashStretch
            );

            //暗化处理（生物感）
            Color drawColor = Color.Lerp(lightColor, new Color(200, 150, 150), 0.3f) * alpha;

            sb.Draw(
                hungryTex,
                drawPos,
                sourceRect,
                drawColor,
                Projectile.rotation,
                origin,
                scale,
                effects,
                0
            );

            //蓄力阶段的发光效果
            if (State == HungryState.Attacking && StateTimer < 20) {
                float chargeIntensity = StateTimer / 20f;
                Color chargeGlow = new Color(255, 100, 100) * (alpha * chargeIntensity * 0.6f);

                sb.Draw(
                    hungryTex,
                    drawPos,
                    sourceRect,
                    chargeGlow,
                    Projectile.rotation,
                    origin,
                    scale * (1f + chargeIntensity * 0.2f),
                    effects,
                    0
                );
            }

            return false;
        }

        private void DrawAttackTrail(SpriteBatch sb, Texture2D hungryTex, Rectangle sourceRect,
            Vector2 origin, Color lightColor, float alpha) {

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float trailProgress = 1f - i / (float)Projectile.oldPos.Length;
                float trailAlpha = trailProgress * alpha * 0.5f;

                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = Color.Lerp(
                    new Color(150, 50, 50),
                    new Color(200, 100, 100),
                    trailProgress
                ) * trailAlpha;

                SpriteEffects effects = Projectile.spriteDirection > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

                sb.Draw(
                    hungryTex,
                    trailPos,
                    sourceRect,
                    trailColor,
                    Projectile.rotation,
                    origin,
                    Projectile.scale * 0.9f,
                    effects,
                    0
                );
            }
        }
    }
}