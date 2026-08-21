using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains
{
    /// <summary>
    /// 墨洼:湖倾档(S≥<see cref="KikasaOverride.TierLakeTilt"/>)的墨滴落地后积成的一汪滞墨,
    /// 踩进来的持续受召唤伤害。出生吸附地表并压一枚渍斑贴花(贴花寿命长于本体,余韵留在地上);
    /// 宽度包络成洼铺开→末段收干,判定与可见同源。同主近洼的合并在墨滴谢幕侧完成
    /// </summary>
    internal class KikasaInkPuddle : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public const int LifeFrames = 150;
        private const int SpreadFrames = 10;
        private const int DryFrames = 26;
        private const float WidthPx = 92f;
        private const float DepthPx = 16f;

        private bool anchored;
        private float life;

        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>铺开(EaseOut)→收干(EaseIn)的宽度包络</summary>
        private float WidthT {
            get {
                float grow = MathHelper.Clamp(life / SpreadFrames, 0f, 1f);
                grow = 1f - (1f - grow) * (1f - grow);
                float dry = 1f - MathHelper.Clamp(Projectile.timeLeft / (float)DryFrames, 0f, 1f);
                return grow * (1f - dry * dry);
            }
        }

        public override void SetDefaults() {
            Projectile.width = (int)WidthPx;
            Projectile.height = (int)DepthPx;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            life++;
            if (!anchored) {
                anchored = true;
                //吸附地表:自出生点向下找实心;找不到就原地作数(空中命中的浮墨)
                if (TryFindGroundBelow(Projectile.Center, 96f, out float surfaceY)) {
                    Projectile.Center = new Vector2(Projectile.Center.X, surfaceY - DepthPx * 0.5f + 3f);
                }
                if (!Main.dedServ) {
                    //贴花比本体长命:洼干了渍还在
                    KikasaInkFX.AddGroundSplat(Projectile.Center + Vector2.UnitY * 6f,
                        Vector2.UnitY * 10f, WidthPx * 0.6f);
                    KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.3f, -0.5f, 4);
                }
            }

            //洼面冒泡:偶发一粒墨珠鼓起又塌回
            if (!Main.dedServ && WidthT > 0.5f && Main.rand.NextBool(9)) {
                float xOff = Main.rand.NextFloat(-0.42f, 0.42f) * WidthPx * WidthT;
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    Projectile.Center + new Vector2(xOff, -2f),
                    new Vector2(xOff * 0.01f, -Main.rand.NextFloat(0.6f, 1.4f)),
                    Main.rand.NextBool(3) ? KikasaInk.BloodCore : KikasaInk.InkDeep,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(Main.rand.Next(12, 20));
            }
            Lighting.AddLight(Projectile.Center, 0.08f, 0.015f, 0.02f);
        }

        private static bool TryFindGroundBelow(Vector2 from, float maxDown, out float surfaceY) {
            int x = (int)(from.X / 16f);
            int startY = (int)(from.Y / 16f);
            int endY = (int)((from.Y + maxDown) / 16f);
            for (int y = startY; y <= endY; y++) {
                Tile t = Framing.GetTileSafely(x, y);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    surfaceY = y * 16f;
                    return true;
                }
            }
            surfaceY = 0f;
            return false;
        }

        /// <summary>判定随包络收窄,干透即失能</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float w = WidthPx * WidthT;
            if (w < 14f) {
                return false;
            }
            Rectangle box = new((int)(Projectile.Center.X - w * 0.5f),
                (int)(Projectile.Center.Y - DepthPx * 0.5f - 6f), (int)w, (int)DepthPx + 10);
            return box.Intersects(targetHitbox);
        }

        /// <summary>扁平三层:暗缘垫底→墨体→血芯细线,加一线 A=0 湿反光</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            float wT = WidthT;
            if (tex == null || wT <= 0.03f) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float w = WidthPx * wT;
            float wob = 1f + MathF.Sin(life * 0.11f + Seed * 4f) * 0.04f;

            Main.EntitySpriteDraw(tex, pos, null, KikasaInk.InkDeep * 0.7f, 0f, origin,
                new Vector2(w * 1.16f / tex.Width, DepthPx * 1.5f / tex.Height * wob), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, null, KikasaInk.InkBody * 0.95f, 0f, origin,
                new Vector2(w / tex.Width, DepthPx / tex.Height * wob), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos + new Vector2(0f, -1f), null, KikasaInk.BloodCore * 0.4f, 0f, origin,
                new Vector2(w * 0.5f / tex.Width, 4f / tex.Height), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos + new Vector2(w * 0.12f, -3f), null,
                (KikasaInk.WetSheen with { A = 0 }) * (0.26f * wob), 0f, origin,
                new Vector2(w * 0.22f / tex.Width, 2.6f / tex.Height), SpriteEffects.None, 0);
            return false;
        }
    }
}
