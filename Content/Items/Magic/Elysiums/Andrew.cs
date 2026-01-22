using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第二门徒：圣安德鲁（渔网束缚）
    /// 能力：减速附近的敌人
    /// 象征物：X形十字架
    /// </summary>
    internal class Andrew : BaseDisciple
    {
        public override int DiscipleIndex => 1;
        public override string DiscipleName => "圣安德鲁";
        public override Color DiscipleColor => new(100, 149, 237); //矢车菊蓝
        public override int AbilityCooldownTime => 90;

        //安德鲁是渔夫，运动如波浪般起伏
        protected override float VerticalWaveAmplitude => 22f;
        protected override float HorizontalWaveAmplitude => 15f;

        protected override void ExecuteAbility() {
            bool hitAny = false;
            foreach (NPC npc in Main.npc) {
                if (npc.CanBeChasedBy() && Vector2.Distance(npc.Center, Projectile.Center) < 200f) {
                    npc.velocity *= 0.7f;
                    hitAny = true;
                    //网状粒子效果
                    for (int i = 0; i < 6; i++) {
                        Dust d = Dust.NewDustPerfect(npc.Center, DustID.Cloud, Main.rand.NextVector2Circular(3f, 3f), 150, Color.White, 1f);
                        d.noGravity = true;
                    }
                }
            }
            if (hitAny) {
                SetCooldown(60);
            }
        }
    }
}
