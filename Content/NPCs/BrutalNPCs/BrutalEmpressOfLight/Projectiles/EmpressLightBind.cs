using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles
{
    /// <summary>
    /// 光绫束缚：三条光绫绕缚受缚玩家的纯视觉载体，零伤害，位置逐帧贴附受缚者；
    /// ai[0]=受缚玩家索引 ai[1]=寿命帧 ai[2]=色相种子；走弹幕同步旁观者可见
    /// </summary>
    internal class EmpressLightBind : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int TightenWindow = 30;

        private ref float Timer => ref Projectile.localAI[0];
        private int VictimIndex => (int)Projectile.ai[0];
        private int Life => Math.Max((int)Projectile.ai[1], 30);
        private float Hue => Projectile.ai[2];

        private Player Victim {
            get {
                if (VictimIndex >= 0 && VictimIndex < Main.maxPlayers) {
                    Player p = Main.player[VictimIndex];
                    if (p.active && !p.dead) {
                        return p;
                    }
                }
                return null;
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.damage = 0;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        public override void AI() {
            if (Timer == 0f) {
                Projectile.timeLeft = Life;
            }
            Timer++;
            Projectile.velocity = Vector2.Zero;

            Player victim = Victim;
            if (victim == null) {
                //受缚者失效：光绫加速散尽（各端从同步的玩家状态得到一致判断）
                if (Projectile.timeLeft > 8) {
                    Projectile.timeLeft = 8;
                }
            }
            else {
                Projectile.Center = victim.Center;
            }

            float fadeIn = MathHelper.Clamp(Timer / 10f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 8f, 0f, 1f);
            Projectile.Opacity = fadeIn * fadeOut;

            //缠绫间逸散的光瓣（客户端低频）
            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_EmpressPetalDust>(
                    Projectile.Center + Main.rand.NextVector2Circular(30f, 40f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.8f, -0.3f)),
                    Main.hslToRgb((Hue + Main.rand.NextFloat(0.1f)) % 1f, 0.8f, 0.7f),
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(30, Hue);
            }

            Lighting.AddLight(Projectile.Center, Main.hslToRgb(Hue, 0.8f, 0.68f).ToVector3() * 0.5f * Projectile.Opacity);
        }

        //绫断瞬间：光瓣迸散+白涟漪（爆绽与提前断投共用的脱缚余韵）
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero, Color.White, 0.7f)?.Configure(16, Hue);
            for (int i = 0; i < 10; i++) {
                float hue = (Hue + i / 10f * 0.3f) % 1f;
                PRTLoader.NewParticle<PRT_EmpressPetalDust>(Projectile.Center + Main.rand.NextVector2Circular(24f, 30f),
                    VaultUtils.RandVr(1.5f, 4f), Main.hslToRgb(hue, 0.85f, 0.7f),
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(26, 40), hue);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture_White.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float time = Main.GlobalTimeWrappedHourly;
            Vector2 origin = star.Size() / 2f;

            //临爆收紧：末段绫圈收窄提亮
            float tighten = Projectile.timeLeft < TightenWindow
                ? 1f - (TightenWindow - Projectile.timeLeft) / (float)TightenWindow * 0.32f : 1f;
            float bright = Projectile.timeLeft < TightenWindow ? 1.3f : 1f;

            const int Segs = 14;
            for (int s = 0; s < 3; s++) {
                float phase = time * 5.6f + s * MathHelper.TwoPi / 3f;
                Vector2 prev = default;
                bool hasPrev = false;
                for (int i = 0; i <= Segs; i++) {
                    float t = i / (float)Segs;
                    //两端收拢的螺旋缠绕，深度分量给前后亮度差
                    float envelope = (float)Math.Sin(t * MathHelper.Pi);
                    float wave = phase + t * 9.4f;
                    float x = (float)Math.Cos(wave) * 30f * envelope * tighten;
                    float depth = (float)Math.Sin(wave);
                    Vector2 pt = Projectile.Center + new Vector2(x, MathHelper.Lerp(30f, -34f, t)) - Main.screenPosition;
                    if (hasPrev) {
                        Vector2 seg = pt - prev;
                        float len = seg.Length();
                        if (len > 0.4f) {
                            float hue = (Hue + t * 0.22f + s * 0.06f) % 1f;
                            float alpha = (0.42f + 0.3f * depth) * Projectile.Opacity * bright;
                            Color c = Main.hslToRgb(hue, 0.75f, 0.72f) with { A = 0 };
                            Main.EntitySpriteDraw(star, (pt + prev) * 0.5f, null, c * alpha, seg.ToRotation(),
                                origin, new Vector2((len + 6f) / star.Width * 1.35f, 0.032f + 0.02f * envelope),
                                SpriteEffects.None, 0);
                        }
                    }
                    prev = pt;
                    hasPrev = true;
                }
            }

            //柔光茧底晕+白芯
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color gold = Main.hslToRgb(Hue, 0.8f, 0.66f) with { A = 0 };
            Main.EntitySpriteDraw(glow, drawPos, null, gold * (0.32f * Projectile.Opacity * bright), 0f,
                glow.Size() / 2f, 1.15f * tighten, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, (Color.White with { A = 0 }) * (0.2f * Projectile.Opacity), 0f,
                glow.Size() / 2f, 0.55f, SpriteEffects.None, 0);
            return false;
        }
    }
}
