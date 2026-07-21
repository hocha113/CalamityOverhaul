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
    /// <summary>右键蓄力球，蓄力/飞行/引爆 Detonation</summary>
    internal class CyberChargeOrbProj : BaseHeldProj, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        #region 常量

        /// <summary>满蓄帧，2s</summary>
        private const int MaxChargeFrames = 120;
        /// <summary>最低蓄力帧，不足取消</summary>
        private const int MinChargeFrames = 15;
        /// <summary>飞行速度</summary>
        private const float FlySpeed = 22f;
        /// <summary>满蓄视觉直径 px</summary>
        private const float MaxOrbDiameter = 100f;
        /// <summary>蓄力汇聚粒子间隔</summary>
        private const int ConvergeParticleInterval = 4;
        /// <summary>飞行拖尾粒子间隔</summary>
        private const int TrailParticleInterval = 2;
        /// <summary>球前向偏移</summary>
        private const float ChargeOffsetDist = 70f;
        /// <summary>蓄力耗蓝间隔帧</summary>
        private static int ManaDrainInterval => 4;
        /// <summary>每次耗蓝量</summary>
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

        //蓄力黄金
        private static readonly Color ChargeCore = new(255, 220, 80);
        private static readonly Color ChargeGlow = new(230, 170, 30);
        private static readonly Color ChargeAura = new(150, 100, 15);

        //满蓄/飞行白青
        private static readonly Color FullCore = new(220, 255, 255);
        private static readonly Color FullGlow = new(80, 230, 220);
        private static readonly Color FullAura = new(20, 140, 130);

        //超驱红炽
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

        /// <summary>时缓倍率，localAI[1]，默认1</summary>
        private float chargeTimeMul = 1f;
        /// <summary>飞行速倍率，localAI[2]，默认1</summary>
        private float flySpeedMul = 1f;

        //改件注入
        //SHPCOverride.OnShoot 写入

        /// <summary>蓄力吸敌</summary>
        public bool DrainAura;
        /// <summary>爆炸半径倍率</summary>
        public float ExplosionRadiusMul = 1f;
        /// <summary>爆炸迷你追踪球数</summary>
        public int DetonationMinions;
        /// <summary>爆炸反推玩家</summary>
        public bool ExplosionPropels;
        /// <summary>飞行偏转追踪</summary>
        public bool FlyingAttract;
        /// <summary>蓄力耗蓝倍率</summary>
        public float ManaCostMul = 1f;
        /// <summary>攻速倍率，推蓄力</summary>
        public float AttackSpeedMul = 1f;

        private OrbState State {
            get => (OrbState)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        /// <summary>蓄力比 0~1，改件可读</summary>
        public float ChargeRatio => chargeRatio;

        /// <summary>蓄力中</summary>
        public bool IsCharging => State == OrbState.Charging;

        /// <summary>ai[1]=手持弹幕索引，枪口定位</summary>
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

            //首帧读改件倍率
            if (Projectile.localAI[0] == 0f) {
                chargeTimeMul = Projectile.localAI[1] > 0f ? Projectile.localAI[1] : 1f;
                flySpeedMul = Projectile.localAI[2] > 0f ? Projectile.localAI[2] : 1f;
                Projectile.localAI[0] = 1f;
            }

            //超驱仅主人自域
            bool insideDomain = Cyberspace.IsInsideDomainOf(Projectile.owner, Projectile.Center);
            float targetOD = insideDomain ? 1f : 0f;
            float prevOD = overdriveAmount;
            overdriveAmount = MathHelper.Lerp(overdriveAmount, targetOD, 0.055f);
            if (overdriveAmount < 0.005f) overdriveAmount = 0f;

            //进超驱阈值随机 burstTimer
            if (prevOD <= 0.3f && overdriveAmount > 0.3f) {
                glitchBurstTimer = Main.rand.Next(8, 20);
            }

            //间歇故障爆发
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
            //手持弹幕枪口
            Vector2 targetPos;
            SHPCChargeHeldProj linkedHeld = null;
            int heldIdx = HeldProjIndex;
            if (heldIdx >= 0 && heldIdx < Main.maxProjectiles
                && Main.projectile[heldIdx].active
                && Main.projectile[heldIdx].ModProjectile is SHPCChargeHeldProj heldProj) {
                linkedHeld = heldProj;
                targetPos = heldProj.TipPosition;
            }
            else {
                //后备前向偏移
                targetPos = Owner.GetPlayerStabilityCenter() + UnitToMouseV * ChargeOffsetDist;
            }

            Projectile.Center = targetPos;
            Vector2 aimDir = UnitToMouseV;
            Projectile.rotation = aimDir.ToRotation();

            Owner.ChangeDir(aimDir.X > 0f ? 1 : -1);
            base.Owner.manaRegenDelay = 16;//右键中禁回蓝

            //耗蓝绕 CheckMana，仅 ManaCostMul
            //仅本地耗蓝/门控
            bool canCharge = true;
            if (Projectile.IsOwnedByLocalPlayer() && chargeRatio < 1f) {
                int cost = Math.Max((int)(ManaDrainCost * ManaCostMul), 1);
                if (chargeTime % ManaDrainInterval == 0) {
                    if (Owner.statMana >= cost) {
                        Owner.statMana -= cost;
                    }
                    else {
                        //蓝不足暂停蓄力
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
            //ChargeTimeMul+AttackSpeedMul 加算
            float effectiveFrames = MaxChargeFrames * MathF.Max(chargeTimeMul - AttackSpeedMul + 1f, 0.1f);
            chargeRatio = MathHelper.Clamp((float)chargeTime / effectiveFrames, 0f, 1f);

            //同步手持弹幕蓄力进度
            if (linkedHeld != null) {
                linkedHeld.ChargeProgress = chargeRatio;
            }

            //蓄力音，pitch 随比例，超驱升调
            if (chargeTime == 1 && Main.netMode != NetmodeID.Server) {
                SoundStyle chargeSound = "CalamityMod/Sounds/Item/NorfleetRecharge".GetSound(SoundID.Item15);
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

            Projectile.velocity = Vector2.Zero;
            Projectile.timeLeft = 600; //防蓄力超时

            fadeAlpha = MathHelper.Clamp(chargeTime / 15f, 0f, 1f);

            //光照，超驱更亮
            Color currentCore = Color.Lerp(
                Color.Lerp(ChargeCore, FullCore, chargeRatio),
                ODCore, overdriveAmount);
            Lighting.AddLight(Projectile.Center, currentCore.ToVector3() * (0.5f + overdriveAmount * 1.0f) * fadeAlpha * (0.3f + chargeRatio * 0.7f));

            //汇聚粒子
            int interval = overdriveAmount > 0.3f ? 1 : ConvergeParticleInterval;
            particleTimer++;
            if (particleTimer >= interval && Main.netMode != NetmodeID.Server) {
                particleTimer = 0;
                SpawnConvergeParticles();
            }

            //满蓄提示音一次
            if (chargeRatio >= 1f && !fullChargeSoundPlayed && Main.netMode != NetmodeID.Server) {
                fullChargeSoundPlayed = true;
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.9f, Pitch = 0.3f }, Projectile.Center);
            }

            //满蓄脉冲
            if (chargeRatio >= 1f && chargeTime % 20 == 0) {
                if (Main.netMode != NetmodeID.Server) {
                    Color pulseMain = overdriveAmount > 0.3f ? ODCore : FullCore;
                    Color pulseEdge = overdriveAmount > 0.3f ? ODGlow : FullGlow;
                    int pulseCount = overdriveAmount > 0.3f ? 14 : 4;
                    for (int i = 0; i < pulseCount; i++) {
                        Vector2 vel = Main.rand.NextVector2CircularEdge(4f + overdriveAmount * 5f, 4f + overdriveAmount * 5f);
                        PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, vel, pulseMain, Main.rand.NextFloat(0.6f, 1.0f + overdriveAmount * 0.5f)).Configure(pulseEdge, Main.rand.Next(15, 25));
                    }
                }
            }

            //改件吸敌
            if (DrainAura && chargeRatio > 0.25f) {
                ApplyDrainAura();
            }

            SHPCModificationSystem.ForEachModule(base.Owner, mod => mod.OnOrbCharging(this, Owner));
            //右键释放发射
            if (!DownRight) {
                if (chargeTime >= MinChargeFrames) {
                    LaunchOrb(Owner);
                }
                else {
                    //蓄力不足取消
                    Projectile.Kill();
                }
            }
        }

        /// <summary>蓄力引力吸敌，仅 owner 端</summary>
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

                PRTLoader.NewParticle<PRT_CyberConverge>(spawnPos, Vector2.Zero, mainCol, Main.rand.NextFloat(0.5f, 1.0f)).Configure(Projectile.Center, edgeCol, Main.rand.Next(18, 35), chargeRatio);
            }
        }

        private void LaunchOrb(Player owner) {
            StopChargeSound();
            State = OrbState.Flying;
            Vector2 aimDir = UnitToMouseV;
            flyAngle = aimDir.ToRotation();
            float timeScale = TimeGear.TimeScale;
            Projectile.velocity = aimDir * FlySpeed * flySpeedMul * timeScale;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 300; //飞行≤5s

            if (!VaultUtils.isServer) {
                float od = overdriveAmount;
                Color launchMain = od > 0.3f ? Color.Lerp(FullCore, ODCore, od) : FullCore;
                Color launchEdge = od > 0.3f ? Color.Lerp(FullGlow, ODGlow, od) : FullGlow;
                int burstCount = od > 0.3f ? 30 : 12;
                for (int i = 0; i < burstCount; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(6f + od * 6f, 6f + od * 6f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, vel + Projectile.velocity * 0.3f, launchMain, Main.rand.NextFloat(0.8f, 1.5f + od * 0.5f)).Configure(launchEdge, Main.rand.Next(20, 35));
                }
                SoundStyle fireSound = "CalamityMod/Sounds/Item/NorfleetFire".GetSound(SoundID.Item45);
                SoundEngine.PlaySound(fireSound with { Pitch = -0.62f, Volume = 0.85f }, Projectile.Center);
            }

            Projectile.netUpdate = true;
            SHPCModificationSystem.ForEachModule(Owner, mod => mod.OnOrbLaunched(this));
        }

        #endregion

        #region 飞行阶段

        private void AI_Flying() {
            //速随齿轮，方向取 flyAngle
            float timeScale = TimeGear.TimeScale;
            float effectiveSpeed = FlySpeed * flySpeedMul * timeScale;

            //FlyingAttract 偏转
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

            //飞行光
            Color flyCore = Color.Lerp(
                Color.Lerp(ChargeCore, FullCore, chargeRatio),
                ODCore, overdriveAmount);
            Lighting.AddLight(Projectile.Center, flyCore.ToVector3() * (0.7f + overdriveAmount * 0.8f));

            //拖尾，冻结跳过
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
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center + offset, vel, mainCol, Main.rand.NextFloat(0.6f, 1.2f + od * 0.6f)).Configure(edgeCol, Main.rand.Next(15, 30));
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
            //消散粒子
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
                PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, vel, mainCol, Main.rand.NextFloat(0.8f, 2.2f + od * 1.2f)).Configure(edgeCol, Main.rand.Next(25, 55));
            }
        }

        private void SpawnDetonation() {
            if (Projectile.owner != Main.myPlayer) return;
            //爆破弹幕
            int damage = (int)(Projectile.damage * (0.5f + chargeRatio * 2.5f)); //蓄力抬伤
            int projIndex = Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<CyberDetonationProj>(),
                damage, Projectile.knockBack,
                Projectile.owner,
                ai0: chargeRatio, //ai0 蓄力比
                ai1: overdriveAmount //ai1 超驱量
            );
            if (projIndex >= 0 && projIndex < Main.maxProjectiles) {
                Main.projectile[projIndex].originalDamage = Projectile.originalDamage;
                //localAI[1] 半径倍率
                if (ExplosionRadiusMul > 0.01f && MathF.Abs(ExplosionRadiusMul - 1f) > 0.01f) {
                    Main.projectile[projIndex].localAI[1] = ExplosionRadiusMul;
                }
            }

            //改件反推
            if (ExplosionPropels && Owner != null && Owner.active) {
                Vector2 push = (Owner.Center - Projectile.Center).SafeNormalize(-Projectile.velocity.SafeNormalize(Vector2.UnitY));
                float power = MathHelper.Lerp(8f, 22f, chargeRatio) + overdriveAmount * 6f;
                Owner.velocity = push * power;
                Owner.fallStart = (int)(Owner.position.Y / 16f); //消摔伤
            }

            //改件撒迷你追踪球
            if (DetonationMinions > 0) {
                SpawnDetonationMinions(damage);
            }
            SHPCModificationSystem.ForEachModule(Owner, mod => mod.OnOrbDetonation(this));
        }

        /// <summary>爆炸处强追踪迷你光束</summary>
        private void SpawnDetonationMinions(int detonationDamage) {
            int n = DetonationMinions;
            //衍生用球原始伤，不再折
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
                    Main.projectile[idx].ai[1] = 2.5f; //强追踪
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

            float sizeRatio = State == OrbState.Charging
                ? 0.2f + chargeRatio * 0.8f
                : 1f;
            float orbDiameterPx = MaxOrbDiameter * sizeRatio;

            //黄金→白青，超驱品红
            float od = overdriveAmount;
            Color currentCore = Color.Lerp(
                Color.Lerp(ChargeCore, FullCore, chargeRatio), ODCore, od);
            Color currentGlow = Color.Lerp(
                Color.Lerp(ChargeGlow, FullGlow, chargeRatio), ODGlow, od);
            Color currentAura = Color.Lerp(
                Color.Lerp(ChargeAura, FullAura, chargeRatio), ODAura, od);

            float pulse = 0.92f + 0.08f * MathF.Sin((float)Main.timeForVisualEffects * 0.12f + chargeRatio * 5f);
            //超驱微脉冲
            pulse += od * 0.18f * MathF.Sin((float)Main.timeForVisualEffects * 0.45f);
            pulse += od * glitchBurstIntensity * 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 1.2f);
            float alpha = fadeAlpha * pulse;
            Vector2 glowOrigin = glow.Size() * 0.5f;

            //外 bloom 收敛，热靠色相
            float outerScale = (orbDiameterPx / glow.Width) * (2.2f + od * 1.6f);
            Color outerColor = currentAura * alpha * (0.18f + od * 0.30f);
            spriteBatch.Draw(glow, drawPos, null, outerColor, 0f,
                glowOrigin, outerScale, SpriteEffects.None, 0f);

            //CyberEnergyOrb
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

            //恢复 Additive+Deferred
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        #endregion

        public override bool ShouldUpdatePosition() => State == OrbState.Flying;

        /// <summary>停蓄力循环音</summary>
        private void StopChargeSound() {
            if (SoundEngine.TryGetActiveSound(chargeSoundSlot, out var sound)) {
                sound.Stop();
            }
        }
    }
}
