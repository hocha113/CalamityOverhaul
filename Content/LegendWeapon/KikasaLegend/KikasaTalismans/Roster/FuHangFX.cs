using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>沆的演出集中处：夜洼萤绿浮点，各端本地纯表现</summary>
    internal static class FuHangFX
    {
        /// <summary>夜洼萤绿浮点+薄雾：OnPuddleUpdate 各端逐帧调用（AI 线程，可安全生成 PRT）</summary>
        internal static void PuddleNightMotes(Projectile puddle, Color accent) {
            float radiusMul = puddle.ai[0] > 0.01f ? puddle.ai[0] : 1f;
            float halfW = KikasaInkPuddle.WidthPx * radiusMul * 0.42f;
            //萤绿浮点：自洼面缓缓升起
            if (Main.rand.NextBool(11)) {
                PRTLoader.NewParticle<PRT_Light>(
                    puddle.Center + new Vector2(Main.rand.NextFloat(-1f, 1f) * halfW, -3f),
                    new Vector2(Main.rand.NextFloat(-0.14f, 0.14f), -Main.rand.NextFloat(0.3f, 0.8f)),
                    accent * 0.65f, Main.rand.NextFloat(0.12f, 0.2f))
                    ?.Configure(Main.rand.Next(26, 44), 0.65f);
            }
            //洼面薄瘴：偶发一口贴面雾
            if (Main.rand.NextBool(26)) {
                PRTLoader.NewParticle<PRT_KikasaInkMist>(
                    puddle.Center + new Vector2(Main.rand.NextFloat(-0.6f, 0.6f) * halfW, -4f),
                    new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.5f)),
                    new Color(46, 74, 48), Main.rand.NextFloat(0.6f, 0.9f))
                    ?.Configure(Main.rand.Next(30, 44));
            }
        }
    }

    /// <summary>
    /// 沆的瘴雾柱：夜里自墨洼面蒸腾而起的缓升伤害柱，沆符（<see cref="FuHang"/>）专属。
    /// 仅所有者端生成（伤害自然同步）；雾体为 Fog 真 alpha 分层堆叠+萤点缀芯，
    /// 内部翻涌走逐层慢旋与镜像错相，材质是"瘴雾"不是光团
    /// </summary>
    internal class FuHangMiasmaColumn : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //雾体贴图：Fog 是真 alpha 单帧烟羽，可直接染色
        [VaultLoaden(CWRConstant.Masking + "Fog")]
        private static ReLogic.Content.Asset<Texture2D> fogTex = null;

        private const int LifeFrames = 150;

        private float life;

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>淡入 14 帧、末 24 帧散尽</summary>
        private float Envelope => MathF.Min(MathHelper.Clamp(life / 14f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = 34;
            Projectile.height = 78;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //缓升柱 0.5s 一轮判定
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        /// <summary>成形前与散尽尾段不咬人，判定窗与雾体浓度同步</summary>
        public override bool? CanDamage() => Envelope > 0.45f ? null : false;

        public override void AI() {
            life++;
            //缓升+极轻的横向游移：瘴气在飘不是在飞
            Projectile.velocity = new Vector2(
                MathF.Sin(life * 0.045f + Seed * 3f) * 0.22f, -0.55f);

            if (Main.dedServ) {
                return;
            }
            //体内萤点与逸散雾丝
            if (Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 30f),
                    new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.6f)),
                    new Color(112, 178, 118) * 0.6f, Main.rand.NextFloat(0.1f, 0.18f))
                    ?.Configure(Main.rand.Next(18, 30), 0.6f);
            }
            if (Main.rand.NextBool(18)) {
                PRTLoader.NewParticle<PRT_KikasaInkMist>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 26f),
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.3f, 0.7f)),
                    new Color(46, 74, 48), Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(Main.rand.Next(24, 38));
            }
            Lighting.AddLight(Projectile.Center, 0.05f * Envelope, 0.11f * Envelope, 0.06f * Envelope);
        }

        //====绘制：Fog 真 alpha 三层堆叠，逐层慢旋+镜像错相防贴纸感====

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = fogTex?.Value;
            float env = Envelope;
            if (fog == null || env <= 0.02f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = fog.Size() * 0.5f;
            Vector2 basePos = Projectile.Center - Main.screenPosition;
            float time = Main.GlobalTimeWrappedHourly;

            //三层雾体：底大顶小沿柱堆叠，逐层反向慢旋、按 identity 定镜像
            for (int i = 0; i < 3; i++) {
                float yOff = (i - 1f) * 26f;
                float size = (52f - i * 10f) * (0.7f + 0.3f * env);
                float rot = time * (0.14f + 0.05f * i) * (i % 2 == 0 ? 1f : -1f) + Seed + i * 2.1f;
                SpriteEffects flip = ((Projectile.identity + i) & 1) == 0
                    ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                float wob = 1f + MathF.Sin(time * 1.6f + Seed * 2f + i) * 0.07f;
                Color deep = new Color(30, 48, 32) * (0.5f * env);
                Color body = new Color(52, 82, 54) * (0.38f * env);
                sb.Draw(fog, basePos + new Vector2(0f, yOff), null, deep, rot, origin,
                    size * 1.15f / fog.Width * wob, flip, 0f);
                sb.Draw(fog, basePos + new Vector2(MathF.Sin(time + i * 1.7f + Seed) * 3f, yOff),
                    null, body, -rot * 0.7f, origin, size / fog.Width * wob, flip, 0f);
            }

            //芯部萤绿微光：小占比 A=0 加色，读作瘴气里浮着的磷光
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null) {
                Color core = new Color(112, 178, 118) with { A = 0 };
                float pulse = 0.8f + 0.2f * MathF.Sin(time * 2.4f + Seed * 4f);
                sb.Draw(glow, basePos, null, core * (0.16f * env * pulse), 0f,
                    glow.Size() * 0.5f, 0.55f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
