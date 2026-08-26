using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EmpressOfLight
{
    /// <summary>
    /// 棱镜爆裂：干涉光径交点的引爆体。owner 端在交点生成，经原版同步链各端可见；
    /// ai[0]=色相 ai[1]=判定半径。多色棱镜碎光+双层绽放环，判定窗口内单次命中
    /// </summary>
    internal class InterferencePrismBurst : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int Life = 26;
        /// <summary>扩张判定窗口（帧），之后纯余辉</summary>
        private const int DamageWindow = 12;

        private ref float Timer => ref Projectile.localAI[0];
        private float Hue => Projectile.ai[0];
        private float Radius => Math.Max(Projectile.ai[1], 60f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Life;
            //整个爆裂窗口对每个敌人只结算一次
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Timer == 0f && !VaultUtils.isServer) {
                //起爆拍：棱彩碎光十二向+折射涟漪+棱镜高音（音高随色相错开防齐爆单调）
                SoundEngine.PlaySound(SoundID.Item163 with { Volume = 0.32f, Pitch = 0.2f + Hue * 0.5f }, Projectile.Center);
                for (int i = 0; i < 12; i++) {
                    float hue = (Hue + i / 12f * 0.4f) % 1f;
                    Vector2 vel = (MathHelper.TwoPi / 12f * i).ToRotationVector2() * Main.rand.NextFloat(3.5f, 8f);
                    PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center, vel,
                        EmpressMotion.Prism(hue, 0.66f), Main.rand.NextFloat(0.8f, 1.3f))?.Configure(22, hue);
                }
                PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero,
                    Color.White, Radius / 210f)?.Configure(18, Hue);
            }
            Timer++;
            Projectile.velocity = Vector2.Zero;

            float p = Timer / Life;
            Lighting.AddLight(Projectile.Center, EmpressMotion.Prism(Hue, 0.7f).ToVector3() * (1f - p) * 1.1f);
        }

        public override bool? CanDamage() => Timer <= DamageWindow ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //扩张圆判定：半径随窗口进度快出
            float cur = Radius * VaultUtils.EaseOutCubic(MathHelper.Clamp(Timer / DamageWindow, 0f, 1f));
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(Projectile.Center, nearest) < cur * cur;
        }

        public override bool PreDraw(ref Color lightColor) {
            float p = MathHelper.Clamp(Timer / Life, 0f, 1f);
            float fade = (1f - p) * (1f - p);
            float ringR = Radius * VaultUtils.EaseOutCubic(p) * 0.9f;

            Color prismCol = EmpressMotion.Prism(Hue, 0.68f);
            //共享冲击环：白热前锋+色散滞后双层（EmpressRadiance 同语法）
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, ringR, ringR * 0.24f,
                Color.White, Color.White, prismCol, 0.75f * fade,
                timeSeed: Projectile.whoAmI * 0.41f);
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, ringR * 0.82f, ringR * 0.2f,
                Color.White, prismCol, EmpressMotion.Prism(Hue + 0.13f, 0.6f), 0.55f * fade,
                innerGlow: 0.25f, timeSeed: Projectile.whoAmI * 0.41f + 3.7f);

            //中心星芒收缩+柔光衬底（黑底贴图走 A=0 加色技法）
            Texture2D flare = CWRAsset.StarFlare01.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color white = Color.White with { A = 0 };
            Color prism = prismCol with { A = 0 };
            Main.EntitySpriteDraw(flare, drawPos, null, white * (0.8f * fade), p * 1.1f,
                flare.Size() / 2f, (0.35f + 0.45f * (1f - p)) * Radius / 340f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, prism * fade, 0f, glow.Size() / 2f,
                Radius / 110f * (1f - p * 0.45f), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 全屏干涉爆发：昼夜切换瞬间由 owner 端生成。范围伤害一次结算；
    /// 敌方弹幕清除由服务端实例首帧执行（弹幕权威端），客户端只演出。
    /// ai[0]=色相种子（入昼暖彩/入夜冷彩）
    /// </summary>
    internal class InterferenceDawnBurst : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int Life = 40;
        private const int DamageWindow = 18;
        /// <summary>敌方弹幕清除半径</summary>
        private const float ClearRadius = 1400f;

        private ref float Timer => ref Projectile.localAI[0];
        private float HueSeed => Projectile.ai[0];
        private static float Radius => WingsOfInterferencePlayer.DawnBurstRadius;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Life;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Timer == 0f) {
                //弹幕清除：只在权威端执行（单机与服务器），客户端等同步
                if (!VaultUtils.isClient) {
                    float r2 = ClearRadius * ClearRadius;
                    for (int i = 0; i < Main.maxProjectiles; i++) {
                        Projectile other = Main.projectile[i];
                        if (!other.active || !other.hostile || other.damage <= 0) {
                            continue;
                        }
                        if (Vector2.DistanceSquared(other.Center, Projectile.Center) < r2) {
                            other.Kill();
                        }
                    }
                }
                //扩张始拍碎光（脉冲屏闪与蝶群由 ModPlayer 侧已播，这里补环心细节）
                if (!VaultUtils.isServer) {
                    for (int i = 0; i < 20; i++) {
                        float hue = (HueSeed + i / 20f * 0.5f) % 1f;
                        Vector2 vel = (MathHelper.TwoPi / 20f * i).ToRotationVector2() * Main.rand.NextFloat(6f, 15f);
                        PRTLoader.NewParticle<PRT_EmpressSpark>(Projectile.Center, vel,
                            EmpressMotion.Prism(hue, 0.7f), Main.rand.NextFloat(1f, 1.6f))?.Configure(30, hue);
                    }
                    PRTLoader.NewParticle<PRT_EmpressRipple>(Projectile.Center, Vector2.Zero,
                        Color.White, 1.6f)?.Configure(26, HueSeed);
                }
            }
            Timer++;
            Projectile.velocity = Vector2.Zero;

            float p = Timer / Life;
            Lighting.AddLight(Projectile.Center, EmpressMotion.Prism(HueSeed, 0.75f).ToVector3() * (1f - p) * 2.2f);
        }

        public override bool? CanDamage() => Timer <= DamageWindow ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float cur = Radius * VaultUtils.EaseOutCubic(MathHelper.Clamp(Timer / DamageWindow, 0f, 1f));
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.DistanceSquared(Projectile.Center, nearest) < cur * cur;
        }

        public override bool PreDraw(ref Color lightColor) {
            float p = MathHelper.Clamp(Timer / Life, 0f, 1f);
            float fade = (1f - p) * (1f - p);
            float ringR = Radius * VaultUtils.EaseOutCubic(p);

            Color prismCol = EmpressMotion.Prism(HueSeed, 0.7f);
            Color lagCol = EmpressMotion.Prism(HueSeed + 0.16f, 0.62f);
            //全屏干涉波前：三层错相环（白热锋线→主色→滞后色散）
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, ringR, ringR * 0.1f,
                Color.White, Color.White, prismCol, 0.85f * fade,
                timeSeed: Projectile.whoAmI * 0.29f);
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, ringR * 0.88f, ringR * 0.085f,
                Color.White, prismCol, lagCol, 0.6f * fade,
                innerGlow: 0.2f, timeSeed: Projectile.whoAmI * 0.29f + 2.3f);
            ShockRingDraw.Draw(Main.spriteBatch, Projectile.Center, ringR * 0.74f, ringR * 0.06f,
                prismCol, lagCol, lagCol, 0.4f * fade,
                timeSeed: Projectile.whoAmI * 0.29f + 5.9f);

            //中心星闪
            Texture2D flare = CWRAsset.StarFlare01.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color white = Color.White with { A = 0 };
            Main.EntitySpriteDraw(flare, drawPos, null, white * (0.9f * fade), p * 0.8f,
                flare.Size() / 2f, 1.4f * (1f - p * 0.5f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(flare, drawPos, null, (prismCol with { A = 0 }) * (0.6f * fade), -p * 0.5f,
                flare.Size() / 2f, 2.1f * (1f - p * 0.6f), SpriteEffects.None, 0);
            return false;
        }
    }
}
