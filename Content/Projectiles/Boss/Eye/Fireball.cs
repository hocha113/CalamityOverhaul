using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Particles;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.Eye
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
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 3);
            Lighting.AddLight(Projectile.Center, Color.Red.ToVector3());
            if (Math.Abs(Projectile.position.X - Main.LocalPlayer.position.X) <= Main.screenWidth / 2
                || Math.Abs(Projectile.position.Y - Main.LocalPlayer.position.Y) <= Main.screenWidth / 2) {
                PRT_LavaFire lavaFire = new PRT_LavaFire {
                    Velocity = Projectile.velocity * 0.2f,
                    Position = Projectile.Center + CWRUtils.randVr(6),
                    Scale = Main.rand.NextFloat(0.8f, 1.2f),
                    maxLifeTime = 20,
                    minLifeTime = 8
                };
                PRTLoader.AddParticle(lavaFire);
            }

            Projectile.ai[0]++;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<HellburnBuff>(), 60);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = CWRUtils.GetRec(mainValue, Projectile.frame, 4);
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, rectangle, Color.White
                , Projectile.rotation, rectangle.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
