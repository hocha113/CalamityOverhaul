using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 延迟引信：弹幕原地悬停三十秒变成雷，阵营不变，敌弹仍会炸你。<br/>
    /// 与弹道冻结的分界：那条五秒后原样放行，这条要么被碰响、要么到期消散，不放行。<br/>
    /// 悬停完全交给时停层（速度快照、位置钉死、timeLeft 逐帧补偿都由它做），
    /// 协议这边只负责每帧续租，不自己缓存任何运动量
    /// </summary>
    internal class DelayFuse : QuickHackDef
    {
        //布雷后的保险帧数，防止上传完成的瞬间就在施术者脸上炸响
        private const int ArmDelayFrames = 15;
        //触发判定的外扩像素：时停层解冻要吃两三帧租约到期，放宽一点保证解冻时还叠着
        private const int TriggerInflatePx = 14;

        private static readonly Color Fuse = new(255, 190, 90);

        //触发状态账本，键用 owner+identity+type（槽位各端不同且会复用，whoAmI 不可靠）。
        //只在权威端写；OnRemove 按键销账，
        //泄漏路径：雷被第三方手段打掉时 OnRemove 不跑（目标已失效），
        //滞留条目由下一次 OnApply 的 PruneLedger 兜底
        private static readonly Dictionary<NetworkProjectileIdentity, bool> triggered = [];
        private static readonly List<NetworkProjectileIdentity> pruneScratch = [];

        public override void SetDefaults() {
            UploadTime = 70;
            RamCost = 3;
            Category = QuickHackCategory.Lethal;
            SupportedTargets = HackTargetKind.Projectile;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 30;

        public override void Unload() {
            base.Unload();
            triggered.Clear();
            pruneScratch.Clear();
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return false;
            //跟随玩家的东西钉住只会锁死自己的输出，照弹道冻结的口径拦掉
            if (projectile.minion || projectile.sentry || projectile.bobber) return false;
            if (Main.projHook[projectile.type]) return false;
            if (projectile.ModProjectile is BaseHeldProj) return false;
            //与弹道冻结互斥：两个冻结源叠在一发弹上，先到期的那个会被另一个的
            //快照语义拖住，触发/放行的时序说不清；同型重复挂也拦掉
            if (HasProjectileEffect<ProjectileFreeze>(projectile.whoAmI)) return false;
            if (HasProjectileEffect<DelayFuse>(projectile.whoAmI)) return false;
            return projectile.damage > 0;
        }

        public override bool CanApplyTo(IHackTarget target, Player caster) {
            if (!CanApplyTo(target)) return false;
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return false;
            //友方弹只能钉自己的，不然可以拿它锁队友的输出
            return !projectile.friendly || projectile.hostile
                || (caster != null && projectile.owner == caster.whoAmI);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return false;
            PruneLedger();
            if (NetworkProjectileIdentity.TryCapture(projectile,
                out NetworkProjectileIdentity key)) {
                triggered[key] = false;
            }
            if (Main.netMode != NetmodeID.Server) EmitApply(projectile);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryProjectile(target, out Projectile projectile)) {
                EmitApply(projectile);
            }
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return true;
            //每帧续租，冻结的运动快照与 timeLeft 补偿全由时停层持有
            TimeFreezeSystem.RefreshProjectile<DelayFuse>(projectile, 2);

            //时停层冻结期间会掐掉全部命中判定，所以"被碰响"由这里的几何扫描代劳：
            //有效目标压进引信圈就提前结束效果，停止续租，两三帧后解冻，
            //叠着的目标由各自端的正常命中管线结算
            if (elapsed >= ArmDelayFrames && HasTriggerContact(projectile)) {
                if (NetworkProjectileIdentity.TryCapture(projectile,
                    out NetworkProjectileIdentity key)) {
                    triggered[key] = true;
                }
                return false;
            }
            if (Main.netMode != NetmodeID.Server) EmitHold(projectile, elapsed);
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryProjectile(target, out Projectile projectile)) {
                TimeFreezeSystem.RefreshProjectile<DelayFuse>(projectile, 2);
                EmitHold(projectile, elapsed);
            }
        }

        public override void OnRemove(IHackTarget target) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return;
            bool wasTriggered = NetworkProjectileIdentity.TryCapture(projectile,
                    out NetworkProjectileIdentity key)
                && triggered.Remove(key, out bool flag) && flag;

            if (wasTriggered) {
                //被碰响：什么都不写，租约自然到期解冻，雷带着入定前的速度出膛，
                //命中在叠着的目标身上照常结算
                if (Main.netMode != NetmodeID.Server) EmitTrigger(projectile.Center);
                return;
            }
            //到期（或施术者离场）：自然消散，走 Kill 让弹幕按自己的死亡逻辑收尾
            if (Main.netMode != NetmodeID.Server) EmitDissipate(projectile.Center);
            KillProjectileSynced(projectile);
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            //远端分不清触发与到期，两种结束都值得一记闪光；死亡本身由 29 号包送达
            if (HackTargets.TryProjectile(target, out Projectile projectile)) {
                EmitTrigger(projectile.Center);
            }
        }

        #region 触发与销账

        private static bool HasTriggerContact(Projectile projectile) {
            Rectangle box = projectile.Hitbox;
            box.Inflate(TriggerInflatePx, TriggerInflatePx);
            //阵营不变：友方雷等敌怪来踩，敌方雷等玩家来踩（包括施术者自己）
            if (projectile.friendly) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                    if (!npc.CanBeChasedBy(projectile)) continue;
                    if (npc.Hitbox.Intersects(box)) return true;
                }
            }
            if (projectile.hostile) {
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player player = Main.player[i];
                    if (player?.active != true || player.dead || player.ghost) continue;
                    if (player.Hitbox.Intersects(box)) return true;
                }
            }
            return false;
        }

        //必须走 Kill：直接置 active=false 跳过死亡逻辑也发不出同步；
        //29 号包按 owner+identity 反查各端槽位，玩家弹与 owner 255 的世界弹都认（照 DataPurge）
        private static void KillProjectileSynced(Projectile projectile) {
            int identity = projectile.identity;
            int owner = projectile.owner;
            projectile.Kill();
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.KillProjectile, -1, -1, null,
                    identity, owner);
            }
        }

        //ProjectileFreeze 的反向互斥也查这张表，故开 internal
        internal static bool HasProjectileEffect<T>(int projectileIndex)
            where T : QuickHackDef {
            IReadOnlyList<ActiveHackEffect> effects = HackEffectTracker.AllActiveTileEffects;
            for (int i = 0; i < effects.Count; i++) {
                ActiveHackEffect effect = effects[i];
                if (effect.Active && effect.Hack is T
                    && effect.Target is ProjectileScannable p
                    && p.ProjectileIndex == projectileIndex) {
                    return true;
                }
            }
            return false;
        }

        private static void PruneLedger() {
            if (triggered.Count == 0) return;
            pruneScratch.Clear();
            foreach (NetworkProjectileIdentity key in triggered.Keys) {
                if (!key.TryResolve(out _)) pruneScratch.Add(key);
            }
            for (int i = 0; i < pruneScratch.Count; i++) {
                triggered.Remove(pruneScratch[i]);
            }
            pruneScratch.Clear();
        }

        #endregion

        #region 表现

        private static void EmitApply(Projectile projectile) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3f, 3f);
                PRTLoader.NewParticle<PRT_Spark>(projectile.Center, vel, Fuse, 1.0f)
                    ?.Configure(false, 20);
            }
        }

        //悬停期：一粒引信火头绕体缓转 + 每三十帧一次心跳闪
        private static void EmitHold(Projectile projectile, int elapsed) {
            if (elapsed % 4 == 0) {
                float angle = elapsed * 0.09f;
                float radius = MathF.Max(projectile.width, projectile.height) * 0.7f + 10f;
                Vector2 orbit = projectile.Center + angle.ToRotationVector2() * radius;
                PRTLoader.NewParticle<PRT_Spark>(orbit, Vector2.Zero, Fuse, 0.55f)
                    ?.Configure(false, 10);
            }
            if (elapsed % 30 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(projectile.Center, Vector2.Zero,
                    Color.White, 1.4f)?.Configure(false, 8);
            }
        }

        private static void EmitTrigger(Vector2 center) {
            for (int i = 0; i < 12; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f);
                PRTLoader.NewParticle<PRT_Spark>(center, vel, Color.White, 1.0f)
                    ?.Configure(false, 14);
            }
        }

        //消散做成向心收拢，读作"引信烧完了"而不是炸开
        private static void EmitDissipate(Vector2 center) {
            for (int i = 0; i < 12; i++) {
                Vector2 offset = Main.rand.NextVector2CircularEdge(22f, 22f);
                PRTLoader.NewParticle<PRT_Spark>(center + offset, -offset * 0.14f,
                    Fuse, 0.8f)?.Configure(false, 16);
            }
        }

        #endregion
    }
}
