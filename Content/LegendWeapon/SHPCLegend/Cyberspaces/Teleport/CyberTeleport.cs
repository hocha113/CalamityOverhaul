using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.RAMSystems;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces.Teleport
{
    /// <summary>领域瞬移管理：光标 clamp 域内，隐藏+裂缝演出；层≥1</summary>
    internal class CyberTeleport : ICWRLoader
    {
        void ICWRLoader.UnLoadData() => Reset();

        /// <summary>
        /// 启用瞬移所需的最低赛博空间层数
        /// </summary>
        public const int RequiredLayer = 1;

        /// <summary>
        /// 单次瞬移消耗的 RAM 量（<see cref="HackTime.InfiniteHack"/> 模式下不消耗）
        /// </summary>
        public const float RamCostPerCast = 2f;

        /// <summary>
        /// 触发后冷却帧数，防止瞬时连按造成视觉混乱（约 0.5 秒）
        /// </summary>
        public const int CooldownFrames = 30;

        /// <summary>
        /// 玩家在裂缝阶段的不可见帧数（演出期间用 PlayerOverride 移除其本体绘制）
        /// </summary>
        public const int HideDuration = 22;

        /// <summary>
        /// 触发瞬移时玩家"瞬移到目标"的网络风格（仅占位）
        /// </summary>
        private const int TeleportStyle = 999;

        //本地玩家专用计时（只在客户端本地推进）
        private static int cooldownTimer;
        private static int hideTimer;

        /// <summary>演出隐藏期，PlayerOverride 移除绘制</summary>
        public static bool IsLocalPlayerHidden => hideTimer > 0;

        /// <summary>
        /// 当前剩余冷却帧（HUD/UI 可读）
        /// </summary>
        public static int CooldownRemain => cooldownTimer;

        /// <summary>
        /// 是否处于冷却中
        /// </summary>
        public static bool OnCooldown => cooldownTimer > 0;

        /// <summary>
        /// 在领域有效半径内夹紧光标位置：超出边缘时投影到边缘上
        /// <br/>对应"瞬移范围只能在领域内"的硬约束
        /// </summary>
        public static Vector2 ClampToDomain(Player owner, Vector2 mouseWorld) {
            if (owner == null) return mouseWorld;

            float effectiveR = Cyberspace.EffectiveOuterRadius;
            if (effectiveR <= 1f) {
                return owner.Center;
            }

            //安全留一格，避免落点正好压在领域边界外
            float maxR = Math.Max(0f, effectiveR - 8f);
            Vector2 toMouse = mouseWorld - owner.Center;
            float dist = toMouse.Length();
            if (dist <= maxR) return mouseWorld;
            if (dist <= 1f) return owner.Center;
            return owner.Center + toMouse * (maxR / dist);
        }

        /// <summary>
        /// 触发瞬移：经过领域/层级/RAM/冷却校验后立即执行
        /// </summary>
        public static void TryTeleport(Player owner) {
            if (owner == null || !owner.Alives()) return;

            //领域必须激活、当前帧视觉强度足够、当前层达标
            if (!Cyberspace.Active) return;
            if (Cyberspace.Intensity < 0.5f) return;
            if (Cyberspace.CurrentLayer < RequiredLayer) return;

            //冷却期不允许再次触发
            if (cooldownTimer > 0) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent with {
                        Volume = 0.35f,
                        Pitch = -0.4f,
                    }, owner.Center);
                }
                return;
            }

            //RAM 检查：不足时除常规失败反馈外，还触发 HUD 红色故障闪烁
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

        /// <summary>
        /// 实际执行：消耗 RAM、生成演出弹幕、瞬移玩家、设置计时
        /// </summary>
        private static void Activate(Player owner) {
            Vector2 origin = owner.Center;
            Vector2 target = ClampToDomain(owner, Main.MouseWorld);

            //极近距离视为无效，避免演出空转
            if (Vector2.DistanceSquared(origin, target) < 64f * 64f) {
                if (!VaultUtils.isServer && Main.myPlayer == owner.whoAmI) {
                    SoundEngine.PlaySound(CWRSound.FailureCurrent with {
                        Volume = 0.35f,
                        Pitch = -0.2f,
                    }, owner.Center);
                }
                return;
            }

            //RAM 消耗（无限骇客时间下不收费）
            if (!HackTime.InfiniteHack) {
                RamSystem.TryConsume((int)Math.Ceiling(RamCostPerCast));
            }

            //计时
            cooldownTimer = CooldownFrames;
            //仅本地客户端隐藏自己的绘制（其它客户端通过弹幕看到效果即可）
            if (Main.myPlayer == owner.whoAmI) {
                hideTimer = HideDuration;
            }

            //生成演出弹幕（NewProjectile 自带网络同步，远端也能看到解构/走廊/重组）
            if (Main.myPlayer == owner.whoAmI) {
                IEntitySource source = owner.GetSource_FromThis();

                //起点解构弹幕：玩家化作像素数据块离心飞散
                Projectile.NewProjectile(source, origin, Vector2.Zero,
                    ModContent.ProjectileType<CyberPixelDecomposeProj>(), 0, 0, owner.whoAmI);

                //数据走廊主弹幕：从起点到终点的传输管道，ai0/ai1 = 目标坐标
                Projectile.NewProjectile(source, origin, Vector2.Zero,
                    ModContent.ProjectileType<CyberRiftSlashProj>(), 0, 0, owner.whoAmI,
                    ai0: target.X, ai1: target.Y);

                //终点重组弹幕：像素数据块向心聚合并实体化
                Projectile.NewProjectile(source, target, Vector2.Zero,
                    ModContent.ProjectileType<CyberReformProj>(), 0, 0, owner.whoAmI);
            }

            //真实瞬移：按 hitbox 中心对齐目标点
            Vector2 newPos = target - new Vector2(owner.width * 0.5f, owner.height * 0.5f);
            //通知领域中心暂留在出发点：玩家瞬间到达终点，但领域慢一拍追上来，营造"撕裂前冲"的速度感
            //仅 myPlayer 播追赶动画，远端各自同步
            if (Main.myPlayer == owner.whoAmI) {
                Cyberspace.NotifyTeleport(origin);
            }
            owner.Teleport(newPos, TeleportStyle);
            //降速但保留惯性方向，给"硬着地"的力量感
            owner.velocity *= 0.25f;
            //短暂无敌帧，避免落到敌人身上立刻挨揍
            owner.immune = true;
            owner.immuneTime = Math.Max(owner.immuneTime, 18);

            //音效（劈裂 + 重现两段）
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

        /// <summary>
        /// 每帧推进：本地计时滴答；远端不需要参与（演出由弹幕回放）
        /// </summary>
        public static void Update() {
            if (cooldownTimer > 0) cooldownTimer--;
            if (hideTimer > 0) hideTimer--;
        }

        /// <summary>
        /// 立即清空所有计时器
        /// </summary>
        public static void Reset() {
            cooldownTimer = 0;
            hideTimer = 0;
        }
    }
}
