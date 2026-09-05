using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 血髓珠：吸血蛙吸髓协同的回血载体。从命中点飞向 owner 玩家，
    /// 真弹幕全端可见；回血只在 owner 本地结算（客户端写自己血量合法且自动同步），
    /// 远端只看到珠子被吸收
    /// </summary>
    internal class GsBloodSipProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.VampireHeal;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            //先散开一小段再咬向玩家，加速渐强
            if (Life > 8f) {
                Vector2 want = (owner.Center - Projectile.Center)
                    .SafeNormalize(Vector2.UnitY) * MathHelper.Clamp(4f + Life * 0.22f, 4f, 13f);
                float turn = MathHelper.Clamp((Life - 8f) / 20f, 0.06f, 0.2f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            //到手：owner 本地回血，各端本地消珠
            if (Projectile.Center.Distance(owner.Center) <= 26f) {
                if (Projectile.owner == Main.myPlayer) {
                    owner.Heal(1);
                }
                Projectile.Kill();
                return;
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item3 with { Volume = 0.3f, Pitch = 0.6f },
                Projectile.Center);
        }
    }
}
