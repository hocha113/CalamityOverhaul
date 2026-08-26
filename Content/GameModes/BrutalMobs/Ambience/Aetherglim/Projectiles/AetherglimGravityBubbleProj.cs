using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Aetherglim.Projectiles
{
    /// <summary>
    /// 「引力泡」：微光湖上空漂过的大型可见重力异常泡。ai[0]=可见半径（像素）。
    /// 预告即承诺：泡从远处缓缓飘来，泡体虹彩清晰可见；玩家进泡获得短暂低重力漂浮
    /// （低重力由 <see cref="AetherglimPlayer"/> 施加，可能把人缓缓飘离平台），
    /// 泡被占用一段时间或撞上地形后破碎成星屑。恒无伤害，纯温和的重力方向扰动。
    /// 材质=引力泡膜：薄锐虹彩环缘承形、膜内星雾缓旋、内部星点绕心公转（重力异常签名）、
    /// 偏心高光点、纵横反相呼吸；破裂=先内收再环面崩开成星尘。
    /// 决策（生成/破裂）只在权威端，运动是出生状态的确定性函数，各端一致
    /// </summary>
    internal class AetherglimGravityBubbleProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "DiffusionCircle4")]
        private static Asset<Texture2D> RimRing = null;

        /// <summary>总存续帧（漂行约 18 秒，档位不改形状只调生成频率）</summary>
        private const int LifeFrames = 1080;
        /// <summary>出生成形帧（远处缓飘而来本身就是预告，这里只管膜体舒张）</summary>
        private const int GrowFrames = 30;
        /// <summary>被占用（泡内载人）累计多少帧后破裂</summary>
        private const int OccupiedPopFrames = 105;
        /// <summary>破裂前的胀缩挣扎窗（占用倒计时的尾段，膜体呼吸加急）</summary>
        private const int StrainFrames = 45;

        private float Radius => Projectile.ai[0];
        private int Elapsed => LifeFrames - Projectile.timeLeft;

        /// <summary>判定半径藏在可见环缘之内（判定不宽于可见体）</summary>
        public static bool Contains(Projectile proj, Vector2 point)
            => proj.Distance(point) < proj.ai[0] * 0.92f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 400;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = false;//恒无伤害，纯物理扰动
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.netImportant = true;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //两端以同一常量各自展开时间轴（镜像 WastesIceSlickZone 的做法）
                Projectile.timeLeft = LifeFrames;
            }

            //纵向缓浮：出生状态+自身标识的确定性函数，不掷随机数，各端一致
            float bobPhase = Elapsed * 0.021f + Projectile.identity * 1.37f;
            Projectile.velocity.Y = MathF.Sin(bobPhase) * 0.17f;

            //占用计时在所有端各自推进（由同步位置确定性得出）：客户端靠它演破裂前的挣扎
            bool occupied = false;
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && Contains(Projectile, player.Center)) {
                    occupied = true;
                    break;
                }
            }
            if (occupied) {
                Projectile.localAI[1]++;
            }

            //破裂裁决只在权威端：占用到时或撞上崖体
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (Projectile.localAI[1] >= OccupiedPopFrames) {
                    Projectile.Kill();
                    return;
                }
                float probe = Radius * 0.55f;
                if (Collision.SolidCollision(Projectile.Center - new Vector2(probe), (int)(probe * 2f), (int)(probe * 2f))) {
                    Projectile.Kill();
                    return;
                }
            }

            if (Main.dedServ) {
                return;
            }

            //膜缘偶尔沁出一粒珠光小泡（客户端点缀，≤3/s/泡）
            if (Main.rand.NextBool(24)) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 rimPos = Projectile.Center + ang.ToRotationVector2() * Radius * 0.95f;
                PRTLoader.NewParticle<PRT_AetherglimPearl>(rimPos,
                    new Vector2(0f, -Main.rand.NextFloat(0.2f, 0.5f)), Color.White,
                    Main.rand.NextFloat(0.12f, 0.2f))
                    .Configure(Main.rand.Next(40, 70), Main.rand.NextFloat(6f));
            }
            //膜内星点微尘
            if (Main.rand.NextBool(30)) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.6f, Radius * 0.6f),
                    DustID.ShimmerSpark, Main.rand.NextVector2Circular(0.3f, 0.3f), 140, default, 0.8f);
                dust.noGravity = true;
            }
            Color glowTint = AetherglimFX.Iridescent(Elapsed * 0.017f + Projectile.identity);
            Lighting.AddLight(Projectile.Center, glowTint.ToVector3() * 0.34f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => false;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D rim = RimRing?.Value;
            if (rim == null) {
                return false;
            }
            int elapsed = Elapsed;
            float grow = elapsed < GrowFrames
                ? 0.35f + 0.65f * VaultUtils.EaseOutCubic(elapsed / (float)GrowFrames)
                : 1f;
            float alpha = MathHelper.Clamp(elapsed / (float)GrowFrames, 0f, 1f);

            //占用挣扎窗：呼吸加急、环缘提亮，读作"要破了"
            float strain = MathHelper.Clamp((Projectile.localAI[1] - (OccupiedPopFrames - StrainFrames)) / (float)StrainFrames, 0f, 1f);
            float wobbleRate = 0.055f + strain * 0.07f;
            float huePhase = elapsed * 0.017f + Projectile.identity;
            float squish = MathF.Sin(elapsed * (0.09f + strain * 0.1f) + Projectile.identity) * (0.04f + strain * 0.05f);
            Vector2 scaleShape = new(1f + squish, 1f - squish);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float diameter = Radius * 2f * grow;

            //膜内星雾：真 alpha 烟羽双层反向缓旋，给泡一个半透明的"身体"
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog != null) {
                float fogScale = diameter / fog.Width * 0.92f;
                Color fogDeep = AetherglimFX.IridescentDeep(huePhase) * (0.16f * alpha);
                Color fogLite = AetherglimFX.IridescentDeep(huePhase + 2.4f) * (0.11f * alpha);
                Main.EntitySpriteDraw(fog, drawPos, null, fogDeep, elapsed * 0.006f + Projectile.identity,
                    fog.Size() / 2f, fogScale * scaleShape, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(fog, drawPos, null, fogLite, -elapsed * 0.004f + Projectile.identity * 2f,
                    fog.Size() / 2f, fogScale * 0.7f * scaleShape, SpriteEffects.FlipHorizontally, 0);
            }

            //内部星点绕心公转：重力异常的签名（里面的东西都在打转）
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (star != null) {
                for (int i = 0; i < 3; i++) {
                    float orbit = elapsed * wobbleRate * 0.55f + Projectile.identity + i * MathHelper.TwoPi / 3f;
                    Vector2 motePos = drawPos + orbit.ToRotationVector2() * Radius * 0.52f * grow
                        * new Vector2(1f, 0.82f);
                    Color moteC = AetherglimFX.Iridescent(huePhase + i * 1.6f);
                    Main.EntitySpriteDraw(star, motePos, null, (moteC with { A = 0 }) * (0.5f * alpha),
                        orbit, star.Size() / 2f, 0.1f, SpriteEffects.None, 0);
                }
                //逆行内环一粒：内外反向，异常感
                float counter = -elapsed * wobbleRate * 0.8f + Projectile.identity * 3f;
                Vector2 innerPos = drawPos + counter.ToRotationVector2() * Radius * 0.26f * grow;
                Main.EntitySpriteDraw(star, innerPos, null, (Color.White with { A = 0 }) * (0.4f * alpha),
                    counter, star.Size() / 2f, 0.07f, SpriteEffects.None, 0);
            }

            //薄锐虹彩环缘三层：色相错拍+尺度微错=薄膜干涉的色散
            float rimBase = diameter / (rim.Width * 0.95f);
            for (int i = 0; i < 3; i++) {
                float hueOff = i * 2.1f;
                float scaleOff = 0.97f + i * 0.03f;
                Color rimC = AetherglimFX.Iridescent(huePhase + hueOff);
                Main.EntitySpriteDraw(rim, drawPos, null, (rimC with { A = 0 }) * ((0.4f + strain * 0.22f) * alpha),
                    elapsed * 0.002f * (i % 2 == 0 ? 1f : -1f), rim.Size() / 2f,
                    rimBase * scaleOff * scaleShape, SpriteEffects.None, 0);
            }
            //白芯环：一线锐缘
            Main.EntitySpriteDraw(rim, drawPos, null, (Color.White with { A = 0 }) * (0.28f * alpha),
                0f, rim.Size() / 2f, rimBase * scaleShape, SpriteEffects.None, 0);

            //偏心高光点：泡膜的湿亮
            if (star != null) {
                Vector2 glintOff = new Vector2(-0.36f, -0.36f) * Radius * grow;
                float glintPulse = 0.75f + 0.25f * MathF.Sin(elapsed * 0.11f + Projectile.identity);
                Main.EntitySpriteDraw(star, drawPos + glintOff, null,
                    (Color.White with { A = 0 }) * (0.55f * alpha * glintPulse),
                    0.5f, star.Size() / 2f, 0.2f, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //破膜：先一声微光碎裂，再环面崩开+星屑上散
            SoundEngine.PlaySound(SoundID.Shimmer1 with { Volume = 0.6f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.24f, Pitch = 0.6f, MaxInstances = 3 }, Projectile.Center);

            float hueSeed = Projectile.identity * 0.7f;
            //DiffusionCircle4 实测 156×156、环带在 0.95R：Scale=R/74 时环半径≈泡半径
            PRTLoader.NewParticle<PRT_AetherglimBurstRing>(Projectile.Center, Vector2.Zero,
                Color.White, Radius / 74f).Configure(26, hueSeed);
            PRTLoader.NewParticle<PRT_AetherglimBurstRing>(Projectile.Center, Vector2.Zero,
                Color.White, Radius / 74f * 0.62f).Configure(20, hueSeed + 2f);

            for (int i = 0; i < 18; i++) {
                float ang = MathHelper.TwoPi * i / 18f + Main.rand.NextFloat(0.3f);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(1.4f, 4.2f);
                vel.Y -= 0.8f;
                PRTLoader.NewParticle<PRT_AetherglimStarMote>(
                    Projectile.Center + ang.ToRotationVector2() * Radius * 0.5f, vel,
                    Color.White, Main.rand.NextFloat(0.18f, 0.34f))
                    .Configure(Main.rand.Next(42, 76), hueSeed + i * 0.35f);
            }
            for (int i = 0; i < 8; i++) {
                Dust dust = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.7f, Radius * 0.7f),
                    DustID.ShimmerSpark, Main.rand.NextVector2Circular(1.6f, 1.6f), 120, default, 1.1f);
                dust.noGravity = true;
            }
        }
    }
}
