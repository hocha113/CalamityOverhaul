using CalamityOverhaul.Common;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class DeadWave : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private Trail Trail;
        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.scale = 0.2f;
            Projectile.alpha = 205;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.MaxUpdates = 3;
            Projectile.timeLeft = 150 * Projectile.MaxUpdates;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.scale < 1f) {
                Projectile.scale += 0.01f;
            }
            if (Projectile.timeLeft < 200) {
                Projectile.alpha -= 1;
            }
            if (Projectile.alpha <= 0) {
                Projectile.Kill();
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 overs = Projectile.velocity.GetNormalVector();
            float wit = 30 * Projectile.scale;
            float point = 0;
            return Collision.CheckAABBvLineCollision
                (targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center + overs * wit, Projectile.Center + overs * -wit, 3, ref point);
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Vector2[] newPoss = new Vector2[20];
            Vector2 norlVer = Projectile.velocity.UnitVector();
            for (int i = 0; i < newPoss.Length; i++) {
                newPoss[i] = Projectile.Center + norlVer * i * 16 - norlVer * 160;
            }
            Trail ??= new Trail(newPoss, (float completionRatio) => Projectile.scale * 130f, (Vector2 _) => Color.Blue);
            Trail.TrailPositions = newPoss;

            Effect effect = Filters.Scene["CWRMod:gradientTrail"].GetShader().Shader;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * 0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * 0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CWRAsset.LightShot.Value);
            effect.Parameters["uFlow"].SetValue(CWRAsset.Airflow.Value);
            effect.Parameters["uGradient"].SetValue(CWRUtils.GetT2DValue(CWRConstant.ColorBar + "AbsoluteZero_Bar"));
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Placeholder_White.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }
    }
}
