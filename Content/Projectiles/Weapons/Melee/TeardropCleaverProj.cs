using CalamityMod.Buffs.StatDebuffs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class TeardropCleaverProj : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "Teardrop";

        private const int maxFrme = 8;
        public override void SetDefaults() {
            Projectile.width = 277;
            Projectile.height = 252;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override bool ShouldUpdatePosition() {
            return true;
        }

        public override void AI() {
            CWRUtils.ClockFrame(ref Projectile.frame, 5, maxFrme - 1);
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.scale += 0.02f;
            if (Projectile.frame >= maxFrme - 1) {
                Projectile.Kill();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<TemporalSadness>(), 60);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<TemporalSadness>(), 60);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition + Projectile.rotation.ToRotationVector2() * -100, CWRUtils.GetRec(value, Projectile.frame, 8), Color.White
                , Projectile.rotation, new Vector2(0, Projectile.velocity.X > 0 ? 45 : 100)
                , Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return false;
        }
    }
}
