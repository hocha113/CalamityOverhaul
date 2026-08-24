using CalamityOverhaul.Common;
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
    /// <summary>霁的演出集中处：收伞雨幕逆卷与霁光落点解算，纯表现各端本地</summary>
    internal static class FuJiFX
    {
        /// <summary>
        /// 收伞雨幕逆卷：伞位周围的雨向上倒流——上掷速度线+逆浮金珠+一圈缓环。
        /// strength 由蓄量折算（旁观端按固定档近似）
        /// </summary>
        internal static void RecallCurl(Projectile umbrella, Color accent, float strength) {
            if (Main.dedServ) {
                return;
            }
            strength = MathHelper.Clamp(strength, 0.25f, 1f);
            Vector2 center = umbrella.Center;

            //逆卷雨线：自伞下向伞位上抽，读作雨幕被收回云里
            int lineCount = 7 + (int)(8 * strength);
            for (int i = 0; i < lineCount; i++) {
                Vector2 pos = center + new Vector2(
                    Main.rand.NextFloat(-70f, 70f), Main.rand.NextFloat(14f, 90f));
                PRTLoader.NewParticle<PRT_Line>(pos,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(4.5f, 8.5f)),
                    Color.Lerp(accent, Color.White, Main.rand.NextFloat(0.3f, 0.6f)) * (0.7f * strength + 0.2f),
                    Main.rand.NextFloat(0.4f, 0.66f))?.Configure(false, Main.rand.Next(12, 19));
            }
            //逆浮金珠：负重力缓升，雨珠也跟着往回走
            int beadCount = 4 + (int)(5 * strength);
            for (int i = 0; i < beadCount; i++) {
                PRTLoader.NewParticle<PRT_KikasaInkBead>(
                    center + Main.rand.NextVector2Circular(46f, 30f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1.2f, 2.6f)),
                    Color.Lerp(accent, Color.White, 0.25f),
                    Main.rand.NextFloat(0.18f, 0.3f))?.Configure(Main.rand.Next(18, 28), -0.04f, 0.98f);
            }
            //收拢缓环+一声轻弦
            PRTLoader.NewParticle<PRT_HeartcarverPulseRing>(center, Vector2.Zero,
                accent * (0.45f * strength + 0.15f), 0.12f)?.Configure(0.12f, 0.7f, 14);
            KikasaInk.Play(SoundID.Item26, center, 0.5f, 0.35f, 2);
        }

        /// <summary>霁光落点：自光标向下探地，束落在地表；无地则悬空绽在光标处</summary>
        internal static Vector2 SolveLanding(Vector2 cursor) {
            int x = (int)(cursor.X / 16f);
            int startY = (int)(cursor.Y / 16f);
            int endY = startY + 25;
            for (int y = startY; y <= endY; y++) {
                Tile t = Framing.GetTileSafely(x, y);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    return new Vector2(cursor.X, y * 16f - 4f);
                }
            }
            return cursor;
        }
    }

    /// <summary>
    /// 霁·霁光束：云隙轰落的金光柱，按蓄量折算的大额单发（ai[0]=蓄量档 0~1）。
    /// 柱/束收口纪律：源头=云隙辉团+雾翼帽住顶端，落点=光丘+虹晕缓散，
    /// 打空（无地面）末端收在光标绽点；宽度走展开→维持→收束的生命周期，判定同源且窄于可见体。
    /// 落点由所有者端解定后随生成包同步；前 4 帧光锋自云隙坠到地，读作"轰落"
    /// </summary>
    internal class KikasaFuJiLightBeam : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int ExpandFrames = 7;
        private const int SustainFrames = 16;
        private const int CollapseFrames = 12;
        /// <summary>柱体可见期（帧），此后只剩虹晕缓散</summary>
        private const int VisualFrames = ExpandFrames + SustainFrames + CollapseFrames;
        /// <summary>光锋自云隙坠地的帧数</summary>
        private const int CrashFrames = 4;
        /// <summary>柱体最大长度（px）；有天花板时钳到云隙贴顶</summary>
        private const float MaxLenPx = 760f;

        //霁金三色：辉缘沉金、柱体霁金、芯线暖白
        private static readonly Color GoldDeep = new(168, 122, 52);
        private static readonly Color Gold = new(240, 206, 118);
        private static readonly Color CoreWhite = new(255, 246, 218);

        /// <summary>蓄量档 0~1（生成包 ai[0]），吃宽度与演出幅度</summary>
        private float MeterT => MathHelper.Clamp(Projectile.ai[0], 0f, 1f);

        private float life;
        private float topLen;
        private bool struck;

        /// <summary>柱宽（px）：蓄得越满越阔</summary>
        private float WidthPx => 64f + 36f * MeterT;

        /// <summary>宽度生命周期：展开铺满→微息维持→收束断流（判定同源）</summary>
        private float WidthT {
            get {
                float t = MathHelper.Clamp(life / ExpandFrames, 0f, 1f);
                float expand = MathHelper.Lerp(0.3f, 1f, 1f - (1f - t) * (1f - t));
                float col = MathHelper.Clamp((life - ExpandFrames - SustainFrames) / (float)CollapseFrames, 0f, 1f);
                float wobble = life > ExpandFrames
                    ? 1f + MathF.Sin((life - ExpandFrames) * 0.6f + Projectile.identity) * 0.025f : 1f;
                return expand * (1f - col * col) * wobble;
            }
        }

        /// <summary>光锋下坠进度 0~1：前几帧从云隙砸到地</summary>
        private float CrashT => MathHelper.Clamp(life / CrashFrames, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 84;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            //大额单发：一束一敌只结算一次
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            life++;
            if (!struck) {
                struck = true;
                //柱长首帧一次性落定：向上探天花板，各端同地形同解
                topLen = SolveTopLength();
                StrikeFX();
            }

            //柱体存续期的光照与柱内金屑
            if (life <= VisualFrames && !Main.dedServ) {
                float glow = WidthT;
                for (int i = 0; i < 3; i++) {
                    Vector2 p = Projectile.Center - new Vector2(0f, topLen * (0.2f + 0.3f * i));
                    Lighting.AddLight(p, 0.7f * glow, 0.58f * glow, 0.3f * glow);
                }
                //柱内缓降金屑：光柱不是空心贴条，里面有东西在落
                if (Main.rand.NextBool(3) && CrashT >= 1f) {
                    PRTLoader.NewParticle<PRT_Sparkle>(
                        Projectile.Center - new Vector2(
                            Main.rand.NextFloat(-0.32f, 0.32f) * WidthPx * WidthT,
                            Main.rand.NextFloat(0.1f, 0.9f) * topLen),
                        new Vector2(0f, Main.rand.NextFloat(2f, 4.5f)),
                        Color.Lerp(Gold, Color.White, 0.4f), Main.rand.NextFloat(0.22f, 0.36f))
                        ?.Configure(Gold * 0.6f, Main.rand.Next(14, 22), 0.05f, 0.7f);
                }
                //落点金尘缓升：光落在地上，尘往上浮
                if (Main.rand.NextBool(4) && CrashT >= 1f) {
                    PRTLoader.NewParticle<PRT_KikasaInkBead>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-0.6f, 0.6f) * WidthPx, -4f),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.8f, 1.8f)),
                        Color.Lerp(Gold, CoreWhite, Main.rand.NextFloat(0.5f)),
                        Main.rand.NextFloat(0.16f, 0.28f))?.Configure(Main.rand.Next(16, 26), -0.02f, 0.98f);
                }
            }
        }

        /// <summary>自落点向上逐格探实心：云隙贴在天花板下，旷野直上屏顶</summary>
        private float SolveTopLength() {
            int x = (int)(Projectile.Center.X / 16f);
            int startY = (int)(Projectile.Center.Y / 16f) - 2;
            int endY = Math.Max(startY - (int)(MaxLenPx / 16f), 1);
            for (int y = startY; y >= endY; y--) {
                Tile t = Framing.GetTileSafely(x, y);
                if (t.HasTile && Main.tileSolid[t.TileType] && !Main.tileSolidTop[t.TileType]) {
                    return MathF.Max(Projectile.Center.Y - (y * 16f + 24f), 200f);
                }
            }
            return MaxLenPx;
        }

        /// <summary>轰落拍：钟声+光爆+云隙雾翼向两侧让开，各端本地</summary>
        private void StrikeFX() {
            if (Main.dedServ) {
                return;
            }
            Vector2 top = Projectile.Center - new Vector2(0f, topLen);
            //云隙让开：顶端两侧各一翼淡金雾推开，云被光顶破
            for (int side = -1; side <= 1; side += 2) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_KikasaInkMist>(
                        top + new Vector2(side * (14f + i * 12f), Main.rand.NextFloat(-8f, 8f)),
                        new Vector2(side * Main.rand.NextFloat(1.2f, 2.4f), Main.rand.NextFloat(-0.3f, 0.3f)),
                        Color.Lerp(GoldDeep, Gold, Main.rand.NextFloat(0.6f)),
                        Main.rand.NextFloat(0.8f, 1.2f))?.Configure(Main.rand.Next(26, 40));
                }
            }
            //霁的钟声起手，光砸在地上的重音随后
            KikasaInk.Play(SoundID.Item35, Projectile.Center, 0.85f, 0.15f, 2);
            KikasaInk.Play(SoundID.Item122, Projectile.Center, 0.5f, 0.3f, 2);
            KikasaInk.Play(KikasaInk.InkSplash, Projectile.Center, 0.8f, -0.3f, 3);
        }

        /// <summary>
        /// 柱体竖直线判定：判定段与点亮段同源——坠落期只判云隙到光锋的已亮段，
        /// 触地后判整柱；判定宽 0.62 倍可见体藏在光里，收束即失能
        /// </summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (life > ExpandFrames + SustainFrames) {
                return false;
            }
            float _ = 0f;
            Vector2 cloudTop = Projectile.Center - new Vector2(0f, topLen);
            Vector2 front = Projectile.Center - new Vector2(0f, topLen * (1f - CrashT));
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                front, cloudTop, WidthPx * 0.62f * WidthT, ref _);
        }

        /// <summary>
        /// 分层柱体+两端收口+虹晕缓散。柱体自云隙向下生长（轰落方向），
        /// 顶端沉进云隙辉团、落点坐进光丘；柱亡后虹晕接管余韵
        /// </summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (tex == null || glow == null) {
                return false;
            }
            Vector2 landing = Projectile.Center - Main.screenPosition;
            Vector2 top = landing - new Vector2(0f, topLen);
            float widthT = WidthT;
            float crash = CrashT;

            if (life <= VisualFrames && widthT > 0.02f) {
                //柱身：自云隙向下生长到光锋，origin 取贴图上沿中点
                float drawnLen = topLen * crash;
                float w = WidthPx * widthT;
                Vector2 columnOrigin = new(tex.Width * 0.5f, 0f);
                //外辉→柱体→芯线，全 A=0 加色：光只加不压
                Main.EntitySpriteDraw(tex, top, null, (GoldDeep with { A = 0 }) * 0.5f, 0f,
                    columnOrigin, new Vector2(w * 1.7f / tex.Width, drawnLen * 1.02f / tex.Height),
                    SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, top, null, (Gold with { A = 0 }) * 0.85f, 0f,
                    columnOrigin, new Vector2(w / tex.Width, drawnLen / tex.Height),
                    SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, top, null, (CoreWhite with { A = 0 }) * 0.8f, 0f,
                    columnOrigin, new Vector2(w * 0.34f / tex.Width, drawnLen * 0.98f / tex.Height),
                    SpriteEffects.None, 0);

                //源头收口：云隙辉团横卧顶端，柱顶沉进辉里不见断面
                Main.EntitySpriteDraw(glow, top, null, (Gold with { A = 0 }) * (0.9f * widthT), 0f,
                    glow.Size() * 0.5f, new Vector2(w * 3.4f / glow.Width, w * 1.2f / glow.Height),
                    SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, top, null, (CoreWhite with { A = 0 }) * (0.5f * widthT), 0f,
                    glow.Size() * 0.5f, new Vector2(w * 1.8f / glow.Width, w * 0.6f / glow.Height),
                    SpriteEffects.None, 0);

                if (crash < 1f) {
                    //坠落光锋：一颗亮头带着柱身砸下来
                    Vector2 front = top + new Vector2(0f, drawnLen);
                    Main.EntitySpriteDraw(glow, front, null, (CoreWhite with { A = 0 }) * 0.95f, 0f,
                        glow.Size() * 0.5f, w * 1.6f / glow.Width, SpriteEffects.None, 0);
                }
                else {
                    //落点收口：光丘坐进地里，比柱宽读作"光砸开了"
                    Main.EntitySpriteDraw(glow, landing + new Vector2(0f, 4f), null,
                        (Gold with { A = 0 }) * (0.85f * widthT), 0f, glow.Size() * 0.5f,
                        new Vector2(w * 2.8f / glow.Width, w * 0.9f / glow.Height), SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(glow, landing, null, (CoreWhite with { A = 0 }) * (0.55f * widthT),
                        0f, glow.Size() * 0.5f, new Vector2(w * 1.3f / glow.Width, w * 0.5f / glow.Height),
                        SpriteEffects.None, 0);
                }
            }

            //虹晕缓散：三圈错拍的贴地椭圆环金→霞粉→青白，活得比柱久（余韵纪律）
            if (crash >= 1f) {
                DrawHaloRing(0, Gold, CoreWhite);
                DrawHaloRing(1, new Color(232, 150, 140), Gold);
                DrawHaloRing(2, new Color(172, 214, 224), CoreWhite);
            }
            return false;
        }

        /// <summary>一圈虹晕：错拍起步、缓涨缓淡，squish 贴地透视</summary>
        private void DrawHaloRing(int index, Color main, Color bright) {
            float start = CrashFrames + index * 9f;
            float dur = 46f;
            float t = (life - start) / dur;
            if (t <= 0f || t >= 1f) {
                return;
            }
            float radius = MathHelper.Lerp(24f, 120f + 30f * index + 40f * MeterT,
                1f - (1f - t) * (1f - t));
            float alpha = MathF.Pow(1f - t, 1.6f) * (0.5f + 0.2f * MeterT);
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, radius, 6f,
                bright, main, GoldDeep, alpha, squish: 0.38f, innerGlow: 0.12f,
                timeSeed: Projectile.identity * 0.53f + index * 1.7f);
        }
    }
}
