using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenSlime.States
{
    /// <summary>凝胶陨雨：跃上高空/悬停，三波扇形凝胶陨石交错编排</summary>
    [InnoVault.StateMachines.VaultState((int)QueenSlimeStateIndex.GelMeteorRain, typeof(QueenSlimeStateContext))]
    internal class QueenGelMeteorRainState : QueenSlimeStateBase
    {
        public override string StateName => "GelMeteorRain";
        public override QueenSlimeStateIndex StateIndex => QueenSlimeStateIndex.GelMeteorRain;

        private const int AscendTime = 52;
        private const int WaveInterval = 55;

        private int WaveCount(QueenSlimeStateContext ctx) => ctx.IsPhase2 ? 4 : 3;
        private int MeteorsPerWave(QueenSlimeStateContext ctx) => ctx.IsDeathMode ? 9 : 7;

        public QueenGelMeteorRainState() {
        }

        public override void OnEnter(QueenSlimeStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            DisableContactDamage(npc);
            npc.noGravity = true;
            npc.noTileCollide = true;
        }

        public override IQueenSlimeState OnUpdate(QueenSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            Timer++;
            DisableContactDamage(npc);
            FaceTarget(npc, player.Center);

            //升空段
            if (Timer <= AscendTime) {
                float p = Timer / (float)AscendTime;
                Vector2 anchor = player.Center + new Vector2(0f, -430f);
                QueenMotion.SpringHover(npc, anchor, 0.02f, 0.1f, 24f);
                context.PoseCommand = context.Phase2Unfolded ? 5 : 1;
                context.WingFlapBoost = 1f;
                context.SetChargeState(2, p);
                if (Timer == 8) {
                    SoundEngine.PlaySound(SoundID.Item155 with { Volume = 0.7f, Pitch = -0.15f }, npc.Center);
                }
                return null;
            }

            //悬停+洒陨段
            int rainTimer = Timer - AscendTime;
            int wave = rainTimer / WaveInterval;

            //缓慢横移保持在玩家上方偏侧
            float drift = (float)System.Math.Sin(rainTimer * 0.03f) * 210f;
            Vector2 hoverAnchor = player.Center + new Vector2(drift, -410f);
            QueenMotion.SpringHover(npc, hoverAnchor, 0.012f, 0.09f, 15f);
            context.PoseCommand = context.Phase2Unfolded ? 5 : 0;

            if (wave >= WaveCount(context)) {
                context.ResetChargeState();
                if (rainTimer >= WaveCount(context) * WaveInterval + 30) {
                    if (!VaultUtils.isClient) {
                        return context.IsPhase2 ? new QueenAerialBalletState() : new QueenBallroomStepState(2);
                    }
                }
                return null;
            }

            int waveT = rainTimer % WaveInterval;

            //波前喷吐姿态+蓄力
            if (waveT < 18) {
                context.PoseCommand = 4;
                context.SetChargeState(2, waveT / 18f);
            }

            //洒陨帧(服务端)
            if (waveT == 18 && !VaultUtils.isClient) {
                FireWave(context, wave);
            }
            if (waveT == 18) {
                //喷吐后坐上浮
                npc.velocity.Y -= 3.4f;
                context.PushSquash(0.42f);
                SoundEngine.PlaySound(SoundID.Item155 with { Volume = 0.85f, Pitch = 0.25f + wave * 0.08f }, npc.Center);
                QueenMotion.GelSplashBurst(npc.Top, 1.1f, 7);
            }

            return null;
        }

        /// <summary>一波扇形陨石(服务端)：横向等距+波间交错半格</summary>
        private void FireWave(QueenSlimeStateContext context, int wave) {
            NPC npc = context.Npc;
            Player player = context.Target;
            int count = MeteorsPerWave(context);
            float spacing = 96f;
            float stagger = wave % 2 == 1 ? spacing * 0.5f : 0f;
            //以玩家预测位为中心横向布点
            float centerX = player.Center.X + player.velocity.X * 26f;

            for (int i = 0; i < count; i++) {
                float targetX = centerX + (i - (count - 1) * 0.5f) * spacing + stagger;
                Vector2 spawn = npc.Top + new Vector2((targetX - npc.Center.X) * 0.25f, -8f);
                //抛物初速：水平朝目标列，竖直上抛
                float vx = MathHelper.Clamp((targetX - spawn.X) * 0.016f, -8.5f, 8.5f);
                float vy = -Main.rand.NextFloat(3.5f, 5.5f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), spawn, new Vector2(vx, vy),
                    ModContent.ProjectileType<QueenGelMeteorProj>(), QueenGelMeteorProj.MeteorDamage, 0f, Main.myPlayer,
                    0f, 0f, (wave * count + i) * 0.13f);
            }
        }

        public override void OnExit(QueenSlimeStateContext context) {
            base.OnExit(context);
            NPC npc = context.Npc;
            if (!context.Phase2Unfolded) {
                npc.noGravity = false;
                npc.noTileCollide = false;
            }
        }
    }
}
