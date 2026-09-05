using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 黄蜂法杖「蜂巢火网」。材质：丛林黄蜂。<br/>
    /// 签名（运动）：黄蜂不再只悬停放刺——每隔几秒就拉升、俯冲、掠过敌人再拉起，
    /// 俯冲到底时贴脸补一小串毒刺（2 根）。<br/>
    /// 操作奖励：下达突击令（右键点敌）后俯冲间隔缩短四成、掠射改 3 根，且毒刺出膛提速 ×1.3。<br/>
    /// 协同「毒液共振」= 同目标 120 帧内吃满 3 根毒刺，owner 垂落毒爆孢囊（0.9×，爆后留毒雾）。<br/>
    /// 预算：底伤 ×0.85，掠射约每 3.8 秒 +2 刺 ≈ 原版 110%；突击令下 ≈ 140%
    /// </summary>
    internal class GsHornetStaff : GsMinionScheme
    {
        public override int TargetItemID => ItemID.HornetStaff;

        public override string GsFamily => "SummonMinionsA";

        protected override string GsDescFallback =>
            "Hive Firenet: hornets climb, dive through their prey and pull up, spitting a short burst of stingers at the bottom of the dive\nThe assault order makes them dive far more often with a longer burst; three stingers into one foe burst a venom pod";
        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Hive,
            Radius = 70f,
            Spacing = 34f,
            //侧上扇区锚
            SectorAnchor = -2.2f,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes
            => [ProjectileID.Hornet, ProjectileID.HornetStinger];

        /// <summary>毒刺出膛提速标记（每端本地各标一次）</summary>
        private sealed class StingerState
        {
            public bool Boosted;
        }

        /// <summary>毒液共振计数（owner 命中路径独占消费）</summary>
        private readonly GsHitTally tally = new();
        private uint podReadyTick;

        /// <summary>
        /// 俯冲掠射状态（各端各持）。相位切换只依赖 NPC 位置与本地计时，各端结果基本一致，
        /// owner 在掠射期每 8 帧推一次位置同步兜底
        /// </summary>
        private sealed class HornetDiveState
        {
            /// <summary>0 待机 / 1 拉升 / 2 俯冲 / 3 拉起</summary>
            public int Phase;
            public int Timer;
            public int TargetWho = -1;
            public int TargetType;
            public Vector2 DiveDir;
        }

        /// <summary>两次掠射之间的待机帧（护卫 / 突击令）</summary>
        private const int DiveGap = 180;
        private const int DiveGapAssault = 110;
        private const int ClimbFrames = 14;
        private const int DiveFrames = 18;
        private const int PullFrames = 16;
        /// <summary>黄蜂发现目标可掠射的距离</summary>
        private const float SpotRange = 460f;

        //预算：掠射额外毒刺约 +30%，底伤回缩到 0.85 → 护卫态 ≈110%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 0.85f;

        protected override void GsMinionPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type == ProjectileID.Hornet) {
                UpdateDive(proj, router);
                return;
            }
            if (proj.type != ProjectileID.HornetStinger) {
                return;
            }
            //突击技：毒刺出膛帧提速（军旗状态各端一致，首帧同倍缩放随原生同步走）
            StingerState state = router.GetOrCreateState<StingerState>();
            if (!state.Boosted) {
                state.Boosted = true;
                if (MinionDoctrine.GetCommand(proj.owner) == MinionDoctrine.CommandAssault) {
                    proj.velocity *= 1.3f;
                }
            }
        }

        //==================== 俯冲掠射 ====================

        /// <summary>拉升 → 俯冲 → 贴脸毒刺 → 拉起。速度直接覆写原版悬停 AI（原版战斗态下阵型引导已让位）</summary>
        private void UpdateDive(Projectile proj, GodSmithProjRouter router) {
            HornetDiveState st = router.GetOrCreateState<HornetDiveState>();
            bool assault = MinionDoctrine.GetCommand(proj.owner) == MinionDoctrine.CommandAssault;
            switch (st.Phase) {
                case 0: {
                    st.Timer++;
                    int gap = assault ? DiveGapAssault : DiveGap;
                    if (st.Timer < gap) {
                        return;
                    }
                    NPC target = PickDiveTarget(proj);
                    if (target == null) {
                        //没敌人就隔一小会再看
                        st.Timer = gap - 20;
                        return;
                    }
                    st.TargetWho = target.whoAmI;
                    st.TargetType = target.type;
                    st.Phase = 1;
                    st.Timer = 0;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 3 }, proj.Center);
                    }
                    break;
                }
                case 1: {
                    //拉升：向上带一点远离目标的分量，攒高度
                    NPC target = ValidTarget(st);
                    if (target == null) {
                        Reset(st);
                        return;
                    }
                    float awayX = Math.Sign(proj.Center.X - target.Center.X);
                    Vector2 climb = new Vector2(awayX * 0.45f, -1f).SafeNormalize(-Vector2.UnitY) * 7.5f;
                    proj.velocity = Vector2.Lerp(proj.velocity, climb, 0.35f);
                    if (++st.Timer >= ClimbFrames) {
                        st.Phase = 2;
                        st.Timer = 0;
                        st.DiveDir = (target.Center + target.velocity * 6f - proj.Center).SafeNormalize(Vector2.UnitY);
                    }
                    break;
                }
                case 2: {
                    //俯冲：轻微修正朝向，速度 10 → 19 越冲越快
                    NPC target = ValidTarget(st);
                    if (target != null) {
                        Vector2 want = (target.Center + target.velocity * 4f - proj.Center).SafeNormalize(st.DiveDir);
                        st.DiveDir = Vector2.Lerp(st.DiveDir, want, 0.18f).SafeNormalize(want);
                    }
                    float speed = MathHelper.Lerp(10f, 19f, st.Timer / (float)DiveFrames);
                    proj.velocity = st.DiveDir * speed;
                    st.Timer++;
                    bool reached = target != null && proj.Distance(target.Center) <= 36f;
                    if (reached || st.Timer >= DiveFrames) {
                        FireDiveBurst(proj, target, st.DiveDir, assault ? 3 : 2);
                        st.Phase = 3;
                        st.Timer = 0;
                    }
                    break;
                }
                default: {
                    //拉起：沿俯冲方向的水平分量向上抬，速度 11 → 3 收住，然后交还原版悬停
                    Vector2 pull = new Vector2(st.DiveDir.X, -MathF.Abs(st.DiveDir.Y) - 0.6f).SafeNormalize(-Vector2.UnitY);
                    proj.velocity = pull * MathHelper.Lerp(11f, 3f, st.Timer / (float)PullFrames);
                    if (++st.Timer >= PullFrames) {
                        Reset(st);
                    }
                    break;
                }
            }
            if (st.Phase != 0 && proj.IsOwnedByLocalPlayer() && st.Timer % 8 == 0) {
                proj.netUpdate = true;
            }
        }

        private static void Reset(HornetDiveState st) {
            st.Phase = 0;
            st.Timer = 0;
            st.TargetWho = -1;
        }

        /// <summary>校验掠射目标仍在（索引跨端一致，类型失配视为槽位复用）</summary>
        private static NPC ValidTarget(HornetDiveState st) {
            if (st.TargetWho < 0 || st.TargetWho >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[st.TargetWho];
            return npc.active && npc.type == st.TargetType && npc.CanBeChasedBy() ? npc : null;
        }

        /// <summary>突击焦点优先，否则黄蜂附近最近的可追击目标；离主人太远的不追</summary>
        private static NPC PickDiveTarget(Projectile proj) {
            Player owner = Main.player[proj.owner];
            if (MinionDoctrine.TryGetAssaultTarget(proj.owner, out NPC focus)
                && proj.Distance(focus.Center) <= SpotRange * 1.5f) {
                return focus;
            }
            NPC best = null;
            float bestDist = SpotRange;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.Distance(owner.Center) > 900f) {
                    continue;
                }
                float d = npc.Distance(proj.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>俯冲到底的贴脸毒刺（owner 端生成，走原版毒刺类型，命中照常计入毒液共振）</summary>
        private static void FireDiveBurst(Projectile proj, NPC target, Vector2 diveDir, int count) {
            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            Vector2 dir = target != null ? (target.Center - proj.Center).SafeNormalize(diveDir) : diveDir;
            for (int i = 0; i < count; i++) {
                float t = count == 1 ? 0.5f : i / (count - 1f);
                Vector2 vel = dir.RotatedBy(MathHelper.Lerp(-0.18f, 0.18f, t)) * 9f;
                Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, vel,
                    ProjectileID.HornetStinger, proj.damage, proj.knockBack, proj.owner);
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            //只统计毒刺（本体撞击不计共振）
            if (proj.type != ProjectileID.HornetStinger) {
                return;
            }
            int count = tally.Bump(target, proj, 120, out _);
            if (count >= 3 && Main.GameUpdateCount >= podReadyTick) {
                podReadyTick = Main.GameUpdateCount + 120;
                tally.Reset(target);
                Projectile.NewProjectile(proj.GetSource_FromAI(),
                    target.Center - new Vector2(0f, 100f), Vector2.Zero,
                    ModContent.ProjectileType<GsVenomPodProj>(),
                    (int)(proj.damage * 0.9f), 2f, proj.owner);
            }
        }
    }
}
