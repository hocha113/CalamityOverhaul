using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States
{
    /// <summary>
    /// 双眼激光扫描协议：双眼充能后各自射出持续扫描光束，
    /// 三角波错拍上下扫掠，当一束在极点时另一束恰在中线，永远存在可穿越的窗口。
    /// 慢速可读的"死亡扫描线"，非双子式剪刀交叉
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)WofStateIndex.EyeScan, typeof(WofStateContext))]
    internal class WofEyeScanState : WofStateBase
    {
        public override string StateName => "EyeScan";
        public override WofStateIndex StateIndex => WofStateIndex.EyeScan;

        private const int Outro = 26;

        private int ScanFrames(WofStateContext ctx) => ctx.Phase >= 3 ? WofDirector.ScanDuration + 40 : WofDirector.ScanDuration;

        public override void OnEnter(WofStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item15 with { Pitch = -0.7f, Volume = 0.9f }, context.Npc.Center);
            }
        }

        public override IWofState OnUpdate(WofStateContext context) {
            NPC npc = context.Npc;
            Timer++;
            int charge = WofDirector.ScanCharge;
            int scanEnd = charge + ScanFrames(context);
            int totalEnd = scanEnd + Outro;

            if (Timer <= charge) {
                //充能：眼部通道亮起(眼由视觉通道读取 eyeCharge 发光与细线预告)
                float p = Timer / (float)charge;
                context.AdvanceFactor = 0.45f;
                context.SetChargeState(4, p);
                context.WallFlush = 0.4f + 0.25f * p;
                context.MouthCommand = 2;

                //末段静默拍
                if (Timer == charge - 10 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.7f, Volume = 0.7f }, npc.Center);
                }
                return null;
            }

            if (Timer == charge + 1) {
                FireBeams(context);
            }

            if (Timer <= scanEnd) {
                //扫描期：墙缓推，扫描线本身是移动的死线
                context.AdvanceFactor = 0.55f;
                context.SetChargeState(4, 1f);
                context.WallFlush = 0.55f;
                context.MouthCommand = 0;
                return null;
            }

            //收束
            context.AdvanceFactor = 0.8f;
            context.ResetChargeState();
            if (Timer >= totalEnd) {
                return new WofAdvanceState();
            }
            return null;
        }

        /// <summary>为两只眼各生成一束扫描光束(服务端)；相位错半周期。无眼可用则立即收招</summary>
        private void FireBeams(WofStateContext context) {
            NPC npc = context.Npc;
            if (!VaultUtils.isClient) {
                int damage = WallOfFleshAI.ScaleDamage(npc, WofDirector.ScanBeamDamage);
                float speedScale = context.Phase >= 3 ? 1.25f : 1f;
                if (context.IsAsuraMode) {
                    speedScale *= 1.15f;
                }
                int fired = 0;
                foreach (var n in Main.ActiveNPCs) {
                    if (n.type != NPCID.WallofFleshEye) {
                        continue;
                    }
                    //上眼(ai0=1)相位0，下眼(ai0=-1)相位半周期
                    float phase = n.ai[0] > 0f ? 0f : 0.5f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), n.Center, Vector2.Zero,
                        ModContent.ProjectileType<WofRetinaScanBeam>(), damage, 0f, Main.myPlayer,
                        n.whoAmI, phase, speedScale);
                    fired++;
                }
                //异常兜底：双眼缺位(理论上共享血量不会死)时不空转扫描窗
                if (fired == 0) {
                    Timer = WofDirector.ScanCharge + ScanFrames(context);
                }
                npc.netUpdate = true;
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie103 with { Pitch = -0.35f, Volume = 1.05f }, npc.Center);
                foreach (var n in Main.ActiveNPCs) {
                    if (n.type == NPCID.WallofFleshEye) {
                        WofMotionFX.SpawnBloodBurst(n.Center, 0.8f, new Vector2(npc.direction, 0f));
                    }
                }
                WofMotionFX.CameraPunch(npc.Center, 4f, 12, "WofScanFire");
            }
        }
    }
}
