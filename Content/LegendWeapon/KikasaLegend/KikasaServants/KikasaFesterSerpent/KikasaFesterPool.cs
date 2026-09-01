using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.NPCs.FestersandSerpents;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaFesterSerpent
{
    /// <summary>
    /// 鬼奴脓蕾沙蟒的小脓池：灵液痰落点的滞留灼金洼，胀开→滞留啃咬→收干。
    /// ai[0]=1 表示贴湖面模式（池坐在水线上滚、持续把水面煮沸）；
    /// 命中走横扁椭圆范围 + 低频跳伤；场上封顶由痰的生成口自查。
    /// 粒子帧内限量，音效稀疏门控
    /// </summary>
    internal class KikasaFesterPool : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private const int GrowFrames = 14;
        private const int TotalLife = 150;
        private const int FadeFrames = 22;
        /// <summary>池面半宽（横向判定半径）</summary>
        private const float MaxHalfWidth = 74f;
        /// <summary>池深（纵向判定半径，横扁椭圆）</summary>
        private const float MaxHalfHeight = 22f;

        private ref float SurfaceMode => ref Projectile.ai[0];
        private ref float Life => ref Projectile.localAI[0];

        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color MistBlood => KikasaDomain.CoolTint(new(58, 18, 20), new(52, 62, 66));

        private float Seed => Projectile.identity * 0.7391f % 5.13f;

        /// <summary>当前池面横半宽：胀开→滞留→收干</summary>
        private float HalfWidth {
            get {
                float grow = MathHelper.Clamp(Life / GrowFrames, 0f, 1f);
                grow = 1f - (1f - grow) * (1f - grow);
                float fade = MathHelper.Clamp((TotalLife - Life) / (float)FadeFrames, 0f, 1f);
                return MaxHalfWidth * grow * (0.35f + 0.65f * fade);
            }
        }

        private float Opacity => MathHelper.Clamp(Life / GrowFrames, 0f, 1f)
            * MathHelper.Clamp((TotalLife - Life) / (float)FadeFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
            Projectile.timeLeft = TotalLife + 6;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;

            Player owner = Main.player[Projectile.owner];
            bool onSurface = SurfaceMode == 1f && owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f;
            KikasaDomainPlayer kdp = onSurface ? owner.GetModPlayer<KikasaDomainPlayer>() : null;
            if (onSurface) {
                //贴水滚：池底压着水线走
                Projectile.Center = new Vector2(Projectile.Center.X, kdp.LakeWorldY - 8f);
            }

            if (Life >= TotalLife) {
                Projectile.Kill();
                return;
            }

            Lighting.AddLight(Projectile.Center, 0.30f * Opacity, 0.22f * Opacity, 0.06f * Opacity);

            if (Main.dedServ) {
                return;
            }

            //池面沸泡：金珠上浮炸裂，胀开期更密
            bool growing = Life < GrowFrames * 2;
            if ((int)Life % (growing ? 3 : 6) == 1) {
                Vector2 pos = Projectile.Center
                    + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth) * 0.8f, -Main.rand.NextFloat(0f, 6f));
                Dust gold = Dust.NewDustPerfect(pos, DustID.Ichor,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.6f, 1.6f)),
                    40, default, Main.rand.NextFloat(0.7f, 1.1f));
                gold.noGravity = false;
            }
            if ((int)Life % 7 == 3) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-HalfWidth, HalfWidth) * 0.6f, -8f),
                    new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), -Main.rand.NextFloat(0.15f, 0.4f)),
                    Color.Lerp(MistBlood, KikasaFesterSerpentServant.IchorDeepColor, 0.4f) * (0.6f * Opacity),
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(36, 60));
            }
            //贴水模式持续把水面煮沸
            if (onSurface && KikasaDomain.Viewed == kdp && (int)Life % 10 == 5) {
                KikasaDomainDeco.RippleAt(
                    new Vector2(Projectile.Center.X + Main.rand.NextFloat(-HalfWidth, HalfWidth) * 0.7f, kdp.LakeWorldY),
                    Main.rand.NextFloat(0.2f, 0.45f));
            }
            //稀疏的灼咬气泡声
            if ((int)Life % 34 == 9 && Main.rand.NextBool(2)) {
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.3f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
            }
        }

        /// <summary>池成形后才咬人，收干前松口</summary>
        public override bool? CanDamage() => HalfWidth > MaxHalfWidth * 0.4f ? null : false;

        /// <summary>横扁椭圆命中：目标矩形最近点做轴缩放距离判定</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 center = Projectile.Center;
            float hw = HalfWidth;
            float hh = MaxHalfHeight * (hw / MaxHalfWidth);
            if (hw < 1f || hh < 1f) {
                return false;
            }
            float nearX = MathHelper.Clamp(center.X, targetHitbox.Left, targetHitbox.Right);
            float nearY = MathHelper.Clamp(center.Y, targetHitbox.Top, targetHitbox.Bottom);
            float dx = (nearX - center.X) / hw;
            float dy = (nearY - center.Y) / hh;
            return dx * dx + dy * dy <= 1f;
        }

        public override bool PreDraw(ref Color lightColor) {
            //池体：三层错相滚动的横扁灼金洼 + 暗血压底
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float w = HalfWidth / 96f;
            float t = Main.GlobalTimeWrappedHourly;

            sb.Draw(tex, pos + new Vector2(0f, 2f), null, MistBlood * (Opacity * 0.55f), 0f, origin,
                new Vector2(w * 2.5f, w * 0.62f), SpriteEffects.None, 0f);
            for (int i = 0; i < 2; i++) {
                float ang = t * (0.3f + i * 0.14f) * (i % 2 == 0 ? 1f : -1f) + Seed + i * 2.1f;
                Vector2 off = new(MathF.Sin(ang) * 6f, 0f);
                Color c = Color.Lerp(KikasaFesterSerpentServant.IchorDeepColor, BloodDeep, i * 0.4f)
                    * (Opacity * (0.55f - i * 0.14f));
                sb.Draw(tex, pos + off, null, c, 0f, origin,
                    new Vector2(w * (2.1f + i * 0.35f), w * (0.5f + i * 0.1f)), SpriteEffects.None, 0f);
            }
            //灼金面芯：加色亮层，读作发光液体
            Color goldCore = (KikasaFesterSerpentServant.GhostIchor with { A = 0 }) * (0.4f * Opacity);
            sb.Draw(tex, pos + new Vector2(MathF.Sin(t * 0.8f + Seed) * 4f, -1f), null, goldCore, 0f, origin,
                new Vector2(w * 1.5f, w * 0.34f), SpriteEffects.None, 0f);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            //灼咬：目标脚下溅金
            FssVfx.IchorBurst(new Vector2(target.Center.X, target.position.Y + target.height), 0.5f);
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.3f, Pitch = -0.5f, MaxInstances = 2 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //收干余韵：最后一口金雾
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.2f), MistBlood * 0.5f, Main.rand.NextFloat(0.5f, 0.8f))
                ?.Configure(Main.rand.Next(36, 56));
            for (int i = 0; i < 3; i++) {
                Dust gold = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), 0f),
                    DustID.Ichor, new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f)),
                    60, default, Main.rand.NextFloat(0.6f, 0.9f));
                gold.noGravity = true;
            }
        }
    }
}
