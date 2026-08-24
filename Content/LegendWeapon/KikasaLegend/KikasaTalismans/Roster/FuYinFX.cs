using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>洇的演出集中处：NPC 渍斑逐层晕开的画法与墨花爆的伴生弹幕</summary>
    internal static class FuYinFX
    {
        /// <summary>墨紫身份色，定义与演出同源取此</summary>
        internal static readonly Color Accent = new(142, 108, 182);

        /// <summary>
        /// 洇痕渍斑：一层一渍，紫晕垫底墨体压顶；新墨落定带回弹，
        /// 随留存流逝缓慢晕开。NPC PostDraw 批内简单精灵绘制，端本地
        /// </summary>
        internal static void DrawStains(SpriteBatch sb, NPC npc,
            int stacks, int timerFrames, Vector2 screenPos, Color drawColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || stacks <= 0) {
                return;
            }
            float lum = (drawColor.R + drawColor.G + drawColor.B) / 765f;
            float alpha = npc.Opacity * (0.35f + 0.65f * lum);
            if (alpha <= 0.02f) {
                return;
            }
            //新墨回弹：写入后 36 帧内轻微鼓起；晕开：随留存消逝整体缓涨
            float fresh = MathHelper.Clamp(
                (timerFrames - (FuYin.StackLifeFrames - 36)) / 36f, 0f, 1f);
            float pop = 1f + 0.30f * fresh;
            float spread = 1f + 0.18f * (1f - timerFrames / (float)FuYin.StackLifeFrames);
            Vector2 origin = tex.Size() * 0.5f;

            for (int i = 0; i < stacks; i++) {
                //确定性布点：渍要钉在身上同一处，不随帧乱跳
                float hx = KikasaInk.Hash(npc.whoAmI * 977 + i, 11);
                float hy = KikasaInk.Hash(npc.whoAmI * 977 + i, 47);
                Vector2 pos = npc.Center - screenPos
                    + new Vector2((hx - 0.5f) * npc.width * 0.72f, (hy - 0.5f) * npc.height * 0.62f);
                float size = (MathF.Min(npc.width, 90f) * 0.34f + 6f + i * 2f)
                    * spread * (i == stacks - 1 ? pop : 1f);
                float rot = hx * MathHelper.TwoPi;
                //紫晕在下（洇开的缘），墨体在上（落墨的心）
                sb.Draw(tex, pos, null, Accent * (alpha * 0.26f), rot, origin,
                    size * 1.5f / tex.Width, SpriteEffects.None, 0f);
                sb.Draw(tex, pos, null, KikasaInk.InkBody * (alpha * 0.55f), rot, origin,
                    new Vector2(size * 1.15f, size * 0.9f) / tex.Width, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 洇·墨花爆：满五层洇痕在命中处绽开的一朵墨花。
    /// 判定盒即花径，一花对每个敌人只咬一口；瓣与雾各端本地绽放
    /// </summary>
    internal class FuYinInkBurst : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 18;
        private const int PetalCount = 6;

        private bool bloomed;

        /// <summary>确定性相位：花瓣朝向各端一致</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        public override void SetDefaults() {
            Projectile.width = 150;
            Projectile.height = 150;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (!bloomed) {
                bloomed = true;
                Bloom();
            }
        }

        /// <summary>绽放拍：声墨齐出，渍斑余韵挂上最近的宿主（各端本地）</summary>
        private void Bloom() {
            if (Main.dedServ) {
                return;
            }
            NPC host = FindHost();
            if (host != null) {
                KikasaInkFX.AddNpcSplat(host, Projectile.Center, Vector2.UnitY * 7f, 48f);
            }
            KikasaInk.Play(KikasaInk.InkFlick, Projectile.Center, 0.55f, -0.55f, 3);
            KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.62f, -0.35f, 4);

            //紫晕冲击环 + 墨雾喷散 + 半空溅珠
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(Projectile.Center, Vector2.Zero,
                FuYinFX.Accent * 0.55f, 0.12f)?.Configure(0.12f, 0.95f, 14);
            for (int i = 0; i < 4; i++) {
                Vector2 dir = (Seed + MathHelper.TwoPi * i / 4f).ToRotationVector2();
                PRTLoader.NewParticle<PRT_KikasaInkMist>(Projectile.Center + dir * 14f,
                    dir * Main.rand.NextFloat(0.8f, 1.6f), KikasaInk.InkDeep,
                    Main.rand.NextFloat(0.9f, 1.3f))?.Configure(Main.rand.Next(26, 38));
            }
            for (int i = 0; i < 7; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4.6f, 3.6f) - Vector2.UnitY * 1.6f;
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f), vel,
                    Main.rand.NextBool(3) ? FuYinFX.Accent : KikasaInk.InkBody,
                    Main.rand.NextFloat(0.16f, 0.28f))?.Configure(Main.rand.Next(18, 30));
            }
        }

        /// <summary>爆心附近最近的可沾渍宿主：花开在谁身上，渍就留给谁</summary>
        private NPC FindHost() {
            NPC best = null;
            float bestDist = 90f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || npc.friendly || npc.dontTakeDamage) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>墨花本体：六瓣放射渐开，紫缘墨体白心，开到荼蘼一并淡去</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float t = 1f - Projectile.timeLeft / (float)LifeFrames;
            float grow = 1f - (1f - t) * (1f - t);
            float alpha = 1f - t * t;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;

            for (int i = 0; i < PetalCount; i++) {
                float ang = Seed + MathHelper.TwoPi * i / PetalCount;
                Vector2 dir = ang.ToRotationVector2();
                float len = (26f + 44f * grow) * (0.85f + 0.3f * KikasaInk.Hash(Projectile.identity, i));
                Vector2 petalPos = pos + dir * len * 0.55f;
                Vector2 petalScale = new Vector2(len * 1.35f, 17f + 8f * grow) / tex.Width;
                //瓣：紫晕垫底、墨体收瓣
                Main.EntitySpriteDraw(tex, petalPos, null, FuYinFX.Accent * (alpha * 0.34f),
                    ang, origin, petalScale * 1.25f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, petalPos, null, KikasaInk.InkBody * (alpha * 0.8f),
                    ang, origin, petalScale, SpriteEffects.None, 0);
            }
            //花心：一点白亮压住构图（冷墨允许亮心）
            Main.EntitySpriteDraw(tex, pos, null, Color.Lerp(FuYinFX.Accent, Color.White, 0.55f) * (alpha * 0.6f),
                0f, origin, new Vector2(24f + 10f * grow) / tex.Width, SpriteEffects.None, 0);
            return false;
        }
    }
}
