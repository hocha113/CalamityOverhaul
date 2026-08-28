using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Nyxdepth
{
    /// <summary>
    /// 「渊压」深渊常态氛围控制器（纯本地演出量，感官恐惧层）：<br/>
    /// 随玩家世界坐标 Y 深度渐强的三件套：屏边黑雾收束（粒子层，交给 <see cref="NyxdepthAmbientRender"/>，
    /// 不改光照引擎、不做全屏遮挡，深渊光照压制归灾厄本体）、耳鸣+水压闷响双循环、
    /// 心跳随深度加快（与 Fleshfen 的随血量心跳刻意区分：这里只看深度，不读生命值）。<br/>
    /// 另持「压裂脆响」：深层随机方位传来的水压脆响+极轻屏震，纯氛围无判定。<br/>
    /// 档位只调渊压渐强曲线指数，机制形状不变。全部只在本机客户端推进
    /// </summary>
    internal class NyxdepthAmbience : ModSystem
    {
        /// <summary>本地在场强度 0~1（进出深渊缓升缓降，Boss 在场压到 0.55 保留但减弱）</summary>
        public static float Presence { get; private set; }

        /// <summary>深度经档位曲线整形后的 0~1（平滑跟随，离场缓降）</summary>
        public static float DepthGrade { get; private set; }

        /// <summary>渊压总驱动 0~1：DepthGrade × Presence，全部感官层从这里取强度</summary>
        public static float Pressure { get; private set; }

        /// <summary>渊压渐强曲线指数（档位唯一旋钮之一：残酷晚起势 / 修罗居中 / 毁灭早起势）</summary>
        private static readonly float[] PressureExpByTier = [1.45f, 1.15f, 0.92f];

        //环境声循环槽（镜像 OldNetAmbience 的 SlotId+回调惯例）
        private static SlotId rumbleSlot;
        private static SlotId tinnitusSlot;
        /// <summary>水压闷响：室内暴雪循环压到最低音高，读作深水闷压</summary>
        private static readonly SoundStyle RumbleStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };
        /// <summary>耳鸣：传送门待机循环拔高音高成细鸣，深层才浮现</summary>
        private static readonly SoundStyle TinnitusStyle =
            SoundID.DD2_EtherianPortalIdleLoop with { IsLooped = true, MaxInstances = 1 };

        //心跳双响（lub-dub 拆两帧，与克脑的同帧双响区分节奏感）
        private static readonly SoundStyle LubStyle = SoundID.DD2_OgreGroundPound with {
            MaxInstances = 3,
            SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
        };
        private static readonly SoundStyle DubStyle = SoundID.DD2_MonkStaffGroundImpact with {
            MaxInstances = 3,
            SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
        };

        private static float depthSmooth;
        private static int beatTimer;
        private static int dubTimer = -1;
        private static float lastBeatVol;
        private static int crackTimer = 600;
        private static int echoTimer = -1;
        private static Vector2 echoPos;

        /// <summary>本机玩家是否处在生效环境（CWRRef 守门 → 残酷旗标 → 灾厄深渊区）</summary>
        private static bool LocalEligible() {
            if (Main.gameMenu || Main.dedServ) {
                return false;
            }
            if (!CWRRef.Has || !GameModeSystem.BrutalActive) {
                return false;
            }
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) {
                return false;
            }
            return player.GetPlayerZoneAbyss();
        }

        /// <summary>以世界坐标 Y 分层：海底下方起势，近地狱层封顶</summary>
        private static float RawDepth(Player player) {
            float tileY = player.Center.Y / 16f;
            float top = (float)Main.worldSurface + 80f;
            float bottom = Main.UnderworldLayer - 40f;
            if (bottom <= top + 40f) {
                return 0f;//异形小世界护栏
            }
            return MathHelper.Clamp((tileY - top) / (bottom - top), 0f, 1f);
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }

            bool eligible = LocalEligible();
            float target = eligible ? (CWRWorld.HasBoss ? 0.55f : 1f) : 0f;
            if (Main.gameMenu) {
                Presence = 0f;
            }
            else {
                Presence = MathHelper.Lerp(Presence, target, 0.035f);
                if (Presence < 0.004f && target <= 0f) {
                    Presence = 0f;
                }
            }

            //深度平滑：进出深渊与上下移动都不会硬切声画强度
            float raw = eligible ? RawDepth(Main.LocalPlayer) : 0f;
            depthSmooth = MathHelper.Lerp(depthSmooth, raw, 0.05f);
            int tier = Math.Clamp(GameModeSystem.EffectiveTier, 1, 3);
            DepthGrade = MathF.Pow(depthSmooth, PressureExpByTier[tier - 1]);
            Pressure = DepthGrade * Presence;

            //凝视自带在场检查（Pressure 掉底自动驱散），无条件推进
            NyxdepthGaze.Update();

            if (Presence <= 0.004f) {
                return;
            }
            UpdateAmbientLoops();
            UpdateHeartbeat();
            UpdateCracks();
        }

        //==================== 环境声循环 ====================

        /// <summary>循环丢失（切场景/音量档变化）就补挂，音量在回调里逐帧走</summary>
        private static void UpdateAmbientLoops() {
            if (Main.gameMenu) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(rumbleSlot, out _)) {
                rumbleSlot = SoundEngine.PlaySound(RumbleStyle, null, UpdateRumble);
            }
            if (!SoundEngine.TryGetActiveSound(tinnitusSlot, out _)) {
                tinnitusSlot = SoundEngine.PlaySound(TinnitusStyle, null, UpdateTinnitus);
            }
        }

        /// <summary>水压闷响：入渊即有一层薄底，随深度压满</summary>
        private static bool UpdateRumble(ActiveSound sound) {
            if (Main.gameMenu || Presence <= 0.003f) {
                return false;
            }
            sound.Volume = (0.08f + 0.42f * DepthGrade) * Presence;
            sound.Pitch = -0.9f;
            sound.Position = null;
            return true;
        }

        /// <summary>耳鸣：浅层近乎无声，越深越尖细</summary>
        private static bool UpdateTinnitus(ActiveSound sound) {
            if (Main.gameMenu || Presence <= 0.003f) {
                return false;
            }
            float deep = MathHelper.Clamp((DepthGrade - 0.22f) / 0.78f, 0f, 1f);
            sound.Volume = 0.15f * deep * Presence;
            sound.Pitch = 0.88f;
            sound.Position = null;
            return true;
        }

        //==================== 心跳（随深度加快） ====================

        private static void UpdateHeartbeat() {
            //dub：lub 之后 9 帧的第二音，稍轻稍高
            if (dubTimer >= 0 && --dubTimer < 0) {
                SoundEngine.PlaySound(DubStyle with {
                    Volume = lastBeatVol * 0.58f,
                    Pitch = -0.5f + Pressure * 0.12f
                });
            }
            if (Pressure < 0.08f) {
                beatTimer = 30;
                return;
            }
            if (--beatTimer > 0) {
                return;
            }
            //深度直接换算节拍：入渊约 96 帧一拍，最深压到 44 帧
            beatTimer = (int)MathHelper.Lerp(96f, 44f, Pressure);
            lastBeatVol = 0.24f + 0.40f * Pressure;
            //不给位置：心跳在颅内响，不做方位声
            SoundEngine.PlaySound(LubStyle with {
                Volume = lastBeatVol,
                Pitch = -0.86f + Pressure * 0.10f
            });
            dubTimer = 9;
        }

        //==================== 压裂脆响（方位声+极轻屏震，纯氛围） ====================

        private static void UpdateCracks() {
            if (echoTimer >= 0 && --echoTimer < 0) {
                PlayCrack(echoPos, 0.5f, true);
            }
            if (Pressure < 0.30f) {
                return;
            }
            if (--crackTimer > 0) {
                return;
            }
            crackTimer = Main.rand.Next(660, 1400) - (int)(Pressure * 260f);
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 pos = Main.LocalPlayer.Center + angle.ToRotationVector2() * Main.rand.NextFloat(520f, 980f);
            PlayCrack(pos, 1f, false);
            //三成概率更远处补一记回响，读作裂纹在岩体里传开
            if (Main.rand.NextBool(3)) {
                echoTimer = Main.rand.Next(22, 46);
                echoPos = Main.LocalPlayer.Center + angle.ToRotationVector2() * Main.rand.NextFloat(760f, 1200f);
            }
        }

        /// <summary>冰裂质感的水压脆响：脆裂双层+主响垫一记深部闷击，主响附带极轻屏震</summary>
        private static void PlayCrack(Vector2 pos, float mul, bool echo) {
            float vol = (0.40f + 0.30f * Pressure) * mul;
            SoundEngine.PlaySound(SoundID.Item27 with {
                Volume = vol,
                Pitch = Main.rand.NextFloat(-0.75f, -0.5f),
                MaxInstances = 3
            }, pos);
            SoundEngine.PlaySound(SoundID.Item50 with {
                Volume = vol * 0.7f,
                Pitch = -0.3f,
                MaxInstances = 3
            }, pos);
            if (!echo) {
                SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with {
                    Volume = 0.20f + 0.14f * Pressure,
                    Pitch = -0.95f,
                    MaxInstances = 2
                }, pos);
                //极轻：常规命中震感在 5 以上，这里只给 1~2
                Main.LocalPlayer.CWR()?.GetScreenShake(1.0f + Pressure);
            }
        }

        public override void ClearWorld() {
            Presence = 0f;
            DepthGrade = 0f;
            Pressure = 0f;
            depthSmooth = 0f;
            beatTimer = 0;
            dubTimer = -1;
            lastBeatVol = 0f;
            crackTimer = 600;
            echoTimer = -1;
            if (!Main.dedServ) {
                NyxdepthGaze.Reset();
                NyxdepthAmbientRender.ClearWisps();
            }
        }
    }
}
