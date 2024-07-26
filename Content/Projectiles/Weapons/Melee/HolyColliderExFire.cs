using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Particles;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using System;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria;
using Terraria.Audio;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class HolyColliderExFire : BaseHeldProj, IDrawWarp
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 400;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 4;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.DamageType = DamageClass.Melee;
            
        }

        private void SpwanPRKAndDustEffect() {
            if (Main.dedServ) {
                return;
            }
            Vector2 spanPos = Projectile.Center;
            for (int i = 0; i < 20; i++) {
                int dust = Dust.NewDust(spanPos, Projectile.width, Projectile.height, DustID.OrangeTorch, Scale: 6);
                Main.dust[dust].position = spanPos;
                Main.dust[dust].velocity = (MathHelper.TwoPi / 20f * i).ToRotationVector2() * 16;
                Main.dust[dust].noGravity = true;
            }
            for (int i = 0; i < 80; i++) {
                DRK_LavaFire lavaFire = new DRK_LavaFire();
                lavaFire.Velocity = CWRUtils.randVr(3, 4);
                lavaFire.Position = spanPos;

                lavaFire.Scale = Main.rand.NextFloat(0.9f, 1.6f);
                lavaFire.Color = Color.White;
                lavaFire.ai[1] = 2;
                DRKLoader.AddParticle(lavaFire);
            }
            for (int i = 0; i < 20; i++) {
                DRK_LavaFire lavaFire = new DRK_LavaFire();
                lavaFire.Velocity = CWRUtils.randVr(3, 4);
                lavaFire.Position = spanPos;
                lavaFire.Scale = Main.rand.NextFloat(0.8f, 1.2f);
                lavaFire.Color = Color.White;
                lavaFire.ai[0] = 1;
                lavaFire.ai[1] = 0;
                lavaFire.minLifeTime = 30;
                lavaFire.maxLifeTime = 60;
                DRKLoader.AddParticle(lavaFire);
            }
        }

        public override void AI() {
            if (Projectile.ai[2] == 0) {
                SoundEngine.PlaySound(SoundID.Item69, Projectile.Center);
                SpwanPRKAndDustEffect();
            }

            Projectile.ai[0] += 0.25f;
            if (Projectile.timeLeft > 15) {
                Projectile.localAI[0] += 0.25f;
                Projectile.ai[1] += 0.2f;
            }
            else {
                Projectile.localAI[0] -= 0.13f;
                Projectile.ai[1] -= 0.066f;
            }

            Projectile.localAI[1] += 0.07f;
            Projectile.ai[1] = Math.Clamp(Projectile.ai[1], 0f, 1f);
            Projectile.ai[2]++;
            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override void OnKill(int timeLeft) {
            
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        void IDrawWarp.Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            Color warpColor = new Color(45, 45, 45) * Projectile.ai[1];
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 drawOrig = warpTex.Size() / 2;
            for (int i = 0; i < 13; i++) {
                Main.spriteBatch.Draw(warpTex, drawPos, null, warpColor, Projectile.ai[0] + i * 115f
                    , drawOrig, Projectile.localAI[0] + i * 0.015f, SpriteEffects.None, 0f);
            }
        }

        bool IDrawWarp.noBlueshift() => true;

        bool IDrawWarp.canDraw() => false;

        void IDrawWarp.costomDraw(SpriteBatch spriteBatch) { }
    }
}
