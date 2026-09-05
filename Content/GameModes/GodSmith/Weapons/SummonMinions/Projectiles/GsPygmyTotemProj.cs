using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 猎首战鼓图腾：矮人妖在集结旗点竖起的图腾桩。本体不伤害，
    /// 是猎首战鼓的光环载体（半径圈内自家仆从增伤，由方案侧查询判定）。
    /// 生命周期 = 破土 12 帧 / 战鼓循环（每 50 帧一记鼓点）/ 末段 12 帧沉土散场
    /// </summary>
    internal class GsPygmyTotemProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.PygmySpear;

        public override string LocalizationCategory => "GodSmithSummonMinionsB";

        /// <summary>战鼓光环半径（方案侧增伤查询用同一常量）</summary>
        internal const float AuraRadius = 190f;

        private const int TotemLife = 300;
        private const int RiseFrames = 12;
        private const int SinkFrames = 12;
        private const int DrumGap = 50;

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 86;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotemLife;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override void AI() {
            Life++;
            Projectile.velocity = Vector2.Zero;
            if (VaultUtils.isServer) {
                return;
            }
            //破土首帧：入土闷响
            if (Life == 1f) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.8f, Pitch = -0.3f },
                    Projectile.Center);
            }
            //鼓点：鼓声（各端按同一 Life 节拍本地播放）
            if (Life > RiseFrames && Projectile.timeLeft > SinkFrames
                && Life % DrumGap == 0f) {
                SoundEngine.PlaySound(SoundID.Item53 with { Volume = 0.55f, Pitch = -0.55f },
                    Projectile.Center);
            }
        }
    }
}
