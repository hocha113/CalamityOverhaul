using CalamityMod.Events;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Particles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    internal class Fireball : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "Fireball";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.timeLeft = 800;
            Projectile.extraUpdates = 1;
            Projectile.tileCollide = false;
            Projectile.maxPenetrate = Projectile.penetrate = 1;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (Projectile.ai[0] == 0) {
                SoundEngine.PlaySound(SoundID.Item8, Projectile.Center);
                Projectile.velocity /= 2;
            }
            if (Projectile.ai[0] <= 60) {
                Projectile.velocity *= 0.99f;
            }
            if (Projectile.ai[0] > 60 && Projectile.ai[0] < 360) {
                Projectile.velocity *= 1.025f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 4);
            Lighting.AddLight(Projectile.Center, Color.Red.ToVector3());
            PRT_LavaFire lavaFire = new PRT_LavaFire {
                Velocity = Projectile.velocity * 0.2f,
                Position = Projectile.Center + CWRUtils.randVr(6),
                Scale = Main.rand.NextFloat(0.8f, 1.2f),
                maxLifeTime = 60,
                minLifeTime = 30
            };
            PRTLoader.AddParticle(lavaFire);
            Projectile.ai[0]++;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<EXHellfire>(), 60);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = CWRUtils.GetRec(mainValue, Projectile.frame, 5);
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, rectangle, Color.White
                , Projectile.rotation, rectangle.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
