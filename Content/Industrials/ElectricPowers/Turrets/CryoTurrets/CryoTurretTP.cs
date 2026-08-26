using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.Turrets.CryoTurrets
{
    /// <summary>
    /// 冰冻塔TP:控制不打伤害。范围内有敌怪时持续耗电,按节拍脉冲施加
    /// 霜火+减速;同一敌怪连续吃满蓄冻计数后短暂冻结。Boss级免疫由原版
    /// buff免疫表自行拦截,判定仅权威端,AddBuff 走原版同步
    /// </summary>
    internal class CryoTurretTP : BaseTurretTP
    {
        public override int TargetTileID => ModContent.TileType<CryoTurretTile>();
        public override int TargetItem => ModContent.ItemType<CryoTurret>();
        public override float MaxUEValue => 800;
        public override float AttackRange => 500;

        /// <summary>运转时每帧耗电</summary>
        internal const float ConsumePerTick = 0.6f;
        /// <summary>脉冲节拍(帧)</summary>
        internal const int PulseInterval = 30;
        /// <summary>脉冲附加的减益时长(帧),略长于节拍保证覆盖连续</summary>
        internal const int DebuffDuration = 150;
        /// <summary>连续吃满多少次脉冲后冻结</summary>
        internal const int FreezeThreshold = 4;
        /// <summary>冻结时长(帧)</summary>
        internal const int FrozenDuration = 60;

        internal float GlowIntensity;

        //---- 寒场视觉状态:纯客户端表现 ----
        /// <summary>脉冲霜环进度 0~1,1=已完成;脉冲拍且场内有敌时归零重放</summary>
        internal float FrostRingT { get; private set; } = 1f;
        private int mistTimer;
        private int glintTimer;

        private int pulseTimer;
        private int clientPulseTimer;
        private int textIdleTime;
        /// <summary>蓄冻计数,按敌怪槽位记;仅权威端使用,不序列化(重启清零可接受)</summary>
        private readonly Dictionary<int, int> freezeCounter = [];
        /// <summary>本次脉冲实际覆盖到的槽位,脉冲后据此清掉离场敌怪的计数</summary>
        private readonly HashSet<int> pulseSeen = [];

        public override void SetBattery() {
            IdleDistance = 4000;//玩家远离后停止运行
            DrawExtendMode = 900;//霜环最大半径500×射程模块(1.25²≈781)+撕裂余量,塔出屏后环仍需绘制
        }

        /// <summary>模块生效的持续耗电(节能模块作用点)</summary>
        private float EffectiveConsumePerTick => ConsumePerTick * ModuleRack.TurretEnergyMult;
        /// <summary>模块生效的脉冲节拍(射速模块让减益与蓄冻更密)</summary>
        private int EffectivePulseInterval => System.Math.Max(1, (int)(PulseInterval / ModuleRack.TurretRateMult));

        protected override void UpdateTurret() {
            if (textIdleTime > 0) {
                textIdleTime--;
            }

            bool running = false;
            if (AttackPattern) {
                if (MachineData.UEvalue >= EffectiveConsumePerTick) {
                    running = UpdateCryoField();
                }
                else if (textIdleTime <= 0 && CenterInWorld.FindClosestNPC(EffectiveRange, true, false) != null) {
                    //有敌无电才提示,避免刷屏
                    Defer(() => CombatText.NewText(HitBox, CryoTurret.Tint, CryoTurret.NoEnergyText.Value));
                    textIdleTime = 300;
                }
            }

            GlowIntensity = running
                ? Math.Min(1f, GlowIntensity + 0.03f)
                : Math.Max(0f, GlowIntensity - 0.03f);

            UpdateFieldVisual(running);

            //权威端同为游戏端时(单人)的脉冲霜雾
            if (running && !VaultUtils.isServer) {
                SpawnAmbientFrost();
            }
        }

        /// <summary>
        /// 寒场氛围推进(两端共用):霜环进度、绕塔寒雾、塔身挂霜闪点。
        /// 雾与闪点只在屏内生成,屏外不发
        /// </summary>
        private void UpdateFieldVisual(bool running) {
            if (FrostRingT < 1f) {
                FrostRingT = Math.Min(1f, FrostRingT + 1f / 26f);
            }
            if (!running || VaultUtils.isServer) {
                return;
            }
            if (!VaultUtils.IsPointOnScreen(CenterInWorld - Main.screenPosition, (int)(EffectiveRange + 220f))) {
                return;
            }

            //绕塔寒雾:缓慢旋绕的冷雾层
            if (++mistTimer >= 9) {
                mistTimer = 0;
                float orbitRadius = EffectiveRange * Rand.NextFloat(0.30f, 0.95f);
                Defer(() => PRTLoader.NewParticle<PRT_DefCryoMist>(CenterInWorld, Vector2.Zero,
                    new Color(185, 222, 252) * 0.30f, Main.rand.NextFloat(0.30f, 0.55f))
                    ?.Configure(Main.rand.Next(100, 150), CenterInWorld, orbitRadius));
            }

            //塔身挂霜:偶发晶面反光
            if (++glintTimer >= 26) {
                glintTimer = 0;
                Vector2 pos = PosInWorld + new Vector2(Rand.Next(Width), Rand.Next(Height));
                Defer(() => PRTLoader.NewParticle<PRT_DefFrostGlint>(pos, Vector2.Zero,
                    new Color(210, 240, 255), Main.rand.NextFloat(0.4f, 0.75f))
                    ?.Configure(Main.rand.Next(14, 24)));
            }
        }

        /// <summary>寒场主体:范围内有敌怪即运转耗电,按节拍脉冲上减益并推进蓄冻</summary>
        private bool UpdateCryoField() {
            bool anyTarget = false;
            bool pulse = ++pulseTimer >= EffectivePulseInterval;
            if (pulse) {
                pulseTimer = 0;
                pulseSeen.Clear();
            }

            foreach (var npc in Main.ActiveNPCs) {
                if (npc.friendly || npc.dontTakeDamage) {
                    continue;
                }
                if (npc.Distance(CenterInWorld) > EffectiveRange) {
                    continue;
                }
                anyTarget = true;
                if (!pulse) {
                    continue;
                }

                int whoAmI = npc.whoAmI;
                pulseSeen.Add(whoAmI);
                //并行阶段Buff写入延迟到主线程执行(串行阶段立即执行);
                //权威端 AddBuff 由原版 buff 同步广播,Boss免疫在 AddBuff 内部被原版拦截
                Defer(() => {
                    if (!Main.npc.IndexInRange(whoAmI)) {
                        return;
                    }
                    NPC target = Main.npc[whoAmI];
                    if (!target.active) {
                        return;
                    }
                    target.AddBuff(BuffID.Frostburn2, DebuffDuration);
                    target.AddBuff(BuffID.Chilled, DebuffDuration);
                });

                //蓄冻:连续吃满阈值次脉冲后短暂冻结
                int count = freezeCounter.GetValueOrDefault(whoAmI) + 1;
                if (count >= FreezeThreshold) {
                    count = 0;
                    Defer(() => {
                        if (!Main.npc.IndexInRange(whoAmI)) {
                            return;
                        }
                        NPC target = Main.npc[whoAmI];
                        if (target.active) {
                            target.AddBuff(BuffID.Frozen, FrozenDuration);
                        }
                    });
                }
                freezeCounter[whoAmI] = count;
            }

            //离场敌怪的蓄冻归零:脉冲帧上未覆盖到的槽位全部清除
            if (pulse && freezeCounter.Count > 0) {
                List<int> stale = null;
                foreach (int key in freezeCounter.Keys) {
                    if (!pulseSeen.Contains(key)) {
                        (stale ??= []).Add(key);
                    }
                }
                if (stale != null) {
                    foreach (int key in stale) {
                        freezeCounter.Remove(key);
                    }
                }
            }

            if (anyTarget) {
                MachineData.UEvalue -= EffectiveConsumePerTick;
                //脉冲拍且场内有敌:重放霜环,读作这一拍寒气扫过全场
                if (pulse) {
                    FrostRingT = 0f;
                }
            }
            return anyTarget;
        }

        /// <summary>塔顶霜雾与环缘冰晶,读作寒场在运转</summary>
        private void SpawnAmbientFrost() {
            if (Rand.NextBool(4)) {
                Vector2 spawnPos = PosInWorld + new Vector2(Rand.Next(Width), Rand.Next(14));
                Defer(() => {
                    Dust dust = Dust.NewDustPerfect(spawnPos, DustID.Frost, new Vector2(0, -0.5f), 120, default, 0.9f);
                    dust.noGravity = true;
                    dust.fadeIn = 0.5f;
                });
            }
            //环缘偶发冰晶,提示作用半径
            if (Rand.NextBool(10)) {
                float ang = Rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = CenterInWorld + ang.ToRotationVector2() * EffectiveRange;
                Defer(() => {
                    Dust dust = Dust.NewDustPerfect(pos, DustID.Frost, Vector2.Zero, 150, default, 0.8f);
                    dust.noGravity = true;
                });
            }
        }

        /// <summary>权威 gate 下客户端的表现帧:辉光近似推进+同节奏霜雾+本地判敌重放霜环</summary>
        protected override void UpdateTurretClient() {
            bool lit = AttackPattern && MachineData.UEvalue >= EffectiveConsumePerTick;
            GlowIntensity = lit
                ? Math.Min(1f, GlowIntensity + 0.03f)
                : Math.Max(0f, GlowIntensity - 0.03f);

            //客户端不知权威脉冲相位,按同节拍自走;NPC全端同步,范围判敌本地可查
            bool anyTarget = false;
            if (lit && GlowIntensity > 0.5f && ++clientPulseTimer >= EffectivePulseInterval) {
                clientPulseTimer = 0;
                SpawnAmbientFrost();
                foreach (var npc in Main.ActiveNPCs) {
                    if (!npc.friendly && !npc.dontTakeDamage
                        && npc.Distance(CenterInWorld) <= EffectiveRange) {
                        anyTarget = true;
                        break;
                    }
                }
                if (anyTarget) {
                    FrostRingT = 0f;
                }
            }

            UpdateFieldVisual(lit && GlowIntensity > 0.4f);
        }

        /// <summary>模式翻转的本地反馈</summary>
        protected override void OnModeToggleEffect() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f }, CenterInWorld);
            CombatText.NewText(HitBox, CryoTurret.Tint,
                AttackPattern ? CryoTurret.TurretOnText.Value : CryoTurret.TurretOffText.Value);
        }

        protected override void OnModeChangedByNet() {
            if (VaultUtils.isServer) {
                return;
            }
            CombatText.NewText(HitBox, CryoTurret.Tint,
                AttackPattern ? CryoTurret.TurretOnText.Value : CryoTurret.TurretOffText.Value);
        }
    }
}
