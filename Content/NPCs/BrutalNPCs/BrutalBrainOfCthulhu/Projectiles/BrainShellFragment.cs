using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles
{
    /// <summary>
    /// 护壳碎片：转阶段崩壳抛射，重力弧线+自旋，触地即碎
    /// ai[0]=碎块象限 0~3（取闭壳帧的四分之一区域），ai[1]=自旋速度
    /// </summary>
    internal class BrainShellFragment : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int Quadrant => (int)Projectile.ai[0] % 4;
        private float SpinRate => Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            Projectile.velocity.Y += 0.3f;
            if (Projectile.velocity.Y > 16f) {
                Projectile.velocity.Y = 16f;
            }
            Projectile.velocity.X *= 0.995f;
            Projectile.rotation += SpinRate;

            //拖血
            if (!VaultUtils.isServer && Main.rand.NextBool(6) && BrainMotion.OnScreen(Projectile.Center)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    BrainMotion.BloodDark, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(16, 28), 0.3f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer || !BrainMotion.OnScreen(Projectile.Center)) {
                return;
            }
            BrainMotion.BloodMistBurst(Projectile.Center, 0.8f, 5, 6f);
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.5f,
                Pitch = -0.5f,
                MaxInstances = 6,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = BrainRenderHelper.GetBrainTexture();
            if (tex == null) {
                return false;
            }
            //闭壳首帧切象限当碎块
            Rectangle full = BrainRenderHelper.GetFrameRect(tex, 0);
            int halfW = full.Width / 2;
            int halfH = full.Height / 2;
            Rectangle chunk = new Rectangle(
                full.X + (Quadrant % 2) * halfW,
                full.Y + (Quadrant / 2) * halfH,
                halfW, halfH);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, drawPos, chunk, lightColor,
                Projectile.rotation, chunk.Size() * 0.5f, Projectile.scale * 0.66f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
