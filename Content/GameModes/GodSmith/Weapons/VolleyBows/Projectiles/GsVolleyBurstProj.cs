using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles
{
    /// <summary>
    /// 家族通用参数化 AoE 爆：处决 rider 与落地溅射共用的跨端可见实体。
    /// ai[0] = 判定半径 px，ai[1] = 主题索引（色板/音效/贴地压缩查表）。
    /// 前 3 帧为判定窗（每目标至多一次），其后纯冲击环演出
    /// </summary>
    internal class GsVolleyBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 主题表 ====================

        public const int ThemeGold = 0;
        public const int ThemeFrost = 1;
        public const int ThemeShadow = 2;
        public const int ThemeSpore = 3;
        public const int ThemeVolt = 4;
        public const int ThemeTide = 5;
        public const int ThemeHoly = 6;

        /// <summary>亮缘 / 主体 / 尾波 / Y 压缩（贴地溅射用 0.45，空中爆用 1）</summary>
        private static readonly (Color Bright, Color Main, Color Deep, float Squish)[] Themes = [
            (new Color(255, 232, 150), new Color(240, 178, 56), new Color(150, 96, 26), 0.45f),
            (new Color(210, 245, 255), new Color(110, 190, 240), new Color(40, 80, 160), 1f),
            (new Color(232, 170, 255), new Color(150, 70, 200), new Color(60, 20, 100), 1f),
            (new Color(214, 255, 140), new Color(120, 200, 60), new Color(40, 100, 30), 1f),
            (new Color(220, 245, 255), new Color(120, 200, 255), new Color(50, 80, 190), 1f),
            (new Color(200, 245, 255), new Color(60, 170, 220), new Color(20, 70, 130), 1f),
            (new Color(255, 250, 200), new Color(255, 210, 110), new Color(170, 120, 40), 1f),
        ];

        private ref float Radius => ref Projectile.ai[0];

        private ref float Theme => ref Projectile.ai[1];

        private ref float Life => ref Projectile.localAI[0];

        private const int TotalLife = 26;

        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
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
            int t = ThemeIndex();
            Lighting.AddLight(Projectile.Center, Themes[t].Main.ToVector3() * (0.5f * (1f - Life / TotalLife)));
        }

        private int ThemeIndex() => (int)MathHelper.Clamp(Theme, 0f, Themes.Length - 1);

        /// <summary>出生帧主题音画（客户端；每主题 ≤10 粒）</summary>
        private void SpawnThemeBurst() {
            int t = ThemeIndex();
            (Color bright, Color main, _, _) = Themes[t];
            SoundStyle sound = t switch {
                ThemeFrost => SoundID.Item27 with { Volume = 0.7f },
                ThemeShadow => SoundID.Item103 with { Volume = 0.6f },
                ThemeSpore => SoundID.Item97 with { Volume = 0.6f },
                ThemeVolt => SoundID.Item94 with { Volume = 0.55f },
                ThemeTide => SoundID.Splash with { Volume = 0.8f },
                ThemeHoly => SoundID.Item29 with { Volume = 0.7f },
                _ => SoundID.Item62 with { Volume = 0.55f, Pitch = 0.3f },
            };
            SoundEngine.PlaySound(sound, Projectile.Center);

            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f),
                    Main.rand.NextBool() ? bright : main,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(14, 24));
            }
            switch (t) {
                case ThemeFrost:
                    PRTLoader.NewParticle<PRT_DefCryoMist>(Projectile.Center, Vector2.Zero, main, 1f)
                        ?.Configure(30, Projectile.Center, Radius * 0.8f);
                    break;
                case ThemeSpore:
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_ToxicMist>(
                            Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.4f, Radius * 0.4f),
                            Main.rand.NextVector2Circular(0.8f, 0.8f), main, 0.8f)?.Configure(34);
                    }
                    break;
                case ThemeTide:
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_CampfireBubble>(
                            Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.35f, Radius * 0.35f),
                            new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.8f, 1.8f)),
                            bright, 0.5f)?.Configure(28);
                    }
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            int t = ThemeIndex();
            (Color bright, Color main, Color deep, float squish) = Themes[t];
            float progress = MathHelper.Clamp(Life / TotalLife, 0f, 1f);
            //快出慢停的环扩，波前先声夺人
            float ease = 1f - (1f - progress) * (1f - progress);
            float alpha = MathHelper.Clamp(1.15f * (1f - progress), 0f, 1f);
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center,
                MathHelper.Max(Radius, 8f) * (0.25f + 0.85f * ease), Radius * 0.2f,
                bright, main, deep, alpha,
                squish: squish, innerGlow: 0.18f, timeSeed: Projectile.identity * 0.37f);
            return false;
        }
    }
}
