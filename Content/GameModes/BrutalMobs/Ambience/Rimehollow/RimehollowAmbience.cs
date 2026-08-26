using ReLogic.Utilities;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Rimehollow
{
    /// <summary>
    /// 冰雪洞穴（Rimehollow）氛围核心：区域判定、在场强度、
    /// 「冽息」的环境声底（闷洞风循环 + 深处冰层开裂的空洞冰鸣）与
    /// 「冰壁回声」的延迟回声队列。全部客户端演出量，服务端不持有任何状态
    /// </summary>
    internal static class RimehollowAmbience
    {
        /// <summary>本地屏幕在场强度 0~1（进出群系缓升缓降，不硬切）</summary>
        public static float Presence { get; private set; }

        //==== 环境声循环槽（镜像 OldNetAmbience 的 SlotId+回调惯例） ====
        private static SlotId caveWindSlot;
        private static readonly SoundStyle CaveWindStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };

        //==== 空洞冰鸣调度 ====
        private static int groanIn = 600;

        //==== 延迟回声队列（冰鸣尾响与镐击回声共用；固定槽位零分配） ====
        private const int EchoSlots = 16;
        internal const byte EchoTink = 0;
        internal const byte EchoRumble = 1;
        internal const byte EchoCrack = 2;

        private struct Echo
        {
            internal int Delay;
            internal byte Kind;
            internal Vector2 Pos;
            internal float Volume;
            internal float Pitch;
        }

        private static readonly Echo[] echoes = new Echo[EchoSlots];

        /// <summary>冰锥可附着的洞顶冰系瓦片</summary>
        private static readonly HashSet<int> IcicleAnchorTiles = [
            TileID.IceBlock, TileID.SnowBlock, TileID.CorruptIce, TileID.HallowedIce, TileID.FleshIce,
        ];

        /// <summary>会折射星闪的晶质冰面（雪块不参与，星闪只属于晶体）</summary>
        private static readonly HashSet<int> CrystalIceTiles = [
            TileID.IceBlock, TileID.BreakableIce, TileID.CorruptIce, TileID.HallowedIce, TileID.FleshIce,
        ];

        /// <summary>
        /// 群系判定：残酷模式 + 雪原 + 地下高度。
        /// 让位规则：邪地/神圣（含冰变体）、地牢、花岗岩/大理石、蘑菇、陨石、微光、蜂巢
        /// 与星辉瘟疫各有专属槽位，不叠加两层氛围
        /// </summary>
        internal static bool In(Player player) {
            if (!GameModeSystem.BrutalActive || !player.ZoneSnow) {
                return false;
            }
            if (!player.ZoneDirtLayerHeight && !player.ZoneRockLayerHeight) {
                return false;
            }
            if (player.ZoneCorrupt || player.ZoneCrimson || player.ZoneHallow
                || player.ZoneDungeon || player.ZoneGranite || player.ZoneMarble
                || player.ZoneGlowshroom || player.ZoneMeteor || player.ZoneShimmer
                || player.ZoneHive) {
                return false;
            }
            if (CWRRef.Has && player.GetPlayerZoneAstral()) {
                return false;
            }
            return true;
        }

        /// <summary>瓦片是否为冰锥附着面</summary>
        internal static bool IsIcicleAnchor(int tileType) => IcicleAnchorTiles.Contains(tileType);

        /// <summary>瓦片是否为可折射星闪的晶质冰</summary>
        internal static bool IsCrystalIce(int tileType) => CrystalIceTiles.Contains(tileType);

        /// <summary>粉尘屏外剔除：只给屏幕边缘 200px 内的位置花粉尘预算</summary>
        internal static bool NearScreen(Vector2 pos) {
            return pos.X > Main.screenPosition.X - 200f
                && pos.X < Main.screenPosition.X + Main.screenWidth + 200f
                && pos.Y > Main.screenPosition.Y - 200f
                && pos.Y < Main.screenPosition.Y + Main.screenHeight + 200f;
        }

        /// <summary>城镇安宁：位置约 60 格内有存活城镇 NPC 时伤害性机制不触发</summary>
        internal static bool TownCalmNear(Vector2 pos) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(pos) < 960f) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>投递一条延迟回声（队列满则丢弃，宁缺毋噪）</summary>
        internal static bool EnqueueEcho(byte kind, int delay, Vector2 pos, float volume, float pitch) {
            for (int i = 0; i < EchoSlots; i++) {
                if (echoes[i].Delay > 0) {
                    continue;
                }
                echoes[i] = new Echo { Delay = delay, Kind = kind, Pos = pos, Volume = volume, Pitch = pitch };
                return true;
            }
            return false;
        }

        /// <summary>队列中仍在等待的回声数（镐击回声的防噪上限用）</summary>
        internal static int PendingEchoes() {
            int count = 0;
            for (int i = 0; i < EchoSlots; i++) {
                if (echoes[i].Delay > 0) {
                    count++;
                }
            }
            return count;
        }

        internal static void Update() {
            if (Main.dedServ || Main.gameMenu) {
                Presence = 0f;
                return;
            }

            //Boss 在场：纯视觉/声音氛围保留但减弱
            float target = In(Main.LocalPlayer) ? (CWRWorld.HasBoss ? 0.6f : 1f) : 0f;
            Presence = Math.Abs(target - Presence) < 0.004f
                ? target : MathHelper.Lerp(Presence, target, 0.045f);

            UpdateCaveWind();
            UpdateGroan();
            DrainEchoes();
        }

        //闷洞风底噪：有形无声的洞穴需要一层被雪吸掉高频的风
        private static void UpdateCaveWind() {
            if (Presence < 0.05f) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(caveWindSlot, out _)) {
                caveWindSlot = SoundEngine.PlaySound(CaveWindStyle, null, UpdateCaveWindLoop);
            }
        }

        private static bool UpdateCaveWindLoop(ActiveSound sound) {
            if (Presence < 0.01f || Main.gameMenu) {
                return false;
            }
            //声底查重（同源 BlizzardInsideBuildingLoop）：Sunkendune 呜咽风在 -0.52 带快呼吸包络、
            //Hollowdeep 耳鸣在 +0.9，这里取 -0.18 的更轻更薄冷气流，配极慢浅起伏拉开身份
            float breathe = 0.9f + 0.1f * MathF.Sin((float)Main.timeForVisualEffects * 0.004f);
            //岩层比泥土层更深更响一点
            float depth = Main.LocalPlayer.ZoneRockLayerHeight ? 1f : 0.8f;
            sound.Volume = 0.24f * Presence * depth * breathe;
            sound.Pitch = -0.18f;
            sound.Position = null;
            return true;
        }

        //空洞冰鸣：远处冰层开裂的一声脆响，随后洞体低频回响
        private static void UpdateGroan() {
            if (Presence < 0.55f || Main.gamePaused) {
                return;
            }
            if (--groanIn > 0) {
                return;
            }
            bool deep = Main.LocalPlayer.ZoneRockLayerHeight;
            groanIn = deep ? Main.rand.Next(430, 900) : Main.rand.Next(560, 1150);

            Vector2 pos = Main.LocalPlayer.Center
                + Main.rand.NextVector2Unit() * Main.rand.NextFloat(520f, 940f);
            SoundEngine.PlaySound(SoundID.DeerclopsIceAttack with {
                Volume = 0.32f * Presence, Pitch = -0.72f, MaxInstances = 3
            }, pos);
            //先脆响后低鸣：洞体在替冰层收尾
            EnqueueEcho(EchoRumble, 8, pos, 0.5f * Presence, -0.55f);
            EnqueueEcho(EchoCrack, 26, pos + new Vector2(Main.rand.NextFloat(-90f, 90f), 40f),
                0.12f * Presence, -0.55f);
        }

        private static void DrainEchoes() {
            for (int i = 0; i < EchoSlots; i++) {
                if (echoes[i].Delay <= 0) {
                    continue;
                }
                if (--echoes[i].Delay > 0) {
                    continue;
                }
                SoundStyle style = echoes[i].Kind switch {
                    EchoRumble => SoundID.WormDig,
                    EchoCrack => SoundID.Item27,
                    _ => SoundID.Tink,
                };
                SoundEngine.PlaySound(style with {
                    Volume = echoes[i].Volume, Pitch = echoes[i].Pitch, MaxInstances = 4
                }, echoes[i].Pos);
            }
        }

        internal static void Reset() {
            Presence = 0f;
            groanIn = 600;
            for (int i = 0; i < EchoSlots; i++) {
                echoes[i].Delay = 0;
            }
        }
    }

    internal class RimehollowAmbienceSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                RimehollowAmbience.Update();
            }
        }

        public override void ClearWorld() {
            if (!Main.dedServ) {
                RimehollowAmbience.Reset();
            }
        }
    }
}
