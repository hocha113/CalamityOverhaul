using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaFishron
{
    /// <summary>
    /// 鬼奴猪龙鱼甩尾拉起的血水龙卷柱：从湖面立到半空的旋转血柱，
    /// 会沿水面缓慢游走，柱身由两股螺旋上升的血珠加环状水幕读出旋转，
    /// 顶端不断甩出离心血滴、根部持续犁水起圈。存在时间限定，场上至多两根
    /// （上限由召唤方把关）。各端全程确定性自演：漂移向/花样籽/湖面 Y
    /// 都从 spawn 参数来，无中途裁决，无需补同步
    /// </summary>
    internal class KikasaFishronBloodnado : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int NadoLife = 320;
        private const float ColumnHeight = 290f;
        private const float BaseRadius = 20f;
        private const float TopRadius = 48f;
        private const int GrowFrames = 16;
        private const int DecayFrames = 44;

        /// <summary>游走方向 ±1</summary>
        private float DriftDir => Projectile.ai[0];
        /// <summary>花样籽：同场两根错开呼吸相位</summary>
        private float Variety => Projectile.ai[1];
        /// <summary>湖面世界 Y：spawn 冻结，服务器上没有领域状态也不会被拽去天上</summary>
        private float LakeY => Projectile.ai[2];

        private int Elapsed => NadoLife - Projectile.timeLeft;

        private static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        private static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        private static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        private static Color RingGlow => KikasaDomain.CoolTint(new(198, 88, 82), new(126, 152, 158));
        private static Color FoamPale => KikasaDomain.CoolTint(new(214, 118, 106), new(170, 185, 190));

        private float Seed => Projectile.identity * 0.7391f % 4.13f + Variety * 1.7f;

        public override void SetDefaults() {
            Projectile.width = 96;
            Projectile.height = (int)ColumnHeight;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 26;
            //活得够久，迟入场的客户端也要收到它
            Projectile.netImportant = true;
            Projectile.timeLeft = NadoLife;
        }

        /// <summary>成柱后才伤人，塌柱前收口——窗口与可见的立柱严格对齐</summary>
        public override bool? CanDamage()
            => Elapsed > 12 && Projectile.timeLeft > 12 ? null : false;

        /// <summary>命中按当前柱形收窄：下窄上宽三段矩形，别用整框糊人</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float height = ColumnHeight * HeightEnv();
            if (height < 30f) {
                return false;
            }
            float baseX = Projectile.Center.X;
            for (int i = 0; i < 3; i++) {
                float h0 = height * i / 3f;
                float h1 = height * (i + 1) / 3f;
                float r = MathHelper.Lerp(BaseRadius, TopRadius, (i + 0.5f) / 3f) * RadiusEnv();
                Rectangle slab = new(
                    (int)(baseX - r), (int)(LakeY - h1),
                    (int)(r * 2f), (int)(h1 - h0));
                if (slab.Intersects(targetHitbox)) {
                    return true;
                }
            }
            return false;
        }

        public override bool? CanCutTiles() => false;

        //==================== 推进 ====================

        public override void AI() {
            int t = Elapsed;

            //游走：缓慢蛇行，濒死时步子放软
            float sway = 0.5f + 0.3f * MathF.Sin(t * 0.016f + Seed * 2.1f);
            float drift = DriftDir * sway * MathHelper.Clamp(Projectile.timeLeft / (float)DecayFrames, 0.3f, 1f);
            Projectile.velocity = new Vector2(drift, 0f);
            //柱脚钉死在湖面（spawn 冻结值，各端一致）
            Projectile.Center = new Vector2(Projectile.Center.X, LakeY - ColumnHeight * 0.5f);

            bool viewedOwner = KikasaDomain.Viewed != null
                && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner;

            //根部犁水：柱是水拉起来的，脚下一直有圈
            if (viewedOwner && t % 6 == 2) {
                KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, LakeY),
                    0.5f + 0.25f * MathF.Sin(t * 0.05f + Seed));
            }
            if (viewedOwner && t % 14 == 5) {
                KikasaDomainDeco.FootSplash(new Vector2(Projectile.Center.X, LakeY), 1.2f, drift * 6f);
            }

            //顶端离心甩滴：旋出去的血在半空散开再落湖
            if (!Main.dedServ && t > GrowFrames && t % 9 == 3 && Projectile.timeLeft > DecayFrames / 2) {
                float ang = Seed + t * 0.23f;
                Vector2 top = new(Projectile.Center.X + MathF.Sin(ang) * TopRadius * RadiusEnv(),
                    LakeY - ColumnHeight * HeightEnv() + 8f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(top,
                    new Vector2(MathF.Sin(ang) * Main.rand.NextFloat(1.6f, 3f), -Main.rand.NextFloat(0.6f, 2f)),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(26, 42));
            }

            //搅水闷响：低频循环
            if (t % 42 == 20) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.24f, Pitch = -0.85f, MaxInstances = 2 },
                    new Vector2(Projectile.Center.X, LakeY));
            }

            float glow = HeightEnv() * 0.5f;
            Lighting.AddLight(new Vector2(Projectile.Center.X, LakeY - 80f), 0.30f * glow, 0.08f * glow, 0.07f * glow);
        }

        /// <summary>柱高包络：起柱猛、塌柱缓</summary>
        private float HeightEnv() {
            float grow = MathHelper.Clamp(Elapsed / (float)GrowFrames, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);
            float decay = MathHelper.Clamp(Projectile.timeLeft / (float)DecayFrames, 0f, 1f);
            return grow * (0.3f + 0.7f * decay);
        }

        private float RadiusEnv() {
            float grow = MathHelper.Clamp(Elapsed / (float)GrowFrames, 0f, 1f);
            float decay = MathHelper.Clamp(Projectile.timeLeft / (float)DecayFrames, 0.25f, 1f);
            return grow * decay;
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            //塌柱：整柱水失去支撑砸回湖面
            Vector2 foot = new(Projectile.Center.X, LakeY);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = -0.4f, MaxInstances = 2 }, foot);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 12; i++) {
                float h = Main.rand.NextFloat(0.1f, 0.9f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(
                    new Vector2(foot.X + Main.rand.NextFloat(-30f, 30f), LakeY - ColumnHeight * h * 0.7f),
                    new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(1f, 3.5f)),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(18, 32));
            }
            if (KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner) {
                KikasaDomainDeco.SplashAt(foot, 9);
                KikasaDomainDeco.RippleAt(foot, 1.4f);
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D bead = CWRAsset.Extra_98?.Value;
            if (ring == null || glow == null || bead == null) {
                return false;
            }
            float heightEnv = HeightEnv();
            if (heightEnv < 0.03f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            float time = Main.GlobalTimeWrappedHourly;
            float baseX = Projectile.Center.X;
            float radiusEnv = RadiusEnv();
            float fade = MathHelper.Clamp(Projectile.timeLeft / (float)DecayFrames, 0f, 1f) * 0.45f + 0.55f;
            fade *= MathHelper.Clamp(Elapsed / 10f, 0f, 1f);

            //加色批：柱芯垫底幽光 + 环状水幕（环贴图不是实心渐变，转起来才像水幕）
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //柱芯：窄长的一道幽光垫底（只做底层，不当本体）
            float coreH = ColumnHeight * heightEnv;
            Vector2 corePos = new(baseX, LakeY - coreH * 0.5f);
            sb.Draw(glow, corePos - Main.screenPosition, null, BloodDeep * (0.16f * fade), 0f,
                glow.Size() * 0.5f,
                new Vector2(BaseRadius * 2.4f / glow.Width, coreH * 1.05f / glow.Height), SpriteEffects.None, 0f);

            Vector2 rOrigin = ring.Size() * 0.5f;
            const int slices = 9;
            for (int i = 0; i < slices; i++) {
                float h = (i + 0.5f) / slices;
                float y = LakeY - coreH * h;
                float r = MathHelper.Lerp(BaseRadius, TopRadius, h) * radiusEnv;
                //柱身摆动：越高甩得越开，两根靠 Seed 错拍
                float swayX = MathF.Sin(time * 3.2f + h * 4.3f + Seed) * (2f + h * 11f);
                //环的横径呼吸读出旋转
                float spin = 0.86f + 0.14f * MathF.Sin(time * 9.5f + h * 9f + Seed * 3f);
                float alpha = (0.16f + 0.10f * (1f - h)) * fade;
                Vector2 scale = new(r * 2f * spin / ring.Width, r * 0.62f / ring.Height);
                sb.Draw(ring, new Vector2(baseX + swayX, y) - Main.screenPosition, null,
                    RingGlow * alpha, MathF.Sin(time * 2.1f + h * 5f + Seed) * 0.08f,
                    rOrigin, scale, SpriteEffects.None, 0f);
            }

            //根部环脚：略亮的一圈咬住水面
            sb.Draw(ring, new Vector2(baseX, LakeY - 2f) - Main.screenPosition, null,
                RingGlow * (0.30f * fade), 0f, rOrigin,
                new Vector2(BaseRadius * 2.6f * radiusEnv / ring.Width, BaseRadius * 0.5f / ring.Height),
                SpriteEffects.None, 0f);

            //回到常规批画血珠：血珠是柱身的"材质本体"，螺旋上升读出涡旋
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 bOrigin = bead.Size() * 0.5f;
            const int strandBeads = 11;
            for (int s = 0; s < 2; s++) {
                for (int k = 0; k < strandBeads; k++) {
                    //沿柱高循环攀升
                    float prog = (k / (float)strandBeads + time * 0.66f * (1f + Variety * 0.12f)) % 1f;
                    float h = prog;
                    float ang = h * MathHelper.TwoPi * 2.2f + Seed + s * MathHelper.Pi + time * 5.6f;
                    float r = MathHelper.Lerp(BaseRadius, TopRadius, h) * radiusEnv;
                    float depth = MathF.Cos(ang);   //>0 在前
                    float swayX = MathF.Sin(time * 3.2f + h * 4.3f + Seed) * (2f + h * 11f);
                    Vector2 pos = new(baseX + swayX + MathF.Sin(ang) * r, LakeY - coreH * h);

                    float a = (0.42f + 0.30f * depth) * fade * MathF.Sin(MathHelper.Clamp(h, 0f, 1f) * MathHelper.Pi * 0.92f + 0.12f);
                    if (a <= 0.03f) {
                        continue;
                    }
                    float bScale = (0.11f + 0.035f * depth + 0.05f * h) * (1.6f - h * 0.4f);
                    //上升速度拉伸：珠子是被卷着往上甩的
                    Vector2 stretch = new(bScale * 0.8f, bScale * 1.35f);
                    Color body = (depth > 0f ? BloodMain : BloodDeep) * a;
                    Color rim = BloodDark * (a * 0.8f);
                    sb.Draw(bead, pos - Main.screenPosition, null, rim, 0.2f * depth,
                        bOrigin, stretch * 1.3f, SpriteEffects.None, 0f);
                    sb.Draw(bead, pos - Main.screenPosition, null, body, 0.2f * depth,
                        bOrigin, stretch, SpriteEffects.None, 0f);
                    if (depth > 0.55f) {
                        //迎面珠子给一粒湿反光
                        sb.Draw(bead, pos - Main.screenPosition, null,
                            (FoamPale with { A = 0 }) * (a * 0.5f), 0f,
                            bOrigin, stretch * 0.4f, SpriteEffects.None, 0f);
                    }
                }
            }

            return false;
        }
    }
}
