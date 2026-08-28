using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 灾变族左键 rider 共用的参数化微爆（P13 返工新增）：各武器签名机制触发的
    /// 跨端可见小型 AoE。ai[0] = 判定半径 px，ai[1] = 主题索引，ai[2] = 主题参数
    /// （星籁=音阶步进定音高）。前 3 帧为判定窗（每目标至多一次），其后纯演出；
    /// 主题不只换色：星籁旋星芒、驻波十字、新星双环、龙焰高内辉、白灾霜缘各有形
    /// </summary>
    internal class GsCataclysmRiderBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicCataclysm";

        //==================== 主题表 ====================

        /// <summary>星籁和弦爆：旋转星芒</summary>
        public const int ThemeStar = 0;
        /// <summary>龙焰孽火爆：高内辉焰环</summary>
        public const int ThemeEmber = 1;
        /// <summary>星云微新星：双环反旋</summary>
        public const int ThemeNova = 2;
        /// <summary>谐振驻波脉冲：十字驻波</summary>
        public const int ThemeNode = 3;
        /// <summary>白灾霜爆：亮霜缘</summary>
        public const int ThemeFrost = 4;

        /// <summary>亮缘 / 主体 / 尾波（色板取各 director 常量，守族色身份）</summary>
        private static (Color Bright, Color Main, Color Deep) ThemeColors(int t) => t switch {
            ThemeEmber => (new Color(255, 214, 150), GsDragonWrathDirector.BetsyOrange, GsDragonWrathDirector.BetsyEmber),
            ThemeNova => (GsNovaDetonationDirector.NovaPink, GsNovaDetonationDirector.NovaViolet, GsNovaDetonationDirector.NovaDeep),
            ThemeNode => (new Color(255, 240, 190), GsResonanceCollapseDirector.ResonGold, GsResonanceCollapseDirector.ResonDeep),
            ThemeFrost => (GsWhiteoutDirector.FrostPale, GsWhiteoutDirector.FrostBlue, new Color(40, 80, 160)),
            _ => (new Color(255, 235, 245), GsStellarFinaleDirector.StarPink, new Color(120, 90, 170)),
        };

        private ref float Radius => ref Projectile.ai[0];

        private ref float Theme => ref Projectile.ai[1];

        private ref float ThemeParam => ref Projectile.ai[2];

        private ref float Life => ref Projectile.localAI[0];

        private const int TotalLife = 24;

        private int ThemeIndex() => (int)MathHelper.Clamp(Theme, 0f, ThemeFrost);

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Life <= 3f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float r = MathHelper.Max(Radius, 8f);
            Vector2 closest = targetHitbox.ClosestPointInRect(Projectile.Center);
            return closest.DistanceSQ(Projectile.Center) <= r * r;
        }

        public override void AI() {
            if (Life == 0f && !VaultUtils.isServer) {
                SpawnThemeBurst();
            }
            Life++;
            (_, Color main, _) = ThemeColors(ThemeIndex());
            Lighting.AddLight(Projectile.Center, main.ToVector3() * (0.5f * (1f - Life / TotalLife)));
        }

        /// <summary>出生帧主题音画（客户端）</summary>
        private void SpawnThemeBurst() {
            int t = ThemeIndex();
            (Color bright, Color main, _) = ThemeColors(t);
            SoundStyle sound = t switch {
                //星籁：音高随和弦步进爬升（ThemeParam = 音阶步）
                ThemeStar => SoundID.Item26 with { Volume = 0.6f, Pitch = -0.1f + 0.08f * ThemeParam },
                ThemeEmber => SoundID.Item74 with { Volume = 0.5f, Pitch = 0.1f },
                ThemeNova => SoundID.Item103 with { Volume = 0.45f, Pitch = 0.3f },
                ThemeNode => SoundID.Item25 with { Volume = 0.55f, Pitch = 0.45f },
                _ => SoundID.Item27 with { Volume = 0.6f },
            };
            SoundEngine.PlaySound(sound, Projectile.Center);

            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f),
                    Main.rand.NextBool() ? bright : main,
                    Main.rand.NextFloat(0.28f, 0.46f))?.Configure(true, Main.rand.Next(14, 22));
            }
            switch (t) {
                case ThemeStar:
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_Note>(
                            Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                            new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), -Main.rand.NextFloat(0.7f, 1.5f)),
                            Main.rand.NextBool() ? bright : main, Main.rand.NextFloat(0.8f, 1.1f))
                            ?.Configure(Main.rand.Next(26, 40));
                    }
                    break;
                case ThemeEmber:
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_HellFlame>(
                            Projectile.Center + Main.rand.NextVector2Circular(10f, 8f),
                            Main.rand.NextVector2Circular(1.4f, 1.4f) - new Vector2(0f, 1.1f),
                            main, Main.rand.NextFloat(0.34f, 0.52f));
                    }
                    break;
                case ThemeNova:
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, main, 0.08f)
                        ?.Configure(0.08f, 0.5f, 14);
                    break;
                case ThemeNode:
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, bright, 0.06f)
                        ?.Configure(0.06f, 0.35f, 12);
                    break;
                case ThemeFrost:
                    PRTLoader.NewParticle<PRT_DefCryoMist>(Projectile.Center, Vector2.Zero, main, 1f)
                        ?.Configure(26, Projectile.Center, Radius * 0.7f);
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_Sparkle>(
                            Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.4f, Radius * 0.4f),
                            new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.4f, 1.1f)),
                            bright, Main.rand.NextFloat(0.3f, 0.48f))?.Configure(main, 24, 0.05f, 0.9f);
                    }
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int t = ThemeIndex();
            (Color bright, Color main, Color deep) = ThemeColors(t);
            float progress = MathHelper.Clamp(Life / TotalLife, 0f, 1f);
            float ease = 1f - (1f - progress) * (1f - progress);
            float alpha = MathHelper.Clamp(1.15f * (1f - progress), 0f, 1f);
            float radius = MathHelper.Max(Radius, 8f) * (0.25f + 0.85f * ease);
            float seed = Projectile.identity * 0.37f;

            //主环：波前先声夺人
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, radius, Radius * 0.2f,
                bright, main, deep, alpha,
                squish: 1f, innerGlow: t == ThemeEmber ? 0.4f : 0.16f, timeSeed: seed);

            //主题形：不止换色
            switch (t) {
                case ThemeStar: {
                    //旋转星芒双层反旋
                    Texture2D star = CWRAsset.StarTexture_White?.Value;
                    if (star != null) {
                        float spin = Life * 0.09f + Projectile.identity * 0.7f;
                        Color glow = main with { A = 0 };
                        Main.EntitySpriteDraw(star, Projectile.Center - Main.screenPosition, null,
                            glow * (0.7f * alpha), spin, star.Size() * 0.5f, 0.16f + 0.1f * ease, SpriteEffects.None, 0);
                        Main.EntitySpriteDraw(star, Projectile.Center - Main.screenPosition, null,
                            (Color.White with { A = 0 }) * (0.4f * alpha), -spin * 0.7f,
                            star.Size() * 0.5f, 0.1f + 0.06f * ease, SpriteEffects.None, 0);
                    }
                    break;
                }
                case ThemeNode: {
                    //十字驻波：两根正交光杆随进度收缩，读作波节驻留
                    Texture2D line = VaultAsset.placeholder2?.Value;
                    if (line != null) {
                        Color glow = main with { A = 0 };
                        float len = radius * (2.2f - 1.1f * ease);
                        float wob = 1f + 0.2f * MathF.Sin(Life * 0.55f + seed);
                        for (int i = 0; i < 2; i++) {
                            float rot = MathHelper.PiOver2 * i + seed;
                            Main.EntitySpriteDraw(line, Projectile.Center - Main.screenPosition,
                                new Rectangle(0, 0, 1, 1), glow * (0.5f * alpha), rot,
                                new Vector2(0.5f, 0.5f), new Vector2(len * wob, 2.2f), SpriteEffects.None, 0);
                        }
                    }
                    break;
                }
                case ThemeNova:
                    //内环反相：双环错拍，呼应新星引爆的错拍环
                    ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center,
                        radius * 0.55f, Radius * 0.14f, bright, deep, deep, alpha * 0.7f,
                        squish: 1f, innerGlow: 0.1f, timeSeed: seed + 3.1f);
                    break;
            }
            return false;
        }
    }
}
