using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents.Projectiles
{
    /// <summary>仙人掌钉刺：直飞后段微坠，用户贴图为体，同材质残影拖尾</summary>
    internal class BssNeedleProj : BssModProjectile
    {
        public override string Texture => CWRConstant.NPC + "BSS/Needle";

        /// <summary>贴图尖端朝向（素材尖朝左下 ≈ 3π/4，进游戏后按观感微调）</summary>
        private const float AuthoredTipAngle = 2.356f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.scale = 1.5f;
        }

        public override void AI() {
            //出手 18 帧后微坠：钉刺有重量，不是激光
            if (++Projectile.localAI[0] > 18f) {
                Projectile.velocity.Y = MathHelper.Clamp(Projectile.velocity.Y + 0.09f, -20f, 12f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() - AuthoredTipAngle;

            if (!Main.dedServ && Main.rand.NextBool(6)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                    -Projectile.velocity * 0.05f, 140, default, 0.7f);
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                    Main.rand.NextVector2Circular(1.4f, 1.4f), 120, default, Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Type);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, lightColor * (0.3f * t), Projectile.rotation,
                    origin, Projectile.scale * (0.8f + 0.2f * t), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
