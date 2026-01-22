using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第十一门徒：西门/狂热者（狂热之力）
    /// 能力：在靠近敌人时增加攻击速度
    /// 象征物：锯
    /// </summary>
    internal class Zealot : BaseDisciple
    {
        public override int DiscipleIndex => 10;
        public override string DiscipleName => "西门";
        public override Color DiscipleColor => new(255, 100, 100); //狂热红
        public override int AbilityCooldownTime => 10;

        private bool isEnraged = false;

        //西门是狂热者，运动最快
        protected override float OrbitSpeedMultiplier => 1.6f;
        protected override float VerticalWaveAmplitude => 10f;
        protected override float HorizontalWaveAmplitude => 14f;
        protected override float MovementSmoothness => 0.22f;

        //3D轨道：西门在最上层
        protected override float OrbitTiltAngle => 0.45f;
        protected override float OrbitTiltDirection => MathHelper.Pi * 1.9f;
        protected override float OrbitHeightLayer => 0.8f;

        protected override void ExecuteAbility() {
            //检测附近是否有敌人
            NPC nearestEnemy = FindNearestEnemy(300f);
            isEnraged = nearestEnemy != null;

            if (isEnraged) {
                //狂热粒子
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch, Main.rand.NextVector2Circular(2f, 2f), 100, Color.Red, 1.5f);
                d.noGravity = true;
            }
            SetCooldown();
        }

        protected override void PassiveEffect() {
            //被动：增加攻击速度，靠近敌人时更强
            Owner.GetAttackSpeed(DamageClass.Generic) += isEnraged ? 0.2f : 0.05f;
        }
    }
}
