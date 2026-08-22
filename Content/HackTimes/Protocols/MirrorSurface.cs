using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 镜像水面：液面变镜，穿过它的敌方弹幕在交点复制一份归施术者。<br/>
    /// 签名级的液面反射 shader 本批不做，先用 PRT 顶表现（polish 待办）
    /// </summary>
    internal class MirrorSurface : QuickHackDef
    {
        //镜面线最长 24 格
        private const int MaxSpanTiles = 24;
        //全程复制上限
        private const int MaxCopies = 12;
        //复制弹伤害折半
        private const float CopyDamageRatio = 0.5f;
        //从命中格向上找表层格的步数上限
        private const int SurfaceSearchUp = 40;

        private static readonly Color Mirror = new(170, 220, 255);

        //每个效果的复制配额，键用 ActivationId（全局唯一、永不复用，
        //比格座标更稳，同一格被两名施术者先后骇入时不共享账）。只在权威端写
        private static readonly Dictionary<long, int> copyCounts = [];
        private static readonly List<long> pruneScratch = [];

        public override void SetDefaults() {
            UploadTime = 160;
            RamCost = 6;
            Category = QuickHackCategory.Paranormal;
            SupportedTargets = HackTargetKind.Water;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 10;

        public override void Unload() {
            base.Unload();
            copyCounts.Clear();
            pruneScratch.Clear();
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryLiquid(target, out int tileX, out int tileY)) return false;
            //封死的液体口袋没有液面，镜子无处可立
            return TryFindSurfaceRow(tileX, tileY, out _);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryLiquid(target, out int tileX, out int tileY)) return false;
            PruneOrphans();
            if (Main.netMode != NetmodeID.Server
                && TryComputeMirrorLine(tileX, tileY, out float lineY,
                    out float minX, out float maxX)) {
                EmitActivate(lineY, minX, maxX);
            }
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryLiquidCoords(target, out int tileX, out int tileY)
                && TryComputeMirrorLine(tileX, tileY, out float lineY,
                    out float minX, out float maxX)) {
                EmitActivate(lineY, minX, maxX);
            }
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryLiquid(target, out int tileX, out int tileY)) return true;
            //液面每帧重算：水在流动，镜线跟着液面走（各端由同一份世界液体数据算出同一条线）
            if (!TryComputeMirrorLine(tileX, tileY, out float lineY,
                out float minX, out float maxX)) {
                return true;
            }

            ActiveHackEffect effect = FindMyEffect(target);
            if (effect != null) {
                int copies = copyCounts.TryGetValue(effect.ActivationId, out int c) ? c : 0;
                if (copies < MaxCopies) {
                    copies += MirrorCrossings(lineY, minX, maxX,
                        effect.CasterIndex, MaxCopies - copies);
                    copyCounts[effect.ActivationId] = copies;
                }
            }
            if (Main.netMode != NetmodeID.Server) {
                EmitSurface(lineY, minX, maxX, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryLiquidCoords(target, out int tileX, out int tileY)
                && TryComputeMirrorLine(tileX, tileY, out float lineY,
                    out float minX, out float maxX)) {
                EmitSurface(lineY, minX, maxX, elapsed);
            }
        }

        public override void OnRemove(IHackTarget target) {
            ActiveHackEffect effect = FindMyEffect(target);
            if (effect != null) {
                copyCounts.Remove(effect.ActivationId);
            }
        }

        #region 镜面线

        /// <summary>
        /// 表层格：本列有液体且上一格没有液体。从种子列向两侧收拢成一条水平镜线；
        /// 泄漏路径说明，效果因液体被抽干而无 OnRemove 结束时，配额账目会滞留，
        /// 由下一次 OnApply 的 <see cref="PruneOrphans"/> 兜底清掉
        /// </summary>
        private static bool TryComputeMirrorLine(int tileX, int tileY,
            out float lineY, out float minX, out float maxX) {
            lineY = minX = maxX = 0f;
            if (!TryFindSurfaceRow(tileX, tileY, out int surfaceY)) return false;

            int left = tileX;
            int right = tileX;
            int span = 1;
            bool growLeft = true;
            bool growRight = true;
            while (span < MaxSpanTiles && (growLeft || growRight)) {
                if (growLeft) {
                    if (IsSurfaceColumn(left - 1, surfaceY)) {
                        left--;
                        span++;
                    }
                    else {
                        growLeft = false;
                    }
                }
                if (span >= MaxSpanTiles) break;
                if (growRight) {
                    if (IsSurfaceColumn(right + 1, surfaceY)) {
                        right++;
                        span++;
                    }
                    else {
                        growRight = false;
                    }
                }
            }

            //液面渲染贴着表层格上沿偏下一点
            lineY = surfaceY * 16f + 6f;
            minX = left * 16f;
            maxX = right * 16f + 16f;
            return true;
        }

        private static bool TryFindSurfaceRow(int tileX, int tileY, out int surfaceY) {
            surfaceY = -1;
            int y = tileY;
            for (int step = 0; step < SurfaceSearchUp; step++) {
                if (!HackTargets.InWorld(tileX, y)
                    || Main.tile[tileX, y].LiquidAmount == 0) {
                    break;
                }
                if (!HackTargets.InWorld(tileX, y - 1)
                    || Main.tile[tileX, y - 1].LiquidAmount == 0) {
                    surfaceY = y;
                    return true;
                }
                y--;
            }
            return false;
        }

        private static bool IsSurfaceColumn(int x, int surfaceY)
            => HackTargets.InWorld(x, surfaceY)
                && Main.tile[x, surfaceY].LiquidAmount > 0
                && HackTargets.InWorld(x, surfaceY - 1)
                && Main.tile[x, surfaceY - 1].LiquidAmount == 0;

        #endregion

        #region 跨线复制（权威端）

        //跨线判定：上一帧在线一侧、这一帧在另一侧，交点横座标落在镜线范围内
        private static int MirrorCrossings(float lineY, float minX, float maxX,
            int casterIndex, int budget) {
            int made = 0;
            for (int i = 0; i < Main.maxProjectiles && made < budget; i++) {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active || !projectile.hostile
                    || projectile.damage <= 0) {
                    continue;
                }
                //已经归你的复制弹不再照镜子，防自我增殖
                if (HackConvertedProjectile.IsConverted(projectile)) continue;

                float nowY = projectile.Center.Y;
                float prevY = projectile.oldPosition.Y + projectile.height * 0.5f;
                if (nowY == prevY) continue;
                bool crossedDown = prevY < lineY && nowY >= lineY;
                bool crossedUp = prevY > lineY && nowY <= lineY;
                if (!crossedDown && !crossedUp) continue;

                float t = MathHelper.Clamp((lineY - prevY) / (nowY - prevY), 0f, 1f);
                float prevX = projectile.oldPosition.X + projectile.width * 0.5f;
                float crossX = MathHelper.Lerp(prevX, projectile.Center.X, t);
                if (crossX < minX || crossX > maxX) continue;

                if (SpawnMirrorCopy(projectile, new Vector2(crossX, lineY), casterIndex)) {
                    made++;
                }
            }
            return made;
        }

        private static bool SpawnMirrorCopy(Projectile original, Vector2 point,
            int casterIndex) {
            if (casterIndex < 0 || casterIndex >= Main.maxPlayers) return false;
            //速度关于水平镜线反射
            Vector2 reflected = new(original.velocity.X, -original.velocity.Y);
            int damage = Math.Max(1, (int)(original.damage * CopyDamageRatio));
            var source = new HackConversionSource(casterIndex, capPenetrate: false);
            int index = Projectile.NewProjectile(source, point, reflected,
                original.type, damage, original.knockBack, casterIndex);
            if (index < 0 || index >= Main.maxProjectiles) return false;

            //转阵营已在 OnSpawn 里按来源做完。服务端上 owner 不是本机（255），
            //NewProjectile 不会自己发生成包，这里显式广播一次
            //包里的阵营与 ExtraAI 标记都是转换后的值，各端从第一帧起就一致
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, index);
            }
            else {
                HackConvertedProjectile.EmitConvertFlash(point);
            }
            return true;
        }

        #endregion

        #region 账本与表现

        private ActiveHackEffect FindMyEffect(IHackTarget target) {
            IReadOnlyList<ActiveHackEffect> effects = HackEffectTracker.AllActiveTileEffects;
            for (int i = 0; i < effects.Count; i++) {
                ActiveHackEffect effect = effects[i];
                if (effect.Active && effect.Hack == this
                    && effect.Target?.TargetEquals(target) == true) {
                    return effect;
                }
            }
            return null;
        }

        private static void PruneOrphans() {
            if (copyCounts.Count == 0) return;
            pruneScratch.Clear();
            foreach (long id in copyCounts.Keys) {
                if (HackEffectTracker.FindEffect(id) == null) {
                    pruneScratch.Add(id);
                }
            }
            for (int i = 0; i < pruneScratch.Count; i++) {
                copyCounts.Remove(pruneScratch[i]);
            }
            pruneScratch.Clear();
        }

        private static void EmitActivate(float lineY, float minX, float maxX) {
            for (int i = 0; i < 18; i++) {
                Vector2 pos = new(Main.rand.NextFloat(minX, maxX),
                    lineY + Main.rand.NextFloat(-3f, 3f));
                Vector2 vel = new(0f, Main.rand.NextFloat(-1.6f, -0.4f));
                PRTLoader.NewParticle<PRT_Spark>(pos, vel, Mirror, 0.9f)
                    ?.Configure(false, 22);
            }
        }

        //镜面常驻微光：沿线漂浮的碎屑 + 偶发的竖直细闪，读作"这条液面在照东西"
        private static void EmitSurface(float lineY, float minX, float maxX, int elapsed) {
            if (elapsed % 5 == 0) {
                Vector2 pos = new(Main.rand.NextFloat(minX, maxX),
                    lineY + Main.rand.NextFloat(-2f, 2f));
                PRTLoader.NewParticle<PRT_Spark>(pos,
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -0.25f), Mirror, 0.55f)
                    ?.Configure(false, 16);
            }
            if (elapsed % 45 == 0) {
                float x = Main.rand.NextFloat(minX, maxX);
                PRTLoader.NewParticle<PRT_Spark>(new Vector2(x, lineY),
                    new Vector2(0f, -1.2f), Color.White, 0.8f)?.Configure(false, 12);
            }
        }

        #endregion
    }
}
