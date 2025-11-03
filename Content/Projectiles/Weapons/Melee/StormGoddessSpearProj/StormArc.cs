using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.StormGoddessSpearProj
{
    /// <summary>
    /// 风暴电弧，较小的连锁闪电，用于二次打击和追踪效果
    /// </summary>
    internal class StormArc : Lightning
    {
        public override string Texture => CWRConstant.Placeholder;

        #region 配置参数 - 比主闪电更小更快
        public override int MaxBranches => 2; //更少的分叉
        public override float BranchProbability => 0.08f; //更低的分叉概率
        public override float BranchLengthRatio => 0.35f; //更短的分叉
        public override float BaseSpeed => 22f; //更快的速度
        public override int LingerTime => 18; //更短的停留时间
        public override int FadeTime => 12; //更快的消失
        public override float BaseWidth => 28f; //更细的闪电
        public override float MinBranchWidthRatio => 0.3f;
        public override float MaxBranchWidthRatio => 0.6f;
        #endregion

        #region 自定义属性
        /// <summary>追踪的目标NPC索引列表</summary>
        private HashSet<int> hitNPCs = new HashSet<int>();

        /// <summary>连锁次数</summary>
        private int chainCount = 0;

        /// <summary>最大连锁次数</summary>
        private int maxChains => 3 + (int)Intensity;

        /// <summary>连锁搜索半径</summary>
        private float chainRadius = 500f; //增加搜索半径

        /// <summary>当前追踪的目标</summary>
        private NPC currentTarget = null;

        /// <summary>是否已经尝试过连锁</summary>
        private bool hasAttemptedChain = false;
        #endregion

        #region 基础设置
        public override void SetLightningDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1; //无限穿透，由连锁次数控制
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //每个敌人只能命中一次
            Projectile.width = 18;
            Projectile.height = 18;
            Intensity = 0.85f; //稍低的强度
        }
        #endregion

        private int dontDmgTimer;
        private bool setDontDmgTimer;
        public override bool? CanDamage() {
            if (dontDmgTimer > 0) {
                return false;
            }
            return base.CanDamage();
        }

        #region 颜色系统 - 改为白蓝色系
        public override Color GetLightningColor(float factor) {
            //使用白蓝色调（比主闪电更亮）
            Color baseColor = new Color(180, 220, 255); //明亮的白蓝色

            //添加电弧特有的闪烁效果
            float sparkle = 0.88f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 28f + Projectile.identity * 3f);

            //根据连锁次数调整颜色（连锁越多越亮）
            float chainBrightness = 1f + chainCount * 0.08f;

            //添加白色高光
            Color highlightColor = Color.Lerp(baseColor, Color.White, 0.3f);

            return highlightColor * sparkle * chainBrightness;
        }

        public override float GetLightningWidth(float factor) {
            //更细更快的电弧
            float curve = MathF.Sin(factor * MathHelper.Pi);
            float shapeFactor = curve * (0.7f + 0.3f * MathF.Sin(factor * MathHelper.Pi));

            //添加高频震颤
            float vibration = 1f + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 40f + factor * 20f);

            return ThunderWidth * shapeFactor * Intensity * vibration;
        }

        public override float GetAlpha(float factor) {
            if (factor < FadeValue)
                return 0;

            float baseAlpha = ThunderAlpha * (factor - FadeValue) / (1 - FadeValue);

            //快速闪烁效果
            float flicker = 1f - 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 35f + factor * 25f);

            return baseAlpha * (0.9f + 0.1f * Intensity) * flicker;
        }
        #endregion

        #region 目标寻找 - 连锁逻辑
        public override Vector2 FindTargetPosition() {
            //寻找最近的有效NPC（排除已命中的）
            currentTarget = FindClosestValidNPC();

            if (currentTarget != null) {
                return currentTarget.Center;
            }

            //如果没有找到目标，向前方射出
            return Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitY) * 400f;
        }

        /// <summary>
        /// 寻找最近的有效NPC（排除已命中的）
        /// </summary>
        private NPC FindClosestValidNPC() {
            NPC closest = null;
            float closestDistance = chainRadius;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (IsValidTarget(npc)) {
                    float distance = Vector2.Distance(Projectile.Center, npc.Center);
                    if (distance < closestDistance) {
                        closestDistance = distance;
                        closest = npc;
                    }
                }
            }

            return closest;
        }

        /// <summary>
        /// 寻找下一个连锁目标（从指定位置搜索）
        /// </summary>
        private NPC FindNextChainTarget(Vector2 fromPosition) {
            NPC nextTarget = null;
            float closestDistance = chainRadius;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (IsValidTarget(npc)) {
                    float distance = Vector2.Distance(fromPosition, npc.Center);
                    if (distance < closestDistance) {
                        closestDistance = distance;
                        nextTarget = npc;
                    }
                }
            }

            return nextTarget;
        }

        /// <summary>
        /// 检查NPC是否是有效目标
        /// </summary>
        private bool IsValidTarget(NPC npc) {
            return npc != null &&
                   npc.active &&
                   npc.CanBeChasedBy() &&
                   !npc.friendly &&
                   npc.life > 0 &&
                   !hitNPCs.Contains(npc.whoAmI);
        }
        #endregion

        #region 特效
        public override void OnStrike() {
            //播放较轻的电击音效
            SoundEngine.PlaySound(SoundID.Item94 with {
                Volume = 0.5f,
                Pitch = 0.3f,
                PitchVariance = 0.2f
            }, Projectile.Center);

            //生成小范围的冲击粒子
            if (!VaultUtils.isServer) {
                SpawnArcImpactParticles();
            }
        }

        public override void OnHit() {
            //这个方法在基类的 StartLinger 中被调用
            //不需要在这里处理连锁，连锁在 OnHitNPC 中处理
        }

        /// <summary>
        /// 生成电弧冲击粒子
        /// </summary>
        private void SpawnArcImpactParticles() {
            Color particleColor = GetLightningColor(0.5f);

            //生成环形粒子
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 12f);
                BasePRT particle = new PRT_Light(
                    Projectile.Center,
                    velocity,
                    0.25f,
                    particleColor,
                    Main.rand.Next(6, 12),
                    0.9f,
                    1.3f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }

        /// <summary>
        /// 尝试进行连锁 - 从指定位置生成新电弧
        /// </summary>
        private void AttemptChain(Vector2 fromPosition) {
            //防止重复触发
            if (hasAttemptedChain) return;
            hasAttemptedChain = true;

            //检查是否还能继续连锁
            if (chainCount >= maxChains) {
                return;
            }

            //寻找下一个目标
            NPC nextTarget = FindNextChainTarget(fromPosition);

            if (nextTarget != null) {
                //创建新的电弧到下一个目标
                Vector2 directionToNext = (nextTarget.Center - fromPosition).SafeNormalize(Vector2.UnitY);

                Projectile arc = Projectile.NewProjectileDirect(
                    Projectile.GetSource_FromThis(),
                    fromPosition,
                    directionToNext * BaseSpeed,
                    Type,
                    (int)(Projectile.damage * 0.85f), //每次连锁伤害衰减15%
                    Projectile.knockBack * 0.7f,
                    Projectile.owner,
                    ai0: 0,
                    ai1: 0,
                    ai2: chainCount + 1 //传递连锁次数
                );

                //传递已命中列表和连锁数据
                StormArc arcModProj = arc.ModProjectile as StormArc;
                if (arcModProj != null) {
                    //复制已命中列表
                    arcModProj.hitNPCs = new HashSet<int>(hitNPCs);
                    arcModProj.chainCount = chainCount + 1;
                    arcModProj.Intensity = Math.Max(0.5f, Intensity - 0.1f); //降低强度
                }

                //生成连锁视觉效果
                if (!VaultUtils.isServer) {
                    SpawnChainEffects(fromPosition, nextTarget.Center);
                }
            }
        }

        /// <summary>
        /// 生成连锁特效
        /// </summary>
        private void SpawnChainEffects(Vector2 from, Vector2 to) {
            Color chainColor = GetLightningColor(0.5f);

            //在连锁路径上生成粒子
            int particleCount = (int)(Vector2.Distance(from, to) / 40f);
            for (int i = 0; i < particleCount; i++) {
                float progress = i / (float)particleCount;
                Vector2 pos = Vector2.Lerp(from, to, progress);

                BasePRT particle = new PRT_Spark(
                    pos,
                    Main.rand.NextVector2Circular(3f, 3f),
                    false,
                    Main.rand.Next(5, 10),
                    0.9f,
                    chainColor * 0.8f,
                    Main.player[Projectile.owner]
                );
                PRTLoader.AddParticle(particle);
            }

            //播放连锁音效
            SoundEngine.PlaySound(SoundID.Item93 with {
                Volume = 0.4f,
                Pitch = 0.5f + chainCount * 0.1f
            }, from);
        }

        protected override void UpdateStrikeMovement() {
            //更激进的追踪
            float baseSpeed = Projectile.velocity.Length();

            //基础朝向
            float selfAngle = Projectile.velocity.ToRotation();
            float targetAngle = (TargetPosition - Projectile.Center).ToRotation();

            //更强的追踪（99%跟随目标）
            float newAngle = MathHelper.Lerp(selfAngle, targetAngle, 0.99f);

            //非常小的扰动（电弧几乎是直的）
            float sinOffset = MathF.Sin(Timer * 0.5f) * 0.1f;
            newAngle += sinOffset;

            //偶尔的轻微抖动
            if (Timer % 5 == 0) {
                float randomAngle = Main.rand.NextFloat(-0.1f, 0.1f);
                newAngle += randomAngle;
            }

            Projectile.velocity = newAngle.ToRotationVector2() * baseSpeed;

            //轻微位置抖动
            Projectile.position += new Vector2(
                MathF.Sin(Timer * 0.4f),
                MathF.Cos(Timer * 0.35f)
            ) * 0.5f;
        }
        #endregion

        #region AI逻辑
        public override void AI() {
            if (dontDmgTimer > 0) {
                dontDmgTimer--;
            }

            if (!setDontDmgTimer) {
                setDontDmgTimer = true;
                if (Projectile.IsOwnedByLocalPlayer()) {
                    dontDmgTimer = Main.rand.Next(16);
                    Projectile.netUpdate = true;
                }               
            }

            //从ai[2]恢复连锁次数
            if (chainCount == 0 && Projectile.ai[2] > 0) {
                chainCount = (int)Projectile.ai[2];
            }

            base.AI();

            //添加光源（白蓝色光）
            Color lightColor = GetLightningColor(0.5f);
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * Intensity * 0.6f);

            //在飞行过程中生成轨迹粒子
            if (State == (float)LightningState.Striking && Timer % 4 == 0 && !VaultUtils.isServer) {
                BasePRT particle = new PRT_Light(
                    Projectile.Center + Main.rand.NextVector2Circular(8, 8),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    0.15f,
                    lightColor * 0.5f,
                    Main.rand.Next(4, 8),
                    0.7f,
                    1f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }
        #endregion

        #region 命中效果
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitNPC(target, hit, damageDone);

            //记录已命中的NPC
            hitNPCs.Add(target.whoAmI);

            //添加电击Debuff（时间较短）
            target.AddBuff(BuffID.Electrified, 120);

            //生成命中粒子
            if (!VaultUtils.isServer) {
                Color particleColor = GetLightningColor(0.5f);
                for (int i = 0; i < Main.rand.Next(3, 6); i++) {
                    Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(8f, 15f);
                    BasePRT particle = new PRT_Spark(
                        target.Center + Main.rand.NextVector2Circular(target.width * 0.3f, target.height * 0.3f),
                        velocity,
                        false,
                        Main.rand.Next(5, 10),
                        1.1f,
                        particleColor,
                        Main.player[Projectile.owner]
                    );
                    PRTLoader.AddParticle(particle);
                }
            }

            //在命中敌人后立即尝试连锁
            //只在本地客户端执行
            if (Projectile.IsOwnedByLocalPlayer() && !hasAttemptedChain) {
                AttemptChain(target.Center);
            }
        }

        protected override void StartLinger() {
            base.StartLinger();

            //记录当前目标
            if (currentTarget != null && currentTarget.active) {
                hitNPCs.Add(currentTarget.whoAmI);
            }

            //如果到达目标但没有命中（比如碰到地面），尝试从停留位置连锁
            if (Projectile.IsOwnedByLocalPlayer() && !hasAttemptedChain) {
                AttemptChain(Projectile.Center);
            }
        }
        #endregion

        #region 网络同步
        public override void SendExtraAI(System.IO.BinaryWriter writer) {
            base.SendExtraAI(writer);
            writer.Write(dontDmgTimer);
            writer.Write(chainCount);
            writer.Write(hasAttemptedChain);
            writer.Write(hitNPCs.Count);
            foreach (int npcIndex in hitNPCs) {
                writer.Write(npcIndex);
            }
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            dontDmgTimer = reader.ReadInt32();
            chainCount = reader.ReadInt32();
            hasAttemptedChain = reader.ReadBoolean();
            int count = reader.ReadInt32();
            hitNPCs.Clear();
            for (int i = 0; i < count; i++) {
                hitNPCs.Add(reader.ReadInt32());
            }
        }
        #endregion
    }
}
