using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles
{
    /// <summary>
    /// 哨兵族通用一次性爆发判定：owner 生成的真弹幕承载额外伤害。<br/>
    /// ai[0]=样式（0 交叉火力钉刺 / 1 高爆芯 / 2 火焰溅射环带）ai[1]=判定半径。<br/>
    /// 前 5 帧判定窗（每敌一次），其后只留范围提示；环带样式只打外带，不与原爆炸区重复结算
    /// </summary>
    internal class GsSentryBurstProj : ModProjectile
    {
        internal const int StyleCrossSpike = 0;
        internal const int StyleHighExplosive = 1;
        internal const int StyleFlameSplash = 2;

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.InfernoFriendlyBlast;

        public override string LocalizationCategory => "GodSmithSummonSentries";

        private ref float Style => ref Projectile.ai[0];
        private ref float Radius => ref Projectile.ai[1];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 20;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => Age <= 5f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dist = DistRectPoint(targetHitbox, Projectile.Center);
            if ((int)Style == StyleFlameSplash) {
                //环带：内圈让位原版爆炸判定
                return dist <= Radius && dist >= Radius * 0.55f;
            }
            return dist <= Radius;
        }

        internal static float DistRectPoint(Rectangle rect, Vector2 point) {
            float dx = MathHelper.Clamp(point.X, rect.Left, rect.Right) - point.X;
            float dy = MathHelper.Clamp(point.Y, rect.Top, rect.Bottom) - point.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        public override void AI() {
            Age++;
            if (Age != 1f || VaultUtils.isServer) {
                return;
            }
            //出生帧：按样式一次音效（各端都跑）
            SoundStyle sound = (int)Style switch {
                StyleCrossSpike => SoundID.Item62 with { Volume = 0.45f, Pitch = 0.5f, MaxInstances = 3 },
                StyleHighExplosive => SoundID.Item14 with { Volume = 0.6f, Pitch = 0.2f, MaxInstances = 3 },
                _ => SoundID.Item74 with { Volume = 0.4f, Pitch = 0.1f, MaxInstances = 3 },
            };
            SoundEngine.PlaySound(sound, Projectile.Center);
        }

        /// <summary>范围提示：原版贴图按判定半径缩放画一笔（lightColor 着色），随寿命渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            float fade = 1f - MathHelper.Clamp(Age / 18f, 0f, 1f);
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = MathHelper.Max(Radius, 8f) * 2f / tex.Width;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade, 0f,
                tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
