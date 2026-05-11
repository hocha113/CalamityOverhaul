using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces
{
    /// <summary>
    /// 赛博蓄力能量球弹幕
    /// <br/>右键持续蓄力，释放后直线高速飞行，命中后爆炸
    /// <br/>蓄力阶段：固定在玩家前方，由小变大，黄金色→白青色过渡
    /// <br/>飞行阶段：直线高速飞行，拖尾方形粒子
    /// <br/>命中阶段：生成 CyberDetonationProj 爆破特效
    /// </summary>
    internal class CyberChargeOrbProj : BaseHeldProj, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        #region 常量

        /// <summary>满蓄所需帧数（2秒 × 60帧）</summary>
        private const int MaxChargeFrames = 120;
        /// <summary>最低蓄力帧数（低于此释放视为取消）</summary>
        private const int MinChargeFrames = 15;
        /// <summary>飞行速度</summary>
        private const float FlySpeed = 22f;
        /// <summary>蓄力完成时球体视觉直径（像素）</summary>
        private const float MaxOrbDiameter = 100f;
        /// <summary>蓄力阶段汇聚粒子生成间隔</summary>
        private const int ConvergeParticleInterval = 4;
        /// <summary>飞行阶段拖尾粒子间隔</summary>
        private const int TrailParticleInterval = 2;
        /// <summary>球体离玩家中心的前方偏移距离</summary>
        private const float ChargeOffsetDist = 70f;
        /// <summary>蓄力阶段魔力消耗间隔帧数</summary>
        private static int ManaDrainInterval => 4;
        /// <summary>每次消耗的魔力量</summary>
        private static int ManaDrainCost => 2;

        #endregion

        #region 状态枚举

        private enum OrbState
        {
            Charging = 0,
            Flying = 1,
        }

        #endregion

        #region 颜色

        //蓄力颜色（黄金色系）
        private static readonly Color ChargeCore = new(255, 220, 80);
        private static readonly Color ChargeGlow = new(230, 170, 30);
        private static readonly Color ChargeAura = new(150, 100, 15);

        //满蓄/飞行颜色（白青色系）
        private static readonly Color FullCore = new(220, 255, 255);
        private static readonly Color FullGlow = new(80, 230, 220);
        private static readonly Color FullAura = new(20, 140, 130);

        //超驱颜色（高温红炽 + 白热）
        private static readonly Color ODCore = new(255, 245, 200);
        private static readonly Color ODGlow = new(255, 25, 40);
        private static readonly Color ODAura = new(180, 0, 15);
        private static readonly Color ODParticleMain = new(255, 170, 40);
        private static readonly Color ODParticleEdge = new(255, 20, 20);

        #endregion

        #region 实例字段

        private int chargeTime;
        private float chargeRatio; //0~1
        private float fadeAlpha;
        private int particleTimer;
        private float flyAngle;

        /// <summary>超驱混合量 0-1</summary>
        private float overdriveAmount;
        /// <summary>故障爆发计时器</summary>
        private int glitchBurstTimer;
        /// <summary>当前故障爆发强度</summary>
        private float glitchBurstIntensity;

        /// <summary>蓄力循环音效跟踪</summary>
        private SlotId chargeSoundSlot;
        /// <summary>满蓄提示音是否已播放</summary>
        private bool fullChargeSoundPlayed;

        /// <summary>蹄力时间倍率，由 localAI[1] 注入；默认 1f</summary>
        private float chargeTimeMul = 1f;
        /// <summary>能量球飞行速度倍率，由 localAI[2] 注入；默认 1f</summary>
        private float flySpeedMul = 1f;

        //═════════════ 改件行为注入字段 ═════════════
        //由 SHPCOverride.OnShoot 在 Projectile.NewProjectile 之后直接写入

        /// <summary>蓄力期间是否在球体周围持续吸引附近敌人</summary>
        public bool DrainAura;
        /// <summary>爆炸半径倍率（最终半径 = 基础 × 此值）</summary>
        public float ExplosionRadiusMul = 1f;
        /// <summary>爆炸时生成的迷你追踪光球数量</summary>
        public int DetonationMinions;
        /// <summary>爆炸时是否将玩家反推弹射</summary>
        public bool ExplosionPropels;
        /// <summary>飞行阶段是否持续向最近敌人偏转追踪</summary>
        public bool FlyingAttract;
        /// <summary>蓄力期间每次消耗蓝量的倍率，仅受模块设置影响</summary>
        public float ManaCostMul = 1f;
        /// <summary>攻速倍率，影响蓄力进度推进速度</summary>
        public float AttackSpeedMul = 1f;

        private OrbState State {
            get => (OrbState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        /// <summary>
        /// 关联的手持弹幕索引（ai[1]），用于蓄力阶段定位枪口
        /// </summary>
        private int HeldProjIndex {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        #endregion

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
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
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool? CanDamage() {
            if (State == OrbState.Charging) {
                return false;
            }
            return base.CanDamage();
        }

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            //首帧读取改件倍率注入值
            if (Projectile.localAI[0] == 0f) {
                chargeTimeMul = Projectile.localAI[1] > 0f ? Projectile.localAI[1] : 1f;
                flySpeedMul = Projectile.localAI[2] > 0f ? Projectile.localAI[2] : 1f;
                Projectile.localAI[0] = 1f;
            }

            //超驱检测与过渡：只取"主人玩家"自己领域内才进入超驱
            bool insideDomain = Cyberspace.IsInsideDomainOf(Projectile.owner, Projectile.Center);
            float targetOD = insideDomain ? 1f : 0f;
            float prevOD = overdriveAmount;
            overdriveAmount = MathHelper.Lerp(overdriveAmount, targetOD, 0.055f);
            if (overdriveAmount < 0.005f) overdriveAmount = 0f;

            //首次进入超驱阈值时，给 burstTimer 一个随机初始值，避免立即触发
            if (prevOD <= 0.3f && overdriveAmount > 0.3f) {
                glitchBurstTimer = Main.rand.Next(8, 20);
            }

            //间歇性故障爆发（高频黑墙撕裂）
            if (overdriveAmount > 0.3f) {
                glitchBurstTimer--;
                if (glitchBurstTimer <= 0) {
                    glitchBurstIntensity = 1f;
                    glitchBurstTimer = Main.rand.Next(18, 35);
                }
            }
            glitchBurstIntensity *= 0.85f;
            if (glitchBurstIntensity < 0.01f) glitchBurstIntensity = 0f;

            switch (State) {
                case OrbState.Charging:
                    AI_Charging();
                    break;
                case OrbState.Flying:
                    AI_Flying();
                    break;
            }
        }

        #region 蓄力阶段

        private void AI_Charging() {
            //尝试从手持弹幕获取枪口位置
            Vector2 targetPos;
            int heldIdx = HeldProjIndex;
            if (heldIdx >= 0 && heldIdx < Main.maxProjectiles
                && Main.projectile[heldIdx].active
                && Main.projectile[heldIdx].ModProjectile is SHPCChargeHeldProj heldProj) {
                targetPos = heldProj.TipPosition;
            }
            else {
                //后备：直接用玩家前方偏移
                targetPos = Owner.GetPlayerStabilityCenter() + UnitToMouseV * ChargeOffsetDist;
            }

            Projectile.Center = targetPos;
            Vector2 aimDir = UnitToMouseV;
            Projectile.rotation = aimDir.ToRotation();

            //玩家面向光球方向
            Owner.ChangeDir(aimDir.X > 0f ? 1 : -1);
            base.Owner.manaRegenDelay = 16;//只要在右键就不要恢复魔力

            //蓄力耗蓝：绕过原版CheckMana路径，仅受模块ManaCostMul影响
            //只有本地玩家才执行消耗与蓄力门控，避免多端逻辑冲突
            bool canCharge = true;
            if (Projectile.IsOwnedByLocalPlayer() && chargeRatio < 1f) {
                int cost = Math.Max((int)(ManaDrainCost * ManaCostMul), 1);
                if (chargeTime % ManaDrainInterval == 0) {
                    if (Owner.statMana >= cost) {
                        Owner.statMana -= cost;
                    }
                    else {
                        //蓝量不足时暂停蓄力推进
                        canCharge = false;
                    }
                }
                if (Owner.statMana <= 0) {
                    canCharge = false;
                }
            }
            if (canCharge) {
                chargeTime++;
            }
            //蓄力进度受ChargeTimeMul和AttackSpeedMul加算影响，避免乘算叠加导致蓄力过慢
            float effectiveFrames = MaxChargeFrames * MathF.Max(chargeTimeMul - AttackSpeedMul + 1f, 0.1f);
            chargeRatio = MathHelper.Clamp((float)chargeTime / effectiveFrames, 0f, 1f);

            //蓄力音效：从开始蓄力即播放，pitch 随蓄力比例递增，超驱时额外升调+抖动
            if (chargeTime == 1 && Main.netMode != NetmodeID.Server) {
                SoundStyle chargeSound = "CalamityMod/Sounds/Item/NorfleetRecharge".GetSound();
                chargeSoundSlot = SoundEngine.PlaySound(chargeSound with { Volume = 0.8f, Pitch = -0.6f }, Projectile.Center);
            }
            if (SoundEngine.TryGetActiveSound(chargeSoundSlot, out var activeChargeSound)) {
                activeChargeSound.Position = Projectile.Center;
                float basePitch = MathHelper.Lerp(-0.3f, 0.5f, chargeRatio);
                float odPitch = overdriveAmount * 0.2f;
                float odFlutter = overdriveAmount > 0.3f
                    ? overdriveAmount * 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.8f)
                    : 0f;
                activeChargeSound.Pitch = basePitch + odPitch + odFlutter;
            }

            //蓄力阶段不移动
            Projectile.velocity = Vector2.Zero;
            Projectile.timeLeft = 600; //重置timeLeft防止蓄力超时消失

            //淡入
            fadeAlpha = MathHelper.Clamp(chargeTime / 15f, 0f, 1f);

            //光照（超驱时更亮）
            Color currentCore = Color.Lerp(
                Color.Lerp(ChargeCore, FullCore, chargeRatio),
                ODCore, overdriveAmount);
            Lighting.AddLight(Projectile.Center, currentCore.ToVector3() * (0.5f + overdriveAmount * 1.0f) * fadeAlpha * (0.3f + chargeRatio * 0.7f));

            //汇聚粒子（超驱时极密集）
            int interval = overdriveAmount > 0.3f ? 1 : ConvergeParticleInterval;
            particleTimer++;
            if (particleTimer >= interval && Main.netMode != NetmodeID.Server) {
                particleTimer = 0;
                SpawnConvergeParticles();
            }

            //满蓄首次提示音
            if (chargeRatio >= 1f && !fullChargeSoundPlayed && Main.netMode != NetmodeID.Server) {
                fullChargeSoundPlayed = true;
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.9f, Pitch = 0.3f }, Projectile.Center);
            }

            //满蓄脉冲提示
            if (chargeRatio >= 1f && chargeTime % 20 == 0) {
                if (Main.netMode != NetmodeID.Server) {
                    Color pulseMain = overdriveAmount > 0.3f ? ODCore : FullCore;
                    Color pulseEdge = overdriveAmount > 0.3f ? ODGlow : FullGlow;
                    int pulseCount = overdriveAmount > 0.3f ? 14 : 4;
                    for (int i = 0; i < pulseCount; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(4f + overdriveAmount * 5f, 4f + overdriveAmount * 5f);
                        PRTLoader.AddParticle(new PRT_CyberSquare(
                            Projectile.Center, vel,
                            pulseMain, pulseEdge,
                            Main.rand.NextFloat(0.6f, 1.0f + overdriveAmount * 0.5f), Main.rand.Next(15, 25)
                        ));
                    }
                }
            }

            //改件钩子：蓄力到一定程度后开始吸引附近敌人
            if (DrainAura && chargeRatio > 0.25f) {
                ApplyDrainAura();
            }

            SHPCModificationSystem.ForEachModule(base.Owner, mod => mod.OnOrbCharging(this, Owner));
            //检测右键释放 → 发射
            if (!DownRight) {
                if (chargeTime >= MinChargeFrames) {
                    LaunchOrb(Owner);
                }
                else {
                    //蓄力不足，取消
                    Projectile.Kill();
                }
            }
        }

        /// <summary>
        /// 引力光环：蓄力期间持续将范围内敌人朝球心拉拽，强度随蓄力比例增长
        /// 仅在弹幕拥有者侧执行，避免多端施力冲突
        /// </summary>
        private void ApplyDrainAura() {
            if (Projectile.owner != Main.myPlayer) return;
            float radius = MathHelper.Lerp(220f, 460f, chargeRatio);
            float radiusSq = radius * radius;
            float pull = MathHelper.Lerp(0.2f, 0.85f, chargeRatio) * (1f + overdriveAmount * 0.5f);
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal || npc.boss) continue;
                Vector2 toOrb = Projectile.Center - npc.Center;
                if (toOrb.LengthSquared() > radiusSq) continue;
                if (toOrb.LengthSquared() < 16f) continue;
                npc.velocity += toOrb.SafeNormalize(Vector2.Zero) * pull;
            }
        }

        private void SpawnConvergeParticles() {
            float spawnRadius = 80f + (1f - chargeRatio) * 120f;
            float od = overdriveAmount;
            int count = 1 + (int)(chargeRatio * 2f) + (od > 0.3f ? 4 : 0);

            Color mainCol = Color.Lerp(
                Color.Lerp(ChargeCore, FullCore, chargeRatio),
                ODParticleMain, od);
            Color edgeCol = Color.Lerp(
                Color.Lerp(ChargeGlow, FullGlow, chargeRatio),
                ODParticleEdge, od);

            for (int i = 0; i < count; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(spawnRadius * 0.6f, spawnRadius);
                Vector2 spawnPos = Projectile.Center + offset;

                PRTLoader.AddParticle(new PRT_CyberConverge(
                    spawnPos, Projectile.Center,
                    mainCol, edgeCol,
                    Main.rand.NextFloat(0.5f, 1.0f),
                    Main.rand.Next(18, 35),
                    chargeRatio
                ));
            }
        }

        private void LaunchOrb(Player owner) {
            //停止蓄力音效
            StopChargeSound();
            State = OrbState.Flying;
            Vector2 aimDir = UnitToMouseV;
            flyAngle = aimDir.ToRotation();
            float timeScale = TimeGear.TimeScale;
            Projectile.velocity = aimDir * FlySpeed * flySpeedMul * timeScale;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 300; //飞行最多5秒

            //发射时粒子爆发
            if (!VaultUtils.isServer) {
                float od = overdriveAmount;
                Color launchMain = od > 0.3f ? Color.Lerp(FullCore, ODCore, od) : FullCore;
                Color launchEdge = od > 0.3f ? Color.Lerp(FullGlow, ODGlow, od) : FullGlow;
                int burstCount = od > 0.3f ? 30 : 12;
                for (int i = 0; i < burstCount; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f + od * 6f, 6f + od * 6f);
                    PRTLoader.AddParticle(new PRT_CyberSquare(
                        Projectile.Center, vel + Projectile.velocity * 0.3f,
                        launchMain, launchEdge,
                        Main.rand.NextFloat(0.8f, 1.5f + od * 0.5f), Main.rand.Next(20, 35)
                    ));
                }
                SoundEngine.PlaySound("CalamityMod/Sounds/Item/NorfleetFire".GetSound() with { Pitch = -0.62f, Volume = 0.85f }, Projectile.Center);
            }

            Projectile.netUpdate = true;
            SHPCModificationSystem.ForEachModule(Owner, mod => mod.OnOrbLaunched(this));
        }

        #endregion

        #region 飞行阶段

        private void AI_Flying() {
            //根据变速齿轮缩放飞行速度，方向从flyAngle恢复避免冻结后丢失
            float timeScale = TimeGear.TimeScale;
            float effectiveSpeed = FlySpeed * flySpeedMul * timeScale;

            //FlyingAttract：持续向最近敌人偏转，增加飞行追踪性
            if (FlyingAttract && Projectile.owner == Main.myPlayer) {
                NPC nearest = Projectile.Center.FindClosestNPC(480f, false, true);
                if (nearest != null) {
                    float toAngle = (nearest.Center - Projectile.Center).ToRotation();
                    float diff = MathHelper.WrapAngle(toAngle - flyAngle);
                    flyAngle += MathHelper.Clamp(diff, -0.08f, 0.08f);
                    Projectile.netUpdate = true;
                }
            }

            Projectile.velocity = flyAngle.ToRotationVector2() * effectiveSpeed;

            Projectile.rotation = flyAngle;
            fadeAlpha = 1f;

            //飞行发光（超驱时更亮）
            Color flyCore = Color.Lerp(
                Color.Lerp(ChargeCore, FullCore, chargeRatio),
                ODCore, overdriveAmount);
            Lighting.AddLight(Projectile.Center, flyCore.ToVector3() * (0.7f + overdriveAmount * 0.8f));

            //拖尾粒子（冻结时不生成）
            if (timeScale > 0.01f) {
                int baseInterval = overdriveAmount > 0.3f ? 1 : TrailParticleInterval;
                int interval = (int)MathHelper.Max(baseInterval / timeScale, baseInterval);
                particleTimer++;
                if (particleTimer >= interval && Main.netMode != NetmodeID.Server) {
                    particleTimer = 0;
                    SpawnTrailParticles();
                }
            }
            SHPCModificationSystem.ForEachModule(Owner, mod => mod.OnOrbFlyingAI(this));
        }

        private void SpawnTrailParticles() {
            float od = overdriveAmount;
            Color mainCol = Color.Lerp(
                Color.Lerp(ChargeCore, FullCore, chargeRatio),
                ODParticleMain, od);
            Color edgeCol = Color.Lerp(
                Color.Lerp(ChargeGlow, FullGlow, chargeRatio),
                ODParticleEdge, od);

            Vector2 perpDir = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            int count = od > 0.3f ? 8 : 3;
            for (int i = 0; i < count; i++) {
                Vector2 offset = perpDir * Main.rand.NextFloat(-12f - od * 10f, 12f + od * 10f);
                Vector2 vel = -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 6f + od * 5f)
                    + perpDir * Main.rand.NextFloat(-3f - od * 2f, 3f + od * 2f);
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    Projectile.Center + offset, vel,
                    mainCol, edgeCol,
                    Main.rand.NextFloat(0.6f, 1.2f + od * 0.6f), Main.rand.Next(15, 30)
                ));
            }
        }

        #endregion

        #region 命中与爆炸

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SpawnDetonation();
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            SpawnDetonation();
            return true;
        }

        public override void OnKill(int timeLeft) {
            StopChargeSound();
            SHPCModificationSystem.ForEachModule(Owner, mod => mod.OnOrbKill(this, timeLeft));
            //消散粒子（超驱时更多更亮）
            if (Main.netMode == NetmodeID.Server) return;
            float od = overdriveAmount;
            Color mainCol = Color.Lerp(
                Color.Lerp(ChargeCore, FullCore, chargeRatio),
                ODParticleMain, od);
            Color edgeCol = Color.Lerp(
                Color.Lerp(ChargeGlow, FullGlow, chargeRatio),
                ODParticleEdge, od);
            int count = od > 0.3f ? 35 : 16;
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(7f + od * 8f, 7f + od * 8f);
                PRTLoader.AddParticle(new PRT_CyberSquare(
                    Projectile.Center, vel,
                    mainCol, edgeCol,
                    Main.rand.NextFloat(0.8f, 2.2f + od * 1.2f), Main.rand.Next(25, 55)
                ));
            }
        }

        private void SpawnDetonation() {
            if (Projectile.owner != Main.myPlayer) return;
            //生成爆破弹幕，传递蓄力比例和伤害
            int damage = (int)(Projectile.damage * (0.5f + chargeRatio * 2.5f)); //蓄力越满伤害越高
            int projIndex = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                damage, Projectile.knockBack,
                Projectile.owner,
                ai0: chargeRatio, //ai[0] = 蓄力比例，影响爆炸规模
                ai1: overdriveAmount //ai[1] = 超驱量，影响爆炸故障效果
            );
            if (projIndex >= 0 && projIndex < Main.maxProjectiles) {
                Main.projectile[projIndex].originalDamage = Projectile.originalDamage;
                //通过 localAI[1] 传递改件提供的爆炸半径倍率
                if (ExplosionRadiusMul > 0.01f && MathF.Abs(ExplosionRadiusMul - 1f) > 0.01f) {
                    Main.projectile[projIndex].localAI[1] = ExplosionRadiusMul;
                }
            }

            //改件钩子：爆炸时反推玩家
            if (ExplosionPropels && Owner != null && Owner.active) {
                Vector2 push = (Owner.Center - Projectile.Center).SafeNormalize(-Projectile.velocity.SafeNormalize(Vector2.UnitY));
                float power = MathHelper.Lerp(8f, 22f, chargeRatio) + overdriveAmount * 6f;
                Owner.velocity = push * power;
                Owner.fallStart = (int)(Owner.position.Y / 16f); //取消摔落伤害
            }

            //改件钩子：爆炸时撒出迷你追踪光球（复用 CyberTraceBeamProj 的强追踪形态）
            if (DetonationMinions > 0) {
                SpawnDetonationMinions(damage);
            }
            SHPCModificationSystem.ForEachModule(Owner, mod => mod.OnOrbDetonation(this));
        }

        /// <summary>
        /// 在爆炸位置撒出若干强追踪迷你光束，伤害削弱、速度略降
        /// </summary>
        private void SpawnDetonationMinions(int detonationDamage) {
            int n = DetonationMinions;
            //衍生弹幕直接使用能量球原始伤害，不从爆炸伤害再次打折
            int dmg = Math.Max(Projectile.damage, 1);
            for (int i = 0; i < n; i++) {
                float ang = MathHelper.TwoPi * i / n + Main.rand.NextFloat(-0.15f, 0.15f);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(8f, 12f);
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                    Projectile.Center, vel,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    dmg, Projectile.knockBack, Projectile.owner,
                    ai0: Main.rand.Next(3));
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].ai[1] = 2.5f; //强力追踪
                    if (Main.projectile[idx].ModProjectile is CyberTraceBeamProj child) {
                        child.IsDerived = true;
                        child.LifeMul = 0.7f;
                    }
                }
            }
        }

        #endregion

        #region 绘制

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.01f) return;

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //根据蓄力比例计算球体大小
            float sizeRatio = State == OrbState.Charging
                ? 0.2f + chargeRatio * 0.8f
                : 1f;
            float orbDiameterPx = MaxOrbDiameter * sizeRatio;

            //当前颜色（黄金→白青过渡，超驱时混合品红）
            float od = overdriveAmount;
            Color currentCore = Color.Lerp(
                Color.Lerp(ChargeCore, FullCore, chargeRatio), ODCore, od);
            Color currentGlow = Color.Lerp(
                Color.Lerp(ChargeGlow, FullGlow, chargeRatio), ODGlow, od);
            Color currentAura = Color.Lerp(
                Color.Lerp(ChargeAura, FullAura, chargeRatio), ODAura, od);

            float pulse = 0.92f + 0.08f * MathF.Sin((float)Main.timeForVisualEffects * 0.12f + chargeRatio * 5f);
            //超驱时脉冲剧烈抽搐
            pulse += od * 0.25f * MathF.Sin((float)Main.timeForVisualEffects * 0.45f);
            pulse += od * glitchBurstIntensity * 0.2f * MathF.Sin((float)Main.timeForVisualEffects * 1.2f);
            float alpha = fadeAlpha * pulse;
            Vector2 glowOrigin = glow.Size() * 0.5f;

            //外层柔和bloom（超驱时巨大炽热光晕）
            float outerScale = (orbDiameterPx / glow.Width) * (2.5f + od * 2.5f);
            Color outerColor = currentAura * alpha * (0.2f + od * 0.4f);
            spriteBatch.Draw(glow, drawPos, null, outerColor, 0f,
                glowOrigin, outerScale, SpriteEffects.None, 0f);

            //CyberEnergyOrb 着色器绘制
            spriteBatch.End();

            Effect orbShader = EffectLoader.CyberEnergyOrb?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (orbShader != null && noise != null) {
                CyberspacePlayer ownerCp = Cyberspace.For(Projectile.owner);
                float timeVal = ownerCp != null && ownerCp.Active
                    ? ownerCp.EffectTime
                    : (float)Main.timeForVisualEffects * 0.04f;

                orbShader.Parameters["uTime"]?.SetValue(timeVal);
                orbShader.Parameters["fadeAlpha"]?.SetValue(alpha);
                orbShader.Parameters["coreColor"]?.SetValue(currentCore.ToVector3());
                orbShader.Parameters["glowColor"]?.SetValue(currentGlow.ToVector3());
                orbShader.Parameters["auraColor"]?.SetValue(currentAura.ToVector3());
                orbShader.Parameters["orbScale"]?.SetValue(pulse);
                orbShader.Parameters["uNoiseTex"]?.SetValue(noise);
                //超驱参数
                orbShader.Parameters["overdriveAmount"]?.SetValue(od);
                orbShader.Parameters["glitchBurst"]?.SetValue(glitchBurstIntensity);
                orbShader.Parameters["odCoreColor"]?.SetValue(ODCore.ToVector3());
                orbShader.Parameters["odGlowColor"]?.SetValue(ODGlow.ToVector3());
                orbShader.Parameters["odAuraColor"]?.SetValue(ODAura.ToVector3());

                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                orbShader.CurrentTechnique.Passes[0].Apply();

                float orbDrawScale = (orbDiameterPx / glow.Width) * (1.2f + od * 0.8f);
                spriteBatch.Draw(glow, drawPos, null, Color.White, 0f,
                    glowOrigin, orbDrawScale, SpriteEffects.None, 0f);

                spriteBatch.End();
            }

            //恢复 Additive + Deferred
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        #endregion

        public override bool ShouldUpdatePosition() => State == OrbState.Flying;

        /// <summary>
        /// 停止蓄力循环音效
        /// </summary>
        private void StopChargeSound() {
            if (SoundEngine.TryGetActiveSound(chargeSoundSlot, out var sound)) {
                sound.Stop();
            }
        }
    }
}
