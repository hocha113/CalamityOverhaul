using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.JungleHell.Projectiles
{
    /// <summary>
    /// 蜂群毒刺：齐射幕的蜂族弹体，直线飞行保持预览扇形不变形。ai[0]=档位。<br/>
    /// 淡入完成才有杀伤（伤害窗口=可见窗口）
    /// </summary>
    internal class JhStingerBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Stinger;

        /// <summary>出膛淡入帧数，未淡入无判定</summary>
        private const int FadeInFrames = 10;
        private const int PoisonTicksBase = 180;
        private const int PoisonTicksPerTier = 60;

        private int Tier => (int)Projectile.ai[0];
        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Age++;
            Projectile.alpha = (int)MathHelper.Lerp(220f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!Main.dedServ && Main.rand.NextBool(9)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch,
                    -Projectile.velocity * 0.1f, 160, default, 0.8f);
                dust.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, 0.14f, 0.2f, 0.04f);
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Poisoned, PoisonTicksBase + PoisonTicksPerTier * Tier);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.JungleGrass,
                    Main.rand.NextVector2Circular(2f, 2f), 100, default, Main.rand.NextFloat(0.7f, 1.1f));
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float opacity = 1f - Projectile.alpha / 255f;

            //同材质拖尾（横轴粗细与本体同贴图同比例）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, new Color(180, 220, 90, 60) * (0.35f * fade * opacity),
                    Projectile.rotation, origin, Projectile.scale * (0.7f + 0.3f * fade), SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor * opacity,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            //毒光敷料
            Main.EntitySpriteDraw(tex, drawPos, null, new Color(170, 255, 90, 0) * (0.35f * opacity),
                Projectile.rotation, origin, Projectile.scale * 1.1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
