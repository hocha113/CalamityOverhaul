using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 墨泉:湖倾档(S≥<see cref="KikasaOverride.TierLakeTilt"/>)满蓄墨瀑的终幕,
    /// 沿落线自地表喷起的墨柱,重击退。两端收口:根部溅丘坐进地里,
    /// 头部圆冠+顶珠,排空自上而下缩回;宽度走展开→收窄断流的包络,判定同源。
    /// ai[0]=喷发前延迟帧(三泉错拍),延迟期洼面鼓包、墨珠向根部倒吸作预告
    /// </summary>
    internal class KikasaInkGeyser : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public const int RiseFrames = 7;
        public const int SustainFrames = 14;
        public const int CollapseFrames = 9;
        private const float HeightPx = 236f;
        private const float WidthPx = 46f;

        /// <summary>喷发前延迟帧,由墨瀑侧写入错拍</summary>
        private ref float DelayAi => ref Projectile.ai[0];

        /// <summary>自喷发帧起算的寿命</summary>
        private float life;
        private bool erupted;
        private bool telegraphed;

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        private float CollapseT
            => MathHelper.Clamp((life - RiseFrames - SustainFrames) / (float)CollapseFrames, 0f, 1f);

        /// <summary>柱高包络:轻过冲窜起→持续微晃→自上而下缩回</summary>
        private float HeightT {
            get {
                float t = MathHelper.Clamp(life / RiseFrames, 0f, 1f);
                const float c1 = 1.3f;
                const float c3 = c1 + 1f;
                float rise = 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * (t - 1f) * (t - 1f);
                float col = CollapseT;
                float wobble = life > RiseFrames
                    ? 1f + MathF.Sin((life - RiseFrames) * 0.5f + Seed) * 0.02f : 1f;
                return MathHelper.Clamp(rise, 0f, 1.08f) * (1f - col * col) * wobble;
            }
        }

        /// <summary>柱宽包络:展开铺满→排空收窄断流</summary>
        private float WidthT {
            get {
                float t = MathHelper.Clamp(life / RiseFrames, 0f, 1f);
                float grow = MathHelper.Lerp(0.45f, 1f, 1f - (1f - t) * (1f - t));
                return grow * (1f - CollapseT * CollapseT * 0.7f);
            }
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;

            //延迟期:预告即承诺，洼面先鼓一个包,墨珠向根部倒吸
            if (DelayAi > 0f) {
                DelayAi--;
                if (!Main.dedServ) {
                    if (!telegraphed) {
                        telegraphed = true;
                        KikasaInkFX.AddGroundSplat(Projectile.Center + Vector2.UnitY * 4f,
                            Vector2.UnitY * 8f, WidthPx * 1.1f);
                        KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.34f, -0.6f, 3);
                    }
                    if (Main.rand.NextBool(2)) {
                        float xOff = Main.rand.NextFloat(-1f, 1f) * WidthPx;
                        PRTLoader.NewParticle<PRT_KikasaInkBead>(
                            Projectile.Center + new Vector2(xOff, -2f),
                            new Vector2(-xOff * 0.06f, -Main.rand.NextFloat(0.4f, 1f)),
                            KikasaInk.InkDeep, Main.rand.NextFloat(0.24f, 0.4f))?.Configure(Main.rand.Next(10, 16));
                    }
                }
                return;
            }

            //喷发拍:一声重水花,根部炸出一圈上掷墨珠
            if (!erupted) {
                erupted = true;
                KikasaInk.Play(KikasaInk.InkSpray, Projectile.Center, 0.8f, -0.2f, 3);
                KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.85f, -0.5f, 3);
                if (!Main.dedServ) {
                    for (int i = 0; i < 10; i++) {
                        Vector2 vel = new(Main.rand.NextFloat(-2.6f, 2.6f), -Main.rand.NextFloat(3f, 8f));
                        PRTLoader.NewParticle<PRT_KikasaInkBead>(
                            Projectile.Center + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * WidthPx, -4f),
                            vel, Main.rand.NextBool(3) ? KikasaInk.BloodCore : KikasaInk.InkBody,
                            Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(16, 26));
                    }
                }
            }

            life++;
            if (life >= RiseFrames + SustainFrames + CollapseFrames) {
                Projectile.Kill();
                return;
            }

            //顶冠滴落:柱头一直在甩珠,头部不平切
            if (!Main.dedServ && CollapseT < 0.5f && Main.rand.NextBool(2)) {
                Vector2 head = Projectile.Center - new Vector2(0f, HeightPx * HeightT);
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    head + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * WidthPx * WidthT, 0f),
                    new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(0.5f, 2.4f)),
                    Main.rand.NextBool(4) ? KikasaInk.BloodCore : KikasaInk.InkDeep,
                    Main.rand.NextFloat(0.22f, 0.38f))?.Configure(Main.rand.Next(14, 22));
            }
            Lighting.AddLight(Projectile.Center - new Vector2(0f, HeightPx * HeightT * 0.5f),
                0.12f, 0.025f, 0.035f);
        }

        /// <summary>竖直线判定:根到柱头,宽随包络;延迟与收干后段失能</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!erupted || CollapseT > 0.6f) {
                return false;
            }
            float _ = 0f;
            Vector2 top = Projectile.Center - new Vector2(0f, HeightPx * HeightT);
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, top, WidthPx * 0.75f * WidthT, ref _);
        }

        /// <summary>
        /// 精灵分层:根部溅丘(坐进地里)→柱身(暗缘+墨体+血芯)→头部圆冠+湿反光。
        /// 全部锚定根部向上生长,收干时整柱缩回地面
        /// </summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null || !erupted) {
                return false;
            }
            float hT = HeightT;
            float wT = WidthT;
            if (hT <= 0.02f || wT <= 0.03f) {
                return false;
            }
            Vector2 basePos = Projectile.Center - Main.screenPosition + new Vector2(0f, 8f);
            Vector2 origin = tex.Size() * 0.5f;
            //柱身锚底:origin 取贴图下沿中点
            Vector2 columnOrigin = new(tex.Width * 0.5f, tex.Height);
            float h = HeightPx * hT;
            float w = WidthPx * wT;
            float alpha = 1f - CollapseT * 0.45f;
            float sway = MathF.Sin(life * 0.23f + Seed * 3f) * 0.02f;

            //根部溅丘:比柱宽,坐进地里,收口第一答案
            Main.EntitySpriteDraw(tex, basePos + new Vector2(0f, 2f), null,
                KikasaInk.InkDeep * (0.75f * alpha), 0f, origin,
                new Vector2(w * 1.9f / tex.Width, 20f / tex.Height), SpriteEffects.None, 0);
            //柱身:暗缘垫底→墨体→血芯细线
            Main.EntitySpriteDraw(tex, basePos, null, KikasaInk.InkDeep * (0.8f * alpha), sway,
                columnOrigin, new Vector2(w * 1.18f / tex.Width, h * 1.02f / tex.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, basePos, null, KikasaInk.InkBody * (0.95f * alpha), sway,
                columnOrigin, new Vector2(w / tex.Width, h / tex.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, basePos, null, KikasaInk.BloodCore * (0.5f * alpha), sway,
                columnOrigin, new Vector2(w * 0.3f / tex.Width, h * 0.9f / tex.Height), SpriteEffects.None, 0);
            //头部圆冠:柱顶一团圆墨帽住断面,加一点 A=0 湿反光
            Vector2 head = basePos - new Vector2(0f, h);
            Main.EntitySpriteDraw(tex, head, null, KikasaInk.InkBody * (0.9f * alpha), 0f, origin,
                new Vector2(w * 0.85f / tex.Width, w * 0.6f / tex.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, head + new Vector2(w * 0.1f, -2f), null,
                (KikasaInk.WetSheen with { A = 0 }) * (0.3f * alpha), 0f, origin,
                new Vector2(w * 0.3f / tex.Width, 3f / tex.Height), SpriteEffects.None, 0);
            return false;
        }
    }
}
