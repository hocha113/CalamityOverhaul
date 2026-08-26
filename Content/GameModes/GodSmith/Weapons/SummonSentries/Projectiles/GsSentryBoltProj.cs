using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles
{
    /// <summary>
    /// 哨兵族通用飞行弹体，五样式共用一类。<br/>
    /// ai[0]=样式（0 棱光射线 / 1 月门伴束 / 2 迫击火雨 / 3 上抛破片 / 4 极寒吐息）
    /// ai[1]=样式参数（棱光=色相种子，其余保留 0）。<br/>
    /// 穿透/重力/寿命按样式在首帧配置（本类自有弹幕，各端由 ai[0] 推得一致）
    /// </summary>
    internal class GsSentryBoltProj : ModProjectile
    {
        internal const int StylePrismRay = 0;
        internal const int StyleLunarLance = 1;
        internal const int StyleMortar = 2;
        internal const int StyleShard = 3;
        internal const int StyleFrostBreath = 4;

        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonSentries";

        private static readonly Color LunarTint = new(130, 200, 240);
        private static readonly Color FrostTint = new(150, 215, 250);

        private ref float Style => ref Projectile.ai[0];
        private ref float StyleParam => ref Projectile.ai[1];
        private ref float Age => ref Projectile.localAI[0];

        private Color PrismColor => Main.hslToRgb((StyleParam + Projectile.identity * 0.191f) % 1f, 0.85f, 0.62f);

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        private void ConfigureByStyle() {
            switch ((int)Style) {
                case StylePrismRay:
                    Projectile.penetrate = 2;
                    Projectile.timeLeft = 40;
                    break;
                case StyleLunarLance:
                    Projectile.penetrate = 5;
                    Projectile.timeLeft = 50;
                    break;
                case StyleMortar:
                    Projectile.tileCollide = true;
                    Projectile.timeLeft = 240;
                    break;
                case StyleShard:
                    Projectile.tileCollide = true;
                    Projectile.timeLeft = 120;
                    break;
                case StyleFrostBreath:
                    Projectile.penetrate = 3;
                    Projectile.timeLeft = 26;
                    Projectile.Resize(26, 26);
                    break;
            }
        }

        public override void AI() {
            Age++;
            if (Age == 1f) {
                ConfigureByStyle();
            }
            int style = (int)Style;
            //样式运动学
            switch (style) {
                case StyleMortar:
                    Projectile.velocity.Y += 0.28f;
                    break;
                case StyleShard:
                    Projectile.velocity.Y += 0.32f;
                    Projectile.velocity.X *= 0.995f;
                    break;
                case StyleFrostBreath:
                    Projectile.velocity *= 0.975f;
                    break;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (VaultUtils.isServer) {
                return;
            }
            //飞行相粒子
            switch (style) {
                case StylePrismRay:
                    Lighting.AddLight(Projectile.Center, PrismColor.ToVector3() * 0.3f);
                    if (Age % 3f == 0f) {
                        PRTLoader.NewParticle<PRT_Light>(Projectile.Center - Projectile.velocity * 0.4f,
                            Vector2.Zero, PrismColor, 0.09f)?.Configure(8, 0.7f);
                    }
                    break;
                case StyleLunarLance:
                    Lighting.AddLight(Projectile.Center, LunarTint.ToVector3() * 0.35f);
                    break;
                case StyleMortar:
                    Lighting.AddLight(Projectile.Center, new Vector3(0.45f, 0.22f, 0.06f));
                    if (Age % 3f == 0f) {
                        PRTLoader.NewParticle<PRT_HellFire>(Projectile.Center,
                            -Projectile.velocity * 0.1f, Color.White, Main.rand.NextFloat(0.35f, 0.55f));
                    }
                    break;
                case StyleShard:
                    if (Age % 4f == 0f) {
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, -Projectile.velocity * 0.15f,
                            new Color(255, 150, 60), Main.rand.NextFloat(0.22f, 0.36f))
                            ?.Configure(false, Main.rand.Next(8, 14));
                    }
                    break;
                case StyleFrostBreath:
                    Lighting.AddLight(Projectile.Center, FrostTint.ToVector3() * 0.2f);
                    if (Age % 2f == 0f) {
                        PRTLoader.NewParticle<PRT_DefFrostGlint>(
                            Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                            Projectile.velocity * 0.3f, FrostTint, Main.rand.NextFloat(0.4f, 0.7f))
                            ?.Configure(Main.rand.Next(10, 18));
                    }
                    break;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if ((int)Style == StyleFrostBreath) {
                //原版减益骑原版同步，跨端一致
                target.AddBuff(BuffID.Frostburn, 120);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            int style = (int)Style;
            switch (style) {
                case StyleMortar:
                    SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.35f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
                    PRTLoader.NewParticle<PRT_MechExplosion>(Projectile.Center, Vector2.Zero,
                        Color.White, 0.6f)?.Configure(20, new Color(255, 130, 40));
                    break;
                case StyleShard:
                    for (int i = 0; i < 3; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                            new Color(255, 150, 60), Main.rand.NextFloat(0.25f, 0.4f))
                            ?.Configure(true, Main.rand.Next(10, 18));
                    }
                    break;
                case StylePrismRay:
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, PrismColor, 0.13f)?.Configure(10, 0.8f);
                    break;
                case StyleFrostBreath:
                    PRTLoader.NewParticle<PRT_DefFrostGlint>(Projectile.Center, Vector2.Zero,
                        FrostTint, 0.6f)?.Configure(14);
                    break;
                case StyleLunarLance:
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, LunarTint, 0.15f)?.Configure(10, 0.8f);
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D shot = CWRAsset.LightShot?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (shot == null || glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float fadeIn = MathHelper.Clamp(Age / 4f, 0f, 1f);
            float speed = Projectile.velocity.Length();
            int style = (int)Style;
            //弹体主色
            Color body = style switch {
                StylePrismRay => PrismColor,
                StyleLunarLance => LunarTint,
                StyleMortar => new Color(255, 140, 50),
                StyleShard => new Color(255, 160, 70),
                _ => FrostTint,
            };
            //速度拉伸光矢主体（吐息不用矢形，走雾体）
            if (style != StyleFrostBreath) {
                float stretch = MathHelper.Clamp(speed * 0.05f, 0.4f, 1.6f);
                Color c = body * (0.85f * fadeIn);
                c.A = 0;
                Main.EntitySpriteDraw(shot, pos, null, c, Projectile.rotation,
                    new Vector2(shot.Width * 0.8f, shot.Height * 0.5f),
                    new Vector2(stretch * 0.5f, 0.10f + speed * 0.002f), SpriteEffects.None, 0);
            }
            //光头
            Color head = body * ((style == StyleFrostBreath ? 0.4f : 0.7f) * fadeIn);
            head.A = 0;
            float headScale = style == StyleFrostBreath ? 0.7f : 0.3f;
            Main.EntitySpriteDraw(glow, pos, null, head, 0f, glow.Size() * 0.5f, headScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
