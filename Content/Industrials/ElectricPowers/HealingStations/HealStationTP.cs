using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.HealingStations
{
    /// <summary>
    /// 治疗站TP:范围回血光环。篝火模型:逻辑各端同跑,每个端为它模拟的
    /// 玩家本地挂再生buff,零同步;回血走原版 lifeRegen,不发治疗事件,
    /// 无战斗数字刷屏也无服务器治疗包
    /// </summary>
    internal class HealStationTP : BaseTurretTP
    {
        public override int TargetTileID => ModContent.TileType<HealStationTile>();
        public override int TargetItem => ModContent.ItemType<HealStation>();
        public override float MaxUEValue => 500;
        /// <summary>光环塔:逻辑各端同跑,buff本地挂(镜像药剂信标/宁静塔)</summary>
        public override bool SimulateOnAllEndpoints => true;

        /// <summary>光环半径(像素)</summary>
        internal const float AuraRadius = 800f;
        /// <summary>运转时每帧耗电</summary>
        internal const float ConsumePerTick = 0.5f;
        //buff续杯节奏与单次时长:短时长反复续,离场即自然消退
        private const int ApplyInterval = 30;
        private const int BuffDuration = 120;

        /// <summary>本 tick 光环是否实际运转(开启且有电且范围内有玩家)</summary>
        internal bool WorkingActive { get; private set; }
        internal float GlowIntensity;

        private int applyTimer;
        private int textIdleTime;
        private int ambienceTimer;

        public override void SetBattery() {
            IdleDistance = 4000;//玩家远离后停止运行
        }

        /// <summary>模块生效光环半径(射程模块作用点)</summary>
        private float EffectiveAuraRadius => AuraRadius * ModuleRack.TurretRangeMult;
        /// <summary>模块生效的持续耗电(节能模块作用点)</summary>
        private float EffectiveConsumePerTick => ConsumePerTick * ModuleRack.TurretEnergyMult;

        protected override void UpdateTurret() {
            if (textIdleTime > 0) {
                textIdleTime--;
            }

            bool running = false;
            if (AttackPattern) {
                //范围内有活玩家才运转耗电
                bool anyPlayer = false;
                float auraRadius = EffectiveAuraRadius;
                float radiusSQ = auraRadius * auraRadius;
                foreach (Player player in Main.ActivePlayers) {
                    if (!player.dead && player.Center.DistanceSQ(CenterInWorld) <= radiusSQ) {
                        anyPlayer = true;
                        break;
                    }
                }

                if (anyPlayer) {
                    if (MachineData.UEvalue >= EffectiveConsumePerTick) {
                        MachineData.UEvalue -= EffectiveConsumePerTick;
                        running = true;
                        ApplyAura(radiusSQ);
                    }
                    else if (textIdleTime <= 0) {
                        //并行阶段CombatText生成延迟到主线程执行(串行阶段立即执行)
                        Defer(() => CombatText.NewText(HitBox, HealStation.Tint, HealStation.NoEnergyText.Value));
                        textIdleTime = 300;
                    }
                }
            }

            WorkingActive = running;
            GlowIntensity = running
                ? Math.Min(1f, GlowIntensity + 0.03f)
                : Math.Max(0f, GlowIntensity - 0.03f);

            //运转氛围:塔顶缓慢上飘的治愈微光
            if (running && !VaultUtils.isServer && ++ambienceTimer >= 20) {
                ambienceTimer = 0;
                Vector2 spawnPos = PosInWorld + new Vector2(Rand.Next(Width), Rand.Next(14));
                Defer(() => {
                    Dust dust = Dust.NewDustPerfect(spawnPos, DustID.PinkTorch, new Vector2(0, -0.7f), 130, default, 0.9f);
                    dust.noGravity = true;
                    dust.fadeIn = 0.5f;
                });
            }
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
                        target.AddBuff(ModContent.BuffType<IndustrialRegenBuff>(), BuffDuration);
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
            CombatText.NewText(HitBox, HealStation.Tint,
                AttackPattern ? HealStation.FieldOnText.Value : HealStation.FieldOffText.Value);
        }

        protected override void OnModeChangedByNet() {
            if (VaultUtils.isServer) {
                return;
            }
            CombatText.NewText(HitBox, HealStation.Tint,
                AttackPattern ? HealStation.FieldOnText.Value : HealStation.FieldOffText.Value);
        }
    }
}
