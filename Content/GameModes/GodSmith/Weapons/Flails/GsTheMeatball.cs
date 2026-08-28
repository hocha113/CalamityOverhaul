using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·肉球】肉球重铸：血肉缝合重锤。签名行为：①满转速一击命中爆出血浆冲击并挂流血
    /// ②高转甩转与掷出飞行沿途滴血 ③慢充能大锤头，一击定音的重锤节奏
    /// </summary>
    internal class GsTheMeatball : GsFlailScheme
    {
        public override int TargetItemID => ItemID.TheMeatball;

        protected override int FlailProjType => ModContent.ProjectileType<GsTheMeatballHead>();

        protected override string GsDescFallback =>
            "Reforged: a fully charged strike bursts into a gout of gore" +
            "\nThe burst wounds everything nearby and inflicts Bleeding";

        //慢重锤定位，爆浆收益吃满转门槛：底伤补一成二，综合 DPS 落在原版 112%~125%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.12f;
    }

    /// <summary>
    /// 肉球锤头。慢充能大锤；满转命中在目标处引爆血浆冲击（owner 端生成），
    /// 高转与飞行期滴血预告分量
    /// </summary>
    internal class GsTheMeatballHead : GsFlailHeadProj
    {
        /// <summary>腥红</summary>
        internal static readonly Color GoreRed = new(206, 48, 62);
        /// <summary>暗肉深红</summary>
        internal static readonly Color FleshDark = new(92, 28, 36);
        /// <summary>血亮心</summary>
        internal static readonly Color GoreBright = new(244, 106, 104);

        public override int SourceItemID => ItemID.TheMeatball;
        public override int VanillaProjID => ProjectileID.TheMeatball;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain13;
        public override Color GlowColor => GoreRed;

        public override int HeadSize => 34;
        public override float MaxChainLength => 330f;
        public override float LaunchSpeed => 15f;
        public override int ChargeFrames => 52;

        /// <summary>爆浆伤害系数</summary>
        private const float BurstDamageMul = 0.55f;

        protected override void OnSpinTick(float charge) {
            //高转甩转滴血：血珠带重力甩出去
            if (!VaultUtils.isServer && charge > 0.55f && spinTimer % 5 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    Main.rand.NextVector2Circular(1.8f, 1.2f), 90, default, Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = false;
            }
        }

        protected override void OnLaunchTick(int flightTime) {
            //飞行沿途滴血
            if (!VaultUtils.isServer && flightTime % 3 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center,
                    DustID.Blood, -Projectile.velocity * 0.08f, 100, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = false;
            }
        }

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            //满转爆浆：以目标为心的一跳血浆冲击
            if (LaunchCharge >= 0.99f && State == StateLaunch) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsTheMeatballBurstProj>(),
                    Math.Max(1, (int)(Projectile.damage * BurstDamageMul)), 1f, Projectile.owner);
            }
        }

        protected override void SpawnHitBurst(NPC target, NPC.HitInfo hit, float charge) {
            base.SpawnHitBurst(target, hit, charge);
            //血肉质感补层：血尘迸溅
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                    Main.rand.NextVector2Circular(3.5f, 3.5f), 90, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = Main.rand.NextBool();
            }
        }
    }

    /// <summary>
    /// 血浆冲击：满转命中处的一跳小范围血爆（径约 90px），触者挂流血。
    /// 自绘：真 alpha 血浪瓣压厚度（Extra_98 染深红）+ 加色亮心；抖动全 identity 播种
    /// </summary>
    internal class GsTheMeatballBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeTicks = 22;
        private const int DamageWindow = 10;
        /// <summary>爆浆半径（径约 90px 按直径计）</summary>
        private const float MaxRadius = 46f;

        private float Age => LifeTicks - Projectile.timeLeft;

        /// <summary>血浪半径：前 7 帧猛涨后驻定，尾段随消散回缩</summary>
        private float RadiusNow {
            get {
                float grow = MathHelper.Clamp(Age / 7f, 0f, 1f);
                float fade = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
                return MaxRadius * (1f - (1f - grow) * (1f - grow)) * (0.4f + 0.6f * fade);
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;//一跳 AoE，同目标只结算一次
            Projectile.timeLeft = LifeTicks;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Age <= DamageWindow ? null : false;

        public override void AI() {
            if (Age == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath21 with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
                //出爆瞬间外抛一圈带重力的血珠
                for (int i = 0; i < 9; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f);
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                        vel, 80, default, Main.rand.NextFloat(1.1f, 1.6f));
                    d.noGravity = false;
                    if (i % 3 == 0) {
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, vel * 0.9f,
                            GsTheMeatballHead.GoreBright, Main.rand.NextFloat(0.35f, 0.5f))
                            ?.Configure(true, Main.rand.Next(12, 20));
                    }
                }
            }
            Lighting.AddLight(Projectile.Center,
                GsTheMeatballHead.GoreRed.ToVector3() * (0.4f * (RadiusNow / MaxRadius)));
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Bleeding, 180);

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = RadiusNow;
            if (r < 8f) {
                return false;
            }
            float nx = MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right);
            float ny = MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom);
            return new Vector2(nx - Projectile.Center.X, ny - Projectile.Center.Y).LengthSquared() <= r * r;
        }

        /// <summary>确定性伪随机（identity+salt 播种，绘制路径不掷 Main.rand）</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (blot == null || glow == null || star == null) {
                return false;
            }
            float r = RadiusNow;
            if (r < 6f) {
                return false;
            }
            float fade = MathHelper.Clamp(Projectile.timeLeft / (float)LifeTicks, 0f, 1f);
            Vector2 center = Projectile.Center - Main.screenPosition;

            //血浆暗体：真 alpha 深红团块拼一圈溅涌（血的厚重加色做不出来）
            const int lobes = 6;
            for (int i = 0; i < lobes; i++) {
                float ang = MathHelper.TwoPi * i / lobes + SegRand(i) * 0.8f;
                float dist = r * (0.4f + 0.3f * SegRand(i + 30));
                float s = (r / blot.Width) * (1f + 0.55f * SegRand(i + 60));
                Color dark = GsTheMeatballHead.FleshDark * (fade * 0.62f);
                Main.EntitySpriteDraw(blot, center + ang.ToRotationVector2() * dist, null, dark,
                    ang, blot.Size() * 0.5f, new Vector2(s, s * 0.66f), SpriteEffects.None, 0);
            }
            //腥红主层：真 alpha 血色瓣叠在暗体之上
            for (int i = 0; i < lobes; i++) {
                float ang = MathHelper.TwoPi * i / lobes + SegRand(i + 90) * 0.8f;
                float dist = r * (0.3f + 0.28f * SegRand(i + 120));
                float s = (r / blot.Width) * (0.75f + 0.4f * SegRand(i + 150));
                Color body = GsTheMeatballHead.GoreRed * (fade * 0.55f);
                Main.EntitySpriteDraw(blot, center + ang.ToRotationVector2() * dist, null, body,
                    -ang, blot.Size() * 0.5f, new Vector2(s, s * 0.7f), SpriteEffects.None, 0);
            }

            //加色亮心：血浆爆点的高光（黑底贴图进加色语义 A=0）
            if (Age <= 12) {
                float flash = 1f - Age / 12f;
                Color core = GsTheMeatballHead.GoreBright * (flash * 0.65f);
                core.A = 0;
                Main.EntitySpriteDraw(glow, center, null, core, 0f, glow.Size() * 0.5f,
                    r / glow.Width * 1.4f, SpriteEffects.None, 0);
                Color glint = Color.Lerp(GsTheMeatballHead.GoreBright, Color.White, 0.35f) * (flash * 0.45f);
                glint.A = 0;
                Main.EntitySpriteDraw(star, center, null, glint, SegRand(7) * MathHelper.TwoPi,
                    star.Size() * 0.5f, r / star.Width * 0.9f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
