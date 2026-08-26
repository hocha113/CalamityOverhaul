using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 十二宫封禁(P2 起):司祭仰祷,黄道环上数个宫位刻痕亮起充能,80 帧后各自点燃辐条封锁扇区<br/>
    /// 整组辐条以常量速率刚体进动,安全扇随组同转不塌<br/>
    /// 公平阀:选宫排除玩家所在宫位±PlayerSectorClearance(当拍保底 90° 安全扇);
    /// 预告与点燃同参同角(预告即承诺);辐条内端净空由弹幕声明
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.ZodiacSeal, typeof(CultistStateContext))]
    internal class CultistZodiacSealState : CultistStateBase
    {
        public override string StateName => "CultistZodiacSeal";
        public override CultistStateIndex StateIndex => CultistStateIndex.ZodiacSeal;

        private const int CastBeat = 14;
        private const int Timeout = 320;
        private const int SlotCount = 12;
        /// <summary>公平阀:玩家所在宫位±此值永不选中</summary>
        private const int PlayerSectorClearance = 1;
        /// <summary>整组进动速率(rad/f):玩家所在半径处步行即可跟上</summary>
        private const float DriftRate = 0.0016f;

        private static int SealCount(CultistStateContext context) =>
            context.Phase >= 4 || context.IsDeathMode ? 5 : 4;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 13);
            FaceTarget(npc, player.Center);
            context.PushAura(0.8f, CultistMotion.PhaseCore(context.Phase));
            context.OrreryGlow = MathHelper.Max(context.OrreryGlow, 0.6f);

            Vector2 hover = context.ArenaCenter + new Vector2(0f, -500f)
                + CultistMotion.BreathingOffset(seed: 8.6f, 9f);
            CultistMotion.SpringHover(npc, hover, 0.012f, 0.09f, 16f);

            //起祷音
            if (Timer == 4 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.85f, Pitch = -0.45f }, npc.Center);
            }

            //落宫(权威端):排除玩家扇区后抽宫位,整组同源进动
            if (Timer == CastBeat && !VaultUtils.isClient && context.ArenaSpawned) {
                float playerAngle = (player.Center - context.ArenaCenter).ToRotation();
                int playerSlot = (int)MathF.Round(playerAngle / MathHelper.TwoPi * SlotCount);
                playerSlot = ((playerSlot % SlotCount) + SlotCount) % SlotCount;

                List<int> candidates = [];
                for (int slot = 0; slot < SlotCount; slot++) {
                    int delta = Math.Abs(((slot - playerSlot) % SlotCount + SlotCount + SlotCount / 2)
                        % SlotCount - SlotCount / 2);
                    if (delta > PlayerSectorClearance) {
                        candidates.Add(slot);
                    }
                }
                for (int i = candidates.Count - 1; i > 0; i--) {
                    int j = Main.rand.Next(i + 1);
                    (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                }

                float drift = (Main.rand.NextBool() ? 1f : -1f) * DriftRate;
                int sealCount = Math.Min(SealCount(context), candidates.Count);
                for (int i = 0; i < sealCount; i++) {
                    float baseAngle = candidates[i] / (float)SlotCount * MathHelper.TwoPi;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), context.ArenaCenter, Vector2.Zero,
                        ModContent.ProjectileType<CultistZodiacSpokeProj>(), 44, 0f, Main.myPlayer,
                        npc.whoAmI, baseAngle, drift);
                }
                npc.netUpdate = true;
            }
            if (Timer == CastBeat) {
                CultistMotion.SigilCommitFX(npc.Center, CultistMotion.RuneGold, 1.3f);
                context.ScalePulse = 1.1f;
            }

            //封禁期祷文持续涌
            if (Timer > CastBeat && Timer % 12 == 0) {
                CultistMotion.RuneBurst(npc.Center + new Vector2(0f, -36f),
                    CultistMotion.PhaseCore(context.Phase), 1, 3f);
            }

            if (VaultUtils.isClient) {
                return null;
            }

            //双出口:辐条散尽即收,或超时兜底
            if (Timer > CastBeat + 40 && !AnySpokeAlive(npc.whoAmI)) {
                return new CultistCoilState(20);
            }
            if (Timer >= Timeout) {
                return new CultistCoilState(20);
            }
            return null;
        }

        private static bool AnySpokeAlive(int ownerWho) {
            int type = ModContent.ProjectileType<CultistZodiacSpokeProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == type && (int)proj.ai[0] == ownerWho) {
                    return true;
                }
            }
            return false;
        }
    }
}
