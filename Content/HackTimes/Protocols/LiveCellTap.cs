using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 活体电源：目标钉死并免疫伤害，按脉冲抽它的生命换施术者的 RAM。<br/>
    /// 与 <see cref="DataLeech"/> 的分工：那条鼓励你打目标，这条禁止你打
    /// 敌人从战斗对象变成经济设施，代价是彻底放弃这份战利品。<br/>
    /// 抽血直写 <c>npc.life</c> 不走伤害管线（不触发 HitEffect、不掉血字、不掉战利品），
    /// 榨干时静默移除（不进 checkDead，无 NPCLoot、不计击杀）。<br/>
    /// <c>dontTakeDamage</c> 由攻击方客户端在本地判定，只在权威端写等于没写，
    /// 所以挂载与每帧维持都镜像到 Replicated 路径
    /// </summary>
    internal class LiveCellTap : QuickHackDef
    {
        /// <summary>抽血脉冲间隔（帧）</summary>
        private const int DrainInterval = 30;
        /// <summary>每次脉冲抽取的生命比例</summary>
        private const float DrainRatio = 0.02f;
        /// <summary>每次脉冲返还的 RAM</summary>
        private const float RestorePerPulse = 0.5f;
        /// <summary>单次激活的返还硬上限；到顶后抽血一并停机，不白榨</summary>
        private const float RestoreCap = 12f;

        private static readonly Color Volt = new(180, 255, 90);
        private static readonly Color VoltDim = new(80, 130, 40);

        //activationId → 已返还总额。协议实例是单例，per-effect 累计账外挂在这张表上；
        //只有权威端会写它。泄漏路径：目标中途消失时 OnRemove 不触发，
        //账由下一次开户时的 PruneStale 与 Unload 清掉
        private static readonly Dictionary<long, float> returned = [];

        public override void SetDefaults() {
            UploadTime = 140;
            RamCost = 3;
            Category = QuickHackCategory.Contagion;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 900;

        public override void Unload() {
            base.Unload();
            returned.Clear();
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            //Boss 与体节不当电池；本来就免伤的目标接不进电路；
            //dontTakeDamage 恒为 false 的前提也让 OnRemove 的还原不需要记原值
            return !npc.boss && !npc.friendly && !npc.townNPC
                && !npc.dontTakeDamage && !npc.immortal
                && !(npc.realLife >= 0 && npc.realLife != npc.whoAmI)
                && !HackEffectTracker.HasEffect<LiveCellTap>(npc.whoAmI);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            npc.dontTakeDamage = true;
            if (Main.netMode != NetmodeID.Server) EmitCage(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return;
            npc.dontTakeDamage = true;
            EmitCage(npc);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return true;
            //不少 AI 会在自己的帧里改回这两样，每帧覆写而不是只在挂载时写一次
            npc.dontTakeDamage = true;
            TimeFreezeSystem.RefreshNPC<LiveCellTap>(npc, 2);

            if (elapsed > 0 && elapsed % DrainInterval == 0 && !DrainPulse(npc)) {
                return false; //榨干，效果随静默移除一起收场
            }
            if (Main.netMode != NetmodeID.Server) EmitIdle(npc, elapsed);
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return;
            npc.dontTakeDamage = true;
            TimeFreezeSystem.RefreshNPC<LiveCellTap>(npc, 2);
            EmitIdle(npc, elapsed);
        }

        public override void OnRemove(IHackTarget target) {
            if (HackTargets.TryNpc(target, out NPC npc)) {
                npc.dontTakeDamage = false;
                if (Main.netMode != NetmodeID.Server) EmitRelease(npc);
            }
            ClearAccount(target);
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return;
            npc.dontTakeDamage = false;
            EmitRelease(npc);
        }

        #region 电池结算（仅权威端）

        /// <summary>一次放电脉冲；返回 false 表示目标已被榨干并静默移除</summary>
        private bool DrainPulse(NPC npc) {
            ActiveHackEffect effect = HackEffectTracker.GetEffect<LiveCellTap>(npc.whoAmI);
            if (effect == null) return true;

            float already = returned.GetValueOrDefault(effect.ActivationId);
            if (already >= RestoreCap) return true; //输出到顶，电池停机但仍然钉死免伤

            Player caster = ResolveCaster(effect.CasterIndex);
            if (caster != null) {
                float grant = Math.Min(RestorePerPulse, RestoreCap - already);
                if (grant > 0f && RamSystem.Restore(caster, grant, out float got)) {
                    PruneStale();
                    returned[effect.ActivationId] = already + got;
                }
            }

            int drain = Math.Max(1, (int)(npc.lifeMax * DrainRatio));
            if (npc.life <= drain) {
                //榨干：留 1 血再下架，避开追踪器"目标被打死"的返还路径
                //榨干是这条协议的正收益结局，不该再叠一笔击杀退款
                npc.life = 1;
                npc.active = false;
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
                }
                else {
                    EmitCollapse(npc);
                }
                return false;
            }

            npc.life -= drain;
            npc.netUpdate = true; //直写生命不进任何原版包，靠 SyncNPC 载运
            if (Main.netMode != NetmodeID.Server) EmitPulse(npc, caster);
            return true;
        }

        private static Player ResolveCaster(int index) {
            if (index < 0 || index >= Main.maxPlayers) return null;
            Player player = Main.player[index];
            return player?.active == true && !player.dead ? player : null;
        }

        private void ClearAccount(IHackTarget target) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return;
            ActiveHackEffect effect = HackEffectTracker.GetEffect<LiveCellTap>(npc.whoAmI);
            if (effect != null) returned.Remove(effect.ActivationId);
        }

        //效果结束时协议拿不到已消亡效果的 activationId，账在下次开户时对齐
        private static void PruneStale() {
            if (returned.Count == 0) return;
            List<long> stale = null;
            foreach (long id in returned.Keys) {
                if (HackEffectTracker.FindEffect(id) == null) (stale ??= []).Add(id);
            }
            if (stale == null) return;
            for (int i = 0; i < stale.Count; i++) {
                returned.Remove(stale[i]);
            }
        }

        #endregion

        #region 表现

        //接入电池外框：上下两道电极合拢
        private static void EmitCage(NPC npc) {
            for (int i = 0; i < 12; i++) {
                float t = i / 11f;
                Vector2 pos = new(
                    npc.position.X + npc.width * t,
                    i % 2 == 0 ? npc.position.Y - 6f : npc.position.Y + npc.height + 6f);
                PRTLoader.NewParticle<PRT_Spark>(pos, Vector2.Zero, Volt, 0.8f)
                    ?.Configure(false, 22);
            }
        }

        //"这只被我当电池了"的常亮标记：体侧电极点 + 缓慢上浮的电荷
        private static void EmitIdle(NPC npc, int elapsed) {
            if (elapsed % 10 != 0) return;
            bool left = elapsed / 10 % 2 == 0;
            Vector2 pole = new(
                left ? npc.position.X - 4f : npc.position.X + npc.width + 4f,
                npc.Center.Y + Main.rand.NextFloat(-npc.height * 0.3f, npc.height * 0.3f));
            PRTLoader.NewParticle<PRT_Spark>(pole, new Vector2(0f, -0.6f),
                Color.Lerp(VoltDim, Volt, Main.rand.NextFloat()), 0.5f)?.Configure(false, 16);
        }

        //放电脉冲朝施术者飞，看得见电从哪儿来
        private static void EmitPulse(NPC npc, Player caster) {
            Vector2 dir = caster == null
                ? Vector2.UnitY * -1f
                : (caster.Center - npc.Center).SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 5; i++) {
                Vector2 vel = dir.RotatedByRandom(0.3f) * Main.rand.NextFloat(3.5f, 7f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, Volt, 0.85f)
                    ?.Configure(false, 16);
            }
        }

        //榨干崩解：电荷散尽，没有尸体、没有战利品
        private static void EmitCollapse(NPC npc) {
            for (int i = 0; i < 16; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.6f, 3.6f)
                    * Main.rand.NextFloat(0.3f, 1f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, VoltDim, 0.8f)
                    ?.Configure(false, 24);
            }
            PRTLoader.NewParticle<PRT_TBUGGlitch>(npc.Center, Vector2.Zero, Volt, 1.3f)
                ?.Configure(24);
        }

        private static void EmitRelease(NPC npc) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2.2f, 2.2f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, VoltDim, 0.6f)
                    ?.Configure(false, 14);
            }
        }

        #endregion
    }
}
