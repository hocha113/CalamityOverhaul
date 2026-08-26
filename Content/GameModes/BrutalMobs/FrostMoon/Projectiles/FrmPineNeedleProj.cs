using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostMoon.Projectiles
{
    /// <summary>
    /// 松针弹：常绿尖叫怪速射流的单发，沿已锁定的瞄准线直飞不转向，
    /// 航程受限（威胁不越出预告线末端）
    /// </summary>
    internal class FrmPineNeedleProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.PineNeedleHostile;

        private float MaxTravel => Projectile.ai[0] <= 0f ? FrmAimLaneOmen.NeedleLaneLength : Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 130;
        }

        public override void AI() {
            //航程记账：各端由同步初速确定性同步消散
            Projectile.localAI[0] += Projectile.velocity.Length();
            if (Projectile.localAI[0] >= MaxTravel) {
                Projectile.Kill();
                return;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.08f, 0.16f, 0.08f);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                    Main.rand.NextVector2Circular(1.4f, 1.4f), 100, default, 0.9f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.PineNeedleHostile);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.PineNeedleHostile].Value;
            int frames = Main.projFrames[ProjectileID.PineNeedleHostile] > 0 ? Main.projFrames[ProjectileID.PineNeedleHostile] : 1;
            Rectangle rect = tex.Frame(1, frames, 0, 0);
            Vector2 orig = rect.Size() / 2f;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //同材质拖尾（速射曳光）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldPos, rect, new Color(140, 220, 140) * (0.38f * t),
                    Projectile.rotation, orig, new Vector2(0.8f, 1f) * t + new Vector2(0.2f), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, pos, rect, lightColor, Projectile.rotation, orig, 1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
