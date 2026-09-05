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
    /// 【连枷·首把】痛苦之球重铸：腐化荆棘铁球。签名行为：①甩转充能满出手更快更狠（族链体物理）
    /// ②掷出飞行沿途崩落悬停倒刺，触碰刺伤 ③满转速命中在目标处炸出一圈倒刺
    /// </summary>
    internal class GsBallOHurt : GsFlailScheme
    {
        public override int TargetItemID => ItemID.BallOHurt;

        protected override int FlailProjType => ModContent.ProjectileType<GsBallOHurtHead>();

        protected override string GsDescFallback =>
            "Reforged: spin to charge the swing; the flying ball sheds hovering barbs along its path\nA fully charged strike bursts a ring of barbs on the target";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.12f;
    }

    /// <summary>
    /// 痛苦之球锤头。飞行期每隔数帧崩落一枚倒刺（单掷上限 6），
    /// 满转命中时以目标为心环爆 5 枚；倒刺一律走 owner 端生成
    /// </summary>
    internal class GsBallOHurtHead : GsFlailHeadProj
    {
        public override int SourceItemID => ItemID.BallOHurt;
        public override int VanillaProjID => ProjectileID.BallOHurt;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain2;

        public override float MaxChainLength => 330f;
        public override float LaunchSpeed => 16f;
        public override int LaunchFrames => 18;

        /// <summary>本次掷出已崩落的倒刺数</summary>
        private int barbsShed;

        /// <summary>倒刺伤害系数</summary>
        private const float BarbDamageMul = 0.4f;
        /// <summary>单掷崩落上限</summary>
        private const int BarbCapPerThrow = 6;
        /// <summary>全场悬停倒刺上限</summary>
        private const int BarbCapTotal = 14;

        protected override void OnLaunch(float charge) => barbsShed = 0;

        protected override void OnLaunchTick(int flightTime) {
            //沿途崩刺：每 5 帧一枚，带一点横向散布；owner 端生成随包广播
            if (!Projectile.IsOwnedByLocalPlayer() || flightTime % 5 != 0
                || barbsShed >= BarbCapPerThrow
                || Owner.ownedProjectileCounts[ModContent.ProjectileType<GsBallOHurtBarbProj>()] >= BarbCapTotal) {
                return;
            }
            barbsShed++;
            Vector2 drift = Projectile.velocity.SafeNormalize(Vector2.UnitX)
                .RotatedBy(MathHelper.PiOver2 * (barbsShed % 2 == 0 ? 1 : -1)) * Main.rand.NextFloat(0.7f, 1.6f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, drift,
                ModContent.ProjectileType<GsBallOHurtBarbProj>(),
                Math.Max(1, (int)(Projectile.damage * BarbDamageMul)), 0.5f, Projectile.owner);
        }

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            //满转命中：以目标为心环爆一圈倒刺
            if (LaunchCharge >= 0.99f && State == StateLaunch) {
                const int ringCount = 5;
                for (int i = 0; i < ringCount; i++) {
                    Vector2 dir = (MathHelper.TwoPi * i / ringCount + Projectile.identity * 0.7f).ToRotationVector2();
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                        target.Center + dir * 30f, dir * 2.2f,
                        ModContent.ProjectileType<GsBallOHurtBarbProj>(),
                        Math.Max(1, (int)(Projectile.damage * BarbDamageMul)), 0.5f, Projectile.owner);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCDeath23 with { Volume = 0.5f, Pitch = 0.2f }, target.Center);
                }
            }
        }
    }

    /// <summary>
    /// 腐化倒刺：崩落后急减速悬停原地（残留物也有加速度曲线），
    /// 悬停约 2.6 秒刺伤触碰者，尾段淡出。原版魔棘尖刺贴图默认绘制
    /// </summary>
    internal class GsBallOHurtBarbProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.VilethornTip;

        private const int LifeFrames = 156;
        private const int FadeInFrames = 6;
        private const int FadeOutFrames = 20;

        /// <summary>identity 播种的相位，悬停浮动不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.917f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.timeLeft = LifeFrames;
        }

        private float Opacity {
            get {
                if (Projectile.timeLeft > LifeFrames - FadeInFrames) {
                    return (LifeFrames - Projectile.timeLeft) / (float)FadeInFrames;
                }
                if (Projectile.timeLeft < FadeOutFrames) {
                    return Projectile.timeLeft / (float)FadeOutFrames;
                }
                return 1f;
            }
        }

        public override void AI() {
            //崩落初速急衰减到悬停，随后按 identity 相位轻微呼吸浮动
            Projectile.velocity *= 0.86f;
            if (Projectile.velocity.LengthSquared() < 0.02f) {
                Projectile.velocity = Vector2.Zero;
                Projectile.position.Y += MathF.Sin(Main.GameUpdateCount * 0.05f + Seed) * 0.12f;
            }
            if (Projectile.rotation == 0f) {
                Projectile.rotation = Seed % MathHelper.TwoPi;
            }
            Projectile.rotation += 0.006f;
            Projectile.Opacity = Opacity;
        }

        public override bool? CanDamage() => Opacity > 0.5f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //被踩中的倒刺立即碎掉，不做持续磨床
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.35f, Pitch = 0.5f }, Projectile.Center);
            }
            Projectile.Kill();
        }
    }
}
