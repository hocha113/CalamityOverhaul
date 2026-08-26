using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EvilBiome.Projectiles
{
    /// <summary>
    /// 侵蚀孢核:瘴云的实体预告。出手即锁定落点直线漂移,全程无伤害,
    /// 寿命耗尽处绽放 <see cref="ErosionCloudProj"/>。
    /// ai[0]=风味 ai[1]=云缺口朝向 ai[2]=出生档位;damage 携带云伤害(本体永不敌对)
    /// </summary>
    internal class ErosionCloudSeed : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>漂移帧数 = 预告时长(契约要求 ≥30)</summary>
        public const int TravelFrames = 46;

        private int Flavor => (int)Projectile.ai[0];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TravelFrames;
            Projectile.netImportant = true;
        }

        /// <summary>纯预告体,永不造成伤害</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            if (Age == 0f) {
                Age = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.4f, Pitch = -0.35f, MaxInstances = 3 }, Projectile.Center);
                }
            }
            Age++;

            //预告即承诺:速度出手即定,不再重新瞄准
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    EvilBiomeFX.DustFor(Flavor), -Projectile.velocity * 0.2f, 140, default, 0.9f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, EvilBiomeFX.Bright(Flavor).ToVector3() * 0.25f);
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center, EvilBiomeFX.DustFor(Flavor),
                        Main.rand.NextVector2Circular(2.4f, 2.4f), 120, default, 1.2f);
                    dust.noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.35f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
            }
            if (VaultUtils.isClient) {
                return;
            }
            //绽放为瘴云,风味/缺口/档位与伤害原样交棒
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<ErosionCloudProj>(), Projectile.damage, 0f, Main.myPlayer,
                Projectile.ai[0], Projectile.ai[1], Projectile.ai[2]);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float pulse = 0.85f + 0.15f * MathHelper.Clamp((float)System.Math.Sin(Age * 0.35f), -1f, 1f);

            //旧位残迹(同材质拖尾,横轴 ≥0.5 倍体宽)
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                DrawGlob(tex, origin, oldDrawPos, t * 0.35f, 0.55f * t, pulse);
            }
            DrawGlob(tex, origin, pos, 1f, 1f, pulse);
            return false;
        }

        private void DrawGlob(Texture2D tex, Vector2 origin, Vector2 drawPos, float alpha, float scaleMul, float pulse) {
            //快成线、慢成珠的液体拉伸
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.06f, 0f, 1f);
            Vector2 scale = new Vector2(0.36f * (1f - stretch * 0.35f), 0.5f * (1f + stretch * 1.4f)) * scaleMul * pulse;
            Color dark = EvilBiomeFX.Deep(Flavor) * (0.92f * alpha);
            Color core = EvilBiomeFX.Bright(Flavor) with { A = 0 } * (0.8f * alpha);
            Main.EntitySpriteDraw(tex, drawPos, null, dark, Projectile.rotation, origin, scale * 1.18f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, core, Projectile.rotation, origin, scale * 0.8f, SpriteEffects.None, 0);
        }
    }
}
