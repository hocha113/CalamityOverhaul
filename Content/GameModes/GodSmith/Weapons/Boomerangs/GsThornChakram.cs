using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 荆棘查克拉姆重铸。材质：丛林荆环。签名行为：①命中让目标中毒并迸出三根放射荆棘刺
    /// ②命中是植物撕裂声
    /// </summary>
    internal class GsThornChakram : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.ThornChakram;

        internal override int BoomerProjType => ModContent.ProjectileType<GsThornChakramProj>();

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "Hits poison the target and burst three thorn spikes outward, each dealing 22% damage";
    }

    /// <summary>荆环镖体：命中迸刺</summary>
    internal class GsThornChakramProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.ThornChakram;

        protected override SoundStyle HitSound => SoundID.Grass with { Volume = 0.6f, Pitch = -0.2f };

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 240);
            //荆棘迸射：owner 端放射三根刺，避开回打自己的方向
            if (Projectile.IsOwnedByLocalPlayer()) {
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.22f));
                float baseRot = (target.Center - Owner.Center).ToRotation();
                for (int i = -1; i <= 1; i++) {
                    Vector2 vel = (baseRot + (i * 0.85f)).ToRotationVector2() * 7.5f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, vel,
                        ModContent.ProjectileType<GsThornChakramSpikeProj>(), dmg, 0.3f, Owner.whoAmI);
                }
            }
        }
    }

    /// <summary>放射荆棘刺：短促穿透刺，原版丛林尖刺贴图</summary>
    internal class GsThornChakramSpikeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.JungleSpike;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 32;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.velocity *= 0.96f;   //刺出即衰减，短促有力
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Poisoned, 120);
    }
}
