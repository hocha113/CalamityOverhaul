using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 金环封阵(P1 起):司祭以浑天仪为模铸一道金环,掷向玩家预判位翻转平铺钉界,环缘点燃成囚阵<br/>
    /// 缺口门初向场心并缓慢进动,困在阵里就沿门走出去<br/>
    /// 公平阀:钉界位出手拍锁死;缺口/进动常量声明于 CultistGoldRingSeal,判定与绘制同参
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.RingPrison, typeof(CultistStateContext))]
    internal class CultistRingPrisonState : CultistStateBase
    {
        public override string StateName => "CultistRingPrison";
        public override CultistStateIndex StateIndex => CultistStateIndex.RingPrison;

        /// <summary>铸环拍:金符聚拢的短蓄势</summary>
        private const int CastBeat = 20;
        private const int Timeout = 220;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, Timer < CastBeat ? 12 : 11);
            FaceTarget(npc, player.Center);
            context.PushAura(0.8f, CultistMotion.RuneGold);
            context.OrreryGlow = 1f;

            //侧位铸环:环飞出去的路径玩家看得全
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 hover = player.Center + new Vector2(side * 480f, -220f)
                + CultistMotion.BreathingOffset(seed: 23.9f, 9f);
            CultistMotion.SpringHover(npc, hover, 0.013f, 0.09f, 17f);

            //铸环蓄势:金符向手心聚,链音爬调
            if (Timer < CastBeat) {
                if (Timer % 5 == 0) {
                    CultistMotion.RuneBurst(npc.Center + Main.rand.NextVector2Unit() * 100f,
                        CultistMotion.RuneGold, 1, -5f);
                }
                if ((Timer == 5 || Timer == 13) && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item101 with {
                        Volume = 0.6f,
                        Pitch = -0.2f + Timer / (float)CastBeat * 0.6f
                    }, npc.Center);
                }
            }

            //掷环拍:锁玩家预判位(按环 24 帧飞抵折算提前量)
            if (Timer == CastBeat) {
                CultistMotion.CastFlash(npc.Center, CultistMotion.RuneGold, 1.3f);
                CultistMotion.Shake(npc.Center, 4f, 9);
                context.ScalePulse = 1.12f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.95f, Pitch = -0.2f }, npc.Center);
                }
                if (!VaultUtils.isClient) {
                    Vector2 aim = player.Center + player.velocity * 24f * 0.55f;
                    //钉界位不出黄道环:留出环身半径的余量
                    if (context.ArenaSpawned) {
                        Vector2 fromArena = aim - context.ArenaCenter;
                        float maxDist = CultistStateContext.ArenaRadius - CultistGoldRingSeal.SealRadius - 60f;
                        if (fromArena.Length() > maxDist) {
                            aim = context.ArenaCenter + fromArena.SafeNormalize(Vector2.UnitY) * maxDist;
                        }
                    }
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                        ModContent.ProjectileType<CultistGoldRingSeal>(), 44, 0f, Main.myPlayer,
                        npc.whoAmI, aim.X, aim.Y);
                    //掷环反冲:身体语言
                    npc.velocity -= (aim - npc.Center).SafeNormalize(Vector2.UnitY) * 4.5f;
                    npc.netUpdate = true;
                }
            }

            if (VaultUtils.isClient) {
                return null;
            }

            //双出口:环钉好点燃后即收(封阵自燃自碎,司祭转下一手),或超时兜底
            if (Timer >= CastBeat + 58) {
                return new CultistCoilState();
            }
            if (Timer >= Timeout) {
                return new CultistCoilState();
            }
            return null;
        }
    }
}
