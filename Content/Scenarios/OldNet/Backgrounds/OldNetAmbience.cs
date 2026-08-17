using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Backgrounds
{
    /// <summary>
    /// 旧网氛围接管（镜像 CyberspaceSystem 的压光换色，参数按旧网永夜标定）：
    /// 染色重心在背景——背景沉黑让地形剪影从天幕上剥出来，地砖只轻染保战斗可读性；
    /// 黑墙气质靠"黑场景+红轮廓"的对比而非满屏红罩。<br/>
    /// 另持环境声层：死寂基调（Music=0）上的两条低鸣——黑墙低频嗡鸣随离墙距离
    /// 指数衰减、静电风噪随带内腐化上量，全部原版音源
    /// </summary>
    internal class OldNetAmbience : ModSystem
    {
        //在场强度：进出世界时 ~1s 缓升缓降，与天幕 intensity 同步观感
        internal static float Presence { get; private set; }

        //环境声循环槽（镜像 Hydroelectric 的 SlotId+回调惯例）
        private static SlotId wallHumSlot;
        private static SlotId staticWindSlot;
        private static readonly SoundStyle WallHumStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };
        private static readonly SoundStyle StaticWindStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            float target = OldNetWorld.Active && !Main.gameMenu ? 1f : 0f;
            Presence = MathHelper.Lerp(Presence, target, 0.04f);
            if (Presence < 0.003f && target <= 0f) {
                Presence = 0f;
            }
            if (OldNetWorld.Active) {
                OldNetDeco.Update();
                UpdateAmbientLoops();
            }
        }

        //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
        private static void UpdateAmbientLoops() {
            if (Main.gameMenu) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(wallHumSlot, out _)) {
                wallHumSlot = SoundEngine.PlaySound(WallHumStyle, null, UpdateWallHum);
            }
            if (!SoundEngine.TryGetActiveSound(staticWindSlot, out _)) {
                staticWindSlot = SoundEngine.PlaySound(StaticWindStyle, null, UpdateStaticWind);
            }
        }

        //黑墙低鸣：离墙 0 列满响，~140 列衰减到近无声；涌动期整体抬一档
        private static bool UpdateWallHum(ActiveSound sound) {
            if (!OldNetWorld.Active || Main.gameMenu) {
                return false;
            }
            float dist = MathF.Max(Main.LocalPlayer.Center.X / 16f - OldNetMetrics.WallCols, 0f);
            sound.Volume = MathF.Exp(-dist / 140f) * 0.50f * Presence
                * (1f + OldNetSkyEvents.Surge * 0.8f);
            sound.Pitch = -0.55f;
            sound.Position = null;
            return true;
        }

        //静电风噪：墙脚近乎无声，衰减区压满——疯域的背景嘶声
        private static bool UpdateStaticWind(ActiveSound sound) {
            if (!OldNetWorld.Active || Main.gameMenu) {
                return false;
            }
            float corrupt = OldNetMetrics.CorruptionAt((int)(Main.LocalPlayer.Center.X / 16f));
            sound.Volume = (0.06f + corrupt * 0.30f) * Presence;
            sound.Pitch = -0.30f + corrupt * 0.18f;
            sound.Position = null;
            return true;
        }

        //压光：氛围级而非致盲级——旧网是永夜，压得比领域(0.30)轻
        public override void ModifyLightingBrightness(ref float scale) {
            if (Presence > 0.001f) {
                scale *= 1f - 0.22f * Presence;
            }
        }

        public override void ModifySunLightColor(ref Color tileColor, ref Color backgroundColor) {
            if (Presence <= 0.001f) {
                return;
            }
            //与赛博领域的酒红拉开：旧网更黑更沉，红只剩一点残温
            Color netTile = new(96, 58, 54);
            Color netBg = new(26, 10, 12);
            tileColor = Color.Lerp(tileColor, netTile, Presence * 0.40f);
            backgroundColor = Color.Lerp(backgroundColor, netBg, Presence * 0.72f);
        }

        public override void ClearWorld() => Presence = 0f;
    }
}
