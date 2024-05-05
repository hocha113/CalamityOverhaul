using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Neutrons
{
    internal class NeutronExplode : ModProjectile, IDrawWarp
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 200;
            Projectile.timeLeft = 20;
            Projectile.aiStyle = -1;
            Projectile.localNPCHitCooldown = 4;
            Projectile.penetrate = -1;
            Projectile.friendly = true;
            Projectile.netImportant = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
        }

        public bool canDraw() => false;

        public override void AI() {
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

            Lighting.AddLight(Projectile.Center, new Vector3(1, 1, 1));
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;

        public void Warp() {
            Texture2D warpTex = TextureAssets.Projectile[Type].Value;
            Color warpColor = new Color(45, 45, 45) * Projectile.ai[1];
            for (int i = 0; i < 3; i++) {
                Main.spriteBatch.Draw(warpTex, Projectile.Center - Main.screenPosition
                    , null, warpColor, Projectile.ai[0] + i * 2f, warpTex.Size() / 2, Projectile.localAI[0], SpriteEffects.None, 0f);
            }
        }

        public void costomDraw(SpriteBatch spriteBatch) { }
    }
}
