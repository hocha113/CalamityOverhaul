using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【自然之核】材质：整块叶绿锭铸的巨阔剑。
    /// 签名：①原版叶绿球保留升级：每一斩掷出自然之核，
    /// 飞行中周期脉冲小荆棘 ②两拍重剑：起势撩斩接长举劈落，一招一式全是分量
    /// ③终结拍的核更大，命中处炸出缠根域，驻留减速并持续刺击
    /// </summary>
    internal class GsChlorophyteClaymore : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.ChlorophyteClaymore;

        protected override int HeldProjID => ModContent.ProjectileType<GsChlorophyteClaymoreHeld>();

        protected override int ComboBeats => 2;

        //重剑一招一式，续段窗口放宽
        protected override int ComboResetFrames => 70;

        protected override string GsDescFallback =>
            "Reforged: a two-beat greatblade; every swing hurls a verdant core that pulses thorns in flight, and the overhead finisher's core roots the ground where it strikes, slowing and stabbing whatever lingers";
        internal static readonly Color CoreBright = new(212, 255, 176); //叶脉亮绿
        internal static readonly Color CoreMain = new(84, 196, 96);     //叶绿锭体色
        internal static readonly Color CoreHot = new(168, 255, 72);     //核心炽绿

        //按原版 26 帧/斩 且核约隔斩一发估：52 帧内 2 斩 + 1 核 ≈ 3.0x；
        //本方案两拍循环 ~78 帧：斩 1.0+1.35、核 0.6x 两发（终结核 0.6x1.35）、
        //荆棘脉冲 ~0.5x、缠根域命中才有 ~0.5x → 约 4.6x/78 帧 ≈ 原版 103%~112%，
        //缠根域与脉冲是范围收益；底伤不动
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 自然之核手持：两拍重剑。0 起势撩斩 / 1 长举劈落（前压终结）。
    /// 每拍斩切爆发掷出自然之核。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsChlorophyteClaymoreHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.ChlorophyteClaymore;
        protected override int BeatCount => 2;
        protected override Color EdgeBright => GsChlorophyteClaymore.CoreBright;
        protected override Color BodyMain => GsChlorophyteClaymore.CoreMain;
        protected override Color HotAccent => GsChlorophyteClaymore.CoreHot;

        //巨剑：触及长、判定宽
        protected override float BaseReach => 132f;
        protected override float CollisionWidth => 48f;
        protected override float PointBlankRadius => 48f;

        private bool orbFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 撩斩：重剑起势，慢举厚出
            0 => new GsBroadBeat {
                Raise = 11, Hold = 3, Slash = 6, Recover = 15,
                RaiseBack = 2.0f, Follow = 1.1f, ReachScale = 1f, LeanAmp = 0.06f,
                DamageMult = 1f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = -0.22f,
            },
            //拍1 劈落：更长的举、死寂的滞、带前压的落
            _ => new GsBroadBeat {
                Raise = 14, Hold = 4, Slash = 7, Recover = 18,
                RaiseBack = 2.4f, Follow = 1.35f, ReachScale = 1.2f, LeanAmp = 0.1f,
                DamageMult = 1.35f, Hitstop = 3, LungeSpeed = 3.6f, SwingPitch = -0.38f,
            },
        };

        /// <summary>每拍斩切爆发掷核：终结拍的核更大且携缠根旗</summary>
        protected override void OnSlashBegin() {
            if (orbFired) {
                return;
            }
            orbFired = true;
            Vector2 dir = baseAngle.ToRotationVector2();
            int orbDamage = Math.Max(1, (int)(Projectile.damage * 0.6f));
            SpawnOwnedProj(ModContent.ProjectileType<GsChlorophyteClaymoreOrbProj>(),
                Hand + dir * (FullReach * 0.8f), dir * 8.5f, orbDamage,
                Projectile.knockBack * 0.4f, IsFinisher ? 1f : 0f);
        }

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.45f, Pitch = -0.5f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = -0.35f }, Owner.Center);
            }
        }
    }

    /// <summary>
    /// 自然之核：每斩掷出的叶绿能量球，用原版叶绿巨剑球贴图。出膛 8.5 减速滑行至 4 上下，
    /// 飞行中每 22 帧脉冲放出三根小荆棘；ai[0]=终结旗（核更大，首个命中生成缠根域）
    /// </summary>
    internal class GsChlorophyteClaymoreOrbProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ChlorophyteOrb;

        private bool Rooting => Projectile.ai[0] > 0.5f;
        private ref float Life => ref Projectile.localAI[0];
        private bool rootSpawned;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.ChlorophyteOrb];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 150;
        }

        public override void AI() {
            Life++;
            //减速滑行：8.5 → 约 4，核是重物不是弹头
            if (Projectile.velocity.Length() > 4f) {
                Projectile.velocity *= 0.985f;
            }
            Projectile.rotation += 0.04f * Math.Sign(Projectile.velocity.X == 0f ? 1f : Projectile.velocity.X);

            //周期脉冲：每 22 帧放三根小荆棘（owner 端生成随包过线）
            if (Life >= 14f && Life % 22f == 0f && Projectile.timeLeft > 20) {
                if (Projectile.owner == Main.myPlayer) {
                    int thornDamage = Math.Max(1, (int)(Projectile.damage * 0.18f));
                    float baseRot = Projectile.velocity.ToRotation();
                    for (int i = 0; i < 3; i++) {
                        //非匀速扇射：角度错开、速度参差
                        Vector2 vel = (baseRot + MathHelper.Lerp(-1.9f, 1.9f, i / 2f)
                            + Main.rand.NextFloat(-0.3f, 0.3f)).ToRotationVector2()
                            * Main.rand.NextFloat(3.2f, 6.4f);
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                            ModContent.ProjectileType<GsChlorophyteClaymoreThornProj>(),
                            thornDamage, 0f, Projectile.owner);
                    }
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.35f, Pitch = 0.3f }, Projectile.Center);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //终结核首个命中：在目标脚下生成缠根域
            if (Rooting && !rootSpawned && Projectile.owner == Main.myPlayer) {
                rootSpawned = true;
                int rootDamage = Math.Max(1, (int)(Projectile.damage * 0.22f));
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), target.Bottom, Vector2.Zero,
                    ModContent.ProjectileType<GsChlorophyteClaymoreRootProj>(),
                    rootDamage, 0f, Projectile.owner);
            }
        }
    }

    /// <summary>
    /// 脉冲小荆棘：核飞行中周期放出的绿刺，直线短程、速度参差，触砖即碎；用原版叶绿水晶叶贴图
    /// </summary>
    internal class GsChlorophyteClaymoreThornProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CrystalLeafShot;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.CrystalLeafShot];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 42;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            //刺出后轻微减速，末段渐钝
            Projectile.velocity *= 0.988f;
        }
    }

    /// <summary>
    /// 缠根域：终结核命中处竖起的驻留根网。150 帧寿命，22 帧一跳小伤，
    /// 域内敌人每帧速度衰减（缠根减速）；用原版荨麻藤贴图按根域判定拉伸画一笔作范围提示
    /// </summary>
    internal class GsChlorophyteClaymoreRootProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NettleBurstEnd;

        private const int TotalLife = 150;
        private const float Radius = 92f;
        private ref float Life => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.NettleBurstEnd];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 22;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.7f, Pitch = -0.4f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = -0.2f }, Projectile.Center);
            }

            //缠根减速：域内敌人速度衰减（逻辑各端一致跑，服务器权威生效）
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile) || npc.knockBackResist <= 0f) {
                    continue;
                }
                if (npc.Hitbox.Distance(Projectile.Center) <= Radius) {
                    npc.velocity *= 0.90f;
                }
            }
        }

        public override bool? CanDamage() => Life >= 4f && Projectile.timeLeft > 10 ? null : false;

        /// <summary>横扁的根域判定：宽圆减一点竖高</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 delta = targetHitbox.Center.ToVector2() - Projectile.Center;
            delta.Y *= 1.6f;
            return delta.Length() <= Radius;
        }

        /// <summary>范围提示：原版荨麻藤贴图按根域判定（宽 2R、高 1.25R）拉伸画一笔，破土 8 帧撑开、末段淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frame = tex.Frame(1, Math.Max(1, Main.projFrames[Type]), 0, 0);
            float grow = MathHelper.Clamp(Life / 8f, 0f, 1f);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            Vector2 stretch = new(Radius * 2f * grow / MathF.Max(frame.Width, 1), Radius * 1.25f * grow / MathF.Max(frame.Height, 1));
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, lightColor * (0.7f * fade),
                0f, frame.Size() * 0.5f, stretch, SpriteEffects.None, 0);
            return false;
        }
    }
}
