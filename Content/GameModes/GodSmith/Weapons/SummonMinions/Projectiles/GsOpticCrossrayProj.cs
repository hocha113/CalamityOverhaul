using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 交叉视线：双瞳协议的结算体。机械双瞳的凝视在目标身上交汇成爆闪，
    /// 三相 = 汇聚 8 帧（无伤害）/ 爆闪 6 帧（伤害窗 + 咒焰引燃）/ 余像 14 帧（无伤害）
    /// </summary>
    internal class GsOpticCrossrayProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CursedFlameFriendly;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private const int GatherFrames = 8;
        private const int FlashFrames = 6;
        private const int FadeFrames = 14;
        private const int TotalFrames = GatherFrames + FlashFrames + FadeFrames;
        /// <summary>爆闪判定半径</summary>
        private const float BlastRadius = 62f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool InFlash => Elapsed >= GatherFrames && Elapsed < GatherFrames + FlashFrames;

        private bool Fading => Elapsed >= GatherFrames + FlashFrames;

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            //爆闪窗内每目标只结算一次
            Projectile.localNPCHitCooldown = TotalFrames;
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;
            if (VaultUtils.isServer) {
                return;
            }
            //汇聚相首帧：双瞳锁定音
            if (Elapsed == 1) {
                SoundEngine.PlaySound(SoundID.Item33 with { Volume = 0.45f, Pitch = 0.35f },
                    Projectile.Center);
            }
            //爆闪首帧：炸裂音
            if (Elapsed == GatherFrames) {
                SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.6f, Pitch = 0.1f },
                    Projectile.Center);
            }
        }

        /// <summary>只有爆闪窗结算伤害</summary>
        public override bool? CanDamage() => InFlash ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => Utils.CenteredRectangle(Projectile.Center, new Vector2(BlastRadius * 2f))
                .Intersects(targetHitbox);

        /// <summary>幽花之瞳的咒焰引燃（双瞳里的绿瞳职责）</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.CursedInferno, 150);

        /// <summary>区域尺寸提示：原版咒焰贴图按爆闪半径缩放一笔（汇聚相张开、余像相渐隐）</summary>
        public override bool PreDraw(ref Color lightColor) {
            float grow = MathHelper.Clamp(Elapsed / (float)GatherFrames, 0.2f, 1f);
            float fade = Fading
                ? MathHelper.Clamp(Projectile.timeLeft / (float)FadeFrames, 0f, 1f) : 1f;
            if (fade <= 0.01f) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade,
                0f, tex.Size() / 2f, BlastRadius * 2f / tex.Width * grow, SpriteEffects.None, 0);
            return false;
        }
    }
}
