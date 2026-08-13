using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron
{
    /// <summary>
    /// 爆裂气泡接管：气泡迷宫的基本单元。<br/>
    /// ai[0]=模式(0追踪 1迷宫驻停 2环阵待发) ai[1]=模式参数(驻停前飞行帧/待发计时)
    /// ai[2]=爆裂倒计时(0未爆) ai[3]=体型系数
    /// </summary>
    internal class FishronBubbleAI : CWRNPCOverride
    {
        public override int TargetID => NPCID.DetonatingBubble;

        private int Mode => (int)npc.ai[0];
        private ref float Param => ref npc.ai[1];
        private ref float PopTimer => ref npc.ai[2];

        public override bool? CanCWROverride() {
            return null;
        }

        public override bool AI() {
            //体型系数初始化（服务端定，随生成同步）
            if (npc.ai[3] == 0f) {
                if (!VaultUtils.isClient) {
                    npc.ai[3] = Main.rand.Next(80, 121) / 100f;
                    npc.netUpdate = true;
                }
                else {
                    npc.ai[3] = 1f;
                }
            }
            npc.scale = npc.ai[3];

            //渐显
            npc.alpha = Math.Max(npc.alpha - 30, 50);

            //爆裂序列：胀大数帧后消失
            if (PopTimer > 0f) {
                UpdatePop();
                return false;
            }

            switch (Mode) {
                case 1:
                    UpdateMazeHold();
                    break;
                case 2:
                    UpdateRingArmed();
                    break;
                default:
                    UpdateChase();
                    break;
            }

            //玩家贴近即引爆
            CheckProximityPop();

            //气泡互斥与位置锚定：互斥力只在权威端算，错峰 netUpdate 把位置压回各端
            if (!VaultUtils.isClient) {
                PushApart();
                if ((Main.GameUpdateCount + (uint)npc.whoAmI) % 90 == 0) {
                    npc.netUpdate = true;
                }
            }

            Lighting.AddLight(npc.Center, FishronMotionFX.SeaGreen.ToVector3() * 0.22f);
            return false;
        }

        /// <summary>模式0：缓慢追踪玩家，超时自爆（近原版）</summary>
        private void UpdateChase() {
            if (npc.target < 0 || npc.target >= 255 || Main.player[npc.target].dead) {
                npc.TargetClosest();
            }
            Player player = Main.player[npc.target];
            if (player.Alives()) {
                Vector2 dir = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                const float inertia = 34f;
                npc.velocity = (npc.velocity * inertia + dir * 5.5f) / (inertia + 1f);
            }
            //轻微上浮扰动
            npc.velocity.Y -= 0.01f;

            Param++;
            if (Param >= 300f && !VaultUtils.isClient) {
                StartPop();
            }
        }

        /// <summary>模式1：按出膛速度飞行 ai[1] 帧，随后驻停成迷宫栅栏，只剩摆动与风漂</summary>
        private void UpdateMazeHold() {
            if (Param > 0f) {
                Param--;
                //抵达前逐渐减速
                if (Param < 12f) {
                    npc.velocity *= 0.86f;
                }
            }
            else {
                //驻停：本地帧计驱动的有界轻漾（正弦积分有界，各端偏差不发散）
                npc.localAI[0]++;
                float phase = npc.localAI[0] * 0.03f + npc.whoAmI * 0.7f;
                npc.velocity *= 0.9f;
                npc.position.Y += (float)Math.Sin(phase) * 0.3f;

                //超时自爆（服务端）
                if (npc.localAI[0] >= 640f && !VaultUtils.isClient) {
                    StartPop();
                }
            }
        }

        /// <summary>模式2：环阵定身待发；发射由 RingSpin 状态服务端统一点火</summary>
        private void UpdateRingArmed() {
            if (Param > 0f) {
                //待发期定身，微微收缩蓄势
                Param--;
                npc.velocity *= 0.82f;
                float pulse = 1f + 0.06f * (float)Math.Sin(Main.GameUpdateCount * 0.35f + npc.whoAmI);
                npc.scale = npc.ai[3] * pulse;
            }
            else {
                //点火后直线飞行，超时自爆
                npc.localAI[0]++;
                if (npc.localAI[0] >= 220f && !VaultUtils.isClient) {
                    StartPop();
                }
            }
        }

        /// <summary>玩家贴近引爆（各端各自检测本地玩家，爆裂由伤害框结算）</summary>
        private void CheckProximityPop() {
            if (VaultUtils.isClient) {
                return;
            }
            const int size = 34;
            Rectangle rect = npc.getRect();
            rect.Inflate(size, size);
            foreach (var player in Main.ActivePlayers) {
                if (!player.dead && rect.Intersects(player.getRect())) {
                    StartPop();
                    break;
                }
            }
        }

        private void StartPop() {
            PopTimer = 4f;
            npc.netUpdate = true;
        }

        /// <summary>爆裂：胀大判定数帧后自灭</summary>
        private void UpdatePop() {
            npc.velocity *= 0.8f;
            //各端首帧进入时各放一次演出（localAI 一次性旗标，容忍同步迟到）
            if (npc.localAI[1] == 0f) {
                npc.localAI[1] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.7f, Pitch = 0.2f, MaxInstances = 5 }, npc.Center);
                    FishronMotionFX.SpawnSprayCone(npc.Center, -Vector2.UnitY, 5, 2f, 6f, MathHelper.Pi, 0.8f);
                }
                //胀大判定窗
                npc.position = npc.Center;
                npc.width = npc.height = (int)(100 * npc.scale);
                npc.position -= new Vector2(npc.width / 2f, npc.height / 2f);
            }

            PopTimer--;
            if (PopTimer <= 0f) {
                npc.life = 0;
                npc.HitEffect();
                npc.active = false;
                if (!VaultUtils.isClient) {
                    npc.netUpdate = true;
                }
            }
        }

        /// <summary>气泡间轻推互斥</summary>
        private void PushApart() {
            foreach (var other in Main.ActiveNPCs) {
                if (other.whoAmI == npc.whoAmI || other.type != npc.type) {
                    continue;
                }
                Vector2 delta = other.Center - npc.Center;
                if (delta.Length() < npc.width + npc.height) {
                    Vector2 push = delta.SafeNormalize(Vector2.UnitY) * -0.06f;
                    npc.velocity += push;
                }
            }
        }

        /// <summary>环阵待发末段的临爆闪烁</summary>
        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Mode == 2 && Param > 0f && Param < 14f) {
                Texture2D tex = TextureAssets.Npc[npc.type].Value;
                float flash = 0.5f + 0.5f * (float)Math.Sin(Param * 1.4f);
                Color glint = new Color(FishronMotionFX.FoamWhite.R, FishronMotionFX.FoamWhite.G,
                    FishronMotionFX.FoamWhite.B, 0) * (flash * 0.7f);
                Rectangle frame = npc.frame;
                spriteBatch.Draw(tex, npc.Center - screenPos, frame, glint, npc.rotation,
                    frame.Size() / 2f, npc.scale * 1.08f, SpriteEffects.None, 0f);
            }
            return true;
        }
    }
}
