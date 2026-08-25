using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 落星·坠星雨：所有星走同一个声明角度彼此平行,最优解=垂直于雨向移动(走位轴从二维压成一维)<br/>
    /// 星尘主场强化：波次+密度上调,近乎连续的星雨<br/>
    /// 公平阀：RainTiltMax 限定倾角接近竖直;每颗星 22 帧高空无判定;波内等距散布不追人
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.StarRite, typeof(CultistStateContext))]
    internal class CultistStarRiteState : CultistStateBase
    {
        public override string StateName => "CultistStarRite";
        public override CultistStateIndex StateIndex => CultistStateIndex.StarRite;

        /// <summary>雨向最大倾角(相对竖直,弧度),声明的公平阀</summary>
        private const float RainTiltMax = 0.42f;

        /// <summary>本轮雨向(权威端定,写 ai 由星自带)</summary>
        private float rainAngle;

        private static bool IsHome(CultistStateContext context) => context.Phase == 2 || context.Phase >= 4;

        private int WaveCount(CultistStateContext context) => IsHome(context) ? 4 : 2;
        private int WaveInterval(CultistStateContext context) => IsHome(context) ? 52 : 68;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            //声明角:接近竖直的随机倾角,全场共用
            rainAngle = MathHelper.PiOver2 + Main.rand.NextFloat(-RainTiltMax, RainTiltMax);
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 13);
            FaceTarget(npc, player.Center);

            //高位仰祷驻停:星是天给的,不是他丢的
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 hover = player.Center + new Vector2(side * 430f, -330f)
                + CultistMotion.BreathingOffset(seed: 2.7f, 10f);
            CultistMotion.SpringHover(npc, hover, 0.01f, 0.085f, 15f);

            context.PushAura(0.6f, CultistMotion.PhaseCore(context.Phase));

            //起雨手势
            if (Timer == 14 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.7f, Pitch = 0.15f }, npc.Center);
            }

            //波次落星(权威端):横向等距+抖动,沿声明角下坠
            int interval = WaveInterval(context);
            int waves = WaveCount(context);
            if (!VaultUtils.isClient && Timer >= 26 && (Timer - 26) % interval == 0) {
                int wave = (int)(Timer - 26) / interval;
                if (wave < waves) {
                    int stars = IsHome(context) ? 7 : 5;
                    float span = 1150f;
                    //沿雨向反推出生线:玩家上方 700px 的横带
                    Vector2 upDir = -rainAngle.ToRotationVector2();
                    for (int i = 0; i < stars; i++) {
                        float lane = (i / (stars - 1f) - 0.5f) * span + Main.rand.NextFloat(-46f, 46f);
                        Vector2 spawn = player.Center + upDir * 720f + new Vector2(lane, 0f);
                        Projectile.NewProjectile(npc.GetSource_FromAI(), spawn, Vector2.Zero,
                            ModContent.ProjectileType<CultistStarFall>(), 40, 0f, Main.myPlayer,
                            rainAngle, i * 4f);
                    }
                    CultistMotion.RuneBurst(npc.Center + new Vector2(0f, -30f), CultistMotion.StardustCore, 4, 4f);
                    context.ScalePulse = 1.05f;
                }
            }

            if (VaultUtils.isClient) {
                return null;
            }
            int total = 26 + waves * interval + 70;
            if (Timer >= total) {
                return new CultistWeaveState();
            }
            return null;
        }
    }
}
