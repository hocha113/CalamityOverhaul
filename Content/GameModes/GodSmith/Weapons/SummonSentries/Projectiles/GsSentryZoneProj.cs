using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles
{
    /// <summary>
    /// 哨兵族通用驻场判定区：火圈/蛛网带/过载电环/极光幕四样式共用一类。<br/>
    /// ai[0]=样式 ai[1]=半径（极光幕为半宽）ai[2]=持续帧（owner 按龄收尾，远端 timeLeft 兜底）。<br/>
    /// 哨兵全为固定炮台，区域生成后不移动；模式关闭当帧自灭
    /// </summary>
    internal class GsSentryZoneProj : ModProjectile
    {
        internal const int StyleFireRing = 0;
        internal const int StyleWebPatch = 1;
        internal const int StyleOverloadRing = 2;
        internal const int StyleAurora = 3;

        /// <summary>极光幕竖向高度</summary>
        private const float AuroraHeight = 160f;

        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonSentries";

        private ref float Style => ref Projectile.ai[0];
        private ref float Radius => ref Projectile.ai[1];
        private ref float Duration => ref Projectile.ai[2];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 30;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            if (!GameModeSystem.GodSmithActive) {
                Projectile.Kill();
                return;
            }
            Age++;
            if (Age == 1f) {
                //tick 间隔按样式定：火圈/蛛网 30f，过载环/极光幕 15f（本端量，各端由 ai[0] 推得一致）
                Projectile.localNPCHitCooldown = (int)Style >= StyleOverloadRing ? 15 : 30;
            }
            Projectile.timeLeft = 30;
            if (Projectile.IsOwnedByLocalPlayer() && Age >= Duration) {
                Projectile.Kill();
                return;
            }
            EmitAmbient();
        }

        /// <summary>持续期环境粒子（预算：单区每帧 ≤2）</summary>
        private void EmitAmbient() {
            if (VaultUtils.isServer) {
                return;
            }
            int style = (int)Style;
            switch (style) {
                case StyleFireRing:
                    Lighting.AddLight(Projectile.Center, new Vector3(0.5f, 0.24f, 0.06f));
                    if (Age % 6f == 0f) {
                        Vector2 at = Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.9f, Radius * 0.4f);
                        PRTLoader.NewParticle<PRT_HellFire>(at, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.3f)),
                            Color.White, Main.rand.NextFloat(0.35f, 0.6f));
                    }
                    break;
                case StyleWebPatch:
                    if (Age % 20f == 0f) {
                        PRTLoader.NewParticle<PRT_Light>(
                            Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.8f, Radius * 0.5f),
                            Vector2.Zero, new Color(210, 210, 220), Main.rand.NextFloat(0.05f, 0.09f))?.Configure(22, 0.5f);
                    }
                    break;
                case StyleOverloadRing:
                    Lighting.AddLight(Projectile.Center, new Vector3(0.2f, 0.32f, 0.5f));
                    if (Age % 5f == 0f) {
                        float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                        float r = MathHelper.Lerp(Radius / 1.4f, Radius, Main.rand.NextFloat());
                        PRTLoader.NewParticle<PRT_GraniteVolt>(Projectile.Center + ang.ToRotationVector2() * r,
                            ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 2f,
                            new Color(140, 200, 255), Main.rand.NextFloat(0.5f, 0.9f))?.Configure(5);
                    }
                    break;
                case StyleAurora:
                    if (Age % 4f == 0f) {
                        for (int i = 0; i < 2; i++) {
                            float hue = (Main.GlobalTimeWrappedHourly * 0.13f + Main.rand.NextFloat(0.25f)) % 1f;
                            Color c = Main.hslToRgb(hue, 0.8f, 0.62f);
                            Vector2 at = Projectile.Center + new Vector2(
                                Main.rand.NextFloat(-Radius, Radius), -AuroraHeight * 0.5f + Main.rand.NextFloat(20f));
                            PRTLoader.NewParticle<PRT_Sparkle>(at, new Vector2(0f, Main.rand.NextFloat(0.5f, 1.1f)),
                                c, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(c * 0.6f, Main.rand.Next(24, 40));
                        }
                    }
                    break;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dist = GsSentryBurstProj.DistRectPoint(targetHitbox, Projectile.Center);
            return (int)Style switch {
                //过载电环只打外带（原版光环判定让位内圈）
                StyleOverloadRing => dist <= Radius && dist >= Radius / 1.4f,
                StyleAurora => targetHitbox.Intersects(Utils.CenteredRectangle(Projectile.Center,
                    new Vector2(Radius * 2f, AuroraHeight))),
                _ => dist <= Radius,
            };
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            switch ((int)Style) {
                case StyleFireRing:
                    target.AddBuff(BuffID.OnFire, 60);
                    break;
                case StyleOverloadRing:
                    //感电标记：owner 本地量，链内其他哨兵吃加成
                    SentryGrid.MarkShocked(target);
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //淡入淡出包络（各端按本地龄，纯表现）
            float env = MathHelper.Clamp(Age / 10f, 0f, 1f)
                * MathHelper.Clamp((Duration - Age) / 20f, 0f, 1f);
            if (env <= 0.01f) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            float phase = Projectile.identity * 0.83f;
            switch ((int)Style) {
                case StyleFireRing: {
                        if (glow == null) {
                            break;
                        }
                        Color c = new Color(255, 110, 30) * (0.30f * env);
                        c.A = 0;
                        //贴地热光，压扁椭圆
                        Main.EntitySpriteDraw(glow, pos, null, c, 0f, glow.Size() * 0.5f,
                            new Vector2(Radius / 24f, Radius / 52f), SpriteEffects.None, 0);
                        break;
                    }
                case StyleWebPatch: {
                        Texture2D dark = CWRAsset.Extra_98?.Value;
                        Texture2D line = CWRAsset.Line?.Value;
                        if (dark == null || line == null) {
                            break;
                        }
                        //真 alpha 暗底 + 三根白丝交叉（identity 定角）
                        Main.EntitySpriteDraw(dark, pos, null, new Color(30, 28, 36) * (0.5f * env), 0f,
                            dark.Size() * 0.5f, new Vector2(Radius / 60f, Radius / 90f), SpriteEffects.None, 0);
                        for (int i = 0; i < 3; i++) {
                            float rot = phase + i * 1.05f;
                            Color silk = new Color(220, 220, 230) * (0.28f * env);
                            Main.EntitySpriteDraw(line, pos, null, silk, rot, line.Size() * 0.5f,
                                new Vector2(Radius / line.Width * 1.7f, 0.6f), SpriteEffects.None, 0);
                        }
                        break;
                    }
                case StyleOverloadRing: {
                        Texture2D arc = CWRAsset.ThunderTrail?.Value;
                        if (arc == null) {
                            break;
                        }
                        //环缘三段电弧缓旋（去同相）
                        float spin = Main.GlobalTimeWrappedHourly * 1.7f + phase;
                        for (int i = 0; i < 3; i++) {
                            float ang = spin + MathHelper.TwoPi * i / 3f;
                            Vector2 at = pos + ang.ToRotationVector2() * Radius * 0.86f;
                            Color c = new Color(150, 205, 255) * (0.5f * env);
                            c.A = 0;
                            Main.EntitySpriteDraw(arc, at, null, c, ang + MathHelper.PiOver2,
                                arc.Size() * 0.5f, new Vector2(0.5f, 0.24f), SpriteEffects.None, 0);
                        }
                        break;
                    }
                case StyleAurora: {
                        Texture2D band = CWRAsset.LightShot?.Value;
                        if (band == null) {
                            break;
                        }
                        //五条竖幕彩带缓摆，identity 相位五色错离
                        for (int i = 0; i < 5; i++) {
                            float fx = (i / 4f - 0.5f) * 2f;
                            float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.1f + phase + i * 1.3f) * 14f;
                            float hue = (i * 0.18f + Main.GlobalTimeWrappedHourly * 0.07f) % 1f;
                            Color c = Main.hslToRgb(hue, 0.75f, 0.6f) * (0.30f * env);
                            c.A = 0;
                            Vector2 at = pos + new Vector2(fx * Radius * 0.8f + sway, 0f);
                            Main.EntitySpriteDraw(band, at, null, c, -MathHelper.PiOver2,
                                new Vector2(0f, band.Height * 0.5f),
                                new Vector2(AuroraHeight / band.Width, 30f / band.Height), SpriteEffects.None, 0);
                        }
                        break;
                    }
            }
            return false;
        }
    }
}
