using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>霜的演出集中处：镜面结晶闪、踏镜霜步、碎镜冰锥，全部端本地纯表现</summary>
    internal static class FuShuangFX
    {
        /// <summary>镜面结晶微闪：洼面上偶发一粒白晶亮点，卖"这不是墨是镜"</summary>
        internal static void MirrorSurfaceGlint(Projectile puddle, Color accent) {
            if (Main.dedServ || !Main.rand.NextBool(14)) {
                return;
            }
            float w = KikasaInkPuddle.WidthPx * (puddle.ai[0] > 0.01f ? puddle.ai[0] : 1f);
            Vector2 pos = puddle.Center
                + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * w, -Main.rand.NextFloat(1f, 4f));
            PRTLoader.NewParticle<PRT_Sparkle>(pos, -Vector2.UnitY * 0.2f,
                Color.Lerp(accent, Color.White, 0.5f), 0.24f)
                ?.Configure(accent * 0.5f, Main.rand.Next(14, 22), 0.05f, 0.7f);
        }

        /// <summary>踏镜霜步：接触扫描拍上一小蓬冰晶碎屑（所有者端本地；旁观靠叠层霜凇）</summary>
        internal static void FrostStep(NPC npc, Color accent) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Line>(
                    npc.Bottom + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * npc.width, -2f),
                    new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(0.8f, 1.8f)),
                    Color.Lerp(accent, Color.White, 0.5f) * 0.6f,
                    Main.rand.NextFloat(0.25f, 0.4f))?.Configure(true, 12);
            }
        }

        /// <summary>
        /// 碎镜：白闪脆响+镜片横飞+冰锥自碎点立起。
        /// 由 OnDropKill 在各客户端本地调用，旁观端同样看得到；伤害走独立爆伤弹幕
        /// </summary>
        internal static void MirrorShatter(Vector2 center, float widthPx, float lifeFrac, Color accent) {
            if (Main.dedServ) {
                return;
            }
            float power = 0.5f + lifeFrac;
            KikasaInk.Play(SoundID.Shatter, center, 0.42f * power, 0.05f, 3);
            KikasaInk.Play(SoundID.Item27, center, 0.4f, 0.25f, 3);

            //碎裂白闪：一圈冷白脉冲
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(center, Vector2.Zero,
                Color.Lerp(accent, Color.White, 0.4f) * 0.6f, 0.1f)?.Configure(0.08f, 0.7f * power, 12);

            //镜片横飞：贴地半圆抛洒的白亮短线
            int shards = (int)(4 + 4 * lifeFrac);
            for (int i = 0; i < shards; i++) {
                Vector2 vel = (-MathHelper.PiOver2 + Main.rand.NextFloat(-1.2f, 1.2f)).ToRotationVector2()
                    * Main.rand.NextFloat(2.5f, 6f) * power;
                PRTLoader.NewParticle<PRT_Line>(
                    center + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * widthPx, -2f),
                    vel, Color.Lerp(accent, Color.White, 0.65f) * 0.8f,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(12, 20));
            }

            //立冰锥：碎点上几根骤起骤定的竖直冰线，一瞬立起再碎成霜雾
            int spikes = (int)(3 + 2 * lifeFrac);
            for (int i = 0; i < spikes; i++) {
                float xOff = (i - (spikes - 1) * 0.5f) * widthPx * 0.22f
                    + Main.rand.NextFloat(-5f, 5f);
                PRTLoader.NewParticle<PRT_Line>(center + new Vector2(xOff, -4f),
                    -Vector2.UnitY * Main.rand.NextFloat(5f, 9f) * power,
                    Color.Lerp(accent, Color.White, 0.8f) * 0.9f,
                    Main.rand.NextFloat(0.55f, 0.85f))?.Configure(false, Main.rand.Next(10, 15));
            }

            //霜雾一口：冷白低伏
            PRTLoader.NewParticle<PRT_KikasaInkMist>(center - Vector2.UnitY * 6f,
                -Vector2.UnitY * 0.6f, Color.Lerp(accent, Color.White, 0.3f) * 0.8f,
                Main.rand.NextFloat(0.8f, 1.1f))?.Configure(Main.rand.Next(24, 34));
        }

        /// <summary>叠层霜凇：踏镜敌脚下的薄霜线与两粒晶屑，随印记计时渐融（旁观端也画）</summary>
        internal static void DrawFrostRime(SpriteBatch sb, NPC npc, int timerFrames,
            Vector2 screenPos, Color accent) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            if (soft == null) {
                return;
            }
            float alpha = MathHelper.Clamp(timerFrames / 12f, 0f, 1f) * 0.4f;
            if (alpha <= 0.02f) {
                return;
            }
            Vector2 feet = npc.Bottom - screenPos - new Vector2(0f, 2f);
            //脚下薄霜线：真透明贴图压扁成一线冷白
            sb.Draw(soft, feet, null, Color.Lerp(accent, Color.White, 0.5f) * alpha, 0f,
                soft.Size() * 0.5f,
                new Vector2(npc.width * 1.1f / soft.Width, 5f / soft.Height), SpriteEffects.None, 0f);
            //两粒晶屑：错相闪烁的小菱点
            Texture2D pixel = VaultAsset.placeholder2.Value;
            for (int i = 0; i < 2; i++) {
                float t = Main.GlobalTimeWrappedHourly * 2.2f + npc.whoAmI * 1.7f + i * 2.6f;
                float blink = 0.5f + 0.5f * MathF.Sin(t);
                Vector2 pos = feet + new Vector2((i == 0 ? -1f : 1f) * npc.width * 0.24f,
                    -3f - 2f * blink);
                sb.Draw(pixel, pos, new Rectangle(0, 0, 1, 1),
                    (Color.White with { A = 0 }) * (alpha * blink), MathHelper.PiOver4,
                    new Vector2(0.5f), new Vector2(2.6f), SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 霜·碎镜爆伤：不可见的一瞬 AoE 判定，伤害与半径随生成包自含
    /// （ai[0]=判定半径 px）；演出由 <see cref="FuShuangFX.MirrorShatter"/> 在各端先行
    /// </summary>
    internal class FuShuangShatterProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>判定半径（px），生成包 ai[0]</summary>
        private ref float Radius => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                int size = (int)(MathHelper.Clamp(Radius <= 0f ? 80f : Radius, 40f, 160f) * 2f);
                Projectile.Resize(size, size);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
