using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist
{
    /// <summary>
    /// 镜像假身：识别线索三条，无足影光渍（静态）、体色偏苍（静态）、齐射弹为苍白色（动态）<br/>
    /// 被打破：30 帧鼓胀预告后苍弹环爆（朝最近玩家的扇区留空，猜错的惩罚有预告且可躲）<br/>
    /// ai[0]=槽位 ai[1]=命令(0常态 1爆裂 2软散) ai[2]=齐射相位差 ai[3]=本体索引
    /// </summary>
    internal class CultistCloneAI : BrutalNPCOverride
    {
        public override int TargetID => NPCID.CultistBossClone;

        private const int VolleyPeriod = 55;
        private const int InflateFrames = 30;

        /// <summary>常态计帧</summary>
        private ref float LocalTimer => ref npc.localAI[0];
        /// <summary>爆裂/软散演出计帧</summary>
        private ref float EndTimer => ref npc.localAI[1];

        public override bool? CanBrutalOverride() {
            //重制未完成：由 DisabledReworkTypes 拒绝接管
            return null;
        }

        public override void SetProperty() {
            npc.knockBackResist = 0f;
            npc.npcSlots = 1f;
        }

        public override bool AI() {
            npc.damage = 0;

            //本体没了假身随散
            int parentIndex = (int)npc.ai[3];
            bool parentAlive = parentIndex >= 0 && parentIndex < Main.maxNPCs
                && Main.npc[parentIndex].active && Main.npc[parentIndex].type == NPCID.CultistBoss;
            if (!parentAlive && npc.ai[1] == 0f && !VaultUtils.isClient) {
                npc.ai[1] = 2f;
                npc.netUpdate = true;
            }

            switch ((int)npc.ai[1]) {
                case 1:
                    UpdateInflate();
                    return false;
                case 2:
                    UpdateSoftDismiss();
                    return false;
            }

            LocalTimer++;
            npc.dontTakeDamage = false;
            //全接管 AI 后自负 alpha：出生/晚入场自愈显形
            if (npc.alpha > 0) {
                npc.alpha = (int)MathHelper.Clamp(npc.alpha - 16, 0f, 255f);
            }

            //施法姿态，驻位微浮
            if (npc.localAI[2] != 11) {
                npc.localAI[2] = 11;
                npc.frameCounter = 0;
            }
            npc.velocity = new Vector2(0f, (float)System.Math.Sin((LocalTimer + npc.ai[0] * 23f) * 0.045f) * 0.55f);

            Player player = FindNearestPlayer();
            if (player != null) {
                npc.direction = npc.spriteDirection = player.Center.X >= npc.Center.X ? 1 : -1;
            }

            //齐射：与真身同拍异相，弹色是苍白的（动态识真线索）
            if (player != null && (LocalTimer + npc.ai[2]) % VolleyPeriod == 30f) {
                Vector2 dir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + dir * 26f, dir * 7.8f,
                        ModContent.ProjectileType<CultistPaleBolt>(), 30, 0f, Main.myPlayer);
                }
                CultistMotion.CastFlash(npc.Center + dir * 26f, CultistMotion.PaleClone, 0.6f);
            }

            Lighting.AddLight(npc.Center, CultistMotion.PaleClone.ToVector3() * 0.25f);
            return false;
        }

        /// <summary>爆裂：鼓胀预告 30 帧 → 苍弹环爆（朝最近玩家扇区留空）→ 散场</summary>
        private void UpdateInflate() {
            EndTimer++;
            npc.dontTakeDamage = true;
            npc.velocity *= 0.85f;
            npc.scale = 1f + 0.4f * MathHelper.Clamp(EndTimer / InflateFrames, 0f, 1f);

            if (EndTimer == 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit55 with { Volume = 0.8f, Pitch = 0.4f }, npc.Center);
            }
            if (EndTimer % 5 == 0) {
                CultistMotion.RuneBurst(npc.Center, CultistMotion.PaleClone, 2, 4f);
            }

            if (EndTimer < InflateFrames) {
                return;
            }

            //环爆：10 向苍弹，朝最近玩家 ±35° 留空，躲进指向自己的扇区即可
            CultistMotion.ImpactBurst(npc.Center, 1, 1.1f);
            CultistMotion.RuneBurst(npc.Center, CultistMotion.PaleClone, 12, 7f);
            if (!VaultUtils.isClient) {
                Player player = FindNearestPlayer();
                float toPlayer = player != null ? (player.Center - npc.Center).ToRotation() : 0f;
                for (int i = 0; i < 10; i++) {
                    float angle = MathHelper.TwoPi * i / 10f;
                    if (player != null && System.Math.Abs(MathHelper.WrapAngle(angle - toPlayer)) < 0.61f) {
                        continue;
                    }
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, angle.ToRotationVector2() * 5.5f,
                        ModContent.ProjectileType<CultistPaleBolt>(), 30, 0f, Main.myPlayer);
                }
                Deactivate();
            }
        }

        /// <summary>软散：本体收阵指令，化符渐隐，无惩罚弹</summary>
        private void UpdateSoftDismiss() {
            EndTimer++;
            npc.dontTakeDamage = true;
            npc.velocity *= 0.9f;
            npc.alpha = (int)MathHelper.Clamp(npc.alpha + 14, 0f, 255f);
            if (EndTimer % 4 == 0) {
                CultistMotion.RuneBurst(npc.Center, CultistMotion.PaleClone, 1, 3f);
            }
            if (npc.alpha >= 255 && !VaultUtils.isClient) {
                Deactivate();
            }
        }

        private void Deactivate() {
            npc.life = 0;
            npc.active = false;
            if (Main.netMode == NetmodeID.Server) {
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npc.whoAmI);
            }
        }

        private Player FindNearestPlayer() {
            Player best = null;
            float bestDist = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                float dist = player.DistanceSQ(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = player;
                }
            }
            return best;
        }

        /// <summary>被打破：不走死亡，转爆裂演出（权威端）</summary>
        public override bool? CheckDead() {
            if (npc.ai[1] == 0f) {
                npc.ai[1] = 1f;
                npc.life = 1;
                npc.dontTakeDamage = true;
                npc.netUpdate = true;
            }
            return false;
        }

        public override bool CheckActive() => false;

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //爆裂期白热渐涨
            float paleness = 0.55f;
            if (npc.ai[1] == 1f) {
                float t = MathHelper.Clamp(EndTimer / InflateFrames, 0f, 1f);
                drawColor = Color.Lerp(drawColor, Color.White, t * 0.7f);
                paleness = 0.55f + t * 0.45f;
            }
            Rendering.CultistRenderHelper.DrawCloneBody(spriteBatch, npc, screenPos, drawColor, paleness);
            return false;
        }
    }
}
