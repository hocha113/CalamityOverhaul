using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States
{
    /// <summary>
    /// 忍者影袭：本体蹲伏静止(受击窗口)，体内忍者亮起→三次影步(左夹/右夹/天袭+手里剑扇)。
    /// P2解锁
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.NinjaFlurry, typeof(KingSlimeStateContext))]
    internal class KingSlimeNinjaFlurryState : KingSlimeStateBase
    {
        public override string StateName => "NinjaFlurry";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.NinjaFlurry;

        private const int WakeTime = 24;
        private const int StrikeInterval = 50;
        private const int TotalTime = WakeTime + StrikeInterval * 3 + 26;

        private int strikesFired;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            strikesFired = 0;
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f, Volume = 0.6f, MaxInstances = 3 }, context.Npc.Center);
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            //蹲伏静止：静止的靶子是给玩家的进攻窗口
            npc.velocity.X *= 0.72f;
            context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 0.78f, 0.2f);

            //忍者剪影亮起(可读前摇)，出招间隙微降
            float wake = MathHelper.Clamp(Timer / (float)WakeTime, 0f, 1f);
            int sinceStrike = (int)Timer - (WakeTime + (strikesFired - 1) * StrikeInterval);
            float dip = strikesFired > 0 && sinceStrike < 18 ? 0.55f : 1f;
            context.NinjaGlow = wake * dip;
            context.AuraMode = 1;
            context.AuraProgress = wake * 0.4f;

            //三次影步
            if (strikesFired < 3 && Timer >= WakeTime + strikesFired * StrikeInterval) {
                FireNinja(context, strikesFired);
                strikesFired++;
                //出手瞬间身体一颤
                context.ImpactSquash(0.14f);
            }

            if (Timer >= TotalTime && !VaultUtils.isClient) {
                return BackToHop(context);
            }

            return null;
        }

        /// <summary>放出影身：0左夹 1右夹 2天袭</summary>
        private void FireNinja(KingSlimeStateContext context, int style) {
            NPC npc = context.Npc;
            Player player = context.Target;

            SoundEngine.PlaySound(SoundID.Item7 with { Pitch = 0.1f + style * 0.15f, Volume = 0.8f, MaxInstances = 3 }, npc.Center);
            if (!VaultUtils.isServer) {
                KingSlimeGelFX.GelSplatter(npc.Center, -Vector2.UnitY, 5, 4f, 0.8f);
            }

            if (VaultUtils.isClient || !player.Alives()) {
                return;
            }

            //定格点：两侧夹击或头顶落斩
            Vector2 strikePoint = style switch {
                0 => player.Center + new Vector2(-250f, -26f),
                1 => player.Center + new Vector2(250f, -26f),
                _ => player.Center + player.velocity * 12f + new Vector2(0f, -290f),
            };
            //8帧冲刺到位
            Vector2 vel = (strikePoint - npc.Center) / 8f;
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                ModContent.ProjectileType<BKSNinjaProj>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, style);
        }
    }
}
