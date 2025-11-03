using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.StormGoddessSpearProj
{
    ///<summary>
    ///风暴女神之矛的主要闪电弹幕
    ///</summary>
    internal class StormLightning : Lightning
    {
        public override string Texture => CWRConstant.Placeholder;

        #region 配置参数
        public override int MaxBranches => 4; //增加分叉数
        public override float BranchProbability => 0.15f; //提高分叉概率
        public override float BranchLengthRatio => 0.6f; //更长的分叉
        public override float BaseSpeed => 18f; //更快的速度
        public override int LingerTime => 30; //更长的停留时间
        public override int FadeTime => 20; //更长的消失时间
        public override float BaseWidth => 55f; //更粗的闪电
        public override float MinBranchWidthRatio => 0.5f;
        public override float MaxBranchWidthRatio => 0.8f;
        #endregion

        #region 自定义属性
        ///<summary>闪电颜色风格（通过ai[2]传入）</summary>
        private int ColorStyle => (int)Projectile.ai[2];
        
        ///<summary>是否已产生冲击波</summary>
        private bool hasSpawnedShockwave = false;
        #endregion

        #region 基础设置
        public override void SetLightningDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.width = 30;
            Projectile.height = 30;
        }
        #endregion

        #region 颜色系统
        public override Color GetLightningColor(float factor) {
            //根据风格返回不同颜色
            Color baseColor = ColorStyle switch {
                1 => new Color(103, 255, 255), //青蓝色（默认）
                2 => new Color(255, 255, 103), //金黄色
                3 => new Color(255, 103, 255), //洋红色
                _ => new Color(103, 255, 255)
            };

            //添加闪烁效果
            float pulseIntensity = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 15f + Projectile.identity);
            
            //根据位置添加颜色变化
            float hueShift = MathF.Sin(factor * MathHelper.Pi) * 0.15f;
            
            return baseColor * pulseIntensity * (0.9f + hueShift);
        }

        public override float GetLightningWidth(float factor) {
            //更动态的宽度变化
            float curve = MathF.Sin(factor * MathHelper.Pi);
            float pulse = 1f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 20f + factor * 10f);
            float shapeFactor = curve * (0.5f + 0.5f * MathF.Sin(factor * MathHelper.Pi * 0.5f));
            
            return ThunderWidth * shapeFactor * Intensity * pulse;
        }

        public override float GetAlpha(float factor) {
            if (factor < FadeValue)
                return 0;

            float baseAlpha = ThunderAlpha * (factor - FadeValue) / (1 - FadeValue);
            
            //添加脉冲透明度
            float pulse = 1f - 0.1f * MathF.Sin(Main.GlobalTimeWrappedHourly * 25f + factor * 15f);
            
            return baseAlpha * (0.8f + 0.2f * Intensity) * pulse;
        }
        #endregion

        #region 目标寻找
        public override Vector2 FindTargetPosition() {
            //优先寻找NPC目标
            NPC closestNPC = null;
            float closestDistance = 1200f;

            closestNPC = Projectile.Center.FindClosestNPC(closestDistance, true, true);

            //如果找到NPC，返回其中心
            if (closestNPC != null) {
                return closestNPC.Center;
            }

            //否则，向下寻找地面
            Vector2 searchPos = Projectile.Center;
            Vector2 direction = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            
            for (int i = 0; i < 100; i++) {
                searchPos += direction * 16f;
                Point tilePos = searchPos.ToTileCoordinates();
                
                if (WorldGen.InWorld(tilePos.X, tilePos.Y)) {
                    Tile tile = Main.tile[tilePos];
                    if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                        return searchPos;
                    }
                }
            }

            //默认返回远处位置
            return Projectile.Center + direction * 800f;
        }
        #endregion

        #region 特效
        public override void OnStrike() {
            //播放雷击音效
            SoundEngine.PlaySound(SoundID.Item122 with { 
                Volume = 0.8f, 
                Pitch = -0.2f,
                PitchVariance = 0.15f 
            }, Projectile.Center);

            //生成冲击波粒子
            if (!VaultUtils.isServer) {
                SpawnImpactParticles();
            }
        }

        public override void OnHit() {
            //命中时生成电弧
            if (Projectile.IsOwnedByLocalPlayer() && !hasSpawnedShockwave) {
                hasSpawnedShockwave = true;
                SpawnElectricArcs();
            }
        }

        ///<summary>
        ///生成冲击粒子特效
        ///</summary>
        private void SpawnImpactParticles() {
            Color particleColor = GetLightningColor(0.5f);

            //生成环形冲击波粒子
            for (int i = 0; i < 16; i++) {
                float angle = MathHelper.TwoPi * i / 16f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(12f, 20f);
                BasePRT particle = new PRT_Spark(
                    Projectile.Center,
                    velocity,
                    false,
                    Main.rand.Next(8, 15),
                    1.5f,
                    particleColor * 0.8f,
                    Main.player[Projectile.owner]
                );
                PRTLoader.AddParticle(particle);
            }
        }

        ///<summary>
        ///生成连锁电弧
        ///</summary>
        private void SpawnElectricArcs() {
            const float arcSpread = MathHelper.TwoPi / 5f;
            int arcCount = Main.rand.Next(4, 7);

            for (int i = 0; i < arcCount; i++) {
                float angle = arcSpread * i + Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(12f, 18f);
                
                Projectile arc = Projectile.NewProjectileDirect(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    velocity,
                    ModContent.ProjectileType<StormArc>(),
                    (int)(Projectile.damage * 0.5f),
                    Projectile.knockBack * 0.5f,
                    Projectile.owner
                );
                
                arc.timeLeft = Main.rand.Next(25, 40);
                arc.penetrate = 3;
                arc.tileCollide = true;
            }
        }

        protected override void UpdateStrikeMovement() {
            //重写移动逻辑，增加更多随机性
            float baseSpeed = Projectile.velocity.Length();
            float distance = Projectile.Center.Distance(TargetPosition);

            //基础朝向
            float selfAngle = Projectile.velocity.ToRotation();
            float targetAngle = (TargetPosition - Projectile.Center).ToRotation();
            float trackingFactor = 1 - Math.Clamp(distance / 600, 0f, 1f);

            //角度插值，更激进的追踪
            float newAngle = MathHelper.Lerp(selfAngle, targetAngle, 0.85f + 0.15f * trackingFactor);

            //更大的扰动
            float sinOffset = MathF.Sin(Timer * 0.4f) * 0.6f;
            newAngle += sinOffset;

            //增加随机抖动频率
            if (Timer % 5 == 0) {
                float randomAngle = Main.rand.NextFloat(-0.5f, 0.5f);
                newAngle += randomAngle;
            }

            Projectile.velocity = newAngle.ToRotationVector2() * baseSpeed;

            //位置抖动
            Projectile.position += new Vector2(
                MathF.Sin(Timer * 0.3f), 
                MathF.Cos(Timer * 0.25f)
            ) * 2f;
        }
        #endregion

        #region 额外AI逻辑
        public override void AI() {
            base.AI();

            //添加光源效果，颜色随状态变化
            Color lightColor = GetLightningColor(0.5f);
            Lighting.AddLight(Projectile.Center, lightColor.ToVector3() * Intensity);

            //在劈击过程中生成路径粒子
            if (State == (float)LightningState.Striking && Timer % 3 == 0 && !VaultUtils.isServer) {
                Vector2 particlePos = Projectile.Center + Main.rand.NextVector2Circular(15, 15);
                Vector2 particleVel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 8f);
                
                BasePRT particle = new PRT_Light(
                    particlePos,
                    particleVel,
                    0.2f,
                    lightColor * 0.6f,
                    Main.rand.Next(5, 12),
                    0.8f,
                    1.2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }
        #endregion

        #region 增强的命中效果
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitNPC(target, hit, damageDone);

            //添加电击Debuff
            target.AddBuff(BuffID.Electrified, 180);

            //生成命中粒子
            if (!VaultUtils.isServer) {
                Color particleColor = GetLightningColor(0.5f);
                for (int i = 0; i < Main.rand.Next(5, 10); i++) {
                    Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(10f, 20f);
                    BasePRT particle = new PRT_Light(
                        target.Center,
                        velocity,
                        0.3f,
                        particleColor,
                        Main.rand.Next(8, 15),
                        1f,
                        1.5f,
                        hueShift: 0f
                    );
                    PRTLoader.AddParticle(particle);
                }
            }
        }
        #endregion
    }
}
