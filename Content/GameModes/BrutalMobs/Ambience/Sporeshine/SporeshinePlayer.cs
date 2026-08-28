using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Sporeshine.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Sporeshine
{
    /// <summary>
    /// 发光蘑菇地逐玩家状态：两条互不相扰的职责线。<br/>
    /// 「巨菇喷发」调度（权威端私产）：周期采样玩家附近的大蘑菇瓦片，锁点起一株菌盖蓄胀；<br/>
    /// 「孢醉」计量（本机端私产）：孢雾浓区滞留累积迷醉，满则短暂咳嗽+微量伤害后清零，
    /// 快速通过无事。逐玩家数据全部住在实例字段上，不落 static
    /// </summary>
    internal class SporeshinePlayer : ModPlayer
    {
        //==== 巨菇喷发调度（权威端） ====
        /// <summary>喷发冷却，档位只调频率不换机制</summary>
        private static readonly int[] EruptCooldownByTier = [640, 520, 400];
        /// <summary>菌盖蓄胀全局并发上限</summary>
        private const int SwellCap = 3;
        /// <summary>条件不满足时的复查间隔</summary>
        private const int RetryFrames = 45;
        /// <summary>被 Boss/城镇安宁挡下时的复查间隔</summary>
        private const int BlockedRetryFrames = 90;
        /// <summary>喷发点与玩家的距离窗（像素）</summary>
        private const float EruptMinRange = 120f;
        private const float EruptMaxRange = 832f;
        /// <summary>单次采样的瓦片探针数</summary>
        private const int ProbeCount = 40;
        /// <summary>城镇安宁半径（约 60 格）</summary>
        private const float TownPeaceRange = 960f;
        /// <summary>两株蓄胀菌盖的最小间距</summary>
        private const float SwellSpacing = 120f;

        //==== 孢醉计量（本机端） ====
        /// <summary>浓雾内积满所需帧数（约 2.5 秒滞留）</summary>
        private const int DazeFillFrames = 150;
        /// <summary>雾外散去所需帧数（快速通过无事）</summary>
        private const int DazeDecayFrames = 90;
        /// <summary>积满时的微量伤害（走防御结算）</summary>
        private static int DazeDamage => Main.hardMode ? 30 : 15;

        /// <summary>喷发冷却；权威端决策私产，客户端不得用它驱动画面</summary>
        private int eruptTimer;
        /// <summary>孢醉计量 0..1；本机端私产</summary>
        private float daze;
        /// <summary>孢醉显示值（平滑），供屏边视觉读取</summary>
        private float dazeVisual;
        /// <summary>第二声咳嗽的延迟拍</summary>
        private int coughDelay;

        /// <summary>屏边蓝光柔化的驱动值（只对本机玩家有意义）</summary>
        internal float DazeVisual => dazeVisual;

        public override void Initialize() {
            eruptTimer = 300 + Main.rand.Next(300);
            daze = 0f;
            dazeVisual = 0f;
            coughDelay = 0;
        }

        public override void PostUpdate() {
            if (GameModeSystem.EffectiveTier <= 0) {
                daze = 0f;
                dazeVisual = 0f;
                return;
            }
            UpdateDazeLocal();
            UpdateEruptionAuthority();
        }

        public override void UpdateDead() {
            //死亡帧不跑 PostUpdate：迷醉随人一起散
            daze = 0f;
            dazeVisual *= 0.9f;
            coughDelay = 0;
        }

        //==================== 孢醉（本机端） ====================

        private void UpdateDazeLocal() {
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }

            //浓雾滞留判定：读同步弹幕的浓雾窗口，几何与雾判定同源（宽限带内的稀雾不积醉）
            bool dense = false;
            if (Player.ZoneGlowshroom && !CWRWorld.HasBoss) {
                int fogType = ModContent.ProjectileType<SporeshineSporeFogProj>();
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.type != fogType || proj.ModProjectile is not SporeshineSporeFogProj fog || !fog.DenseNow) {
                        continue;
                    }
                    float r = fog.HurtRadius;
                    if (Vector2.DistanceSquared(Player.Center, proj.Center) < r * r) {
                        dense = true;
                        break;
                    }
                }
            }

            daze = MathHelper.Clamp(daze + (dense ? 1f / DazeFillFrames : -1f / DazeDecayFrames), 0f, 1f);
            dazeVisual = MathHelper.Lerp(dazeVisual, daze, 0.08f);

            if (coughDelay > 0 && --coughDelay == 0) {
                //第二声咳嗽（补的一口）
                SoundEngine.PlaySound(SoundID.DoubleJump with { Volume = 0.5f, Pitch = -0.35f, MaxInstances = 3 }, Player.Center);
            }

            if (daze < 1f) {
                return;
            }
            //积满：咳嗽双响+头部孢尘喷出+微量伤害，随后清零重新累积
            daze = 0f;
            coughDelay = 7;
            SoundEngine.PlaySound(SoundID.DoubleJump with { Volume = 0.6f, Pitch = -0.55f, MaxInstances = 3 }, Player.Center);
            Vector2 head = Player.Top + new Vector2(0f, 6f);
            for (int i = 0; i < 7; i++) {
                Dust dust = Dust.NewDustPerfect(head, DustID.GlowingMushroom,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(0.5f, 2f)),
                    120, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = true;
            }
            PlayerDeathReason reason = PlayerDeathReason.ByCustomReason(
                SporeshineAmbience.DazeDeathReason.ToNetworkText(Player.name));
            Player.Hurt(reason, DazeDamage, 0);
        }

        //==================== 巨菇喷发（权威端） ====================

        private void UpdateEruptionAuthority() {
            if (!VaultUtils.isServer && !VaultUtils.isSinglePlayer) {
                return;
            }
            if (eruptTimer > 0) {
                eruptTimer--;
                return;
            }
            eruptTimer = RetryFrames;
            if (!Player.ZoneGlowshroom || Player.dead) {
                eruptTimer = BlockedRetryFrames;
                return;
            }
            //Boss 在场/城镇安宁：伤害性机制不触发（氛围保留）
            if (CWRWorld.HasBoss || TownNpcNear(Player.Center)) {
                eruptTimer = BlockedRetryFrames;
                return;
            }
            int swellType = ModContent.ProjectileType<SporeshineCapSwellProj>();
            if (CountActive(swellType) >= SwellCap) {
                eruptTimer = 60;
                return;
            }
            if (!TryFindCap(out Vector2 capAnchor)) {
                return;//45 帧后重试
            }

            int tier = GameModeSystem.EffectiveTier;
            //落点在预告生成帧锁死（预告即承诺），走位可避
            Projectile.NewProjectile(Player.GetSource_Misc("SporeshineErupt"), capAnchor, Vector2.Zero,
                swellType, 0, 0f, Main.myPlayer, Player.Center.X, Player.Center.Y, tier);
            eruptTimer = EruptCooldownByTier[tier - 1] + Main.rand.Next(90);
        }

        /// <summary>随机探针找玩家附近的大蘑菇，命中后上溯到菌柱顶作锚点</summary>
        private bool TryFindCap(out Vector2 capAnchor) {
            capAnchor = default;
            Point center = Player.Center.ToTileCoordinates();
            for (int i = 0; i < ProbeCount; i++) {
                int tx = center.X + Main.rand.Next(6, 53) * (Main.rand.NextBool() ? 1 : -1);
                int ty = center.Y + Main.rand.Next(-26, 27);
                if (!WorldGen.InWorld(tx, ty, 10)) {
                    continue;
                }
                Tile tile = Main.tile[tx, ty];
                if (!tile.HasTile || tile.TileType != TileID.MushroomTrees) {
                    continue;
                }
                //上溯到菌柱顶格（大蘑菇是树类瓦片，只有一列柱身）
                int top = ty;
                while (top > 10) {
                    Tile above = Main.tile[tx, top - 1];
                    if (!above.HasTile || above.TileType != TileID.MushroomTrees) {
                        break;
                    }
                    top--;
                }
                Vector2 anchor = new(tx * 16f + 8f, top * 16f - 22f);
                float dist = Vector2.Distance(anchor, Player.Center);
                if (dist < EruptMinRange || dist > EruptMaxRange) {
                    continue;
                }
                if (SwellNear(anchor)) {
                    continue;//同一株/近旁已在蓄胀
                }
                capAnchor = anchor;
                return true;
            }
            return false;
        }

        /// <summary>附近已有蓄胀体（避免同株叠加）</summary>
        private static bool SwellNear(Vector2 pos) {
            int swellType = ModContent.ProjectileType<SporeshineCapSwellProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == swellType && Vector2.DistanceSquared(proj.Center, pos) < SwellSpacing * SwellSpacing) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>城镇安宁：玩家附近有存活城镇 NPC</summary>
        private static bool TownNpcNear(Vector2 pos) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.townNPC && npc.Distance(pos) < TownPeaceRange) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>统计某类弹幕的活动实例数（到 stopAt 提前退出；只在冷却尽头调用，非每帧）</summary>
        internal static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }
    }
}
