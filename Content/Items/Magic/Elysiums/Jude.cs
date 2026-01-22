using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第十门徒：达泰（奇迹显现）
    /// 能力：随机触发各种正面奇迹效果
    /// 象征物：斧头
    /// </summary>
    internal class Jude : BaseDisciple
    {
        public override int DiscipleIndex => 9;
        public override string DiscipleName => "达泰";
        public override Color DiscipleColor => new(255, 200, 255); //奇迹粉
        public override int AbilityCooldownTime => 240;

        //达泰是奇迹者，运动神秘
        protected override bool UseSpiralMotion => true;
        protected override bool UsePulseMotion => true;
        protected override float VerticalWaveAmplitude => 12f;
        protected override float MovementSmoothness => 0.14f;

        //3D轨道：达泰在上层
        protected override float OrbitTiltAngle => 0.38f;
        protected override float OrbitTiltDirection => MathHelper.Pi * 1.7f;
        protected override float OrbitHeightLayer => 0.5f;

        protected override void ExecuteAbility() {
            //随机触发一个奇迹效果
            int miracle = Main.rand.Next(5);
            switch (miracle) {
                case 0: //瞬间回复
                    Owner.Heal(50);
                    CombatText.NewText(Owner.Hitbox, Color.Green, "治愈奇迹");
                    break;
                case 1: //短暂无敌
                    Owner.immuneTime = Math.Max(Owner.immuneTime, 60);
                    CombatText.NewText(Owner.Hitbox, Color.Gold, "守护奇迹");
                    break;
                case 2: //伤害爆发
                    NPC target = FindNearestEnemy(500f);
                    if (target != null) {
                        target.SimpleStrikeNPC(200, 0, true, 0, DamageClass.Magic);
                        CombatText.NewText(target.Hitbox, Color.Red, "审判奇迹");
                    }
                    break;
                case 3: //速度提升
                    Owner.velocity *= 1.5f;
                    CombatText.NewText(Owner.Hitbox, Color.Cyan, "迅捷奇迹");
                    break;
                case 4: //法力恢复
                    Owner.statMana = Math.Min(Owner.statMana + 100, Owner.statManaMax2);
                    CombatText.NewText(Owner.Hitbox, Color.Blue, "魔力奇迹");
                    break;
            }
            //奇迹光芒
            for (int i = 0; i < 30; i++) {
                Dust d = Dust.NewDustPerfect(Owner.Center, DustID.GoldFlame, Main.rand.NextVector2Circular(6f, 6f), 100, default, 2f);
                d.noGravity = true;
            }
            SetCooldown(200);
        }
    }
}
