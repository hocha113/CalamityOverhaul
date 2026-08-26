using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Martians.Projectiles
{
    /// <summary>
    /// 标线打击光矢：沿已承诺的激光标线直飞，绝不转向。ai[0]=风味（配色与附加减益）。
    /// 出膛淡入完成才有判定（判定窗=可见窗）
    /// </summary>
    internal class MrtLaserBoltProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MartianTurretBolt;

        /// <summary>出膛淡入帧：判定门与透明度用同一时钟</summary>
        private const int MuzzleFadeFrames = 6;

        /// <summary>风味不透明弹体配色（与标线警示色同相，A&gt;0 保证有遮挡像素）</summary>
        private static readonly Color[] BodyColors = [
            new(110, 225, 255),
            new(255, 120, 205),
            new(130, 255, 150),
            new(200, 130, 255),
            new(255, 205, 110),
            new(255, 90, 90),
        ];

        private int Flavor => (int)Projectile.ai[0];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 255;
        }

        public override void AI() {
            if (Age == 0f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.4f, Pitch = 0.15f - Flavor * 0.05f, MaxInstances = 6 }, Projectile.Center);
            }
            Age++;

            Projectile.alpha = (int)MathHelper.Lerp(200f, 0f, MathHelper.Clamp(Age / (float)MuzzleFadeFrames, 0f, 1f));
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Color body = BodyColors[Flavor];
            Lighting.AddLight(Projectile.Center, body.ToVector3() * 0.32f);
            if (!VaultUtils.isServer && Main.rand.NextBool(6)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.MartianSaucerSpark,
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.6f, 0.6f), 0, default, 0.7f);
                dust.noGravity = true;
            }
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > MuzzleFadeFrames ? null : false;

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //命中方本机结算，减益原生同步；风味：扰乱者致混乱，军官致带电
            if (Flavor == 3) {
                target.AddBuff(BuffID.Confused, 60);
            }
            else if (Flavor == 4) {
                target.AddBuff(BuffID.Electrified, 60);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.MartianSaucerSpark,
                    Main.rand.NextVector2Circular(3f, 3f), 0, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.3f, Pitch = 0.5f, MaxInstances = 5 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float opacity = 1f - Projectile.alpha / 255f;
            Color body = Color.Lerp(lightColor, BodyColors[Flavor], 0.55f);
            Color glow = BodyColors[Flavor] with { A = 0 };

            //同材质拖尾（横轴 1:1 于弹体，缩放降透明重画旧位）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldDrawPos, null, glow * (0.3f * t * opacity), Projectile.rotation,
                    origin, Projectile.scale * (0.5f + 0.4f * t), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(tex, oldDrawPos, null, body * (0.4f * t * opacity), Projectile.rotation,
                    origin, Projectile.scale * (0.45f + 0.35f * t), SpriteEffects.None, 0);
            }

            //弹体：加色衬底 + 原版贴图实体层（A>0）
            Main.EntitySpriteDraw(tex, drawPos, null, glow * (0.55f * opacity), Projectile.rotation,
                origin, Projectile.scale * 1.25f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, body * opacity, Projectile.rotation,
                origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
