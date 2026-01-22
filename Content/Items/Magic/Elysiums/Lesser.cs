using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第九门徒：雅各/小雅各（奉献治愈）
    /// 能力：定期治愈玩家
    /// 象征物：棍棒
    /// </summary>
    internal class Lesser : BaseDisciple
    {
        public override int DiscipleIndex => 8;
        public override string DiscipleName => "雅各";
        public override Color DiscipleColor => new(100, 255, 100); //治愈绿
        public override int AbilityCooldownTime => 180;

        //小雅各是奉献者，运动柔和流畅
        protected override float OrbitSpeedMultiplier => 0.9f;
        protected override float VerticalWaveAmplitude => 16f;
        protected override float MovementSmoothness => 0.1f;

        //3D轨道：小雅各在下层
        protected override float OrbitTiltAngle => 0.22f;
        protected override float OrbitTiltDirection => MathHelper.Pi * 1.5f;
        protected override float OrbitHeightLayer => -0.5f;

        protected override void ExecuteAbility() {
            if (Owner.statLife < Owner.statLifeMax2) {
                int healAmount = 20 + Owner.statLifeMax2 / 20;
                Owner.Heal(healAmount);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f }, Owner.Center);
                //治愈光芒
                for (int i = 0; i < 15; i++) {
                    Dust d = Dust.NewDustPerfect(Owner.Center, DustID.GreenFairy, Main.rand.NextVector2Circular(4f, 4f), 100, default, 1.5f);
                    d.noGravity = true;
                }
                SetCooldown(150);
            }
        }

        protected override void PassiveEffect() {
            //被动：增加生命恢复
            Owner.lifeRegen += 2;
        }
    }
}
