using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 三元素轮盘：三球绕身展开旋转，顶位（指向玩家侧）球按其元素开火；终拍当前元素球总爆
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.ElementWheel, typeof(CultistStateContext))]
    internal class CultistElementWheelState : CultistStateBase
    {
        public override string StateName => "ElementWheel";
        public override CultistStateIndex StateIndex => CultistStateIndex.ElementWheel;

        private const int OrbSpawnMoment = 12;
        private const int FireStart = 64;
        private const int FireInterval = 34;
        private const int FinaleMoment = 262;
        private const int Duration = 306;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            FaceTarget(context);
            context.ElementAura = 1f;
            context.CastPose = CultistPose.CastUp;
            context.CastGlow = MathHelper.Clamp(Timer / 40f, 0f, 0.8f);
            CultistScreenFX.DeclareVeil(npc.Center, 0.16f, context.Element);

            //缓慢压向玩家（威慑感）
            if (player.Alives()) {
                Vector2 goal = player.Center + new Vector2(0f, -340f);
                SetHover(context, Vector2.Lerp(npc.Center, goal, 0.4f));
            }

            //展开轮盘
            if ((int)Timer == OrbSpawnMoment) {
                if (!VaultUtils.isClient) {
                    for (int e = 0; e < 3; e++) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                            ModContent.ProjectileType<CultistElementOrb>(), 0, 0f, Main.myPlayer,
                            e, e, npc.whoAmI);
                    }
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item123 with { Volume = 0.7f, Pitch = 0.3f }, npc.Center);
                }
            }

            //蓄力预告：每拍前10帧，预测顶位球并标记膨胀（各端本地推演同一公式）
            const int SwellLead = 10;
            if (!VaultUtils.isServer && Timer >= FireStart - SwellLead && Timer < FinaleMoment - SwellLead
                && ((int)Timer - FireStart + SwellLead) % FireInterval == 0 && player.Alives()) {
                float fireAge = Timer + SwellLead - OrbSpawnMoment;
                int apexOrb = FindApexOrb(npc, player, fireAge);
                Projectile orb = FindOrbProj(npc, apexOrb);
                if (orb != null) {
                    orb.localAI[1] = SwellLead;
                }
            }

            //顶位开火节拍
            if (Timer >= FireStart && Timer < FinaleMoment && ((int)Timer - FireStart) % FireInterval == 0) {
                float orbAge = Timer - OrbSpawnMoment;
                int apexOrb = FindApexOrb(npc, player, orbAge);
                Vector2 orbPos = CultistElementOrb.WheelPos(npc.Center, apexOrb, orbAge);
                int volleyIndex = ((int)Timer - FireStart) / FireInterval;

                if (!VaultUtils.isServer && player.Alives()) {
                    CultistRenderHelper.CastBurst(orbPos, (player.Center - orbPos).SafeNormalize(Vector2.UnitY),
                        (CultistElement)apexOrb, 1.2f);
                    //释放帧：球体白闪+收缩（蓄力→释放的包络闭环）
                    Projectile orb = FindOrbProj(npc, apexOrb);
                    if (orb != null) {
                        orb.localAI[0] = 1f;
                        orb.localAI[1] = 0f;
                    }
                }
                if (!VaultUtils.isClient && player.Alives()) {
                    FireFromOrb(context, npc, player, orbPos, (CultistElement)apexOrb, volleyIndex);
                }
            }

            //终拍：当前元素球总爆
            if ((int)Timer == FinaleMoment) {
                float orbAge = Timer - OrbSpawnMoment;
                Vector2 orbPos = CultistElementOrb.WheelPos(npc.Center, (int)context.Element, orbAge);
                CultistScreenFX.PushFlash(0.4f, 16);
                CultistScreenFX.Punch(orbPos, 7f, 14, "CultistWheelFinale");
                if (!VaultUtils.isServer) {
                    CultistRenderHelper.ElementImpact(orbPos, context.Element, 2f);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.2f }, orbPos);
                }
                if (!VaultUtils.isClient && player.Alives()) {
                    FinaleBurst(context, npc, player, orbPos);
                }
            }

            if (Timer >= Duration) {
                return new CultistWeaveState();
            }
            return null;
        }

        /// <summary>按轮位索引找轮盘球弹幕实体（表现层查找，各端本地）</summary>
        internal static Projectile FindOrbProj(NPC npc, int orbIndex) {
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == ModContent.ProjectileType<CultistElementOrb>()
                    && (int)p.ai[1] == orbIndex && (int)p.ai[2] == npc.whoAmI) {
                    return p;
                }
            }
            return null;
        }

        /// <summary>找当前最接近“指向玩家”方位的球</summary>
        private static int FindApexOrb(NPC npc, Player player, float orbAge) {
            float toPlayer = player.Alives() ? (player.Center - npc.Center).ToRotation() : 0f;
            int best = 0;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < 3; i++) {
                float diff = Math.Abs(MathHelper.WrapAngle(CultistElementOrb.WheelAngle(i, orbAge) - toPlayer));
                if (diff < bestDiff) {
                    bestDiff = diff;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>顶位球开火：元素各有语言</summary>
        private void FireFromOrb(CultistStateContext context, NPC npc, Player player,
            Vector2 orbPos, CultistElement element, int volleyIndex) {
            Vector2 aim = (player.Center + player.velocity * 12f - orbPos).SafeNormalize(Vector2.UnitY);
            var source = npc.GetSource_FromAI();
            switch (element) {
                case CultistElement.Fire: {
                    int damage = ProjDamage(npc, 38f, 27f);
                    for (int i = 0; i < 4; i++) {
                        Vector2 vel = aim.RotatedBy(MathHelper.Lerp(-0.34f, 0.34f, i / 3f)) * 6.6f;
                        Projectile.NewProjectile(source, orbPos, vel,
                            ModContent.ProjectileType<CultistFireBolt>(), damage, 0f, Main.myPlayer, 30f, 0f);
                    }
                    break;
                }
                case CultistElement.Ice: {
                    int damage = ProjDamage(npc, 40f, 28f);
                    for (int i = -1; i <= 1; i += 2) {
                        Vector2 pos = orbPos + aim.RotatedBy(i * 0.5f) * 40f;
                        Projectile.NewProjectile(source, pos, aim,
                            ModContent.ProjectileType<CultistIceLance>(), damage, 0f, Main.myPlayer, 30f, 18f);
                    }
                    if (volleyIndex % 2 == 0) {
                        Projectile.NewProjectile(source, orbPos, aim * 1.4f,
                            ModContent.ProjectileType<CultistFrostMistZone>(), 0, 0f, Main.myPlayer);
                    }
                    break;
                }
                default: {
                    int damage = ProjDamage(npc, 38f, 27f);
                    for (int i = -1; i <= 1; i += 2) {
                        Vector2 vel = aim.RotatedBy(i * 0.16f) * 7.4f;
                        Projectile.NewProjectile(source, orbPos, vel,
                            ModContent.ProjectileType<CultistArcSpark>(), damage, 0f, Main.myPlayer,
                            (float)CultistElement.Thunder, 0f);
                    }
                    if (volleyIndex % 2 == 1) {
                        int colDamage = ProjDamage(npc, 44f, 30f);
                        Vector2 ground = CultistElementBarrageState.FindGround(player.Center);
                        Projectile.NewProjectile(source, ground, Vector2.Zero,
                            ModContent.ProjectileType<CultistThunderColumn>(), colDamage, 0f, Main.myPlayer, 48f, 1400f);
                    }
                    break;
                }
            }
        }

        /// <summary>终拍总爆：当前元素的大签名</summary>
        private void FinaleBurst(CultistStateContext context, NPC npc, Player player, Vector2 orbPos) {
            var source = npc.GetSource_FromAI();
            switch (context.Element) {
                case CultistElement.Fire: {
                    int damage = ProjDamage(npc, 40f, 28f);
                    for (int i = 0; i < 10; i++) {
                        Vector2 vel = (MathHelper.TwoPi * i / 10f).ToRotationVector2() * 5.4f;
                        //环爆火球半数留焚地
                        Projectile.NewProjectile(source, orbPos, vel,
                            ModContent.ProjectileType<CultistFireBolt>(), damage, 0f, Main.myPlayer, 0f, i % 2);
                    }
                    break;
                }
                case CultistElement.Ice: {
                    int damage = ProjDamage(npc, 42f, 29f);
                    for (int i = 0; i < 8; i++) {
                        float angle = MathHelper.TwoPi * i / 8f;
                        Vector2 pos = player.Center + angle.ToRotationVector2() * 520f;
                        Vector2 aim = (player.Center - pos).SafeNormalize(Vector2.UnitY);
                        Projectile.NewProjectile(source, pos, aim,
                            ModContent.ProjectileType<CultistIceLance>(), damage, 0f, Main.myPlayer, 42f, 21f);
                    }
                    break;
                }
                default: {
                    int damage = ProjDamage(npc, 46f, 31f);
                    for (int i = -1; i <= 1; i++) {
                        Vector2 ground = CultistElementBarrageState.FindGround(player.Center + new Vector2(i * 150f, 0f));
                        Projectile.NewProjectile(source, ground, Vector2.Zero,
                            ModContent.ProjectileType<CultistThunderColumn>(), damage, 0f, Main.myPlayer,
                            52f + Math.Abs(i) * 10f, 1400f);
                    }
                    break;
                }
            }
        }
    }
}
