using CalamityMod;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.StormGoddessSpearProj
{
    /// <summary>
    /// 风暴女神之矛的持握弹幕
    /// </summary>
    internal class StormGoddessSpearHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Items.Melee.StormGoddessSpear>();
        public override Texture2D TextureValue => CWRUtils.GetT2DValue(CWRConstant.Projectile_Melee + "StormGoddessSpearProj");
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "Greentide_Bar";

        #region 长矛属性
        /// <summary>
        /// 连击计数
        /// </summary>
        private int comboCounter = 0;
        /// <summary>
        /// 是否已发射闪电
        /// </summary>
        private bool hasSpawnedLightning = false;
        /// <summary>
        /// 闪电颜色风格
        /// </summary>
        private int lightningColorStyle = 0;
        #endregion

        public override void SetKnifeProperty() {
            AnimationMaxFrme = 8;
            Projectile.width = Projectile.height = 50;
            canDrawSlashTrail = true;
            drawTrailHighlight = false;
            distanceToOwner = 25;
            drawTrailBtommWidth = 60;
            drawTrailTopWidth = 150;
            drawTrailCount = 8;
            Length = 65;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            ShootSpeed = 18f;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void KnifeInitialize() {
            //根据ai[0]设置不同的攻击模式
            comboCounter = (int)Projectile.ai[0] % 3;
            hasSpawnedLightning = false;

            //循环颜色风格（统一为白蓝色系）
            lightningColorStyle = (comboCounter % 3) + 1;
        }

        public override bool PreSwingAI() {
            //第一击：快速突刺
            if (comboCounter == 0) {
                StabBehavior(
                    initialLength: 120,
                    lifetime: 24,
                    scaleFactorDenominator: 480f,
                    maxLength: 204,
                    canDrawSlashTrail: true
                );

                //在突刺过程中生成电火花（更细致）
                if (Time < 8 * UpdateRate && !VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Vector2 sparkPos = Projectile.Center + Projectile.velocity.UnitVector() * Length * 0.7f;
                    BasePRT spark = new PRT_Spark(
                        sparkPos,
                        Projectile.velocity * 0.3f,
                        false,
                        4,
                        0.8f, //更小的粒子
                        GetLightningColorForStyle(lightningColorStyle),
                        Owner
                    );
                    PRTLoader.AddParticle(spark);
                }

                return false;
            }
            //第二击和第三击使用正常挥舞
            if (comboCounter == 1) {
                //横扫攻击
                ExecuteAdaptiveSwing(
                    initialMeleeSize: 0.9f,
                    phase1Ratio: 0.45f,
                    phase2Ratio: 0.65f,
                    phase0SwingSpeed: -0.35f,
                    phase1SwingSpeed: 5f,
                    phase2SwingSpeed: 10f,
                    phase0MeleeSizeIncrement: 0.003f,
                    phase2MeleeSizeIncrement: -0.002f,
                    swingSound: SoundID.Item1,
                    drawSlash: true
                );
            }
            else if (comboCounter == 2) {
                //上挑攻击
                ExecuteAdaptiveSwing(
                    initialMeleeSize: 0.95f,
                    phase1Ratio: 0.4f,
                    phase2Ratio: 0.7f,
                    phase0SwingSpeed: -0.4f,
                    phase1SwingSpeed: 5.5f,
                    phase2SwingSpeed: 14f,
                    phase0MeleeSizeIncrement: 0.004f,
                    phase2MeleeSizeIncrement: -0.003f,
                    swingSound: SoundID.Item1,
                    drawSlash: true
                );
            }
            return true;
        }

        public override void Shoot() {
            if (hasSpawnedLightning) return;
            hasSpawnedLightning = true;

            //根据不同连击发射不同效果的闪电
            if (comboCounter == 0) {
                //第一击：单个精准闪电（细长型）
                SpawnPlayerLightning(
                    ShootVelocity, 
                    1f, 
                    lightningColorStyle, 
                    false, 
                    widthScale: 0.7f //70%宽度
                );
            }
            else if (comboCounter == 1) {
                //第二击：三道优雅扇形闪电（设计感分布）
                for (int i = -1; i <= 1; i++) {
                    //黄金角度分布：中间更密，两侧更开
                    float angle = i * 0.25f * (1f + MathF.Abs(i) * 0.2f);
                    Vector2 velocity = ShootVelocity.RotatedBy(angle);
                    
                    SpawnPlayerLightning(
                        velocity, 
                        0.65f, 
                        lightningColorStyle, 
                        true,
                        widthScale: 0.65f, //65%宽度
                        speedScale: 0.9f
                    );
                }
            }
            else if (comboCounter == 2) {
                //第三击：螺旋上升闪电阵（更有设计感）
                bool hasAdrenaline = Owner.Calamity().adrenalineModeActive;
                int count = hasAdrenaline ? 7 : 0;
                float damageMultiplier = hasAdrenaline ? 0.85f : 0.6f;

                for (int i = 0; i < count; i++) {
                    //螺旋分布，而非均匀圆形
                    float progress = i / (float)count;
                    float spiralAngle = MathHelper.TwoPi * progress + progress * MathHelper.PiOver4;
                    float radiusOffset = 0.8f + progress * 0.4f; //螺旋扩散
                    
                    Vector2 velocity = spiralAngle.ToRotationVector2() * ShootSpeed * radiusOffset;

                    SpawnPlayerLightning(
                        velocity, 
                        damageMultiplier, 
                        lightningColorStyle, 
                        true,
                        widthScale: 0.6f, //60%宽度
                        speedScale: 0.85f + progress * 0.3f //渐进速度
                    );
                }

                //减少冲击波粒子
                if (!VaultUtils.isServer) {
                    SpawnShockwaveParticles();
                }

                //播放音效
                SoundEngine.PlaySound(SoundID.DD2_LightningBugZap with {
                    Volume = 0.6f,
                    Pitch = -0.3f
                }, ShootSpanPos);
            }
        }

        /// <summary>
        /// 生成玩家闪电（优化版 - 更细致）
        /// </summary>
        private void SpawnPlayerLightning(
            Vector2 velocity, 
            float damageMultiplier, 
            int colorStyle, 
            bool disableHoming,
            float widthScale = 1f,
            float speedScale = 1f) {
            
            //使用ai[2]传递宽度缩放：原值 + 1000 * widthScale
            //例如：colorStyle=1, widthScale=0.7 → ai2 = 1 + 700 = 701
            int ai2Value = colorStyle;
            if (disableHoming) ai2Value += 100;
            ai2Value += (int)(1000 * widthScale); //编码宽度缩放

            Projectile.NewProjectile(
                Source,
                ShootSpanPos,
                velocity * speedScale,
                ModContent.ProjectileType<StormLightning>(),
                (int)(Projectile.damage * damageMultiplier),
                Projectile.knockBack * 0.7f,
                Owner.whoAmI,
                ai0: 0,
                ai1: 0,
                ai2: ai2Value
            );
        }

        /// <summary>
        /// 生成冲击波粒子（优化版 - 减少数量）
        /// </summary>
        private void SpawnShockwaveParticles() {
            Color particleColor = GetLightningColorForStyle(lightningColorStyle);

            //环形冲击波（减少密度）
            for (int i = 0; i < 12; i++) {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(12f, 20f);
                BasePRT particle = new PRT_Light(
                    ShootSpanPos,
                    velocity,
                    0.35f,
                    particleColor * 0.8f,
                    Main.rand.Next(15, 25),
                    1.2f,
                    1.6f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }

            //向上爆发的粒子（减少数量）
            for (int i = 0; i < 8; i++) {
                float angle = Main.rand.NextFloat(-MathHelper.PiOver4, MathHelper.PiOver4) - MathHelper.PiOver2;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(15f, 28f);
                BasePRT particle = new PRT_Spark(
                    ShootSpanPos,
                    velocity,
                    false,
                    Main.rand.Next(12, 20),
                    1.4f,
                    particleColor * 0.9f,
                    Owner
                );
                PRTLoader.AddParticle(particle);
            }
        }

        /// <summary>
        /// 获取指定风格的闪电颜色（统一为白蓝色系）
        /// </summary>
        private Color GetLightningColorForStyle(int style) {
            return style switch {
                1 => new Color(200, 230, 255), //亮白蓝（第一击）
                2 => new Color(150, 200, 255), //中蓝白（第二击）
                3 => new Color(100, 180, 255), //深蓝白（第三击）
                _ => new Color(180, 220, 255)  //默认白蓝
            };
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //添加电击效果
            target.AddBuff(BuffID.Electrified, 120);

            //暴击时生成额外的电弧（减少数量）
            if (hit.Crit) {
                SpawnCriticalArcs(target);
            }

            //生成命中粒子（减少密度）
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                SpawnHitParticles(target);
            }
        }

        /// <summary>
        /// 生成暴击电弧（优化版）
        /// </summary>
        private void SpawnCriticalArcs(NPC target) {
            if (!Projectile.IsOwnedByLocalPlayer()) return;

            int arcCount = Owner.Calamity().adrenalineModeActive ? 3 : 2; //减少数量

            for (int i = 0; i < arcCount; i++) {
                float angle = MathHelper.TwoPi * i / arcCount + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 16f);

                Projectile arc = Projectile.NewProjectileDirect(
                    Source,
                    target.Center,
                    velocity,
                    ModContent.ProjectileType<StormArc>(),
                    (int)(Projectile.damage * 0.35f),
                    Projectile.knockBack * 0.4f,
                    Owner.whoAmI
                );

                arc.timeLeft = 25;
                arc.penetrate = 2;
                arc.tileCollide = true;
            }
        }

        /// <summary>
        /// 生成命中粒子（优化版 - 减少数量）
        /// </summary>
        private void SpawnHitParticles(NPC target) {
            Color particleColor = GetLightningColorForStyle(lightningColorStyle);

            for (int i = 0; i < Main.rand.Next(4, 8); i++) {
                Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(6f, 14f);
                BasePRT particle = new PRT_Light(
                    target.Center + Main.rand.NextVector2Circular(target.width * 0.3f, target.height * 0.3f),
                    velocity,
                    0.25f,
                    particleColor * 0.9f,
                    Main.rand.Next(8, 15),
                    1f,
                    1.5f,
                    hueShift: Main.rand.NextFloat(-0.05f, 0.05f)
                );
                PRTLoader.AddParticle(particle);
            }

            //音效概率播放
            if (Main.rand.NextBool(3)) {
                SoundEngine.PlaySound(SoundID.Item94 with {
                    Volume = 0.4f,
                    Pitch = 0.2f
                }, target.Center);
            }
        }

        public override void MeleeEffect() {
            //在挥舞过程中生成电火花轨迹（减少频率）
            if (Time % 5 == 0 && !VaultUtils.isServer) {
                Vector2 tipPos = Projectile.Center + safeInSwingUnit * Length;
                Color particleColor = GetLightningColorForStyle(lightningColorStyle);

                BasePRT spark = new PRT_Spark(
                    tipPos + Main.rand.NextVector2Circular(3, 3),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    false,
                    Main.rand.Next(3, 5),
                    0.8f, //更小的粒子
                    particleColor * 0.7f,
                    Owner
                );
                PRTLoader.AddParticle(spark);
            }
        }
    }
}
