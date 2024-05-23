using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Boss.SkeletronPrime
{
    internal class PrimeCannonOnSpan : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "DeathLaser";
        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.scale = 1f;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.alpha = 0;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0) {
                Projectile.rotation = Projectile.velocity.ToRotation();
            }
            NPC npc = CWRUtils.GetNPCInstance((int)Projectile.ai[0]);
            if (npc.Alives()) {
                Projectile.Center = npc.Center;
            }
            Player player = CWRUtils.GetPlayerInstance((int)Projectile.ai[1]);
            if (player.Alives()) {
                Vector2 toSet = Projectile.Center.To(player.Center);
                Projectile.EntityToRot(toSet.ToRotation() + Projectile.ai[2], 0.05f);
            }

            Projectile.scale += 0.05f;
            if (Projectile.alpha < 255) {
                Projectile.alpha += 15;
            }

            Projectile.localAI[0]++;
        }

        public override void OnKill(int timeLeft) {
            if (!CWRUtils.isClient) {
                SoundEngine.PlaySound(SoundID.Item62, Projectile.Center);
                int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI()
                        , Projectile.Center, Projectile.rotation.ToRotationVector2() * 13
                        , ProjectileID.RocketSkeleton, Projectile.damage, 0f, Main.myPlayer, Projectile.ai[1], 2);
                Main.projectile[proj].timeLeft = 600;
                Main.projectile[proj].extraUpdates = 6;
                Main.projectile[proj].tileCollide = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = Projectile.T2DValue();
            float sengs = Projectile.alpha / 255f;
            Color color = CWRUtils.MultiStepColorLerp(sengs, Color.Blue, Color.Red);
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, null, color with { A = 255 } * sengs, Projectile.rotation - MathHelper.PiOver2
                    , new Vector2(mainValue.Width / 2, 0), new Vector2(1, 100) * Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
