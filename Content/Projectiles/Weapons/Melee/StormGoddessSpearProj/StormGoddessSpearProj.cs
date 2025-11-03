using CalamityMod;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
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
        /// <summary>连击计数</summary>
        private int comboCounter = 0;
        /// <summary>是否已发射闪电</summary>
        private bool hasSpawnedLightning = false;
        /// <summary>闪电颜色风格</summary>
        private int lightningColorStyle = 0;
        #endregion

        public override void SetKnifeProperty() {
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
            
            //循环颜色风格
            lightningColorStyle = (comboCounter % 3) + 1;
        }

        public override bool PreSwingAI() {
            //第一击：快速突刺
            if (comboCounter == 0) {
                StabBehavior(
                    initialLength: 70, 
                    lifetime: 24, 
                    scaleFactorDenominator: 480f, 
                    maxLength: 125, 
                    canDrawSlashTrail: true
                );

                //在突刺过程中生成电火花
                if (Time < 8 * UpdateRate && !VaultUtils.isServer) {
                    Vector2 sparkPos = Projectile.Center + Projectile.velocity.UnitVector() * Length * 0.7f;
                    BasePRT spark = new PRT_Spark(
                        sparkPos, 
                        Projectile.velocity * 0.5f, 
                        false, 
                        5, 
                        1.5f, 
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
                    phase2SwingSpeed: 11f,
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
                //第一击：单个强力闪电
                SpawnMainLightning(1f, lightningColorStyle);
            }
            else if (comboCounter == 1) {
                //第二击：三道扇形闪电
                for (int i = -1; i <= 1; i++) {
                    Vector2 velocity = ShootVelocity.RotatedBy(i * 0.3f);
                    SpawnLightningProjectile(velocity, 0.7f, lightningColorStyle);
                }
            }
            else if (comboCounter == 2) {
                //第三击：五道环绕闪电（如果有肾上腺素）
                bool hasAdrenaline = Owner.Calamity().adrenalineModeActive;
                int count = hasAdrenaline ? 7 : 5;
                float damageMultiplier = hasAdrenaline ? 0.9f : 0.65f;

                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi * i / count + Main.rand.NextFloat(-0.15f, 0.15f);
                    Vector2 velocity = angle.ToRotationVector2() * ShootSpeed;
                    SpawnLightningProjectile(velocity, damageMultiplier, lightningColorStyle);
                }

                //额外生成冲击波效果
                if (!VaultUtils.isServer) {
                    SpawnShockwaveParticles();
                }
            }
        }

        /// <summary>
        /// 生成主闪电
        /// </summary>
        private void SpawnMainLightning(float damageMultiplier, int colorStyle) {
            Projectile.NewProjectileDirect(
                Source,
                ShootSpanPos,
                ShootVelocity,
                ModContent.ProjectileType<StormLightning>(),
                (int)(Projectile.damage * damageMultiplier),
                Projectile.knockBack,
                Owner.whoAmI,
                ai0: 0,
                ai1: 0,
                ai2: colorStyle
            );
        }

        /// <summary>
        /// 生成闪电弹幕
        /// </summary>
        private void SpawnLightningProjectile(Vector2 velocity, float damageMultiplier, int colorStyle) {
            Projectile.NewProjectile(
                Source,
                ShootSpanPos,
                velocity,
                ModContent.ProjectileType<StormLightning>(),
                (int)(Projectile.damage * damageMultiplier),
                Projectile.knockBack * 0.8f,
                Owner.whoAmI,
                ai0: 0,
                ai1: 0,
                ai2: colorStyle
            );
        }

        /// <summary>
        /// 生成冲击波粒子
        /// </summary>
        private void SpawnShockwaveParticles() {
            Color particleColor = GetLightningColorForStyle(lightningColorStyle);
            
            //环形冲击波
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(15f, 25f);
                BasePRT particle = new PRT_Light(
                    ShootSpanPos,
                    velocity,
                    0.4f,
                    particleColor,
                    Main.rand.Next(20, 30),
                    1.5f,
                    2f,
                    hueShift: 0f
                );
                PRTLoader.AddParticle(particle);
            }
        }

        /// <summary>
        /// 获取指定风格的闪电颜色
        /// </summary>
        private Color GetLightningColorForStyle(int style) {
            return style switch {
                1 => new Color(103, 255, 255), //青蓝色
                2 => new Color(255, 255, 103), //金黄色
                3 => new Color(255, 103, 255), //洋红色
                _ => new Color(103, 255, 255)
            };
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //添加电击效果
            target.AddBuff(BuffID.Electrified, 120);

            //暴击时生成额外的电弧
            if (hit.Crit) {
                SpawnCriticalArcs(target);
            }

            //生成命中粒子
            if (!VaultUtils.isServer) {
                SpawnHitParticles(target);
            }
        }

        /// <summary>
        /// 生成暴击电弧
        /// </summary>
        private void SpawnCriticalArcs(NPC target) {
            if (!Projectile.IsOwnedByLocalPlayer()) return;

            int arcCount = Owner.Calamity().adrenalineModeActive ? 5 : 3;
            
            for (int i = 0; i < arcCount; i++) {
                float angle = MathHelper.TwoPi * i / arcCount + Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(12f, 18f);
                
                Projectile arc = Projectile.NewProjectileDirect(
                    Source,
                    target.Center,
                    velocity,
                    ModContent.ProjectileType<StormArc>(),
                    (int)(Projectile.damage * 0.4f),
                    Projectile.knockBack * 0.5f,
                    Owner.whoAmI
                );
                
                arc.timeLeft = 30;
                arc.penetrate = 2;
                arc.tileCollide = true;
            }
        }

        /// <summary>
        /// 生成命中粒子
        /// </summary>
        private void SpawnHitParticles(NPC target) {
            Color particleColor = GetLightningColorForStyle(lightningColorStyle);
            
            for (int i = 0; i < Main.rand.Next(8, 15); i++) {
                Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(8f, 18f);
                BasePRT particle = new PRT_Light(
                    target.Center + Main.rand.NextVector2Circular(target.width * 0.5f, target.height * 0.5f),
                    velocity,
                    0.3f,
                    particleColor,
                    Main.rand.Next(10, 18),
                    1.2f,
                    1.8f,
                    hueShift: Main.rand.NextFloat(-0.05f, 0.05f)
                );
                PRTLoader.AddParticle(particle);
            }

            //播放电击音效
            SoundEngine.PlaySound(SoundID.Item94 with { 
                Volume = 0.5f, 
                Pitch = 0.2f 
            }, target.Center);
        }

        public override void MeleeEffect() {
            //在挥舞过程中生成电火花轨迹
            if (Time % 3 == 0 && !VaultUtils.isServer) {
                Vector2 tipPos = Projectile.Center + safeInSwingUnit * Length;
                Color particleColor = GetLightningColorForStyle(lightningColorStyle);
                
                BasePRT spark = new PRT_Spark(
                    tipPos + Main.rand.NextVector2Circular(5, 5),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f),
                    false,
                    Main.rand.Next(3, 6),
                    1f,
                    particleColor * 0.8f,
                    Owner
                );
                PRTLoader.AddParticle(spark);
            }
        }
    }
}
