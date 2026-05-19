using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.OtherMods.InfernumMode;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    internal class RocketSkeleton : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder3;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.timeLeft = 600;
            Projectile.extraUpdates = 4;
            if (InfernumRef.InfernumModeOpenState) {
                Projectile.extraUpdates += 1;
            }
            if (CWRRef.GetBossRushActive() || Main.getGoodWorld || Main.zenithWorld || NPC.downedMoonlord) {
                Projectile.extraUpdates += 1;
            }

            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Projectile.scale += 0.012f;

            if (PRTLoader.NumberUsablePRT() > 10) {
                PRTLoader.NewParticle<PRT_SparkAlpha>(Projectile.Center, Projectile.velocity * 0.7f, Color.DarkRed, 1.4f).Configure(false, 6);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, Projectile.velocity * 0.7f, Color.LightGoldenrodYellow, 1f).Configure(false, 10);
            }
            else {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.FireworkFountain_Red, Projectile.velocity);
                dust.noGravity = true;
                dust.scale *= Main.rand.NextFloat(0.3f, 1.2f);
            }
            Projectile.localAI[0]++;
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Projectile.localAI[0] <= 0) {
                Projectile.tileCollide = false;
                return false;
            }
            return base.OnTileCollide(oldVelocity);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            CWRRef.LargeFieryExplosion(Projectile);
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
            Projectile.numHits++;
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.numHits <= 0) {
                CWRRef.LargeFieryExplosion(Projectile);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[ProjectileID.RocketSkeleton].Value;
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation
                , mainValue.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
