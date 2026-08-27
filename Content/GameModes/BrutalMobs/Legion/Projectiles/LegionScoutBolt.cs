using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Legion.Projectiles
{
    /// <summary>
    /// 斥候短矢：瞄准线倒数结束放出的单发轻矢。比军团战矢更快更短命（短射程点射），
    /// 冷钢青拖尾与弓手齐射可辨区分；方向由预告体锁定，淡入完成才有杀伤（伤害窗=可见窗）。
    /// 独立于 <see cref="LegionVolleyArrow"/>，避免斥候占用弓手齐射的并发闸
    /// </summary>
    internal class LegionScoutBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowHostile;

        /// <summary>出膛初速（比军团战矢快，读作点射狙击）</summary>
        internal const float BoltSpeed = 14.5f;
        /// <summary>出膛淡入帧数，期间无判定</summary>
        private const int FadeInFrames = 7;
        /// <summary>短矢寿命（短射程口径：约 14.5×78≈1100px 后自散）</summary>
        private const int BoltLife = 78;

        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = BoltLife;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Age++;
            if (Age == 1f && !VaultUtils.isServer) {
                //出弦声锚定实体首帧：凡收到本矢的端都在自己的正确时刻听到
                SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.55f, Pitch = 0.25f }, Projectile.Center);
            }
            Projectile.alpha = (int)MathHelper.Lerp(220f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行冷光（低频）
            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                Dust glint = Dust.NewDustPerfect(Projectile.Center, DustID.SilverCoin,
                    -Projectile.velocity * 0.08f, 140, default, 0.7f);
                glint.noGravity = true;
            }
        }

        /// <summary>淡入完成才有杀伤（公平阀门）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.55f }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                Dust chip = Dust.NewDustPerfect(Projectile.Center,
                    DustID.WoodFurniture, -Projectile.velocity.SafeNormalize(Vector2.UnitY)
                        .RotatedByRandom(0.6f) * Main.rand.NextFloat(1f, 3f),
                    60, default, Main.rand.NextFloat(0.7f, 1.1f));
                chip.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            float opacity = 1f - Projectile.alpha / 255f;

            //同材质幽灵拖尾（横轴粗细=本体，契约量级）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, ghostPos, null,
                    new Color(140, 210, 255, 90) * (0.4f * t * opacity),
                    Projectile.rotation, orig, 0.85f * t + 0.1f, SpriteEffects.None, 0);
            }

            //本体：真 alpha 原版箭贴图（缩小读作短矢）+ 冷钢青微辉
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, Projectile.GetAlpha(lightColor),
                Projectile.rotation, orig, 0.85f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, new Color(120, 210, 255, 0) * (0.3f * opacity),
                Projectile.rotation, orig, 0.92f, SpriteEffects.None, 0);
            return false;
        }
    }
}
