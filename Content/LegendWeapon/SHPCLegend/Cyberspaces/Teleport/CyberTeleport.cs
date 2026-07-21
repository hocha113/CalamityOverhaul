using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.TimeFreezes;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Teleport
{
    /// <summary>领域瞬移，光标 clamp 域内，隐藏+裂缝；层≥1</summary>
    internal class CyberTeleport : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => Reset();

        /// <summary>最低层数</summary>
        public const int RequiredLayer = 1;

        /// <summary>单次 RAM（<see cref="HackTime.InfiniteHack"/> 免耗）</summary>
        public const float RamCostPerCast = 2f;

        /// <summary>冷却帧，约0.5s</summary>
        public const int CooldownFrames = 30;

        /// <summary>裂缝隐藏帧，PlayerOverride 去绘制</summary>
        public const int HideDuration = 22;

        /// <summary>Teleport 风格占位</summary>
        private const int TeleportStyle = 999;

        //本地计时
        private static int cooldownTimer;
        private static float cooldownTimerCarry;
        private static int hideTimer;
        private static float hideTimerCarry;

        /// <summary>演出隐藏期，PlayerOverride 移除绘制</summary>
        public static bool IsLocalPlayerHidden => hideTimer > 0;

        /// <summary>剩余冷却帧</summary>
        public static int CooldownRemain => cooldownTimer;

        /// <summary>冷却中</summary>
        public static bool OnCooldown => cooldownTimer > 0;

        /// <summary>光标夹到域内边缘</summary>
        public static Vector2 ClampToDomain(Player owner, Vector2 mouseWorld) {
            if (owner == null) return mouseWorld;

            float effectiveR = Cyberspace.EffectiveOuterRadius;
            if (effectiveR <= 1f) {
                return owner.Center;
            }

            //边界内留 8px
            float maxR = Math.Max(0f, effectiveR - 8f);
            Vector2 toMouse = mouseWorld - owner.Center;
            float dist = toMouse.Length();
            if (dist <= maxR) return mouseWorld;
            if (dist <= 1f) return owner.Center;
            return owner.Center + toMouse * (maxR / dist);
        }

        /// <summary>校验后触发瞬移</summary>
        public static void TryTeleport(Player owner) {
            if (owner == null || !owner.Alives()) return;

            //域激活、强度、层
            if (!Cyberspace.Active) return;
            if (Cyberspace.Intensity < 0.5f) return;
            if (Cyberspace.CurrentLayer < RequiredLayer) return;

            //冷却中拒
            if (cooldownTimer > 0) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent with {
                        Volume = 0.35f,
                        Pitch = -0.4f,
                    }, owner.Center);
                }
                return;
            }

            //RAM 不足则 HUD 闪
            if (!HackTime.InfiniteHack && (RamSystem.IsLocked || RamSystem.CurrentRam < RamCostPerCast)) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent with {
                        Volume = 0.4f,
                        Pitch = -0.3f,
                    }, owner.Center);
                    RamSystem.NotifyInsufficient();
                    Color denyColor = new(255, 90, 80);
                    CombatText.NewText(owner.Hitbox, denyColor, "// LOW RAM", true);
                }
                return;
            }

            Activate(owner);
        }

        /// <summary>耗 RAM、演出弹幕、瞬移、计时</summary>
        private static void Activate(Player owner) {
            Vector2 origin = owner.Center;
            Vector2 target = ClampToDomain(owner, Main.MouseWorld);

            //过近无效
            if (Vector2.DistanceSquared(origin, target) < 64f * 64f) {
                if (!VaultUtils.isServer && Main.myPlayer == owner.whoAmI) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent with {
                        Volume = 0.35f,
                        Pitch = -0.2f,
                    }, owner.Center);
                }
                return;
            }

            //耗 RAM
            if (!HackTime.InfiniteHack) {
                RamSystem.TryConsume((int)Math.Ceiling(RamCostPerCast));
            }

            cooldownTimer = CooldownFrames;
            cooldownTimerCarry = 0f;
            //仅 myPlayer 隐藏绘制
            if (Main.myPlayer == owner.whoAmI) {
                hideTimer = HideDuration;
                hideTimerCarry = 0f;
            }

            //演出弹幕
            if (Main.myPlayer == owner.whoAmI) {
                IEntitySource source = owner.GetSource_FromThis();

                //起点解构
                Projectile.NewProjectile(source, origin, Vector2.Zero,
                    ModContent.ProjectileType<CyberPixelDecomposeProj>(), 0, 0, owner.whoAmI);

                //走廊，ai0/ai1=目标
                Projectile.NewProjectile(source, origin, Vector2.Zero,
                    ModContent.ProjectileType<CyberRiftSlashProj>(), 0, 0, owner.whoAmI,
                    ai0: target.X, ai1: target.Y);

                //终点重组
                Projectile.NewProjectile(source, target, Vector2.Zero,
                    ModContent.ProjectileType<CyberReformProj>(), 0, 0, owner.whoAmI);
            }

            //hitbox 中心对齐
            Vector2 newPos = target - new Vector2(owner.width * 0.5f, owner.height * 0.5f);
            //领域中心暂留起点，慢追
            //仅 myPlayer 播追赶
            if (Main.myPlayer == owner.whoAmI) {
                Cyberspace.NotifyTeleport(origin);
            }
            owner.Teleport(newPos, TeleportStyle);
            //降速留惯性
            owner.velocity *= 0.25f;
            //短暂无敌
            owner.immune = true;
            owner.immuneTime = Math.Max(owner.immuneTime, 18);

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.FaultOccurred with {
                    Volume = 0.65f,
                    Pitch = 0.35f,
                }, origin);
                SoundEngine.PlaySound(CWRSound.Faultrelease with {
                    Volume = 0.7f,
                    Pitch = 0.15f,
                }, target);
                SoundEngine.PlaySound(CWRSound.FaultTransition with {
                    Volume = 0.45f,
                    Pitch = 0.5f,
                }, target);
            }
        }

        /// <summary>本地计时滴答</summary>
        public static void Update() {
            TimeGear.ConsumeFrames(ref cooldownTimer, ref cooldownTimerCarry);
            TimeGear.ConsumeFrames(ref hideTimer, ref hideTimerCarry);
        }

        /// <summary>清计时</summary>
        public static void Reset() {
            cooldownTimer = 0;
            cooldownTimerCarry = 0f;
            hideTimer = 0;
            hideTimerCarry = 0f;
        }
    }
}
