using CalamityOverhaul.Common;
using InnoVault.Cinematics;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.Victors
{
    /// <summary>
    /// 手术过场；主体 Bound Victor whoAmI；帧86全黑换装
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
                //锁输入 0-210
                .Add(new InputLockTrack(0, 210, CutsceneInputLockFlags.All))
                //镜头中点，上抬 -24
                .Add(CameraFocusTrack.Midpoint(0, 210, VictorCenter, c => c.PlayerCenter, new Vector2(0f, -24f), 0.06f))
                //变焦 0-56→1.42，165-210回拉
                .Add(new CameraZoomTrack(0, 56, 1f, 1.42f, 0.045f, CutsceneEase.CubicOut))
                .Add(new CameraZoomTrack(165, 45, 1.42f, 1f, 0.05f, CutsceneEase.CubicOut))
                //帧46闭眼
                .AddEvent(46, _ => {
                    VictorSurgery.EyelidTarget = 1f;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(CWRSound.Scanning with { Volume = 0.55f });
                    }
                })
                //帧86换装+微震
                .Add(new CameraShakeTrack(86, Vector2.Zero, 9f, 0.85f, 16))
                .AddEvent(86, _ => VictorSurgery.ApplyPendingOp())
                //帧140睁眼眩光
                .AddEvent(140, _ => {
                    VictorSurgery.EyelidTarget = 0f;
                    VictorSurgery.GlowValue = 1f;
                });
        }
    }
}
