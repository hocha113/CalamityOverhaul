using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 荆棘查克拉姆重铸。材质：丛林荆环。签名行为：①命中让目标中毒并迸出三根放射荆棘刺
    /// ②荆棘刺穿透飞行留下毒绿细芒 ③命中是植物撕裂声与绿芒迸溅
    /// </summary>
    internal class GsThornChakram : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.ThornChakram;

        internal override int BoomerProjType => ModContent.ProjectileType<GsThornChakramProj>();

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "Hits poison the target and burst three thorn spikes outward, each dealing 22% damage\n" +
            "Right click while it flies: command it to dash toward your cursor";
    }

    /// <summary>荆环镖体：命中迸刺</summary>
    internal class GsThornChakramProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.ThornChakram;

        protected override Color GlowColor => new(115, 205, 85);

        protected override Color TrailColor => new(90, 170, 70);

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

        protected override void HitBurstFX(NPC target, NPC.HitInfo hit) {
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GlowColor, 0.22f)?.Configure(9, 0.8f);
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.GrassBlades,
                    Main.rand.NextVector2Circular(3.5f, 3.5f), 60, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
        }
    }

    /// <summary>放射荆棘刺：穿透细芒，速度拉伸自绘</summary>
    internal class GsThornChakramSpikeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

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
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!VaultUtils.isServer && Projectile.timeLeft % 4 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                    -Projectile.velocity * 0.05f, 100, default, 0.8f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Poisoned, 120);

        public override bool PreDraw(ref Color lightColor) {
            Texture2D streak = CWRAsset.LightShot?.Value;
            if (streak == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float speedK = MathF.Min(1f, Projectile.velocity.Length() / 8f);
            //速度拉伸的绿芒刺
            Color c = new Color(115, 205, 85) * (0.35f + (0.45f * speedK));
            c.A = 0;
            Main.spriteBatch.Draw(streak, pos, null, c, Projectile.rotation,
                new Vector2(streak.Width * 0.75f, streak.Height / 2f),
                new Vector2(0.16f * (0.5f + speedK), 0.05f), SpriteEffects.None, 0);
            return false;
        }
    }
}
