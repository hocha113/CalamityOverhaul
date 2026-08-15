using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.States;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh
{
    /// <summary>
    /// 饥饿者：常态系绳突袭(移植原版锚点几何+灾厄口径加速)；
    /// 结网态飞入网槽编成拦截网，绘制肉链与通电辉光
    /// </summary>
    internal class TheHungryAI : CWRNPCOverride
    {
        public override int TargetID => NPCID.TheHungry;

        public override bool? CanCWROverride() {
            return null;
        }

        public override bool AI() {
            if (!WallOfFleshAI.TryGetWall(out NPC wall)) {
                npc.active = false;
                return false;
            }

            if (npc.justHit) {
                npc.ai[1] = 10f;
            }
            npc.TargetClosest();

            WofStateIndex wallState = WallOfFleshAI.GetStateIndex(wall);
            int wallPhase = (int)wall.ai[1] > 0 ? (int)wall.ai[1] : 1;

            //结网模式
            if (wallState == WofStateIndex.HungryNet
                && WofHungryNetState.TryGetNetSlot(wall, npc, wallPhase, out Vector2 slotPos)) {
                UpdateNetMode(slotPos);
                return false;
            }

            UpdateTetherMode(wall);
            return false;
        }

        #region 结网模式
        /// <summary>飞向网槽并驻位：强加速入位，驻位期小幅呼吸</summary>
        private void UpdateNetMode(Vector2 slotPos) {
            Vector2 toSlot = slotPos - npc.Center;
            float dist = toSlot.Length();

            if (dist > 40f) {
                //入位：定向强推
                Vector2 desired = toSlot.SafeNormalize(Vector2.Zero) * MathHelper.Clamp(dist / 14f, 8f, 19f);
                npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.18f);
            }
            else {
                //驻位：贴槽阻尼
                npc.velocity *= 0.82f;
                npc.velocity += toSlot * 0.06f;
            }

            //面向目标玩家(维持威胁读感)
            Player target = Main.player[npc.target];
            Vector2 toTarget = target.Center - npc.Center;
            if (toTarget.X > 0f) {
                npc.spriteDirection = 1;
                npc.rotation = (float)Math.Atan2(toTarget.Y, toTarget.X);
            }
            else if (toTarget.X < 0f) {
                npc.spriteDirection = -1;
                npc.rotation = (float)Math.Atan2(toTarget.Y, toTarget.X) + MathHelper.Pi;
            }

            Lighting.AddLight(npc.Center, 0.32f, 0.1f, 0.08f);
        }
        #endregion

        #region 系绳模式(原版几何+灾厄口径)
        private void UpdateTetherMode(NPC wall) {
            bool death = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
            float acceleration = death ? 0.15f : 0.12f;
            float tetherRadius = 300f;

            npc.damage = npc.defDamage;
            npc.defense = npc.defDefense;
            float wallLifeRatio = MathHelper.Clamp(wall.life / (float)wall.lifeMax, 0f, 1f);
            if (wallLifeRatio < 0.5f) {
                npc.damage = npc.defDamage * 2;
                npc.defense = 30;
                acceleration += death ? 0.1f : 0.08f;
            }
            else if (wallLifeRatio < 0.75f) {
                npc.damage = (int)Math.Round(npc.defDamage * 1.5f);
                npc.defense = 20;
                acceleration += death ? 0.05f : 0.04f;
            }

            //个体差异化系绳半径(原版按whoAmI打散)
            if (npc.whoAmI % 4 == 0) {
                tetherRadius *= 1.75f;
            }
            if (npc.whoAmI % 4 == 1) {
                tetherRadius *= 1.5f;
            }
            if (npc.whoAmI % 4 == 2) {
                tetherRadius *= 1.25f;
            }
            if (npc.whoAmI % 3 == 0) {
                tetherRadius *= 1.5f;
            }
            if (npc.whoAmI % 3 == 1) {
                tetherRadius *= 1.25f;
            }
            tetherRadius *= 0.75f;

            //锚点：墙域高度按ai[0]分数定位(原版几何)
            float anchorX = wall.Center.X;
            float anchorY = WofWallField.Top + WofWallField.Height * npc.ai[0];

            //突袭涨圈循环(原版 ai[2])
            npc.ai[2] += 1f;
            if (npc.ai[2] > 100f) {
                tetherRadius = (int)(tetherRadius * 1.3f);
                if (npc.ai[2] > 200f) {
                    npc.ai[2] = 0f;
                }
            }

            Vector2 anchor = new Vector2(anchorX, anchorY);
            Player target = Main.player[npc.target];
            float toTargetX = target.Center.X - npc.width / 2 - anchor.X;
            float toTargetY = target.Center.Y - npc.height / 2 - anchor.Y;
            float targetDist = (float)Math.Sqrt(toTargetX * toTargetX + toTargetY * toTargetY);

            if (npc.ai[1] == 0f) {
                //系绳内追玩家：目标点=锚点+受限向量(原版算法)
                if (targetDist > tetherRadius) {
                    float scale = tetherRadius / targetDist;
                    toTargetX *= scale;
                    toTargetY *= scale;
                }

                if (npc.position.X < anchorX + toTargetX) {
                    npc.velocity.X += acceleration;
                    if (npc.velocity.X < 0f && toTargetX > 0f) {
                        npc.velocity.X += acceleration * 2.5f;
                    }
                }
                else if (npc.position.X > anchorX + toTargetX) {
                    npc.velocity.X -= acceleration;
                    if (npc.velocity.X > 0f && toTargetX < 0f) {
                        npc.velocity.X -= acceleration * 2.5f;
                    }
                }
                if (npc.position.Y < anchorY + toTargetY) {
                    npc.velocity.Y += acceleration;
                    if (npc.velocity.Y < 0f && toTargetY > 0f) {
                        npc.velocity.Y += acceleration * 2.5f;
                    }
                }
                else if (npc.position.Y > anchorY + toTargetY) {
                    npc.velocity.Y -= acceleration;
                    if (npc.velocity.Y > 0f && toTargetY < 0f) {
                        npc.velocity.Y -= acceleration * 2.5f;
                    }
                }

                //速度上限：墙冲刺时大幅上调追赶(网不脱节)
                float maxVelocity = 4f;
                float velocityBoost = 1.5f;
                if (wallLifeRatio < 0.75f) {
                    velocityBoost += 0.7f;
                }
                if (wallLifeRatio < 0.5f) {
                    velocityBoost += 0.7f;
                }
                if (wallLifeRatio < 0.25f) {
                    velocityBoost += 0.9f;
                }
                if (wallLifeRatio < 0.1f) {
                    velocityBoost += 0.9f;
                }
                velocityBoost *= death ? 1.4f : 1.25f;
                velocityBoost += 0.3f;
                maxVelocity += velocityBoost * 0.35f;
                if ((npc.Center.X < wall.Center.X && wall.velocity.X > 0f)
                    || (npc.Center.X > wall.Center.X && wall.velocity.X < 0f)) {
                    maxVelocity += 6f;
                }
                //墙突进期整体提速
                if (WallOfFleshAI.GetStateIndex(wall) == WofStateIndex.SurgeDash) {
                    maxVelocity += 8f;
                }

                npc.velocity.X = MathHelper.Clamp(npc.velocity.X, -maxVelocity, maxVelocity);
                npc.velocity.Y = MathHelper.Clamp(npc.velocity.Y, -maxVelocity, maxVelocity);
            }
            else if (npc.ai[1] > 0f) {
                npc.ai[1] -= 1f;
            }
            else {
                npc.ai[1] = 0f;
            }

            //旋转朝目标
            if (toTargetX > 0f) {
                npc.spriteDirection = 1;
                npc.rotation = (float)Math.Atan2(toTargetY, toTargetX);
            }
            else if (toTargetX < 0f) {
                npc.spriteDirection = -1;
                npc.rotation = (float)Math.Atan2(toTargetY, toTargetX) + MathHelper.Pi;
            }

            Lighting.AddLight(npc.Center, 0.3f, 0.2f, 0.1f);
        }
        #endregion

        #region 绘制：网链
        /// <summary>
        /// 结网期绘制肉链：只在成对节点(0-1、2-3...)间拉链，与判定同源，
        /// 对与对之间是可穿越的窗口。偶数秩节点负责绘制到下一节点的链。
        /// 编织期暗色垂坠，通电后血光贲张
        /// </summary>
        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!WallOfFleshAI.TryGetWall(out NPC wall)
                || WallOfFleshAI.GetStateIndex(wall) != WofStateIndex.HungryNet) {
                return false;
            }

            int wallPhase = (int)wall.ai[1] > 0 ? (int)wall.ai[1] : 1;
            List<NPC> members = CollectNetMembers(wallPhase);
            int selfIndex = members.IndexOf(npc);
            //只有偶数秩且有下一节点的成员持链
            if (selfIndex < 0 || selfIndex % 2 != 0 || selfIndex + 1 >= members.Count) {
                return false;
            }

            bool armed = WofHungryNetState.NetArmed(wall);
            DrawFleshLink(spriteBatch, npc.Center, members[selfIndex + 1].Center, armed);
            return false;
        }

        /// <summary>网成员：whoAmI升序取前N(与判定/槽位同源规则，逐帧稳定)</summary>
        private static List<NPC> CollectNetMembers(int phase) {
            List<NPC> members = [];
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.TheHungry) {
                    members.Add(n);
                }
            }
            members.Sort((a, b) => a.whoAmI.CompareTo(b.whoAmI));
            int cap = WofHungryNetState.MaxNetNodes(phase);
            if (members.Count > cap) {
                members.RemoveRange(cap, members.Count - cap);
            }
            return members;
        }

        /// <summary>肉链：Chain12 分段铺设+轻微垂坠，通电时叠加血光与行走脉冲</summary>
        private static void DrawFleshLink(SpriteBatch spriteBatch, Vector2 from, Vector2 to, bool armed) {
            Texture2D chainTex = TextureAssets.Chain12.Value;
            Texture2D glowTex = CWRAsset.SoftGlow.Value;
            float segLen = chainTex.Height;
            Vector2 delta = to - from;
            float dist = delta.Length();
            if (dist < 8f || dist > 900f) {
                return;
            }
            Vector2 dir = delta / dist;
            //链端内收：首尾链节埋进两端肉体内，不在肉身表面暴露平切链头
            float inset = MathHelper.Min(14f, dist * 0.12f);
            from += dir * inset;
            dist -= inset * 2f;
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            int segments = (int)(dist / segLen) + 1;
            float rotation = dir.ToRotation() + MathHelper.PiOver2;

            //行走脉冲相位(通电后一颗亮珠沿链滑动)
            float pulsePos = (Main.GlobalTimeWrappedHourly * 1.7f) % 1f;

            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                //端点锥收：靠近两端的链节缩小变暗，读作没入肉体
                float endT = MathHelper.Clamp(Math.Min(t, 1f - t) * 5f, 0f, 1f);
                //垂坠：中段下垂+轻微呼吸摆
                float sag = (float)Math.Sin(t * MathHelper.Pi) *
                    (10f + 5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.3f + from.X * 0.01f));
                Vector2 pos = from + dir * (t * dist) + perp * sag * 0.3f + Vector2.UnitY * sag * 0.7f;
                Color light = Lighting.GetColor((int)pos.X / 16, (int)(pos.Y / 16f));
                if (!armed) {
                    light *= 0.75f;
                }
                light *= MathHelper.Lerp(0.6f, 1f, endT);
                spriteBatch.Draw(chainTex, pos - Main.screenPosition, null, light, rotation,
                    chainTex.Size() / 2f, MathHelper.Lerp(0.7f, 1f, endT), SpriteEffects.None, 0f);

                if (armed) {
                    //通电血光
                    float pulse = (float)Math.Exp(-Math.Pow((t - pulsePos) * 6f, 2));
                    Color glow = new Color(255, 50, 35, 0) * (0.22f + pulse * 0.6f);
                    spriteBatch.Draw(glowTex, pos - Main.screenPosition, null, glow, 0f,
                        glowTex.Size() / 2f, 0.5f + pulse * 0.4f, SpriteEffects.None, 0f);
                }
            }
        }
        #endregion

        public override bool CheckActive() => false;
    }
}
