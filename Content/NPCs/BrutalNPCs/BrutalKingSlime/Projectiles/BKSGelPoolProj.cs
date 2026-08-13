using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>地面凝胶滞留池；ai[0]=池宽px ai[1]=存留帧；踩入减速+低伤；服务端生成</summary>
    internal class BKSGelPoolProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int GrowTime = 18;
        private const int DrainTime = 40;

        private float PoolWidth => Projectile.ai[0] <= 0f ? 130f : Projectile.ai[0];
        private int HoldTime => (int)(Projectile.ai[1] <= 0f ? 200f : Projectile.ai[1]);
        private int TotalLife => GrowTime + HoldTime + DrainTime;

        private ref float Timer => ref Projectile.localAI[0];

        /// <summary>铺开进度 0~1</summary>
        private float Spread => MathHelper.Clamp(Timer / GrowTime, 0f, 1f);
        /// <summary>排空进度 0~1</summary>
        private float Drain => MathHelper.Clamp((Timer - GrowTime - HoldTime) / DrainTime, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Timer++;
            if (Timer >= TotalLife) {
                Projectile.Kill();
                return;
            }

            //池面冒泡
            if (!VaultUtils.isServer && Drain < 0.5f && Main.rand.NextBool(9)) {
                KingSlimeGelFX.BubbleFizz(Projectile.Center + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * PoolWidth * Spread, -8f), 8f, 1);
            }

            Lighting.AddLight(Projectile.Center, KingSlimeGelFX.GelMid.ToVector3() * 0.3f * Spread * (1f - Drain));
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Slimed, 180);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //扁平判定带：池宽×浅高
            float halfW = PoolWidth * 0.5f * Spread * (1f - Drain * 0.8f);
            Rectangle poolRect = new Rectangle(
                (int)(Projectile.Center.X - halfW), (int)(Projectile.Center.Y - 18f),
                (int)(halfW * 2f), 24);
            return poolRect.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.BKSGelPool?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (shader == null || noise == null) {
                //着色器不可用：扁平凝胶渍回退，绝不许无形判定
                Texture2D blob = CWRAsset.Extra_98?.Value;
                if (blob != null) {
                    float w = PoolWidth * Spread * (1f - Drain * 0.8f);
                    Color gel = Color.Lerp(KingSlimeGelFX.GelMid, KingSlimeGelFX.GelDeep, 0.45f) * 0.65f;
                    Main.EntitySpriteDraw(blob, Projectile.Center - Main.screenPosition - new Vector2(0f, 6f), null,
                        gel, 0f, blob.Size() * 0.5f,
                        new Vector2(w / blob.Width, 26f / blob.Height), SpriteEffects.None, 0);
                }
                return false;
            }

            KingSlimeGelFX.SetPoolParams(shader,
                spread: Spread,
                drain: Drain,
                alpha: 0.9f,
                boil: 0.25f,
                seed: Projectile.whoAmI * 0.137f % 1f);

            //画布：池宽 × 60px 高，中心对准池心，底边贴地
            Vector2 quadSize = new Vector2(PoolWidth * 1.05f, 62f);
            KingSlimeGelFX.DrawShaderQuad(shader, noise, Projectile.Center + new Vector2(0f, -quadSize.Y * 0.5f + 12f), quadSize, 1f);
            return false;
        }
    }
}
