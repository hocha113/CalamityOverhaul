using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles
{
    /// <summary>吞沙炮霰弹：原版沙球实体染坏死紫 + 金缘辉光，重力弧线，触地即散（不留池）</summary>
    internal class FssMortarShard : FssModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.scale = 1.35f;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.32f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }
            Projectile.rotation += Projectile.velocity.X * 0.04f;

            if (!VaultUtils.isServer && Main.rand.NextBool(7)) {
                Dust gold = Dust.NewDustPerfect(Projectile.Center, DustID.Ichor,
                    -Projectile.velocity * 0.08f, 60, default, Main.rand.NextFloat(0.6f, 0.9f));
                gold.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, FssVfx.IchorGold.ToVector3() * 0.2f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                    Main.rand.NextVector2Circular(2f, 1.5f) - new Vector2(0f, 1f),
                    100, FssVfx.TaintedSand, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
            }
            FssVfx.IchorBurst(Projectile.Center, 0.35f, -Projectile.velocity.SafeNormalize(Vector2.UnitY));
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.SandBallFalling);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.SandBallFalling].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //同材质拖尾
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, FssVfx.IchorDeep with { A = 60 } * (0.35f * t),
                    Projectile.rotation, origin, Projectile.scale * (0.6f + 0.3f * t), SpriteEffects.None, 0);
            }

            //实体：原版沙球染坏死紫 + 金缘
            Color body = lightColor.MultiplyRGB(FssVfx.SkinMul);
            Main.EntitySpriteDraw(tex, drawPos, null, body, Projectile.rotation,
                origin, Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, FssVfx.IchorGold with { A = 0 } * 0.4f,
                Projectile.rotation, origin, Projectile.scale * 1.12f, SpriteEffects.None, 0);
            return false;
        }
    }
}
