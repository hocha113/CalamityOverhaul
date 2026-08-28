using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Ashreign.Projectiles
{
    /// <summary>
    /// 熔泡爆·迸溅熔渣珠。ai[0]=风味种子（0~1，生成参数随包）。
    /// 材质=结着灰壳的熔渣：黑壳骑在炽亮体上、重力抛物线、沿途甩火星、
    /// 落地起一柱小烟（溅点烟柱余韵）。
    /// 与 JungleHell 小鬼火弹划清：珠走重力弧且撞地即灭，火弹直线穿墙；
    /// 贴图也换用灰烬块，不与 BallofFire 撞脸。
    /// 出膛淡入期无判定（公平阀）；命中挂短暂原版 On Fire!
    /// </summary>
    internal class AshreignMagmaBeadProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.AshBallFalling;

        /// <summary>淡入帧数，判定开启与可见同门</summary>
        private const int FadeInFrames = 6;
        private const float Gravity = 0.22f;
        private const float MaxFallSpeed = 11f;
        /// <summary>命中灼烧时长（短暂 On Fire!）</summary>
        private const int BurnTicks = 150;

        private float Seed => Projectile.ai[0];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 160;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Age++;
            Projectile.alpha = (int)MathHelper.Lerp(200f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));

            //抛物线：重力 + 终端速度
            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }
            //熔渣翻滚
            Projectile.rotation += 0.16f * (Seed > 0.5f ? 1f : -1f);

            if (Main.dedServ) {
                return;
            }
            //沿途甩火星与细屑（低频）
            if (Main.rand.NextBool(5)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    -Projectile.velocity * 0.12f, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
            if (Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_DefEmber>(Projectile.Center,
                    -Projectile.velocity * 0.2f + VaultUtils.RandVr(0.8f),
                    Ashreign.EmberWarm, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Main.rand.Next(16, 28), 0.1f);
            }
            Lighting.AddLight(Projectile.Center, 0.34f, 0.15f, 0.04f);
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire, BurnTicks);
        }

        /// <summary>溅点余韵：落回岩浆是闷响飞沫，落到实地起一柱小烟（活得比珠久）</summary>
        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            Point tile = Projectile.Center.ToTileCoordinates();
            bool intoLava = Ashreign.IsLavaTile(tile.X, tile.Y) || Ashreign.IsLavaTile(tile.X, tile.Y + 1);

            SoundEngine.PlaySound(SoundID.LiquidsWaterLava with {
                Volume = intoLava ? 0.3f : 0.22f,
                Pitch = intoLava ? -0.4f : 0.25f,
                MaxInstances = 4,
            }, Projectile.Center);

            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    -Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.7f)
                    * Main.rand.NextFloat(0.5f, 2.2f) + Main.rand.NextVector2Circular(1f, 1f),
                    80, default, Main.rand.NextFloat(0.9f, 1.4f));
                dust.noGravity = Main.rand.NextBool();
            }
            if (intoLava) {
                return;
            }

            //烟柱：烟尘上涌 + 黑屑上飘 + 火星，余韵在珠死后仍在
            for (int i = 0; i < 5; i++) {
                Dust smoke = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-5f, 5f), -2f),
                    DustID.Smoke, new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), -Main.rand.NextFloat(1f, 2.4f)),
                    100, default, Main.rand.NextFloat(1f, 1.6f));
                smoke.noGravity = true;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_AshreignFlake>(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-4f, 4f), -4f),
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1f)),
                    Ashreign.AshDark * 0.7f, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(Main.rand.Next(50, 90), -Main.rand.NextFloat(0.3f, 0.7f), 0.2f);
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_DefEmber>(Projectile.Center,
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1f, 2f)),
                    Ashreign.EmberWarm, Main.rand.NextFloat(0.32f, 0.5f))
                    ?.Configure(Main.rand.Next(30, 50), -0.004f, 0.99f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float opacity = 1f - Projectile.alpha / 255f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //熔芯透光（画在壳下，A=0 加色）：黑壳骑在炽亮体上的岩浆身份签名
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(glow, drawPos, null,
                new Color(255, 128, 38, 0) * (0.5f * opacity), 0f, glow.Size() * 0.5f,
                0.36f, SpriteEffects.None, 0);

            //速度拉伸热痕（运动各向异性）
            float speed = Projectile.velocity.Length();
            if (speed > 2f) {
                float stretch = MathHelper.Clamp(speed * 0.075f, 0.3f, 1.1f);
                Main.EntitySpriteDraw(glow, drawPos - Projectile.velocity * 0.9f, null,
                    new Color(255, 110, 36, 0) * (0.32f * opacity),
                    Projectile.velocity.ToRotation(), glow.Size() * 0.5f,
                    new Vector2(stretch, 0.14f), SpriteEffects.None, 0);
            }

            //同材质拖尾（灰壳残影）
            Color crust = Color.Lerp(lightColor, new Color(96, 58, 44), 0.5f) * opacity;
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, crust * (0.35f * t),
                    Projectile.rotation - i * 0.16f, origin, Projectile.scale * (0.5f + 0.3f * t),
                    SpriteEffects.None, 0);
            }

            //壳体本体：灰烬块贴图翻滚，热缘轻染
            Color body = Color.Lerp(crust, new Color(255, 132, 60), 0.22f);
            Main.EntitySpriteDraw(tex, drawPos, null, body, Projectile.rotation, origin,
                Projectile.scale * (0.85f + 0.25f * Seed), SpriteEffects.None, 0);
            return false;
        }
    }
}
