using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Sunkendune
{
    /// <summary>
    /// 残酷模式地下沙漠「尘窖」常态氛围（纯本机演出，服务端不参与）：
    /// 弥漫沙尘微粒 + 干燥空洞的风鸣与沙粒摩擦声双循环（镜像 OldNetAmbience 的 SlotId+回调惯例）。
    /// 石缝细沙流与甲虫惊群的状态与绘制在 <see cref="SunkenduneAmbientRender"/>。
    /// 主题是"地形吞噬"：地下无任何风推位移机制，与地表 DuneStorm 的风驱动划清
    /// </summary>
    internal class SunkenduneAmbience : ModSystem
    {
        /// <summary>本机在场强度 0~1（进出群系缓升缓降，Boss 在场压半，不硬切）</summary>
        internal static float Presence { get; private set; }

        //环境声循环槽（镜像 Hydroelectric/OldNet 的 SlotId+回调惯例，循环丢失就补挂）
        private static SlotId windMoanSlot;
        private static SlotId grainHissSlot;
        private static readonly SoundStyle WindMoanStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };
        private static readonly SoundStyle GrainHissStyle =
            SoundID.BlizzardStrongLoop with { IsLooped = true, MaxInstances = 1 };

        private static int moteTimer;

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            Player player = Main.LocalPlayer;
            bool inZone = !Main.gameMenu && GameModeSystem.BrutalActive
                && player.active && player.ZoneUndergroundDesert;
            //Boss 在场：纯视觉氛围保留但减弱，伤害/位移机制由各实体自行暂停
            float target = inZone ? (CWRWorld.HasBoss ? 0.5f : 1f) : 0f;
            Presence = MathHelper.Lerp(Presence, target, 0.045f);
            if (Presence < 0.004f && target <= 0f) {
                Presence = 0f;
            }
            if (Presence <= 0.03f || Main.gameMenu) {
                return;
            }
            UpdateAmbientLoops();
            if (!Main.gamePaused) {
                UpdateMotes();
            }
        }

        private static void UpdateAmbientLoops() {
            if (!SoundEngine.TryGetActiveSound(windMoanSlot, out _)) {
                windMoanSlot = SoundEngine.PlaySound(WindMoanStyle, null, UpdateWindMoan);
            }
            if (!SoundEngine.TryGetActiveSound(grainHissSlot, out _)) {
                grainHissSlot = SoundEngine.PlaySound(GrainHissStyle, null, UpdateGrainHiss);
            }
        }

        //干燥空洞的风鸣：低沉洞穴气流，慢呼吸起伏避免死循环感
        private static bool UpdateWindMoan(ActiveSound sound) {
            if (Presence <= 0.01f || Main.gameMenu) {
                return false;
            }
            float breathe = 0.85f + 0.15f * MathF.Sin((float)Main.timeForVisualEffects * 0.011f);
            sound.Volume = 0.30f * Presence * breathe;
            sound.Pitch = -0.52f;
            sound.Position = null;
            return true;
        }

        //沙粒摩擦：高频气声，屏内细沙流越多越明显
        private static bool UpdateGrainHiss(ActiveSound sound) {
            if (Presence <= 0.01f || Main.gameMenu) {
                return false;
            }
            sound.Volume = (0.07f + 0.03f * SunkenduneAmbientRender.ActiveTrickleCount) * Presence;
            sound.Pitch = 0.32f;
            sound.Position = null;
            return true;
        }

        //弥漫沙尘微粒：约 8 粒/秒，只在屏内空气处生成
        private static void UpdateMotes() {
            if (--moteTimer > 0 || Presence < 0.35f) {
                return;
            }
            moteTimer = 7;
            Vector2 pos = Main.screenPosition + new Vector2(
                Main.rand.NextFloat(Main.screenWidth), Main.rand.NextFloat(Main.screenHeight));
            Point tile = pos.ToTileCoordinates();
            if (!WorldGen.InWorld(tile.X, tile.Y, 10) || WorldGen.SolidTile(tile.X, tile.Y)) {
                return;
            }
            Dust dust = Dust.NewDustPerfect(pos, DustID.Sand,
                new Vector2(Main.rand.NextFloat(-0.22f, 0.22f), Main.rand.NextFloat(0.02f, 0.1f)),
                150, default, Main.rand.NextFloat(0.6f, 0.95f));
            dust.noGravity = true;
            dust.fadeIn = 0.8f;
        }

        public override void ClearWorld() => Presence = 0f;
    }
}
