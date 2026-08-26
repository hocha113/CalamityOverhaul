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
    /// 星陨召唤：头部仰颂，天空先连出星座标明弹道，再沿星图逐位坠下弯折彗星，
    /// 双波次（第二波预判走位），落点留星火封位。星图与弹道同种子，各端一致
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.Starfall, typeof(MLordContext))]
    internal class MLordStarfallState : MLordStateBase
    {
        public override string StateName => "Starfall";
        public override MLordStateIndex StateIndex => MLordStateIndex.Starfall;

        internal const int WaveOneReveal = 10;
        internal const int WaveOneFire = 76;
        internal const int WaveTwoReveal = 212;
        internal const int WaveTwoFire = 278;
        internal const int CometStagger = 9;

        private int nodeCount;
        private int stateLength;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            nodeCount = context.CoreExposed ? 7 : 6;
            //收尾拍 80：末彗星落地+星火燃尽即换拍，不拖节奏
            stateLength = WaveTwoFire + nodeCount * CometStagger + Frames(context, 80);
            if (!VaultUtils.isClient) {
                context.Owner.ai[MLordAiSlots.OvAttackSeed] = Main.rand.Next(1, 100000);
                context.Npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie95 with { Volume = 0.9f, Pitch = -0.35f }, context.Npc.Center);
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            Player target = context.Target;

            //头部仰颂，核心压低身位缓爬跟随（颂唱期潜行的诡异慢步）
            RequestMove(context, target.Center + MLordDirector.CoreHoverOffset + new Vector2(0f, 70f), 0.55f);
            UpdateLean(context);

            if (!VaultUtils.isClient) {
                RunServer(context);
            }

            //颂唱期蓄力观感
            bool channeling = Timer < WaveOneFire || (Timer >= WaveTwoReveal && Timer < WaveTwoFire);
            if (channeling) {
                context.SetChargeState(0.5f + 0.5f * (float)System.Math.Sin(Timer * 0.09f));
                if (!VaultUtils.isServer && context.Parts.Head >= 0) {
                    MLordScreenFX.ConvergeStreak(Main.npc[context.Parts.Head].Center, 240f, 0.4f);
                }
            }
            else {
                context.ResetChargeState();
            }

            Timer++;
            if (Timer >= stateLength) {
                return NextAttack(context);
            }
            return null;
        }

        private void RunServer(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;
            int seed = (int)context.Owner.ai[MLordAiSlots.OvAttackSeed];

            //两波星图：第一波挂当前位，第二波带预判
            if (Timer == WaveOneReveal || Timer == WaveTwoReveal) {
                bool second = Timer == WaveTwoReveal;
                Vector2 anchor = target.Center + new Vector2(0f, -640f)
                    + (second ? target.velocity * 34f : Vector2.Zero);
                context.Owner.ai[MLordAiSlots.OvAnchorX] = anchor.X;
                context.Owner.ai[MLordAiSlots.OvAnchorY] = anchor.Y;
                npc.netUpdate = true;
                Projectile.NewProjectile(npc.GetSource_FromAI(), anchor, Vector2.Zero,
                    ModContent.ProjectileType<MLordConstellationProj>(), 0, 0f, Main.myPlayer,
                    seed + (second ? 1 : 0), nodeCount, WaveOneFire - WaveOneReveal + nodeCount * CometStagger);
            }

            //沿星图弹道逐位坠彗星
            SpawnWaveComets(context, WaveOneFire, seed);
            SpawnWaveComets(context, WaveTwoFire, seed + 1);
        }

        private void SpawnWaveComets(MLordContext context, int fireTick, int seed) {
            for (int i = 0; i < nodeCount; i++) {
                if (Timer != fireTick + i * CometStagger) {
                    continue;
                }
                Vector2 anchor = new(context.Owner.ai[MLordAiSlots.OvAnchorX], context.Owner.ai[MLordAiSlots.OvAnchorY]);
                Vector2 node = anchor + MLordConstellationProj.GetNodeOffset(seed, i, nodeCount);
                Vector2 vel = MLordConstellationProj.GetLaneVelocity(seed, i);
                float groundY = MLordScreenFX.FindGroundBelow(context.Target.Center).Y + 40f;
                //隔位留星火，避免满地封死
                float leaveFire = i % 2 == 0 ? 1f : 0f;
                Projectile.NewProjectile(context.Npc.GetSource_FromAI(), node, vel,
                    ModContent.ProjectileType<MLordCometProj>(),
                    ScaleDamage(context, MLordDirector.CometDamage), 0f, Main.myPlayer,
                    MLordConstellationProj.GetLaneCurve(seed, i), leaveFire, groundY);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 6 },
                        node);
                }
            }
        }
    }
}
