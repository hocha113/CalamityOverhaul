using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 载荷改写：十秒内目标射出的弹幕改判归你，按你的伤害封顶结算。<br/>
    /// 与 <see cref="ProjectileHijack"/> 的分工：那条改一发已经在飞的弹，
    /// 这条改弹幕源，目标本身不受影响、照常开火。<br/>
    /// 真正的翻转在 <see cref="HackNpcSourceProjectile.OnSpawn"/>（权威端，赶在生成包发出之前）
    /// 与 <c>ReceiveExtraAI</c>（各客户端）里各自执行——hostile/friendly 不在任何原版同步包里，
    /// 只在权威端翻等于联机空炮，这是镜像 ProjectileHijack 修复后的裁决
    /// </summary>
    internal class PayloadRewrite : QuickHackDef
    {
        /// <summary>转换伤害的硬上限倍率：施术者手持武器基础伤害 × 此值，防止 Boss 弹雨变己方核弹</summary>
        internal const float DamageCapMult = 1.5f;

        internal static readonly Color Signal = new(120, 200, 255);

        public override void SetDefaults() {
            UploadTime = 190;
            RamCost = 7;
            Category = QuickHackCategory.Control;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 600;

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            //友方源没有改写的意义；同一目标不叠加
            return !npc.friendly && !npc.townNPC
                && !HackEffectTracker.HasEffect<PayloadRewrite>(npc.whoAmI);
        }

        /// <summary>硬上限：min(原伤害, 手持武器基础伤害 × 1.5)。空手时封到 1，改写只剩骚扰价值</summary>
        internal static int ComputeDamageCap(Player caster) {
            int held = caster?.HeldItem?.damage ?? 0;
            return Math.Max(1, (int)(held * DamageCapMult));
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryNpc(target, out NPC npc)) return false;
            if (Main.netMode != NetmodeID.Server) EmitApply(npc);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitApply(npc);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitTick(npc, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitTick(npc, elapsed);
        }

        public override void OnRemove(IHackTarget target) {
            if (Main.netMode != NetmodeID.Server
                && HackTargets.TryNpc(target, out NPC npc)) {
                EmitRemove(npc);
            }
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (HackTargets.TryNpc(target, out NPC npc)) EmitRemove(npc);
        }

        #region 表现

        //上载完成：一圈信号蓝环绕炸开，读作"火控被接进来了"
        private static void EmitApply(NPC npc) {
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.4f, 3.4f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel, Signal, 1.0f)
                    ?.Configure(false, 22);
            }
            PRTLoader.NewParticle<PRT_Spark>(npc.Center, Vector2.Zero, Color.White, 1.6f)
                ?.Configure(false, 10);
        }

        //持续期：信号蓝沿体表巡走
        private static void EmitTick(NPC npc, int elapsed) {
            if (elapsed % 12 != 0) return;
            float ang = elapsed * 0.09f;
            Vector2 pos = npc.Center + new Vector2(MathF.Cos(ang), MathF.Sin(ang))
                * (npc.width * 0.55f + 6f);
            PRTLoader.NewParticle<PRT_Spark>(pos, Vector2.Zero, Signal, 0.5f)
                ?.Configure(false, 14);
        }

        private static void EmitRemove(NPC npc) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(2f, 2f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center, vel,
                    new Color(80, 120, 160), 0.6f)?.Configure(false, 14);
            }
        }

        /// <summary>单发弹幕被改写瞬间的确认闪，权威端（非服务端）与各客户端首见时各播一次</summary>
        internal static void EmitFlip(Projectile projectile) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2.4f, 2.4f);
                PRTLoader.NewParticle<PRT_Spark>(projectile.Center, vel, Signal, 0.8f)
                    ?.Configure(false, 14);
            }
            PRTLoader.NewParticle<PRT_Spark>(projectile.Center, Vector2.Zero,
                Color.White, 1.2f)?.Configure(false, 8);
        }

        #endregion
    }
}
