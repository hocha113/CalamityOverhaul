using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ShieldGenerators
{
    /// <summary>
    /// 护盾发生器TP:范围光环,给半径内玩家挂吸收护盾buff。
    /// 篝火模型:逻辑各端同跑,每个端为它模拟的所有玩家本地挂buff,零同步;
    /// 吸收结算完全在受击玩家自己的客户端(见 <see cref="ShieldGeneratorPlayer"/>)。
    /// 耗电按范围内玩家数计费
    /// </summary>
    internal class ShieldGeneratorTP : BaseTurretTP
    {
        public override int TargetTileID => ModContent.TileType<ShieldGeneratorTile>();
        public override int TargetItem => ModContent.ItemType<ShieldGenerator>();
        public override float MaxUEValue => 800;
        /// <summary>光环塔:逻辑各端同跑,buff本地挂(镜像药剂信标/宁静塔)</summary>
        public override bool SimulateOnAllEndpoints => true;

        /// <summary>光环半径(像素)</summary>
        internal const float AuraRadius = 800f;
        /// <summary>每名受庇护玩家每帧耗电</summary>
        internal const float ConsumePerPlayerTick = 0.5f;
        //buff续杯节奏与单次时长:短时长反复续,离场即自然消退
        private const int ApplyInterval = 30;
        private const int BuffDuration = 120;

        /// <summary>本 tick 力场是否实际运转(开启且有电且范围内有玩家)</summary>
        internal bool WorkingActive { get; private set; }
        internal float GlowIntensity;

        //---- 能量膜视觉状态:纯客户端表现,不参与判定,吸收判定始终走buff+护盾池 ----
        /// <summary>膜显示半径,欠阻尼弹簧跟随光环半径,扩张末端自带过冲回稳</summary>
        internal float DomeVisualRadius { get; private set; }
        /// <summary>膜总体强度包络 0~1</summary>
        internal float DomeVisualIntensity { get; private set; }
        /// <summary>半径变化强调量 0~1,喂给着色器的扩张/塌缩前沿</summary>
        internal float DomeExpandGlow { get; private set; }
        /// <summary>电力紧张度 0~1:剩余电量撑不过数秒时膜分段熄灭+闪烁</summary>
        internal float DomeStress { get; private set; }
        private float domeRadiusVel;
        private bool oldWorking;
        private int lastPlayerCount;
        private int fallbackDustTimer;

        private int applyTimer;
        private int textIdleTime;
        private int ambienceTimer;

        public override void SetBattery() {
            IdleDistance = 4000;//玩家远离后停止运行
            DrawExtendMode = 1650;//膜最大半径800×射程模块(1.25²=1250)+quad余量,塔出屏后膜仍需绘制
        }

        /// <summary>模块生效光环半径(射程模块作用点)</summary>
        private float EffectiveAuraRadius => AuraRadius * ModuleRack.TurretRangeMult;

        protected override void UpdateTurret() {
            if (textIdleTime > 0) {
                textIdleTime--;
            }

            bool running = false;
            int count = 0;
            if (AttackPattern) {
                //数一遍范围内活玩家,耗电按人头计费
                float auraRadius = EffectiveAuraRadius;
                float radiusSQ = auraRadius * auraRadius;
                foreach (Player player in Main.ActivePlayers) {
                    if (!player.dead && player.Center.DistanceSQ(CenterInWorld) <= radiusSQ) {
                        count++;
                    }
                }

                if (count > 0) {
                    float cost = ConsumePerPlayerTick * count * ModuleRack.TurretEnergyMult;
                    if (MachineData.UEvalue >= cost) {
                        MachineData.UEvalue -= cost;
                        running = true;
                        ApplyAura(radiusSQ);
                    }
                    else if (textIdleTime <= 0) {
                        //并行阶段CombatText生成延迟到主线程执行(串行阶段立即执行)
                        Defer(() => CombatText.NewText(HitBox, ShieldGenerator.Tint, ShieldGenerator.NoEnergyText.Value));
                        textIdleTime = 300;
                    }
                }
            }

            WorkingActive = running;
            lastPlayerCount = count;
            GlowIntensity = running
                ? Math.Min(1f, GlowIntensity + 0.03f)
                : Math.Max(0f, GlowIntensity - 0.03f);

            UpdateDomeVisual();

            //运转氛围:塔顶缓慢上飘的护盾微光
            if (running && !VaultUtils.isServer && ++ambienceTimer >= 20) {
                ambienceTimer = 0;
                Vector2 spawnPos = PosInWorld + new Vector2(Rand.Next(Width), Rand.Next(14));
                Defer(() => {
                    Dust dust = Dust.NewDustPerfect(spawnPos, DustID.PurpleTorch, new Vector2(0, -0.7f), 130, default, 0.9f);
                    dust.noGravity = true;
                    dust.fadeIn = 0.5f;
                });
            }
        }

        /// <summary>
        /// 能量膜包络:欠阻尼弹簧半径(镜像特斯拉护卫环)+电力紧张度+塌缩碎膜边沿。
        /// 逻辑各端同跑(篝火模型),包络在每个端各自推进,无需同步
        /// </summary>
        private void UpdateDomeVisual() {
            float target = WorkingActive ? EffectiveAuraRadius : 0f;
            float stiffness = WorkingActive ? 0.085f : 0.20f;
            float damping = WorkingActive ? 0.85f : 0.66f;
            domeRadiusVel = domeRadiusVel * damping + (target - DomeVisualRadius) * stiffness;
            DomeVisualRadius += domeRadiusVel;
            if (DomeVisualRadius < 0f) {
                DomeVisualRadius = 0f;
                domeRadiusVel = 0f;
            }

            DomeVisualIntensity = MathHelper.Lerp(DomeVisualIntensity, WorkingActive ? 1f : 0f, WorkingActive ? 0.10f : 0.09f);
            if (!WorkingActive && DomeVisualIntensity < 0.015f) {
                DomeVisualIntensity = 0f;
            }

            DomeExpandGlow = MathHelper.Clamp(Math.Abs(domeRadiusVel) * 0.10f, 0f, 1f);

            //电力紧张:按"还能撑几秒"折算,只剩4秒进入警戒闪烁
            if (WorkingActive) {
                float drainPerSecond = ConsumePerPlayerTick * 60f * Math.Max(1, lastPlayerCount) * ModuleRack.TurretEnergyMult;
                float secondsLeft = MachineData.UEvalue / Math.Max(drainPerSecond, 0.01f);
                DomeStress = MathHelper.Clamp(1f - secondsLeft / 4f, 0f, 1f);
            }
            else {
                DomeStress = 0f;
            }

            //塌缩边沿:附近仍有玩家才演碎膜(玩家全走开的静默停机不打扰)
            if (oldWorking && !WorkingActive && DomeVisualRadius > 60f && !VaultUtils.isServer && AnyPlayerNearDome()) {
                Vector2 center = CenterInWorld;
                float burstRadius = DomeVisualRadius;
                Defer(() => ShieldDomeFX.SpawnCollapseBurst(center, burstRadius));
            }
            oldWorking = WorkingActive;

            if (!WorkingActive || VaultUtils.isServer || DomeVisualRadius < 40f) {
                return;
            }

            //着色器缺失的回退粒子环(镜像特斯拉 SpawnGuardEffect)
            if (EffectLoader.ShieldDome?.Value == null && ++fallbackDustTimer >= 10) {
                fallbackDustTimer = 0;
                Defer(() => {
                    for (int i = 0; i < 8; i++) {
                        Vector2 pos = CenterInWorld + VaultUtils.RandVr(DomeVisualRadius - 2, DomeVisualRadius + 2);
                        Dust dust = Dust.NewDustPerfect(pos, DustID.PurpleTorch, Vector2.Zero, 140, default, 0.9f);
                        dust.noGravity = true;
                    }
                });
            }

            //膜缘取样点打光,力场照亮场地(弱于特斯拉,膜是防御不是武器)
            Defer(() => {
                Vector3 lightColor = ShieldGenerator.Tint.ToVector3() * 0.20f * DomeVisualIntensity;
                for (int i = 0; i < 8; i++) {
                    Vector2 pos = CenterInWorld + (MathHelper.TwoPi * i / 8f).ToRotationVector2() * DomeVisualRadius;
                    Lighting.AddLight(pos, lightColor);
                }
            });
        }

        /// <summary>塌缩演出门:膜半径+300 内是否有活玩家</summary>
        private bool AnyPlayerNearDome() {
            float range = DomeVisualRadius + 300f;
            float rangeSQ = range * range;
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && player.Center.DistanceSQ(CenterInWorld) <= rangeSQ) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>篝火模型:每个端为它模拟的所有玩家续短时长buff,无需网络包</summary>
        private void ApplyAura(float radiusSQ) {
            if (++applyTimer < ApplyInterval) {
                return;
            }
            applyTimer = 0;

            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.Center.DistanceSQ(CenterInWorld) > radiusSQ) {
                    continue;
                }
                int whoAmI = player.whoAmI;
                //并行阶段buff写入延迟到主线程执行(串行阶段立即执行)
                Defer(() => {
                    Player target = Main.player[whoAmI];
                    if (target.active && !target.dead) {
                        target.AddBuff(ModContent.BuffType<IndustrialShieldBuff>(), BuffDuration);
                    }
                });
            }
        }

        /// <summary>模式翻转的本地反馈</summary>
        protected override void OnModeToggleEffect() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f }, CenterInWorld);
            CombatText.NewText(HitBox, ShieldGenerator.Tint,
                AttackPattern ? ShieldGenerator.FieldOnText.Value : ShieldGenerator.FieldOffText.Value);
        }

        protected override void OnModeChangedByNet() {
            if (VaultUtils.isServer) {
                return;
            }
            CombatText.NewText(HitBox, ShieldGenerator.Tint,
                AttackPattern ? ShieldGenerator.FieldOnText.Value : ShieldGenerator.FieldOffText.Value);
        }
    }
}
