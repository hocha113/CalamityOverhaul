using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【自然之核】材质：整块叶绿锭铸的巨阔剑，剑身走叶脉光路。
    /// 签名：①原版叶绿球保留升级：每一斩掷出自然之核（绿核+叶脉光晕），
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
            "Reforged: a two-beat greatblade; every swing hurls a verdant core that pulses thorns " +
            "in flight, and the overhead finisher's core roots the ground where it strikes, " +
            "slowing and stabbing whatever lingers";

        //叶绿重剑色板
        internal static readonly Color CoreBright = new(212, 255, 176); //叶脉亮绿
        internal static readonly Color CoreMain = new(84, 196, 96);     //叶绿锭体色
        internal static readonly Color CoreHot = new(168, 255, 72);     //核心炽绿
        internal static readonly Color CoreDeep = new(18, 44, 24);      //根须暗绿

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
        protected override Color DeepShadow => GsChlorophyteClaymore.CoreDeep;

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

        //叶绿巨剑常亮脉光
        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsChlorophyteClaymore.CoreHot : GsChlorophyteClaymore.CoreBright;
        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsChlorophyteClaymore.CoreMain, 0.15f);

        //重剑残影更沉
        protected override int GhostCount => IsFinisher ? 4 : 2;
        protected override float GhostSpacing => IsFinisher ? 0.26f : 0.2f;

        /// <summary>每拍斩切爆发掷核：终结拍的核更大且携缠根旗</summary>
        protected override void OnSlashBegin() {
            if (orbFired) {
                return;
            }
            orbFired = true;
            if (IsFinisher) {
                SetFlash(8);
            }
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

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (IsFinisher && phase <= PhaseHold) {
                //长举聚能：绿光尘自地面涌向剑身
                Vector2 hand = Hand;
                Vector2 at = hand + Main.rand.NextVector2Unit() * Main.rand.NextFloat(42f, 78f);
                PRTLoader.NewParticle<PRT_Light>(at, (Vector2.Lerp(hand, mainTip, 0.7f) - at) * 0.14f,
                    GsChlorophyteClaymore.CoreMain, Main.rand.NextFloat(0.07f, 0.12f))?.Configure(9, 0.6f);
            }
            else if (phase == PhaseSlash && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.6f, 1f)),
                    DustID.ChlorophyteWeapon, Vector2.Zero, 90, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
                d.velocity = (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2() * 2.2f;
            }
        }
    }

    /// <summary>
    /// 自然之核：每斩掷出的叶绿能量球。出膛 8.5 减速滑行至 4 上下，
    /// 飞行中每 22 帧脉冲放出三根小荆棘并闪一记环光；ai[0]=终结旗
    /// （核更大，首个命中生成缠根域）。绘制走确定性相位
    /// </summary>
    internal class GsChlorophyteClaymoreOrbProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private bool Rooting => Projectile.ai[0] > 0.5f;
        private float SizeMul => Rooting ? 1.35f : 1f;
        private ref float Life => ref Projectile.localAI[0];
        private ref float PulseFlash => ref Projectile.localAI[1];
        private bool rootSpawned;

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
            if (PulseFlash > 0f) {
                PulseFlash -= 0.1f;
            }

            //周期脉冲：每 22 帧放三根小荆棘（owner 端生成随包过线）
            if (Life >= 14f && Life % 22f == 0f && Projectile.timeLeft > 20) {
                PulseFlash = 1f;
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

            Lighting.AddLight(Projectile.Center, GsChlorophyteClaymore.CoreMain.ToVector3() * (0.5f * SizeMul));

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                //航迹叶尘
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.ChlorophyteWeapon, -Projectile.velocity * 0.15f, 120, default, Main.rand.NextFloat(0.6f, 1f));
                d.noGravity = true;
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
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f),
                        Main.rand.NextBool() ? GsChlorophyteClaymore.CoreHot : GsChlorophyteClaymore.CoreBright,
                        Main.rand.NextFloat(0.35f, 0.55f))?.Configure(true, Main.rand.Next(12, 20));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.5f, 1.5f),
                    GsChlorophyteClaymore.CoreMain, Main.rand.NextFloat(0.06f, 0.11f))?.Configure(12, 0.65f);
            }
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || star == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            //出生 5 帧撑满带过冲
            float grow = Life <= 5f ? 1.1f * (Life / 5f)
                : MathHelper.Lerp(1.1f, 1f, MathHelper.Clamp((Life - 5f) / 4f, 0f, 1f));
            //呼吸脉动 + 脉冲余闪
            float breath = 1f + 0.07f * MathF.Sin(Life * 0.32f) + 0.22f * PulseFlash;
            float s = SizeMul * grow * breath;
            Vector2 velDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);

            //旧位置残核
            for (int i = 1; i <= 3; i++) {
                Vector2 back = center - Projectile.velocity * (i * 2.2f);
                Color trail = GsChlorophyteClaymore.CoreMain * (0.13f * (1f - i / 4f));
                trail.A = 0;
                Main.EntitySpriteDraw(glow, back, null, trail, 0f, glow.Size() * 0.5f, 0.42f * s, SpriteEffects.None, 0);
            }

            //核体：暗绿底 + 绿核 + 炽绿心
            Color rim = GsChlorophyteClaymore.CoreDeep * 0.55f;
            Main.EntitySpriteDraw(glow, center, null, rim, 0f, glow.Size() * 0.5f, 0.52f * s, SpriteEffects.None, 0);
            Color body = GsChlorophyteClaymore.CoreMain * 0.75f;
            body.A = 0;
            Main.EntitySpriteDraw(glow, center, null, body, 0f, glow.Size() * 0.5f, 0.4f * s, SpriteEffects.None, 0);
            Color hot = GsChlorophyteClaymore.CoreHot * (0.8f + 0.2f * PulseFlash);
            hot.A = 0;
            Main.EntitySpriteDraw(glow, center, null, hot, 0f, glow.Size() * 0.5f, 0.2f * s, SpriteEffects.None, 0);

            //叶脉光晕：六根细芒绕核旋转，如叶脉展开
            for (int i = 0; i < 6; i++) {
                float ang = MathHelper.TwoPi * i / 6f + Life * 0.06f + SegRand(i) * 0.5f;
                Vector2 at = center + ang.ToRotationVector2() * (17f * s);
                Color vein = GsChlorophyteClaymore.CoreBright * (0.5f + 0.25f * PulseFlash);
                vein.A = 0;
                Main.EntitySpriteDraw(star, at, null, vein, ang, star.Size() * 0.5f,
                    new Vector2(0.34f, 0.1f) * s, SpriteEffects.None, 0);
            }

            //行进向的锋头微光
            Color head = GsChlorophyteClaymore.CoreBright * 0.35f;
            head.A = 0;
            Main.EntitySpriteDraw(glow, center + velDir * (14f * s), null, head, 0f,
                glow.Size() * 0.5f, 0.24f * s, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 脉冲小荆棘：核飞行中周期放出的绿刺，直线短程、速度参差，
    /// 触砖即碎。绘制为沿速度拉长的绿芒细刺
    /// </summary>
    internal class GsChlorophyteClaymoreThornProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Life => ref Projectile.localAI[0];

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
            Life++;
            Projectile.rotation = Projectile.velocity.ToRotation();
            //刺出后轻微减速，末段渐钝
            Projectile.velocity *= 0.988f;
            Lighting.AddLight(Projectile.Center, GsChlorophyteClaymore.CoreHot.ToVector3() * 0.15f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.ChlorophyteWeapon,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 2.5f), 100, default,
                    Main.rand.NextFloat(0.6f, 1f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (star == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f)
                * MathHelper.Clamp(Life / 3f, 0f, 1f);
            //刺身：沿速度拉长的绿芒 + 亮尖
            Color body = GsChlorophyteClaymore.CoreMain * (0.65f * fade);
            body.A = 0;
            Main.EntitySpriteDraw(star, center, null, body, Projectile.rotation,
                star.Size() * 0.5f, new Vector2(0.5f, 0.12f), SpriteEffects.None, 0);
            Color tip = GsChlorophyteClaymore.CoreHot * (0.8f * fade);
            tip.A = 0;
            Main.EntitySpriteDraw(star, center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 6f,
                null, tip, Projectile.rotation, star.Size() * 0.5f,
                new Vector2(0.26f, 0.08f), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 缠根域：终结核命中处竖起的驻留根网。150 帧寿命，22 帧一跳小伤，
    /// 域内敌人每帧速度衰减（缠根减速）；根须摇曳与蚀散全走确定性相位
    /// </summary>
    internal class GsChlorophyteClaymoreRootProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 150;
        private const float Radius = 92f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

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
                for (int i = 0; i < 8; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.5f, 8f),
                        DustID.ChlorophyteWeapon, new Vector2(0f, -Main.rand.NextFloat(1f, 3f)), 80, default,
                        Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = true;
                }
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

            Lighting.AddLight(Projectile.Center, GsChlorophyteClaymore.CoreMain.ToVector3() * (0.4f * (1f - Life01)));

            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                Vector2 at = Projectile.Center + new Vector2(Main.rand.NextFloat(-Radius, Radius) * 0.8f, Main.rand.NextFloat(-6f, 4f));
                Dust d = Dust.NewDustPerfect(at, DustID.ChlorophyteWeapon,
                    new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.2f)), 130, default, Main.rand.NextFloat(0.5f, 0.9f));
                d.noGravity = true;
            }
        }

        public override bool? CanDamage() => Life >= 4f && Projectile.timeLeft > 10 ? null : false;

        /// <summary>横扁的根域判定：宽圆减一点竖高</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 delta = targetHitbox.Center.ToVector2() - Projectile.Center;
            delta.Y *= 1.6f;
            return delta.Length() <= Radius;
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blot == null || star == null || glow == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float life = Life01;
            //破土 8 帧带 10% 过冲
            float grow = Life <= 8f ? 1.1f * (Life / 8f)
                : MathHelper.Lerp(1.1f, 1f, MathHelper.Clamp((Life - 8f) / 6f, 0f, 1f));

            //地表暗根座：数团真 alpha 暗绿斑铺底
            for (int i = 0; i < 4; i++) {
                float dieAt = 0.6f + 0.4f * SegRand(i);
                float segFade = MathHelper.Clamp((dieAt - life) / 0.28f, 0f, 1f);
                if (segFade <= 0.01f) {
                    continue;
                }
                Vector2 at = center + new Vector2((SegRand(i + 10) - 0.5f) * Radius * 1.5f, 2f + 3f * SegRand(i + 15));
                Color dark = GsChlorophyteClaymore.CoreDeep * (0.5f * segFade);
                Main.EntitySpriteDraw(blot, at, null, dark, SegRand(i + 20) * 0.8f - 0.4f,
                    blot.Size() * 0.5f, new Vector2(0.34f, 0.14f) * grow, SpriteEffects.None, 0);
            }

            //根须：七根细刺自地面竖起，各自摇曳、参差蚀散
            for (int i = 0; i < 7; i++) {
                float dieAt = 0.55f + 0.45f * SegRand(i + 30);
                float segFade = MathHelper.Clamp((dieAt - life) / 0.3f, 0f, 1f);
                if (segFade <= 0.01f) {
                    continue;
                }
                float x = (i / 6f - 0.5f) * Radius * 1.6f + (SegRand(i + 40) - 0.5f) * 16f;
                float height = (26f + 22f * SegRand(i + 50)) * grow;
                float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * (1.6f + SegRand(i + 60)) + SegRand(i + 70) * 6.28f) * 0.22f;
                float ang = -MathHelper.PiOver2 + sway;
                Vector2 baseAt = center + new Vector2(x, 4f);
                Color thorn = GsChlorophyteClaymore.CoreMain * (0.6f * segFade);
                thorn.A = 0;
                Main.EntitySpriteDraw(star, baseAt + ang.ToRotationVector2() * (height * 0.5f), null, thorn,
                    ang, star.Size() * 0.5f, new Vector2(height / star.Width * 2.2f, 0.1f), SpriteEffects.None, 0);
                //刺尖亮点：跳伤节奏同步的明灭
                float pulse = 0.5f + 0.5f * MathF.Sin(Life * 0.28f + SegRand(i + 80) * 6.28f);
                Color tip = GsChlorophyteClaymore.CoreHot * (0.5f * segFade * pulse);
                tip.A = 0;
                Main.EntitySpriteDraw(glow, baseAt + ang.ToRotationVector2() * height, null, tip, 0f,
                    glow.Size() * 0.5f, 0.14f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
