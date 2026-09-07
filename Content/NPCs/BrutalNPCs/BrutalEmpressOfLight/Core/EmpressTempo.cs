using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core
{
    /// <summary>
    /// 光之圆舞的节拍器：三拍子，一小节 60f（一秒），拍长 20f。
    /// 服务端在 NPCOverride.ai[4] 里推进小节内帧数；客户端本地推进，收到新同步值时才对表，避免 10f 同步间隔造成的跳拍。
    /// 所有招式在第一拍起手、在下一小节第一拍落下
    /// </summary>
    internal static class EmpressTempo
    {
        internal const int BarFrames = 60;
        internal const int BeatFrames = 20;
        internal const int Beats = 3;
        internal const int SlotBarFrame = 4;
        internal const int SlotBarIndex = 5;

        /// <summary>服务端推进节拍；客户端本地推进，只在同步值变化时对表</summary>
        internal static void Update(EmpressOfLightAI ai, EmpressStateContext ctx) {
            if (!VaultUtils.isClient) {
                ai.ai[SlotBarFrame] = (ai.ai[SlotBarFrame] + 1f) % BarFrames;
                ctx.BarFrame = (int)ai.ai[SlotBarFrame];
                if (ctx.BarFrame == 0) {
                    ai.ai[SlotBarIndex] += 1f;
                }
                ctx.BarIndex = (int)ai.ai[SlotBarIndex];
                return;
            }
            int synced = (int)ai.ai[SlotBarFrame];
            int syncedBar = (int)ai.ai[SlotBarIndex];
            if (synced != ctx.LastSyncedBarFrame || syncedBar != ctx.LastSyncedBarIndex) {
                ctx.LastSyncedBarFrame = synced;
                ctx.LastSyncedBarIndex = syncedBar;
                ctx.BarFrame = synced;
                ctx.BarIndex = syncedBar;
            }
            else {
                ctx.BarFrame = (ctx.BarFrame + 1) % BarFrames;
                if (ctx.BarFrame == 0) {
                    ctx.BarIndex++;
                }
            }
        }

        /// <summary>弹幕侧读宿主节拍（各端本地值）</summary>
        internal static bool TryGet(NPC host, out int barFrame, out int barIndex) {
            barFrame = 0;
            barIndex = 0;
            if (host == null || !host.active) {
                return false;
            }
            if (!host.TryGetOverride(out System.Collections.Generic.Dictionary<System.Type, InnoVault.GameSystem.NPCOverride> overrides)
                || !overrides.TryGetValue(typeof(EmpressOfLightAI), out InnoVault.GameSystem.NPCOverride raw)
                || raw is not EmpressOfLightAI ai || ai.Context == null) {
                return false;
            }
            barFrame = ai.Context.BarFrame;
            barIndex = ai.Context.BarIndex;
            return true;
        }

        /// <summary>第一拍的可见可听提示：翅根一圈涟漪 + 低音铃；仅她在附近的客户端</summary>
        internal static void DownbeatCue(EmpressStateContext ctx) {
            if (VaultUtils.isServer || ctx.BarFrame != 0 || !ctx.TempoCueEnabled) {
                return;
            }
            NPC npc = ctx.Npc;
            if (npc.Distance(Main.LocalPlayer.Center) > 2400f || npc.Opacity < 0.3f) {
                return;
            }
            float day = ctx.DayFormBlend;
            PRTLoader.NewParticle<PRT_EmpressRipple>(npc.Center, Vector2.Zero, Color.White, 0.42f)?.Configure(14, 0.12f, day);
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.28f, Pitch = 0.45f, MaxInstances = 1 }, npc.Center);
        }

        /// <summary>弱拍的更轻提示（第二、三拍），只有音没有涟漪</summary>
        internal static void WeakbeatCue(EmpressStateContext ctx) {
            if (VaultUtils.isServer || ctx.BarFrame % BeatFrames != 0 || ctx.BarFrame == 0 || !ctx.TempoCueEnabled) {
                return;
            }
            NPC npc = ctx.Npc;
            if (npc.Distance(Main.LocalPlayer.Center) > 2400f || npc.Opacity < 0.3f) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item35 with { Volume = 0.12f, Pitch = 0.1f, MaxInstances = 1 }, npc.Center);
        }
    }
}
