using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 合流潮涌：龙卷与鲨的杀意合成一面横扫的浪墙。
    /// 三相 = 隆起 8 帧（原地蓄浪，无伤害）/ 横扫 32 帧（伤害窗）/ 消散 10 帧（滞停塌落，无伤害）
    /// </summary>
    internal class GsTempestSurgeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WaterBolt;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private const int SwellFrames = 8;
        private const int SweepFrames = 32;
        private const int FadeFrames = 10;
        private const int TotalFrames = SwellFrames + SweepFrames + FadeFrames;
        private const float WallWidth = 64f;
        private const float WallHeight = 104f;
        /// <summary>浪墙底沿相对中心的下沉量</summary>
        private const float WallFoot = 54f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool Sweeping => Elapsed >= SwellFrames && Elapsed < SwellFrames + SweepFrames;

        private bool Fading => Elapsed >= SwellFrames + SweepFrames;

        /// <summary>浪高进度：隆起段升起，消散段塌落</summary>
        private float HeightT {
            get {
                if (Elapsed < SwellFrames) {
                    float t = Elapsed / (float)SwellFrames;
                    return t * t;
                }
                if (Fading) {
                    return MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f);
                }
                return 1f;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 110;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //横扫全程每目标至多两段
            Projectile.localNPCHitCooldown = 25;
        }

        public override void AI() {
            //隆起期原地蓄浪（抵消位移但保住横扫速度），消散期滞停衰减
            if (Elapsed < SwellFrames) {
                Projectile.position -= Projectile.velocity;
            }
            else if (Fading) {
                Projectile.velocity *= 0.82f;
            }
            if (VaultUtils.isServer) {
                return;
            }
            if (Elapsed == 1) {
                SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.7f, Pitch = -0.3f },
                    Projectile.Center);
            }
            if (Elapsed == SwellFrames) {
                SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.5f, Pitch = -0.4f },
                    Projectile.Center);
            }
        }

        /// <summary>只有横扫相结算伤害</summary>
        public override bool? CanDamage() => Sweeping ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float h = WallHeight * HeightT;
            Rectangle wall = new((int)(Projectile.Center.X - WallWidth / 2f),
                (int)(Projectile.Center.Y + WallFoot - h), (int)WallWidth, (int)h);
            return wall.Intersects(targetHitbox);
        }

        /// <summary>区域尺寸提示：原版水弹贴图按浪墙矩形拉伸一笔（底沿锚定，随浪高隆起塌落）</summary>
        public override bool PreDraw(ref Color lightColor) {
            float h = HeightT;
            if (h <= 0.02f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 foot = Projectile.Center + new Vector2(0f, WallFoot) - Main.screenPosition;
            Main.EntitySpriteDraw(tex, foot, null, lightColor * h, 0f,
                new Vector2(tex.Width / 2f, tex.Height),
                new Vector2(WallWidth / tex.Width, WallHeight * h / tex.Height), SpriteEffects.None, 0);
            return false;
        }
    }
}
