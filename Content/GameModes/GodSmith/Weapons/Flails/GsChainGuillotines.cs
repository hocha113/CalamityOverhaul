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
    /// 【连枷·链式断头台】腐化断头双铡：腐绿褐铁锈双刃。签名行为：①一次出手双铡齐飞，甩转反相、出手各自偏航
    /// ②双铡交剪：掷出或收链期两铡靠近即在中点炸十字剪切爆 ③铡刃咬速度方向
    /// </summary>
    internal class GsChainGuillotines : GsFlailScheme
    {
        public override int TargetItemID => ItemID.ChainGuillotines;

        protected override int FlailProjType => ModContent.ProjectileType<GsChainGuillotinesHead>();

        /// <summary>双铡齐飞</summary>
        protected override int HeadCount => 2;

        /// <summary>ai[2] 写双铡序号：0=主 1=从（随生成包过线）</summary>
        protected override float LaunchAi2(Player player, int index) => index;

        protected override string GsDescFallback =>
            "Reforged: hurls both guillotines at once, fanning apart mid-flight" +
            "\nWhen the two blades cross paths they shear, bursting cross-cut slashes between them";

        //双头吞吐量高（两颗锤头+交剪 70% AOE），底伤只补 2%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.02f;
    }

    /// <summary>
    /// 断头铡锤头。ai[2] 区分主从：0 号驱动姿态，从铡只挂链；甩转反相（spinAngle+π）、
    /// 出手各自偏航 ∓0.18 弧度；交剪由主铡 owner 端扫描生成，剪切冷却 12 帧
    /// </summary>
    internal class GsChainGuillotinesHead : GsFlailHeadProj
    {
        /// <summary>腐绿褐</summary>
        internal static readonly Color CorruptGreen = new(122, 148, 70);
        /// <summary>铁锈</summary>
        internal static readonly Color RustBrown = new(142, 92, 56);
        /// <summary>剪切锐光</summary>
        internal static readonly Color ShearLight = new(214, 236, 150);

        public override int SourceItemID => ItemID.ChainGuillotines;
        public override int VanillaProjID => ProjectileID.ChainGuillotine;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain40;
        public override Color GlowColor => CorruptGreen;

        //铡刀参数：出手利落、链短、蓄压稍长
        public override float LaunchSpeed => 17f;
        public override int LaunchFrames => 16;
        public override float MaxChainLength => 330f;
        public override int ChargeFrames => 40;
        /// <summary>铡刃咬速度方向</summary>
        public override bool SelfSpinHead => false;

        /// <summary>剪切冷却帧计数</summary>
        internal int shearCooldown;

        /// <summary>交剪触发距离</summary>
        private const float ShearRange = 44f;
        /// <summary>剪切冷却帧数</summary>
        private const int ShearCooldownFrames = 12;

        /// <summary>0 号为主铡</summary>
        private bool IsPrimary => WeaponAi2 < 0.5f;

        /// <summary>只让主铡驱动玩家姿态，从铡挂链不抢臂</summary>
        protected override bool ControlsPose => IsPrimary;

        public override void Initialize() {
            base.Initialize();
            //从铡甩转反相，双铡对称轮转
            if (!IsPrimary) {
                spinAngle += MathHelper.Pi;
            }
        }

        protected override void OnLaunch(float charge) {
            //出手瞬间各自偏航（owner 端，紧随基类 netUpdate 同帧过线）
            Projectile.velocity = Projectile.velocity.RotatedBy(IsPrimary ? -0.18f : 0.18f);
        }

        protected override void PostStateAI() {
            if (shearCooldown < ShearCooldownFrames) {
                shearCooldown++;
            }
            //交剪：主铡 owner 端扫描，双方都在掷出/收链态、彼此够近且冷却就绪
            if (!IsPrimary || !Projectile.IsOwnedByLocalPlayer()
                || State == StateSpin || shearCooldown < ShearCooldownFrames) {
                return;
            }
            foreach (Projectile other in Main.ActiveProjectiles) {
                if (other.type != Projectile.type || other.owner != Projectile.owner
                    || other.whoAmI == Projectile.whoAmI || (int)other.ai[0] == StateSpin) {
                    continue;
                }
                if (other.ModProjectile is not GsChainGuillotinesHead mate
                    || mate.shearCooldown < ShearCooldownFrames
                    || Projectile.Center.Distance(other.Center) >= ShearRange) {
                    continue;
                }
                shearCooldown = 0;
                mate.shearCooldown = 0;
                Vector2 mid = (Projectile.Center + other.Center) * 0.5f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), mid, Vector2.Zero,
                    ModContent.ProjectileType<GsChainGuillotinesShearProj>(),
                    Math.Max(1, (int)(Projectile.damage * 0.70f)), 5f, Projectile.owner);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = 0.25f }, mid);
                }
                break;
            }
        }

        protected override void SpawnHitBurst(NPC target, NPC.HitInfo hit, float charge) {
            base.SpawnHitBurst(target, hit, charge);
            //腐化铁锈补层：绿褐碎屑
            for (int i = 0; i < 4; i++) {
                Color c = Main.rand.NextBool() ? CorruptGreen : RustBrown;
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Circular(3.5f, 3.5f), c, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        public override Color ChainLinkColor(int linkIndex, float t, Color light)
            //近头链节染一层腐绿，读得出哪头是铡
            => Color.Lerp(light, CorruptGreen, 0.22f * t);
    }

    /// <summary>
    /// 十字剪切爆：两道交叉锐光扩张收拢+腐绿溅射，早窗结伤（70% 小 AOE）；
    /// 自绘，交叉角与相位用 identity 播种
    /// </summary>
    internal class GsChainGuillotinesShearProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 20;
        private const int DamageWindow = 8;

        private float Seed => Projectile.identity * 0.917f;
        private float LifeT => 1f - Projectile.timeLeft / (float)LifeFrames;
        /// <summary>剪切判定半径：先猛后缓外扩</summary>
        private float ShearRadius => MathHelper.Lerp(16f, 58f, 1f - (1f - LifeT) * (1f - LifeT));

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.timeLeft = LifeFrames;
        }

        public override void AI()
            => Lighting.AddLight(Projectile.Center, GsChainGuillotinesHead.CorruptGreen.ToVector3() * (0.4f * (1f - LifeT)));

        public override bool? CanDamage() => Projectile.timeLeft > LifeFrames - DamageWindow ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= ShearRadius;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blade = CWRAsset.LightShot?.Value;
            Texture2D splash = CWRAsset.TearSpread01?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (blade == null || splash == null || star == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float fade = 1f - LifeT;
            //刃长先展开后收拢
            float reach = MathHelper.Lerp(0.35f, 0.75f, MathF.Sin(MathHelper.Clamp(LifeT * 1.6f, 0f, 1f) * MathHelper.Pi));
            float crossAngle = Seed % MathHelper.Pi;

            //两道交叉锐光（加色 A=0），各沿正反方向画满一条
            for (int i = 0; i < 2; i++) {
                float ang = crossAngle + (i == 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4);
                Color edge = GsChainGuillotinesHead.ShearLight * (0.8f * fade);
                edge.A = 0;
                foreach (float sign in new float[] { 1f, -1f }) {
                    Vector2 dir = ang.ToRotationVector2() * sign;
                    Main.EntitySpriteDraw(blade, pos + dir * (18f * reach / 0.75f), null, edge,
                        dir.ToRotation(), blade.Size() / 2f, new Vector2(reach, 0.16f), SpriteEffects.None, 0);
                }
            }
            //腐绿溅射（TearSpread01 真 alpha）
            Main.EntitySpriteDraw(splash, pos, null, GsChainGuillotinesHead.CorruptGreen * (0.65f * fade),
                Seed, splash.Size() / 2f, 0.16f + LifeT * 0.10f, SpriteEffects.None, 0);
            //铁锈暗屑第二层，转 90 度错开
            Main.EntitySpriteDraw(splash, pos, null, GsChainGuillotinesHead.RustBrown * (0.4f * fade),
                Seed + MathHelper.PiOver2, splash.Size() / 2f, 0.11f + LifeT * 0.08f, SpriteEffects.None, 0);
            //交点四芒闪（前段）
            if (LifeT < 0.4f) {
                Color flash = Color.Lerp(GsChainGuillotinesHead.ShearLight, Color.White, 0.5f)
                    * (0.7f * (1f - LifeT / 0.4f));
                flash.A = 0;
                Main.EntitySpriteDraw(star, pos, null, flash, -Seed, star.Size() / 2f, 0.20f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
