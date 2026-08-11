using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 索引预读：纯信息协议，不改容器任何状态。<br/>
    /// "索引"没有独立存储——效果本身挂在追踪器里就是索引，
    /// <see cref="ContainerScannable.BuildScanData"/> 查 <see cref="IsIndexed"/>
    /// 决定是否把内容清单铺进扫描面板；效果到期即索引失效，零清账、零泄漏
    /// </summary>
    internal class IndexPreread : QuickHackDef
    {
        private static readonly Color DataGlow = new(120, 220, 255);

        public override void SetDefaults() {
            UploadTime = 80;
            RamCost = 3;
            Category = QuickHackCategory.Covert;
            SupportedTargets = HackTargetKind.Container;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 60;

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (target is not ContainerScannable c) return false;
            //已缓存的箱子重复预读只是白花 RAM，直接拒绝
            return !IsIndexed(c.AnchorX, c.AnchorY);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not ContainerScannable) return false;
            //不写世界任何状态：面板呈现由效果存在性驱动
            if (!VaultUtils.isServer) EmitScanBurst(target.WorldCenter);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            EmitScanBurst(target.WorldCenter);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (!VaultUtils.isServer) EmitIndexPulse(target.WorldCenter, elapsed);
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            EmitIndexPulse(target.WorldCenter, elapsed);
        }

        public override void OnRemove(IHackTarget target) {
            if (!VaultUtils.isServer) EmitFizzle(target.WorldCenter);
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            EmitFizzle(target.WorldCenter);
        }

        /// <summary>
        /// 该锚点的箱子是否已被任何施术者索引。
        /// 复制效果也进 <c>activeTileEffects</c>，所以每个端都能查到——
        /// 情报在队伍里共享是设计意图
        /// </summary>
        public static bool IsIndexed(int anchorX, int anchorY) {
            IReadOnlyList<ActiveHackEffect> effects
                = HackEffectTracker.AllActiveTileEffects;
            for (int i = 0; i < effects.Count; i++) {
                ActiveHackEffect effect = effects[i];
                if (effect.Active && effect.Hack is IndexPreread
                    && effect.Target is ContainerScannable c
                    && c.AnchorX == anchorX && c.AnchorY == anchorY) {
                    return true;
                }
            }
            return false;
        }

        //上传落地的一次读出：数据流从箱体涌向上方
        private static void EmitScanBurst(Vector2 center) {
            for (int i = 0; i < 14; i++) {
                Vector2 offset = Main.rand.NextVector2Circular(20f, 14f);
                Vector2 vel = new(Main.rand.NextFloat(-0.4f, 0.4f),
                    Main.rand.NextFloat(-2.2f, -0.8f));
                PRTLoader.NewParticle<PRT_Spark>(center + offset, vel, DataGlow, 0.8f)
                    ?.Configure(false, 24);
            }
        }

        //缓存存续期的低频心跳，读作"这个箱子已在库里"
        private static void EmitIndexPulse(Vector2 center, int elapsed) {
            if (elapsed % 50 != 0) return;
            Vector2 offset = new(Main.rand.NextFloat(-16f, 16f), -12f);
            PRTLoader.NewParticle<PRT_Spark>(center + offset,
                new Vector2(0f, -0.6f), DataGlow, 0.45f)?.Configure(false, 20);
        }

        private static void EmitFizzle(Vector2 center) {
            for (int i = 0; i < 6; i++) {
                Vector2 offset = Main.rand.NextVector2Circular(18f, 12f);
                PRTLoader.NewParticle<PRT_Spark>(center + offset,
                    new Vector2(0f, 0.4f), DataGlow * 0.6f, 0.5f)?.Configure(false, 14);
            }
        }
    }
}
