using Microsoft.Xna.Framework.Graphics;
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
    internal class FishofCthulu : FishSkill
    {
        public override int UnlockFishID => ItemID.TheFishofCthulu;
        public override int DefaultCooldown => 180 - HalibutData.GetDomainLayer() * 12;
        public override int ResearchDuration => 60 * 25;

        /// <summary>每次射击生成的眼球数量</summary>
        private int EyesPerShot => 1 + HalibutData.GetDomainLayer() / 3; // 1-4个眼球

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, 
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            
            //检查技能是否在冷却中
            if (Cooldown > 0) {
                return null;
            }

            //生成多个眼球
            for (int i = 0; i < EyesPerShot; i++) {
                //计算随机偏移角度
                float angleOffset = MathHelper.Lerp(-0.4f, 0.4f, i / (float)Math.Max(1, EyesPerShot - 1));
                Vector2 eyeVelocity = velocity.RotatedBy(angleOffset) * Main.rand.NextFloat(0.9f, 1.1f);

                //生成眼球
                int proj = Projectile.NewProjectile(
                    source,
                    position + Main.rand.NextVector2Circular(30f, 30f),
                    eyeVelocity,
                    ModContent.ProjectileType<CthulhuEye>(),
                    (int)(damage * (1.8f + HalibutData.GetDomainLayer() * 0.4f)),
                    knockback * 0.6f,
                    player.whoAmI,
                    ai0: i //用于区分不同眼球
                );
            }

            //播放克苏鲁之眼召唤音效
            SoundEngine.PlaySound(SoundID.NPCHit1 with { 
                Volume = 0.7f, 
                Pitch = -0.5f,
                MaxInstances = 3
            }, position);

            //召唤特效
            SpawnSummonEffect(position);

            SetCooldown();

            return null;
        }

        private void SpawnSummonEffect(Vector2 position) {
            //召唤时的暗红色能量粒子
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 7f);

                Dust eye = Dust.NewDustPerfect(
                    position,
                    DustID.Blood,
                    velocity,
                    100,
                    new Color(200, 50, 50),
                    Main.rand.NextFloat(1.5f, 2.2f)
                );
                eye.noGravity = true;
                eye.fadeIn = 1.3f;
            }

            //额外的暗影粒子
            for (int i = 0; i < 12; i++) {
                Dust shadow = Dust.NewDustPerfect(
                    position + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Shadowflame,
                    Main.rand.NextVector2Circular(4f, 4f),
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                shadow.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 克苏鲁之眼弹幕，具有追踪、冲刺和环绕能力
    /// </summary>
    internal class CthulhuEye : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.EyeofCthulhu;

        private ref float EyeID => ref Projectile.ai[0];
        private ref float AIState => ref Projectile.ai[1];
        private ref float AITimer => ref Projectile.localAI[0];
        private ref float DashCooldown => ref Projectile.localAI[1];

        //追踪目标
        private int targetNPC = -1;
        private float homingStrength = 0f;

        //环绕参数
        private float orbitAngle = 0f;
        private float orbitRadius = 0f;
        private bool isOrbiting = false;

        //冲刺参数
        private bool isDashing = false;
        private Vector2 dashDirection = Vector2.Zero;
        private int dashTimer = 0;

        //状态枚举
        private enum EyeState
        {
            Seeking,      //寻找目标
            Orbiting,     //环绕目标
            Dashing,      //冲刺攻击
            Returning,    //返回环绕
            Idle          //待机
        }

        //眼球瞳孔旋转
        private float pupilRotation = 0f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            Main.projFrames[Projectile.type] = 4; // 使用4帧动画
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 8; //可穿透8个敌人
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;

            //初始化环绕参数
            orbitAngle = EyeID * MathHelper.TwoPi / 4f; // 错开初始角度
            orbitRadius = 120f + Main.rand.NextFloat(-20f, 20f);
        }

        public override void AI() {
            AITimer++;
            DashCooldown--;

            //状态机
            EyeState currentState = (EyeState)AIState;
            switch (currentState) {
                case EyeState.Seeking:
                    SeekingAI();
                    break;
                case EyeState.Orbiting:
                    OrbitingAI();
                    break;
                case EyeState.Dashing:
                    DashingAI();
                    break;
                case EyeState.Returning:
                    ReturningAI();
                    break;
                case EyeState.Idle:
                    IdleAI();
                    break;
            }

            //更新瞳孔朝向
            UpdatePupilRotation();

            //帧动画
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 3);

            //生成粒子效果
            if (Main.rand.NextBool(4)) {
                SpawnTrailParticles();
            }

            //淡出效果
            if (Projectile.timeLeft < 30) {
                Projectile.alpha = (int)((1f - Projectile.timeLeft / 30f) * 255);
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        private void SeekingAI() {
            //寻找目标阶段
            if (targetNPC == -1 || !Main.npc[targetNPC].active) {
                var npc = Projectile.Center.FindClosestNPC(1000f);
                if (npc != null) {
                    targetNPC = npc.whoAmI;
                }
            }

            if (targetNPC != -1) {
                //找到目标，进入环绕状态
                AIState = (float)EyeState.Orbiting;
                AITimer = 0;
                homingStrength = 0.03f;
                isOrbiting = true;

                //播放锁定音效
                SoundEngine.PlaySound(SoundID.NPCHit1 with { 
                    Volume = 0.4f, 
                    Pitch = 0.3f 
                }, Projectile.Center);
            }
            else {
                //没有目标时缓慢移动
                Projectile.velocity *= 0.98f;
            }
        }

        private void OrbitingAI() {
            //环绕目标阶段
            if (targetNPC < 0 || !Main.npc[targetNPC].active) {
                //目标丢失，返回寻找状态
                AIState = (float)EyeState.Seeking;
                targetNPC = -1;
                isOrbiting = false;
                return;
            }

            NPC target = Main.npc[targetNPC];

            //环绕角度递增
            orbitAngle += 0.08f + HalibutData.GetDomainLayer() * 0.01f;

            //计算环绕位置
            Vector2 idealPosition = target.Center + orbitAngle.ToRotationVector2() * orbitRadius;

            //平滑移动到环绕位置
            Vector2 toIdeal = idealPosition - Projectile.Center;
            float distance = toIdeal.Length();

            if (distance > 20f) {
                Projectile.velocity = Vector2.Lerp(
                    Projectile.velocity,
                    toIdeal.SafeNormalize(Vector2.Zero) * Math.Min(distance * 0.15f, 18f),
                    0.15f
                );
            }

            //判断是否可以发起冲刺
            if (DashCooldown <= 0 && AITimer > 40) {
                float distanceToTarget = Vector2.Distance(Projectile.Center, target.Center);
                
                //距离合适且有视线时冲刺
                if (distanceToTarget > 80f && distanceToTarget < 400f) {
                    AIState = (float)EyeState.Dashing;
                    AITimer = 0;
                    isDashing = true;
                    dashTimer = 25; // 冲刺持续时间

                    //计算冲刺方向（预判目标移动）
                    Vector2 predictedPos = target.Center + target.velocity * 15f;
                    dashDirection = (predictedPos - Projectile.Center).SafeNormalize(Vector2.Zero);

                    //播放冲刺音效
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { 
                        Volume = 0.6f, 
                        Pitch = 0.5f 
                    }, Projectile.Center);

                    //重置冷却
                    DashCooldown = 80 - HalibutData.GetDomainLayer() * 5;
                }
            }
        }

        private void DashingAI() {
            //冲刺攻击阶段
            dashTimer--;

            if (dashTimer > 0) {
                //高速冲刺
                float dashSpeed = 28f + HalibutData.GetDomainLayer() * 2f;
                Projectile.velocity = Vector2.Lerp(
                    Projectile.velocity,
                    dashDirection * dashSpeed,
                    0.25f
                );

                //冲刺粒子特效
                if (Main.rand.NextBool(2)) {
                    SpawnDashParticles();
                }
            }
            else {
                //冲刺结束，返回环绕
                AIState = (float)EyeState.Returning;
                AITimer = 0;
                isDashing = false;
            }
        }

        private void ReturningAI() {
            //返回环绕状态
            if (targetNPC < 0 || !Main.npc[targetNPC].active) {
                AIState = (float)EyeState.Seeking;
                targetNPC = -1;
                return;
            }

            NPC target = Main.npc[targetNPC];

            //减速
            Projectile.velocity *= 0.92f;

            //距离目标较近时重新进入环绕
            float distanceToTarget = Vector2.Distance(Projectile.Center, target.Center);
            if (distanceToTarget < orbitRadius * 1.5f && Projectile.velocity.Length() < 10f) {
                AIState = (float)EyeState.Orbiting;
                AITimer = 0;
                isOrbiting = true;
            }
        }

        private void IdleAI() {
            //待机状态
            Projectile.velocity *= 0.95f;

            if (AITimer > 60) {
                AIState = (float)EyeState.Seeking;
                AITimer = 0;
            }
        }

        private void UpdatePupilRotation() {
            //瞳孔朝向最近的敌人或鼠标
            if (targetNPC >= 0 && Main.npc[targetNPC].active) {
                pupilRotation = (Main.npc[targetNPC].Center - Projectile.Center).ToRotation();
            }
            else {
                pupilRotation = (Main.MouseWorld - Projectile.Center).ToRotation();
            }
        }

        private void SpawnTrailParticles() {
            //轨迹粒子
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.Blood,
                -Projectile.velocity * Main.rand.NextFloat(0.2f, 0.4f),
                100,
                new Color(200, 50, 50),
                Main.rand.NextFloat(1f, 1.5f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1f;
        }

        private void SpawnDashParticles() {
            //冲刺粒子特效
            for (int i = 0; i < 2; i++) {
                Dust dash = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(15f, 15f),
                    DustID.Shadowflame,
                    -Projectile.velocity * Main.rand.NextFloat(0.3f, 0.6f),
                    100,
                    default,
                    Main.rand.NextFloat(1.3f, 2f)
                );
                dash.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中效果
            SoundEngine.PlaySound(SoundID.NPCHit1 with { 
                Pitch = 0.2f 
            }, Projectile.Center);

            //血液粒子
            for (int i = 0; i < 10; i++) {
                Vector2 particleVel = Main.rand.NextVector2Circular(5f, 5f);
                Dust blood = Dust.NewDustDirect(
                    target.position,
                    target.width,
                    target.height,
                    DustID.Blood,
                    particleVel.X,
                    particleVel.Y,
                    100,
                    default,
                    1.8f
                );
                blood.noGravity = true;
            }

            //暗影粒子
            for (int i = 0; i < 6; i++) {
                Dust shadow = Dust.NewDustDirect(
                    target.position,
                    target.width,
                    target.height,
                    DustID.Shadowflame,
                    0, -2f,
                    100,
                    default,
                    1.5f
                );
                shadow.noGravity = true;
            }

            //冲刺击中时造成更高伤害
            if (isDashing && hit.Crit) {
                //额外伤害已在基础伤害中体现
                target.AddBuff(BuffID.ShadowFlame, 180);
            }

            //击中后继续返回环绕
            if (isDashing) {
                AIState = (float)EyeState.Returning;
                isDashing = false;
                dashTimer = 0;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //加载眼球纹理
            Main.instance.LoadNPC(NPCID.EyeofCthulhu);
            Texture2D texture = TextureAssets.Npc[NPCID.EyeofCthulhu].Value;

            //计算纹理参数
            int frameHeight = texture.Height / Main.npcFrameCount[NPCID.EyeofCthulhu];
            Rectangle sourceRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = sourceRect.Size() / 2f;

            //绘制残影轨迹（冲刺时更明显）
            int trailLength = isDashing ? Projectile.oldPos.Length : Projectile.oldPos.Length / 2;
            for (int i = 1; i < trailLength; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float trailProgress = 1f - i / (float)trailLength;
                float trailAlpha = trailProgress * 0.5f * (1f - Projectile.alpha / 255f);

                if (isDashing) trailAlpha *= 1.5f; // 冲刺时更亮

                Color trailColor = new Color(200, 50, 50) * trailAlpha;

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float rotation = Projectile.oldRot[i];

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    sourceRect,
                    trailColor,
                    rotation,
                    origin,
                    Projectile.scale * 0.6f * (0.8f + trailProgress * 0.2f),
                    SpriteEffects.None,
                    0
                );
            }

            //绘制主体眼球
            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            Color mainColor = lightColor * (1f - Projectile.alpha / 255f);

            Main.EntitySpriteDraw(
                texture,
                mainDrawPos,
                sourceRect,
                mainColor,
                Projectile.rotation,
                origin,
                Projectile.scale * 0.6f,
                SpriteEffects.None,
                0
            );

            //发光效果
            if (isDashing || isOrbiting) {
                Color glowColor = new Color(255, 100, 100, 0) * 0.4f * (1f - Projectile.alpha / 255f);
                
                if (isDashing) {
                    glowColor *= 1.5f; // 冲刺时更亮
                }

                Main.EntitySpriteDraw(
                    texture,
                    mainDrawPos,
                    sourceRect,
                    glowColor,
                    Projectile.rotation,
                    origin,
                    Projectile.scale * 0.7f,
                    SpriteEffects.None,
                    0
                );
            }

            //绘制瞳孔（简化版）
            DrawPupil(mainDrawPos, origin);

            return false;
        }

        private void DrawPupil(Vector2 drawPos, Vector2 origin) {
            //使用简单的圆形表示瞳孔
            Texture2D pupilTex = TextureAssets.Extra[ExtrasID.SharpTears].Value;
            
            //瞳孔偏移（朝向目标）
            Vector2 pupilOffset = pupilRotation.ToRotationVector2() * 8f;

            Color pupilColor = new Color(50, 10, 10) * (1f - Projectile.alpha / 255f);

            Main.EntitySpriteDraw(
                pupilTex,
                drawPos + pupilOffset,
                null,
                pupilColor,
                0f,
                pupilTex.Size() / 2f,
                0.3f,
                SpriteEffects.None,
                0
            );
        }

        public override Color? GetAlpha(Color lightColor) {
            return new Color(255, 200, 200, 200) * (1f - Projectile.alpha / 255f);
        }
    }
}
