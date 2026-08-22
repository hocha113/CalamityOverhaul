using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 仪式咏唱：驻停快充（法阵充能表全程可见），六法球环轨护体<br/>
    /// 拆台窗：240 帧内打掉其 6% 血量即打断，踉跄+充能大扣；不打则充能大涨<br/>
    /// 公平阀：环轨半径/角速恒定、球间 60° 空档；本体咏唱期不出手
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Chant, typeof(CultistStateContext))]
    internal class CultistChantState : CultistStateBase
    {
        public override string StateName => "CultistChant";
        public override CultistStateIndex StateIndex => CultistStateIndex.Chant;

        private const int Duration = 240;
        private int lifeAtEnter;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            lifeAtEnter = context.Npc.life;
            context.Npc.velocity = Vector2.Zero;
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            SetPose(npc, 13);
            npc.velocity *= 0.9f;

            //咏唱声与视觉语调
            context.ChantGlow = 1f;
            context.PushAura(1f, CultistMotion.ElementCore(context.Element));
            CultistScreenFX.SetVeil(0.55f, npc.Center, CultistMotion.ElementCore(context.Element), 620f);

            if (Timer == 6 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1f, Pitch = -0.3f }, npc.Center);
            }

            //环轨护体（权威端出球，各端确定性环轨）
            if (Timer == 18 && !VaultUtils.isClient) {
                for (int i = 0; i < 6; i++) {
                    float angle0 = MathHelper.TwoPi * i / 6f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + angle0.ToRotationVector2() * 60f,
                        Vector2.Zero, ModContent.ProjectileType<CultistTrueBolt>(), 40, 0f, Main.myPlayer,
                        context.Element, 1f, angle0);
                }
            }

            //咏唱符文涌动
            if (Timer % 7 == 0) {
                CultistMotion.RuneBurst(npc.Center + Main.rand.NextVector2Circular(30f, 40f),
                    CultistMotion.ElementCore(context.Element), 1, 2.2f);
            }

            if (VaultUtils.isClient) {
                return null;
            }

            //打断判定：咏唱期吃满阈值伤害 → 仪式破碎
            if (lifeAtEnter - npc.life >= npc.lifeMax * CultistStateContext.ChantBreakRatio) {
                KillOrbitBolts();
                context.AddRitual(-80f);
                context.StaggerDuration = 90;
                context.ChantCooldown = 900;
                CultistScreenFX.PushFlash(0.4f);
                return new CultistStaggerState();
            }

            if (Timer >= Duration) {
                //咏唱走满：环轨收势
                KillOrbitBolts();
                context.ChantCooldown = 900;
                CultistMotion.CastFlash(npc.Center, CultistMotion.ElementCore(context.Element), 1.3f);
                return new CultistWeaveState();
            }
            return null;
        }

        /// <summary>清掉本体的环轨法球（权威端）</summary>
        private static void KillOrbitBolts() {
            int orbitType = ModContent.ProjectileType<CultistTrueBolt>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == orbitType && proj.ai[1] == 1f) {
                    proj.Kill();
                }
            }
        }
    }
}
