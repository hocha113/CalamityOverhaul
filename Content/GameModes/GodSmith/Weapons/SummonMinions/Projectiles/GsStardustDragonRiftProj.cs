using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 星穹裂隙：星尘龙的头锥凿穿现实撕开的一道口子。
    /// 三相 = 撕开 8 帧（无伤害）/ 星涌 26 帧（伤害窗，每约 9 帧一段星涌脉冲）/
    /// 弥合 12 帧（无伤害）
    /// </summary>
    internal class GsStardustDragonRiftProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.StarWrath;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private const int TearFrames = 8;
        private const int GushFrames = 26;
        private const int SealFrames = 12;
        private const int TotalFrames = TearFrames + GushFrames + SealFrames;
        /// <summary>判定方区边长</summary>
        private const float RiftSpan = 130f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool Gushing => Elapsed >= TearFrames && Elapsed < TearFrames + GushFrames;

        private bool Sealing => Elapsed >= TearFrames + GushFrames;

        /// <summary>裂口开度 0~1</summary>
        private float OpenT {
            get {
                if (Elapsed < TearFrames) {
                    float t = Elapsed / (float)TearFrames;
                    return t * t;
                }
                if (Sealing) {
                    return MathHelper.Clamp(Projectile.timeLeft / (float)SealFrames, 0f, 1f);
                }
                return 1f;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //星涌 26 帧内约 3 段脉冲
            Projectile.localNPCHitCooldown = 9;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (VaultUtils.isServer) {
                return;
            }
            if (Elapsed == 1) {
                SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.6f, Pitch = -0.4f },
                    Projectile.Center);
            }
            if (Elapsed == TearFrames) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = -0.2f },
                    Projectile.Center);
            }
        }

        /// <summary>只有星涌相结算伤害</summary>
        public override bool? CanDamage() => Gushing ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => Utils.CenteredRectangle(Projectile.Center, new Vector2(RiftSpan))
                .Intersects(targetHitbox);

        /// <summary>区域尺寸提示：原版星辉贴图按判定尺寸缩放一笔（随开度撕开与弥合）</summary>
        public override bool PreDraw(ref Color lightColor) {
            float open = OpenT;
            if (open <= 0.02f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * open,
                0f, tex.Size() / 2f, RiftSpan / tex.Width * open, SpriteEffects.None, 0);
            return false;
        }
    }
}
