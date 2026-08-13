using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles
{
    /// <summary>
    /// 脉络预警线（无伤纯预告）：ai[0]=方向弧度 ai[1]=长度 ai[2]=总寿命
    /// 视觉是充血鼓胀的血管，不是激光
    /// </summary>
    internal class BrainVeinTelegraph : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float Direction => Projectile.ai[0];
        private float Length => Projectile.ai[1];
        private int TotalLife => (int)Projectile.ai[2];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 900;
        }

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Age++;
            Projectile.velocity = Vector2.Zero;
            if (TotalLife > 0 && Age >= TotalLife && !VaultUtils.isClient) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D line = CWRAsset.MaskLaserLine.Value;
            float lifeT = TotalLife > 0 ? MathHelper.Clamp(Age / TotalLife, 0f, 1f) : 0.5f;
            //淡入-驻留-淡出包络
            float fadeIn = MathHelper.Clamp(Age / 10f, 0f, 1f);
            float fadeOut = MathHelper.Clamp((1f - lifeT) * 4f, 0f, 1f);
            float alpha = fadeIn * fadeOut;
            if (alpha <= 0.01f) {
                return false;
            }

            //节拍充血鼓胀
            float swell = 1f + BrainHeartbeat.Pulse * 0.5f + (float)Math.Sin(Age * 0.22f) * 0.08f;

            Vector2 start = Projectile.Center - Main.screenPosition;
            Vector2 scaleOuter = new Vector2(Length / line.Width, 0.5f * swell);
            Vector2 scaleInner = new Vector2(Length / line.Width, 0.2f * swell);
            Vector2 origin = new Vector2(0f, line.Height * 0.5f);

            //暗脉衬底+亮血芯（加色）
            Main.spriteBatch.Draw(line, start, null, new Color(96, 10, 20, 0) * (0.55f * alpha),
                Direction, origin, scaleOuter, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(line, start, null, new Color(205, 40, 48, 0) * (0.7f * alpha),
                Direction, origin, scaleInner, SpriteEffects.None, 0f);
            return false;
        }
    }
}
