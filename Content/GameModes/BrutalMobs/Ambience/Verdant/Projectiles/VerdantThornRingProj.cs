using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Verdant.Projectiles
{
    /// <summary>
    /// 荆棘合拢圈：沼雾伏影的惩罚落地。ai[0]=起始半径 ai[1]=档位 ai[2]=藤隙中心角。
    /// 蓄势(荆棘影沿雾缘浮现颤动,≥45 帧)→合拢(影环缓慢向心收拢,判定=可见环带)→枯落(无害余韵)。
    /// 「藤隙」是生成瞬间锁定的具名安全缺口：缺口扇区内可见为空,判定亦为空。
    /// 与 JhVineLash 的差异：那是 NPC 定点直线抽打,这是环境雾团的径向收拢圈。
    /// 绘制在 <see cref="VerdantAmbientRender"/>（压在雾之上,全程可读）
    /// </summary>
    internal class VerdantThornRingProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>蓄势帧数（公平契约 ≥45，且叠加在雾团自身的滞留预告之后）</summary>
        internal const int WarnFrames = 46;
        /// <summary>合拢帧数，档位只调收拢速度不改形状</summary>
        private static readonly int[] ConvergeFramesByTier = [116, 96, 78];
        internal const int WitherFrames = 42;
        /// <summary>收拢终点半径（无安全圆心）</summary>
        private const float EndRadius = 26f;
        /// <summary>荆棘环带判定半宽（=可见荆棘影带半宽）</summary>
        private const float BandHalfWidth = 22f;
        /// <summary>藤隙半角（判定豁免宽度；绘制缺口略窄于此，不许视觉夸大安全区）</summary>
        internal const float GapHalfAngle = 0.5f;

        private float StartRadius => Math.Max(Projectile.ai[0], 60f);
        private int Tier => Math.Clamp((int)Projectile.ai[1], 1, 3);
        internal float GapCenter => Projectile.ai[2];

        private int ConvergeFrames => ConvergeFramesByTier[Tier - 1];
        private int TotalLife => WarnFrames + ConvergeFrames + WitherFrames;
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>合拢进度 0~1（缓入缓出）</summary>
        private float ConvergeProgress {
            get {
                int t = Elapsed - WarnFrames;
                if (t <= 0) {
                    return 0f;
                }
                if (t >= ConvergeFrames) {
                    return 1f;
                }
                float x = t / (float)ConvergeFrames;
                return x * x * (3f - 2f * x);
            }
        }

        /// <summary>当前环半径（判定与绘制同源）</summary>
        internal float CurrentRadius => MathHelper.Lerp(StartRadius, EndRadius, ConvergeProgress);

        /// <summary>枯落进度 0~1</summary>
        internal float WitherT {
            get {
                int t = Elapsed - WarnFrames - ConvergeFrames;
                return t <= 0 ? 0f : MathHelper.Clamp(t / (float)WitherFrames, 0f, 1f);
            }
        }

        /// <summary>整体可见度：蓄势渐显→合拢全显→枯落渐隐</summary>
        internal float VisualAlpha {
            get {
                int e = Elapsed;
                if (e < WarnFrames) {
                    return 0.75f * (e / (float)WarnFrames);
                }
                return MathHelper.Lerp(0.9f, 0f, WitherT);
            }
        }

        /// <summary>蓄势期颤动幅度（越接近出手越静，静即将动）</summary>
        internal float TrembleAmp {
            get {
                int e = Elapsed;
                if (e >= WarnFrames) {
                    return 0f;
                }
                return 4.2f * (1f - e / (float)WarnFrames);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 520;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = false;//合拢窗口内才置真
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            //SetDefaults 期还读不到 ai，先按最慢档位铺满，首帧按实际档位重定
            Projectile.timeLeft = WarnFrames + ConvergeFramesByTier[0] + WitherFrames;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    //荆棘影自雾中立起：湿藤拉紧声 + 低哑影动
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.4f, MaxInstances = 4 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.28f, Pitch = -0.7f, MaxInstances = 3 }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;
            //判定窗=可见合拢窗；Boss 中途在场则立即缴械（视觉照常走完）
            Projectile.hostile = elapsed >= WarnFrames && elapsed < WarnFrames + ConvergeFrames
                && !CWRWorld.HasBoss;

            if (Main.dedServ) {
                return;
            }

            if (elapsed == WarnFrames) {
                //合拢起手：破空的湿藤鞭音
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.55f, Pitch = -0.5f, MaxInstances = 4 }, Projectile.Center);
            }
            else if (elapsed > WarnFrames && elapsed < WarnFrames + ConvergeFrames) {
                //收拢途中持续的枝叶摩擦
                if (elapsed % 12 == 0) {
                    SoundEngine.PlaySound(SoundID.Grass with {
                        Volume = 0.26f,
                        Pitch = Main.rand.NextFloat(-0.55f, -0.2f),
                        MaxInstances = 5,
                    }, Projectile.Center);
                }
                //环带上稀疏叶屑（≤1 粒/2 帧）
                if (elapsed % 2 == 0) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    if (Math.Abs(MathHelper.WrapAngle(ang - GapCenter)) > GapHalfAngle) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * CurrentRadius,
                            DustID.JungleGrass, ang.ToRotationVector2() * -0.6f, 140, default, 0.9f);
                        dust.noGravity = true;
                    }
                }
            }
            else if (elapsed == WarnFrames + ConvergeFrames) {
                //枯落起手：干裂脆响
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 4 }, Projectile.Center);
            }
            else if (elapsed > WarnFrames + ConvergeFrames && elapsed % 2 == 0) {
                //枯落余韵：荆棘化作坠叶
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Dust dust = Dust.NewDustPerfect(Projectile.Center + ang.ToRotationVector2() * CurrentRadius,
                    DustID.JungleGrass, new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.6f, 1.6f)),
                    120, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = false;
            }
        }

        /// <summary>环带判定：|到心距 − 当前半径| ≤ 带半宽，藤隙扇区豁免；判定与可见影带同源</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Projectile.hostile) {
                return false;
            }
            Vector2 delta = targetHitbox.Center.ToVector2() - Projectile.Center;
            float dist = delta.Length();
            //+14 为玩家盒半尺寸的近似补偿
            if (Math.Abs(dist - CurrentRadius) > BandHalfWidth + 14f) {
                return false;
            }
            if (dist > 1f && Math.Abs(MathHelper.WrapAngle(delta.ToRotation() - GapCenter)) < GapHalfAngle) {
                return false;//藤隙豁免
            }
            return true;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Poisoned, 240);
        }

        //绘制全部在 VerdantAmbientRender（压在雾层之上保证全程可读）
        public override bool PreDraw(ref Color lightColor) => false;
    }
}
