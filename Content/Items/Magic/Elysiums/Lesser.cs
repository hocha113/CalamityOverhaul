using System;
using Terraria;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第九门徒：雅各/小雅各（奉献治愈）
    /// 能力：定期治愈玩家，释放神圣治愈光芒
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

        /// <summary>被动效果计时器</summary>
        private int passiveTimer = 0;

        /// <summary>治愈蓄能（低生命时积累）</summary>
        private float healingCharge = 0f;

        protected override void ExecuteAbility() {
            if (Owner.statLife < Owner.statLifeMax2) {
                //基础治愈量 + 基于最大生命的加成
                int baseHeal = 25;
                int bonusHeal = Owner.statLifeMax2 / 15;

                //如果玩家生命很低，治愈量增加
                float healthRatio = (float)Owner.statLife / Owner.statLifeMax2;
                if (healthRatio < 0.5f) {
                    bonusHeal += (int)((0.5f - healthRatio) * 50); //最多额外+25
                }

                int totalHeal = baseHeal + bonusHeal;
                Owner.Heal(totalHeal);

                //生成完整的神圣治愈效果
                LesserHealingEffects.SpawnFullHealingEffect(Owner, totalHeal, Projectile.Center);

                //显示治愈文字
                CombatText.NewText(Owner.Hitbox, new Color(100, 255, 150), $"神圣治愈 +{totalHeal}", true);

                //根据治愈量调整冷却时间
                int cooldown = Math.Max(120, 180 - totalHeal);
                SetCooldown(cooldown);

                //重置蓄能
                healingCharge = 0f;
            }
            else {
                //生命已满时，积累治愈蓄能
                healingCharge = Math.Min(healingCharge + 0.1f, 1f);
            }
        }

        protected override void PassiveEffect() {
            passiveTimer++;

            //被动：增加生命恢复
            Owner.lifeRegen += 3;

            //如果玩家生命低于50%，额外增加恢复
            if ((float)Owner.statLife / Owner.statLifeMax2 < 0.5f) {
                Owner.lifeRegen += 2;
            }

            //生成被动治愈光环效果
            if (!VaultUtils.isServer) {
                LesserHealingEffects.SpawnPassiveHealingAura(Owner.Center, Projectile.Center, passiveTimer);
            }

            //当玩家生命降到30%以下时，缩短冷却
            if ((float)Owner.statLife / Owner.statLifeMax2 < 0.3f && abilityCooldown > 60) {
                abilityCooldown -= 2; //加速冷却
            }
        }

        protected override Vector2 CalculateCustomOffset() {
            //当玩家受伤时，小雅各会靠近玩家
            float healthRatio = (float)Owner.statLife / Owner.statLifeMax2;
            if (healthRatio < 0.7f) {
                //生命越低，越靠近玩家
                float closerFactor = (0.7f - healthRatio) * 30f;
                Vector2 toPlayer = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                return toPlayer * closerFactor;
            }
            return Vector2.Zero;
        }
    }
}
