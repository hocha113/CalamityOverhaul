using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 弹幕征收：你的一发弹幕开始吞掉靠近的敌弹，每吞一发长大一点，
    /// 到期（或中途阵亡）按吞的量在原地炸开
    /// </summary>
    internal class ProjectileTithe : QuickHackDef
    {
        //征收半径
        internal const float EatRadius = 120f;
        //每吞一发：爆炸伤害加成 +15%，体积 +0.06
        internal const float DamageStep = 0.15f;
        internal const float ScaleStep = 0.06f;
        //上限 20 发（加成封顶 +300%）
        internal const int MaxEats = 20;

        private static readonly Color Levy = new(255, 150, 70);

        /// <summary>权威端账本条目：吞了多少、炸多大、在哪炸</summary>
        private sealed class TitheLedgerEntry
        {
            internal long ActivationId;
            internal int CasterIndex;
            internal int BaseDamage;
            internal float BaseScale;
            internal int EatCount;
            internal Vector2 LastPosition;
            internal bool Detonated;
        }

        /// <summary>
        /// 远端观感账本：看管中的敌弹在宿主附近消失就 +1。
        /// 权威端吞谁由它自己说了算并广播 29 号击杀包，这份账只驱动本端的
        /// 体积与吸收表现；到期爆炸的伤害用权威端的账，偏差只体现在观感上
        /// </summary>
        private sealed class TitheVisualEntry
        {
            internal float BaseScale;
            internal int SeenEats;
            internal ulong LastTouchedFrame;
            internal readonly Dictionary<NetworkProjectileIdentity, Vector2> Watched = [];
        }

        //两份账本都以宿主弹的 owner+identity+type 为键（槽位各端不同且会复用）。
        //权威账的泄漏路径：宿主中途阵亡时 OnRemove 不跑（目标已失效），
        //由 ProjectileTitheSystem 的每帧清账兜底（顺带补上死亡爆炸）；
        //观感账靠 LastTouchedFrame 过期自清
        private static readonly Dictionary<NetworkProjectileIdentity, TitheLedgerEntry>
            authorityLedger = [];
        private static readonly Dictionary<NetworkProjectileIdentity, TitheVisualEntry>
            visualLedger = [];
        private static readonly List<NetworkProjectileIdentity> keyScratch = [];
        private static readonly List<KeyValuePair<NetworkProjectileIdentity, Vector2>>
            watchScratch = [];

        public override void SetDefaults() {
            UploadTime = 100;
            RamCost = 4;
            Category = QuickHackCategory.Lethal;
            SupportedTargets = HackTargetKind.Projectile;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 10;

        public override void Unload() {
            base.Unload();
            authorityLedger.Clear();
            visualLedger.Clear();
            keyScratch.Clear();
            watchScratch.Clear();
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return false;
            //随从和哨兵会一直活着，挂上就是一颗贴身炸弹；手持弹同理
            if (projectile.minion || projectile.sentry || projectile.bobber) return false;
            if (Main.projHook[projectile.type]) return false;
            if (projectile.ModProjectile is BaseHeldProj) return false;
            //一发弹只征收一次
            if (HasProjectileEffect<ProjectileTithe>(projectile.whoAmI)) return false;
            return projectile.friendly && !projectile.hostile && projectile.damage > 0;
        }

        public override bool CanApplyTo(IHackTarget target, Player caster) {
            if (!CanApplyTo(target)) return false;
            //只能征收自己的弹，队友的输出不归你改
            return caster != null
                && HackTargets.TryProjectile(target, out Projectile projectile)
                && projectile.owner == caster.whoAmI;
        }

        #region 权威端

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return false;
            if (!NetworkProjectileIdentity.TryCapture(projectile,
                out NetworkProjectileIdentity key)) {
                return false;
            }
            ActiveHackEffect effect = FindMyEffect(target);
            authorityLedger[key] = new TitheLedgerEntry {
                ActivationId = effect?.ActivationId ?? 0,
                CasterIndex = caster.whoAmI,
                BaseDamage = Math.Max(1, projectile.damage),
                BaseScale = projectile.scale,
                LastPosition = projectile.Center,
            };
            if (Main.netMode != NetmodeID.Server) EmitApply(projectile.Center);
            return true;
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return true;
            if (!NetworkProjectileIdentity.TryCapture(projectile,
                out NetworkProjectileIdentity key)
                || !authorityLedger.TryGetValue(key, out TitheLedgerEntry entry)) {
                return true;
            }
            //死亡爆炸要用的落点每帧刷新
            entry.LastPosition = projectile.Center;
            if (entry.EatCount < MaxEats) {
                EatNearby(projectile, entry);
            }
            //体积在各端各自长（scale 不进同步包）；权威端这份供单人模式用
            projectile.scale = entry.BaseScale + ScaleStep * entry.EatCount;
            if (Main.netMode != NetmodeID.Server) {
                EmitCharged(projectile, entry.EatCount, elapsed);
            }
            return true;
        }

        public override void OnRemove(IHackTarget target) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return;
            if (NetworkProjectileIdentity.TryCapture(projectile,
                out NetworkProjectileIdentity key)
                && authorityLedger.Remove(key, out TitheLedgerEntry entry)
                && !entry.Detonated) {
                entry.Detonated = true;
                Detonate(projectile.Center, entry);
            }
            //到期即引爆，吞饱的宿主不再放它继续飞
            KillProjectileSynced(projectile);
        }

        //吞弹：击杀走 Kill + 29 号广播（照 DataPurge），各端看到同一发消失
        private static void EatNearby(Projectile host, TitheLedgerEntry entry) {
            float radiusSq = EatRadius * EatRadius;
            for (int i = 0; i < Main.maxProjectiles && entry.EatCount < MaxEats; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.whoAmI == host.whoAmI) continue;
                if (!other.hostile || other.friendly || other.damage <= 0) continue;
                if (HackConvertedProjectile.IsConverted(other)) continue;
                if (Vector2.DistanceSquared(other.Center, host.Center) > radiusSq) {
                    continue;
                }
                Vector2 eatenAt = other.Center;
                KillProjectileSynced(other);
                entry.EatCount++;
                if (Main.netMode != NetmodeID.Server) {
                    EmitAbsorb(eatenAt, host.Center);
                }
            }
        }

        private static void Detonate(Vector2 center, TitheLedgerEntry entry) {
            int count = Math.Clamp(entry.EatCount, 0, MaxEats);
            int damage = Math.Max(1,
                (int)(entry.BaseDamage * (1f + DamageStep * count)));
            int index = Projectile.NewProjectile(
                new EntitySource_Misc("CWRProjectileTithe"), center, Vector2.Zero,
                ModContent.ProjectileType<TitheDetonationProj>(), damage, 6f,
                entry.CasterIndex, count);
            if (index < 0 || index >= Main.maxProjectiles) return;
            //服务端上 owner 不是本机，NewProjectile 不自己发包，显式广播；
            //owner 给施术者，NPC 命中由他的客户端结算
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, index);
            }
        }

        /// <summary>宿主中途阵亡的兜底：效果连 OnRemove 都不跑，账本里余下的就是这类</summary>
        internal static void SweepDeadHosts() {
            if (authorityLedger.Count == 0) return;
            keyScratch.Clear();
            foreach (var pair in authorityLedger) {
                if (!pair.Key.TryResolve(out _)) keyScratch.Add(pair.Key);
            }
            for (int i = 0; i < keyScratch.Count; i++) {
                if (authorityLedger.Remove(keyScratch[i], out TitheLedgerEntry entry)
                    && !entry.Detonated) {
                    entry.Detonated = true;
                    Detonate(entry.LastPosition, entry);
                }
            }
            keyScratch.Clear();
        }

        #endregion

        #region 远端观感

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return;
            if (NetworkProjectileIdentity.TryCapture(projectile,
                out NetworkProjectileIdentity key)) {
                visualLedger[key] = new TitheVisualEntry {
                    BaseScale = projectile.scale,
                    LastTouchedFrame = Main.GameUpdateCount,
                };
            }
            EmitApply(projectile.Center);
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryProjectile(target, out Projectile projectile)) return;
            if (!NetworkProjectileIdentity.TryCapture(projectile,
                out NetworkProjectileIdentity key)) {
                return;
            }
            if (!visualLedger.TryGetValue(key, out TitheVisualEntry entry)) {
                entry = new TitheVisualEntry { BaseScale = projectile.scale };
                visualLedger[key] = entry;
            }
            entry.LastTouchedFrame = Main.GameUpdateCount;
            UpdateVisualWatch(projectile, entry);
            //owner 端的 scale 顺带放大了他本机的命中盒——命中本来就在 owner 端结算，
            //这份成长因此是真实收益而不止是观感
            projectile.scale = entry.BaseScale + ScaleStep * entry.SeenEats;
            EmitCharged(projectile, entry.SeenEats, elapsed);
            PruneVisualLedger();
        }

        public override void OnReplicatedRemove(IHackTarget target) {
            if (HackTargets.TryProjectile(target, out Projectile projectile)
                && NetworkProjectileIdentity.TryCapture(projectile,
                    out NetworkProjectileIdentity key)) {
                visualLedger.Remove(key);
            }
        }

        private static void UpdateVisualWatch(Projectile host, TitheVisualEntry entry) {
            float watchRadiusSq = EatRadius * EatRadius * 2.25f;
            keyScratch.Clear();
            watchScratch.Clear();
            foreach (var pair in entry.Watched) {
                if (!pair.Key.TryResolve(out Projectile watched)) {
                    //看管中的敌弹没了：多半是权威端吞的（29 号包送达），记一笔
                    if (entry.SeenEats < MaxEats) {
                        entry.SeenEats++;
                        EmitAbsorb(pair.Value, host.Center);
                    }
                    keyScratch.Add(pair.Key);
                }
                else if (Vector2.DistanceSquared(watched.Center, host.Center)
                    > watchRadiusSq) {
                    //飞离征收圈，不再看管，避免远处的自然死亡记错账
                    keyScratch.Add(pair.Key);
                }
                else {
                    watchScratch.Add(new(pair.Key, watched.Center));
                }
            }
            for (int i = 0; i < keyScratch.Count; i++) {
                entry.Watched.Remove(keyScratch[i]);
            }
            for (int i = 0; i < watchScratch.Count; i++) {
                entry.Watched[watchScratch[i].Key] = watchScratch[i].Value;
            }
            keyScratch.Clear();
            watchScratch.Clear();

            //收编新的看管对象
            if (entry.Watched.Count >= MaxEats + 4) return;
            float radiusSq = EatRadius * EatRadius;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.whoAmI == host.whoAmI) continue;
                if (!other.hostile || other.friendly || other.damage <= 0) continue;
                if (HackConvertedProjectile.IsConverted(other)) continue;
                if (Vector2.DistanceSquared(other.Center, host.Center) > radiusSq) {
                    continue;
                }
                if (!NetworkProjectileIdentity.TryCapture(other,
                    out NetworkProjectileIdentity otherKey)) {
                    continue;
                }
                if (!entry.Watched.ContainsKey(otherKey)) {
                    entry.Watched[otherKey] = other.Center;
                }
            }
        }

        //宿主没了时 OnReplicatedRemove 取不到目标不会跑，观感账靠过期自清
        private static void PruneVisualLedger() {
            if (visualLedger.Count == 0) return;
            ulong now = Main.GameUpdateCount;
            keyScratch.Clear();
            foreach (var pair in visualLedger) {
                if (pair.Value.LastTouchedFrame + 120 < now) keyScratch.Add(pair.Key);
            }
            for (int i = 0; i < keyScratch.Count; i++) {
                visualLedger.Remove(keyScratch[i]);
            }
            keyScratch.Clear();
        }

        #endregion

        #region 共用

        private static void KillProjectileSynced(Projectile projectile) {
            int identity = projectile.identity;
            int owner = projectile.owner;
            projectile.Kill();
            if (Main.netMode == NetmodeID.Server) {
                //29 号按 owner+identity 反查各端槽位，玩家弹与 owner 255 的世界弹都认
                NetMessage.SendData(MessageID.KillProjectile, -1, -1, null,
                    identity, owner);
            }
        }

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

        private static bool HasProjectileEffect<T>(int projectileIndex)
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

        private static void EmitApply(Vector2 center) {
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(3.2f, 3.2f);
                PRTLoader.NewParticle<PRT_Spark>(center, vel, Levy, 1.0f)
                    ?.Configure(false, 18);
            }
        }

        //吸收：从被吞位置向宿主拉一道收束流
        private static void EmitAbsorb(Vector2 from, Vector2 to) {
            Vector2 dir = (to - from).SafeNormalize(Vector2.UnitY);
            for (int i = 0; i < 6; i++) {
                Vector2 pos = Vector2.Lerp(from, to, Main.rand.NextFloat(0.35f));
                PRTLoader.NewParticle<PRT_Spark>(pos, dir * Main.rand.NextFloat(3f, 7f),
                    Levy, 0.85f)?.Configure(false, 14);
            }
        }

        //宿主随吞噬量越来越"烫"：环绕碎屑密度跟着账走
        private static void EmitCharged(Projectile host, int eats, int elapsed) {
            if (eats <= 0) {
                if (elapsed % 20 == 0) {
                    PRTLoader.NewParticle<PRT_Spark>(host.Center, Vector2.Zero,
                        Levy, 0.5f)?.Configure(false, 10);
                }
                return;
            }
            int interval = Math.Max(2, 8 - eats / 3);
            if (elapsed % interval != 0) return;
            Vector2 offset = Main.rand.NextVector2Circular(
                host.width * host.scale * 0.6f + 8f,
                host.height * host.scale * 0.6f + 8f);
            PRTLoader.NewParticle<PRT_Spark>(host.Center + offset,
                -offset * 0.05f, Levy, 0.6f + eats * 0.02f)?.Configure(false, 12);
        }

        #endregion
    }

    /// <summary>宿主中途阵亡时效果无 OnRemove 落点，这里每帧在权威端补上死亡爆炸</summary>
    internal sealed class ProjectileTitheSystem : ModSystem
    {
        public override void PostUpdateProjectiles() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            ProjectileTithe.SweepDeadHosts();
        }
    }

    /// <summary>
    /// 征收爆破：一次性 AOE，半径按吞噬数走 ai[0]（生成包自带同步）。<br/>
    /// 伤害只在每个目标上结算一次；无 shader，PRT 顶表现（polish 待办）
    /// </summary>
    internal class TitheDetonationProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 30;

        private float Radius => 96f + 8f * MathHelper.Clamp(Projectile.ai[0], 0f,
            ProjectileTithe.MaxEats);

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            //爆炸视觉持续 30 帧，但伤害只在每个目标上结算一次
            Projectile.localNPCHitCooldown = -1;
            Projectile.DamageType = DamageClass.Generic;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                int size = (int)(Radius * 2f);
                Projectile.Resize(size, size);
                SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
                if (Main.netMode != NetmodeID.Server) {
                    SpawnExplosionParticles();
                }
            }
            Projectile.velocity = Vector2.Zero;
            float t = 1f - Projectile.timeLeft / (float)Lifetime;
            Lighting.AddLight(Projectile.Center,
                new Vector3(1f, 0.6f, 0.28f) * MathF.Pow(1f - t, 2f) * 1.2f);
        }

        private void SpawnExplosionParticles() {
            Color main = new(255, 170, 80);
            int count = 18 + (int)(Projectile.ai[0] * 2.5f);
            for (int i = 0; i < count; i++) {
                float angle = MathHelper.TwoPi * i / count
                    + Main.rand.NextFloat(-0.12f, 0.12f);
                float speed = Main.rand.NextFloat(3.5f, 9f)
                    * (0.7f + Projectile.ai[0] / ProjectileTithe.MaxEats * 0.6f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    angle.ToRotationVector2() * speed, main,
                    Main.rand.NextFloat(0.9f, 1.6f))?.Configure(false, 26);
            }
            PRTLoader.NewParticle<PRT_Spark>(Projectile.Center, Vector2.Zero,
                Color.White, 2.2f)?.Configure(false, 12);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return Vector2.Distance(Projectile.Center,
                targetHitbox.Center.ToVector2()) < Radius;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //边缘衰减到六成
            float dist = Vector2.Distance(Projectile.Center, target.Center);
            float falloff = 1f - dist / Radius * 0.4f;
            modifiers.FinalDamage *= MathHelper.Clamp(falloff, 0.6f, 1f);
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.8f;
            }
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
