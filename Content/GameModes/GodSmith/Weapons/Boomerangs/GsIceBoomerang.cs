using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 冰回旋镖重铸。材质：寒晶冰刃。签名行为：①命中附加霜火并迸出两片穿刺碎冰
    /// ②命中是冰晶碎裂声
    /// </summary>
    internal class GsIceBoomerang : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.IceBoomerang;

        internal override int BoomerProjType => ModContent.ProjectileType<GsIceBoomerangProj>();

        internal override float DamageMul => 1.05f;

        protected override string GsDescFallback =>
            "Hits inflict Frostburn and shatter off two piercing ice shards, each dealing 25% damage\nWhile hovering it frosts over, wreathed in freezing mist";
    }

    /// <summary>冰刃镖体：碎冰迸射</summary>
    internal class GsIceBoomerangProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.IceBoomerang;

        protected override SoundStyle HitSound => SoundID.Item27 with { Volume = 0.5f, Pitch = 0.2f };

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Frostburn, 180);
            //碎冰迸射：owner 端沿命中面法向掰出两片碎冰
            if (Projectile.IsOwnedByLocalPlayer()) {
                int dmg = Math.Max(1, (int)(Projectile.damage * 0.25f));
                Vector2 baseDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                for (int i = 0; i < 2; i++) {
                    Vector2 vel = baseDir.RotatedBy((i == 0 ? 1 : -1) * 0.9f)
                        * Main.rand.NextFloat(6f, 8f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, vel,
                        ModContent.ProjectileType<GsIceBoomerangShardProj>(), dmg, 0.5f, Owner.whoAmI);
                }
            }
        }
    }

    /// <summary>穿刺碎冰：轻坠短寿命冰片，原版冰矢贴图</summary>
    internal class GsIceBoomerangShardProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceBolt;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 45;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.12f;   //轻坠弧线
            Projectile.velocity *= 0.985f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Frostburn, 90);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = 0.5f }, Projectile.Center);
        }
    }
}
