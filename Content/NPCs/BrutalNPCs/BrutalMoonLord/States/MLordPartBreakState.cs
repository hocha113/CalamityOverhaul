using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 部件破坏事件：全体僵直→白闪星环→真眼自碎裂眼窝脱出入列。
    /// 每破坏一个部件必经此态，事件感与公平清束一次到位
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.PartBreak, typeof(MLordContext))]
    internal class MLordPartBreakState : MLordStateBase
    {
        public override string StateName => "PartBreak";
        public override MLordStateIndex StateIndex => MLordStateIndex.PartBreak;

        internal const int FlashTick = 8;
        internal const int EyeBirthTick = 34;
        internal const int EventEnd = 92;

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                //冻结期清掉自家死光，宿主僵直束不悬空
                foreach (Projectile p in Main.ActiveProjectiles) {
                    if (p.type == ModContent.ProjectileType<MLordScanRayProj>()
                        || p.type == ModContent.ProjectileType<MLordArcRayProj>()) {
                        p.Kill();
                    }
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie93 with { Volume = 1.1f, Pitch = -0.7f }, context.Npc.Center);
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;

            //全体僵直
            context.HoldAllParts = true;
            npc.velocity *= 0.88f;

            Vector2 brokenPos = GetBrokenPartPos(context);

            if (Timer == FlashTick && !VaultUtils.isServer) {
                MoonlordDeathDrama.RequestLight(0.55f, brokenPos);
                MLordScreenEffects.PushStarRing(brokenPos, 1f, 760f, 32);
                MLordScreenFX.StarBurst(brokenPos, 1.5f, 24);
                MLordScreenFX.Punch(brokenPos, 8f, 16);
            }
            if (Timer > FlashTick && Timer < FlashTick + 20 && !VaultUtils.isServer) {
                MoonlordDeathDrama.RequestLight(0.55f * (1f - (Timer - FlashTick) / 20f), brokenPos);
            }

            //真眼已在部件破坏瞬间由原版 checkDead 特判脱出，此处补成形收束的仪式拍
            if (Timer == EyeBirthTick && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie102 with { Volume = 1f, Pitch = -0.2f }, brokenPos);
                MLordScreenFX.StarBurst(brokenPos, 1f, 14);
            }

            Timer++;
            if (Timer >= EventEnd) {
                //还有排队事件（同帧多部件被打破）继续演
                if (!VaultUtils.isClient && context.PendingBreakEvents > 0) {
                    context.PendingBreakEvents--;
                    return new MLordPartBreakState();
                }
                if (!VaultUtils.isClient) {
                    if (context.Parts.AllBroken && npc.ai[MLordAiSlots.CorePhase] != MLordPhase.CoreExposed) {
                        return new MLordCoreExposureState();
                    }
                    return NextAttack(context);
                }
            }
            return null;
        }

        /// <summary>最近被破坏部件的位置（Override 槽同步），失效退核心位</summary>
        private static Vector2 GetBrokenPartPos(MLordContext context) {
            int partId = (int)context.Owner.ai[MLordAiSlots.OvLastBrokenPart];
            MLordPartsStatus parts = context.Parts;
            int index = partId switch {
                1 => parts.LeftHand,
                2 => parts.RightHand,
                3 => parts.Head,
                _ => -1,
            };
            if (index >= 0 && index < Main.maxNPCs && Main.npc[index].active) {
                return Main.npc[index].Center;
            }
            return context.Npc.Center;
        }
    }
}
