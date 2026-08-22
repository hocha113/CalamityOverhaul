using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaKingSlime
{
    /// <summary>
    /// 重踏激起的宽矮血浪：贴着湖面横推的一段隆起水体，不是飞行物。
    /// 出生鼓浪→全速横扫→末段泄劲摊平，全程犁着水面走（连环涟漪 + 浪冠甩珠），
    /// 与石巨人的高窄双水柱在量感上反着来，宽、矮、横。
    /// ai0=横推方向(±1)，ai1=湖面 Y，spawn 一次带齐，轨迹各端确定性
    /// </summary>
    internal class KikasaBloodSurgeWave : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int WaveLife = 88;
        private const int SwellFrames = 12;
        private const int SpillStart = 66;

        private ref float Dir => ref Projectile.ai[0];
        private ref float FloorY => ref Projectile.ai[1];

        private int Timer => WaveLife - Projectile.timeLeft;

        private static Color GelMain => KikasaDomain.CoolTint(new(224, 66, 62), new(122, 154, 160));
        private static Color GelDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color GelDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color GelBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>浪高包络：鼓起→满冠→摊平</summary>
        private float HeightK {
            get {
                int t = Timer;
                if (t < SwellFrames) {
                    float k = t / (float)SwellFrames;
                    return k * k * (3f - 2f * k);
                }
                if (t > SpillStart) {
                    return MathHelper.Lerp(1f, 0.18f, (t - SpillStart) / (float)(WaveLife - SpillStart));
                }
                return 1f;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 150;
            Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.timeLeft = WaveLife;
        }

        /// <summary>浪冠塌了就不再伤人，接触窗与可见的隆起严格对齐</summary>
        public override bool? CanDamage() => HeightK > 0.35f ? null : false;

        public override bool? CanCutTiles() => false;

        public override void AI() {
            int t = Timer;
            float dir = MathF.Sign(Dir) == 0f ? 1f : MathF.Sign(Dir);

            //速度剖面：出生鼓浪提速，中段微泄，末段摊平骤减，没有一帧是匀速
            float speed;
            if (t < SwellFrames) {
                speed = MathHelper.Lerp(3.5f, 13.5f, t / (float)SwellFrames);
            }
            else if (t > SpillStart) {
                speed = MathF.Max(MathF.Abs(Projectile.velocity.X) * 0.94f, 2f);
            }
            else {
                speed = MathF.Abs(Projectile.velocity.X) * 0.997f;
            }
            Projectile.velocity = new Vector2(dir * speed, 0f);
            //浪体钉在水面上
            Projectile.Bottom = new Vector2(Projectile.Bottom.X, FloorY + 10f);

            float hk = HeightK;
            bool viewed = KikasaDomain.Viewed != null
                && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

            //犁开水面：浪脚连环涟漪
            if (viewed && t % 3 == 1) {
                KikasaDomainDeco.RippleAt(
                    new Vector2(Projectile.Center.X + dir * 26f, FloorY), 0.35f + 0.35f * hk);
            }
            //浪冠向前甩珠：速度拉伸的碎血
            if (!Main.dedServ && hk > 0.3f && t % 2 == 0) {
                Vector2 crest = new(Projectile.Center.X + dir * Main.rand.NextFloat(-8f, 30f),
                    FloorY - 30f * hk - Main.rand.NextFloat(0f, 10f));
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(crest,
                    new Vector2(dir * Main.rand.NextFloat(2f, 4.5f), -Main.rand.NextFloat(0.5f, 2f)),
                    Main.rand.NextBool(3) ? GelDeep : GelMain,
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(12, 22));
            }
            //浪后翻涌的潮气
            if (!Main.dedServ && t % 11 == 5 && hk > 0.4f) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    new Vector2(Projectile.Center.X - dir * 40f, FloorY - 12f),
                    new Vector2(dir * 0.2f, -0.3f),
                    KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66)) * 0.6f,
                    Main.rand.NextFloat(0.5f, 0.8f))?.Configure(Main.rand.Next(40, 70));
            }
            //行进的低哗声
            if (t % 18 == 6 && hk > 0.4f) {
                SoundEngine.PlaySound(SoundID.SplashWeak with {
                    Volume = 0.22f,
                    Pitch = -0.55f,
                    MaxInstances = 2
                }, Projectile.Center);
            }

            Lighting.AddLight(Projectile.Center, 0.24f * hk, 0.06f * hk, 0.05f * hk);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //浪头穿体：溅血顺着推进方向泼
            if (Main.dedServ) {
                return;
            }
            float dir = MathF.Sign(Dir);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    target.Center + Main.rand.NextVector2Circular(16f, 16f),
                    new Vector2(dir * Main.rand.NextFloat(2f, 5f), -Main.rand.NextFloat(0.5f, 2.5f)),
                    GelMain * 0.6f, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(14, 24));
            }
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            //谢幕：摊平成一片薄水，最后一圈宽涟漪
            if (Main.dedServ) {
                return;
            }
            if (KikasaDomain.Viewed != null
                && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner) {
                KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, FloorY), 1.1f);
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    new Vector2(Projectile.Center.X + Main.rand.NextFloat(-40f, 40f), FloorY - 6f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.5f, 1.5f)),
                    GelMain * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
            }
        }

        //==================== 绘制 ====================

        /// <summary>宽矮浪体分层：水线暗底带 + 三瓣错相浪峰 + 迎面亮唇 + 浪头碎沫</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D blob = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blob == null || glow == null) {
                return false;
            }
            float hk = HeightK;
            if (hk < 0.02f) {
                return false;
            }
            float dir = MathF.Sign(Dir) == 0f ? 1f : MathF.Sign(Dir);
            SpriteBatch sb = Main.spriteBatch;
            Vector2 blobOrigin = blob.Size() * 0.5f;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            Vector2 baseLine = new(Projectile.Center.X, FloorY);

            //水线暗底带：被犁开的浑浊水体，比浪冠宽一截
            //（暗色层必须用真 alpha 的 Extra_98：黑底 SoftGlow 在 AlphaBlend 里会糊出黑块；
            //×2 补偿其更紧的径向衰减，视觉尺寸对齐原稿）
            sb.Draw(blob, baseLine - Main.screenPosition + new Vector2(0f, -4f), null,
                GelDark * (0.55f * hk), 0f, blobOrigin,
                new Vector2(210f * 2f / blob.Width, 16f * 2f / blob.Height) * 2f, SpriteEffects.None, 0f);

            //三瓣浪峰：中瓣最高、前后错相呼吸，破掉单贴纸的读法
            float wob = Main.GlobalTimeWrappedHourly * 7f + Seed * 3f;
            Span<float> lobeOff = [dir * 34f, 0f, -dir * 30f];
            Span<float> lobeH = [0.78f, 1f, 0.62f];
            for (int i = 0; i < 3; i++) {
                float jig = 1f + MathF.Sin(wob + i * 1.9f) * 0.08f;
                float h = 46f * hk * lobeH[i] * jig;
                float w = 58f * (1.05f - 0.12f * i);
                Vector2 pos = baseLine + new Vector2(lobeOff[i], -h * 0.42f);
                //暗缘
                sb.Draw(blob, pos - Main.screenPosition, null, GelDark * (0.8f * hk), dir * 0.1f,
                    blobOrigin, new Vector2(w * 1.12f / blob.Width * 2f, h * 1.1f / blob.Height * 2f), SpriteEffects.None, 0f);
                //主体
                sb.Draw(blob, pos - Main.screenPosition, null, GelMain * (0.85f * hk), dir * 0.1f,
                    blobOrigin, new Vector2(w / blob.Width * 2f, h / blob.Height * 2f), SpriteEffects.None, 0f);
            }

            //迎面亮唇：浪要卷的那一侧，湿反光（A=0 预乘加色）
            Vector2 lipPos = baseLine + new Vector2(dir * 40f, -40f * hk);
            sb.Draw(glow, lipPos - Main.screenPosition, null,
                (GelBright with { A = 0 }) * (0.5f * hk), dir * 0.35f, glowOrigin,
                new Vector2(34f * 2f / glow.Width, 12f * 2f / glow.Height), SpriteEffects.None, 0f);
            //浪头碎沫点
            for (int i = 0; i < 3; i++) {
                float foamPhase = (wob * 0.6f + i * 2.1f) % MathHelper.TwoPi;
                Vector2 foam = baseLine + new Vector2(
                    dir * (46f + MathF.Sin(foamPhase) * 14f),
                    -hk * (34f + MathF.Cos(foamPhase * 1.3f) * 12f));
                sb.Draw(glow, foam - Main.screenPosition, null,
                    (GelBright with { A = 0 }) * (0.3f * hk), 0f, glowOrigin,
                    new Vector2(7f * 2f / glow.Width, 5f * 2f / glow.Height), SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}
