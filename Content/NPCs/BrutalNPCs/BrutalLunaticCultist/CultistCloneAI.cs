using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.States;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist
{
    /// <summary>
    /// 分身：镜像本体施法但滞后5帧（可学习破绽），仅博弈窗口可被击中；
    /// npc.ai[0]=阵位索引 ai[1]=个体种子 ai[2]=惩罚反击计时(&gt;0前摇 &lt;0冷却) ai[3]=本体whoAmI
    /// </summary>
    internal class CultistCloneAI : CWRNPCOverride
    {
        public override int TargetID => NPCID.CultistBossClone;

        /// <summary>镜像滞后帧数（破绽规则之一：真身先动）</summary>
        internal const int MirrorLag = 5;
        /// <summary>惩罚反击前摇</summary>
        internal const int PunishTelegraph = 36;
        /// <summary>惩罚冷却</summary>
        internal const int PunishCooldown = 45;

        //本体姿态环形缓冲，实现滞后镜像
        private readonly int[] poseBuffer = new int[MirrorLag + 1];
        private int poseBufferHead;
        private int frameCounter;
        private int lastLife;

        public override bool? CanCWROverride() {
            return null;
        }

        public override void SetProperty() {
            lastLife = npc.lifeMax;
        }

        /// <summary>取本体，无效返回 null</summary>
        private NPC GetBoss() {
            int bossIdx = (int)npc.ai[3];
            if (bossIdx < 0 || bossIdx >= Main.maxNPCs) {
                return null;
            }
            NPC boss = Main.npc[bossIdx];
            if (!boss.active || boss.type != NPCID.CultistBoss) {
                return null;
            }
            return boss;
        }

        /// <summary>无害破灭（服务端）</summary>
        internal static void MarkHarmlessDeath(NPC clone) {
            if (VaultUtils.isClient || !clone.active) {
                return;
            }
            clone.life = 0;
            clone.HitEffect();
            clone.active = false;
            clone.netUpdate = true;
        }

        public override bool AI() {
            NPC boss = GetBoss();
            if (boss == null) {
                //本体失效即自毁
                if (!VaultUtils.isClient) {
                    MarkHarmlessDeath(npc);
                }
                return false;
            }

            CultistBossAI bossOverride = boss.GetOverride<CultistBossAI>();
            CultistStateIndex bossState = (CultistStateIndex)(int)boss.ai[2];
            CultistElement element = bossOverride?.Context?.Element ?? CultistElement.Fire;

            //姿态滞后镜像
            int bossPose = bossOverride?.Context?.CastPose ?? CultistPose.Float;
            poseBuffer[poseBufferHead] = bossPose;
            poseBufferHead = (poseBufferHead + 1) % poseBuffer.Length;

            //博弈窗口才可被命中
            bool vulnerable = bossState is CultistStateIndex.MirrorBlink or CultistStateIndex.GrandRitual;
            npc.dontTakeDamage = !vulnerable;
            npc.chaseable = vulnerable;
            npc.damage = 0;

            //面向与本体一致（镜像感）
            if (bossOverride?.Context?.Target != null) {
                int sign = Math.Sign(bossOverride.Context.Target.Center.X - npc.Center.X);
                if (sign != 0) {
                    npc.direction = npc.spriteDirection = sign;
                }
            }

            UpdateMovement(boss, bossState);
            UpdatePunish(boss, bossOverride, bossState, element);

            //透明度贴合本体（瞬移窗口由本体状态驱动位置，透明度跟随本体走）
            npc.alpha = boss.alpha;

            //微光
            Lighting.AddLight(npc.Center, CultistPalette.CloneMain(element).ToVector3() * 0.35f);

            if (!VaultUtils.isClient && Main.GameUpdateCount % 10 == 0) {
                npc.netUpdate = true;
            }
            return false;
        }

        /// <summary>按本体状态决定悬浮方式</summary>
        private void UpdateMovement(NPC boss, CultistStateIndex bossState) {
            switch (bossState) {
                case CultistStateIndex.MirrorBlink:
                    //阵位由本体状态直接摆放，仅保留原地呼吸浮沉
                    npc.velocity = new Vector2(0f, (float)Math.Sin(Main.GameUpdateCount * 0.05f + npc.ai[1]) * 0.6f);
                    break;
                case CultistStateIndex.GrandRitual: {
                    //围绕仪式圆心的确定性环列（各端同算）
                    CultistBossAI bossOverride = boss.GetOverride<CultistBossAI>();
                    Vector2 ritualCenter = bossOverride?.Context != null
                        ? bossOverride.Context.RitualCenter
                        : boss.Center;
                    if (ritualCenter == Vector2.Zero) {
                        ritualCenter = boss.Center;
                    }
                    int slot = (int)npc.ai[0];
                    int slotCount = Math.Max(CountSiblings(boss) + 1, 2);
                    float angle = MathHelper.TwoPi * (slot + 1) / slotCount
                        + Main.GameUpdateCount * 0.006f - MathHelper.PiOver2;
                    Vector2 goal = ritualCenter + angle.ToRotationVector2() * CultistGrandRitualState.RingRadius;
                    SpringTo(goal, 0.09f, 15f);
                    break;
                }
                default: {
                    //斜后方护法编队
                    int slot = (int)npc.ai[0];
                    float side = slot % 2 == 0 ? -1f : 1f;
                    float tier = 1f + slot / 2;
                    Vector2 offset = new(side * (150f + 60f * tier), -60f - 34f * tier
                        + (float)Math.Sin(Main.GameUpdateCount * 0.035f + npc.ai[1]) * 18f);
                    SpringTo(boss.Center + offset, 0.06f, 13f);
                    break;
                }
            }
        }

        private void SpringTo(Vector2 goal, float rate, float maxSpeed) {
            Vector2 desired = (goal - npc.Center) * rate;
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.14f);
            npc.rotation = npc.velocity.X * 0.012f;
        }

        private static int CountSiblings(NPC boss) {
            int count = 0;
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.CultistBossClone && (int)n.ai[3] == boss.whoAmI) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>惩罚反击：博弈窗口被击中→前摇→放射电火花</summary>
        private void UpdatePunish(NPC boss, CultistBossAI bossOverride, CultistStateIndex bossState, CultistElement element) {
            //受击检测在各端都跑：服务端裁决，非服务端表现
            int hurt = lastLife - npc.life;
            lastLife = npc.life;
            bool freshHurt = hurt > 0 && npc.localAI[0] <= 0f;

            if (freshHurt && bossState == CultistStateIndex.GrandRitual) {
                npc.localAI[0] = PunishCooldown;
                if (!VaultUtils.isClient && bossOverride?.Context != null) {
                    //错误献祭：仪式加速
                    bossOverride.Context.RitualPunishRequests++;
                }
                if (!VaultUtils.isServer) {
                    //献祭闪红+播报
                    CultistRenderHelper.ElementImpact(npc.Center, element, 1.4f);
                    SoundEngine.PlaySound(SoundID.Zombie89 with { Volume = 0.9f, Pitch = 0.5f, MaxInstances = 3 }, npc.Center);
                    CultistBossAI.LocalText(CultistBossAI.LunaticCultist_RitualPunishText, CultistPalette.FireMain);
                }
            }
            if (npc.localAI[0] > 0f) {
                npc.localAI[0]--;
            }

            //冷却回升
            if (npc.ai[2] < 0f) {
                npc.ai[2]++;
                return;
            }

            //镜影博弈的反击前摇（服务端启动，走同步 ai[2]）
            if (!VaultUtils.isClient && freshHurt && npc.ai[2] == 0f
                && bossState == CultistStateIndex.MirrorBlink) {
                npc.ai[2] = PunishTelegraph;
                npc.netUpdate = true;
            }

            //前摇推进（各端同步走 ai[2]）
            if (npc.ai[2] > 0f) {
                //前摇视觉：向心电弧收拢
                if (!VaultUtils.isServer && Main.GameUpdateCount % 2 == 0) {
                    Vector2 start = npc.Center + Main.rand.NextVector2CircularEdge(90f, 90f);
                    PRTLoader.NewParticle<PRT_CultistRune>(start, Vector2.Zero,
                        CultistPalette.ThunderBright, 0.9f)?.Configure(npc.Center, 0.3f, 14);
                }
                if (npc.ai[2] == PunishTelegraph - 1f && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_LightningBugZap with { Volume = 0.8f, Pitch = -0.2f }, npc.Center);
                    CultistBossAI.LocalText(CultistBossAI.LunaticCultist_MirrorPunishText, CultistPalette.ThunderMain);
                }

                npc.ai[2]--;
                if (npc.ai[2] <= 0f) {
                    //释放帧
                    if (!VaultUtils.isServer) {
                        CultistRenderHelper.ElementImpact(npc.Center, CultistElement.Thunder, 1.2f);
                        SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.85f, MaxInstances = 3 }, npc.Center);
                    }
                    if (!VaultUtils.isClient) {
                        int damage = npc.GetAttackDamage_ForProjectiles(38f, 26f);
                        int count = 10;
                        float baseAngle = npc.ai[1] * 0.37f;
                        for (int i = 0; i < count; i++) {
                            Vector2 vel = (MathHelper.TwoPi * i / count + baseAngle).ToRotationVector2() * 6.2f;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel,
                                ModContent.ProjectileType<CultistArcSpark>(), damage, 0f, Main.myPlayer,
                                (float)CultistElement.Thunder, 1f);
                        }
                        npc.ai[2] = -PunishCooldown;
                        npc.netUpdate = true;
                    }
                }
            }
        }

        #region 帧与绘制

        public override bool FindFrame(int frameHeight) {
            if (npc.IsABestiaryIconDummy) {
                return true;
            }
            //滞后姿态
            int lagged = poseBuffer[poseBufferHead];
            frameCounter++;
            if (frameCounter >= 15) {
                frameCounter = 0;
            }
            int cell = frameCounter / 5;
            int row = lagged switch {
                CultistPose.CastForward => 10 + cell,
                CultistPose.CastUp => 7 + cell,
                CultistPose.Scream => 13 + cell,
                CultistPose.Stand => 0,
                _ => 4 + cell,
            };
            npc.frame.Y = row * frameHeight;
            return false;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            NPC boss = GetBoss();
            CultistElement element = CultistElement.Fire;
            var ctx = boss?.GetOverride<CultistBossAI>()?.Context;
            if (ctx != null) {
                element = ctx.Element;
            }

            //原版贴图懒加载守卫
            Main.instance.LoadNPC(npc.type);
            Texture2D texture = TextureAssets.Npc[npc.type].Value;
            Rectangle frameRec = npc.frame;
            Vector2 origin = frameRec.Size() / 2f;
            Vector2 drawPos = npc.Center - screenPos;
            SpriteEffects flip = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            float opacity = 1f - npc.alpha / 255f;
            if (opacity <= 0.01f) {
                return false;
            }

            Color main = CultistPalette.CloneMain(element);

            //去饱和元素光环（破绽规则之二：分身色偏灰）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            spriteBatch.Draw(glow, drawPos, null, main with { A = 0 } * (0.28f * opacity),
                0f, glow.Size() / 2f, 1.5f, SpriteEffects.None, 0f);

            //惩罚前摇警告环
            if (npc.ai[2] > 0f) {
                float t = 1f - npc.ai[2] / PunishTelegraph;
                Texture2D ring = CWRAsset.DiffusionCircle.Value;
                float ringScale = MathHelper.Lerp(1.7f, 0.4f, t) * 1.1f;
                spriteBatch.Draw(ring, drawPos, null, CultistPalette.ThunderBright with { A = 0 } * (0.55f * t + 0.2f),
                    Main.GlobalTimeWrappedHourly * 5f, ring.Size() / 2f, ringScale, SpriteEffects.None, 0f);
            }

            //本体（轻微灰调，肉眼可辨但需专注）
            Color body = drawColor.MultiplyRGB(new Color(232, 228, 238));
            spriteBatch.Draw(texture, drawPos, frameRec, body * opacity,
                npc.rotation, origin, npc.scale, flip, 0f);

            return false;
        }

        #endregion

        public override bool CheckActive() => false;

        /// <summary>分身被打死也按无害破灭处理（不掉落不触发事件）</summary>
        public override bool? CheckDead() {
            if (!VaultUtils.isServer) {
                CultistRenderHelper.CloneBurst(npc.Center, CultistElement.Thunder);
            }
            return true;
        }
    }
}
