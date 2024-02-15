using CalamityMod.Buffs.StatDebuffs;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class TeardropCleaverProj : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "Teardrop";
        public override void SetDefaults() {
            Projectile.width = 77;
            Projectile.height = 52;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
        }

        public override bool ShouldUpdatePosition() {
            return true;
        }

        public override void AI() {
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 6);
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.scale += 0.02f;
            if (Projectile.frame >= 6) {
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
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, CWRUtils.GetRec(value, Projectile.frame, 7), Color.White
                , Projectile.rotation, new Vector2(0, Projectile.velocity.X > 0 ? 26 : 6)
                , Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
            return false;
        }
    }
}
