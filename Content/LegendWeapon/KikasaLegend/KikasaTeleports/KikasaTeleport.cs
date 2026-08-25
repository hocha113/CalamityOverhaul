using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaResets;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTeleports
{
    /// <summary>
    /// 鬼域传送门面：持伞按 <see cref="Common.CWRKeySystem.Legend_Teleport"/>
    /// 以水为媒介瞬移到鼠标处，瞬发无前后摇（时序在 <see cref="KikasaTeleportProj"/>）。
    /// 不需要领域；血湖稳态里水本就在脚下，节奏与冷却都更短。
    /// 输入受理在 <see cref="KikasaTeleportPlayer"/>，水舞台与位移在
    /// <see cref="KikasaTeleportProj"/>，常驻悬伞检测到水舞台即入传送态亲自执行，
    /// 全程只有一把伞
    /// </summary>
    internal static class KikasaTeleport
    {
        //冷却（帧），域内明显更勤

        internal const int CooldownFull = 240;

        internal const int CooldownFast = 90;

        //太近不挪，整套仪式不该换来原地踏步
        private const float MinDistance = 64f;

        private static uint localLockUntil;
        private static int localLockTotal = 1;

        /// <summary>本机传送锁剩余 0~1（1=刚上锁），HUD 冷却弧消费；无锁=0</summary>
        internal static float LocalCooldown01 {
            get {
                if (Main.GameUpdateCount >= localLockUntil) {
                    return 0f;
                }
                return MathHelper.Clamp(
                    (localLockUntil - Main.GameUpdateCount) / (float)localLockTotal, 0f, 1f);
            }
        }

        private static void LockLocal(int frames) {
            localLockUntil = Main.GameUpdateCount + (uint)Math.Max(frames, 1);
            localLockTotal = Math.Max(frames, 1);
        }

        /// <summary>本机受理入口：门控通过即起演出弹幕并上锁，目标与变体全随出生包走</summary>
        internal static void TryTeleport(Player player) {
            if (player.whoAmI != Main.myPlayer || !player.Alives()) {
                return;
            }
            //倒带正接管位置历史，传送与倒放不能互相拉扯
            if (KikasaReset.IsPlayerAffected(player.whoAmI)) {
                return;
            }
            //上一场演出未谢幕不叠加
            if (player.ownedProjectileCounts[ModContent.ProjectileType<KikasaTeleportProj>()] > 0) {
                return;
            }
            KikasaDomainPlayer domain = player.GetModPlayer<KikasaDomainPlayer>();
            //世界正翻转或困在梦里，此刻没有可落脚的彼岸
            if (domain.Phase is KikasaDomainPhase.Flipping or KikasaDomainPhase.DreamPull
                or KikasaDomainPhase.Dreaming or KikasaDomainPhase.DreamReturn
                || KikasaDream.DreamWorldAt(player.Center)) {
                Refuse(player);
                return;
            }
            if (LocalCooldown01 > 0f) {
                Refuse(player);
                return;
            }
            Vector2 target = Main.MouseWorld;
            if (!float.IsFinite(target.X) || !float.IsFinite(target.Y)
                || Vector2.DistanceSquared(player.Center, target) < MinDistance * MinDistance) {
                return;
            }

            bool fast = domain.Phase == KikasaDomainPhase.Open;
            Projectile.NewProjectile(player.GetSource_Misc("KikasaTeleport"),
                player.Center, Vector2.Zero,
                ModContent.ProjectileType<KikasaTeleportProj>(), 0, 0, player.whoAmI,
                ai0: target.X, ai1: target.Y, ai2: fast ? 1f : 0f);
            LockLocal(fast ? CooldownFast : CooldownFull);
        }

        /// <summary>冷却/相位不受理的轻拒声，与沉溺的拒绝反馈同款</summary>
        private static void Refuse(Player player) {
            SoundEngine.PlaySound(SoundID.MenuTick with {
                Volume = 0.5f,
                Pitch = -0.65f,
                MaxInstances = 2,
            }, player.Center);
        }
    }
}
