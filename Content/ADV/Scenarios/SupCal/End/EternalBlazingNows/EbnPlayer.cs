using CalamityMod.Dusts;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows
{
    internal class EbnPlayer : ModPlayer
    {
        /// <summary>
        /// 玩家是否达成永恒燃烧的现在结局
        /// </summary>
        public bool IsEbn => IsConquered(Player);

        public static bool IsConquered(Player player) => player.TryGetADVSave(out var save) && save.SupCalYharonQuestReward;

        #region 数据字段
        //视觉效果数据
        private readonly List<BrimstoneTrailData> brimstoneTrails = new();
        private readonly List<AuraParticleData> auraParticles = new();
        private int particleSpawnTimer = 0;
        private float auraPhase = 0f;
        private float pulsePhase = 0f;
        private float wingFlamePhase = 0f;

        //特殊能力冷却
        private int blinkCooldown = 0;
        private int blinkDuration = 0;
        private Vector2 blinkStartPos;
        private Vector2 blinkEndPos;

        //粒子数据类
        private class BrimstoneTrailData
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Life;
            public float MaxLife;
            public float Scale;
            public float Rotation;
            public Color Color;
            public float Alpha;
        }

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

        public override void ResetEffects() {
            if (!IsEbn) {
                //重置所有效果
                brimstoneTrails.Clear();
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
            UpdateBrimstoneTrails();
            UpdateAuraParticles();
            SpawnMovementParticles();

            //更新冷却
            if (blinkCooldown > 0) blinkCooldown--;
            if (blinkDuration > 0) {
                blinkDuration--;
                UpdateBlinkEffect();
            }

            //动态照明
            UpdateLighting();
        }

        public override void ModifyMaxStats(out StatModifier health, out StatModifier mana) {
            health = StatModifier.Default;
            mana = StatModifier.Default;

            if (IsEbn) {
                //血量上限提升到220万
                health.Base = 2200000;
                //法力上限大幅提升
                mana.Base = 2400;
            }
        }

        public override void PostUpdate() {
            if (!IsEbn) return;

            //生命值过低时自动触发护盾效果
            if (Player.statLife < Player.statLifeMax2 * 0.3f && Main.rand.NextBool(3)) {
                SpawnShieldParticles();
            }
        }

        #region 属性加成
        public override void PostUpdateEquips() {
            if (!IsEbn) return;

            //大幅属性提升
            Player.statDefense += 50;           //防御力+50
            Player.GetDamage(DamageClass.Generic) += 0.5f;  //全伤害+50%
            Player.GetCritChance(DamageClass.Generic) += 50f; //暴击率+50%
            Player.moveSpeed += 1.2f;            //移速+120%
            Player.maxRunSpeed += 5f;           //最大奔跑速度
            Player.maxFallSpeed += 1f;           //最大坠落速度
            Player.jumpSpeedBoost += 2f;         //跳跃力
            Player.wingTimeMax = (int)(Player.wingTimeMax * 3f); //飞行时间*3

            //特殊能力
            Player.noFallDmg = true;             //免疫坠落伤害
            Player.fireWalk = true;              //可在岩浆上行走
            Player.buffImmune[BuffID.OnFire] = true;
            Player.buffImmune[BuffID.OnFire3] = true;
            Player.buffImmune[BuffID.CursedInferno] = true;
            Player.buffImmune[BuffID.Burning] = true;

            //生命再生
            Player.lifeRegen += 100;             //每秒恢复50生命
            Player.lifeRegenTime = Math.Max(Player.lifeRegenTime, 300);

            //法力再生
            Player.manaRegen += 50;
            Player.manaCost *= 0.5f;             //法力消耗减半
        }
        #endregion

        #region 粒子效果更新
        private void UpdateBrimstoneTrails() {
            for (int i = brimstoneTrails.Count - 1; i >= 0; i--) {
                var trail = brimstoneTrails[i];
                trail.Life++;
                trail.Position += trail.Velocity;
                trail.Velocity *= 0.96f;
                trail.Velocity.Y -= 0.08f; //轻微上升
                trail.Rotation += 0.05f;

                //淡出
                if (trail.Life > trail.MaxLife * 0.6f) {
                    trail.Alpha = MathHelper.Lerp(1f, 0f, (trail.Life - trail.MaxLife * 0.6f) / (trail.MaxLife * 0.4f));
                }

                if (trail.Life >= trail.MaxLife) {
                    brimstoneTrails.RemoveAt(i);
                }
            }
        }

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

        private void SpawnMovementParticles() {
            particleSpawnTimer++;

            //行走粒子
            if (Math.Abs(Player.velocity.X) > 0.5f && Player.velocity.Y == 0f && particleSpawnTimer % 3 == 0) {
                SpawnFootstepParticles();
            }

            //飞行粒子
            if (Player.wingTime > 0 && particleSpawnTimer % 2 == 0) {
                SpawnWingParticles();
            }

            //冲刺粒子
            if (Player.dash > 0 || Math.Abs(Player.velocity.X) > 10f) {
                SpawnDashParticles();
            }
        }

        private void SpawnFootstepParticles() {
            Vector2 spawnPos = Player.Bottom + new Vector2(Main.rand.NextFloat(-8f, 8f), 0);

            //硫磺火尘埃
            for (int i = 0; i < 2; i++) {
                Dust d = Dust.NewDustPerfect(spawnPos, (int)CalamityDusts.Brimstone,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-3f, -1f)),
                    100, default, Main.rand.NextFloat(1.5f, 2.5f));
                d.noGravity = true;
                d.fadeIn = 1.2f;
            }

            //红色火焰
            if (Main.rand.NextBool(2)) {
                Dust fire = Dust.NewDustPerfect(spawnPos, DustID.Torch,
                    new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, 0f)),
                    100, Color.Red, 1.5f);
                fire.noGravity = true;
            }

            //自定义拖尾
            brimstoneTrails.Add(new BrimstoneTrailData {
                Position = spawnPos,
                Velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, -0.5f)),
                Life = 0,
                MaxLife = Main.rand.NextFloat(30f, 50f),
                Scale = Main.rand.NextFloat(1.2f, 2.0f),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                Color = Main.rand.Next(new Color[] {
                    new Color(255, 100, 50),
                    new Color(200, 60, 30),
                    new Color(255, 140, 70)
                }),
                Alpha = 1f
            });
        }

        private void SpawnWingParticles() {
            //翅膀位置
            Vector2 wingCenter = Player.Center + new Vector2(0, -10);
            float wingSpread = 25f;

            for (int i = 0; i < 2; i++) {
                float side = i == 0 ? -1 : 1;
                Vector2 wingPos = wingCenter + new Vector2(wingSpread * side, 0);

                //火焰羽毛效果
                Vector2 velocity = new Vector2(
                    Main.rand.NextFloat(-2f, 2f) * side,
                    Main.rand.NextFloat(1f, 3f)
                );

                Dust d = Dust.NewDustPerfect(wingPos, (int)CalamityDusts.Brimstone, velocity,
                    100, default, Main.rand.NextFloat(1.8f, 2.8f));
                d.noGravity = true;
                d.fadeIn = 1.5f;

                //金色火花
                if (Main.rand.NextBool(3)) {
                    Dust spark = Dust.NewDustPerfect(wingPos, DustID.Torch, velocity,
                        100, Color.OrangeRed, 1.2f);
                    spark.noGravity = true;
                }
            }
        }

        private void SpawnDashParticles() {
            Vector2 spawnPos = Player.Center + new Vector2(
                -Player.direction * Main.rand.NextFloat(15f, 25f),
                Main.rand.NextFloat(-15f, 15f)
            );

            //硫磺火拖尾
            Dust d = Dust.NewDustPerfect(spawnPos, (int)CalamityDusts.Brimstone,
                -Player.velocity * Main.rand.NextFloat(0.2f, 0.5f),
                100, default, Main.rand.NextFloat(2f, 3.5f));
            d.noGravity = true;
            d.fadeIn = 1.8f;

            //火焰残影
            if (Main.rand.NextBool(4)) {
                for (int i = 0; i < 3; i++) {
                    Dust fire = Dust.NewDustPerfect(spawnPos, DustID.Torch,
                        Main.rand.NextVector2Circular(3f, 3f),
                        100, Color.Red, 1.5f);
                    fire.noGravity = true;
                }
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

        private void SpawnShieldParticles() {
            //生命值低时的护盾粒子
            for (int i = 0; i < 8; i++) {
                float angle = MathHelper.TwoPi * i / 8f;
                Vector2 pos = Player.Center + angle.ToRotationVector2() * 60f;
                Vector2 vel = angle.ToRotationVector2() * 2f;

                Dust d = Dust.NewDustPerfect(pos, (int)CalamityDusts.Brimstone, vel,
                    100, default, 2.5f);
                d.noGravity = true;
                d.fadeIn = 2f;
            }
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

        #region 特殊能力
        private void UpdateBlinkEffect() {
            //闪现动画
            float progress = 1f - (blinkDuration / 15f);
            Player.position = Vector2.Lerp(blinkStartPos, blinkEndPos, CWRUtils.EaseOutCubic(progress));

            //闪现粒子
            if (Main.rand.NextBool(2)) {
                Vector2 pos = Player.Center + Main.rand.NextVector2Circular(30f, 30f);
                Dust d = Dust.NewDustPerfect(pos, (int)CalamityDusts.Brimstone,
                    Main.rand.NextVector2Circular(4f, 4f), 100, default, 2f);
                d.noGravity = true;
                d.fadeIn = 1.5f;
            }
        }

        //可以添加右键技能触发闪现
        public void TriggerBlink(Vector2 targetPosition) {
            if (blinkCooldown > 0) return;

            blinkStartPos = Player.position;
            blinkEndPos = targetPosition - new Vector2(Player.width / 2f, Player.height / 2f);
            blinkDuration = 15;
            blinkCooldown = 180; //3秒冷却

            //闪现音效
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.8f, Pitch = -0.3f }, Player.Center);

            //闪现爆发粒子
            for (int i = 0; i < 30; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(8f, 8f);
                Dust d = Dust.NewDustPerfect(Player.Center, (int)CalamityDusts.Brimstone, vel,
                    100, default, Main.rand.NextFloat(2f, 3.5f));
                d.noGravity = true;
                d.fadeIn = 2f;
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
                    Dust d = Dust.NewDustPerfect(Player.Center, (int)CalamityDusts.Brimstone,
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
                    Dust d = Dust.NewDustPerfect(target.Center, (int)CalamityDusts.Brimstone, vel,
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

        #region 绘制效果
        public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright) {
        }
        #endregion
    }
}
