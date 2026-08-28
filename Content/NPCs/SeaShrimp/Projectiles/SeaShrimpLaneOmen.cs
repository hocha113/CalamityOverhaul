using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 泡幕车道预告:实体预告(预告即承诺)——服务器在锁列帧按最终列位生成,
    /// 缺口列不生成,可见缺口=真实缺口。无伤害,上升的泡沫标记柱指明泡列将至。
    /// ai[0]=预告帧数(其后 16f 余晖淡出)
    /// </summary>
    internal class SeaShrimpLaneOmen : SeaShrimpModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "DiffusionCircle")]
        private static Asset<Texture2D> RingTex = null;

        private int OmenFrames => (int)Projectile.ai[0];
        private const int FadeFrames = 16;
        /// <summary>标记柱纵向覆盖范围 px</summary>
        private const float ColumnSpan = 620f;

        /// <summary>本地帧龄:逐端计数,迟入端不重播预告</summary>
        private int Age => (int)Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            if (Age >= OmenFrames + FadeFrames) {
                Projectile.Kill();
                return;
            }
            Lighting.AddLight(Projectile.Center, 0.03f, 0.08f, 0.16f);
            //上浮微沫:标记柱里的活水暗示
            if (!Main.dedServ && Main.GameUpdateCount % 5 == 0 && Age < OmenFrames) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-0.5f, 0.5f) * ColumnSpan * 0.5f),
                    new Vector2(0f, -Main.rand.NextFloat(0.8f, 1.6f)),
                    SeaShrimpVFX.Glow * 0.7f, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(14);
            }
        }

        public override bool? CanDamage() => false;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ring = RingTex?.Value;
            if (ring == null) {
                return false;
            }
            int age = Age;
            float alpha = MathHelper.Clamp(age / 8f, 0f, 1f);
            if (age > OmenFrames) {
                alpha *= 1f - (age - OmenFrames) / (float)FadeFrames;
            }
            if (alpha <= 0.02f) {
                return false;
            }

            //上升的泡沫标记串:确定性相位,各端一致
            float rise = age * 2.6f;
            for (int i = 0; i < 6; i++) {
                float offset = (rise + i * (ColumnSpan / 6f)) % ColumnSpan;
                Vector2 pos = Projectile.Center + new Vector2(0f, ColumnSpan * 0.5f - offset) - Main.screenPosition;
                //端部软收:柱两端渐隐
                float endFade = MathF.Sin(offset / ColumnSpan * MathF.PI);
                float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + i * 1.7f + Projectile.identity);
                float scale = 11f / ring.Width * pulse;
                Main.spriteBatch.Draw(ring, pos, null, SeaShrimpVFX.Film * (0.34f * alpha * endFade), 0f,
                    ring.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
