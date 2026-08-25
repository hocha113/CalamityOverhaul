using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 燃地:焰弹落地留下的燃烧区,火焰技能的"地形化"载体<br/>
    /// ai[0]=寿命帧(日耀主场传更长) ai[1]=半宽px<br/>
    /// 公平阀:出生 20 帧无判定;寿命尾段 50 帧渐熄且无判定(伤害窗=可见窗)
    /// </summary>
    internal class CultistBurnPatch : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Timer => ref Projectile.localAI[0];
        private int LifeFrames => (int)MathHelper.Max(Projectile.ai[0], 60f);
        private float HalfWidth => MathHelper.Max(Projectile.ai[1], 40f);

        private const int IgniteFrames = 20;
        private const int FadeFrames = 50;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 900;
            Projectile.netImportant = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>燃烧强度 0~1:点燃升,尾段熄</summary>
        private float Intensity {
            get {
                float rise = MathHelper.Clamp(Timer / IgniteFrames, 0f, 1f);
                float fall = MathHelper.Clamp((LifeFrames - Timer) / (float)FadeFrames, 0f, 1f);
                return MathHelper.Min(rise, fall);
            }
        }

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;
            if (Timer >= LifeFrames) {
                Projectile.Kill();
                return;
            }

            float strength = Intensity;
            //火舌粒子:密度随强度,这就是它的"体"(火=光,加色豁免)
            if (!VaultUtils.isServer && strength > 0.1f && CultistMotion.OnScreen(Projectile.Center, 300f)) {
                if (Main.rand.NextFloat() < 0.55f * strength) {
                    Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth), 6f);
                    PRTLoader.NewParticle<PRT_CultistEmber>(pos,
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(1.2f, 2.8f)),
                        Color.Lerp(CultistMotion.SolarCore, CultistMotion.SolarEdge, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.9f, 1.6f))?.Configure(Main.rand.Next(18, 32), 0.10f);
                }
            }
            Lighting.AddLight(Projectile.Center, CultistMotion.SolarEdge.ToVector3() * 0.8f * strength);
        }

        /// <summary>伤害窗=可见窗:燃烧强度过半才咬人</summary>
        public override bool CanHitPlayer(Player target) => Intensity > 0.5f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Rectangle zone = new(
                (int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - 26f),
                (int)(HalfWidth * 2f), 44);
            return zone.Intersects(targetHitbox);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire3, 120);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            float strength = Intensity;
            if (strength < 0.02f) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition + new Vector2(0f, 2f);
            //贴地扁晕:双层,宽体+亮芯,火是光,加色
            Color edge = CultistMotion.SolarEdge with { A = 0 };
            Color core = CultistMotion.SolarCore with { A = 0 };
            float w = HalfWidth * 2f / glow.Width;
            float flicker = 0.85f + 0.15f * (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 11f + Projectile.identity);
            Main.spriteBatch.Draw(glow, pos, null, edge * (0.55f * strength * flicker), 0f,
                glow.Size() * 0.5f, new Vector2(w, 0.34f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(glow, pos, null, core * (0.45f * strength * flicker), 0f,
                glow.Size() * 0.5f, new Vector2(w * 0.6f, 0.22f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
