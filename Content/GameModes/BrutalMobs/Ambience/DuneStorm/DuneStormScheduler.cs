using CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.DuneStorm.Projectiles;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.DuneStorm
{
    /// <summary>
    /// 风堑与沙鞭的权威端调度（决策与敌对弹幕生成只在服务端/单人跑，客户端经同步弹幕看到状态）：
    /// 风堑按间歇为每个暴露在地表沙漠的玩家起一道阵风波（同点位去重）；
    /// 沙鞭仅在原版 Sandstorm 事件期间调度，先落无害预告体再由预告体提交。
    /// Boss 在场与城镇安宁一律跳过；档位只调频率，不换机制形状
    /// </summary>
    internal class DuneStormScheduler : ModSystem
    {
        /// <summary>同一玩家附近已有风堑波时的去重半径</summary>
        private const float GustDedupeRange = 1100f;
        /// <summary>沙鞭预告体离玩家的横向落点范围（像素）</summary>
        private const float LashMinOffset = 220f;
        private const float LashMaxOffset = 380f;
        /// <summary>沙鞭方向的仰角约束（弧度，向上为负）：保证始终斜向上扬</summary>
        private const float LashSteepest = -1.35f;
        private const float LashShallowest = -0.45f;

        private static int gustTimer;
        private static int lashTimer;

        public override void PostUpdateEverything() {
            //决策只在权威端
            if (!VaultUtils.isServer && !VaultUtils.isSinglePlayer) {
                return;
            }
            if (!DuneStorm.MechanicsAllowed) {
                return;
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }

            if (--gustTimer <= 0) {
                gustTimer = DuneStorm.GustIntervalByTier[tier - 1] + Main.rand.Next(240);
                TryGustRound(tier);
            }

            //沙鞭只属于沙暴事件；事件外计时器挂起，入场即从满间隔起算
            if (!Sandstorm.Happening) {
                lashTimer = DuneStorm.LashIntervalByTier[tier - 1];
                return;
            }
            if (--lashTimer <= 0) {
                lashTimer = DuneStorm.LashIntervalByTier[tier - 1] + Main.rand.Next(180);
                TryLashRound(tier);
            }
        }

        /// <summary>风堑一轮：为每个符合条件的玩家落一道阵风波（波内已有人则不重复）</summary>
        private static void TryGustRound(int tier) {
            int waveType = ModContent.ProjectileType<DuneStormGustWaveProj>();
            //风向绑定本轮：无风时随机取向（离散决策只在权威端掷，经 ai 同步）
            float dir = DuneStorm.WindDir();
            if (dir == 0f) {
                dir = Main.rand.NextBool() ? 1f : -1f;
            }

            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost || !DuneStorm.InSurfaceDesert(player)) {
                    continue;
                }
                if (DuneStorm.TownCalm(player.Center)) {
                    continue;
                }
                if (DuneStorm.CountActive(waveType, DuneStorm.GustCap) >= DuneStorm.GustCap) {
                    return;
                }
                //同点位去重：附近已有波的玩家共享那道波
                bool covered = false;
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.type == waveType && proj.Distance(player.Center) < GustDedupeRange) {
                        covered = true;
                        break;
                    }
                }
                if (covered) {
                    continue;
                }
                Projectile.NewProjectile(player.GetSource_Misc("CWR_DuneStormGust"), player.Center,
                    Vector2.Zero, waveType, 0, 0f, Main.myPlayer, dir, tier);
            }
        }

        /// <summary>沙鞭一轮：在沙暴中的玩家侧翼找沙地落预告体（预告即承诺，方向此刻锁死）</summary>
        private static void TryLashRound(int tier) {
            int omenType = ModContent.ProjectileType<DuneStormSandLashOmen>();
            int lashType = ModContent.ProjectileType<DuneStormSandLashProj>();

            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost || !DuneStorm.InSurfaceDesert(player) || !player.ZoneSandstorm) {
                    continue;
                }
                if (DuneStorm.TownCalm(player.Center)) {
                    continue;
                }
                if (DuneStorm.CountActive(omenType, DuneStorm.LashOmenCap) >= DuneStorm.LashOmenCap
                    || DuneStorm.CountActive(lashType, DuneStorm.LashCap) >= DuneStorm.LashCap) {
                    return;
                }

                //侧翼落点：优先上风侧（鞭随沙暴来），找不到沙地则本轮放过该玩家
                float side = DuneStorm.WindDir();
                if (side == 0f) {
                    side = Main.rand.NextBool() ? 1f : -1f;
                }
                //鞭从上风起，扫向玩家：落点取上风侧（风来的方向）
                float offset = Main.rand.NextFloat(LashMinOffset, LashMaxOffset) * -side;
                int tileX = (int)((player.Center.X + offset) / 16f);
                int startY = (int)(player.Bottom.Y / 16f) - 10;
                if (!DuneStorm.TryFindGround(tileX, startY, out Vector2 ground)) {
                    continue;
                }
                if (!DuneStorm.IsSandFamily(Framing.GetTileSafely(tileX, (int)(ground.Y / 16f)).TileType)) {
                    continue;
                }

                //方向在预告生成帧锁死（预告即承诺），并钳进斜向仰角带
                Vector2 aimFrom = ground - Vector2.UnitY * 8f;
                float aim = (player.Center - aimFrom).ToRotation();
                if (side > 0f) {
                    //右扫（向 +X 上扬）
                    aim = MathHelper.Clamp(aim, LashSteepest, LashShallowest);
                }
                else {
                    //左扫（向 -X 上扬）：镜像到右扫带钳制后再镜像回去
                    float mirrored = MathHelper.WrapAngle(MathHelper.Pi - aim);
                    mirrored = MathHelper.Clamp(mirrored, LashSteepest, LashShallowest);
                    aim = MathHelper.WrapAngle(MathHelper.Pi - mirrored);
                }
                Projectile.NewProjectile(player.GetSource_Misc("CWR_DuneStormLash"), aimFrom,
                    Vector2.Zero, omenType, DuneStorm.LashDamage(), 2f, Main.myPlayer, aim, tier);
            }
        }

        public override void ClearWorld() {
            gustTimer = 0;
            lashTimer = 0;
        }
    }
}
