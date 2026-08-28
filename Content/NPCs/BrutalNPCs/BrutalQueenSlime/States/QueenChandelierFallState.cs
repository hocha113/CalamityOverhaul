using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>水晶吊灯：空中悬晶错帧蓄能坠落，皇后在灯位间掠影穿梭+尖刺扇点压</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.ChandelierFall, typeof(QueenSlimeStateContext))]
    internal class QueenChandelierFallState : QueenSlimeStateBase
    {
        public override string StateName => "ChandelierFall";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.ChandelierFall;

        private const int TotalTime = 350;
        /// <summary>灯间穿梭节拍</summary>
        private const int PerchPeriod = 54;

        private Vector2 stageCenter;
        private bool anchored;
        /// <summary>本拍冲刺方向(发射帧锁定)</summary>
        private Vector2 dartDir = Vector2.UnitX;
        private float dartSpeed;

        public QueenChandelierFallState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
            anchored = false;
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            DisableContactDamage(npc);

            if (!anchored) {
                anchored = true;
                stageCenter = player.Center;
                //挂灯(服务端)
                if (!VaultUtils.isClient) {
                    int count = context.IsAsuraMode ? 5 : 4;
                    float spacing = 300f;
                    for (int i = 0; i < count; i++) {
                        float x = stageCenter.X + (i - (count - 1) * 0.5f) * spacing;
                        Vector2 pos = new Vector2(x, stageCenter.Y - 440f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), pos, Vector2.Zero,
                            ModContent.ProjectileType<QueenChandelierProj>(), QueenChandelierProj.BurstDamage, 0f, Main.myPlayer,
                            i * 26, 0f, i * 0.19f);
                    }
                }
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.85f, Pitch = -0.25f }, npc.Center);
            }

            //皇后在灯位间掠影穿梭：三个高位灯台，跳序 0→2→1 防匀速圆规感
            int perchBeat = (int)Timer / PerchPeriod;
            int perchT = (int)Timer % PerchPeriod;
            int[] perchOrder = [0, 2, 1];
            int perchIdx = perchOrder[perchBeat % 3];
            Vector2 perch = stageCenter + new Vector2((perchIdx - 1) * 430f, -560f + perchIdx * 22f);
            context.PoseCommand = 5;

            if (perchT < 22) {
                //驻灯凝视
                QueenMotion.SpringHover(npc, perch, 0.018f, 0.12f, 12f);
                QueenMotion.FlightLean(npc);
                FaceTarget(npc, player.Center);
                context.WingFlapBoost = 0.6f;
                if (perchT == 21) {
                    //锁定去往下一灯台的冲刺线
                    int nextIdx = perchOrder[(perchBeat + 1) % 3];
                    Vector2 next = stageCenter + new Vector2((nextIdx - 1) * 430f, -560f + nextIdx * 22f);
                    dartDir = (next - npc.Center).SafeNormalize(Vector2.UnitX);
                    dartSpeed = MathHelper.Clamp(Vector2.Distance(npc.Center, next) / 10f, 15f, 30f);
                }
            }
            else if (perchT < 28) {
                //蓄势后拉
                QueenMotion.FlitPullback(npc, dartDir, (perchT - 22) / 6f, 2.2f);
                context.WingFlapBoost = 1.4f;
            }
            else if (perchT == 28) {
                //一帧全速穿灯
                QueenMotion.FlitLaunch(npc, dartDir, dartSpeed);
                context.PushSquash(0.45f);
                context.AfterimageBoost = 1f;
                SoundEngine.PlaySound(SoundID.Item160 with { Volume = 0.45f, Pitch = 0.55f, MaxInstances = 3 }, npc.Center);
            }
            else if (perchT < 40) {
                //直线掠行
                context.AfterimageBoost = Math.Max(context.AfterimageBoost, 0.8f);
                context.WingFlapBoost = 1.5f;
                QueenMotion.FlightLean(npc, 0.045f, 0.5f);
            }
            else {
                //硬刹落位
                QueenMotion.FlitBrake(npc, 0.76f);
                QueenMotion.FlightLean(npc);
                FaceTarget(npc, player.Center);
            }

            //间奏尖刺扇：驻灯期点压(服务端)
            if (perchT == 12 && Timer < TotalTime - 80 && !VaultUtils.isClient) {
                QueenMotion.SpawnSpikeFan(npc, npc.Center, player.Center, 3, 0.2f, 9.2f,
                    QueenCrystalSpikeProj.SpikeDamage, Timer * 0.01f % 1f);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.55f, Pitch = 0.4f }, npc.Center);
            }

            if (Timer >= TotalTime && !VaultUtils.isClient) {
                return new QueenAerialBalletState();
            }

            return null;
        }
    }
}
