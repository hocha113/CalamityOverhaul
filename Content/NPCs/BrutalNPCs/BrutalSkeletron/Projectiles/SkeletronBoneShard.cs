using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Projectiles
{
    /// <summary>骨风暴骨片：翻滚飞行，ai[0]=每帧弧旋弧度（螺旋幕用）</summary>
    internal class SkeletronBoneShard : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bone;

        private ref float CurveRate => ref Projectile.ai[0];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 340;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Age++;

            //出膛淡入
            Projectile.alpha = (int)MathHelper.Lerp(220f, 0f, MathHelper.Clamp(Age / 12f, 0f, 1f));

            //弧旋（骨风暴螺旋幕）
            if (CurveRate != 0f) {
                Projectile.velocity = Projectile.velocity.RotatedBy(CurveRate);
            }

            //缓慢加速外扩
            if (Projectile.velocity.Length() < 8f) {
                Projectile.velocity *= 1.008f;
            }

            //骨片翻滚
            Projectile.rotation += 0.22f * (CurveRate >= 0f ? 1f : -1f);

            Lighting.AddLight(Projectile.Center, SkeletronRenderHelper.GhostDeep.ToVector3() * 0.2f);
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > 12 ? null : false;

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_SkeleBoneChip>(Projectile.Center,
                    Main.rand.NextVector2Circular(2f, 2f), Color.White,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(26, 44));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float opacity = 1f - Projectile.alpha / 255f;

            //幽蓝残象衬底（预乘批 A=0 加色）
            Main.EntitySpriteDraw(tex, drawPos - Projectile.velocity * 0.6f, null,
                SkeletronRenderHelper.AsAdditive(SkeletronRenderHelper.GhostDeep) * (0.45f * opacity),
                Projectile.rotation - 0.3f, orig, Projectile.scale, SpriteEffects.None, 0);
            //本体骨白
            Main.EntitySpriteDraw(tex, drawPos, null,
                Color.Lerp(SkeletronRenderHelper.BonePale, lightColor, 0.35f) * opacity,
                Projectile.rotation, orig, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
