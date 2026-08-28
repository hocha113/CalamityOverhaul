using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Sporeshine
{
    /// <summary>
    /// 「蓝澜」发光蘑菇地氛围枢纽（纯本机演出层）：<br/>
    /// 环境声：湿洞低鸣+潮气风两条循环（进出淡入淡出），滴水/气泡咕噜一次性点缀；<br/>
    /// 踩菇反馈：任意玩家踏过地面小蘑菇时喷小孢环+荧光波纹（纯美观，各端本地自算）；<br/>
    /// 「菌歌」：夜里屏内大蘑菇低频发出风琴式和声，每株音高由自身位置决定。<br/>
    /// 光尘与波纹的绘制在 <see cref="SporeshineRender"/>，此处只产数据
    /// </summary>
    internal class SporeshineAmbience : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "GameModes";

        /// <summary>孢醉窒息的死亡原因（{0}=玩家名）</summary>
        internal static LocalizedText DazeDeathReason { get; private set; }

        /// <summary>本机在场强度 0~1（进出群系缓升缓降）</summary>
        internal static float Presence { get; private set; }

        /// <summary>Boss 在场时氛围整体收敛的有效强度</summary>
        internal static float EffectivePresence => Presence * (CWRWorld.HasBoss ? 0.55f : 1f);

        //==== 环境声 ====
        private static SlotId humSlot;
        private static SlotId dampSlot;
        private static readonly SoundStyle HumStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };
        private static readonly SoundStyle DampStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };
        private static int dripIn;
        private static int gurgleIn;

        //==== 踩菇反馈 ====
        /// <summary>逐槽位踩菇冷却（本机演出私产，槽位数组是世界级共享口径）</summary>
        private static readonly int[] stepCooldown = new int[256];

        //==== 荧光波纹注册表（渲染层只读） ====
        internal const int RippleMax = 24;
        internal struct Ripple
        {
            internal bool Active;
            internal Vector2 Pos;
            internal int Life;
            internal int MaxLife;
            /// <summary>0=踩菇地面波纹 1=菌歌圆晕</summary>
            internal byte Kind;
            internal float Seed;
        }
        internal static readonly Ripple[] Ripples = new Ripple[RippleMax];
        private static int rippleCursor;

        //==== 菌歌 ====
        private static int chorusIn;
        /// <summary>风琴音阶（低频五声，音高按菌株位置散列取用）</summary>
        private static readonly float[] ChorusPitches = [-0.6f, -0.4f, -0.2f, 0f, 0.2f];

        public override void SetStaticDefaults() {
            DazeDeathReason = this.GetLocalization(nameof(DazeDeathReason),
                () => "{0} drowsed away deep in the spore haze");
        }

        public override void ClearWorld() {
            Presence = 0f;
            for (int i = 0; i < Ripples.Length; i++) {
                Ripples[i].Active = false;
            }
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            float target = !Main.gameMenu && GameModeSystem.BrutalActive
                && Main.LocalPlayer.active && Main.LocalPlayer.ZoneGlowshroom ? 1f : 0f;
            Presence = MathHelper.Lerp(Presence, target, 0.035f);
            if (Presence < 0.004f && target <= 0f) {
                Presence = 0f;
            }

            //波纹推进不受在场门控（离开群系后余波仍要走完）
            for (int i = 0; i < Ripples.Length; i++) {
                if (Ripples[i].Active && ++Ripples[i].Life >= Ripples[i].MaxLife) {
                    Ripples[i].Active = false;
                }
            }

            if (Presence < 0.01f) {
                return;
            }
            UpdateAmbientLoops();
            UpdateOneShots();
            UpdateStepRings();
            UpdateChorus();
        }

        //==================== 环境声 ====================

        //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
        private static void UpdateAmbientLoops() {
            if (Main.gameMenu) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(humSlot, out _)) {
                humSlot = SoundEngine.PlaySound(HumStyle, null, UpdateHum);
            }
            if (!SoundEngine.TryGetActiveSound(dampSlot, out _)) {
                dampSlot = SoundEngine.PlaySound(DampStyle, null, UpdateDamp);
            }
        }

        //湿洞低鸣：菌群像在极缓地呼吸
        private static bool UpdateHum(ActiveSound sound) {
            if (Presence < 0.01f || Main.gameMenu) {
                return false;
            }
            sound.Volume = 0.15f * EffectivePresence;
            sound.Pitch = -0.72f;
            sound.Position = null;
            return true;
        }

        //潮气风：闷在洞里的湿风底噪
        private static bool UpdateDamp(ActiveSound sound) {
            if (Presence < 0.01f || Main.gameMenu) {
                return false;
            }
            sound.Volume = 0.085f * EffectivePresence;
            sound.Pitch = -0.35f;
            sound.Position = null;
            return true;
        }

        //滴水与蘑菇气泡咕噜：随机点缀在玩家四周
        private static void UpdateOneShots() {
            if (--dripIn <= 0) {
                dripIn = Main.rand.Next(90, 210);
                Vector2 at = Main.LocalPlayer.Center
                    + new Vector2(Main.rand.NextFloat(-540f, 540f), Main.rand.NextFloat(-320f, 320f));
                SoundEngine.PlaySound(SoundID.Drip with {
                    Volume = 0.34f * EffectivePresence,
                    Pitch = Main.rand.NextFloat(-0.25f, 0.1f),
                    MaxInstances = 3,
                }, at);
            }
            if (--gurgleIn <= 0) {
                gurgleIn = Main.rand.Next(140, 300);
                Vector2 at = Main.LocalPlayer.Center
                    + new Vector2(Main.rand.NextFloat(-480f, 480f), Main.rand.NextFloat(-260f, 260f));
                SoundEngine.PlaySound(SoundID.Item85 with {
                    Volume = 0.26f * EffectivePresence,
                    Pitch = Main.rand.NextFloat(-0.65f, -0.35f),
                    MaxInstances = 3,
                }, at);
            }
        }

        //==================== 踩菇反馈 ====================

        private static void UpdateStepRings() {
            //只遍历活跃玩家；离线槽的冷却残值最多 30 帧，无感
            foreach (Player player in Main.ActivePlayers) {
                int i = player.whoAmI;
                if (stepCooldown[i] > 0) {
                    stepCooldown[i]--;
                }
                if (player.dead || stepCooldown[i] > 0) {
                    continue;
                }
                if (player.velocity.Y != 0f || Math.Abs(player.velocity.X) < 1.2f) {
                    continue;
                }
                //离屏太远的玩家不算（纯美观，各端只演自己看得见的）
                if (Vector2.DistanceSquared(player.Center, Main.LocalPlayer.Center) > 1500f * 1500f) {
                    continue;
                }
                if (!FeetOnMushroomPlants(player)) {
                    continue;
                }
                stepCooldown[i] = 30;
                SpawnStepBurst(player);
            }
        }

        /// <summary>脚所在的瓦格里长着小蘑菇（植株占据地面块上方那格）</summary>
        private static bool FeetOnMushroomPlants(Player player) {
            int ty = (int)((player.Bottom.Y - 2f) / 16f);
            int txL = (int)(player.position.X / 16f);
            int txR = (int)((player.position.X + player.width) / 16f);
            for (int tx = txL; tx <= txR; tx++) {
                if (!WorldGen.InWorld(tx, ty, 10)) {
                    continue;
                }
                Tile tile = Main.tile[tx, ty];
                if (tile.HasTile && tile.TileType == TileID.MushroomPlants) {
                    return true;
                }
            }
            return false;
        }

        //小孢环喷出 + 荧光波纹沿地面扩散
        private static void SpawnStepBurst(Player player) {
            Vector2 feet = player.Bottom;
            SoundEngine.PlaySound(SoundID.Grass with {
                Volume = 0.42f * EffectivePresence,
                Pitch = 0.35f,
                MaxInstances = 3,
            }, feet);
            for (int i = 0; i < 9; i++) {
                float ang = MathHelper.Pi + MathHelper.Pi * (i + 0.5f) / 9f;//上半圈孢环
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(1.2f, 2.6f);
                Dust dust = Dust.NewDustPerfect(feet + new Vector2(0f, -4f), DustID.GlowingMushroom,
                    vel, 120, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
            AddRipple(feet + new Vector2(0f, -2f), 0);
        }

        /// <summary>登记一圈荧光波纹（覆写最旧槽位）</summary>
        internal static void AddRipple(Vector2 pos, byte kind) {
            ref Ripple r = ref Ripples[rippleCursor];
            rippleCursor = (rippleCursor + 1) % RippleMax;
            r.Active = true;
            r.Pos = pos;
            r.Life = 0;
            r.MaxLife = kind == 0 ? 42 : 66;
            r.Kind = kind;
            r.Seed = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        //==================== 菌歌（夜） ====================

        private static void UpdateChorus() {
            if (--chorusIn > 0) {
                return;
            }
            if (Main.dayTime || Presence < 0.5f) {
                chorusIn = 150;
                return;
            }
            chorusIn = 240 + Main.rand.Next(300);

            //屏内随机探大蘑菇，至多两株同拍成和声
            int found = 0;
            int screenL = (int)(Main.screenPosition.X / 16f) - 4;
            int screenT = (int)(Main.screenPosition.Y / 16f) - 4;
            int screenW = Main.screenWidth / 16 + 8;
            int screenH = Main.screenHeight / 16 + 8;
            for (int i = 0; i < 36 && found < 2; i++) {
                int tx = screenL + Main.rand.Next(screenW);
                int ty = screenT + Main.rand.Next(screenH);
                if (!WorldGen.InWorld(tx, ty, 10)) {
                    continue;
                }
                Tile tile = Main.tile[tx, ty];
                if (!tile.HasTile || tile.TileType != TileID.MushroomTrees) {
                    continue;
                }
                int top = ty;
                while (top > 10) {
                    Tile above = Main.tile[tx, top - 1];
                    if (!above.HasTile || above.TileType != TileID.MushroomTrees) {
                        break;
                    }
                    top--;
                }
                Vector2 cap = new(tx * 16f + 8f, top * 16f - 20f);
                //每株音高由自身位置决定（这株蘑菇永远唱这个音）
                int note = (tx * 7919 + top * 104729) % ChorusPitches.Length;
                if (note < 0) {
                    note += ChorusPitches.Length;
                }
                SoundEngine.PlaySound(SoundID.Item26 with {
                    Volume = (found == 0 ? 0.3f : 0.24f) * EffectivePresence,
                    Pitch = ChorusPitches[note],
                    MaxInstances = 4,
                }, cap);
                AddRipple(cap, 1);
                found++;
            }
            if (found == 0) {
                chorusIn = 120;//这一屏没有大蘑菇，早点再探
            }
        }
    }
}
