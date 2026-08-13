using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera
{
    /// <summary>
    /// 触手部件：二阶段近身武器。ai[0]=方位种子 ai[1]=命令参数(环向/鞭角)
    /// ai[2]=模式 ai[3]=0(保持原版锚语义)；决策服务端，运动各端确定性积分
    /// </summary>
    internal class PlanteraTentacleAI : CWRNPCOverride
    {
        public override int TargetID => NPCID.PlanterasTentacle;

        /// <summary>松弛环绕本体</summary>
        internal const int ModeIdle = 0;
        /// <summary>处刑圈：外扩硬环旋转</summary>
        internal const int ModeRing = 1;
        /// <summary>鞭刑预告(缩卷+瞄线)</summary>
        internal const int ModeWhipAim = 2;
        /// <summary>死亡脱力</summary>
        internal const int ModeLimp = 4;

        private const float WhipReach = 340f;
        private const int WhipExtend = 6;
        private const int WhipHold = 8;
        private const int WhipRetract = 12;

        private int lastMode = -1;
        private int modeTimer;
        private float spinPhase;
        private bool lashing;

        public override bool? CanCWROverride() {
            return null;
        }

        public override void SetProperty() {
            lastMode = -1;
            modeTimer = 0;
            spinPhase = 0f;
            lashing = false;
        }

        #region 命令接口(服务端调用)
        internal static void CommandIdle(NPC tent) {
            if (!tent.active) {
                return;
            }
            tent.ai[2] = ModeIdle;
            tent.netUpdate = true;
        }

        /// <summary>处刑圈，dir=±1旋向</summary>
        internal static void CommandRing(NPC tent, int dir) {
            if (!tent.active) {
                return;
            }
            tent.ai[2] = ModeRing;
            tent.ai[1] = dir;
            tent.netUpdate = true;
        }

        /// <summary>鞭刑，angle=出鞭方向</summary>
        internal static void CommandWhip(NPC tent, float angle) {
            if (!tent.active) {
                return;
            }
            tent.ai[2] = ModeWhipAim;
            tent.ai[1] = angle;
            tent.netUpdate = true;
        }

        internal static void GoLimp(NPC tent) {
            if (!tent.active) {
                return;
            }
            tent.ai[2] = ModeLimp;
            tent.netUpdate = true;
        }

        /// <summary>服务端生成一根触手</summary>
        internal static int SpawnTentacle(NPC boss, float phaseSeed) {
            int index = NPC.NewNPC(boss.GetSource_FromAI(), (int)boss.Center.X, (int)boss.Center.Y,
                NPCID.PlanterasTentacle, boss.whoAmI, ai0: phaseSeed);
            if (index >= 0 && index < Main.maxNPCs) {
                Main.npc[index].netUpdate = true;
            }
            return index;
        }
        #endregion

        public override bool AI() {
            NPC boss = PlanteraAI.FindBoss();

            npc.aiStyle = -1;
            npc.timeLeft = 300;
            npc.knockBackResist = 0f;

            if (boss == null) {
                if (!VaultUtils.isClient) {
                    npc.life = 0;
                    npc.HitEffect();
                    npc.active = false;
                    npc.netUpdate = true;
                }
                return false;
            }

            int mode = (int)npc.ai[2];
            if (mode != lastMode) {
                lastMode = mode;
                modeTimer = 0;
                lashing = false;
            }
            modeTimer++;

            switch (mode) {
                case ModeRing:
                    UpdateRing(boss);
                    break;
                case ModeWhipAim:
                    UpdateWhip(boss);
                    break;
                case ModeLimp:
                    UpdateLimp();
                    break;
                default:
                    UpdateIdle(boss);
                    break;
            }

            //根朝本体
            npc.rotation = (boss.Center - npc.Center).ToRotation() - MathHelper.PiOver2;

            return false;
        }

        #region 各模式运动
        /// <summary>松弛环绕：小半径呼吸游动</summary>
        private void UpdateIdle(NPC boss) {
            spinPhase += 0.012f;
            float angle = npc.ai[0] + spinPhase
                + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 0.9f + npc.ai[0] * 3f) * 0.4f;
            float radius = 120f + 26f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.4f + npc.ai[0] * 5f);
            Vector2 wish = boss.Center + angle.ToRotationVector2() * radius;
            SpringTo(wish, 0.09f, 16f);
            npc.damage = npc.defDamage;
        }

        /// <summary>处刑圈：外扩到硬环随大部队旋转</summary>
        private void UpdateRing(NPC boss) {
            float dir = npc.ai[1] >= 0f ? 1f : -1f;
            spinPhase += 0.011f * dir;
            //入环软展开
            float deploy = MathHelper.Clamp(modeTimer / 26f, 0f, 1f);
            float radius = MathHelper.Lerp(120f, PlanteraDirector.TentacleRingRadius, VaultUtils.EaseOutCubic(deploy));
            float angle = npc.ai[0] + spinPhase;
            Vector2 wish = boss.Center + angle.ToRotationVector2() * radius;
            SpringTo(wish, 0.16f, 26f);
            npc.damage = (int)(npc.defDamage * 1.1f);

            //环上荧光
            if (!VaultUtils.isServer && Main.rand.NextBool(14)) {
                InnoVault.PRT.PRTLoader.NewParticle<PRT_PlanteraSporeMote>(npc.Center,
                    Main.rand.NextVector2Circular(0.5f, 0.5f),
                    PlanteraRenderHelper.GlowMagenta * 0.8f, 0.7f)?.SetLife(24);
            }
        }

        /// <summary>鞭刑：缩卷瞄准→甩出→收回，全程各端同步数学</summary>
        private void UpdateWhip(NPC boss) {
            float angle = npc.ai[1];
            Vector2 dir = angle.ToRotationVector2();
            int aimTime = PlanteraDirector.WhipTelegraphFrames;

            if (modeTimer <= aimTime) {
                //缩卷蓄势：贴回本体，读作拉弓
                float t = modeTimer / (float)aimTime;
                Vector2 coil = boss.Center + dir * MathHelper.Lerp(110f, 55f, t)
                    + dir.RotatedBy(MathHelper.PiOver2) * (float)Math.Sin(t * 18f) * 6f * (1f - t);
                SpringTo(coil, 0.25f, 30f);
                npc.damage = 0;

                if (modeTimer == aimTime - 8 && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Grass with { Pitch = 0.3f, Volume = 0.6f, MaxInstances = 6 }, npc.Center);
                }
                return;
            }

            int lashTimer = modeTimer - aimTime;

            if (lashTimer <= WhipExtend) {
                //甩出：poly(6)极锐缓出
                if (!lashing) {
                    lashing = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item32 with { Pitch = 0.2f, Volume = 0.9f, MaxInstances = 6 }, npc.Center);
                    }
                }
                float t = lashTimer / (float)WhipExtend;
                float ease = 1f - (float)Math.Pow(1f - t, 6);
                npc.Center = boss.Center + dir * MathHelper.Lerp(55f, WhipReach, ease);
                npc.velocity = Vector2.Zero;
                npc.damage = (int)(npc.defDamage * 1.3f);

                //鞭梢速度线
                if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                    Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.Plantera_Pink, 0f, 0f, 120, default, 1.2f);
                    dust.velocity = dir * Main.rand.NextFloat(4f, 9f);
                    dust.noGravity = true;
                }
                return;
            }

            if (lashTimer <= WhipExtend + WhipHold) {
                //定格顶点：伤害窗口与视觉一致
                npc.Center = boss.Center + dir * WhipReach;
                npc.velocity = Vector2.Zero;
                npc.damage = (int)(npc.defDamage * 1.3f);
                return;
            }

            if (lashTimer <= WhipExtend + WhipHold + WhipRetract) {
                //收鞭无伤
                float t = (lashTimer - WhipExtend - WhipHold) / (float)WhipRetract;
                npc.Center = boss.Center + dir * MathHelper.Lerp(WhipReach, 120f, VaultUtils.EaseInQuad(t));
                npc.velocity = Vector2.Zero;
                npc.damage = 0;
                return;
            }

            //鞭完自动回松弛(服务端广播归位)
            if (!VaultUtils.isClient) {
                CommandIdle(npc);
            }
        }

        /// <summary>脱力垂落</summary>
        private void UpdateLimp() {
            npc.velocity.X *= 0.96f;
            npc.velocity.Y = Math.Min(npc.velocity.Y + 0.2f, 8f);
            npc.damage = 0;
        }

        /// <summary>弹簧趋近目标点</summary>
        private void SpringTo(Vector2 wish, float rate, float maxSpeed) {
            Vector2 delta = wish - npc.Center;
            Vector2 targetVel = delta.SafeNormalize(Vector2.Zero) * Math.Min(delta.Length() * 0.14f, maxSpeed);
            npc.velocity = Vector2.Lerp(npc.velocity, targetVel, rate);
        }
        #endregion

        #region 帧动画与绘制
        public override bool FindFrame(int frameHeight) {
            npc.frameCounter++;
            if (npc.frameCounter >= 6) {
                npc.frameCounter = 0;
                npc.frame.Y += frameHeight;
                if (npc.frame.Y >= frameHeight * 4) {
                    npc.frame.Y = 0;
                }
            }
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            NPC boss = PlanteraAI.FindBoss();

            //藤蔓连体(鞭刑时绷成直线)
            if (boss != null && (int)npc.ai[2] != ModeLimp) {
                float dist = npc.Distance(boss.Center);
                bool whipping = (int)npc.ai[2] == ModeWhipAim && lashing;

                VineParams vine = VineParams.Default;
                vine.RestLength = dist + (whipping ? 0f : 40f);
                vine.HalfWidth = 7f;
                vine.Taut = whipping ? 1f : MathHelper.Clamp((dist - 200f) / 220f, 0f, 0.5f);
                vine.Pulse = whipping ? 0.6f : 0.12f;
                vine.PulseDir = 1f;
                vine.Phase2 = true;
                vine.Seed = 0.41f + npc.ai[0] * 0.07f % 0.9f;

                PlanteraVineRenderer.DrawVine(spriteBatch, boss.Center, npc.Center, vine);
            }

            Main.instance.LoadNPC(npc.type);
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            int frameHeight = texture.Height / Main.npcFrameCount[npc.type];
            Rectangle frameRec = new(0, npc.frame.Y, texture.Width, frameHeight);
            Vector2 origin = frameRec.Size() / 2f;
            Vector2 mainPos = npc.Center - screenPos;

            //鞭击时沿出鞭向速度拉伸
            Vector2 scale = new(npc.scale, npc.scale);
            float stretchRot = npc.rotation;
            if ((int)npc.ai[2] == ModeWhipAim && lashing) {
                scale.Y *= 1.35f;
            }

            spriteBatch.Draw(texture, mainPos, frameRec, drawColor,
                stretchRot, origin, scale, SpriteEffects.None, 0f);

            //荧光罩层
            spriteBatch.Draw(texture, mainPos, frameRec,
                PlanteraRenderHelper.GlowMagenta with { A = 0 } * 0.3f,
                stretchRot, origin, scale * 1.03f, SpriteEffects.None, 0f);

            return false;
        }
        #endregion
    }
}
