using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows
{
    internal class EbnPlayer : ModPlayer
    {
        /// <summary>
        /// 玩家是否达成永恒燃烧的现在结局
        /// </summary>
        public bool IsEbn => OnEbn(Player);

        #region 数据字段
        private readonly List<AuraParticleData> auraParticles = [];
        private float auraPhase = 0f;
        private float pulsePhase = 0f;
        private float wingFlamePhase = 0f;

        private class AuraParticleData
        {
            public Vector2 Offset;
            public float Angle;
            public float Distance;
            public float Life;
            public float MaxLife;
            public float Scale;
            public float RotationSpeed;
            public Color Color;
        }
        #endregion

        public static bool OnEbn(Player player) => player.TryGetADVSave(out var save) && save.EternalBlazingNow;
        public static bool IsConquered(Player player) => player.TryGetADVSave(out var save) && save.SupCalYharonQuestReward;

        public override void ResetEffects() {
            if (!IsEbn) {
                //重置所有效果
                auraParticles.Clear();
            }
        }

        public override void PostUpdateMiscEffects() {
            if (!IsEbn) return;

            //更新动画相位
            auraPhase += 0.04f;
            pulsePhase += 0.06f;
            wingFlamePhase += 0.08f;

            if (auraPhase > MathHelper.TwoPi) auraPhase -= MathHelper.TwoPi;
            if (pulsePhase > MathHelper.TwoPi) pulsePhase -= MathHelper.TwoPi;
            if (wingFlamePhase > MathHelper.TwoPi) wingFlamePhase -= MathHelper.TwoPi;

            //更新粒子
            UpdateAuraParticles();

            //动态照明
            UpdateLighting();
        }

        public override void ModifyMaxStats(out StatModifier health, out StatModifier mana) {
            health = StatModifier.Default;
            mana = StatModifier.Default;

            if (IsEbn) {
                //血量上限提升2200
                health.Base = 2200;
                //法力上限大幅提升
                mana.Base = 2400;
            }
        }

        #region 属性加成
        public override void PostUpdateEquips() {
            if (!IsEbn) return;

            //大幅属性提升
            Player.statDefense += 50;           //防御力+50
            Player.GetDamage(DamageClass.Generic) += 0.5f;  //全伤害+50%
            Player.GetCritChance(DamageClass.Generic) += 50f; //暴击率+50%
            Player.moveSpeed += 0.2f;            //移速+20%
            Player.maxRunSpeed += 2f;           //最大奔跑速度
            Player.maxFallSpeed += 1f;           //最大坠落速度
            Player.jumpSpeedBoost += 1f;         //跳跃力
            Player.wingTimeMax = (int)(Player.wingTimeMax * 3f); //飞行时间*3

            //特殊能力
            Player.noFallDmg = true;             //免疫坠落伤害
            Player.fireWalk = true;              //可在岩浆上行走
            Player.buffImmune[BuffID.OnFire] = true;
            Player.buffImmune[BuffID.OnFire3] = true;
            Player.buffImmune[BuffID.CursedInferno] = true;
            Player.buffImmune[BuffID.Burning] = true;

            //法力再生
            Player.manaRegen += 50;
            Player.manaCost *= 0.5f;             //法力消耗减半
        }
        #endregion

        #region 粒子效果更新
        private void UpdateAuraParticles() {
            for (int i = auraParticles.Count - 1; i >= 0; i--) {
                var particle = auraParticles[i];
                particle.Life++;
                particle.Angle += particle.RotationSpeed;
                particle.Distance += (float)Math.Sin(particle.Life * 0.1f) * 0.5f;

                if (particle.Life >= particle.MaxLife) {
                    auraParticles.RemoveAt(i);
                }
            }

            //持续生成光环粒子
            if (Main.rand.NextBool(5)) {
                SpawnAuraParticle();
            }
        }

        private void SpawnAuraParticle() {
            auraParticles.Add(new AuraParticleData {
                Offset = Vector2.Zero,
                Angle = Main.rand.NextFloat(MathHelper.TwoPi),
                Distance = Main.rand.NextFloat(40f, 80f),
                Life = 0,
                MaxLife = Main.rand.NextFloat(80f, 120f),
                Scale = Main.rand.NextFloat(0.8f, 1.5f),
                RotationSpeed = Main.rand.NextFloat(-0.05f, 0.05f),
                Color = Main.rand.Next([
                    new Color(255, 120, 60),
                    new Color(255, 80, 40),
                    new Color(200, 50, 30)
                ])
            });
        }

        private void UpdateLighting() {
            //动态照明（硫磺火风格）
            float pulse = (float)Math.Sin(pulsePhase * 2f) * 0.3f + 0.7f;
            float lightIntensity = 2.5f * pulse;

            Lighting.AddLight(Player.Center,
                2.0f * lightIntensity,  //红色分量
                0.6f * lightIntensity,  //绿色分量
                0.3f * lightIntensity); //蓝色分量（橙红色）

            //翅膀位置额外照明
            if (Player.wingTime > 0) {
                Lighting.AddLight(Player.Center + new Vector2(-25f, -10f),
                    1.5f * pulse, 0.5f * pulse, 0.2f * pulse);
                Lighting.AddLight(Player.Center + new Vector2(25f, -10f),
                    1.5f * pulse, 0.5f * pulse, 0.2f * pulse);
            }
        }
        #endregion

        #region 伤害修改
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (!IsEbn) return;

            //额外暴击伤害
            if (modifiers.CritDamage.Multiplicative > 0) {
                modifiers.CritDamage *= 2.5f; //暴击伤害倍率提升
            }

            //对Boss额外伤害
            if (target.boss) {
                modifiers.FinalDamage *= 1.5f;
            }
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            if (!IsEbn) return;

            //大幅减伤
            modifiers.FinalDamage *= 0.3f; //承受伤害减少70%

            //有概率完全闪避
            if (Main.rand.NextBool(5)) {
                modifiers.FinalDamage *= 0f;
                //闪避特效
                for (int i = 0; i < 15; i++) {
                    Dust d = Dust.NewDustPerfect(Player.Center, CWRID.Dust_Brimstone,
                        Main.rand.NextVector2Circular(6f, 6f), 100, default, 2f);
                    d.noGravity = true;
                }
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = 0.5f }, Player.Center);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!IsEbn) return;

            //击中效果
            if (Main.rand.NextBool(3)) {
                //硫磺火爆炸
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                    Dust d = Dust.NewDustPerfect(target.Center, CWRID.Dust_Brimstone, vel,
                        100, default, 1.5f);
                    d.noGravity = true;
                }
            }

            //吸血效果（伤害的5%）
            int healAmount = Math.Max(1, damageDone / 20);
            if (Player.statLife < Player.statLifeMax2) {
                Player.statLife = Math.Min(Player.statLife + healAmount, Player.statLifeMax2);
            }
        }
        #endregion
    }
}
