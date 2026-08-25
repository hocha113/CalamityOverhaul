using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 入场演出：静默祷姿 → 法阵按弧序描绘 → 定形迸发,法阵外环留场成为限制圈 → 首颗星球(星旋)穿门降临<br/>
    /// 全程无伤输出；前 92 帧免伤防抢戏
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Intro, typeof(CultistStateContext))]
    internal class CultistIntroState : CultistStateBase
    {
        public override string StateName => "CultistIntro";
        public override CultistStateIndex StateIndex => CultistStateIndex.Intro;

        private const int Duration = 190;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            NPC npc = context.Npc;
            npc.alpha = 255;
            npc.dontTakeDamage = true;
            npc.velocity = Vector2.Zero;
            context.SigilReveal = 0f;
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            npc.velocity *= 0.9f;
            SetPose(npc, 13);
            FaceTarget(npc, context.Target.Center);

            //法阵描绘：20~90 帧弧序展开
            context.SigilReveal = MathHelper.Clamp((Timer - 20) / 70f, 0f, 1f);
            //真身显形：30~80 帧渐显
            npc.alpha = (int)MathHelper.Clamp(255f - (Timer - 30) * 5.2f, 0f, 255f);

            CultistScreenFX.SetVeil(0.3f, npc.Center, CultistMotion.PhaseCore(0), 560f);

            //描绘起音
            if (Timer == 20 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.7f, Pitch = -0.35f }, npc.Center);
            }

            //定形迸发：印记落定,他画下的法阵成为这场仪式的边界
            if (Timer == 92) {
                context.SigilCommit = 1f;
                context.PushAura(1f, CultistMotion.PhaseCore(0));
                CultistMotion.SigilCommitFX(npc.Center, CultistMotion.PhaseCore(0), 1.4f);
                CultistMotion.RuneBurst(npc.Center, CultistMotion.RuneGold, 14, 6f);
                CultistMotion.Shake(npc.Center, 5f, 12);
                CultistScreenFX.PushFlash(0.35f);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 0.9f, Pitch = 0.1f }, npc.Center);
                }
            }

            //限制圈+首颗星球(权威端):场心取本体与目标中点,定桩后全场不动
            if (Timer == 92 && !VaultUtils.isClient && !context.ArenaSpawned) {
                context.ArenaCenter = Vector2.Lerp(npc.Center, context.Target.Center, 0.5f);
                context.ArenaSpawned = true;
                Projectile.NewProjectile(npc.GetSource_FromAI(), context.ArenaCenter, Vector2.Zero,
                    ModContent.ProjectileType<CultistArenaProj>(), 0, 0f, Main.myPlayer, npc.whoAmI);
                //星旋裹挟风暴穿门而来
                Projectile.NewProjectile(npc.GetSource_FromAI(), context.ArenaCenter, Vector2.Zero,
                    ModContent.ProjectileType<CultistPlanetProj>(), 60, 0f, Main.myPlayer,
                    CultistPlanetProj.KindVortex, npc.whoAmI, 0f);
                npc.netUpdate = true;
            }

            if (Timer >= 92) {
                npc.dontTakeDamage = false;
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (Timer >= Duration) {
                return new CultistWeaveState();
            }
            return null;
        }

        public override void OnExit(CultistStateContext context) {
            context.Npc.alpha = 0;
            context.Npc.dontTakeDamage = false;
            context.SigilReveal = 1f;
        }
    }
}
