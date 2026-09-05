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
            "Reforged: hurls both guillotines at once, fanning apart mid-flight\nWhen the two blades cross paths they shear, bursting cross-cut slashes between them";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.02f;
    }

    /// <summary>
    /// 断头铡锤头。ai[2] 区分主从：0 号驱动姿态，从铡只挂链；甩转反相（spinAngle+π）、
    /// 出手各自偏航 ∓0.18 弧度；交剪由主铡 owner 端扫描生成，剪切冷却 12 帧
    /// </summary>
    internal class GsChainGuillotinesHead : GsFlailHeadProj
    {
        public override int SourceItemID => ItemID.ChainGuillotines;
        public override int VanillaProjID => ProjectileID.ChainGuillotine;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain40;

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
    }

    /// <summary>
    /// 十字剪切爆：圆域外扩的早窗结伤（70% 小 AOE）；
    /// 用原版咒焰贴图按当前半径画一笔作范围提示
    /// </summary>
    internal class GsChainGuillotinesShearProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CursedFlameFriendly;

        private const int LifeFrames = 20;
        private const int DamageWindow = 8;

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

        public override bool? CanDamage() => Projectile.timeLeft > LifeFrames - DamageWindow ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= ShearRadius;

        /// <summary>范围提示：原版咒焰贴图按当前半径缩放画一笔，随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = ShearRadius * 2f / MathF.Max(tex.Width, 1);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * (1f - LifeT),
                0f, tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
