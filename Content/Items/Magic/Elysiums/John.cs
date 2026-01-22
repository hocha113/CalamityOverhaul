using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第四门徒：圣约翰（启示录视野）
    /// 能力：标记范围内所有敌人的弱点
    /// 象征物：圣杯
    /// </summary>
    internal class John : BaseDisciple
    {
        public override int DiscipleIndex => 3;
        public override string DiscipleName => "圣约翰";
        public override Color DiscipleColor => new(200, 200, 255); //启示白蓝
        public override int AbilityCooldownTime => 180;

        //约翰是启示者，带有螺旋运动
        protected override bool UseSpiralMotion => true;
        protected override float VerticalWaveAmplitude => 10f;
        protected override float MovementSmoothness => 0.15f;

        //3D轨道：约翰在中上层
        protected override float OrbitTiltAngle => 0.3f;
        protected override float OrbitTiltDirection => MathHelper.Pi * 0.5f;
        protected override float OrbitHeightLayer => 0.4f;

        protected override void ExecuteAbility() {
            int markedCount = 0;
            //标记范围内所有敌人
            foreach (NPC npc in Main.npc) {
                if (npc.CanBeChasedBy() && Vector2.Distance(npc.Center, Owner.Center) < 600f) {
                    markedCount++;
                    //添加发光粒子标记
                    for (int i = 0; i < 4; i++) {
                        float angle = MathHelper.TwoPi * i / 4f;
                        Dust d = Dust.NewDustPerfect(npc.Center + angle.ToRotationVector2() * 30f, DustID.GoldFlame, Vector2.Zero, 100, default, 2f);
                        d.noGravity = true;
                        d.fadeIn = 1.5f;
                    }
                }
            }
            if (markedCount > 0) {
                SetCooldown(120);
            }
        }

        protected override void PassiveEffect() {
            //被动：增加玩家的暴击率
            Owner.GetCritChance(DamageClass.Generic) += 5;
        }
    }
}
