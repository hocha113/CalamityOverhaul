using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles
{
    /// <summary>凝胶珍珠：重力弧线的胶质弹，本体=原版御凝胶贴图(实体遮挡)+胶光衬底；ai[2]=色相种子</summary>
    internal class QueenGelPearlProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int PearlDamage = 28;

        private float HueSeed => Projectile.ai[2];
        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;
            if (Timer == 1 && !VaultUtils.isServer) {
                Main.instance.LoadProjectile(ProjectileID.QueenSlimeGelAttack);
            }

            //重力弧线
            Projectile.velocity.Y += 0.21f;
            if (Projectile.velocity.Y > 11f) {
                Projectile.velocity.Y = 11f;
            }
            Projectile.tileCollide = Timer > 12;
            Projectile.rotation += Projectile.velocity.X * 0.03f;

            Lighting.AddLight(Projectile.Center, QueenMotion.PrismHue(HueSeed).ToVector3() * 0.28f);

            if (!VaultUtils.isServer && Main.rand.NextBool(5) && Projectile.velocity.Length() > 1f) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.TintableDust,
                    -Projectile.velocity * 0.15f, 150, QueenMotion.GetQueenDustColor(), 1f);
                d.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) => true;

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                QueenMotion.GelSplashBurst(Projectile.Center, 0.55f, 3);
            }
        }

        /// <summary>本体：原版御凝胶贴图(实体遮挡)+同材质残影</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D gel = TextureAssets.Projectile[ProjectileID.QueenSlimeGelAttack].Value;
            Rectangle rect = gel.Frame();
            Vector2 origin = rect.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            //胶体染色：留 alpha 保遮挡
            Color bodyColor = Color.Lerp(Color.White, QueenMotion.RoyalPink, 0.25f);
            bodyColor = Color.Lerp(bodyColor, lightColor, 0.3f);

            //速度拉伸(液滴各向异性)
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.03f, 0f, 0.4f);
            Vector2 scale = new Vector2(1f - stretch * 0.4f, 1f + stretch) * 0.82f;
            float rot = speed > 0.5f ? Projectile.velocity.ToRotation() - MathHelper.PiOver2 : Projectile.rotation;

            //同材质残影
            for (int i = Projectile.oldPos.Length - 1; i >= 2; i -= 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Main.EntitySpriteDraw(gel, ghostPos, rect, bodyColor * (0.35f * fade), rot,
                    origin, scale * (0.6f + 0.3f * fade), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(gel, drawPos, rect, bodyColor, rot, origin, scale, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>胶光衬底(真加色批)：小体积微光+高光芯</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color hue = QueenMotion.PrismHue(HueSeed);
            spriteBatch.Draw(glow, drawPos, null, hue * 0.5f, 0f, glow.Size() / 2f, 0.3f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, Color.White * 0.32f, 0f, glow.Size() / 2f, 0.14f, SpriteEffects.None, 0f);
        }
    }
}
