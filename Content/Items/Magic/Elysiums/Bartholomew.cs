using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第六门徒：巴多罗买（真言揭示）
    /// 能力：降低敌人的防御
    /// 象征物：刀
    /// </summary>
    internal class Bartholomew : BaseDisciple
    {
        public override int DiscipleIndex => 5;
        public override string DiscipleName => "巴多罗买";
        public override Color DiscipleColor => new(200, 100, 255); //真言紫
        public override int AbilityCooldownTime => 150;

        //巴多罗买揭示真相，运动沉稳而深邃
        protected override float OrbitSpeedMultiplier => 0.9f;
        protected override float VerticalWaveAmplitude => 18f;
        protected override float HorizontalWaveAmplitude => 8f;

        protected override void ExecuteAbility() {
            NPC target = FindNearestEnemy(300f);
            if (target != null) {
                target.defense = Math.Max(0, target.defense - 10);
                CombatText.NewText(target.Hitbox, Color.Purple, "真言揭示");
                //紫色光芒效果
                for (int i = 0; i < 15; i++) {
                    Dust d = Dust.NewDustPerfect(target.Center, DustID.PurpleTorch, Main.rand.NextVector2Circular(5f, 5f), 100, default, 1.5f);
                    d.noGravity = true;
                }
                SetCooldown(120);
            }
        }
    }
}
