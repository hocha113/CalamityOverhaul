using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using Terraria;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 盘转连接段:侧翼悬浮呼吸拍,浑天仪空转,选招洗牌;<br/>
    /// 本体收手=星球开火闸放行(轮流出手),充能满格优先转合相祭仪
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.Coil, typeof(CultistStateContext))]
    internal class CultistCoilState : CultistStateBase
    {
        public override string StateName => "CultistCoil";
        public override CultistStateIndex StateIndex => CultistStateIndex.Coil;

        private readonly int restFrames;

        public CultistCoilState() : this(0) {
        }

        /// <param name="extraRest">额外呼吸帧(大招后的长喘息)</param>
        public CultistCoilState(int extraRest) {
            restFrames = extraRest;
        }

        /// <summary>基础时长随阶段收紧(2026-08-28 二次提速:技能间隔全段较上版再收短 30%)</summary>
        private static int BaseDuration(CultistStateContext context) {
            int frames = context.Phase switch { >= 4 => 13, 3 => 15, 2 => 17, 1 => 20, _ => 22 };
            if (context.IsAsuraMode) {
                frames = (int)(frames * 0.85f);
            }
            return frames;
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 0);
            FaceTarget(npc, player.Center);

            //侧上方弹簧悬停+呼吸浮动;距离过远时加劲追上(不瞬移,硬赶)
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 hoverTarget = player.Center + new Vector2(side * 370f, -190f)
                + CultistMotion.BreathingOffset(seed: 1.3f);
            float dist = npc.Distance(player.Center);
            float stiffness = dist > 980f ? 0.030f : 0.012f;
            float maxSpeed = dist > 980f ? 30f : 22f;
            CultistMotion.SpringHover(npc, hoverTarget, stiffness, 0.09f, maxSpeed);

            //决策仅权威端
            if (VaultUtils.isClient) {
                return null;
            }

            //充能满格:合相祭仪压过一切(最短停留闸随间隔提速同步收短)
            if (context.AlignFull && Timer > 11) {
                return new CultistConjunctionState();
            }

            if (Timer >= BaseDuration(context) + restFrames) {
                CultistStateIndex next = context.NextAttack();
                //掷环收势解耦后旧环可能仍在场:此时再抽到掷环就换下一张,防同环序重复离体(P0 主场双倍权重下会连抽)
                for (int reroll = 0; reroll < 2 && next == CultistStateIndex.RingHurl
                    && CultistRingHurlState.AnyRingAlive(npc.whoAmI); reroll++) {
                    next = context.NextAttack();
                }
                return CreateAttackState(next);
            }
            return null;
        }

        /// <summary>按索引实例化攻击状态</summary>
        internal static ICultistState CreateAttackState(CultistStateIndex index) {
            return index switch {
                CultistStateIndex.OrbitLance => new CultistOrbitLanceState(),
                CultistStateIndex.RingHurl => new CultistRingHurlState(),
                CultistStateIndex.StarChart => new CultistStarChartState(),
                CultistStateIndex.Eclipse => new CultistEclipseState(),
                CultistStateIndex.Gaze => new CultistGazeState(),
                CultistStateIndex.PlanetHurl => new CultistPlanetHurlState(),
                CultistStateIndex.Comet => new CultistCometVolleyState(),
                CultistStateIndex.ZodiacSeal => new CultistZodiacSealState(),
                CultistStateIndex.StasisMines => new CultistStasisMinesState(),
                CultistStateIndex.ArcaneNova => new CultistArcaneNovaState(),
                CultistStateIndex.Starfall => new CultistStarfallState(),
                CultistStateIndex.SeekerStars => new CultistSeekerStarsState(),
                CultistStateIndex.RingPrison => new CultistRingPrisonState(),
                _ => new CultistOrbitLanceState(),
            };
        }
    }
}
