using InnoVault.GameContent.BaseEntity;
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
    internal class FishObsidian : FishSkill
    {
        public override int UnlockFishID => ItemID.Obsidifish;
        public override int DefaultCooldown => 120 - HalibutData.GetDomainLayer() * 5;
        public override int ResearchDuration => 60 * 18;

        //黑曜石鱼管理系统
        private static readonly List<int> ActiveObsidianFish = new();
        private static int MaxObsidianFish => 5 + HalibutData.GetDomainLayer() / 2; //最多5-9条鱼

        //检测玩家受伤
        private int lastPlayerHitCount = 0;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {

            if (Cooldown <= 0) {
                SetCooldown();
                CleanupInactiveFish();

                if (ActiveObsidianFish.Count < MaxObsidianFish) {
                    //生成新的黑曜石鱼
                    int fishProj = Projectile.NewProjectile(
                        source,
                        player.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<ObsidianFishOrbit>(),
                        (int)(damage * (1.8f + HalibutData.GetDomainLayer() * 0.6f)),
                        knockback * 0.3f,
                        player.whoAmI,
                        ai0: ActiveObsidianFish.Count //传递索引用于环形排列
                    );

                    if (fishProj >= 0 && fishProj < Main.maxProjectiles) {
                        ActiveObsidianFish.Add(fishProj);
                        SpawnSummonEffect(player.Center);

                        //黑曜石形成音效
                        SoundEngine.PlaySound(SoundID.Item30 with {
                            Volume = 0.5f,
                            Pitch = -0.3f + ActiveObsidianFish.Count * 0.05f
                        }, player.Center);
                    }
                }
            }

            return null;
        }

        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            //检测玩家受伤
            int currentHitCount = player.CountProjectilesOfID<Content.Projectiles.Others.Hit>();
            
            if (currentHitCount > lastPlayerHitCount && ActiveObsidianFish.Count > 0) {
                //玩家受伤，打碎一条黑曜石鱼
                ShatterOneFish(player);
            }
            
            lastPlayerHitCount = currentHitCount;
            return true;
        }

        //打碎一条黑曜石鱼
        private void ShatterOneFish(Player player) {
            CleanupInactiveFish();
            
            if (ActiveObsidianFish.Count > 0) {
                //优先打碎最后一条（最新生成的）
                int fishID = ActiveObsidianFish[ActiveObsidianFish.Count - 1];
                
                if (fishID >= 0 && fishID < Main.maxProjectiles && Main.projectile[fishID].active) {
                    Projectile fish = Main.projectile[fishID];
                    if (fish.ModProjectile is ObsidianFishOrbit obsidianFish) {
                        obsidianFish.Shatter();
                    }
                }
                
                ActiveObsidianFish.RemoveAt(ActiveObsidianFish.Count - 1);
            }
        }

        private static void CleanupInactiveFish() {
            ActiveObsidianFish.RemoveAll(id => {
                if (id < 0 || id >= Main.maxProjectiles) return true;
                Projectile proj = Main.projectile[id];
                return !proj.active || proj.type != ModContent.ProjectileType<ObsidianFishOrbit>();
            });
        }

        private void SpawnSummonEffect(Vector2 position) {
            //黑曜石碎片生成效果
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 6f);

                Dust obsidian = Dust.NewDustPerfect(
                    position,
                    DustID.Obsidian,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                obsidian.noGravity = true;
                obsidian.fadeIn = 1.3f;
            }

            //暗色烟雾
            for (int i = 0; i < 10; i++) {
                Dust smoke = Dust.NewDustDirect(
                    position - new Vector2(15),
                    30, 30,
                    DustID.Smoke,
                    Scale: Main.rand.NextFloat(1.5f, 2.5f)
                );
                smoke.velocity = Main.rand.NextVector2Circular(3f, 3f);
                smoke.noGravity = true;
                smoke.color = new Color(50, 50, 50);
            }
        }
    }

    /// <summary>
    /// 黑曜石鱼弹幕，环绕玩家旋转保护
    /// </summary>
    internal class ObsidianFishOrbit : BaseHeldProj
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Obsidifish;

        //状态
        private enum FishState
        {
            Gathering,   //聚集阶段
            Orbiting,    //环绕阶段
            Shattering   //破碎阶段
        }

        private FishState State {
            get => (FishState)Projectile.ai[1];
            set => Projectile.ai[1] = (float)value;
        }

        private ref float FishIndex => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[2];

        //环绕参数
        private float orbitRadius = 150f; //环绕半径
        private float orbitAngle = 0f; //当前环绕角度
        private float baseOrbitSpeed = 0.015f; //基础环绕速度（大幅降低）
        private float orbitSpeedMultiplier = 1f; //速度倍数
        
        //层次参数 - 创造立体感
        private float layerDepth = 0f; //深度层次（-1到1）
        private float layerPhase = 0f; //层次相位

        //摆动参数
        private float swimPhase = 0f; //游动相位
        private float swimAmplitude = 8f; //游动幅度
        private float swimFrequency = 0.08f; //游动频率

        //视觉效果
        private float glowIntensity = 0f;
        private float bodyRotation = 0f; //鱼体旋转（独立于环绕角度）
        private float targetBodyRotation = 0f; //目标鱼体旋转
        private Vector2 shatterVelocity = Vector2.Zero;
        private float scaleMultiplier = 1f; //尺寸倍数（用于深度感）

        private const int GatherDuration = 20;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 132;
            Projectile.height = 132;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10086;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.6f;
            }
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (!FishSkill.GetT<FishObsidian>().Active(Owner) && State != FishState.Shattering) {
                Projectile.Kill();
                return;
            }

            StateTimer++;

            //状态机
            switch (State) {
                case FishState.Gathering:
                    GatheringPhaseAI(Owner);
                    break;

                case FishState.Orbiting:
                    OrbitingPhaseAI(Owner);
                    break;

                case FishState.Shattering:
                    ShatteringPhaseAI();
                    break;
            }

            //平滑更新鱼体旋转
            bodyRotation = targetBodyRotation;

            //暗红色照明（黑曜石的熔岩色泽）
            float lightIntensity = glowIntensity * 0.5f;
            Lighting.AddLight(Projectile.Center,
                0.8f * lightIntensity,
                0.2f * lightIntensity,
                0.1f * lightIntensity);
        }

        //聚集阶段
        private void GatheringPhaseAI(Player owner) {
            float progress = StateTimer / GatherDuration;

            //计算目标环绕位置
            int totalFish = GetTotalActiveFish();
            float targetAngle = MathHelper.TwoPi * FishIndex / Math.Max(totalFish, 1);
            
            //初始化层次深度（基于索引）
            if (StateTimer == 1) {
                layerDepth = (float)Math.Sin(targetAngle) * 0.6f; //基于角度的深度
                layerPhase = Main.rand.NextFloat(MathHelper.TwoPi); //随机相位
                swimPhase = Main.rand.NextFloat(MathHelper.TwoPi); //随机游动相位
            }

            Vector2 targetPos = owner.Center + targetAngle.ToRotationVector2() * orbitRadius;

            //快速飞向目标位置
            float easeProgress = CWRUtils.EaseOutCubic(progress);
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, easeProgress * 0.5f);

            orbitAngle = targetAngle;
            glowIntensity = MathHelper.Lerp(0f, 0.8f, progress);
            
            //计算初始朝向
            Vector2 toTarget = targetPos - Projectile.Center;
            if (toTarget.LengthSquared() > 1f) {
                targetBodyRotation = toTarget.ToRotation();
            }

            //聚集粒子
            if (Main.rand.NextBool(3)) {
                SpawnGatherParticle();
            }

            //转入环绕阶段
            if (StateTimer >= GatherDuration) {
                State = FishState.Orbiting;
                StateTimer = 0;

                SoundEngine.PlaySound(SoundID.Item27 with {
                    Volume = 0.3f,
                    Pitch = 0.2f
                }, Projectile.Center);
            }
        }

        //环绕阶段
        private void OrbitingPhaseAI(Player owner) {
            //动态调整环绕速度（领域层数影响 + 缓动效果）
            float targetSpeed = baseOrbitSpeed * (1f + HalibutData.GetDomainLayer() * 0.15f);
            orbitSpeedMultiplier = MathHelper.Lerp(orbitSpeedMultiplier, 1f, 0.02f);
            float currentSpeed = targetSpeed * orbitSpeedMultiplier;

            //更新环绕角度
            orbitAngle += currentSpeed;
            if (orbitAngle > MathHelper.TwoPi) orbitAngle -= MathHelper.TwoPi;

            //重新计算所有鱼的索引分布
            int totalFish = GetTotalActiveFish();
            int myRealIndex = GetMyRealIndex();
            
            //计算理想角度（均匀分布）
            float idealAngle = MathHelper.TwoPi * myRealIndex / Math.Max(totalFish, 1) + orbitAngle;
            
            //平滑调整到理想角度
            float angleDiff = MathHelper.WrapAngle(idealAngle - orbitAngle);
            orbitAngle += angleDiff * 0.05f;

            //更新层次深度动画
            layerPhase += 0.02f;
            layerDepth = (float)Math.Sin(orbitAngle * 2f + layerPhase) * 0.5f;
            
            //深度影响半径和尺寸
            float depthScale = 0.85f + layerDepth * 0.3f; //后方的鱼更小
            float currentRadius = orbitRadius * depthScale;
            scaleMultiplier = 0.8f + depthScale * 0.4f;

            //游动摆动效果
            swimPhase += swimFrequency;
            Vector2 swimOffset = new Vector2(
                (float)Math.Sin(swimPhase) * swimAmplitude * 0.5f,
                (float)Math.Cos(swimPhase * 1.3f) * swimAmplitude
            );

            //额外的波浪起伏
            float waveOffset = (float)Math.Sin(StateTimer * 0.05f + myRealIndex) * 15f;
            
            //计算环绕位置
            Vector2 orbitOffset = orbitAngle.ToRotationVector2() * currentRadius;
            Vector2 targetPos = owner.Center + orbitOffset + swimOffset;
            targetPos.Y += waveOffset;

            //平滑跟随
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.2f);

            //计算鱼的朝向 - 沿着运动方向
            Vector2 velocity = targetPos - Projectile.Center;
            if (velocity.LengthSquared() > 0.5f) {
                float targetAngle = velocity.ToRotation();
                
                //添加轻微的前后摆动
                float swayAngle = (float)Math.Sin(swimPhase * 2f) * 0.15f;
                targetBodyRotation = targetAngle + swayAngle;
            }

            //辉光强度脉冲
            glowIntensity = 0.7f + (float)Math.Sin(StateTimer * 0.15f) * 0.2f;
            
            //深度影响辉光（后方的鱼稍暗）
            glowIntensity *= (0.8f + layerDepth * 0.4f);

            //环绕粒子（降低频率）
            if (Main.rand.NextBool(15)) {
                SpawnOrbitParticle();
            }

            //拖尾粒子（游动时）
            if (Main.rand.NextBool(20) && velocity.LengthSquared() > 1f) {
                SpawnSwimTrail(velocity);
            }
        }

        //破碎阶段
        private void ShatteringPhaseAI() {
            //应用破碎速度
            Projectile.velocity = shatterVelocity;
            shatterVelocity *= 0.95f;

            //快速淡出
            Projectile.alpha += 15;
            if (Projectile.alpha >= 255) {
                Projectile.Kill();
            }

            glowIntensity *= 0.9f;
            bodyRotation += 0.2f;
            scaleMultiplier *= 0.98f;

            //破碎粒子
            if (Main.rand.NextBool(2)) {
                SpawnShatterParticle();
            }
        }

        //触发破碎
        public void Shatter() {
            if (State == FishState.Shattering) return;

            State = FishState.Shattering;
            StateTimer = 0;
            Projectile.tileCollide = false;
            Projectile.friendly = false;

            //随机破碎方向
            shatterVelocity = Main.rand.NextVector2Circular(12f, 12f);

            //破碎特效
            SpawnShatterEffect();

            //破碎音效
            SoundEngine.PlaySound(SoundID.Item27 with {
                Volume = 0.6f,
                Pitch = -0.3f
            }, Projectile.Center);

            SoundEngine.PlaySound(SoundID.Shatter with {
                Volume = 0.5f
            }, Projectile.Center);
        }

        //获取当前活跃的黑曜石鱼总数
        private int GetTotalActiveFish() {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == Projectile.type && proj.owner == Projectile.owner) {
                    count++;
                }
            }
            return count;
        }

        //获取自己的实际索引
        private int GetMyRealIndex() {
            int index = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == Projectile.type && proj.owner == Projectile.owner) {
                    if (proj.whoAmI == Projectile.whoAmI) {
                        return index;
                    }
                    index++;
                }
            }
            return 0;
        }

        //粒子效果
        private void SpawnGatherParticle() {
            Dust gather = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                DustID.Obsidian,
                (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f),
                100,
                default,
                Main.rand.NextFloat(1f, 1.5f)
            );
            gather.noGravity = true;
            gather.fadeIn = 1.1f;
        }

        private void SpawnOrbitParticle() {
            Dust orbit = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.Obsidian,
                Main.rand.NextVector2Circular(0.5f, 0.5f),
                100,
                default,
                Main.rand.NextFloat(0.6f, 1f)
            );
            orbit.noGravity = true;
            orbit.fadeIn = 0.8f;
            orbit.alpha = 100;
        }

        private void SpawnSwimTrail(Vector2 velocity) {
            //游动拖尾粒子
            Vector2 trailDir = -velocity.SafeNormalize(Vector2.Zero);
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + trailDir * 15f,
                DustID.Smoke,
                trailDir * Main.rand.NextFloat(0.5f, 1.5f),
                100,
                new Color(80, 40, 40),
                Main.rand.NextFloat(0.8f, 1.3f)
            );
            trail.noGravity = true;
            trail.fadeIn = 0.9f;
            trail.alpha = 150;
        }

        private void SpawnShatterParticle() {
            Dust shatter = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                DustID.Obsidian,
                Main.rand.NextVector2Circular(8f, 8f),
                100,
                default,
                Main.rand.NextFloat(1.2f, 2f)
            );
            shatter.noGravity = true;
            shatter.fadeIn = 1.2f;
        }

        private void SpawnShatterEffect() {
            //爆炸式碎片
            for (int i = 0; i < 30; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 15f);

                Dust shatter = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Obsidian,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                shatter.noGravity = Main.rand.NextBool();
                shatter.fadeIn = 1.3f;
            }

            //黑色烟雾
            for (int i = 0; i < 15; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(6f, 6f);
                Dust smoke = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Smoke,
                    velocity,
                    100,
                    new Color(50, 50, 50),
                    Main.rand.NextFloat(2f, 3f)
                );
                smoke.noGravity = true;
            }

            //环形冲击波
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 velocity = angle.ToRotationVector2() * 6f;

                Dust wave = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Obsidian,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.8f, 2.5f)
                );
                wave.noGravity = true;
                wave.fadeIn = 1.4f;
            }
        }

        private void SpawnReflectEffect(Vector2 position) {
            //反弹冲击波
            for (int i = 0; i < 10; i++) {
                float angle = MathHelper.TwoPi * i / 10f;
                Vector2 velocity = angle.ToRotationVector2() * 3f;

                Dust reflect = Dust.NewDustPerfect(
                    position,
                    DustID.Obsidian,
                    velocity,
                    100,
                    default,
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                reflect.noGravity = true;
                reflect.fadeIn = 1.1f;
            }

            SoundEngine.PlaySound(SoundID.Item27 with {
                Volume = 0.3f,
                Pitch = 0.5f
            }, position);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //反弹效果
            SpawnReflectEffect(target.Center);
        }

        public override void OnKill(int timeLeft) {
            //消散效果
            if (State != FishState.Shattering) {
                SpawnShatterEffect();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D fishTex = TextureAssets.Item[ItemID.Obsidifish].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = fishTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;

            Color baseColor = lightColor;
            float alpha = (255f - Projectile.alpha) / 255f;
            
            //应用深度层次到颜色（后方的鱼稍暗）
            float depthDarken = 0.7f + layerDepth * 0.3f;
            baseColor = Color.Lerp(baseColor, Color.Black, (1f - depthDarken) * 0.3f);

            //绘制拖尾
            if (State == FishState.Orbiting || State == FishState.Shattering) {
                DrawFishAfterimages(sb, fishTex, sourceRect, origin, baseColor, alpha);
            }

            //发光层（熔岩橙红色）
            if (glowIntensity > 0.5f) {
                Color glowColor = new Color(255, 120, 60); //柔和的熔岩色
                float glowAlpha = (glowIntensity - 0.5f) * 2f * alpha * 0.4f;

                sb.Draw(
                    fishTex,
                    drawPos,
                    sourceRect,
                    glowColor * glowAlpha,
                    bodyRotation + MathHelper.PiOver4,
                    origin,
                    Projectile.scale * scaleMultiplier * 1.15f,
                    SpriteEffects.None,
                    0
                );
            }

            //主体绘制
            sb.Draw(
                fishTex,
                drawPos,
                sourceRect,
                baseColor * alpha,
                bodyRotation + MathHelper.PiOver4,
                origin,
                Projectile.scale * scaleMultiplier,
                SpriteEffects.None,
                0
            );

            //额外高光（增强立体感）
            if (State == FishState.Orbiting) {
                Color highlightColor = new Color(255, 180, 100, 0);
                float highlightAlpha = glowIntensity * 0.3f * alpha;

                sb.Draw(
                    fishTex,
                    drawPos,
                    sourceRect,
                    highlightColor * highlightAlpha,
                    bodyRotation,
                    origin,
                    Projectile.scale * scaleMultiplier * 1.05f,
                    SpriteEffects.None,
                    0
                );
            }

            return false;
        }

        private void DrawFishAfterimages(SpriteBatch sb, Texture2D fishTex, Rectangle sourceRect,
            Vector2 origin, Color baseColor, float alpha) {

            int afterimageCount = State == FishState.Shattering ? 10 : 6;

            for (int i = 1; i < afterimageCount; i++) {
                if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) continue;

                float afterimageProgress = 1f - i / (float)afterimageCount;
                float afterimageAlpha = afterimageProgress * alpha * 0.4f;

                Color afterimageColor = Color.Lerp(
                    new Color(60, 30, 20),
                    new Color(180, 80, 40),
                    afterimageProgress
                ) * afterimageAlpha;

                Vector2 afterimagePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float afterimageScale = Projectile.scale * scaleMultiplier * MathHelper.Lerp(0.75f, 0.95f, afterimageProgress);
                
                //拖尾旋转插值
                float afterimageRotation = MathHelper.Lerp(bodyRotation, bodyRotation - 0.3f, i / (float)afterimageCount) + MathHelper.PiOver4;

                sb.Draw(
                    fishTex,
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
    }
}
