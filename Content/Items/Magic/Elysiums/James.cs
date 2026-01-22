using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第三门徒：雅各布（雷霆审判）
    /// 能力：随机雷击附近的敌人
    /// 象征物：剑
    /// </summary>
    internal class James : BaseDisciple
    {
        public override int DiscipleIndex => 2;
        public override string DiscipleName => "雅各布";
        public override Color DiscipleColor => new(255, 255, 100); //雷霆黄
        public override int AbilityCooldownTime => 120;

        //雅各布是雷霆之子，运动迅捷
        protected override float OrbitSpeedMultiplier => 1.4f;
        protected override float MovementSmoothness => 0.2f;

        //3D轨道：雅各布在上层
        protected override float OrbitTiltAngle => 0.35f;
        protected override float OrbitTiltDirection => MathHelper.Pi * 0.1f;
        protected override float OrbitHeightLayer => 0.7f;

        protected override void ExecuteAbility() {
            NPC target = FindNearestEnemy(400f);
            if (target != null) {
                //生成闪电效果
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.8f }, target.Center);
                for (int i = 0; i < 20; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center, DustID.Electric, Main.rand.NextVector2Circular(8f, 8f), 100, default, 1.5f);
                    d.noGravity = true;
                }
                //造成伤害
                int damage = 50 + Owner.GetWeaponDamage(Owner.HeldItem) / 2;
                target.SimpleStrikeNPC(damage, 0, false, 0, DamageClass.Magic);
                SetCooldown(90);
            }
        }
    }
}
