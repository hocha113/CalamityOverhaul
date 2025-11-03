using CalamityMod;
using CalamityOverhaul.Content.Projectiles;
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
    /// 风暴电弧 - 较小的连锁闪电，用于二次打击和追踪效果
    /// </summary>
    internal class StormArc : Lightning
    {
        public override string Texture => CWRConstant.Placeholder;

        #region 配置参数 - 比主闪电更小更快
        public override int MaxBranches => 2; // 更少的分叉
        public override float BranchProbability => 0.08f; // 更低的分叉概率
        public override float BranchLengthRatio => 0.35f; // 更短的分叉
        public override float BaseSpeed => 22f; // 更快的速度
        public override int LingerTime => 18; // 更短的停留时间
        public override int FadeTime => 12; // 更快的消失
        public override float BaseWidth => 28f; // 更细的闪电
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
        private float chainRadius = 400f;
        
        /// <summary>当前追踪的目标</summary>
        private NPC currentTarget = null;
        #endregion

        #region 基础设置
        public override void SetLightningDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1; // 无限穿透，由连锁次数控制
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // 每个敌人只能命中一次
            Projectile.width = 18;
            Projectile.height = 18;
            Intensity = 0.85f; // 稍低的强度
        }
        #endregion

        #region 颜色系统
        public override Color GetLightningColor(float factor) {
            // 使用青蓝色调，稍微偏紫
            Color baseColor = new Color(143, 235, 255); // 更亮的青蓝色
            
            // 添加电弧特有的闪烁效果
            float sparkle = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 25f + Projectile.identity * 3f);
            
            // 根据连锁次数调整颜色（越多连锁越亮）
            float chainBrightness = 1f + chainCount * 0.1f;
            
            return baseColor * sparkle * chainBrightness;
        }

        public override float GetLightningWidth(float factor) {
            // 更细更快的电弧
            float curve = MathF.Sin(factor * MathHelper.Pi);
            float shapeFactor = curve * (0.7f + 0.3f * MathF.Sin(factor * MathHelper.Pi));
            
            // 添加高频震颤
            float vibration = 1f + 0.08f * MathF.Sin(Main.GlobalTimeWrappedHourly * 40f + factor * 20f);
            
            return ThunderWidth * shapeFactor * Intensity * vibration;
        }

        public override float GetAlpha(float factor) {
            if (factor < FadeValue)
                return 0;

            float baseAlpha = ThunderAlpha * (factor - FadeValue) / (1 - FadeValue);
            
            // 快速闪烁效果
            float flicker = 1f - 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 35f + factor * 25f);
            
            return baseAlpha * (0.9f + 0.1f * Intensity) * flicker;
        }
        #endregion

        #region 目标寻找 - 连锁逻辑
        public override Vector2 FindTargetPosition() {
            // 如果是新生成的电弧，寻找最近的敌人
            if (chainCount == 0) {
                currentTarget = Projectile.Center.FindClosestNPC(chainRadius, true, true);
                if (currentTarget != null) {
                    return currentTarget.Center;
                }
            }
            else {
                // 连锁模式：寻找下一个目标
                currentTarget = FindNextChainTarget();
                if (currentTarget != null) {
                    chainCount++;
                    return currentTarget.Center;
                }
            }

            // 如果没有找到目标，向前方射出
            return Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitY) * 300f;
        }

        /// <summary>
        /// 寻找下一个连锁目标
        /// </summary>
        private NPC FindNextChainTarget() {
            if (currentTarget == null || !currentTarget.active) {
                return null;
            }

            NPC nextTarget = null;
            float closestDistance = chainRadius;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (IsValidTarget(npc) && !hitNPCs.Contains(npc.whoAmI)) {
                    float distance = Vector2.Distance(currentTarget.Center, npc.Center);
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
                   !hitNPCs.Contains(npc.whoAmI);
        }
        #endregion

        #region 特效
        public override void OnStrike() {
            // 播放较轻的电击音效
            SoundEngine.PlaySound(SoundID.Item94 with { 
                Volume = 0.5f, 
                Pitch = 0.3f,
                PitchVariance = 0.2f 
            }, Projectile.Center);

            // 生成小范围的冲击粒子
            if (!VaultUtils.isServer) {
                SpawnArcImpactParticles();
            }
        }

        public override void OnHit() {
            // 尝试进行连锁
            if (chainCount < maxChains && Projectile.IsOwnedByLocalPlayer()) {
                AttemptChain();
            }
        }

        /// <summary>
        /// 生成电弧冲击粒子
        /// </summary>
        private void SpawnArcImpactParticles() {
            Color particleColor = GetLightningColor(0.5f);
            
            // 生成环形粒子
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
        /// 尝试进行连锁
        /// </summary>
        private void AttemptChain() {
            NPC nextTarget = FindNextChainTarget();
            
            if (nextTarget != null) {
                // 创建新的电弧到下一个目标
                Vector2 directionToNext = (nextTarget.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                
                Projectile arc = Projectile.NewProjectileDirect(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    directionToNext * BaseSpeed,
                    Type,
                    (int)(Projectile.damage * 0.85f), // 每次连锁伤害衰减
                    Projectile.knockBack * 0.7f,
                    Projectile.owner,
                    ai0: 0,
                    ai1: 0,
                    ai2: chainCount + 1 // 传递连锁次数
                );
                
                // 传递已命中列表
                StormArc arcModProj = arc.ModProjectile as StormArc;
                if (arcModProj != null) {
                    arcModProj.hitNPCs = new HashSet<int>(hitNPCs);
                    arcModProj.chainCount = chainCount + 1;
                    arcModProj.Intensity = Math.Max(0.5f, Intensity - 0.1f); // 降低强度
                }
            }
        }

        protected override void UpdateStrikeMovement() {
            // 更激进的追踪
            float baseSpeed = Projectile.velocity.Length();
            float distance = Projectile.Center.Distance(TargetPosition);

            // 基础朝向
            float selfAngle = Projectile.velocity.ToRotation();
            float targetAngle = (TargetPosition - Projectile.Center).ToRotation();
            
            // 更强的追踪
            float newAngle = MathHelper.Lerp(selfAngle, targetAngle, 0.95f);

            // 较小的扰动（电弧更直）
            float sinOffset = MathF.Sin(Timer * 0.5f) * 0.2f;
            newAngle += sinOffset;

            // 高频抖动
            if (Timer % 3 == 0) {
                float randomAngle = Main.rand.NextFloat(-0.2f, 0.2f);
                newAngle += randomAngle;
            }

            Projectile.velocity = newAngle.ToRotationVector2() * baseSpeed;

            // 轻微位置抖动
            Projectile.position += new Vector2(
                MathF.Sin(Timer * 0.4f), 
                MathF.Cos(Timer * 0.35f)
            ) * 0.8f;
        }
        #endregion

        #region AI逻辑
        public override void AI() {
            // 从ai[2]恢复连锁次数
            if (chainCount == 0 && Projectile.ai[2] > 0) {
                chainCount = (int)Projectile.ai[2];
            }

            base.AI();

            // 添加光源
            Color lightColor = GetLightningColor(0.5f);
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * Intensity * 0.6f);

            // 在飞行过程中生成轨迹粒子
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

            // 记录已命中的NPC
            hitNPCs.Add(target.whoAmI);

            // 添加电击Debuff（时间较短）
            target.AddBuff(BuffID.Electrified, 120);

            // 生成命中粒子
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
        }

        protected override void StartLinger() {
            base.StartLinger();
            
            // 记录当前目标
            if (currentTarget != null && currentTarget.active) {
                hitNPCs.Add(currentTarget.whoAmI);
            }
        }
        #endregion

        #region 网络同步
        public override void SendExtraAI(System.IO.BinaryWriter writer) {
            base.SendExtraAI(writer);
            writer.Write(chainCount);
            writer.Write(hitNPCs.Count);
            foreach (int npcIndex in hitNPCs) {
                writer.Write(npcIndex);
            }
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader) {
            base.ReceiveExtraAI(reader);
            chainCount = reader.ReadInt32();
            int count = reader.ReadInt32();
            hitNPCs.Clear();
            for (int i = 0; i < count; i++) {
                hitNPCs.Add(reader.ReadInt32());
            }
        }
        #endregion
    }
}
