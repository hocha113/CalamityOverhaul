using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 融胶爆：史莱姆协同震波。同一目标短窗内被两只不同史莱姆命中时由 owner 生成，
    /// 半径 60 的一次性凝胶爆（伤害窗前 6 帧）
    /// </summary>
    internal class GsGelBurstProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SlimeGun;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        private const int BurstLife = 14;
        private const float BurstRadius = 60f;

        private ref float Life => ref Projectile.localAI[0];

        /// <summary>当前判定半径（前 4 帧张开）</summary>
        private float Radius => BurstRadius * MathHelper.Clamp(Life / 4f, 0.4f, 1f);

        public override void SetDefaults() {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = BurstLife;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //一次爆每目标只结算一次
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;
            //出手音放 AI 首帧：OnSpawn 只在生成端跑，远端听不到
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.6f, Pitch = 0.15f },
                    Projectile.Center);
            }
        }

        /// <summary>伤害窗只开前 6 帧</summary>
        public override bool? CanDamage() => Life <= 6f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Slimed, 150);

        /// <summary>区域尺寸提示：原版贴图按判定半径缩放一笔</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                0f, tex.Size() / 2f, Radius * 2f / tex.Width, SpriteEffects.None, 0);
            return false;
        }
    }
}
