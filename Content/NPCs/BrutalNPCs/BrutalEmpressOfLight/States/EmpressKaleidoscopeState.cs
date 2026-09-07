using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.States
{
    /// <summary>
    /// 万华镜：她的鞭。每小节第一拍标一处鞭尖落点，下一小节第一拍整条鞭线抽下。
    /// 三小节一组按华尔兹强弱弱：强击落在你的预测位置，两记弱击甩在你两侧 300px。两组六击，尾拍收鞭
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)EmpressStateIndex.Kaleidoscope, typeof(EmpressStateContext))]
    internal class EmpressKaleidoscopeState : EmpressStateBase
    {
        public override string StateName => "EmpressKaleidoscope";
        public override EmpressStateIndex StateIndex => EmpressStateIndex.Kaleidoscope;

        private const int Cracks = 6;
        private const float SideOffset = 300f;

        private EmpressStateContext Context;
        private int cracksCast;
        private int tailTimer = -1;
        private int lastSide = 1;

        public override void OnEnter(EmpressStateContext context) {
            base.OnEnter(context);
            Context = context;
            cracksCast = 0;
            tailTimer = -1;
            PlayLocal(SoundID.Item164 with { Volume = 0.7f, Pitch = 0.1f }, context.Npc.Center);
        }

        public override IEmpressState OnUpdate(EmpressStateContext context) {
            Context = context;
            NPC npc = context.Npc;
            Player target = context.Target;
            Timer++;

            //手随鞭：面向落点那侧施法
            context.Pose = lastSide > 0 ? EmpressPose.CastRight : EmpressPose.CastLeft;
            context.PoseTimer = MathHelper.Clamp(context.BarFrame, 0f, 59f);
            //蓄势读拍：第一拍前臂上扬
            context.SetChargeState(lastSide > 0 ? 2 : 1, context.BarFrame / (float)EmpressTempo.BarFrames);

            //她在你斜上方 (∓420,-260) 随鞭子换边
            if (target.Alives()) {
                Vector2 dest = target.Center + new Vector2(-lastSide * 420f, -260f);
                npc.velocity = npc.velocity * 0.72f + (dest - npc.Center) * 0.04f;
            }
            else {
                npc.velocity *= 0.9f;
            }

            if (Timer > 4 && cracksCast < Cracks && context.Downbeat && target.Alives()) {
                CastCrack(context, npc, target, cracksCast);
                cracksCast++;
                if (cracksCast >= Cracks) {
                    tailTimer = 0;
                }
            }
            if (tailTimer >= 0) {
                tailTimer++;
            }

            EmpressMotion.AmbientGlow(npc, context.DayFormBlend);
            //最后一鞭抽下（一小节后）再收半小节
            if (tailTimer >= EmpressTempo.BarFrames + 30) {
                return new EmpressConnectorState();
            }
            return null;
        }

        private void CastCrack(EmpressStateContext context, NPC npc, Player target, int idx) {
            int beatInGroup = idx % 3;
            bool strong = beatInGroup == 0;
            Vector2 tip;
            if (strong) {
                //强：一小节后你会在哪（速度 × 60f，最远不超过 500）
                Vector2 lead = target.velocity * EmpressTempo.BarFrames;
                if (lead.Length() > 500f) {
                    lead = lead.SafeNormalize(Vector2.Zero) * 500f;
                }
                tip = target.Center + lead;
                lastSide = tip.X >= npc.Center.X ? 1 : -1;
            }
            else {
                //弱：甩在你两侧，先左后右（相对她的朝向）
                int side = beatInGroup == 1 ? -1 : 1;
                tip = target.Center + new Vector2(side * SideOffset, -20f);
                lastSide = side;
            }
            PlayLocal(SoundID.Item159 with { Volume = strong ? 0.8f : 0.55f, Pitch = strong ? -0.1f : 0.25f }, npc.Center);
            if (VaultUtils.isClient) {
                return;
            }
            int damage = strong ? context.WhipDamage + 8 : context.WhipDamage;
            Projectile.NewProjectile(npc.GetSource_FromAI(), tip, Vector2.Zero, ModContent.ProjectileType<EmpressWhipCrack>(),
                damage, 0f, Main.myPlayer, tip.X, tip.Y, EmpressWhipCrack.PackAi2(npc.whoAmI, EmpressTempo.BarFrames, strong));
        }
    }
}
