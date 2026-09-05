using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 毒爆孢囊：黄蜂毒液共振的结算体。目标上方垂落 24 帧后爆裂（伤害窗 4 帧，半径 70），
    /// 之后转为毒雾残留：不再伤害，owner 端每 30 帧给圈内敌人补挂原版中毒。
    /// 相位完全由固定时间线驱动（timeLeft 各端同初值本地递减），零额外同步
    /// </summary>
    internal class GsVenomPodProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Beenade;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        private const int FallFrames = 24;
        private const int BurstFrames = 4;
        private const int MistFrames = 90;
        private const int TotalFrames = FallFrames + BurstFrames + MistFrames;
        private const float BurstRadius = 70f;
        private const float MistRadius = 80f;

        private int Elapsed => TotalFrames - Projectile.timeLeft;

        private bool InFall => Elapsed < FallFrames;

        private bool InBurst => Elapsed >= FallFrames && Elapsed < FallFrames + BurstFrames;

        public override void SetDefaults() {
            Projectile.width = 100;
            Projectile.height = 100;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalFrames;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (InFall) {
                //垂落相：缓降到位
                Projectile.velocity = new Vector2(0f,
                    3.4f * (1f - Elapsed / (float)FallFrames) + 0.6f);
                return;
            }
            Projectile.velocity = Vector2.Zero;

            //爆裂帧音效
            if (Elapsed == FallFrames && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCDeath1 with { Volume = 0.5f, Pitch = 0.4f },
                    Projectile.Center);
            }

            //毒雾相：owner 端每 30 帧给圈内敌人补挂中毒（AddBuff 骑原版 buff 同步）
            if (!InBurst && Projectile.IsOwnedByLocalPlayer() && Elapsed % 30 == 0) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.CanBeChasedBy() && npc.Center.Distance(Projectile.Center) <= MistRadius) {
                        npc.AddBuff(BuffID.Poisoned, 150);
                    }
                }
            }
        }

        /// <summary>伤害窗只在爆裂 4 帧</summary>
        public override bool? CanDamage() => InBurst ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= BurstRadius;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Poisoned, 240);

        /// <summary>区域尺寸提示：垂落期画孢囊原尺寸，爆裂后按毒雾半径撑开一笔并随余命渐隐</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = 1f;
            float fade = 1f;
            if (!InFall) {
                int sinceBurst = Elapsed - FallFrames;
                scale = MistRadius * 2f / tex.Width * MathHelper.Clamp(sinceBurst / 6f, 0.3f, 1f);
                fade = MathHelper.Clamp(Projectile.timeLeft / 24f, 0f, 1f);
            }
            if (fade <= 0.01f) {
                return false;
            }
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * fade,
                0f, tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
