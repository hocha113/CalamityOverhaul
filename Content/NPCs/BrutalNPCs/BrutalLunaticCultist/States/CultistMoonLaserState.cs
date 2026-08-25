using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 月明激光：他跪祷不出手,月亮睁眼,自盘心放辐条死光旋扫圆环<br/>
    /// 时间轴:40 帧睁瞳(PupilOpen 推满)→辐条预告 50 帧(锁死起始角)→恒速旋扫,玩家沿环跑在它前面<br/>
    /// 修罗/死亡模式:双辐条对向,必须选缺口并提前承诺<br/>
    /// 公平阀:恒定角速度=早决断永远跑得掉;预告即承诺;本体全程不出手且受伤加深(专注的代价)
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.MoonLaser, typeof(CultistStateContext))]
    internal class CultistMoonLaserState : CultistStateBase
    {
        public override string StateName => "CultistMoonLaser";
        public override CultistStateIndex StateIndex => CultistStateIndex.MoonLaser;

        private const int EyeOpenFrames = 40;
        private const int SweepFrames = 250;
        private const int Duration = EyeOpenFrames + 50 + SweepFrames + 60;

        public override void OnEnter(CultistStateContext context) {
            base.OnEnter(context);
            context.Npc.velocity = Vector2.Zero;
        }

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            //跪祷:他不打了,天在打
            SetPose(npc, 13);
            npc.velocity *= 0.88f;
            context.PushAura(0.8f, CultistMotion.MoonCore);
            //睁瞳:各端本地推,月球绘制读 PupilOpen
            context.PupilOpen = MathHelper.Clamp(MathHelper.Max(context.PupilOpen, Timer / (float)EyeOpenFrames), 0f, 1f);
            CultistScreenFX.SetVeil(0.5f, context.ArenaCenter, CultistMotion.MoonCore, 900f);

            if (Timer == 6 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie105 with { Volume = 1f, Pitch = -0.8f }, npc.Center);
            }
            if (Timer == EyeOpenFrames) {
                CultistScreenFX.PushFlash(0.35f);
                CultistMotion.Shake(context.ArenaCenter, 5f, 12);
            }

            //放辐条(权威端):自月心(场心)起,起始角取玩家当前方位再偏半圈,给读线时间
            if (Timer == EyeOpenFrames && !VaultUtils.isClient) {
                Player player = context.Target;
                float startAngle = (player.Center - context.ArenaCenter).ToRotation() + MathHelper.Pi * 0.7f;
                float dir = Main.rand.NextBool() ? 1f : -1f;
                Projectile.NewProjectile(npc.GetSource_FromAI(), context.ArenaCenter, Vector2.Zero,
                    ModContent.ProjectileType<CultistMoonBeam>(), 55, 0f, Main.myPlayer,
                    startAngle, dir, SweepFrames);
                if (context.IsDeathMode) {
                    //对向第二根:同速同向,缺口恒在两根之间
                    Projectile.NewProjectile(npc.GetSource_FromAI(), context.ArenaCenter, Vector2.Zero,
                        ModContent.ProjectileType<CultistMoonBeam>(), 55, 0f, Main.myPlayer,
                        startAngle + MathHelper.Pi, dir, SweepFrames);
                }
            }

            if (VaultUtils.isClient) {
                return null;
            }
            if (Timer >= Duration) {
                return new CultistWeaveState(40);
            }
            return null;
        }
    }
}
