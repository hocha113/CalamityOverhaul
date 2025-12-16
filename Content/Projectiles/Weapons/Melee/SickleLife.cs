using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class SickleLife : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "LifeScythe";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 62;
            Projectile.height = 72;
            Projectile.aiStyle = ProjAIStyleID.Sickle;
            Projectile.alpha = 55;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 240;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            AIType = ProjectileID.DeathSickle;
        }

        public override void AI() {
            float scaling = Main.mouseTextColor / 200f - 0.35f;
            scaling *= 0.2f;
            Projectile.scale = scaling + 0.95f;
            if (Projectile.timeLeft < 202) {
                if (Projectile.velocity.Length() < 16) {
                    Projectile.velocity *= 1.05f;
                }
                NPC target = Projectile.Center.FindClosestNPC(900);
                if (target != null) {
                    Vector2 idealVelocity = Projectile.To(target.Center).UnitVector() * (Projectile.velocity.Length() + 6.5f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, idealVelocity, 0.08f);
                }
            }
            Lighting.AddLight(Projectile.Center, 0.1f, 0.5f, 0.15f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            float alp = (Projectile.timeLeft / 200f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
            GameShaders.Armor.ApplySecondary(ContentSamples.CommonlyUsedContentSamples.ColorOnlyShaderIndex, Main.player[Projectile.owner], null);

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + new Vector2(33, 33);
                Color color = Color.Green * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(value, drawPos, null, color * Projectile.Opacity * alp * 0.65f
                    , Projectile.oldRot[k], value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, lightColor * alp
                , Projectile.rotation, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
