using CalamityOverhaul.Common;
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
    internal class FishronBubbleAI : BrutalNPCOverride
    {
        public override int TargetID => NPCID.DetonatingBubble;

        /// <summary>在场帧戳：AI 与绘制各盖一次，水膜泡统一绘制层据此跳过无泡时的全表扫描</summary>
        internal static ActivityStamp PresenceStamp;

        private int Mode => (int)npc.ai[0];
        private ref float Param => ref npc.ai[1];
        private ref float PopTimer => ref npc.ai[2];

        #region 水膜泡渲染参数（FishronBubbleRender 消费）
        /// <summary>变形幅度：驻停轻漾，飞行时被气流揉得更凶</summary>
        internal float RenderWobble => MathHelper.Clamp(0.45f + npc.velocity.Length() * 0.08f, 0.45f, 1f);
        /// <summary>环阵待发末段的绷紧量</summary>
        internal float RenderArm {
            get {
                if (Mode != 2 || Param <= 0f) {
                    return 0f;
                }
                return Param < 16f ? 1f - Param / 16f : 0.15f;
            }
        }
        /// <summary>破膜进度：PopTimer 4→0 映射 0→1</summary>
        internal float RenderBurst => PopTimer > 0f ? MathHelper.Clamp((4f - PopTimer) / 3.2f, 0f, 1f) : 0f;
        /// <summary>渐显包络</summary>
        internal float RenderFade => MathHelper.Clamp((255 - npc.alpha) / 205f, 0f, 1f);
        #endregion

        public override bool? CanBrutalOverride() {
            return null;
        }

        /// <summary>着色器可用时本体交给 <see cref="Rendering.FishronBubbleRender"/> 单批绘制，缺失则回退原版</summary>
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            PresenceStamp.Stamp();
            return Rendering.FishronBubbleRender.PathReady ? false : null;
        }

        public override bool AI() {
            PresenceStamp.Stamp();
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

        /// <summary>解析猎物：目标失效就重找最近玩家，无可猎则 null（模式0/末相漂近共用）</summary>
        private Player ResolvePrey() {
            if (npc.target < 0 || npc.target >= 255 || Main.player[npc.target].dead) {
                npc.TargetClosest();
            }
            Player player = Main.player[npc.target];
            return player.Alives() ? player : null;
        }

        /// <summary>末相驻停泡的缓慢压近分量（服务端调用），无猎物回零</summary>
        private Vector2 PhaseThreeCreep() {
            if (!DukeFishronAI.AnyPhaseThreeActive()) {
                return Vector2.Zero;
            }
            Player prey = ResolvePrey();
            if (prey == null) {
                return Vector2.Zero;
            }
            return (prey.Center - npc.Center).SafeNormalize(Vector2.Zero) * 1.1f;
        }

        /// <summary>模式0：缓慢追踪玩家，超时自爆（近原版）</summary>
        private void UpdateChase() {
            Player player = ResolvePrey();
            if (player != null) {
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

        /// <summary>
        /// 模式1：按出膛速度飞行 ai[1] 帧，随后驻停成迷宫栅栏——
        /// 驻停不再钉死：洋流整阵缓摆（全体同相，走廊几何随阵平移不变形）
        /// 叠逐泡小环游。速度由服务端权威书写，客户端只积分同步值，
        /// 周期 netUpdate 校正漂差
        /// </summary>
        private void UpdateMazeHold() {
            if (Param > 0f) {
                Param--;
                //抵达前逐渐减速
                if (Param < 12f) {
                    npc.velocity *= 0.86f;
                }
            }
            else {
                npc.localAI[0]++;
                if (!VaultUtils.isClient) {
                    float t = Main.GameUpdateCount;
                    //洋流：低频正弦摆，积分有界（±60px 级），全泡共用一股
                    Vector2 current = new(
                        (float)Math.Sin(t * 0.006f) * 0.55f,
                        (float)Math.Sin(t * 0.0043f + 1.3f) * 0.38f);
                    //小环游：逐泡错相的缓慢绕圈（半径 10px 级）
                    Vector2 loop = (t * 0.055f + npc.whoAmI * 1.7f).ToRotationVector2() * 0.6f;
                    //末相：整阵在洋流之上再缓慢压向玩家，走廊随时间收拢
                    npc.velocity = Vector2.Lerp(npc.velocity, current + loop + PhaseThreeCreep(), 0.1f);
                }

                //超时自爆（服务端）
                if (npc.localAI[0] >= 640f && !VaultUtils.isClient) {
                    StartPop();
                }
            }
        }

        /// <summary>模式2：环阵待发漂浮（小环游代替定身）；发射由 RingSpin 状态服务端统一点火</summary>
        private void UpdateRingArmed() {
            if (Param > 0f) {
                Param--;
                //待发漂浮：服务端写小环游速度，环位轻轻游动仍保阵形；末相整环缓慢向玩家收口
                if (!VaultUtils.isClient) {
                    Vector2 loop = (Main.GameUpdateCount * 0.09f + npc.whoAmI * 1.3f).ToRotationVector2() * 0.8f;
                    npc.velocity = Vector2.Lerp(npc.velocity, loop + PhaseThreeCreep(), 0.15f);
                }
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
                    //破膜水珠环：膜化成一圈水屑弹开
                    for (int i = 0; i < 9; i++) {
                        Vector2 dir = (MathHelper.TwoPi * i / 9f + npc.whoAmI * 0.7f).ToRotationVector2();
                        InnoVault.PRT.PRTLoader.NewParticle<Rendering.PRT_FishronSpray>(
                            npc.Center + dir * 12f * npc.scale, dir * Main.rand.NextFloat(3f, 6.5f),
                            Color.Lerp(FishronMotionFX.SeaGreen, FishronMotionFX.FoamWhite, Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(16, 26), 0.2f);
                    }
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

        /// <summary>环阵待发末段的临爆闪烁：仅着色器缺失的回退路径需要，膜体自带 uArm 灼光</summary>
        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!Rendering.FishronBubbleRender.PathReady && Mode == 2 && Param > 0f && Param < 14f) {
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
