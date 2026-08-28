using CalamityOverhaul.Content.GameModes.BrutalMobs.Common;
using CalamityOverhaul.Content.GameModes.BrutalMobs.DungeonDeep.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.DungeonDeep
{
    /// <summary>
    /// 远程分册：法师族吟唱齐射（法阵 omen 提交发射，传送/死亡即散）、
    /// 诅咒颅族咒锁俯冲（绕旋预告→锁向短冲，不追踪）、射手族三型
    /// （狙击长标线单发 / 战术短扇面三连 / 突击兵火箭抛物）
    /// </summary>
    internal partial class DungeonDeepNPC
    {
        //==== 法师族吟唱齐射 ====
        private const float CasterMinRange = 170f;
        private const float CasterMaxRange = 720f;
        /// <summary>吟唱法阵全局并发上限（火柱随吟唱 1:1 生成，经此闸间接受限）</summary>
        private const int CastCap = 4;
        /// <summary>吟唱被原版传送打断的位移阈值平方（打断=可感的取消）</summary>
        private const float CastBreakDistSq = 160f * 160f;
        /// <summary>提交后的收势帧</summary>
        private const int CastRecoverFrames = 10;
        private static readonly int[] CastCooldownByTier = [380, 330, 280];
        /// <summary>水矢/咒焰/影束伤害 = npc.damage（已缩放值）× 各值</summary>
        private const float WaterDamageFrac = 0.45f;
        private const float CursedDamageFrac = 0.5f;
        private const float ShadowDamageFrac = 0.55f;
        /// <summary>地狱火柱伤害比例</summary>
        private const float PillarDamageFrac = 0.6f;

        //==== 诅咒颅族咒锁俯冲 ====
        private const float SkullMinRange = 130f;
        private const float SkullMaxRange = 520f;
        /// <summary>绕旋目标半径与切向速度（前摇轨道姿态，非打击位移）</summary>
        private const float SkullOrbitRadius = 190f;
        private const float SkullOrbitSpeed = 6f;
        /// <summary>俯冲名义峰速（小颅更快，大颅更重）</summary>
        private const float SkullDashPeak = 10.5f;
        private const float GiantDashPeak = 9.5f;
        /// <summary>大颅冲刺后的四向咒火弹速与伤害比例</summary>
        private const float CrossBoltSpeed = 5.5f;
        private const float CrossDamageFrac = 0.45f;
        /// <summary>咒颅预告全局并发上限</summary>
        private const int SkullCap = 3;
        private static readonly int[] SkullCooldownByTier = [300, 260, 220];

        //==== 射手族 ====
        private const float SniperMinRange = 240f;
        private const float SniperMaxRange = 1100f;
        private const float TacticalMinRange = 130f;
        private const float TacticalMaxRange = 540f;
        private const float RocketMinRange = 260f;
        private const float RocketMaxRange = 900f;
        /// <summary>射手预告全局并发上限</summary>
        private const int MarksmanCap = 4;
        /// <summary>火箭全局并发上限</summary>
        private const int RocketCap = 3;
        private const float SniperDamageFrac = 0.8f;
        private const float PelletDamageFrac = 0.38f;
        private const float RocketDamageFrac = 0.6f;
        /// <summary>火箭出膛后座与收势帧</summary>
        private const int RocketRecoverFrames = 14;
        private static readonly int[] SniperCooldownByTier = [420, 370, 320];
        private static readonly int[] TacticalCooldownByTier = [360, 315, 270];
        private static readonly int[] RocketCooldownByTier = [500, 450, 400];

        #region 法师族吟唱齐射
        /// <summary>吟唱起手：法阵 omen（来源校验、死亡即散）；Diabolist 同帧在目标脚下点火柱（落点即承诺）</summary>
        private void TryStartCaster(NPC npc, Player player) {
            float dist = npc.Distance(player.Center);
            if (dist < CasterMinRange || dist > CasterMaxRange) {
                cooldown = RetryDelay;
                return;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)) {
                cooldown = RetryDelay;
                return;
            }
            if (CountActive(ModContent.ProjectileType<DdCastOmen>()) >= CastCap) {
                cooldown = RetryDelay;
                return;
            }
            DdCastRow row = CastRows[npc.type];

            //火柱先找地：目标悬空则本次不施法
            Vector2 pillarBase = default;
            if (row.Mode == DdCastOmen.ModePillar && !TryFindGround(player, out pillarBase)) {
                cooldown = RetryDelay;
                return;
            }

            //预告即承诺：瞄角在此帧锁死随生成包同步
            float aim = (player.Center - npc.Center).ToRotation();
            float damageFrac = row.Mode switch {
                DdCastOmen.ModeCursed => CursedDamageFrac,
                DdCastOmen.ModeShadow => ShadowDamageFrac,
                DdCastOmen.ModePillar => 0f,//火柱伤害走火柱实体
                _ => WaterDamageFrac,
            };
            int damage = Math.Max(1, (int)(npc.damage * damageFrac));
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<DdCastOmen>(), damage, 0f, Main.myPlayer,
                PackSource(npc), aim, boundTier);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                AbortToCooldown(RetryDelay);
                return;
            }
            if (row.Mode == DdCastOmen.ModePillar) {
                int pillarDamage = Math.Max(1, (int)(npc.damage * PillarDamageFrac));
                auxIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), pillarBase, Vector2.Zero,
                    ModContent.ProjectileType<DdFirePillarProj>(), pillarDamage, 1f, Main.myPlayer,
                    PackSource(npc), row.AuxA, row.AuxB);
                if (auxIndex < 0 || auxIndex >= Main.maxProjectiles) {
                    //火柱生成失败：撤回吟唱（失败方向=安全方向）
                    Main.projectile[omenIndex].Kill();
                    AbortToCooldown(RetryDelay);
                    return;
                }
            }
            //立定吟唱：记录吟唱位用于传送打断检测
            lockPoint = npc.Center;
            npc.velocity.X *= 0.1f;
            npc.netUpdate = true;
            phase = PhaseWindup;
            timer = DdCastOmen.TelegraphFrames + CastRecoverFrames;
        }

        private void TickCaster(NPC npc) {
            timer--;
            bool casting = timer > CastRecoverFrames;
            if (casting) {
                if (!TryGetBoundOmen(omenIndex, ModContent.ProjectileType<DdCastOmen>(), npc, 0, out _)) {
                    KillBoundPillar(npc);
                    AbortToCooldown(CastCooldownByTier[boundTier - 1]);
                    return;
                }
                //原版传送打断吟唱：法阵与火柱一并散去（可感的取消），回冷却
                if (Vector2.DistanceSquared(npc.Center, lockPoint) > CastBreakDistSq) {
                    Main.projectile[omenIndex].Kill();
                    KillBoundPillar(npc);
                    AbortToCooldown(CastCooldownByTier[boundTier - 1]);
                    return;
                }
                if (timer % 8 == 0) {
                    //立定吟唱：离散刹车脉冲（脉冲帧才跟同步）
                    npc.velocity.X *= 0.35f;
                    npc.netUpdate = true;
                }
                return;
            }
            if (timer <= 0) {
                //提交发射由 omen 自理，本体收势后归位
                omenIndex = -1;
                auxIndex = -1;
                phase = PhaseIdle;
                cooldown = CastCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            }
        }

        /// <summary>撤回名下火柱（仅预告期有效；已喷发的火收不回，由火柱自身的来源校验兜底）</summary>
        private void KillBoundPillar(NPC npc) {
            if (TryGetBoundOmen(auxIndex, ModContent.ProjectileType<DdFirePillarProj>(), npc, 0, out Projectile pillar)) {
                pillar.Kill();
            }
            auxIndex = -1;
        }
        #endregion

        #region 诅咒颅族咒锁俯冲
        /// <summary>咒锁起手：绕旋预告实体（轨迹咒光点），30 帧后锁向短冲</summary>
        private void TryStartSkull(NPC npc, Player player) {
            float dist = npc.Distance(player.Center);
            if (dist < SkullMinRange || dist > SkullMaxRange || !CanSee(npc, player)) {
                cooldown = RetryDelay;
                return;
            }
            if (CountActive(ModContent.ProjectileType<DdSkullOmen>()) >= SkullCap) {
                cooldown = RetryDelay;
                return;
            }
            int variant = npc.type == NPCID.GiantCursedSkull ? 1 : 0;
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<DdSkullOmen>(), 0, 0f, Main.myPlayer,
                PackSource(npc), variant, 0f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                AbortToCooldown(RetryDelay);
                return;
            }
            npc.velocity *= 0.4f;
            npc.netUpdate = true;
            phase = PhaseWindup;
            timer = DdSkullOmen.OrbitFrames;
        }

        private void TickSkull(NPC npc) {
            bool giant = npc.type == NPCID.GiantCursedSkull;
            if (phase == PhaseWindup) {
                timer--;
                if (!TryGetBoundOmen(omenIndex, ModContent.ProjectileType<DdSkullOmen>(), npc, 0, out Projectile omen)) {
                    AbortToCooldown(SkullCooldownByTier[boundTier - 1]);
                    return;
                }
                if (npc.target < 0 || npc.target >= Main.maxPlayers || !Main.player[npc.target].Alives()) {
                    omen.Kill();
                    AbortToCooldown(RetryDelay);
                    return;
                }
                Player player = Main.player[npc.target];

                //绕旋（前摇轨道姿态，非打击位移）：切向绕转+半径回缩，速度除回提速补偿
                Vector2 rel = npc.Center - player.Center;
                float radius = rel.Length();
                Vector2 radialOut = rel.SafeNormalize(Vector2.UnitX);
                float sign = npc.whoAmI % 2 == 0 ? 1f : -1f;
                Vector2 tangent = radialOut.RotatedBy(MathHelper.PiOver2 * sign);
                float pull = MathHelper.Clamp((radius - SkullOrbitRadius) * 0.05f, -2.5f, 2.5f);
                npc.velocity = (tangent * SkullOrbitSpeed - radialOut * pull) / MoveGain;
                if (timer % 6 == 0) {
                    npc.netUpdate = true;
                }

                if (timer <= 0) {
                    //锁向：方向自此为承诺，写回预告实体一次性同步（此后不追踪）
                    lockDir = (player.Center - npc.Center).ToRotation();
                    omen.ai[2] = lockDir + 10f;
                    omen.netUpdate = true;
                    phase = PhaseStrike;
                    timer = 0;
                    npc.netUpdate = true;
                }
                return;
            }
            if (phase == PhaseStrike) {
                timer++;
                float peak = giant ? GiantDashPeak : SkullDashPeak;
                float env = MobDash.Envelope(timer, 6, 10, 10);
                npc.velocity = lockDir.ToRotationVector2() * (peak / MoveGain) * env;
                if (timer == 1 || timer % 6 == 0) {
                    npc.netUpdate = true;
                }
                if (timer >= DdSkullOmen.StrikeFrames) {
                    if (giant) {
                        SpawnCross(npc);
                    }
                    phase = PhaseRecover;
                    timer = 12;
                    npc.netUpdate = true;
                }
                return;
            }
            timer--;
            npc.velocity *= 0.86f;
            if (timer <= 0) {
                omenIndex = -1;
                phase = PhaseIdle;
                cooldown = SkullCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            }
        }

        /// <summary>大颅冲刺尾帧的咒火小十字：固定上下左右四向发射，固定向即非追踪保证</summary>
        private void SpawnCross(NPC npc) {
            int damage = Math.Max(1, (int)(npc.damage * CrossDamageFrac));
            int boltType = ModContent.ProjectileType<DdBoltProj>();
            for (int k = 0; k < 4; k++) {
                Vector2 vel = (MathHelper.PiOver2 * k).ToRotationVector2() * CrossBoltSpeed;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                    boltType, damage, 0f, Main.myPlayer, DdBoltProj.ModeCross);
            }
        }
        #endregion

        #region 射手族
        /// <summary>射手起手：狙击=追踪长标线（锁定即承诺）；战术=生成帧锁角短扇面（三连同角同缺口）</summary>
        private void TryStartMarksman(NPC npc, Player player) {
            bool sniper = family == DdFamily.Sniper;
            float min = sniper ? SniperMinRange : TacticalMinRange;
            float max = sniper ? SniperMaxRange : TacticalMaxRange;
            float dist = npc.Distance(player.Center);
            if (dist < min || dist > max) {
                cooldown = RetryDelay;
                return;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)) {
                cooldown = RetryDelay;
                return;
            }
            if (CountActive(ModContent.ProjectileType<DdMarksmanOmen>()) >= MarksmanCap) {
                cooldown = RetryDelay;
                return;
            }

            float frac = sniper ? SniperDamageFrac : PelletDamageFrac;
            int damage = Math.Max(1, (int)(npc.damage * frac));
            float aim = (player.Center - npc.Center).ToRotation();
            //狙击标线先追踪后锁定（ai[2]=0 表示未锁）；战术扇面生成帧即锁角
            omenIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<DdMarksmanOmen>(), damage, 0f, Main.myPlayer,
                PackSource(npc), sniper ? DdMarksmanOmen.ModeSniper : DdMarksmanOmen.ModeTactical,
                sniper ? 0f : aim + 10f);
            if (omenIndex < 0 || omenIndex >= Main.maxProjectiles) {
                AbortToCooldown(RetryDelay);
                return;
            }
            npc.velocity.X *= 0.1f;
            npc.netUpdate = true;
            phase = PhaseWindup;
            timer = sniper
                ? DdMarksmanOmen.SniperTelegraphFrames + 8
                : DdMarksmanOmen.TacticalTelegraphFrames + DdMarksmanOmen.TacticalVolleyGap * 2 + 8;
        }

        private void TickMarksman(NPC npc) {
            timer--;
            bool sniper = family == DdFamily.Sniper;
            if (timer <= 0) {
                omenIndex = -1;
                phase = PhaseIdle;
                cooldown = (sniper ? SniperCooldownByTier : TacticalCooldownByTier)[boundTier - 1]
                    + Main.rand.Next(CooldownJitter + 1);
                return;
            }
            //收势尾帧不再校验（预告实体寿命略短于本相位，齐射早已提交完毕）
            if (timer > 6) {
                if (!TryGetBoundOmen(omenIndex, ModContent.ProjectileType<DdMarksmanOmen>(), npc, 0, out Projectile omen)) {
                    AbortToCooldown((sniper ? SniperCooldownByTier : TacticalCooldownByTier)[boundTier - 1]);
                    return;
                }
                //狙击锁定帧：标线方向自此为承诺，写 ai[] 一次性同步（镜像俯冲预告的锁向写法，独立实现）
                if (sniper && timer == DdMarksmanOmen.SniperLockFrames + 8
                    && npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].Alives()) {
                    lockDir = (Main.player[npc.target].Center - npc.Center).ToRotation();
                    omen.ai[2] = lockDir + 10f;
                    omen.netUpdate = true;
                }
            }
            if (timer % 10 == 0) {
                //据枪立定：离散刹车脉冲
                npc.velocity.X *= 0.3f;
                npc.netUpdate = true;
            }
        }

        /// <summary>
        /// 火箭抛物：落点此帧锁死（警示环从 0 帧起画满 ≥36 帧），弹道帧数与初速一并解算随生成包同步；
        /// 伤害窗=落点环的爆窗（火箭飞行全程无判定）
        /// </summary>
        private void TryStartRocket(NPC npc, Player player) {
            float dist = npc.Distance(player.Center);
            if (dist < RocketMinRange || dist > RocketMaxRange) {
                cooldown = RetryDelay;
                return;
            }
            if (CountActive(ModContent.ProjectileType<DdRocketProj>()) >= RocketCap) {
                cooldown = RetryDelay;
                return;
            }
            //落点=目标脚下地面；悬空目标取其本体中心（空中警示环）
            if (!TryFindGround(player, out Vector2 basePos)) {
                basePos = player.Center;
            }
            lockPoint = basePos;

            Vector2 muzzle = npc.Center - Vector2.UnitY * 10f;
            Vector2 to = lockPoint - muzzle;
            int flight = (int)MathHelper.Clamp(to.Length() / 8f, DdRocketProj.RingWarnMinFrames, 64f);
            //抛物解算：位移项/重力项按原版口径，火箭为表现层不受模式提速影响
            Vector2 vel = new(
                MathHelper.Clamp(to.X / flight, -12f, 12f),
                MathHelper.Clamp(to.Y / flight - DdRocketProj.Gravity * (flight - 1) * 0.5f, -15f, 8f));
            int damage = Math.Max(1, (int)(npc.damage * RocketDamageFrac));
            int index = Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, vel,
                ModContent.ProjectileType<DdRocketProj>(), damage, 1.5f, Main.myPlayer,
                lockPoint.X, lockPoint.Y, flight);
            if (index < 0 || index >= Main.maxProjectiles) {
                cooldown = RetryDelay;
                return;
            }
            //出膛后座
            npc.velocity.X -= Math.Sign(to.X) * 1.5f;
            npc.netUpdate = true;
            phase = PhaseRecover;
            timer = RocketRecoverFrames;
        }

        private void TickRocketRecover(NPC npc) {
            timer--;
            if (timer <= 0) {
                phase = PhaseIdle;
                cooldown = RocketCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            }
        }
        #endregion
    }
}
