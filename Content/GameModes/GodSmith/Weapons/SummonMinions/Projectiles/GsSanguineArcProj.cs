using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 回航血弧：血蝠从猎物身上撕下的一弯血浆凝刃，向主人飞回，沿途割伤挡路者。
    /// 生命周期 = 撕出（保留冲势）/ 回航（软寻的主人）/ 归怀（贴近主人消散）
    /// </summary>
    internal class GsSanguineArcProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.VampireKnife;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Volume = 0.5f, Pitch = -0.25f },
                    Projectile.Center);
            }
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            //回航寻的：目标速度指向主人，前 12 帧保留撕出冲势后逐渐接管
            Vector2 want = (owner.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            float speed = MathHelper.Lerp(8.5f, 13f, MathHelper.Clamp(Life / 40f, 0f, 1f));
            float steer = Life < 12f ? 0.05f : 0.14f;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, want * speed, steer);
            Projectile.rotation = Projectile.velocity.ToRotation();

            //归怀：贴近主人即消散
            if (Life > 8f && Projectile.Center.Distance(owner.Center) < 26f) {
                Projectile.Kill();
                return;
            }
        }
    }
}
