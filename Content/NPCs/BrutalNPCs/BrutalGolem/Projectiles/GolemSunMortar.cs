using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalGolem.Projectiles
{
    /// <summary>太阳臼炮弹：弧线上抛，弧顶空爆成余烬雨；ai[0]=0臼炮/1余烬</summary>
    internal class GolemSunMortar : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
        }

        private bool IsEmber => Projectile.ai[0] == 1f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            //出膛声画：首帧各端本地（OnSpawn 不在远端客户端执行）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!Main.dedServ && !IsEmber) {
                    SoundEngine.PlaySound(SoundID.Item61 with { Pitch = -0.35f, Volume = 0.8f }, Projectile.Center);
                    for (int i = 0; i < 8; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare,
                            Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(2f, 2f), 0, default, 1.5f);
                        dust.noGravity = true;
                    }
                }
            }

            //重力弧线
            Projectile.velocity.Y += IsEmber ? 0.26f : 0.32f;
            if (Projectile.velocity.Y > 18f) {
                Projectile.velocity.Y = 18f;
            }
            Projectile.rotation += Projectile.velocity.X * 0.05f;
            Lighting.AddLight(Projectile.Center, new Vector3(0.65f, 0.45f, 0.14f) * (IsEmber ? 0.6f : 1f));

            //臼炮：弧顶空爆
            if (!IsEmber && Projectile.velocity.Y > -0.5f && Projectile.ai[1] == 0f) {
                Projectile.ai[1] = 1f;
                Burst();
            }

            if (!Main.dedServ && Main.rand.NextBool(IsEmber ? 5 : 2)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare,
                    -Projectile.velocity * 0.08f, 0, default, IsEmber ? 0.9f : 1.3f);
                dust.noGravity = true;
            }
        }

        /// <summary>空爆：扇形余烬雨（服务端生成，客户端仅表现）</summary>
        private void Burst() {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.35f, Volume = 0.7f }, Projectile.Center);
                for (int i = 0; i < 14; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare,
                        Main.rand.NextVector2Circular(4f, 3f), 0, default, 1.6f);
                    dust.noGravity = true;
                }
            }

            if (VaultUtils.isClient) {
                return;
            }
            int embers = Main.rand.Next(4, 6);
            int damage = (int)(Projectile.damage * 0.85f);
            for (int i = 0; i < embers; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-4.2f, 4.2f), Main.rand.NextFloat(-2.5f, 1.5f));
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    Type, damage, 0f, Main.myPlayer, 1f, 0f);
            }
            Projectile.Kill();
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.1f, Volume = 0.5f }, Projectile.Center);
            for (int i = 0; i < (IsEmber ? 6 : 10); i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.SolarFlare,
                    Main.rand.NextVector2Circular(2.6f, 2.6f), 0, default, 1.2f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarGlow01.Value;
            Texture2D rim = CWRAsset.Extra_98.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float scale = IsEmber ? 0.6f : 1f;
            //暗层用真alpha衬底：亮背景下的岩弹剪影
            Color rimDark = new(80, 28, 6);
            float stretch = 1f + MathHelper.Clamp(Projectile.velocity.Length() * 0.02f, 0f, 0.5f);

            //拖尾：暗缘衬底 + 热光（同材质缩淡）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(rim, oldPos, null, rimDark * (0.45f * fade * scale),
                    Projectile.rotation, rim.Size() / 2f, 0.5f * fade * scale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, oldPos, null, new Color(200, 90, 25, 0) * (0.35f * fade * scale),
                    0f, glow.Size() / 2f, 0.4f * fade * scale, SpriteEffects.None, 0);
            }

            //体：暗缘岩壳 → 热核 + 四芒
            Main.EntitySpriteDraw(rim, drawPos, null, rimDark * 0.9f,
                Projectile.rotation, rim.Size() / 2f,
                new Vector2(0.66f * scale * stretch, 0.6f * scale), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 150, 45, 0) * 0.9f,
                0f, glow.Size() / 2f, 0.55f * scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, new Color(255, 225, 150, 0),
                Projectile.rotation, star.Size() / 2f, 0.13f * scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
