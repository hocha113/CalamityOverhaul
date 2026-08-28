using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 星轨连珠:在场心架起倾斜轨道椭圆,星珠沿轨巡行;星尘主场追加错拍副轨<br/>
    /// 巡行末段全轨刹停,星珠向心收缩、预警线锁死外法向,停顿一拍后全珠极速环爆(预告即承诺)<br/>
    /// 公平阀在轨道实体内声明(GapSlots/近平面阈/外法向环爆不入环内域);本体全程只施法不出手
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.OrbitLance, typeof(CultistStateContext))]
    internal class CultistOrbitLanceState : CultistStateBase
    {
        public override string StateName => "CultistOrbitLance";
        public override CultistStateIndex StateIndex => CultistStateIndex.OrbitLance;

        private const int Timeout = 300;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            SetPose(npc, 12);
            FaceTarget(npc, context.Target.Center);
            context.PushAura(0.7f, CultistMotion.PhaseCore(context.Phase));
            context.OrreryGlow = MathHelper.Max(context.OrreryGlow, 0.6f);

            //撤到场心上方施法位
            Vector2 hover = context.ArenaCenter + new Vector2(0f, -420f)
                + CultistMotion.BreathingOffset(seed: 3.1f, 10f);
            CultistMotion.SpringHover(npc, hover, 0.014f, 0.09f, 18f);

            //起手拍
            if (Timer == 4 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.8f, Pitch = -0.4f }, npc.Center);
            }

            //架轨(权威端):主轨即刻,星尘主场 24 帧后错拍副轨(一轨歇一轨压)
            if (Timer == 8 && !VaultUtils.isClient) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), context.ArenaCenter, Vector2.Zero,
                    ModContent.ProjectileType<CultistOrbitPath>(), 40, 0f, Main.myPlayer,
                    npc.whoAmI, Main.rand.Next(100000));
            }
            if (Timer == 32 && context.Phase == 2 && !VaultUtils.isClient) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), context.ArenaCenter, Vector2.Zero,
                    ModContent.ProjectileType<CultistOrbitPath>(), 40, 0f, Main.myPlayer,
                    npc.whoAmI, Main.rand.Next(100000), 1f);
            }

            if (VaultUtils.isClient) {
                return null;
            }

            //双出口:轨道散场即收,或超时兜底
            if (Timer > 48 && !AnyPathAlive(npc.whoAmI)) {
                return new CultistCoilState();
            }
            if (Timer >= Timeout) {
                return new CultistCoilState();
            }
            return null;
        }

        private static bool AnyPathAlive(int ownerWho) {
            int type = ModContent.ProjectileType<CultistOrbitPath>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == ownerWho) {
                    return true;
                }
            }
            return false;
        }
    }
}
