using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles
{
    /// <summary>
    /// 蜕变壳屑：旧皮撕裂甩离的灰紫空壳，自旋坠落、短暂带伤后碎散。
    /// 伤害窗=可见坠势（速度门）；短寿命自净，不与任何后续机制纠缠。
    /// </summary>
    internal class FssHuskShard : FssModProjectile
    {
        public override string Texture => CWRConstant.NPC + "BSS/Body";

        private float spin;

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                spin = Main.rand.NextFloat(-0.22f, 0.22f);
                Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            Projectile.velocity.Y += 0.32f;
            if (Projectile.velocity.Y > 14f) {
                Projectile.velocity.Y = 14f;
            }
            Projectile.velocity.X *= 0.995f;
            Projectile.rotation += spin;

            //伤害窗=可见坠势
            Projectile.hostile = Projectile.velocity.Length() > 6f;

            //旧壳掉渣
            if (!VaultUtils.isServer && Main.rand.NextBool(6)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.CorruptGibs, Projectile.velocity * 0.1f, 90, default, Main.rand.NextFloat(0.7f, 1.1f));
                d.noGravity = false;
            }

            //末段淡出
            if (Projectile.timeLeft < 20) {
                Projectile.alpha = (int)MathHelper.Clamp(Projectile.alpha + 14, 0, 255);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    DustID.CorruptGibs, Main.rand.NextVector2Circular(2.5f, 2f) - new Vector2(0f, 1f),
                    80, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //旧壳：体节素帧染灰紫（死皮，无灵液光）
            Main.instance.LoadProjectile(Type);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frame = new(0, 0, tex.Width, tex.Height / 2);
            Vector2 origin = frame.Size() / 2f;
            float fade = 1f - Projectile.alpha / 255f;
            Color husk = lightColor.MultiplyRGB(new Color(150, 132, 168));
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame,
                husk * (0.85f * fade), Projectile.rotation, origin, Projectile.scale * 0.95f, SpriteEffects.None, 0);
            return false;
        }
    }
}
