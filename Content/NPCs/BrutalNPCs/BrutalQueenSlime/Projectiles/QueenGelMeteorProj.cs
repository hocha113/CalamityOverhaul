using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles
{
    /// <summary>凝胶陨石：ai[0]=0扇形抛物/1穹顶直落 ai[2]=色相种子；坠地溅裂</summary>
    internal class QueenGelMeteorProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int MeteorDamage = 34;

        private bool DomeMode => (int)Projectile.ai[0] == 1;
        private float HueSeed => Projectile.ai[2];
        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;

            if (DomeMode) {
                //穹顶直落：持续加速
                Projectile.velocity.Y += 0.3f;
                if (Projectile.velocity.Y > 16f) {
                    Projectile.velocity.Y = 16f;
                }
            }
            else {
                //扇形抛物
                Projectile.velocity.Y += 0.19f;
                if (Projectile.velocity.Y > 13f) {
                    Projectile.velocity.Y = 13f;
                }
            }

            //出手短暂无碰撞，防止贴着皇后自杀
            Projectile.tileCollide = Timer > 14;
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, QueenMotion.RoyalPink.ToVector3() * 0.4f);

            //坠落甩滴
            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_QueenGelDrop>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    QueenMotion.RoyalPink * 0.7f, Main.rand.NextFloat(0.5f, 0.8f));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            QueenMotion.GelSplashBurst(Projectile.Center, 1.15f, 9);
            QueenMotion.LandingRingFX(Projectile.Center, 0.7f, HueSeed);
            SoundEngine.PlaySound(SoundID.Item167 with { Volume = 0.42f, Pitch = 0.65f, MaxInstances = 5 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarGlow01.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color hue = QueenMotion.PrismHue(HueSeed);
            Color pink = QueenMotion.RoyalPink;

            //残影
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float fade = (1f - i / (float)Projectile.oldPos.Length) * 0.5f;
                spriteBatch.Draw(glow, ghostPos, null, pink * (fade * 0.55f), 0f,
                    glow.Size() / 2f, 0.5f * fade + 0.14f, SpriteEffects.None, 0f);
            }

            //凝胶体：速度拉伸的泪滴
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.05f, 0f, 0.9f);
            Vector2 bodyScale = new Vector2(0.52f - stretch * 0.16f, 0.52f + stretch * 0.5f);
            spriteBatch.Draw(glow, drawPos, null, pink * 0.95f, Projectile.rotation, glow.Size() / 2f, bodyScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, hue * 0.5f, Projectile.rotation, glow.Size() / 2f, bodyScale * 1.35f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos, null, Color.White * 0.7f, Projectile.rotation, glow.Size() / 2f, bodyScale * 0.42f, SpriteEffects.None, 0f);
            //晶芯闪
            spriteBatch.Draw(star, drawPos, null, Color.White * 0.5f, Timer * 0.06f, star.Size() / 2f, 0.3f, SpriteEffects.None, 0f);
        }
    }
}
