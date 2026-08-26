using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 虚空撕裂（低血大招，一场一次）：光被吸走的三拍蓄势→三道追踪衰减死光弧
    /// 呈三辉弧展开→星陨/波列崩解收束→长硬直大惩罚窗（受击加伤）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.VoidRupture, typeof(MLordContext))]
    internal class MLordVoidRuptureState : MLordStateBase
    {
        public override string StateName => "VoidRupture";
        public override MLordStateIndex StateIndex => MLordStateIndex.VoidRupture;

        internal const int ChargeEnd = 100;
        internal const int RaysEnd = ChargeEnd + MLordArcRayProj.TotalLife;
        internal const int BurstEnd = RaysEnd + 26;
        internal const int StaggerEnd = BurstEnd + 78;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                context.Owner.ai[MLordAiSlots.OvUltUsed] = 1f;
                context.Owner.ai[MLordAiSlots.OvEyeCommand] = MLordEyeCommand.Retreat;
                context.Owner.ai[MLordAiSlots.OvAttackSeed] = Main.rand.Next(1, 100000);
                //清自家死光（含真眼链束与月明湮灭巨束），让大招独占舞台
                foreach (Projectile p in Main.ActiveProjectiles) {
                    if (p.type == ModContent.ProjectileType<MLordScanRayProj>()
                        || p.type == ModContent.ProjectileType<MLordArcRayProj>()
                        || p.type == ModContent.ProjectileType<MLordEyeLinkProj>()
                        || p.type == ModContent.ProjectileType<MLordAnnihilationRayProj>()) {
                        p.Kill();
                    }
                }
                context.Npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie96 with { Volume = 1.2f, Pitch = -0.75f }, context.Npc.Center);
            }
        }

        public override void OnExit(MLordContext context) {
            base.OnExit(context);
            if (!VaultUtils.isClient) {
                context.Owner.ai[MLordAiSlots.OvEyeCommand] = MLordEyeCommand.Solo;
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            //大招发射架：黑臂四爪抓桩定身；蓄势期锁伤（玩家该走位而非抢血线），出弧后放开
            RequestMove(context, target.Center + new Vector2(0f, -430f), 0.5f, MLordMovePolicy.Brace);
            UpdateLean(context);
            context.EclipseDrive = 1f;
            npc.dontTakeDamage = Timer < ChargeEnd;

            if (Timer < ChargeEnd) {
                UpdateCharge(context);
            }
            else if (Timer == ChargeEnd) {
                FireTriArc(context);
            }
            else if (Timer > RaysEnd && Timer <= BurstEnd) {
                UpdateCollapseBurst(context);
            }
            else if (Timer > BurstEnd) {
                //硬直惩罚窗：心脏洞开，受击加伤
                context.StaggerVulnerable = true;
                context.HeartExposure = 1f;
                npc.velocity *= 0.9f;
            }

            //弧光期间点缀星陨
            if (!VaultUtils.isClient && (Timer == ChargeEnd + 66 || Timer == ChargeEnd + 128)) {
                SpawnPunctuationComets(context);
            }

            Timer++;
            if (Timer >= StaggerEnd) {
                return NextAttack(context);
            }
            return null;
        }

        /// <summary>蓄势三拍：吸光、聚星、升调蜂鸣</summary>
        private void UpdateCharge(MLordContext context) {
            NPC npc = context.Npc;
            context.SetChargeState(Timer / (float)ChargeEnd);
            context.HoldAllParts = true;

            if (VaultUtils.isServer) {
                return;
            }
            MLordScreenEffects.PushGravityDim(npc.Center, Timer / (float)ChargeEnd * 0.85f);
            MLordScreenFX.ConvergeStreak(npc.Center, 560f, Timer / (float)ChargeEnd);

            //三拍升调蜂鸣（固定 28f 节拍，玩家可内化）
            if (Timer == 28 || Timer == 56 || Timer == 84) {
                float pitch = -0.4f + (Timer / 28) * 0.3f;
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.9f, Pitch = pitch }, npc.Center);
                MLordScreenFX.Punch(npc.Center, 3f + Timer / 28f, 8);
            }
        }

        /// <summary>
        /// 三辉弧：120° 相位差的追踪衰减死光（宿主为大招期核心自动挂追踪）。
        /// 公平阀（契约3）：三束共享刚性 120° 相位追踪——ai[2] 传各束固定偏移，
        /// 追踪只旋整个三叉阵，任意时刻保证两个完整 120° 逃生扇区（独立追踪会收敛塌缩扇区）
        /// </summary>
        private void FireTriArc(MLordContext context) {
            if (!VaultUtils.isServer) {
                MLordScreenEffects.PushStarRing(context.Npc.Center, 1.1f, 980f, 34);
                MLordScreenFX.Punch(context.Npc.Center, 10f, 18);
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 1.2f, Pitch = -0.6f }, context.Npc.Center);
            }
            if (VaultUtils.isClient) {
                return;
            }
            NPC npc = context.Npc;
            int damage = ScaleDamage(context, MLordDirector.UltRayDamage);
            float baseAngle = (context.Target.Center - npc.Center).ToRotation();
            for (int i = 0; i < 3; i++) {
                float offset = MathHelper.TwoPi / 3f * i;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<MLordArcRayProj>(), damage, 0f, Main.myPlayer,
                    npc.whoAmI, baseAngle + offset, offset);
            }
        }

        /// <summary>崩解收束：星环 + 双重旋转缺口波列</summary>
        private void UpdateCollapseBurst(MLordContext context) {
            NPC npc = context.Npc;
            if (Timer == RaysEnd + 4 && !VaultUtils.isServer) {
                MLordScreenEffects.PushStarRing(npc.Center, 1.2f, 1200f, 40);
                MLordScreenFX.StarBurst(npc.Center, 2.2f, 30);
                MLordScreenFX.Punch(npc.Center, 9f, 16);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f, Pitch = -0.5f }, npc.Center);
            }
            if (VaultUtils.isClient) {
                return;
            }
            int seed = (int)context.Owner.ai[MLordAiSlots.OvAttackSeed];
            //两圈缺口环：不同起始相位，缺口错开
            if (Timer == RaysEnd + 6 || Timer == RaysEnd + 20) {
                int ring = Timer == RaysEnd + 6 ? 0 : 1;
                int count = 14;
                int gapAt = (int)(MLordConstellationProj.Hash01(seed, 60 + ring) * count);
                float baseAngle = MLordConstellationProj.Hash01(seed, 70 + ring) * MathHelper.TwoPi;
                int damage = ScaleDamage(context, MLordDirector.BoltDamage);
                for (int i = 0; i < count; i++) {
                    //连缺三位形成可穿越走廊
                    int delta = (i - gapAt + count) % count;
                    if (delta <= 2) {
                        continue;
                    }
                    float angle = baseAngle + MathHelper.TwoPi / count * i;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center,
                        angle.ToRotationVector2() * (5.2f + ring * 1.6f),
                        ModContent.ProjectileType<MLordBoltProj>(), damage, 0f, Main.myPlayer);
                }
            }
        }

        /// <summary>弧光期间的点缀彗星（直落玩家两侧，无星火）</summary>
        private void SpawnPunctuationComets(MLordContext context) {
            Player target = context.Target;
            int damage = ScaleDamage(context, MLordDirector.CometDamage);
            float groundY = MLordScreenFX.FindGroundBelow(target.Center).Y + 40f;
            for (int i = 0; i < 4; i++) {
                float offsetX = (i - 1.5f) * 260f;
                Vector2 spawn = target.Center + new Vector2(offsetX, -760f);
                Projectile.NewProjectile(context.Npc.GetSource_FromAI(), spawn,
                    new Vector2(0f, 9f), ModContent.ProjectileType<MLordCometProj>(),
                    damage, 0f, Main.myPlayer, 0f, 0f, groundY);
            }
        }
    }
}
