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
                //冻结期清掉自家死光（含真眼链束），宿主僵直/退避时束不悬空乱扫
                foreach (Projectile p in Main.ActiveProjectiles) {
                    if (p.type == ModContent.ProjectileType<MLordScanRayProj>()
                        || p.type == ModContent.ProjectileType<MLordArcRayProj>()
                        || p.type == ModContent.ProjectileType<MLordEyeLinkProj>()) {
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
            //演出强度按"第几个部件被破"递进（1~5），终局感逐层加码
            float escalate = 1f + MathHelper.Clamp(context.Parts.BrokenCount - 1, 0, 4) * 0.18f;

            if (Timer == FlashTick && !VaultUtils.isServer) {
                MoonlordDeathDrama.RequestLight(0.55f * escalate, brokenPos);
                MLordScreenEffects.PushStarRing(brokenPos, escalate, 760f * escalate, 32);
                MLordScreenFX.StarBurst(brokenPos, 1.5f * escalate, (int)(24 * escalate));
                MLordScreenFX.Punch(brokenPos, 8f * escalate, 16);
            }
            if (Timer > FlashTick && Timer < FlashTick + 20 && !VaultUtils.isServer) {
                MoonlordDeathDrama.RequestLight(0.55f * escalate * (1f - (Timer - FlashTick) / 20f), brokenPos);
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
                    //换写下一个排队破坏的归因，各事件各归其位
                    if (context.PendingBreakCodes.Count > 0) {
                        context.Owner.ai[MLordAiSlots.OvLastBrokenPart] = context.PendingBreakCodes[0];
                        context.PendingBreakCodes.RemoveAt(0);
                        context.Npc.netUpdate = true;
                    }
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

        /// <summary>最近被破坏部件的位置（Override 槽同步：1上左/2上右/3头/4下左/5下右），失效退核心位</summary>
        private static Vector2 GetBrokenPartPos(MLordContext context) {
            int partId = (int)context.Owner.ai[MLordAiSlots.OvLastBrokenPart];
            MLordPartsStatus parts = context.Parts;
            int index = partId switch {
                1 => parts.HandIndex(0),
                2 => parts.HandIndex(1),
                3 => parts.Head,
                4 => parts.HandIndex(2),
                5 => parts.HandIndex(3),
                _ => -1,
            };
            if (index >= 0 && index < Main.maxNPCs && Main.npc[index].active) {
                return Main.npc[index].Center;
            }
            return context.Npc.Center;
        }
    }
}
