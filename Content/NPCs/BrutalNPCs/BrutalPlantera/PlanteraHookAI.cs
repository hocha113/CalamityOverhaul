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
    /// 钩爪部件：锚定墙体拖动本体的运动引擎。
    /// npc.ai[0/1]=锚点tile坐标 ai[2]=模式 ai[3]=序号0~2；
    /// 决策服务端，运动各端从ai确定性积分，netUpdate对账
    /// </summary>
    internal class PlanteraHookAI : CWRNPCOverride
    {
        public override int TargetID => NPCID.PlanterasHook;

        /// <summary>自由狩锚，跟随玩家</summary>
        internal const int ModeFree = 0;
        /// <summary>状态命令锚定(格栅/猛扑/新星/入场)</summary>
        internal const int ModeCommand = 1;
        /// <summary>死亡脱力垂落</summary>
        internal const int ModeLimp = 2;

        /// <summary>到锚判定距离</summary>
        private const float ArriveDist = 16f;

        /// <summary>飞行状态锁存，0未达 1已嵌入(各端本地)</summary>
        private bool embedded;
        private float glowFlash;

        public override bool? CanCWROverride() {
            return null;
        }

        public override void SetStaticDefaults() {
            //钩爪常在屏外锚定，藤蔓要一直画到屏内，不能被视野裁剔
            NPCID.Sets.MustAlwaysDraw[NPCID.PlanterasHook] = true;
        }

        public override void SetProperty() {
            embedded = false;
            glowFlash = 0f;
        }

        #region 命令接口(服务端调用)
        /// <summary>命令钩爪锚到世界坐标(内部换tile)，服务端调用</summary>
        internal static void Command(NPC hook, Vector2 worldPos) {
            if (!hook.active) {
                return;
            }
            hook.ai[0] = (int)(worldPos.X / 16f);
            hook.ai[1] = (int)(worldPos.Y / 16f);
            hook.ai[2] = ModeCommand;
            hook.netUpdate = true;
        }

        /// <summary>释放回自由模式，服务端调用</summary>
        internal static void Release(NPC hook) {
            if (!hook.active) {
                return;
            }
            hook.ai[2] = ModeFree;
            hook.localAI[0] = Main.rand.Next(30, 90);
            hook.netUpdate = true;
        }

        /// <summary>死亡脱力，服务端调用</summary>
        internal static void GoLimp(NPC hook) {
            if (!hook.active) {
                return;
            }
            hook.ai[2] = ModeLimp;
            hook.netUpdate = true;
        }

        /// <summary>锚点世界坐标</summary>
        internal static Vector2 AnchorWorld(NPC hook) {
            return new Vector2(hook.ai[0] * 16f + 8f, hook.ai[1] * 16f + 8f);
        }

        /// <summary>是否已嵌入锚点(按距离判定，各端一致)</summary>
        internal static bool IsAnchored(NPC hook) {
            return (int)hook.ai[2] != ModeLimp && hook.Distance(AnchorWorld(hook)) < ArriveDist + 4f;
        }

        /// <summary>在玩家周边搜一个实心锚点tile；找不到给空气锚(藤结)，保证收敛</summary>
        internal static Vector2 FindAnchorNear(Vector2 center, float spreadTiles, Vector2 bias) {
            int cx = (int)(center.X / 16f);
            int cy = (int)(center.Y / 16f);
            for (int attempt = 0; attempt < 90; attempt++) {
                int radius = (int)(spreadTiles * (0.5f + attempt / 90f));
                int tx = cx + (int)(bias.X / 16f) + Main.rand.Next(-radius, radius + 1);
                int ty = cy + (int)(bias.Y / 16f) + Main.rand.Next(-radius, radius + 1);
                if (!WorldGen.InWorld(tx, ty, 10)) {
                    continue;
                }
                if (WorldGen.SolidTile(tx, ty) || (attempt > 55 && Main.tile[tx, ty].WallType > 0)) {
                    return new Vector2(tx * 16f + 8f, ty * 16f + 8f);
                }
            }
            //空气锚：荆棘藤结
            return center + bias + Main.rand.NextVector2Circular(spreadTiles * 8f, spreadTiles * 8f);
        }
        #endregion

        public override bool AI() {
            NPC boss = PlanteraAI.FindBoss();

            //禁用原版aiStyle画藤，藤由本类自绘
            npc.aiStyle = -1;
            npc.dontTakeDamage = true;
            npc.timeLeft = 300;

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
            bool phase2 = boss.ai[3] > 0.5f;
            PlanteraStateIndex bossState = PlanteraAI.GetStateIndex(boss);

            if (mode == ModeLimp) {
                UpdateLimp();
                return false;
            }

            //自由模式服务端狩锚
            if (mode == ModeFree && !VaultUtils.isClient) {
                UpdateFreeRetarget(boss, phase2);
            }

            UpdateTravel(boss, phase2, bossState);

            //爪面朝本体(藤从爪根长出)
            npc.rotation = (boss.Center - npc.Center).ToRotation() - MathHelper.PiOver2;

            //移动接触伤害门：静止锚桩无害
            float speed = npc.velocity.Length();
            npc.damage = speed > 8f ? (int)(npc.defDamage * (phase2 ? 1.2f : 1f)) : 0;

            if (glowFlash > 0f) {
                glowFlash *= 0.92f;
            }

            return false;
        }

        #region 运动
        /// <summary>向锚点飞行/嵌入，各端确定性</summary>
        private void UpdateTravel(NPC boss, bool phase2, PlanteraStateIndex bossState) {
            Vector2 anchor = AnchorWorld(npc);
            Vector2 delta = anchor - npc.Center;
            float dist = delta.Length();

            if (dist <= ArriveDist) {
                //嵌入：吸附锚点
                if (!embedded) {
                    embedded = true;
                    glowFlash = 1f;
                    OnEmbed();
                }
                npc.Center = anchor;
                npc.velocity = Vector2.Zero;
                return;
            }

            if (embedded && dist > ArriveDist * 3f) {
                //锚点被改，重新起飞
                embedded = false;
            }

            //飞行速度：近减远增，二阶段更快
            float travelSpeed = (phase2 ? 46f : 38f) + Math.Min(dist * 0.012f, 10f);
            //死亡/撤离期慢一点，读得清
            if (bossState == PlanteraStateIndex.Death || bossState == PlanteraStateIndex.Despawn) {
                travelSpeed *= 0.5f;
            }
            //末段减速扎入
            if (dist < 120f) {
                travelSpeed = Math.Max(travelSpeed * (dist / 120f), 7f);
            }

            npc.velocity = delta / dist * travelSpeed;

            //飞行叶屑(速度门控)
            if (!VaultUtils.isServer && npc.velocity.Length() > 14f && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                    DustID.JungleGrass, 0f, 0f, 100, default, Main.rand.NextFloat(0.9f, 1.4f));
                dust.velocity = -npc.velocity * 0.12f;
                dust.noGravity = true;
            }
        }

        /// <summary>嵌入瞬间反馈，各端本地</summary>
        private void OnEmbed() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 dir = npc.velocity.SafeNormalize(Vector2.UnitY);
            PlanteraRenderHelper.SpawnAnchorImpact(npc.Center, dir);
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.35f, Volume = 0.85f, MaxInstances = 4 }, npc.Center);
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.6f, Volume = 0.5f, MaxInstances = 4 }, npc.Center);
        }

        /// <summary>自由模式狩锚：计时到期换锚，跟玩家走，一次只动一只</summary>
        private void UpdateFreeRetarget(NPC boss, bool phase2) {
            Player target = Main.player[boss.target];
            if (!target.Alives()) {
                return;
            }

            npc.localAI[0] -= 1f;
            //低血狩锚更勤
            if (phase2) {
                npc.localAI[0] -= 1f;
            }
            if (boss.life < boss.lifeMax / 4) {
                npc.localAI[0] -= 1f;
            }

            //玩家甩太远强制追锚
            bool tooFar = npc.Distance(target.Center) > 1250f;
            if (!tooFar && npc.localAI[0] > 0f) {
                return;
            }

            //错峰：别的钩爪在飞就等
            if (!tooFar) {
                foreach (var other in Main.ActiveNPCs) {
                    if (other.whoAmI != npc.whoAmI && other.type == NPCID.PlanterasHook
                        && other.velocity.LengthSquared() > 4f) {
                        npc.localAI[0] = Main.rand.Next(40, 110);
                        return;
                    }
                }
            }

            npc.localAI[0] = phase2 ? Main.rand.Next(150, 280) : Main.rand.Next(240, 420);

            //锚点选址：玩家周边+移动预判偏置
            Vector2 bias = target.velocity * 22f + Main.rand.NextVector2Circular(90f, 90f);
            Vector2 anchor = FindAnchorNear(target.Center, 22f, bias);
            npc.ai[0] = (int)(anchor.X / 16f);
            npc.ai[1] = (int)(anchor.Y / 16f);
            npc.netUpdate = true;
        }

        /// <summary>脱力垂落：重力+摇摆衰减，死亡演出用</summary>
        private void UpdateLimp() {
            npc.velocity.X *= 0.97f;
            npc.velocity.Y = Math.Min(npc.velocity.Y + 0.25f, 9f);
            npc.rotation += npc.velocity.X * 0.01f;
            npc.damage = 0;
        }
        #endregion

        #region 帧动画
        public override bool FindFrame(int frameHeight) {
            //飞行张爪，静止合爪(镜像原版)
            bool moving = npc.velocity.LengthSquared() > 1f;
            npc.frameCounter++;
            if (npc.frameCounter >= 5) {
                npc.frameCounter = 0;
                if (moving && npc.frame.Y < frameHeight * 3) {
                    npc.frame.Y += frameHeight;
                }
                else if (!moving && npc.frame.Y > 0) {
                    npc.frame.Y -= frameHeight;
                }
            }
            return false;
        }
        #endregion

        #region 绘制
        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            NPC boss = PlanteraAI.FindBoss();

            //藤蔓画在爪下(本体whoAmI更小画得更晚，覆盖藤根)
            if (boss != null && (int)npc.ai[2] != ModeLimp) {
                bool phase2 = boss.ai[3] > 0.5f;
                float dist = npc.Distance(boss.Center);
                float pulse = PlanteraVineRenderer.ReadAndDecayPulse(npc.whoAmI);

                VineParams vine = VineParams.Default;
                vine.RestLength = dist + MathHelper.Lerp(70f, 10f, MathHelper.Clamp(pulse * 1.4f, 0f, 1f));
                vine.HalfWidth = 10f;
                //距离自然张力+蓄力命令张力
                vine.Taut = MathHelper.Clamp((dist - 520f) / 380f, 0f, 0.6f) + pulse * 0.55f;
                vine.Taut = MathHelper.Clamp(vine.Taut, 0f, 1f);
                vine.Pulse = pulse;
                vine.PulseDir = -1f;
                vine.Phase2 = phase2;
                vine.Seed = 0.13f + (int)npc.ai[3] * 0.29f;

                PlanteraVineRenderer.DrawVine(spriteBatch, boss.Center, npc.Center, vine);
            }

            //爪体
            Main.instance.LoadNPC(npc.type);
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            int frameHeight = texture.Height / Main.npcFrameCount[npc.type];
            Rectangle frameRec = new(0, npc.frame.Y, texture.Width, frameHeight);
            Vector2 origin = frameRec.Size() / 2f;
            Vector2 mainPos = npc.Center - screenPos;

            spriteBatch.Draw(texture, mainPos, frameRec, drawColor,
                npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);

            //嵌入/蓄力荧光闪
            if (glowFlash > 0.03f && boss != null) {
                Color glow = PlanteraRenderHelper.GlowByPhase(boss.ai[3] > 0.5f);
                spriteBatch.Draw(texture, mainPos, frameRec, glow with { A = 0 } * glowFlash,
                    npc.rotation, origin, npc.scale * 1.05f, SpriteEffects.None, 0f);
            }

            return false;
        }
        #endregion
    }
}
