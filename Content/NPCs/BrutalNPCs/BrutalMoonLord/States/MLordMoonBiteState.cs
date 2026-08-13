using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 月蚀噬咬：头部（含破损残口）张口甩出月咬之舌锁疗，被咬期间派出星髓凝滴回航吸血，
    /// 凝滴可拦截。四掌踞于绕玩家缓旋方阵四角收放合围（缺口随阵旋转移动），
    /// 头部全程睁眼（高风险高回报窗口）
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.MoonBite, typeof(MLordContext))]
    internal class MLordMoonBiteState : MLordStateBase
    {
        public override string StateName => "MoonBite";
        public override MLordStateIndex StateIndex => MLordStateIndex.MoonBite;

        internal const int MouthOpenEnd = 40;
        internal const int BiteEnd = 380;

        private int stateLength;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            stateLength = Frames(context, BiteEnd + 50);

            //头部实体彻底不在场（极端边界）则跳招
            if (!VaultUtils.isClient && context.Parts.Head < 0) {
                Timer = stateLength;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie94 with { Volume = 1f, Pitch = -0.45f }, context.Npc.Center);
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            //核心贴近压迫（舌区施压）
            HoverTo(npc, target.Center + MLordDirector.CoreHoverOffset + new Vector2(0f, -40f), 6f, 0.05f);
            UpdateLean(context);

            if (!VaultUtils.isClient) {
                RunServer(context);
            }

            Timer++;
            if (Timer >= stateLength) {
                return NextAttack(context);
            }
            return null;
        }

        private void RunServer(MLordContext context) {
            if (context.Parts.Head < 0) {
                return;
            }
            NPC head = Main.npc[context.Parts.Head];

            //甩舌：对 3000 内所有玩家各出一条月咬之舌（原版弹幕，锁疗身份保留）
            if (Timer == MouthOpenEnd) {
                Vector2 mouth = head.Center + new Vector2(0f, 216f);
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player player = Main.player[i];
                    if (!player.active || player.dead || player.Distance(mouth) > 3000f) {
                        continue;
                    }
                    Vector2 aim = (player.Center - mouth).SafeNormalize(Vector2.UnitY);
                    Projectile.NewProjectile(head.GetSource_FromAI(), mouth, aim,
                        ProjectileID.MoonLeech, 0, 0f, Main.myPlayer, head.whoAmI + 1, i);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Zombie101 with { Volume = 1f, Pitch = -0.3f }, head.Center);
                }
            }

            //凝滴回航波：被咬玩家处生成星髓凝滴（可拦截的治疗载体）
            int waveInterval = Frames(context, 80);
            if (Timer > MouthOpenEnd && Timer < BiteEnd && (Timer - MouthOpenEnd) % waveInterval == 0) {
                SpawnLeechBlobs(head);
            }

            //慢压期间头部偶发直射弹补压
            if (Timer > MouthOpenEnd && Timer % Frames(context, 64) == 30) {
                Vector2 aim = (context.Target.Center - head.Center).SafeNormalize(Vector2.UnitY);
                Projectile.NewProjectile(head.GetSource_FromAI(), head.Center + aim * 44f, aim * 6.8f,
                    ProjectileID.PhantasmalBolt, ScaleDamage(context, MLordDirector.BoltDamage), 0f, Main.myPlayer);
            }
        }

        /// <summary>为每个被月咬命中的玩家生成一枚凝滴，沿舌线回航（原版装配约定）</summary>
        private static void SpawnLeechBlobs(NPC head) {
            for (int projIndex = 0; projIndex < Main.maxProjectiles; projIndex++) {
                Projectile tongue = Main.projectile[projIndex];
                if (!tongue.active || tongue.type != ProjectileID.MoonLeech
                    || (int)tongue.ai[0] != head.whoAmI + 1) {
                    continue;
                }
                int playerIndex = (int)tongue.ai[1];
                if (playerIndex < 0 || playerIndex >= Main.maxPlayers) {
                    continue;
                }
                Player player = Main.player[playerIndex];
                if (!player.active || player.dead || player.FindBuffIndex(BuffID.MoonLeech) == -1) {
                    continue;
                }
                int blob = NPC.NewNPC(head.GetSource_FromAI(), (int)player.Center.X, (int)player.Center.Y,
                    NPCID.MoonLordLeechBlob);
                if (blob < Main.maxNPCs) {
                    Main.npc[blob].ai[0] = head.whoAmI + 1;
                    Main.npc[blob].ai[1] = projIndex;
                    Main.npc[blob].netUpdate = true;
                }
            }
        }
    }
}
