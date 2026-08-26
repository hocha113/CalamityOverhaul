using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Stratos.Projectiles;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Stratos
{
    /// <summary>
    /// 「稀空」在场强度：本机玩家处于残酷模式太空高层时缓升缓降的演出量，
    /// 星尘微粒、流星划痕、薄风声都由它统一门控；Boss 在场压低不清零（纯视觉减弱保留）
    /// </summary>
    internal static class StratosAmbience
    {
        /// <summary>本地屏幕在场强度 0~1</summary>
        public static float Presence { get; private set; }

        /// <summary>仍需在场（含渐出尾巴）</summary>
        public static bool Visible => Presence > 0.01f;

        internal static void Update() {
            if (Main.dedServ || Main.gameMenu) {
                Presence = 0f;
                return;
            }
            Player local = Main.LocalPlayer;
            float target = 0f;
            if (GameModeSystem.BrutalActive && local.active && local.ZoneSkyHeight) {
                target = CWRWorld.HasBoss ? 0.55f : 1f;
            }
            Presence = Math.Abs(target - Presence) < 0.008f
                ? target : MathHelper.Lerp(Presence, target, 0.045f);
        }

        internal static void Reset() => Presence = 0f;
    }

    /// <summary>
    /// 太空高层环境总控：客户端负责「稀空」氛围（薄风声循环、缺氧呼吸声、星尘微粒），
    /// 权威端负责「坠星」与「星屑升流」的低频调度。
    /// 决策只在权威端跑，客户端一律通过同步弹幕实体看到结果；
    /// 档位只调坠星频率（气薄速度在 <see cref="StratosPlayer"/>），升流节奏档位无关
    /// </summary>
    internal class StratosAmbienceSystem : ModSystem
    {
        //==== 坠星 ====
        /// <summary>坠星冷却，档位只调频率不换机制</summary>
        private static readonly int[] MeteorCooldownByTier = [1650, 1250, 920];
        /// <summary>坠星全局并发上限</summary>
        private const int MeteorCap = 4;
        /// <summary>落点距玩家的最小横向偏移：大于爆炸半径，永不点名玩家坐标</summary>
        private const float MeteorMinOffset = 180f;
        private const float MeteorMaxOffset = 620f;
        /// <summary>坠星伤害 = 高空原版敌怪接触伤害基线 × 此值</summary>
        private const float MeteorDamageFrac = 0.55f;
        /// <summary>入区宽限：刚上高空不会立刻挨砸</summary>
        private const int MeteorEntryGrace = 900;

        //==== 星屑升流 ====
        /// <summary>升流冷却（档位无关：档位只调坠星频率与气薄累积速度）</summary>
        private const int UpdraftCooldown = 1080;
        /// <summary>升流柱全局并发上限</summary>
        private const int UpdraftCap = 3;
        private const int UpdraftEntryGrace = 240;

        //==== 通用 ====
        /// <summary>触发条件不满足时的复查间隔</summary>
        private const int RetryFrames = 45;
        /// <summary>城镇安宁半径（60 格）：圈内有存活城镇 NPC 则伤害机制不触发</summary>
        private const float TownSafeRadius = 960f;

        /// <summary>逐玩家坠星计时（服务端决策私产，索引 whoAmI；0=未入区）</summary>
        private readonly int[] meteorTimer = new int[Main.maxPlayers];
        /// <summary>逐玩家升流计时（同上）</summary>
        private readonly int[] updraftTimer = new int[Main.maxPlayers];

        //环境声循环槽（镜像 OldNetAmbience 的 SlotId+回调惯例）
        private static SlotId thinWindSlot;
        private static SlotId breathSlot;
        /// <summary>薄风：暴风雪嘶声拉高音调，比地表风更薄更冷</summary>
        private static readonly SoundStyle ThinWindStyle =
            SoundID.BlizzardStrongLoop with { IsLooped = true, MaxInstances = 1 };
        /// <summary>呼吸：屋内闷风循环加节律起伏，读作面罩里渐重的喘息</summary>
        private static readonly SoundStyle BreathStyle =
            SoundID.BlizzardInsideBuildingLoop with { IsLooped = true, MaxInstances = 1 };

        public override void PostUpdateEverything() {
            if (!Main.dedServ) {
                StratosAmbience.Update();
                UpdateAmbientLoops();
                UpdateMotes();
            }
            if (GameModeSystem.BrutalActive && (VaultUtils.isServer || VaultUtils.isSinglePlayer)) {
                UpdateSchedulers();
            }
        }

        public override void ClearWorld() {
            Array.Clear(meteorTimer, 0, meteorTimer.Length);
            Array.Clear(updraftTimer, 0, updraftTimer.Length);
            StratosAmbience.Reset();
        }

        //==================== 权威端调度 ====================

        private void UpdateSchedulers() {
            int tier = Math.Clamp(GameModeSystem.EffectiveTier, 1, 3);
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (!player.active || player.dead || !player.ZoneSkyHeight) {
                    meteorTimer[i] = 0;
                    updraftTimer[i] = 0;//离区归零，回区重走宽限
                    continue;
                }
                if (CWRWorld.HasBoss) {
                    continue;//Boss 在场：伤害/位移机制暂停，计时冻结
                }

                if (meteorTimer[i] <= 0) {
                    meteorTimer[i] = MeteorEntryGrace + Main.rand.Next(600);
                }
                else if (--meteorTimer[i] == 0) {
                    meteorTimer[i] = TrySpawnMeteor(player)
                        ? MeteorCooldownByTier[tier - 1] + Main.rand.Next(300) : RetryFrames;
                }

                if (updraftTimer[i] <= 0) {
                    updraftTimer[i] = UpdraftEntryGrace + Main.rand.Next(240);
                }
                else if (--updraftTimer[i] == 0) {
                    updraftTimer[i] = TrySpawnUpdraft(player)
                        ? UpdraftCooldown + Main.rand.Next(420) : RetryFrames;
                }
            }
        }

        /// <summary>统计某类弹幕的活动实例数（只在冷却尽头调用，非每帧）</summary>
        private static int CountActive(int projType, int stopAt = 16) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>目标附近 60 格内有存活城镇 NPC（城镇安宁：伤害机制不触发）</summary>
        private static bool TownNearby(Vector2 center) {
            float radiusSq = TownSafeRadius * TownSafeRadius;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && Vector2.DistanceSquared(npc.Center, center) <= radiusSq) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 坠星：落点在玩家横向 180~620px 的随机偏移处（永不点名玩家坐标），
        /// 优先吸附下方地表，找不到则在云层高度空爆。伤害基线取高空原版敌怪接触伤害
        /// （鸟妖 25 / 飞龙 50）× 难度系数，镜像 DamageFrac 写法
        /// </summary>
        private static bool TrySpawnMeteor(Player target) {
            if (CountActive(ModContent.ProjectileType<StratosFallingStarProj>()) >= MeteorCap) {
                return false;
            }
            if (TownNearby(target.Center)) {
                return false;
            }

            float offset = (Main.rand.NextBool() ? 1f : -1f) * Main.rand.NextFloat(MeteorMinOffset, MeteorMaxOffset);
            float landX = target.Center.X + offset;
            int tileX = (int)(landX / 16f);
            if (tileX < 60 || tileX > Main.maxTilesX - 60) {
                //贴世界边缘就翻向另一侧，仍保持最小偏移
                landX = target.Center.X - offset;
                tileX = (int)(landX / 16f);
                if (tileX < 60 || tileX > Main.maxTilesX - 60) {
                    return false;
                }
            }

            //地面扫描：从玩家上方一段开始向下找可承接的地表（浮岛面）；扫不到就空爆
            Vector2 basePos = default;
            bool grounded = false;
            int startY = (int)((target.Center.Y - 280f) / 16f);
            for (int dy = 0; dy < 100; dy++) {
                int tileY = startY + dy;
                if (!WorldGen.InWorld(tileX, tileY, 10) || tileY * 16f > target.Center.Y + 680f) {
                    break;
                }
                if (WorldGen.SolidTile(tileX, tileY)) {
                    basePos = new Vector2(tileX * 16f + 8f, tileY * 16f);
                    grounded = true;
                    break;
                }
            }
            if (!grounded) {
                basePos = new Vector2(landX, target.Center.Y + Main.rand.NextFloat(-100f, 180f));
            }

            float contactBase = (Main.hardMode ? 50f : 25f) * Main.GameModeInfo.EnemyDamageMultiplier;
            int damage = (int)(contactBase * MeteorDamageFrac);
            //ai[0]=天穹起点横向偏斜 ai[1]=空爆标记
            Projectile.NewProjectile(new EntitySource_Misc("CWR_StratosSkyfall"), basePos, Vector2.Zero,
                ModContent.ProjectileType<StratosFallingStarProj>(), damage, 2f, Main.myPlayer,
                Main.rand.NextFloat(-150f, 150f), grounded ? 0f : 1f);
            return true;
        }

        /// <summary>星屑升流：在玩家侧向随机立一根可见上升气流柱（无伤害的甜头场地）</summary>
        private static bool TrySpawnUpdraft(Player target) {
            if (CountActive(ModContent.ProjectileType<StratosUpdraftProj>()) >= UpdraftCap) {
                return false;
            }
            float offset = (Main.rand.NextBool() ? 1f : -1f) * Main.rand.NextFloat(150f, 520f);
            Vector2 bottom = new(target.Center.X + offset, target.Center.Y + Main.rand.NextFloat(140f, 340f));
            //ai[0]=柱半宽 ai[1]=存续帧 ai[2]=柱高
            Projectile.NewProjectile(new EntitySource_Misc("CWR_StratosUpdraft"), bottom, Vector2.Zero,
                ModContent.ProjectileType<StratosUpdraftProj>(), 0, 0f, Main.myPlayer,
                54f, 430f, 760f);
            return true;
        }

        //==================== 客户端氛围 ====================

        //循环丢失（切场景/音量档变化）就补挂；音量在回调里逐帧走
        private static void UpdateAmbientLoops() {
            if (Main.gameMenu || !StratosAmbience.Visible) {
                return;
            }
            if (!SoundEngine.TryGetActiveSound(thinWindSlot, out _)) {
                thinWindSlot = SoundEngine.PlaySound(ThinWindStyle, null, UpdateThinWind);
            }
            StratosPlayer local = Main.LocalPlayer.GetModPlayer<StratosPlayer>();
            if (local.BreathLoud > 0.02f && !SoundEngine.TryGetActiveSound(breathSlot, out _)) {
                breathSlot = SoundEngine.PlaySound(BreathStyle, null, UpdateBreath);
            }
        }

        /// <summary>薄风啸：音调拉高读作稀薄高频，音量随在场强度与风速</summary>
        private static bool UpdateThinWind(ActiveSound sound) {
            float presence = StratosAmbience.Presence;
            if (Main.gameMenu || presence <= 0.01f) {
                return false;
            }
            sound.Volume = presence * (0.22f + 0.08f * Math.Min(Math.Abs(Main.windSpeedCurrent), 1f));
            sound.Pitch = 0.62f;
            sound.Position = null;
            return true;
        }

        /// <summary>呼吸渐重：随缺氧深度增强，节律起伏与渐晕脉动同一时钟</summary>
        private static bool UpdateBreath(ActiveSound sound) {
            StratosPlayer local = Main.LocalPlayer.GetModPlayer<StratosPlayer>();
            float loud = local.BreathLoud;
            if (Main.gameMenu || loud <= 0.01f) {
                return false;
            }
            sound.Volume = loud * (0.10f + 0.26f * local.BreathWave);
            sound.Pitch = -0.42f;
            sound.Position = null;
            return true;
        }

        /// <summary>星尘微粒：屏内低密度漂浮星屑（约 7.5 粒/秒），随风缓移</summary>
        private static void UpdateMotes() {
            if (Main.gamePaused || StratosAmbience.Presence < 0.25f || !Main.rand.NextBool(8)) {
                return;
            }
            Vector2 pos = Main.screenPosition + new Vector2(
                Main.rand.NextFloat(Main.screenWidth), Main.rand.NextFloat(Main.screenHeight));
            Dust dust = Dust.NewDustPerfect(pos, DustID.YellowStarDust,
                new Vector2(Main.windSpeedCurrent * 1.5f + Main.rand.NextFloat(-0.15f, 0.15f),
                    Main.rand.NextFloat(0.05f, 0.4f)),
                170, default, Main.rand.NextFloat(0.5f, 0.9f));
            dust.noGravity = true;
        }
    }
}
