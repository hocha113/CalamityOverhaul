using InnoVault.Cinematics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Victors
{
    /// <summary>
    /// Victor 手术过场片段（基于 InnoVault Cinematics）。
    /// <br/>主体为被交互的 Victor 的 whoAmI：镜头聚焦"Victor 与玩家中点"并拉近，全程锁输入；
    /// 时间轴事件驱动眼睑开合，并在全黑关键帧真正执行换装
    /// </summary>
    internal class VictorSurgeryCutscene : CutsceneClip<int>
    {
        public override int Priority => 50;

        public override bool CanPlay(Player player, int whoAmI) {
            return whoAmI >= 0 && whoAmI < Main.maxNPCs
                && Main.npc[whoAmI].active && Main.npc[whoAmI].type == ModContent.NPCType<Victor>();
        }

        private static Vector2 VictorCenter(CutsceneContext ctx) {
            if (ctx.TryGetSubject(out int who) && who >= 0 && who < Main.maxNPCs && Main.npc[who].active) {
                return Main.npc[who].Center;
            }
            return ctx.PlayerCenter;
        }

        protected override void BuildTimeline(CutsceneTimeline timeline) {
            timeline.Duration = 210;

            timeline
                //全程锁定移动/跳跃/使用/交互
                .Add(new InputLockTrack(0, 210, CutsceneInputLockFlags.All))
                //聚焦 Victor 与玩家中点（略微上抬取景），平滑跟随
                .Add(CameraFocusTrack.Midpoint(0, 210, VictorCenter, c => c.PlayerCenter, new Vector2(0f, -24f), 0.06f))
                //入场拉近，收尾拉回
                .Add(new CameraZoomTrack(0, 56, 1f, 1.42f, 0.045f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(165, 45, 1.42f, 1f, 0.05f, CutsceneEase.CubicOut))
                //闭眼（麻醉低鸣）
                .AddEvent(46, _ => {
                    VictorSurgery.EyelidTarget = 1f;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.45f, Volume = 0.7f });
                    }
                })
                //全黑：执行换装 + 手术微震
                .Add(new CameraShakeTrack(86, Vector2.Zero, 9f, 0.85f, 16))
                .AddEvent(86, _ => VictorSurgery.ApplyPendingOp())
                //睁眼（手术灯眩光 + 通电音）
                .AddEvent(140, _ => {
                    VictorSurgery.EyelidTarget = 0f;
                    VictorSurgery.GlowValue = 1f;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.2f });
                    }
                });
        }
    }
}
