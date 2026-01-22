using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第十二门徒：犹大（背叛契约）
    /// 能力：提供最强增益，但会在玩家血量低于30%时背叛并斩杀玩家
    /// 象征物：绳索（上吊自尽）
    /// 危险：收集12门徒会获得巨大增益，但犹大的背叛是致命的
    /// </summary>
    internal class JudasIscariot : BaseDisciple
    {
        public override int DiscipleIndex => 11;
        public override string DiscipleName => "犹大";
        public override Color DiscipleColor => new(80, 80, 80); //背叛灰黑
        public override int AbilityCooldownTime => 30;

        private int ominousTimer = 0;

        //犹大是背叛者，运动阴森但流畅
        protected override float OrbitSpeedMultiplier => 0.75f;
        protected override bool UsePulseMotion => true;
        protected override float VerticalWaveAmplitude => 20f;
        protected override float MovementSmoothness => 0.08f;

        //3D轨道：犹大在最下层
        protected override float OrbitTiltAngle => 0.5f;
        protected override float OrbitTiltDirection => MathHelper.Pi * 0.6f;
        protected override float OrbitHeightLayer => -0.8f;

        protected override void ExecuteAbility() {
            //产生阴暗的视觉效果
            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame, Main.rand.NextVector2Circular(2f, 2f), 100, default, 1.2f);
            d.noGravity = true;

            //偶尔显示不祥的文字
            ominousTimer++;
            if (ominousTimer >= 600 && Main.rand.NextBool(3)) {
                string[] ominousTexts = ["三十银币...", "背叛...", "亲吻...", "绞刑架..."];
                CombatText.NewText(Projectile.Hitbox, Color.DarkRed, Main.rand.Next(ominousTexts));
                ominousTimer = 0;
            }
            SetCooldown();
        }

        protected override void PassiveEffect() {
            //被动：提供强大的全面增益(作为12门徒的代价)
            Owner.GetDamage(DamageClass.Generic) += 0.15f;
            Owner.GetCritChance(DamageClass.Generic) += 10;
            Owner.statDefense += 10;
            Owner.lifeRegen += 3;

            //注意：犹大的背叛机制在ElysiumPlayer中实现
        }

        protected override void OnDiscipleDeath() {
            //犹大死亡时的特殊效果：三十银币散落
            for (int i = 0; i < 30; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.SilverCoin, Main.rand.NextVector2Circular(8f, 8f), 100, default, 1.5f);
                d.noGravity = false; //银币会下落
            }
        }
    }
}
