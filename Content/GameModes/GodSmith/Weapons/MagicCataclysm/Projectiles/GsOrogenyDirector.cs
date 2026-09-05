using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 岩柱：大地法杖左键 rider 在巨石碎裂处顶起。自地面顶升、驻留、沉降，判定矩形与可见柱高同源。
    /// ai[0]=相位计时（各端自增） ai[1]=满柱高 px
    /// </summary>
    internal class GsEarthPillarProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BoulderStaffOfEarth;

        public override string LocalizationCategory => "GodSmithMagicCataclysm";

        private const int RiseTicks = 12;
        private const int HoldTicks = 26;
        private const int SinkTicks = 16;
        private const int LifeTicks = RiseTicks + HoldTicks + SinkTicks;
        private const float HalfWidth = 23f;

        private ref float Timer => ref Projectile.ai[0];
        private float FullHeight => Projectile.ai[1] > 8f ? Projectile.ai[1] : 140f;

        /// <summary>当前柱高：顶升长满、沉降收回</summary>
        private float HeightNow {
            get {
                float grow = VaultUtils.EaseOutCubic(MathHelper.Clamp(Timer / RiseTicks, 0f, 1f));
                float sink = MathHelper.Clamp((LifeTicks - Timer) / (float)SinkTicks, 0f, 1f);
                return FullHeight * Math.Min(grow, VaultUtils.EaseOutQuad(sink));
            }
        }

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = LifeTicks + 6;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (Timer == 0f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
            }
            Timer++;
            if (Timer >= LifeTicks) {
                Projectile.Kill();
            }
        }

        /// <summary>沉降段无伤</summary>
        public override bool? CanDamage() => Timer < RiseTicks + HoldTicks ? null : false;

        /// <summary>柱体判定与可见高度同源：自地面向上 HeightNow 的矩形</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float h = HeightNow;
            if (h < 4f) {
                return false;
            }
            Rectangle pillar = new((int)(Projectile.Center.X - HalfWidth), (int)(Projectile.Center.Y - h),
                (int)(HalfWidth * 2f), (int)h);
            return pillar.Intersects(targetHitbox);
        }

        /// <summary>柱体提示：原版巨石贴图按判定矩形拉伸画一笔（lightColor 着色）</summary>
        public override bool PreDraw(ref Color lightColor) {
            float h = HeightNow;
            if (h < 4f) {
                return false;
            }
            Texture2D rock = TextureAssets.Projectile[Type].Value;
            Vector2 pos = Projectile.Center + new Vector2(0f, -h * 0.5f) - Main.screenPosition;
            Vector2 scale = new(HalfWidth * 2f / rock.Width, h / rock.Height);
            Main.EntitySpriteDraw(rock, pos, null, lightColor, 0f, rock.Size() * 0.5f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
