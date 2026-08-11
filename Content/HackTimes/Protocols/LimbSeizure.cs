using CalamityOverhaul.Content.HackTimes.BossParts;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 肢体征收：八秒内这条肢体把自己的招式表用在本体身上。<br/>
    /// 不夺 AI——<see cref="BossPartAiSpoof"/> 在部件 AI 前后把「它读到的玩家位置」
    /// 换成本体中心再还原，部件用原生逻辑瞄准并攻击本体所在的位置。<br/>
    /// 伤害走两条通道，都按部件原伤害的六成、由权威端 <c>SimpleStrikeNPC</c> 结算（吃 DR）：
    /// 弹幕通道由 <see cref="LimbSeizureProjectile"/> 对本体做手动碰撞，
    /// 接触通道在 OnTick 里查部件与本体的判定盒重叠（爪类冲撞用）。<br/>
    /// 部件自身不受影响也不掉血，它的弹幕对玩家依然致命——站位照躲
    /// </summary>
    internal class LimbSeizure : QuickHackDef
    {
        /// <summary>结算给本体的伤害比例</summary>
        internal const float ConvertRatio = 0.6f;
        //接触通道冷却（帧）：爪子贴着本体磨蹭不该每帧都算一刀
        private const int ContactCooldown = 30;

        private static readonly Color Override = new(255, 128, 74);

        /// <summary>ActivationId → 接触冷却计时。单例协议的 per-effect 状态外挂</summary>
        private static readonly Dictionary<long, int> contactCooldowns = [];

        public override void SetDefaults() {
            UploadTime = 170;
            RamCost = 6;
            Category = QuickHackCategory.Control;
            SupportedTargets = HackTargetKind.BossPart;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 480;

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            SweepOrphaned();
            //只收登记过的攻击性肢体；原版肢体在灾厄在场时 AI 进了灾厄的
            //PreAI（先于本模组的钩子），位置伪装打不进去，CanSeizeLimb 一并把关
            if (!BossPartResolver.TryGetPart(npc, out BossPartInfo info)
                || info.Role != BossPartRole.Limb
                || !BossPartResolver.CanSeizeLimb(npc)) {
                return false;
            }
            return !HackEffectTracker.HasEffect<LimbSeizure>(npc.whoAmI);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            if (!BossPartResolver.TryGetPart(npc, out BossPartInfo info)
                || !info.IsPart) {
                return false;
            }
            ActiveHackEffect effect
                = HackEffectTracker.GetEffect<LimbSeizure>(npc.whoAmI);
            if (effect == null) return false;
            contactCooldowns[effect.ActivationId] = 0;
            BossPartAiSpoof.RefreshSeizure(npc.whoAmI, info.AnchorIndex);
            if (Main.netMode != NetmodeID.Server) EmitSeize(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitSeize(npc);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return true;
            //本体没了征收就没有对象，提前收摊（返回 false 会走 OnRemove 清账）
            if (!BossPartResolver.TryGetPart(npc, out BossPartInfo info)
                || !info.IsPart) {
                return false;
            }
            NPC anchor = Main.npc[info.AnchorIndex];
            BossPartAiSpoof.RefreshSeizure(npc.whoAmI, info.AnchorIndex);

            //接触通道：部件撞进本体判定盒，按冷却结一刀
            ActiveHackEffect effect
                = HackEffectTracker.GetEffect<LimbSeizure>(npc.whoAmI);
            if (effect != null
                && contactCooldowns.TryGetValue(effect.ActivationId, out int cooldown)) {
                if (cooldown > 0) {
                    contactCooldowns[effect.ActivationId] = cooldown - 1;
                }
                else if (npc.damage > 0 && !anchor.dontTakeDamage
                    && npc.Hitbox.Intersects(anchor.Hitbox)) {
                    int contact = Math.Max(1, (int)(npc.damage * ConvertRatio));
                    anchor.SimpleStrikeNPC(contact, 0, false, 0f, null, false, 0f, true);
                    contactCooldowns[effect.ActivationId] = ContactCooldown;
                    if (Main.netMode != NetmodeID.Server) EmitStrike(anchor.Center);
                }
            }

            if (Main.netMode != NetmodeID.Server) EmitLink(npc, anchor, elapsed);
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return;
            if (!BossPartResolver.TryGetPart(npc, out BossPartInfo info)
                || !info.IsPart) {
                return;
            }
            EmitLink(npc, Main.npc[info.AnchorIndex], elapsed);
        }

        public override void OnRemove(IHackTarget target) {
            if (HackTargets.TryNpc(target, out NPC npc)) {
                BossPartAiSpoof.ClearSeizure(npc.whoAmI);
                ActiveHackEffect effect
                    = HackEffectTracker.GetEffect<LimbSeizure>(npc.whoAmI);
                if (effect != null) {
                    contactCooldowns.Remove(effect.ActivationId);
                }
                if (Main.netMode != NetmodeID.Server) EmitRelease(npc);
            }
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitRelease(npc);
        }

        public override void Unload() {
            base.Unload();
            contactCooldowns.Clear();
        }

        internal static void ResetLedgers() => contactCooldowns.Clear();

        //目标失效被追踪器静默丢弃时不走 OnRemove，冷却账下次施放前对齐
        private static void SweepOrphaned() {
            if (contactCooldowns.Count == 0) return;
            List<long> orphaned = null;
            foreach (long activationId in contactCooldowns.Keys) {
                if (HackEffectTracker.FindEffect(activationId) != null) continue;
                (orphaned ??= []).Add(activationId);
            }
            if (orphaned == null) return;
            for (int i = 0; i < orphaned.Count; i++) {
                contactCooldowns.Remove(orphaned[i]);
            }
        }

        #region 表现

        //接管电弧绕部件一圈
        private static void EmitSeize(NPC npc) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.2f, 3.2f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, Override, 0.9f)
                    ?.Configure(false, 20);
            }
        }

        //部件→本体的征收连线，预算与蜂群链接同款克制
        private static void EmitLink(NPC part, NPC anchor, int elapsed) {
            if (elapsed % 26 != 0 || anchor == null || !anchor.active) return;
            Vector2 delta = anchor.Center - part.Center;
            int steps = (int)MathHelper.Clamp(delta.Length() / 70f, 2f, 7f);
            for (int i = 0; i <= steps; i++) {
                Vector2 pos = part.Center + delta * (i / (float)steps);
                PRTLoader.NewParticle<PRT_Spark>(pos, Vector2.Zero, Override, 0.5f)
                    ?.Configure(false, 12);
            }
        }

        private static void EmitStrike(Vector2 center) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.6f, 3.6f);
                PRTLoader.NewParticle<PRT_Spark>(center, vel, Override, 0.7f)
                    ?.Configure(false, 16);
            }
        }

        private static void EmitRelease(NPC npc) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2f, 2f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel,
                    new Color(180, 120, 90), 0.6f)?.Configure(false, 14);
            }
        }

        #endregion
    }

    /// <summary>
    /// 征收弹幕的手动本体碰撞。<br/>
    /// 敌对弹幕在原版管线里根本不测 NPC 碰撞（trap 弹除外），
    /// 翻 <c>CanHitNPC</c> 没有用——这里在权威端自己做 AABB。<br/>
    /// 标记只在生成端（权威端）打上：NPC 的弹幕由权威端生成，
    /// 远端客户端拿不到标记也不需要——结算与判定全在权威端
    /// </summary>
    internal class LimbSeizureProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        //-1 = 未被征收
        private int anchorIndex = -1;
        private int hitCooldown;

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (Main.netMode == NetmodeID.MultiplayerClient
                || !projectile.hostile
                || source is not EntitySource_Parent { Entity: NPC parent }) {
                return;
            }
            if (BossPartAiSpoof.TryGetSeizureAnchor(parent, out NPC anchor)) {
                anchorIndex = anchor.whoAmI;
            }
        }

        public override void PostAI(Projectile projectile) {
            if (anchorIndex < 0 || Main.netMode == NetmodeID.MultiplayerClient
                || WorldFreezeSystem.IsActive) {
                return;
            }
            if (hitCooldown > 0) {
                hitCooldown--;
                return;
            }
            NPC anchor = Main.npc[anchorIndex];
            if (!anchor.active || anchor.life <= 0) {
                anchorIndex = -1;
                return;
            }
            if (!projectile.hostile || projectile.damage <= 0
                || anchor.dontTakeDamage
                || !projectile.Hitbox.Intersects(anchor.Hitbox)) {
                return;
            }

            int damage = Math.Max(1, (int)(projectile.damage * LimbSeizure.ConvertRatio));
            anchor.SimpleStrikeNPC(damage, 0, false, 0f, null, false, 0f, true);
            //穿透型（激光等）按冷却反复结算，一次性弹自灭
            if (projectile.penetrate == 1) {
                projectile.Kill();
                return;
            }
            hitCooldown = 20;
        }
    }
}
