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
    /// 【连枷·滴血链锤】滴血者血瘤锤：猩红血肉黑血渗液。签名行为：①高转速与掷出期沿途滴落带重力血珠
    /// ②血珠触敌或落地炸成小血刺爆 ③六帧血肉链节逐节轮播、飞行拖血
    /// </summary>
    internal class GsDripplerFlail : GsFlailScheme
    {
        public override int TargetItemID => ItemID.DripplerFlail;

        protected override int FlailProjType => ModContent.ProjectileType<GsDripplerFlailHead>();

        protected override string GsDescFallback =>
            "Reforged: at high spin and through every throw, the head weeps gravity-bound blood beads" +
            "\nBeads burst into stinging blood spikes on contact or landing";

        //血珠群（35%×至多 10 颗在场）收益可观但落点被动，底伤补 8%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 滴血链锤锤头。链贴图用 Extra[99] 六帧竖排血肉链，覆写 ChainFrame 逐节轮播（镜像原版对 757 的处理）；
    /// 血珠 owner 端生成、单场上限 10
    /// </summary>
    internal class GsDripplerFlailHead : GsFlailHeadProj
    {
        /// <summary>猩红</summary>
        internal static readonly Color BloodRed = new(198, 36, 48);
        /// <summary>黑血</summary>
        internal static readonly Color BloodDark = new(84, 16, 26);
        /// <summary>血珠高光</summary>
        internal static readonly Color BloodShine = new(255, 128, 130);

        public override int SourceItemID => ItemID.DripplerFlail;
        public override int VanillaProjID => ProjectileID.DripplerFlail;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Extra[99];
        public override Color GlowColor => BloodRed;

        /// <summary>滴珠间隔帧</summary>
        private const int DripInterval = 6;
        /// <summary>血珠伤害系数</summary>
        private const float BeadDamageMul = 0.35f;
        /// <summary>全场血珠上限</summary>
        private const int BeadCapTotal = 10;

        /// <summary>上一帧锤头位置，用于算移动速度（甩转期 velocity 恒为零）</summary>
        private Vector2 lastCenter;
        /// <summary>滴珠计时</summary>
        private int dripTimer;

        /// <summary>六帧竖排链逐节轮播，镜像原版 DrawProj_FlailChains 对 757 的处理</summary>
        public override Rectangle? ChainFrame(int linkIndex)
            => TextureAssets.Extra[99].Frame(1, 6, 0, linkIndex % 6);

        protected override void PostStateAI() {
            Vector2 moveDelta = lastCenter == Vector2.Zero ? Vector2.Zero : Projectile.Center - lastCenter;
            lastCenter = Projectile.Center;

            //充能 >0.5 的甩转期与整个掷出期滴珠；owner 端生成随包广播
            bool dripping = (State == StateSpin && spinCharge > 0.5f) || State == StateLaunch;
            if (!dripping || !Projectile.IsOwnedByLocalPlayer() || ++dripTimer < DripInterval) {
                return;
            }
            if (Owner.ownedProjectileCounts[ModContent.ProjectileType<GsDripplerFlailBeadProj>()] >= BeadCapTotal) {
                return;
            }
            dripTimer = 0;
            //初速=锤头速度×0.2+微下坠
            Vector2 vel = moveDelta * 0.2f + new Vector2(0f, 0.6f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                ModContent.ProjectileType<GsDripplerFlailBeadProj>(),
                Math.Max(1, (int)(Projectile.damage * BeadDamageMul)), 0.8f, Projectile.owner);
        }

        protected override void OnLaunchTick(int flightTime) {
            //飞行期额外拖血
            if (!VaultUtils.isServer && flightTime % 2 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(1f, 1f),
                    60, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = false;
            }
        }

        protected override void SpawnHitBurst(NPC target, NPC.HitInfo hit, float charge) {
            base.SpawnHitBurst(target, hit, charge);
            //血肉质感补层：溅血
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Blood,
                    Main.rand.NextVector2Circular(4f, 4f), 60, default, Main.rand.NextFloat(1.1f, 1.6f));
                d.noGravity = Main.rand.NextBool();
            }
        }
    }

    /// <summary>
    /// 血珠：带重力坠落（0.3/帧），触敌或落地炸成小血刺爆；
    /// 自绘：血红椭球（真 alpha）+加色高光点+按速度拉伸，绘制用速度向量不掷 Main.rand
    /// </summary>
    internal class GsDripplerFlailBeadProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 90;
        /// <summary>爆裂窗帧数</summary>
        private const int BurstFrames = 10;

        /// <summary>ai[0]=1 进入爆裂态（owner 触发 + netUpdate 过线）；ai[1]=爆裂计时</summary>
        private bool Bursting => Projectile.ai[0] >= 1f;

        private float Seed => Projectile.identity * 0.917f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.timeLeft = LifeFrames;
        }

        public override void AI() {
            if (Bursting) {
                Projectile.velocity = Vector2.Zero;
                if (++Projectile.ai[1] >= BurstFrames) {
                    Projectile.Kill();
                }
                return;
            }
            //坠落：重力 0.3，横向微阻尼
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.3f, 15f);
            Projectile.velocity.X *= 0.995f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, GsDripplerFlailHead.BloodRed.ToVector3() * 0.12f);
        }

        /// <summary>进入爆裂：判定盒撑大成小血刺爆，短窗结伤后消亡</summary>
        private void Burst() {
            if (Bursting) {
                return;
            }
            Projectile.ai[0] = 1f;
            Projectile.ai[1] = 0f;
            Projectile.Resize(46, 46);
            Projectile.timeLeft = BurstFrames + 2;
            Projectile.netUpdate = true;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
                for (int i = 0; i < 5; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                        Main.rand.NextVector2Circular(3.5f, 3.5f), 60, default, Main.rand.NextFloat(1f, 1.5f));
                    d.noGravity = Main.rand.NextBool();
                }
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Circular(3f, 3f), GsDripplerFlailHead.BloodRed,
                        Main.rand.NextFloat(0.3f, 0.45f))?.Configure(true, Main.rand.Next(8, 14));
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //落地炸刺，不反弹不穿地
            Projectile.velocity = Vector2.Zero;
            Burst();
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Burst();

        public override bool PreDraw(ref Color lightColor) {
            Texture2D body = CWRAsset.Fog?.Value;
            Texture2D shine = CWRAsset.SoftGlow?.Value;
            Texture2D burst = CWRAsset.RayBurst01?.Value;
            if (body == null || shine == null || burst == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;

            if (Bursting) {
                //血刺爆：放射尖刺（加色）+黑血溅斑（真 alpha），随窗扩张淡出
                float t = Projectile.ai[1] / BurstFrames;
                float grow = MathHelper.Lerp(0.10f, 0.24f, 1f - (1f - t) * (1f - t));
                Color spike = GsDripplerFlailHead.BloodRed * (0.9f * (1f - t));
                spike.A = 0;
                Main.EntitySpriteDraw(burst, pos, null, spike, Seed, burst.Size() / 2f, grow, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(body, pos, null, GsDripplerFlailHead.BloodDark * (0.6f * (1f - t)),
                    Seed * 0.5f, body.Size() / 2f, 0.16f + t * 0.08f, SpriteEffects.None, 0);
                return false;
            }

            //坠落态：按速度拉伸的血红椭球（真 alpha），绘制方向取速度向量
            float vLen = Projectile.velocity.Length();
            float stretch = 1f + MathHelper.Clamp(vLen * 0.05f, 0f, 0.9f);
            Vector2 beadScale = new(0.075f * stretch, 0.055f / MathF.Sqrt(stretch));
            Color deep = Color.Lerp(GsDripplerFlailHead.BloodDark, GsDripplerFlailHead.BloodRed, 0.45f);
            Main.EntitySpriteDraw(body, pos, null, deep, Projectile.rotation,
                body.Size() / 2f, beadScale, SpriteEffects.None, 0);
            //加色高光点，偏上模拟湿润反光
            Color glint = GsDripplerFlailHead.BloodShine * 0.65f;
            glint.A = 0;
            Main.EntitySpriteDraw(shine, pos - new Vector2(2f, 3f), null, glint, 0f,
                shine.Size() / 2f, 0.07f, SpriteEffects.None, 0);
            return false;
        }
    }
}
