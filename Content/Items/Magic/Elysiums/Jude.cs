using System;
using Terraria;
using Terraria.Audio;
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

        /// <summary>
        /// 奇迹类型枚举
        /// </summary>
        private enum MiracleType
        {
            Healing = 0,    //治愈奇迹
            Guardian = 1,   //守护奇迹
            Judgment = 2,   //审判奇迹
            Swift = 3,      //迅捷奇迹
            Mana = 4        //魔力奇迹
        }

        protected override void ExecuteAbility() {
            //随机触发一个奇迹效果
            MiracleType miracle = (MiracleType)Main.rand.Next(5);
            ExecuteJudgmentMiracle();
            switch (miracle) {
                case MiracleType.Healing:
                    ExecuteHealingMiracle();
                    break;
                case MiracleType.Guardian:
                    ExecuteGuardianMiracle();
                    break;
                case MiracleType.Judgment:
                    for (int i = 0; i < 13; i++) {
                        ExecuteJudgmentMiracle();
                    }
                    break;
                case MiracleType.Swift:
                    ExecuteSwiftMiracle();
                    break;
                case MiracleType.Mana:
                    ExecuteManaMiracle();
                    break;
            }

            //通用奇迹光芒（金色神圣光辉）
            SpawnMiracleGlow();
            SetCooldown(200);
        }

        #region 治愈奇迹
        /// <summary>
        /// 执行治愈奇迹 - 瞬间回复生命
        /// </summary>
        private void ExecuteHealingMiracle() {
            //治愈量根据玩家最大生命值计算
            int healAmount = Math.Max(50, Owner.statLifeMax2 / 8);
            Owner.Heal(healAmount);

            //生成治愈奇迹特效
            JudeMiracleEffects.SpawnHealingMiracleEffect(Owner);

            //显示奇迹名称
            JudeMiracleEffects.SpawnMiracleAura(Owner.Center, JudeMiracleEffects.HealingColor, "治愈奇迹");

            //给玩家添加短暂的生命恢复buff
            Owner.AddBuff(BuffID.RapidHealing, 180);
        }
        #endregion

        #region 守护奇迹
        /// <summary>
        /// 执行守护奇迹 - 短暂无敌
        /// </summary>
        private void ExecuteGuardianMiracle() {
            //延长无敌时间
            Owner.immuneTime = Math.Max(Owner.immuneTime, 90); //1.5秒无敌
            Owner.immuneNoBlink = false; //允许闪烁效果

            //添加防御buff
            Owner.AddBuff(BuffID.Ironskin, 300);
            Owner.AddBuff(BuffID.Endurance, 300);

            //生成守护奇迹特效
            JudeMiracleEffects.SpawnGuardianMiracleEffect(Owner);

            //显示奇迹名称
            JudeMiracleEffects.SpawnMiracleAura(Owner.Center, JudeMiracleEffects.GuardianColor, "守护奇迹");

            //播放守护音效
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f, Pitch = -0.2f }, Owner.Center);
        }
        #endregion

        #region 审判奇迹
        /// <summary>
        /// 执行审判奇迹 - 召唤神圣雷霆打击敌人
        /// </summary>
        private void ExecuteJudgmentMiracle() {
            //寻找最近的敌人
            NPC target = FindNearestEnemy(2600f);

            if (target != null) {
                //计算雷霆生成位置（目标上方天空）
                Vector2 spawnPos = target.Center - new Vector2(Main.rand.NextFloat(-50f, 50f), 1400f);

                //计算朝向目标的方向
                Vector2 direction = (target.Center - spawnPos).SafeNormalize(Vector2.UnitY);

                //生成审判闪电弹幕
                if (Projectile.IsOwnedByLocalPlayer()) {
                    int damage = 200 + Owner.statDefense; //伤害与玩家防御相关

                    Projectile lightning = Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        spawnPos,
                        direction * 20f,
                        ModContent.ProjectileType<JudgmentLightning>(),
                        damage,
                        8f,
                        Owner.whoAmI,
                        0f, //ai[0] = state
                        0f, //ai[1] = hited
                        target.whoAmI //ai[2] = target NPC index
                    );

                    lightning.DamageType = DamageClass.Magic;
                }

                //显示奇迹名称在目标位置
                JudeMiracleEffects.SpawnMiracleAura(target.Center, JudeMiracleEffects.JudgmentColor, "审判奇迹");

                //播放预兆音效
                SoundEngine.PlaySound(SoundID.Item122 with {
                    Volume = 0.5f,
                    Pitch = 0.5f
                }, target.Center);
            }
            else {
                //没有敌人时，在门徒位置生成一道警示闪电
                Vector2 spawnPos = Projectile.Center - new Vector2(0, 200f);
                Vector2 direction = Vector2.UnitY;

                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile lightning = Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        spawnPos,
                        direction * 15f,
                        ModContent.ProjectileType<JudgmentLightning>(),
                        0, //无敌人时不造成伤害
                        0f,
                        Owner.whoAmI,
                        0f, 0f, -1f
                    );
                }

                //显示提示
                CombatText.NewText(Projectile.Hitbox, JudeMiracleEffects.JudgmentColor, "审判待命", true);
            }

            //生成十字架光芒
            JudeMiracleEffects.SpawnCrossPattern(Projectile.Center, JudeMiracleEffects.JudgmentColor, 50f, 10);
        }
        #endregion

        #region 迅捷奇迹
        /// <summary>
        /// 执行迅捷奇迹 - 大幅提升移动速度
        /// </summary>
        private void ExecuteSwiftMiracle() {
            //速度提升
            float speedBoost = 1.8f;
            Owner.velocity *= speedBoost;

            //限制最大速度
            float maxSpeed = 25f;
            if (Owner.velocity.Length() > maxSpeed) {
                Owner.velocity = Owner.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }

            //添加速度相关buff
            Owner.AddBuff(BuffID.Swiftness, 300);
            Owner.AddBuff(BuffID.SugarRush, 180);

            //生成迅捷奇迹特效
            JudeMiracleEffects.SpawnSwiftMiracleEffect(Owner);

            //显示奇迹名称
            JudeMiracleEffects.SpawnMiracleAura(Owner.Center, JudeMiracleEffects.SwiftColor, "迅捷奇迹");
        }
        #endregion

        #region 魔力奇迹
        /// <summary>
        /// 执行魔力奇迹 - 恢复大量魔力
        /// </summary>
        private void ExecuteManaMiracle() {
            //魔力恢复量根据最大魔力计算
            int manaRestore = Math.Max(100, Owner.statManaMax2 / 3);
            Owner.statMana = Math.Min(Owner.statMana + manaRestore, Owner.statManaMax2);

            //添加魔力相关buff
            Owner.AddBuff(BuffID.ManaRegeneration, 300);
            Owner.AddBuff(BuffID.MagicPower, 300);

            //生成魔力奇迹特效
            JudeMiracleEffects.SpawnManaMiracleEffect(Owner);

            //显示奇迹名称
            JudeMiracleEffects.SpawnMiracleAura(Owner.Center, JudeMiracleEffects.ManaColor, "魔力奇迹");

            //显示恢复的魔力数值
            CombatText.NewText(Owner.Hitbox, JudeMiracleEffects.ManaColor, $"+{manaRestore} MP");
        }
        #endregion

        #region 通用效果
        /// <summary>
        /// 生成通用奇迹光芒
        /// </summary>
        private void SpawnMiracleGlow() {
            //金色神圣光芒
            for (int i = 0; i < 20; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 8f);
                Dust d = Dust.NewDustPerfect(
                    Owner.Center + Main.rand.NextVector2Circular(20, 20),
                    DustID.GoldFlame,
                    vel,
                    100,
                    Color.White,
                    Main.rand.NextFloat(1.5f, 2.5f)
                );
                d.noGravity = true;
            }

            //在门徒位置也生成光芒
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f);
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(15, 15),
                    DustID.GoldFlame,
                    vel,
                    100,
                    DiscipleColor,
                    Main.rand.NextFloat(1f, 1.8f)
                );
                d.noGravity = true;
            }
        }
        #endregion
    }
}
