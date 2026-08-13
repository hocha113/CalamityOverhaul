using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States;
using InnoVault.Cinematics;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight
{
    /// <summary>死亡运镜：跟随她的升空与绽散，对齐EmpressDeathState阶段帧</summary>
    internal sealed class EmpressDeathCutscene : CutsceneClip<NPC>
    {
        public override int Priority => 100;

        public override bool CanPlay(Player player, NPC subject)
            => base.CanPlay(player, subject) && subject != null && subject.active;

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = EmpressDeathState.TotalTime;

            int ascendLen = EmpressDeathState.AscendEnd - EmpressDeathState.StaggerEnd;
            int gatherLen = EmpressDeathState.GatherEnd - EmpressDeathState.AscendEnd;
            int dissolveLen = EmpressDeathState.TotalTime - EmpressDeathState.GatherEnd;

            //聚焦她本体：踉跄近景→升空跟拍→收束贴近→绽散缓缓拉远
            timeline
                .Add(CameraFocusTrack.Follow(0, EmpressDeathState.StaggerEnd,
                    BossCenter, new Vector2(0f, 10f), 0.05f))
                .Add(CameraFocusTrack.Follow(EmpressDeathState.StaggerEnd, ascendLen,
                    BossCenter, new Vector2(0f, 40f), 0.06f))
                .Add(CameraFocusTrack.Follow(EmpressDeathState.AscendEnd, gatherLen,
                    BossCenter, Vector2.Zero, 0.085f))
                .Add(CameraFocusTrack.Follow(EmpressDeathState.GatherEnd, dissolveLen,
                    BossCenter, new Vector2(0f, -20f), 0.05f));

            //变焦：1→1.25踉跄，1.25→1.5收束顶点，绽散回落1.1看满屏光蝶
            timeline
                .Add(new CameraZoomTrack(0, EmpressDeathState.StaggerEnd, 1f, 1.25f, 0.035f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(EmpressDeathState.StaggerEnd, ascendLen, 1.25f, 1.35f, 0.045f))
                .Add(new CameraZoomTrack(EmpressDeathState.AscendEnd, gatherLen, 1.35f, 1.5f, 0.06f))
                .Add(new CameraZoomTrack(EmpressDeathState.GatherEnd, dissolveLen, 1.5f, 1.1f, 0.045f, CutsceneEase.CubicOut));

            //全程锁操作（与Prime同规格）
            timeline.Add(new InputLockTrack(0, EmpressDeathState.TotalTime,
                CutsceneInputLockFlags.Movement | CutsceneInputLockFlags.Jump | CutsceneInputLockFlags.UseItem));
        }

        //演出主体失效时回退玩家中心，防镜头瞬移
        private static Vector2 BossCenter(CutsceneContext context)
            => context.TryGetSubject(out NPC boss) && boss.active ? boss.Center : context.PlayerCenter;
    }

    /// <summary>死亡演出玩家侧：本地启停运镜</summary>
    internal class EmpressDeathPerformancePlayer : ModPlayer
    {
        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            EmpressOfLightAI performing = FindPerformingEmpress(out NPC boss);
            bool playing = CutsceneDirector.CurrentClip is EmpressDeathCutscene;

            if (performing != null && boss != null) {
                //距离太远的旁观者不接管镜头
                if (!playing && Player.Distance(boss.Center) < 2600f) {
                    CutsceneDirector.Play<EmpressDeathCutscene, NPC>(boss, restartSameClip: false);
                }
            }
            else if (playing) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>死亡演出中的女皇，无则null</summary>
        private static EmpressOfLightAI FindPerformingEmpress(out NPC boss) {
            boss = null;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.HallowBoss) {
                    continue;
                }
                //本扫描无接管前提：配置关闭时原版女皇没有挂载覆写，字典可能缺键甚至为null，探测字典防炸
                if (!npc.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)
                    || !overrides.TryGetValue(typeof(EmpressOfLightAI), out NPCOverride raw)
                    || raw is not EmpressOfLightAI ai) {
                    continue;
                }
                if (ai.InDeathPerformance) {
                    boss = npc;
                    return ai;
                }
            }
            return null;
        }
    }
}
