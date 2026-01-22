using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 审判奇迹的神圣雷霆 - 金色十字架闪电
    /// </summary>
    internal class JudgmentLightning : Lightning
    {
        public override string Texture => CWRConstant.Placeholder;

        #region 配置参数
        public override int MaxBranches => 6; //更多分叉形成十字架形状
        public override float BranchProbability => 0.25f; //较高分叉概率
        public override float BranchLengthRatio => 0.4f; //较短分叉
        public override float BaseSpeed => 22f; //较快速度
        public override int LingerTime => 45; //较长停留时间
        public override int FadeTime => 25; //较长消失时间
        public override float BaseWidth => 50f; //适中宽度
        public override float MinBranchWidthRatio => 0.4f;
        public override float MaxBranchWidthRatio => 0.7f;
        #endregion

        #region 自定义属性
        /// <summary>是否已生成十字架冲击</summary>
        private bool hasSpawnedCrossImpact = false;

        /// <summary>十字架分叉已生成数量</summary>
        private int crossBranchCount = 0;

        /// <summary>十字架分叉生成的位置记录</summary>
        private List<Vector2> crossBranchPositions = new();

        /// <summary>目标NPC的whoAmI（通过ai[2]传入）</summary>
        private int targetNPCIndex = -1;
        #endregion

        #region 基础设置
        public override void SetLightningDefaults() {
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //只命中一次
            Projectile.width = 30;
            Projectile.height = 30;
        }
        #endregion

        public override void OnSpawn(IEntitySource source) {
            //从ai[2]获取目标NPC索引
            targetNPCIndex = (int)Projectile.ai[2];
            Projectile.netUpdate = true;
        }

        #region 颜色系统 - 神圣金色
        public override Color GetLightningColor(float factor) {
            //神圣金色主题：从纯白到金色的渐变
            Color coreColor = new Color(255, 255, 220); //白金色核心
            Color outerColor = new Color(255, 200, 50); //金色外层

            //根据位置从核心到外层渐变
            float curve = MathF.Sin(factor * MathHelper.Pi);
            Color baseColor = Color.Lerp(outerColor, coreColor, curve * 0.6f);

            //添加神圣脉冲效果
            float pulseIntensity = 0.9f + 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 15f + Projectile.identity);

            //添加轻微的彩虹色调（神圣光芒）
            float hueShift = MathF.Sin(factor * MathHelper.Pi * 2f + Main.GlobalTimeWrappedHourly * 5f) * 0.03f;
            Vector3 hsl = Main.rgbToHsl(baseColor);
            hsl.X = (hsl.X + hueShift + 1f) % 1f;
            Color shiftedColor = Main.hslToRgb(hsl);

            return shiftedColor * pulseIntensity;
        }

        public override float GetLightningWidth(float factor) {
            //十字架闪电的宽度曲线 - 中部较粗
            float curve = MathF.Sin(factor * MathHelper.Pi);
            float pulse = 1f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 18f + factor * 8f);
            float shapeFactor = curve * (0.55f + 0.45f * MathF.Sin(factor * MathHelper.Pi * 0.5f));

            return ThunderWidth * shapeFactor * Intensity * pulse;
        }

        public override float GetAlpha(float factor) {
            if (factor < FadeValue)
                return 0;

            float baseAlpha = ThunderAlpha * (factor - FadeValue) / (1 - FadeValue);
            float pulse = 1f - 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 25f + factor * 15f);

            return baseAlpha * (0.88f + 0.12f * Intensity) * pulse;
        }
        #endregion

        #region 目标寻找
        public override Vector2 FindTargetPosition() {
            //如果有指定目标NPC
            if (targetNPCIndex >= 0 && targetNPCIndex < Main.maxNPCs) {
                NPC target = Main.npc[targetNPCIndex];
                if (target.active && target.CanBeChasedBy()) {
                    return target.Center;
                }
            }

            //否则寻找最近的敌人
            NPC closestNPC = Projectile.Center.FindClosestNPC(800f, true, true);
            if (closestNPC != null) {
                return closestNPC.Center;
            }

            //默认向下
            return Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitY) * 500f;
        }
        #endregion

        #region 十字架分叉生成
        protected override void CreateBranch() {
            if (LightningTexture == null || TrailPoints.Count < 5) return;

            var points = TrailPoints.ToArray();

            //从较早的位置选择分叉点
            int maxIndex = (int)(points.Length * 0.7f);
            int branchIndex = Main.rand.Next(Math.Max(5, points.Length / 4), maxIndex);
            Vector2 branchStart = points[branchIndex];

            //检查是否太靠近已有的十字架分叉
            bool tooClose = false;
            foreach (var pos in crossBranchPositions) {
                if (Vector2.Distance(pos, branchStart) < 60f) {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose && crossBranchCount < 4) return;

            crossBranchPositions.Add(branchStart);
            crossBranchCount++;

            List<Vector2> branchPoints = new List<Vector2> { branchStart };

            //计算主干方向
            Vector2 mainDirection = Projectile.velocity.SafeNormalize(Vector2.UnitY);

            //十字架分叉：垂直于主方向（形成十字）
            float baseAngle = mainDirection.ToRotation();

            //交替生成左右分叉
            float sideSign = (crossBranchCount % 2 == 0) ? 1 : -1;
            float branchAngle = baseAngle + sideSign * MathHelper.PiOver2; //垂直方向
            branchAngle += Main.rand.NextFloat(-0.2f, 0.2f); //轻微随机偏移

            //分叉长度
            int branchLength = (int)(TrailPoints.Count * BranchLengthRatio * Main.rand.NextFloat(0.6f, 0.9f));
            branchLength = Math.Max(6, Math.Min(branchLength, 80));

            Vector2 currentPos = branchStart;
            Vector2 branchDirection = branchAngle.ToRotationVector2();

            for (int i = 0; i < branchLength; i++) {
                float progressFactor = i / (float)branchLength;

                //保持较直的路径（十字架的横臂应该较直）
                float randomOffset = Main.rand.NextFloat(-6f, 6f) * (1f - progressFactor * 0.3f);
                Vector2 perpendicular = branchDirection.RotatedBy(MathHelper.PiOver2);

                float stepSize = Main.rand.NextFloat(8f, 12f) * (1f - progressFactor * 0.2f);
                currentPos += branchDirection * stepSize + perpendicular * randomOffset;
                branchPoints.Add(currentPos);

                //随机提前结束
                if (Main.rand.NextFloat() < 0.02f + progressFactor * 0.05f) break;
            }

            if (branchPoints.Count > 3) {
                float widthRatio = Main.rand.NextFloat(MinBranchWidthRatio, MaxBranchWidthRatio);

                InnoVault.Trails.ThunderTrail branch = new InnoVault.Trails.ThunderTrail(LightningTexture,
                    factor => GetLightningWidth(factor) * widthRatio * 0.75f,
                    factor => GetLightningColor(factor) * Main.rand.NextFloat(0.8f, 1f),
                    GetAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 2,
                    BasePositions = branchPoints.ToArray()
                };
                branch.SetRange((0, 4));
                branch.SetExpandWidth(3);
                branch.RandomThunder();

                BranchTrails.Add(branch);
            }
        }
        #endregion

        #region 特效
        public override void OnStrike() {
            if (Projectile.numHits == 0) {
                //播放神圣雷击音效
                SoundStyle sound = SoundID.Item122 with {
                    Volume = 1f,
                    Pitch = 0.3f, //较高音调表示神圣
                    PitchVariance = 0.1f,
                    MaxInstances = 2,
                };
                SoundEngine.PlaySound(sound, Projectile.Center);

                //播放额外的神圣音效
                SoundEngine.PlaySound(SoundID.Item29 with {
                    Volume = 0.7f,
                    Pitch = 0.5f
                }, Projectile.Center);
            }

            //生成十字架冲击粒子
            if (!VaultUtils.isServer) {
                SpawnCrossImpactParticles();
            }
        }

        public override void OnHit() {
            if (Projectile.IsOwnedByLocalPlayer() && !hasSpawnedCrossImpact) {
                hasSpawnedCrossImpact = true;
                SpawnHolyCrossEffect();
            }
        }

        /// <summary>
        /// 生成十字架形状的冲击粒子
        /// </summary>
        private void SpawnCrossImpactParticles() {
            Color goldColor = new Color(255, 215, 100);
            Color whiteColor = new Color(255, 255, 240);

            //生成十字架形状的粒子爆发
            //垂直方向
            for (int i = 0; i < 20; i++) {
                float speed = Main.rand.NextFloat(8f, 20f);
                float upDown = Main.rand.NextBool() ? 1 : -1;
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), upDown * speed);

                BasePRT particle = new PRT_Light(
                    Projectile.Center + Main.rand.NextVector2Circular(10, 10),
                    velocity,
                    0.35f,
                    Color.Lerp(goldColor, whiteColor, Main.rand.NextFloat(0.3f)),
                    Main.rand.Next(15, 25),
                    1.1f,
                    1.8f,
                    hueShift: Main.rand.NextFloat(-0.02f, 0.02f)
                );
                PRTLoader.AddParticle(particle);
            }

            //水平方向
            for (int i = 0; i < 15; i++) {
                float speed = Main.rand.NextFloat(6f, 16f);
                float leftRight = Main.rand.NextBool() ? 1 : -1;
                Vector2 velocity = new Vector2(leftRight * speed, Main.rand.NextFloat(-2f, 2f));

                BasePRT particle = new PRT_Light(
                    Projectile.Center + Main.rand.NextVector2Circular(10, 10),
                    velocity,
                    0.3f,
                    Color.Lerp(goldColor, whiteColor, Main.rand.NextFloat(0.4f)),
                    Main.rand.Next(12, 20),
                    1f,
                    1.5f,
                    hueShift: Main.rand.NextFloat(-0.02f, 0.02f)
                );
                PRTLoader.AddParticle(particle);
            }

            //生成环形神圣光芒
            int ringCount = 16;
            for (int i = 0; i < ringCount; i++) {
                float angle = MathHelper.TwoPi * i / ringCount;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 18f);

                BasePRT particle = new PRT_Spark(
                    Projectile.Center,
                    velocity,
                    false,
                    Main.rand.Next(10, 18),
                    1.3f,
                    goldColor * 0.9f,
                    Main.player[Projectile.owner]
                );
                PRTLoader.AddParticle(particle);
            }

            //生成中心爆发
            for (int i = 0; i < 25; i++) {
                Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(5f, 15f);
                BasePRT particle = new PRT_Light(
                    Projectile.Center,
                    velocity,
                    0.25f,
                    whiteColor,
                    Main.rand.Next(8, 15),
                    0.9f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }

        /// <summary>
        /// 生成神圣十字架效果
        /// </summary>
        private void SpawnHolyCrossEffect() {
            //在命中位置生成持续的十字架光芒粒子
            Vector2 center = Projectile.Center;

            //生成十字架形状的Dust
            for (int arm = 0; arm < 4; arm++) {
                float baseAngle = MathHelper.PiOver2 * arm;
                for (int i = 1; i <= 8; i++) {
                    Vector2 pos = center + baseAngle.ToRotationVector2() * (i * 12f);
                    Vector2 vel = baseAngle.ToRotationVector2() * 3f;

                    Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame, vel, 100, Color.White, 1.5f - i * 0.1f);
                    d.noGravity = true;
                }
            }
        }

        protected override void UpdateStrikeMovement() {
            float baseSpeed = Projectile.velocity.Length();
            float distance = Projectile.Center.Distance(TargetPosition);

            //基础朝向
            float selfAngle = Projectile.velocity.ToRotation();
            float targetAngle = (TargetPosition - Projectile.Center).ToRotation();
            float trackingFactor = 1 - Math.Clamp(distance / 400, 0f, 1f);

            //非常强的追踪（神圣审判必中）
            float newAngle = MathHelper.Lerp(selfAngle, targetAngle, 0.9f + 0.1f * trackingFactor);

            //较小的扰动（神圣雷霆较直）
            float sinOffset = MathF.Sin(Timer * 0.3f) * 0.35f;
            newAngle += sinOffset;

            //较少的随机抖动
            if (Timer % 8 == 0) {
                float randomAngle = Main.rand.NextFloat(-0.25f, 0.25f);
                newAngle += randomAngle;
            }

            Projectile.velocity = newAngle.ToRotationVector2() * baseSpeed;

            //轻微位置抖动
            Projectile.position += new Vector2(
                MathF.Sin(Timer * 0.2f),
                MathF.Cos(Timer * 0.15f)
            ) * 1.2f;
        }
        #endregion

        #region 额外AI逻辑
        public override void AI() {
            base.AI();

            //添加金色光源
            Color lightColor = GetLightningColor(0.5f);
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * Intensity * 1.2f);

            //在劈击过程中生成路径粒子（金色）
            if (State == (float)LightningState.Striking && Timer % 4 == 0 && !VaultUtils.isServer) {
                Vector2 particlePos = Projectile.Center + Main.rand.NextVector2Circular(12, 12);
                Vector2 particleVel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f);

                BasePRT particle = new PRT_Light(
                    particlePos,
                    particleVel,
                    0.12f,
                    new Color(255, 220, 100) * 0.7f,
                    Main.rand.Next(6, 12),
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

            //添加神圣灼烧效果（使用On Fire作为替代）
            target.AddBuff(BuffID.OnFire3, 180); //地狱火

            //生成命中的神圣粒子
            if (!VaultUtils.isServer) {
                Color goldColor = new Color(255, 215, 100);
                for (int i = 0; i < Main.rand.Next(8, 15); i++) {
                    Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(8f, 18f);
                    BasePRT particle = new PRT_Light(
                        target.Center,
                        velocity,
                        0.28f,
                        goldColor,
                        Main.rand.Next(10, 18),
                        0.9f,
                        1.4f,
                        hueShift: 0f
                    );
                    PRTLoader.AddParticle(particle);
                }

                //显示"审判"文字
                CombatText.NewText(target.Hitbox, goldColor, "审判", true);
            }
        }
        #endregion
    }
}
