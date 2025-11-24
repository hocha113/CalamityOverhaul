using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
{
    internal class BrinyBaronHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<BrinyBaron>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "BrinyBaron_Bar";
        private float momentumPower; //动量力度
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 86;
            canDrawSlashTrail = true;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            drawTrailCount = 10;
            distanceToOwner = 60;
            ownerOrientationLock = true;
            SwingData.baseSwingSpeed = 4.6f;
            IgnoreImpactBoxSize = true;
            autoSetShoot = true;
            drawTrailHighlight = true;
        }

        public override bool PreInOwner() {
            //动量衰减
            momentumPower *= 0.92f;

            if (Projectile.ai[0] == 1) {
                //强力上挑斩
                distanceToOwner = 105;
                SwingAIType = SwingAITypeEnum.None;
                SwingData.starArg = 70;
                SwingData.baseSwingSpeed = 7.5f;
                SwingData.ler1_UpLengthSengs = 0.15f;
                SwingData.ler1_UpSizeSengs = 0.08f;
                SwingData.minClampLength = 95;
                SwingData.maxClampLength = 140;

                //挥舞音效
                if (Time == 0) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.1f, Volume = 1.2f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Pitch = 0.3f }, Owner.Center);
                    momentumPower = 15f;
                }

                ExecuteAdaptiveSwing(
                    initialMeleeSize: 1.05f,
                    phase1Ratio: 0.15f,
                    phase2Ratio: 0.55f,
                    phase0SwingSpeed: 2.5f,
                    phase1SwingSpeed: 10f,
                    phase2SwingSpeed: 4f,
                    phase0MeleeSizeIncrement: 0.015f,
                    phase2MeleeSizeIncrement: 0,
                    swingSound: SoundID.Item71
                );

                //水花粒子拖尾
                if (Time % 2 == 0 && rotSpeed > 0.15f) {
                    for (int i = 0; i < 2; i++) {
                        Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(60, 60);
                        Dust water = Dust.NewDustPerfect(dustPos, DustID.DungeonWater,
                            -Projectile.velocity * 0.5f, 0, default, 2f);
                        water.noGravity = true;
                        water.velocity += Owner.velocity * 0.5f;
                    }
                }
            }
            else if (Projectile.ai[0] == 2) {
                //蓄力水爆
                if (Time == 0) {
                    SoundEngine.PlaySound(SoundID.Item88 with { Pitch = 0.1f, Volume = 1.3f }, Owner.Center);
                    SoundEngine.PlaySound(SoundID.Splash, Owner.Center);
                    momentumPower = 20f;
                }

                SwingAIType = SwingAITypeEnum.None;
                shootSengs = 0.22f; //更早射击
                maxSwingTime = 60; //缩短总时长
                canDrawSlashTrail = false;
                SwingData.starArg = 15;
                SwingData.baseSwingSpeed = 2.8f;
                SwingData.ler1_UpLengthSengs = 0.14f;
                SwingData.ler1_UpSpeedSengs = 0.12f;
                SwingData.ler1_UpSizeSengs = 0.018f;
                SwingData.ler2_DownLengthSengs = 0.08f;
                SwingData.ler2_DownSpeedSengs = 0.28f;
                SwingData.maxSwingTime = 35;

                //蓄力进度
                float chargeProgress = MathHelper.Clamp(Time / 30f, 0f, 1f);

                //蓄力粒子环绕
                if (Time % 3 == 0) {
                    float angle = Time * 0.2f;
                    Vector2 offset = angle.ToRotationVector2() * (50 + chargeProgress * 30);
                    Dust bubble = Dust.NewDustPerfect(Projectile.Center + offset, DustID.DungeonWater,
                        -offset * 0.08f, 0, default, 1.8f + chargeProgress * 0.5f);
                    bubble.noGravity = true;
                }

                //蓄力完成气泡
                if (chargeProgress >= 0.7f && Time % 4 == 0) {
                    Gore orb = Gore.NewGorePerfect(
                        Projectile.GetSource_FromAI(),
                        Projectile.Center + Main.rand.NextVector2Circular(70, 70),
                        Main.rand.NextVector2Circular(2, 2),
                        Main.rand.NextBool() ? 411 : 412
                    );
                    orb.timeLeft = 15;
                    orb.scale = 1.2f + chargeProgress * 0.3f;
                }

                //发射瞬间效果
                if (Time == (int)(maxSwingTime * shootSengs)) {
                    SoundEngine.PlaySound(SoundID.Item96 with { Pitch = -0.3f, Volume = 1.2f }, Owner.Center);
                }
            }
            else {
                //普通连击
                if (Time == 0) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, Owner.Center);
                    momentumPower = 8f;
                }

                //普通攻击水花
                if (Time % 3 == 0 && Main.rand.NextBool()) {
                    Dust splash = Dust.NewDustDirect(Projectile.position, Projectile.width,
                        Projectile.height, DustID.DungeonWater);
                    splash.velocity *= 0.6f;
                    splash.scale = 1.5f;
                    splash.noGravity = true;
                }
            }

            return true;
        }

        public override void Shoot() {
            int type = ModContent.ProjectileType<Razorwind>();

            if (Projectile.ai[0] == 1) {
                type = ProjectileID.Bubble;
                SoundEngine.PlaySound(SoundID.Item85 with { Pitch = 0.3f, Volume = 1.1f }, Owner.Center);

                for (int i = 0; i < 28; i++) {
                    Vector2 ver = UnitToMouseV.RotatedByRandom(1.7f) * ShootSpeed * Main.rand.NextFloat(0.7f, 1.3f);
                    int proj = Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        Owner.Center + ver * 65f,
                        ver,
                        type,
                        Projectile.damage / 2,
                        2,
                        Owner.whoAmI
                    );
                    Main.projectile[proj].DamageType = DamageClass.Melee;
                }

                //气泡爆发音效
                SoundEngine.PlaySound(SoundID.Item54, Owner.Center);
                return;
            }

            if (Projectile.ai[0] == 2) {
                //水球终结技
                SoundEngine.PlaySound(SoundID.Item20 with { MaxInstances = 6, Volume = 1.3f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item96 with { Pitch = -0.2f }, Owner.Center);

                type = ModContent.ProjectileType<BrinyBaronOrb>();

                //发射3个强力水球
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        Owner.Center,
                        safeInSwingUnit.RotatedBy((-1 + i) * 0.12f) * 14,
                        type,
                        (int)(Projectile.damage * 0.85f),
                        3f,
                        Owner.whoAmI
                    );
                }

                //水花爆发环
                int constant = 45;
                for (int j = 0; j < constant; j++) {
                    Vector2 vr = (MathHelper.TwoPi / constant * j).ToRotationVector2() * 15;
                    int dust = Dust.NewDust(Projectile.Center, 0, 0, DustID.DungeonWater, 0, 0, 100, default, 1.8f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = vr;
                }

                //气泡环绕
                for (int k = 0; k < 15; k++) {
                    Gore bubble = Gore.NewGorePerfect(
                        Projectile.GetSource_FromAI(),
                        Projectile.Center,
                        Main.rand.NextVector2Circular(8, 8),
                        Main.rand.NextBool() ? 411 : 412
                    );
                    bubble.timeLeft = 20;
                    bubble.scale = Main.rand.NextFloat(0.8f, 1.4f);
                }

                Owner.CWR().GetScreenShake(8f);
                return;
            }

            //普通攻击
            SoundEngine.PlaySound(SoundID.Item84 with { Pitch = 0.1f }, Owner.Center);
            Projectile.NewProjectile(
                Projectile.GetSource_FromAI(),
                Owner.Center,
                ShootVelocity * 1.2f,
                type,
                (int)(Projectile.damage * 0.55f),
                Projectile.knockBack * 0.6f,
                Owner.whoAmI
            );

            //普通攻击水花
            for (int i = 0; i < 5; i++) {
                Dust water = Dust.NewDustDirect(Owner.Center, 20, 20, DustID.DungeonWater,
                    ShootVelocity.X * 0.3f, ShootVelocity.Y * 0.3f);
                water.noGravity = true;
                water.scale = 1.3f;
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //击中音效
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = 0.2f, Volume = 0.8f }, target.Center);

            //基于攻击类型的效果
            if (Projectile.ai[0] == 2) {
                //终结技击中
                SoundEngine.PlaySound(SoundID.Item96, target.Center);

                //强力水花爆发
                for (int i = 0; i < 25; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(8, 8);
                    Dust explosion = Dust.NewDustPerfect(target.Center, DustID.DungeonWater, vel, 0, default, 2.5f);
                    explosion.noGravity = true;
                }

                //气泡爆炸
                for (int i = 0; i < 10; i++) {
                    Gore bubble = Gore.NewGorePerfect(
                        Projectile.GetSource_FromAI(),
                        target.Center,
                        Main.rand.NextVector2Circular(6, 6),
                        Main.rand.NextBool() ? 411 : 412
                    );
                    bubble.timeLeft = 15;
                    bubble.scale = Main.rand.NextFloat(1f, 1.5f);
                }
            }
            else if (Projectile.ai[0] == 1) {
                for (int i = 0; i < 12; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(6, 6);
                    Dust water = Dust.NewDustPerfect(target.Center, DustID.DungeonWater, vel, 0, default, 2f);
                    water.noGravity = true;
                }

                //气泡
                for (int i = 0; i < 5; i++) {
                    Gore bubble = Gore.NewGorePerfect(
                        Projectile.GetSource_FromAI(),
                        target.Center,
                        Main.rand.NextVector2Circular(4, 4),
                        Main.rand.NextBool() ? 411 : 412
                    );
                    bubble.timeLeft = 12;
                    bubble.scale = Main.rand.NextFloat(0.7f, 1.2f);
                }
            }
            else {
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(4, 4);
                    Dust splash = Dust.NewDustPerfect(target.Center, DustID.DungeonWater, vel, 0, default, 1.5f);
                    splash.noGravity = true;
                }
            }

            //击中粒子动量
            momentumPower += 3f;
        }

        public override void MeleeEffect() {
            //基于动量的额外粒子效果
            if (momentumPower > 5f && Main.rand.NextBool(3)) {
                float intensity = MathHelper.Clamp(momentumPower / 20f, 0f, 1f);
                Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(50, 50) * intensity;
                Dust trail = Dust.NewDustPerfect(pos, DustID.DungeonWater, Vector2.Zero, 0, default, 1.5f * intensity);
                trail.noGravity = true;
                trail.velocity = Owner.velocity * 0.3f;
            }
        }
    }
}
