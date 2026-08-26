using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Martians.Projectiles
{
    /// <summary>
    /// Scutlix 骑手蓄力主炮弹：蓄能标线（骑手风味）走完后沿承诺方向发射的重弹。
    /// 慢速大体积，暗缘衬底+原版弹体核+白热心三层（实体感配方），命中挂带电
    /// </summary>
    internal class MrtScutlixHeavyBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MartianTurretBolt;

        /// <summary>出膛淡入帧：判定门与透明度用同一时钟</summary>
        private const int MuzzleFadeFrames = 6;

        private static readonly Color ShellRed = new(255, 90, 90);
        private static readonly Color DarkRim = new(96, 28, 40);

        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 9;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 360;
            Projectile.alpha = 255;
            Projectile.scale = 1.3f;
        }

        public override void AI() {
            if (Age == 0f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.8f, Pitch = -0.45f, MaxInstances = 4 }, Projectile.Center);
            }
            Age++;

            Projectile.alpha = (int)MathHelper.Lerp(200f, 0f, MathHelper.Clamp(Age / (float)MuzzleFadeFrames, 0f, 1f));
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, ShellRed.ToVector3() * 0.5f);
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    DustID.MartianSaucerSpark, -Projectile.velocity * 0.15f, 0, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > MuzzleFadeFrames ? null : false;

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Electrified, 90);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.MartianSaucerSpark,
                    Main.rand.NextVector2Circular(4.5f, 4.5f), 0, default, Main.rand.NextFloat(1f, 1.6f));
                dust.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 4 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Texture2D rim = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 rimOrigin = rim.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float opacity = 1f - Projectile.alpha / 255f;
            Color body = Color.Lerp(lightColor, ShellRed, 0.6f);
            Color glow = ShellRed with { A = 0 };

            //同材质拖尾：暗缘+弹核成对重画（横轴 ≥0.5 弹体）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(rim, oldDrawPos, null, DarkRim * (0.5f * t * opacity), Projectile.rotation,
                    rimOrigin, new Vector2(0.2f, 0.3f) * Projectile.scale * (0.6f + 0.4f * t), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, oldDrawPos, null, body * (0.42f * t * opacity), Projectile.rotation,
                    origin, Projectile.scale * (0.55f + 0.4f * t), SpriteEffects.None, 0);
            }

            //暗缘衬底（真 alpha，亮背景下的轮廓保险）→ 加色光晕 → 弹体核 → 白热心
            Main.EntitySpriteDraw(rim, drawPos, null, DarkRim * (0.85f * opacity), Projectile.rotation,
                rimOrigin, new Vector2(0.24f, 0.36f) * Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, glow * (0.6f * opacity), Projectile.rotation,
                origin, Projectile.scale * 1.35f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, body * opacity, Projectile.rotation,
                origin, Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, new Color(255, 240, 230, 40) * (0.7f * opacity), Projectile.rotation,
                origin, Projectile.scale * 0.5f, SpriteEffects.None, 0);
            return false;
        }
    }
}
