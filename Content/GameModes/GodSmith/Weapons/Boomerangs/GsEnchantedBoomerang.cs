using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 附魔回旋镖重铸。材质：秘法附魔的蓝钢镖身。签名行为：悬停蓄势顶点迸出三枚追敌星辉
    /// </summary>
    internal class GsEnchantedBoomerang : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.EnchantedBoomerang;

        internal override int BoomerProjType => ModContent.ProjectileType<GsEnchantedBoomerangProj>();

        internal override float DamageMul => 1.05f;

        protected override string GsDescFallback =>
            "Decelerates outbound, gathers starlight while hovering, accelerates home\nAt the hover peak it flings three homing star sparks, each dealing 30% damage";
    }

    /// <summary>附魔镖体：悬停顶点放星</summary>
    internal class GsEnchantedBoomerangProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.EnchantedBoomerang;

        protected override int HoverTime => 22;

        protected override void OnEnterPhase(int phase, Player owner) {
            if (phase != PhaseHover) {
                return;
            }
            //悬停顶点：owner 端迸出三枚追星（30% 伤害随弹幕自带过线）
            if (Projectile.IsOwnedByLocalPlayer()) {
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.30f));
                for (int i = 0; i < 3; i++) {
                    Vector2 vel = (Projectile.rotation + (MathHelper.TwoPi / 3f * i)).ToRotationVector2() * 5f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                        ModContent.ProjectileType<GsEnchantedBoomerangStarProj>(), dmg, 0.5f, owner.whoAmI);
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
            }
        }
    }

    /// <summary>追敌星辉：短寿命追踪星屑，原版坠星贴图</summary>
    internal class GsEnchantedBoomerangStarProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        private NPC HomingTarget {
            get => Projectile.ai[0] > 0f ? Main.npc[(int)Projectile.ai[0] - 1] : null;
            set => Projectile.ai[0] = value == null ? 0f : value.whoAmI + 1;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 80;
        }

        public override void AI() {
            Projectile.rotation += 0.3f;

            NPC target = HomingTarget;
            if (target == null || !target.active || target.dontTakeDamage) {
                //各端同参搜索最近可打目标，结果确定性一致
                target = Projectile.FindTargetWithinRange(520f);
                HomingTarget = target;
            }
            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 11f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.09f);
            }
            else {
                Projectile.velocity *= 0.97f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.35f, Pitch = 0.5f }, target.Center);
        }
    }
}
