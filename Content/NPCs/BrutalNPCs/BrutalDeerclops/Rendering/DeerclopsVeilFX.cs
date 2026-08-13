using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Rendering
{
    /// <summary>
    /// 暴风雪视界全屏FX推送中心。客户端静态：主控AI每帧Push写入，
    /// VeilRender调Update平滑衰减并读取。不走网络，靠各端观察NPC状态自驱
    /// </summary>
    internal static class DeerclopsVeilFX
    {
        //推送目标(每帧覆写)
        private static float veilTarget;
        private static float whiteoutTarget;
        private static int gazePhase;
        private static int pushNpcIndex = -1;
        private static int lastPushStamp = -100;

        //平滑现值(渲染消费)
        internal static float Veil { get; private set; }
        internal static float Whiteout { get; private set; }
        /// <summary>凝视警告(本地玩家正面向它时爬升)</summary>
        internal static float GazeWarn { get; private set; }
        /// <summary>凝视惩罚白闪</summary>
        internal static float PunishFlash { get; private set; }
        internal static Vector2 BossWorldCenter { get; private set; }
        internal static bool BossValid { get; private set; }

        public static bool HasAny => Veil > 0.02f || Whiteout > 0.02f || GazeWarn > 0.02f || PunishFlash > 0.02f;

        /// <summary>主控AI每帧推送(客户端)。按本地玩家与boss的距离衰减，远处旁观者不吃全屏效果</summary>
        public static void Push(NPC npc, DeerclopsStateContext ctx) {
            if (VaultUtils.isServer) {
                return;
            }
            float atten = 1f;
            if (Main.LocalPlayer.active) {
                float dist = Main.LocalPlayer.Distance(npc.Center);
                atten = MathHelper.Clamp(1.55f - dist / 2000f, 0f, 1f);
            }
            veilTarget = MathHelper.Clamp(ctx.VeilTarget, 0f, 1f) * atten;
            whiteoutTarget = MathHelper.Clamp(ctx.Whiteout, 0f, 1f) * atten;
            gazePhase = ctx.GazePhase;
            pushNpcIndex = npc.whoAmI;
            BossWorldCenter = npc.Center;
            lastPushStamp = (int)Main.GameUpdateCount;
        }

        /// <summary>凝视惩罚命中，本地白闪</summary>
        public static void TriggerPunishFlash() {
            if (VaultUtils.isServer) {
                return;
            }
            PunishFlash = 1f;
        }

        /// <summary>每帧平滑(渲染句柄驱动，仅客户端)</summary>
        public static void Update() {
            bool stale = (int)Main.GameUpdateCount - lastPushStamp > 8;
            if (stale) {
                veilTarget = 0f;
                whiteoutTarget = 0f;
                gazePhase = 0;
                BossValid = false;
            }
            else {
                BossValid = pushNpcIndex >= 0 && pushNpcIndex < Main.maxNPCs
                    && Main.npc[pushNpcIndex].active && Main.npc[pushNpcIndex].type == NPCID.Deerclops;
            }

            //暴雪浓度渐进、消散稍快
            float veilRate = veilTarget > Veil ? 0.015f : 0.03f;
            Veil = MathHelper.Lerp(Veil, veilTarget, veilRate * 4f);
            Whiteout = MathHelper.Lerp(Whiteout, whiteoutTarget, 0.05f);

            //凝视警告：警告期面向它则爬升
            bool warnActive = !stale && gazePhase >= 1 && BossValid
                && DeerclopsAI.LocalPlayerFacing(Main.npc[pushNpcIndex], 1400f);
            GazeWarn = MathHelper.Clamp(GazeWarn + (warnActive ? 0.035f : -0.05f), 0f, 1f);

            PunishFlash = System.Math.Max(PunishFlash - 0.022f, 0f);
        }

        /// <summary>暴风雪瞬步爆雾(两端各自补)</summary>
        public static void SpawnStepBurst(Vector2 worldPos) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 16; i++) {
                Dust dust = Dust.NewDustPerfect(worldPos + Main.rand.NextVector2Circular(50f, 70f),
                    DustID.Snow, Main.rand.NextVector2Circular(4f, 3f) - Vector2.UnitY * Main.rand.NextFloat(0f, 2f),
                    80, default, Main.rand.NextFloat(1.2f, 2.2f));
                dust.noGravity = Main.rand.NextBool();
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(worldPos + Main.rand.NextVector2Circular(30f, 50f),
                    Main.rand.NextVector2Circular(1.5f, 1f) - Vector2.UnitY * 0.6f,
                    DeerclopsMotion.ColdWhite * 0.5f, Main.rand.NextFloat(0.9f, 1.4f))
                    .Configure(Main.rand.Next(30, 50), 0.6f, Main.rand.NextFloat(-0.04f, 0.04f));
            }
        }

        /// <summary>卸载/清场</summary>
        public static void Clear() {
            Veil = Whiteout = GazeWarn = PunishFlash = 0f;
            veilTarget = whiteoutTarget = 0f;
            gazePhase = 0;
            pushNpcIndex = -1;
            BossValid = false;
        }
    }
}
