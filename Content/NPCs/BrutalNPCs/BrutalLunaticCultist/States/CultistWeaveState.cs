using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 悬织连接段：侧翼悬浮呼吸拍 + 慢速真言弹点射，计满抽下一招<br/>
    /// 充能满格优先转仪式迸发；距离过远先走帷幕挪移
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Weave, typeof(CultistStateContext))]
    internal class CultistWeaveState : CultistStateBase
    {
        public override string StateName => "CultistWeave";
        public override CultistStateIndex StateIndex => CultistStateIndex.Weave;

        private readonly int restFrames;
        private int shootTimer;

        public CultistWeaveState() : this(0) {
        }

        /// <param name="extraRest">额外呼吸帧（大招后的长喘息）</param>
        public CultistWeaveState(int extraRest) {
            restFrames = extraRest;
        }

        /// <summary>基础时长随阶段收紧</summary>
        private static int BaseDuration(CultistStateContext context) {
            int frames = context.Phase switch { 2 => 46, 1 => 58, _ => 72 };
            if (context.IsDeathMode) {
                frames = (int)(frames * 0.85f);
            }
            return frames;
        }

        private int ShootRate(CultistStateContext context) => context.Phase >= 2 ? 38 : 46;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            shootTimer = 0;
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;
            shootTimer++;

            SetPose(npc, 0);
            FaceTarget(npc, player.Center);

            //侧上方弹簧悬停+呼吸浮动
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 hoverTarget = player.Center + new Vector2(side * 370f, -190f)
                + CultistMotion.BreathingOffset(seed: 1.3f);
            CultistMotion.SpringHover(npc, hoverTarget);

            //慢速真言弹点射：背景压力，不锁走位
            if (shootTimer >= ShootRate(context) && Timer > 24) {
                shootTimer = 0;
                bool canShoot = Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1)
                    && npc.Distance(player.Center) > 240f;
                if (canShoot) {
                    Vector2 predicted = CultistMotion.PredictTarget(player, npc.Center, 8.5f, 0.5f);
                    Vector2 dir = (predicted - npc.Center).SafeNormalize(Vector2.UnitY);
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + dir * 30f, dir * 8.5f,
                            ModContent.ProjectileType<CultistTrueBolt>(), 34, 0f, Main.myPlayer, context.Element);
                    }
                    context.PushAura(0.6f, CultistMotion.ElementCore(context.Element));
                    CultistMotion.CastFlash(npc.Center + dir * 30f, CultistMotion.ElementCore(context.Element), 0.7f);
                    npc.velocity -= dir * 2.4f;
                }
            }

            //决策仅权威端
            if (VaultUtils.isClient) {
                return null;
            }

            //充能满格：仪式迸发压过一切
            if (context.RitualFull && Timer > 30) {
                return new CultistRiteBurstState();
            }

            //距离过远：先挪移贴近
            if (Timer > 24 && npc.Distance(player.Center) > 980f) {
                return new CultistVeilStepState();
            }

            if (Timer >= BaseDuration(context) + restFrames) {
                return CreateAttackState(context.NextAttack());
            }
            return null;
        }

        /// <summary>按索引实例化攻击状态</summary>
        internal static ICultistState CreateAttackState(CultistStateIndex index) {
            return index switch {
                CultistStateIndex.FlameHunt => new CultistFlameHuntState(),
                CultistStateIndex.FrostLattice => new CultistFrostLatticeState(),
                CultistStateIndex.StormCadence => new CultistStormCadenceState(),
                CultistStateIndex.AncientRite => new CultistAncientRiteState(),
                CultistStateIndex.MirrorRite => new CultistMirrorRiteState(),
                CultistStateIndex.Chant => new CultistChantState(),
                _ => new CultistFlameHuntState(),
            };
        }
    }
}
