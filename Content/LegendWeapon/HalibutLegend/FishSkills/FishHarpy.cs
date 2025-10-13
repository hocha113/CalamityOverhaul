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
    internal class FishHarpy : FishSkill
    {
        public override int UnlockFishID => ItemID.Harpyfish;
        public override int DefaultCooldown => 30;

        //羽毛管理系统
        public static List<int> ActiveFeathers = new();
        private const int MaxFeathers = 12; //最多12根羽毛
        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            
            if (Cooldown <= 0) {
                SetCooldown();
                //检查当前活跃的羽毛数量
                CleanupInactiveFeathers();

                if (ActiveFeathers.Count < MaxFeathers) {
                    //生成新的羽毛弹幕
                    int featherProj = Projectile.NewProjectile(
                        source,
                        player.Center,
                        Vector2.Zero, //初始速度为0
                        ModContent.ProjectileType<HarpyFeatherOrbit>(),
                        (int)(damage * 0.75f),
                        knockback * 0.6f,
                        player.whoAmI,
                        ai0: ActiveFeathers.Count //传递索引用于均匀分布
                    );

                    if (featherProj >= 0 && featherProj < Main.maxProjectiles) {
                        ActiveFeathers.Add(featherProj);

                        //生成召唤粒子
                        SpawnSummonEffect(player.Center);

                        //轻柔的风声音效
                        SoundEngine.PlaySound(SoundID.Item32 with {
                            Volume = 0.5f,
                            Pitch = 0.2f + ActiveFeathers.Count * 0.04f
                        }, player.Center);
                        
                        //检查是否达到最大值
                        if (ActiveFeathers.Count >= MaxFeathers) {
                            //通知所有羽毛准备发射
                            NotifyFeathersToLaunch(player);
                        }
                    }
                }
            }

            return null;
        }

        private static void CleanupInactiveFeathers() {
            List<int> list = [];
            foreach (var id in ActiveFeathers) {
                if (!id.TryGetProjectile(out var proj)) {
                    continue;
                }
                if (proj.type != ModContent.ProjectileType<HarpyFeatherOrbit>()) {
                    continue;
                }
                if (proj.ai[1] >= 4) {
                    continue;
                }
                list.Add(id);
            }
            ActiveFeathers = list;
        }

        private void NotifyFeathersToLaunch(Player player) {
            //播放特殊的蓄力完成音效
            SoundEngine.PlaySound(SoundID.Item30 with { 
                Volume = 0.7f, 
                Pitch = 0.6f 
            }, player.Center);
            
            SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with { 
                Volume = 0.5f, 
                Pitch = 0.8f 
            }, player.Center);
            
            //生成蓄力完成的视觉特效
            for (int i = 0; i < 30; i++) {
                float angle = MathHelper.TwoPi * i / 30f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);
                
                Dust charge = Dust.NewDustPerfect(
                    player.Center,
                    DustID.Cloud,
                    velocity,
                    100,
                    new Color(255, 255, 255),
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                charge.noGravity = true;
                charge.fadeIn = 1.3f;
            }
        }

        private void SpawnSummonEffect(Vector2 position) {
            //召唤时的羽毛粒子效果
            for (int i = 0; i < 18; i++) {
                float angle = MathHelper.TwoPi * i / 18f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(1.5f, 4f);
                
                //使用羽毛尘埃
                Dust feather = Dust.NewDustPerfect(
                    position,
                    DustID.Cloud,
                    velocity,
                    100,
                    new Color(240, 240, 255),
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                feather.noGravity = true;
                feather.fadeIn = 1.1f;
            }
            
            //额外的空气涟漪效果
            for (int i = 0; i < 6; i++) {
                Dust air = Dust.NewDustDirect(
                    position - new Vector2(15),
                    30, 30,
                    DustID.Cloud,
                    Scale: Main.rand.NextFloat(1.5f, 2.2f)
                );
                air.velocity = Main.rand.NextVector2Circular(2f, 2f);
                air.noGravity = true;
                air.alpha = 120;
            }
        }
    }

    /// <summary>
    /// 鸟妖羽毛弹幕 - 飘逸旋转后飞出
    /// </summary>
    internal class HarpyFeatherOrbit : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.HarpyFeather;
        
        //状态机
        private enum FeatherState {
            Gathering,    //聚集阶段：羽毛飘向玩家
            Floating,     //漂浮阶段：自然漂浮并均匀分布
            Orbiting,     //环绕阶段：优雅旋转
            Charging,     //蓄力阶段：所有羽毛到齐，准备同步发射
            Launching     //发射阶段：沿切线方向飞出
        }
        
        private FeatherState State {
            get => (FeatherState)Projectile.ai[1];
            set => Projectile.ai[1] = (float)value;
        }
        
        private ref float FeatherIndex => ref Projectile.ai[0];
        private ref float StateTimer => ref Projectile.ai[2];
        
        //环绕参数
        private float orbitRadius = 140f;
        private float targetOrbitAngle = 0f;
        private float currentOrbitAngle = 0f;
        private float orbitSpeed = 0.03f;
        private const float MaxOrbitSpeed = 0.15f;
        
        //漂浮参数
        private float floatPhase = 0f;
        private float floatAmplitude = 8f;
        private float floatFrequency = 0.08f;
        
        //阶段时长
        private const int GatherDuration = 25;      //聚集时间
        private const int FloatDuration = 35;       //漂浮时间
        private const int ChargeDuration = 30;      //蓄力时间
        private const float LaunchSpeed = 22f;      //发射速度
        
        //视觉效果
        private float glowIntensity = 0f;
        private float swayAngle = 0f; //羽毛摇摆角度
        
        //同步发射计时器
        private int launchCountdown = 0;
        private const int LaunchDelay = 20; //蓄力完成后20帧发射
        
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SoftGlow = null;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 600;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            
            //初始化随机漂浮相位
            floatPhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            
            StateTimer++;
            
            //检查是否应该进入蓄力阶段
            if (State == FeatherState.Orbiting) {
                CheckForChargingPhase(owner);
            }
            
            //状态机
            switch (State) {
                case FeatherState.Gathering:
                    GatheringPhaseAI(owner);
                    break;
                    
                case FeatherState.Floating:
                    FloatingPhaseAI(owner);
                    break;
                    
                case FeatherState.Orbiting:
                    OrbitingPhaseAI(owner);
                    break;
                    
                case FeatherState.Charging:
                    ChargingPhaseAI(owner);
                    break;
                    
                case FeatherState.Launching:
                    LaunchingPhaseAI();
                    break;
            }
            
            //羽毛摇摆效果
            if (State != FeatherState.Launching) {
                swayAngle = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + floatPhase) * 0.15f;
            }
            
            //轻柔的照明
            float lightIntensity = glowIntensity * 0.4f;
            Lighting.AddLight(Projectile.Center, 
                0.9f * lightIntensity, 
                0.9f * lightIntensity, 
                1.0f * lightIntensity);
        }

        /// <summary>
        /// 检查是否应该进入蓄力阶段
        /// </summary>
        private void CheckForChargingPhase(Player owner) {
            //计算当前羽毛总数
            int totalFeathers = FishHarpy.ActiveFeathers.Count;

            //如果达到12根且环绕时间超过一定阈值，进入蓄力阶段
            if (totalFeathers >= 12 && StateTimer >= 30) {
                State = FeatherState.Charging;
                StateTimer = 0;
                launchCountdown = LaunchDelay;
                
                //只由第一个羽毛播放蓄力音效（避免重复）
                if (Projectile.whoAmI == GetFirstFeatherID(owner)) {
                    SoundEngine.PlaySound(SoundID.Item30 with { 
                        Volume = 0.7f, 
                        Pitch = 0.6f 
                    }, owner.Center);
                    
                    SoundEngine.PlaySound(SoundID.DD2_WitherBeastAuraPulse with { 
                        Volume = 0.5f, 
                        Pitch = 0.8f 
                    }, owner.Center);
                    
                    //蓄力完成的视觉特效
                    for (int i = 0; i < 30; i++) {
                        float angle = MathHelper.TwoPi * i / 30f;
                        Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);
                        
                        Dust charge = Dust.NewDustPerfect(
                            owner.Center,
                            DustID.Cloud,
                            velocity,
                            100,
                            new Color(255, 255, 255),
                            Main.rand.NextFloat(1.5f, 2.5f)
                        );
                        charge.noGravity = true;
                        charge.fadeIn = 1.3f;
                    }
                }
            }
        }

        /// <summary>
        /// 获取第一个羽毛的ID（用于避免重复播放音效）
        /// </summary>
        private int GetFirstFeatherID(Player owner) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && 
                    Main.projectile[i].type == Projectile.type && 
                    Main.projectile[i].owner == owner.whoAmI) {
                    return Main.projectile[i].whoAmI;
                }
            }
            return Projectile.whoAmI;
        }

        /// <summary>
        /// 聚集阶段：羽毛轻柔飘向玩家
        /// </summary>
        private void GatheringPhaseAI(Player owner) {
            float progress = StateTimer / GatherDuration;
            
            //计算目标环绕角度（均匀分布）
            int totalFeathers = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && 
                    Main.projectile[i].type == Projectile.type && 
                    Main.projectile[i].owner == owner.whoAmI) {
                    totalFeathers++;
                }
            }
            
            targetOrbitAngle = MathHelper.TwoPi * FeatherIndex / Math.Max(totalFeathers, 1);
            Vector2 targetPos = owner.Center + targetOrbitAngle.ToRotationVector2() * orbitRadius;
            
            //使用EaseOutSine缓动（更飘逸）
            float easeProgress = EaseOutSine(progress);
            
            //添加飘动轨迹
            Vector2 driftOffset = new Vector2(
                (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + floatPhase) * 15f * (1f - easeProgress),
                (float)Math.Cos(Main.GlobalTimeWrappedHourly * 1.5f + floatPhase) * 12f * (1f - easeProgress)
            );
            
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos + driftOffset, easeProgress * 0.3f);
            
            currentOrbitAngle = targetOrbitAngle;
            glowIntensity = MathHelper.Lerp(0f, 0.4f, progress);
            
            //聚集羽毛粒子
            if (Main.rand.NextBool(5)) {
                SpawnGatherParticle();
            }
            
            //转入漂浮阶段
            if (StateTimer >= GatherDuration) {
                State = FeatherState.Floating;
                StateTimer = 0;
                
                //轻柔的风声
                SoundEngine.PlaySound(SoundID.Item32 with { 
                    Volume = 0.3f, 
                    Pitch = 0.3f 
                }, Projectile.Center);
            }
        }

        /// <summary>
        /// 漂浮阶段：自然漂浮并重新分布
        /// </summary>
        private void FloatingPhaseAI(Player owner) {
            float progress = StateTimer / FloatDuration;
            
            //重新计算均匀分布
            int totalFeathers = 0;
            int myIndex = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && 
                    Main.projectile[i].type == Projectile.type && 
                    Main.projectile[i].owner == owner.whoAmI) {
                    if (Main.projectile[i].whoAmI == Projectile.whoAmI) {
                        myIndex = totalFeathers;
                    }
                    totalFeathers++;
                }
            }
            
            targetOrbitAngle = MathHelper.TwoPi * myIndex / Math.Max(totalFeathers, 1);
            
            //半径轻微脉动
            float radiusPulse = (float)Math.Sin(StateTimer * 0.15f) * 8f;
            float currentRadius = orbitRadius + radiusPulse;
            
            //漂浮运动
            floatPhase += floatFrequency;
            Vector2 floatOffset = new Vector2(
                (float)Math.Sin(floatPhase * 1.2f) * floatAmplitude,
                (float)Math.Cos(floatPhase * 0.8f) * floatAmplitude * 0.7f
            );
            
            //平滑过渡到目标角度
            currentOrbitAngle = MathHelper.Lerp(currentOrbitAngle, targetOrbitAngle, 0.08f);
            
            Vector2 orbitPos = owner.Center + currentOrbitAngle.ToRotationVector2() * currentRadius;
            Vector2 targetPos = orbitPos + floatOffset;
            
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.25f);
            
            glowIntensity = MathHelper.Lerp(0.4f, 0.6f, progress);
            
            //漂浮羽毛粒子
            if (Main.rand.NextBool(8)) {
                SpawnFloatParticle();
            }
            
            //转入环绕阶段
            if (StateTimer >= FloatDuration) {
                State = FeatherState.Orbiting;
                StateTimer = 0;
                
                //开始旋转音效
                SoundEngine.PlaySound(SoundID.Item30 with { 
                    Volume = 0.35f, 
                    Pitch = 0.4f 
                }, Projectile.Center);
            }
        }

        /// <summary>
        /// 环绕阶段：优雅旋转
        /// </summary>
        private void OrbitingPhaseAI(Player owner) {
            //不再使用进度，因为不会自动结束
            float timeProgress = MathHelper.Clamp(StateTimer / 60f, 0f, 1f); // 仅用于加速曲线
            
            //逐渐加速到最大速度
            float speedProgress = EaseInOutQuad(timeProgress);
            orbitSpeed = MathHelper.Lerp(0.03f, MaxOrbitSpeed, speedProgress);
            
            //半径随时间轻微收缩
            float radiusScale = MathHelper.Lerp(1f, 0.92f, MathHelper.Clamp(speedProgress, 0f, 1f));
            float radiusWave = (float)Math.Sin(StateTimer * 0.2f) * 6f;
            float currentRadius = orbitRadius * radiusScale + radiusWave;
            
            //更新环绕角度（逆时针旋转，更优雅）
            currentOrbitAngle -= orbitSpeed;
            
            //漂浮叠加（随时间减弱）
            float floatScale = MathHelper.Clamp(1f - speedProgress * 0.6f, 0.4f, 1f);
            floatPhase += floatFrequency * floatScale;
            Vector2 floatOffset = new Vector2(
                (float)Math.Sin(floatPhase) * floatAmplitude * floatScale,
                (float)Math.Cos(floatPhase * 0.7f) * floatAmplitude * 0.6f * floatScale
            );
            
            Vector2 orbitPos = owner.Center + currentOrbitAngle.ToRotationVector2() * currentRadius;
            Vector2 targetPos = orbitPos + floatOffset;
            
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.35f);
            
            //辉光逐渐增强
            glowIntensity = MathHelper.Lerp(0.6f, 0.8f, timeProgress);
            
            //优雅的羽毛轨迹
            if (Main.rand.NextBool(4)) {
                SpawnOrbitParticle(timeProgress);
            }
            
            //旋转风声
            if (StateTimer % (int)MathHelper.Lerp(30, 15, timeProgress) == 0) {
                SoundEngine.PlaySound(SoundID.Item32 with { 
                    Volume = 0.2f + 0.15f * timeProgress, 
                    Pitch = 0.3f + timeProgress * 0.3f 
                }, Projectile.Center);
            }
            
            //注意：不再自动转入发射阶段，只能通过CheckForChargingPhase进入蓄力
        }

        /// <summary>
        /// 蓄力阶段：所有羽毛准备同步发射
        /// </summary>
        private void ChargingPhaseAI(Player owner) {
            float progress = StateTimer / ChargeDuration;
            
            //保持最高旋转速度
            orbitSpeed = MaxOrbitSpeed;
            
            //半径脉动（蓄力震荡感）
            float radiusOscillation = (float)Math.Sin(StateTimer * 0.6f) * 12f * progress;
            float currentRadius = orbitRadius * 0.92f + radiusOscillation;
            
            //更新环绕
            currentOrbitAngle -= orbitSpeed;
            Vector2 orbitOffset = currentOrbitAngle.ToRotationVector2() * currentRadius;
            Vector2 targetPos = owner.Center + orbitOffset;
            Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.4f);
            
            //最大辉光并闪烁
            glowIntensity = 0.9f + (float)Math.Sin(StateTimer * 1.2f) * 0.1f;
            
            //密集蓄力粒子
            if (Main.rand.NextBool()) {
                SpawnChargeParticle(owner.Center, progress);
            }
            
            //蓄力脉冲
            if (StateTimer % 8 == 0) {
                SpawnChargePulse();
            }
            
            //高频蓄力音效
            if (StateTimer % 6 == 0) {
                SoundEngine.PlaySound(SoundID.Item32 with { 
                    Volume = 0.2f + progress * 0.3f, 
                    Pitch = 0.5f + progress * 0.5f 
                }, Projectile.Center);
            }
            
            //倒计时发射
            launchCountdown--;
            if (launchCountdown <= 0) {
                State = FeatherState.Launching;
                StateTimer = 0;
                LaunchFeather();
            }
        }

        private void SpawnChargeParticle(Vector2 ownerCenter, float progress) {
            Vector2 toCenter = (ownerCenter - Projectile.Center).SafeNormalize(Vector2.Zero);
            Vector2 velocity = toCenter * Main.rand.NextFloat(2f, 5f) * progress;
            
            Dust charge = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.Cloud,
                velocity,
                100,
                new Color(255, 255, 255),
                Main.rand.NextFloat(1.3f, 2f)
            );
            charge.noGravity = true;
            charge.fadeIn = 1.2f;
        }

        private void SpawnChargePulse() {
            //环形蓄力脉冲
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 velocity = angle.ToRotationVector2() * 3f;
                
                Dust pulse = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Cloud,
                    velocity,
                    100,
                    new Color(255, 255, 255),
                    Main.rand.NextFloat(1.5f, 2.2f)
                );
                pulse.noGravity = true;
                pulse.fadeIn = 1.3f;
            }
        }

        /// <summary>
        /// 发射羽毛
        /// </summary>
        private void LaunchFeather() {
            // 使用羽毛当前的朝向作为发射方向
            // 环绕时羽毛朝向为 currentOrbitAngle - MathHelper.PiOver2
            Vector2 launchDir = (currentOrbitAngle - MathHelper.PiOver2).ToRotationVector2();
            
            // 计算发射速度（基于旋转速度）
            float speedBonus = orbitSpeed / MaxOrbitSpeed;
            float finalSpeed = LaunchSpeed * (1f + speedBonus * 0.4f);
            
            Projectile.velocity = launchDir * finalSpeed;
            Projectile.tileCollide = true;
            
            // 发射羽毛特效
            SpawnLaunchEffect();
            
            // 轻柔的发射音效（只由第一个羽毛播放）
            if (Projectile.whoAmI == GetFirstFeatherID(Main.player[Projectile.owner])) {
                SoundEngine.PlaySound(SoundID.Item1 with { 
                    Volume = 0.6f, 
                    Pitch = 0.6f 
                }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item32 with { 
                    Volume = 0.5f, 
                    Pitch = 0.8f 
                }, Projectile.Center);
            }
        }

        /// <summary>
        /// 发射阶段：优雅飞行
        /// </summary>
        private void LaunchingPhaseAI() {
            //轻微速度衰减
            Projectile.velocity *= 0.995f;
            
            //羽毛飘动（添加轻微波动）
            Vector2 driftForce = new Vector2(
                (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f + floatPhase) * 0.05f,
                (float)Math.Cos(Main.GlobalTimeWrappedHourly * 1.5f + floatPhase) * 0.04f
            );
            Projectile.velocity += driftForce;
            
            //朝向速度方向
            if (Projectile.velocity.LengthSquared() > 0.1f) {
                Projectile.rotation = Projectile.velocity.ToRotation();
            }
            
            glowIntensity = 0.8f;
            
            //飞行羽毛轨迹
            if (Main.rand.NextBool(4)) {
                SpawnLaunchTrailParticle();
            }
        }

        //===== 粒子效果方法 =====
        
        private void SpawnGatherParticle() {
            Dust gather = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                DustID.Cloud,
                (Main.player[Projectile.owner].Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.5f, 2f),
                100,
                new Color(240, 240, 255),
                Main.rand.NextFloat(0.8f, 1.3f)
            );
            gather.noGravity = true;
            gather.fadeIn = 1f;
        }

        private void SpawnFloatParticle() {
            Vector2 velocity = Main.rand.NextVector2Circular(1f, 1f);
            
            Dust float_ = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                DustID.Cloud,
                velocity,
                100,
                new Color(245, 245, 255),
                Main.rand.NextFloat(0.7f, 1.2f)
            );
            float_.noGravity = true;
            float_.fadeIn = 1f;
            float_.alpha = 100;
        }

        private void SpawnOrbitParticle(float progress) {
            //沿切线方向的轨迹粒子
            Vector2 tangentDir = new Vector2(
                -(float)Math.Sin(currentOrbitAngle),
                (float)Math.Cos(currentOrbitAngle)
            );
            
            Vector2 velocity = tangentDir * Main.rand.NextFloat(1f, 3f) * progress;
            
            Dust orbit = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                DustID.Cloud,
                velocity,
                100,
                new Color(250, 250, 255),
                Main.rand.NextFloat(0.9f, 1.5f)
            );
            orbit.noGravity = true;
            orbit.fadeIn = 1.1f;
            orbit.alpha = 80;
        }

        private void SpawnLaunchEffect() {
            //发射时的羽毛爆发
            for (int i = 0; i < 15; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 8f);
                
                Dust launch = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Cloud,
                    velocity,
                    100,
                    new Color(250, 250, 255),
                    Main.rand.NextFloat(1.2f, 2f)
                );
                launch.noGravity = true;
                launch.fadeIn = 1.2f;
            }
            
            //空气涟漪
            for (int i = 0; i < 8; i++) {
                Dust air = Dust.NewDustDirect(
                    Projectile.Center - new Vector2(10),
                    20, 20,
                    DustID.Cloud,
                    Scale: Main.rand.NextFloat(1.5f, 2.5f)
                );
                air.velocity = Main.rand.NextVector2Circular(4f, 4f);
                air.noGravity = true;
                air.alpha = 120;
            }
        }

        private void SpawnLaunchTrailParticle() {
            Dust trail = Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                DustID.Cloud,
                -Projectile.velocity * Main.rand.NextFloat(0.1f, 0.2f),
                100,
                new Color(245, 245, 255),
                Main.rand.NextFloat(0.8f, 1.4f)
            );
            trail.noGravity = true;
            trail.fadeIn = 1f;
            trail.alpha = 120;
        }

        //===== 缓动函数 =====
        
        private float EaseOutSine(float t) {
            return (float)Math.Sin(t * MathHelper.PiOver2);
        }

        private float EaseInOutQuad(float t) {
            return t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //碰撞后轻柔反弹
            if (Math.Abs(Projectile.velocity.X - oldVelocity.X) > float.Epsilon) {
                Projectile.velocity.X = -oldVelocity.X * 0.6f;
            }
            if (Math.Abs(Projectile.velocity.Y - oldVelocity.Y) > float.Epsilon) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.6f;
            }
            
            //轻柔的碰撞音效
            SoundEngine.PlaySound(SoundID.Item32 with { 
                Volume = 0.3f, 
                Pitch = 0.5f 
            }, Projectile.Center);
            
            //碰撞羽毛粒子
            for (int i = 0; i < 5; i++) {
                Dust.NewDust(
                    Projectile.position,
                    Projectile.width,
                    Projectile.height,
                    DustID.Cloud,
                    Scale: Main.rand.NextFloat(1f, 1.5f)
                );
            }
            
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中羽毛粒子效果
            for (int i = 0; i < 10; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(5f, 5f);
                Dust hitDust = Dust.NewDustPerfect(
                    Projectile.Center,
                    DustID.Cloud,
                    velocity,
                    100,
                    new Color(250, 250, 255),
                    Main.rand.NextFloat(1.2f, 1.8f)
                );
                hitDust.noGravity = true;
                hitDust.fadeIn = 1.1f;
            }
            
            //击中音效
            SoundEngine.PlaySound(SoundID.NPCHit5 with { 
                Volume = 0.4f, 
                Pitch = 0.4f 
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D featherTex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle sourceRect = featherTex.Frame(1, 1);
            Vector2 origin = sourceRect.Size() / 2f;
            
            Color baseColor = lightColor;
            float alpha = (255f - Projectile.alpha) / 255f;
            
            //===== 绘制飘逸拖尾 =====
            if (State == FeatherState.Orbiting || State == FeatherState.Charging || State == FeatherState.Launching) {
                DrawFeatherAfterimages(sb, featherTex, sourceRect, origin, baseColor, alpha);
            }
            
            //===== 绘制外层辉光 =====
            if (glowIntensity > 0.3f && SoftGlow?.Value != null) {
                Texture2D glow = SoftGlow.Value;
                float glowScale = Projectile.scale * (0.8f + glowIntensity * 0.4f);
                float glowAlpha = (glowIntensity - 0.3f) * alpha * 0.3f;
                
                //蓄力阶段增强辉光
                if (State == FeatherState.Charging) {
                    glowAlpha *= 1.5f;
                }
                
                //柔和白光
                sb.Draw(
                    glow,
                    drawPos,
                    null,
                    new Color(250, 250, 255, 0) * glowAlpha,
                    Projectile.rotation + swayAngle,
                    glow.Size() / 2f,
                    glowScale,
                    SpriteEffects.None,
                    0f
                );
            }
            
            //===== 绘制主体羽毛 =====
            //计算羽毛朝向
            float drawRotation;
            if (State == FeatherState.Launching) {
                drawRotation = Projectile.rotation - MathHelper.PiOver2;
            }
            else {
                //环绕时保持切线方向
                drawRotation = currentOrbitAngle - MathHelper.PiOver2 + swayAngle;
            }
            
            //基础绘制
            sb.Draw(
                featherTex,
                drawPos,
                sourceRect,
                baseColor * alpha,
                drawRotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );
            
            //环绕/蓄力/发射时的轻微发光覆盖层
            if ((State == FeatherState.Orbiting || State == FeatherState.Charging || State == FeatherState.Launching) 
                && glowIntensity > 0.5f) {
                float lightAlpha = (glowIntensity - 0.5f) * 2f * alpha * 0.35f;
                
                //蓄力阶段增强
                if (State == FeatherState.Charging) {
                    lightAlpha *= 1.3f;
                }
                
                Color featherLight = new Color(245, 245, 255);
                
                sb.Draw(
                    featherTex,
                    drawPos,
                    sourceRect,
                    featherLight * lightAlpha,
                    drawRotation,
                    origin,
                    Projectile.scale * 1.02f,
                    SpriteEffects.None,
                    0
                );
            }
            
            return false;
        }

        /// <summary>
        /// 绘制飘逸的羽毛残影
        /// </summary>
        private void DrawFeatherAfterimages(SpriteBatch sb, Texture2D featherTex, Rectangle sourceRect, 
            Vector2 origin, Color baseColor, float alpha) {
            
            int afterimageCount = State == FeatherState.Launching ? 10 : (State == FeatherState.Charging ? 8 : 6);
            
            for (int i = 0; i < afterimageCount; i++) {
                if (i >= Projectile.oldPos.Length || Projectile.oldPos[i] == Vector2.Zero) continue;
                
                float afterimageProgress = 1f - i / (float)afterimageCount;
                float afterimageAlpha = afterimageProgress * alpha * (State == FeatherState.Charging ? 0.6f : 0.5f);
                
                //残影颜色：环绕时淡蓝白，蓄力时更亮，发射时最亮
                Color afterimageColor;
                if (State == FeatherState.Launching) {
                    afterimageColor = Color.Lerp(
                        new Color(240, 240, 255),
                        new Color(255, 255, 255),
                        afterimageProgress
                    ) * afterimageAlpha;
                }
                else if (State == FeatherState.Charging) {
                    afterimageColor = Color.Lerp(
                        new Color(245, 245, 255),
                        new Color(255, 255, 255),
                        afterimageProgress
                    ) * afterimageAlpha;
                }
                else {
                    afterimageColor = new Color(245, 245, 255) * (afterimageAlpha * 0.6f);
                }
                
                Vector2 afterimagePos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float afterimageScale = Projectile.scale * MathHelper.Lerp(0.9f, 1f, afterimageProgress);
                
                //残影旋转（轻微延迟）
                float afterimageRotation;
                if (State == FeatherState.Launching) {
                    afterimageRotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2 - i * 0.05f;
                }
                else {
                    afterimageRotation = currentOrbitAngle - MathHelper.PiOver2 + swayAngle - i * 0.08f;
                }
                
                sb.Draw(
                    featherTex,
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
