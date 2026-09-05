using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 刀刃法杖「八卦剑影」：破防斩第 6 击落地时在目标身上闪过的匕首幻影。
    /// 真弹幕结算一段 40% 召唤伤害，owner 生成全端可见
    /// </summary>
    internal class GsBladeStaffPhantomProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Smolstar;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        private const int LifeFrames = 18;
        private const int DamageFrames = 5;

        private int Elapsed => LifeFrames - Projectile.timeLeft;

        public override void SetDefaults() {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Elapsed < DamageFrames ? null : false;

        public override void AI() {
            if (Elapsed == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = 0.5f }, Projectile.Center);
            }
        }
    }
}
