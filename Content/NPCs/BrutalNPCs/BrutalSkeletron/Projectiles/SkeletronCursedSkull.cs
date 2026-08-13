using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles
{
    /// <summary>诅咒颅火：幽蓝骷髅弹，ai[0]=1 时带初段弱追踪</summary>
    internal class SkeletronCursedSkull : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Skull;

        /// <summary>弱追踪窗口帧数</summary>
        private const int HomingWindow = 90;

        private ref float HomingMode => ref Projectile.ai[0];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            //沿用原版颅骨贴图的三帧竖排
            Main.projFrames[Type] = 3;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Age++;

            //初段淡入（公平阀：出膛不打脸）
            Projectile.alpha = (int)MathHelper.Lerp(200f, 0f, MathHelper.Clamp(Age / 10f, 0f, 1f));

            //弱追踪窗口
            if (HomingMode == 1f && Age < HomingWindow) {
                int target = Player.FindClosest(Projectile.position, Projectile.width, Projectile.height);
                if (target >= 0) {
                    Vector2 want = (Main.player[target].Center - Projectile.Center).SafeNormalize(Vector2.UnitY)
                        * Projectile.velocity.Length();
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.022f);
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //三帧循环
            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }

            //幽火剥落
            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    -Projectile.velocity * 0.16f,
                    SkeletronRenderHelper.GhostCyan, Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(14, 24));
            }

            Lighting.AddLight(Projectile.Center, SkeletronRenderHelper.GhostCyan.ToVector3() * 0.42f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(Projectile.Center,
                    Main.rand.NextVector2Circular(2.6f, 2.6f),
                    SkeletronRenderHelper.GhostDeep, Main.rand.NextFloat(1f, 1.6f))?.Configure(Main.rand.Next(16, 26));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle rect = tex.GetRectangle(Projectile.frame, Main.projFrames[Type]);
            Vector2 orig = rect.Size() / 2f;
            float opacity = 1f - Projectile.alpha / 255f;

            //拖尾残影（预乘批 A=0 加色）
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = (1f - i / (float)Projectile.oldPos.Length) * 0.4f * opacity;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, rect,
                    SkeletronRenderHelper.AsAdditive(SkeletronRenderHelper.GhostDeep) * fade,
                    Projectile.oldRot[i], orig, Projectile.scale * (1f - i * 0.05f), SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            //三层幽灵体
            Main.EntitySpriteDraw(tex, drawPos, rect, SkeletronRenderHelper.GhostDeep * (0.85f * opacity),
                Projectile.rotation, orig, Projectile.scale * 1.18f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, rect, SkeletronRenderHelper.GhostCyan * (0.85f * opacity),
                Projectile.rotation, orig, Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, rect, new Color(230, 255, 250, 0) * (0.6f * opacity),
                Projectile.rotation, orig, Projectile.scale * 0.82f, SpriteEffects.None, 0);
            return false;
        }
    }
}
