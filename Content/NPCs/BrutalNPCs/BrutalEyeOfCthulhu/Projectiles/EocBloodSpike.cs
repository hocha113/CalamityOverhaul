using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Projectiles
{
    /// <summary>
    /// 血棘：大招螺旋弹幕，慢出膛复合增压+恒定微旋（各端确定性同轨）<br/>
    /// ai[0]=旋向(±1)，ai[1]=每帧转角(弧度)
    /// </summary>
    internal class EocBloodSpike : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 255;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            //复合增压到上限
            if (Projectile.velocity.Length() < 19f) {
                Projectile.velocity *= 1.024f;
            }
            //恒定微旋成螺旋臂，转角由 ai 同步，全端一致
            float curl = Projectile.ai[1] * (Projectile.ai[0] >= 0f ? 1f : -1f);
            Projectile.velocity = Projectile.velocity.RotatedBy(curl);

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!VaultUtils.isServer && Main.rand.NextBool(6) && EocMotion.OnScreen(Projectile.Center, 200f)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center, -Projectile.velocity * 0.1f,
                    EocMotion.Arterial * 0.8f, Main.rand.NextFloat(0.4f, 0.8f))?
                    .Configure(Main.rand.Next(12, 20), 0.24f, 0.98f);
            }

            Lighting.AddLight(Projectile.Center, EocMotion.Arterial.ToVector3() * 0.35f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Bleeding, 120);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer || !EocMotion.OnScreen(Projectile.Center, 300f)) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                    Main.rand.NextVector2Circular(3f, 3f), EocMotion.Arterial,
                    Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(14, 24), 0.32f, 0.98f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tear = CWRAsset.SoftGlow.Value;
            Texture2D body = CWRAsset.Extra_98.Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.055f, 0.3f, 1.15f);

            //旧位拖影
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i -= 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(body, oldDrawPos, null, EocMotion.VenousDark * (0.5f * t),
                    Projectile.oldRot[i], body.Size() * 0.5f,
                    new Vector2(0.26f, 0.5f * (1f + stretch)) * t, SpriteEffects.None, 0);
            }

            //暗鞘+亮芯，锥形血棘
            Vector2 bodyScale = new(0.3f * (1f - stretch * 0.25f), 0.62f * (1f + stretch * 1.4f));
            Main.EntitySpriteDraw(body, pos, null, EocMotion.VenousDark, Projectile.rotation,
                body.Size() * 0.5f, bodyScale * 1.22f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(body, pos, null, EocMotion.BrightBlood, Projectile.rotation,
                body.Size() * 0.5f, bodyScale, SpriteEffects.None, 0);
            //尖端微光（加色小点，不作本体）
            Vector2 tip = pos + Projectile.velocity.SafeNormalize(Vector2.Zero) * 14f * stretch;
            Main.EntitySpriteDraw(tear, tip, null, (EocMotion.BrightBlood with { A = 0 }) * 0.55f,
                0f, tear.Size() * 0.5f, 0.3f, SpriteEffects.None, 0);
            return false;
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }
    }
}
