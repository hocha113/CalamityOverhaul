using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFlashSteps;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>
    /// 鬼切玩法资源层：气力(神威疾走的燃料)与架势(处决技的蓄势)。<br/>
    /// 右键=神威疾走：耗气,按下即出;气力自然恢复(消耗后有回气延迟),连段命中回气。<br/>
    /// 架势由连段命中与疾走穿身格挡(蠕虫全身算一条,单次冲刺封顶)积攒;
    /// <see cref="CWRKeySystem.WeponSkill_R"/> 处决：蓄满出终结乱舞(耗全部),
    /// 过半出灭世一闪(耗一半),不足则鞘刀顿挫提醒。处决键任何状态下即时响应,不被连段阻塞。<br/>
    /// 数值 owner 端自治,不存 static、不进网络、不存档(进世界/复活重置);
    /// HUD 经 <see cref="OnikiriResourceSource"/> 只读本类,招式弹幕由 tML 自动同步
    /// </summary>
    internal class OnikiriPlayer : ModPlayer
    {
        //====调参常量====
        public const float VigorMax = 100f;
        /// <summary>神威疾走的气力开销</summary>
        public const float DashVigorCost = 30f;
        /// <summary>每帧自然回气(约 6/s)</summary>
        private const float VigorRegenPerTick = 0.10f;
        /// <summary>消耗后回气延迟(帧),防右键无脑连打</summary>
        private const int VigorRegenDelayTicks = 48;
        /// <summary>连段每命中一敌回气</summary>
        private const float VigorPerComboHit = 2f;

        public const float StanceMax = 100f;
        /// <summary>灭世一闪的架势门槛与开销</summary>
        public const float AnnihilateCost = 50f;
        /// <summary>连段每命中一敌蓄势</summary>
        private const float StancePerComboHit = 2.5f;
        /// <summary>疾走穿身格挡每敌蓄势</summary>
        private const float StancePerParry = 12f;
        /// <summary>单次冲刺穿身蓄势封顶</summary>
        private const float StanceParryCapPerDash = 36f;

        /// <summary>疾走墨痕伤害系数:定位是位移+格挡工具,不与连段争输出</summary>
        private const float DashDamageMul = 0.65f;
        /// <summary>灭世一闪伤害倍率(单次巨额结算)</summary>
        private const float AnnihilateDamageMul = 5f;
        /// <summary>冲刺再触发锁(帧):盖住位移+刹车段,防中途二次起跳双花</summary>
        private const int DashRefireLockTicks = 14;
        /// <summary>终结乱舞焦点距离钳制(与疾走射程同量级,演出保持在可读范围)</summary>
        private const float FinaleFocusMaxDist = 800f;
        /// <summary>终结乱舞光标磁吸半径</summary>
        private const float FinaleMagnetRadius = 150f;

        //====状态(owner 端自治)====
        internal float Vigor = VigorMax;
        internal float Stance;
        private int vigorRegenDelay;
        private int dashLock;
        private bool prevMouseRight;
        //本次冲刺穿身蓄势的已得量与已计根:蠕虫全身只算一条
        private float dashParryGained;
        private readonly HashSet<int> parriedRoots = [];
        private int readyCueTimer;

        public override void OnEnterWorld() {
            Vigor = VigorMax;
            Stance = 0f;
        }

        public override void OnRespawn() {
            Vigor = VigorMax;
            Stance = 0f;
        }

        public override void PostUpdate() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }

            if (vigorRegenDelay > 0) {
                vigorRegenDelay--;
            }
            else {
                Vigor = Math.Min(VigorMax, Vigor + VigorRegenPerTick);
            }
            if (dashLock > 0) {
                dashLock--;
            }

            bool justRight = Main.mouseRight && !prevMouseRight;
            prevMouseRight = Main.mouseRight;

            Item item = Player.GetItem();
            bool holding = item != null && item.Alives() && item.type == ModContent.ItemType<OnikiriItem>();
            if (!holding || Player.dead || Player.CCed) {
                return;
            }
            //点鬼簿/铭刻仪式演出中不受理招式输入
            if ((OniRegisterUI.Instance?.IsOpen ?? false) || (OniEngraveRiteUI.Instance?.Active ?? false)) {
                return;
            }

            ReadyCue();

            if (justRight && !Player.mouseInterface && !Player.cursorItemIconEnabled) {
                TryDash(item);
            }
            if (CWRKeySystem.WeponSkill_R.JustPressed) {
                TryExecute(item);
            }
        }

        //==================== 神威疾走 ====================

        private void TryDash(Item item) {
            //再触发锁内静默(是节拍不是资源问题);骑乘时位移权在坐骑
            if (dashLock > 0 || Player.mount?.Active == true) {
                return;
            }
            if (Vigor < DashVigorCost - 0.01f) {
                OniTalismanHud.NotifyVigorDenied();
                return;
            }

            Vigor -= DashVigorCost;
            vigorRegenDelay = VigorRegenDelayTicks;
            dashLock = DashRefireLockTicks;
            dashParryGained = 0f;
            parriedRoots.Clear();

            ShootState state = Player.GetShootState();
            Vector2 aim = Main.MouseWorld - Player.Center;
            OniFlashStep.Fire(Player, aim, (int)(state.WeaponDamage * DashDamageMul)
                , state.WeaponKnockback, source: Player.GetSource_ItemUse(item));
        }

        //==================== 处决 ====================

        private void TryExecute(Item item) {
            //演出进行中静默忽略:满屏刀光本身就是"正在忙"的答复
            if (Player.ownedProjectileCounts[ModContent.ProjectileType<OniFinaleSlash>()] > 0
                || Player.ownedProjectileCounts[ModContent.ProjectileType<OniAnnihilate>()] > 0) {
                return;
            }

            ShootState state = Player.GetShootState();
            if (Stance >= StanceMax - 0.01f) {
                //蓄满:终结乱舞,焦点=光标定区域+小半径磁吸
                Stance = 0f;
                Vector2 focus = ComputeFinaleFocus(out Vector2 aim);
                OniFinaleSlash.Fire(Player, focus, aim, state.WeaponDamage
                    , state.WeaponKnockback, source: Player.GetSource_ItemUse(item));
            }
            else if (Stance >= AnnihilateCost - 0.01f) {
                //过半:灭世一闪,以我为中心朝光标张开
                Stance -= AnnihilateCost;
                Vector2 aim = Main.MouseWorld - Player.Center;
                OniAnnihilate.Fire(Player, Player.Center, aim, (int)(state.WeaponDamage * AnnihilateDamageMul)
                    , state.WeaponKnockback, source: Player.GetSource_ItemUse(item));
            }
            else {
                OniTalismanHud.NotifyStanceDenied();
            }
        }

        /// <summary>
        /// 终结乱舞焦点：光标位置钳到射程内,再在小半径内磁吸到最要紧的目标
        /// (boss 旗优先,其次最大生命,同权取近;蠕虫按主体计旗)。半径内无敌则空地也认账
        /// </summary>
        private Vector2 ComputeFinaleFocus(out Vector2 aim) {
            Vector2 focus = Main.MouseWorld;
            Vector2 toMouse = focus - Player.Center;
            float dist = toMouse.Length();
            if (dist > FinaleFocusMaxDist) {
                focus = Player.Center + toMouse * (FinaleFocusMaxDist / dist);
            }

            NPC best = null;
            bool bestBoss = false;
            float bestLife = 0f;
            float bestD = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float d = Vector2.Distance(focus, npc.Center) - Math.Max(npc.width, npc.height) * 0.5f;
                if (d > FinaleMagnetRadius) {
                    continue;
                }
                NPC root = npc.realLife >= 0 && npc.realLife < Main.maxNPCs ? Main.npc[npc.realLife] : npc;
                bool better = best == null
                    || (root.boss != bestBoss
                        ? root.boss
                        : Math.Abs(root.lifeMax - bestLife) > 1f ? root.lifeMax > bestLife : d < bestD);
                if (better) {
                    best = npc;
                    bestBoss = root.boss;
                    bestLife = root.lifeMax;
                    bestD = d;
                }
            }
            if (best != null) {
                focus = best.Center;
            }

            aim = focus - Player.Center;
            if (aim.LengthSquared() < 1f) {
                aim = Main.MouseWorld - Player.Center;
            }
            return focus;
        }

        //==================== 资源增益(玩法挂点调用,owner 端) ====================

        /// <summary>连段命中:回气 + 蓄势(<see cref="CrimsonRendSlash.OnHitNPC"/> 调用)</summary>
        internal void OnComboHit() {
            Vigor = Math.Min(VigorMax, Vigor + VigorPerComboHit);
            Stance = Math.Min(StanceMax, Stance + StancePerComboHit);
        }

        /// <summary>疾走穿身即格挡:蓄势(<see cref="OniFlashStep"/> 标记成功时调用);
        /// 蠕虫按 realLife 归主体只算一条,单次冲刺封顶</summary>
        internal void OnDashParry(NPC npc) {
            int root = npc.realLife >= 0 ? npc.realLife : npc.whoAmI;
            if (!parriedRoots.Add(root) || dashParryGained >= StanceParryCapPerDash - 0.01f) {
                return;
            }
            float gain = Math.Min(StancePerParry, StanceParryCapPerDash - dashParryGained);
            dashParryGained += gain;
            Stance = Math.Min(StanceMax, Stance + gain);
        }

        /// <summary>满架势的身上提示：身周低密度绯焰火星上升,不看角落也知道刀可拔了</summary>
        private void ReadyCue() {
            if (Stance < StanceMax - 0.01f) {
                readyCueTimer = 0;
                return;
            }
            if (++readyCueTimer < 26) {
                return;
            }
            readyCueTimer = 0;
            Vector2 pos = Player.Center + new Vector2(Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(-8f, 20f));
            PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.7f, 1.5f)
                , new Color(255, 96, 58), Main.rand.NextFloat(0.2f, 0.32f))
                ?.Configure(Main.rand.Next(20, 32), affectedByGravity: false);
        }
    }

    /// <summary>把 <see cref="OnikiriPlayer"/> 的数值接给 HUD 的数据入口(只读)</summary>
    internal sealed class OnikiriResourceSource : IOniVigorSource, IOniStanceSource
    {
        public bool TryGetVigor(Player player, out OniVigorSnapshot snapshot) {
            if (player != null && player.active && player.TryGetModPlayer(out OnikiriPlayer okp)) {
                snapshot = new OniVigorSnapshot(okp.Vigor, OnikiriPlayer.VigorMax);
                return true;
            }
            snapshot = default;
            return false;
        }

        public bool TryGetStance(Player player, out OniStanceSnapshot snapshot) {
            if (player != null && player.active && player.TryGetModPlayer(out OnikiriPlayer okp)) {
                snapshot = new OniStanceSnapshot(okp.Stance, OnikiriPlayer.StanceMax);
                return true;
            }
            snapshot = default;
            return false;
        }
    }

    /// <summary>装载期把真实数据源挂进 HUD 入口,演示源退休;卸载时退回</summary>
    internal sealed class OnikiriResourceLoader : ICWRLoader
    {
        void ICWRLoader.SetupData() {
            OnikiriResourceSource source = new();
            OniVigor.SetSource(source);
            OniStance.SetSource(source);
        }

        void ICWRLoader.UnLoadData() {
            OniVigor.SetSource(null);
            OniStance.SetSource(null);
        }
    }
}
