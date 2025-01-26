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

namespace CalamityOverhaul.Content.Projectiles.Boss.MechanicalEye
{
    internal class Laser : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "Laser";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.timeLeft = 600;
            Projectile.extraUpdates = 1;
            Projectile.tileCollide = false;
            Projectile.maxPenetrate = Projectile.penetrate = 1;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            if (Projectile.ai[0] == 0) {
                SoundEngine.PlaySound(SoundID.Item33, Projectile.Center);
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 4);
            Lighting.AddLight(Projectile.Center, Color.Gold.ToVector3());
            if (Math.Abs(Projectile.position.X - Main.LocalPlayer.position.X) <= Main.screenWidth / 2
                || Math.Abs(Projectile.position.Y - Main.LocalPlayer.position.Y) <= Main.screenWidth / 2) {
                PRT_LonginusWave wave = new PRT_LonginusWave(Projectile.Center, Projectile.velocity
                , Color.Gold, new Vector2(0.1f, 0.1f), Projectile.rotation, 2, 3, 12, null);
                PRTLoader.AddParticle(wave);
            }

            Projectile.ai[0]++;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire3, 60);
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
