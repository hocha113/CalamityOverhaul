using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States
{
    /// <summary>
    /// 滞星雷阵(P3 起):司祭连挥三拍,每拍朝玩家当下位置撒一环滞星(星珠模式1),
    /// 珠向心缓滑后悬停成短命雷区,逼迫持续走位<br/>
    /// 公平阀:落点锁定出手拍不追踪;SpawnRadius 声明(不贴脸);
    /// 沿玩家动向留 EscapeHalf 逃生扇(发射循环直读);滞星自带 150 帧短命自清
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)CultistStateIndex.StasisMines, typeof(CultistStateContext))]
    internal class CultistStasisMinesState : CultistStateBase
    {
        public override string StateName => "CultistStasisMines";
        public override CultistStateIndex StateIndex => CultistStateIndex.StasisMines;

        /// <summary>三拍撒环</summary>
        private static readonly int[] WaveBeats = [26, 56, 86];
        /// <summary>环半径(px):不在玩家身上生成</summary>
        private const float SpawnRadius = 250f;
        private const int MineSlots = 8;
        /// <summary>逃生扇半宽(槽):沿玩家动向连空 2*EscapeHalf+1 槽</summary>
        private const int EscapeHalf = 1;
        private const int Timeout = 180;

        public override ICultistState OnUpdate(CultistStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            SetPose(npc, 11);
            FaceTarget(npc, player.Center);
            context.PushAura(0.7f, CultistMotion.PhaseCore(context.Phase));

            //贴近侧位压场:每一拍都看得见他挥手
            float side = npc.Center.X < player.Center.X ? -1f : 1f;
            Vector2 hover = player.Center + new Vector2(side * 430f, -240f)
                + CultistMotion.BreathingOffset(seed: 4.9f, 11f);
            CultistMotion.SpringHover(npc, hover, 0.013f, 0.09f, 18f);

            //三拍撒环
            for (int wave = 0; wave < WaveBeats.Length; wave++) {
                if (Timer != WaveBeats[wave]) {
                    continue;
                }
                //挥手拍:全端演出
                CultistMotion.CastFlash(npc.Center + new Vector2(side * -40f, -20f),
                    CultistMotion.PhaseCore(context.Phase), 1f);
                CultistMotion.Shake(npc.Center, 3f, 7);
                context.ScalePulse = 1.09f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.85f, Pitch = -0.25f + wave * 0.15f }, npc.Center);
                }

                if (VaultUtils.isClient) {
                    continue;
                }
                //落点锁定出手拍;逃生扇沿玩家动向(静止时朝场心,永远有活路)
                Vector2 anchor = player.Center;
                Vector2 escapeDir = player.velocity.Length() > 1f
                    ? player.velocity.SafeNormalize(Vector2.UnitX)
                    : (context.ArenaCenter - player.Center).SafeNormalize(Vector2.UnitX);
                float escapeAngle = escapeDir.ToRotation();
                int escapeSlot = (int)MathF.Round(escapeAngle / MathHelper.TwoPi * MineSlots);
                for (int slot = 0; slot < MineSlots; slot++) {
                    int delta = Math.Abs(((slot - escapeSlot) % MineSlots + MineSlots + MineSlots / 2)
                        % MineSlots - MineSlots / 2);
                    if (delta <= EscapeHalf) {
                        continue;
                    }
                    Vector2 dir = (slot / (float)MineSlots * MathHelper.TwoPi).ToRotationVector2();
                    Projectile.NewProjectile(npc.GetSource_FromAI(), anchor + dir * SpawnRadius,
                        -dir * 2.2f, ModContent.ProjectileType<CultistStarBead>(), 38, 0f,
                        Main.myPlayer, context.Phase, 1f);
                }
                npc.netUpdate = true;
            }

            if (VaultUtils.isClient) {
                return null;
            }

            //双出口:三拍撒完收势(滞星自灭),或超时兜底
            if (Timer >= WaveBeats[^1] + 40) {
                return new CultistCoilState();
            }
            if (Timer >= Timeout) {
                return new CultistCoilState();
            }
            return null;
        }
    }
}
