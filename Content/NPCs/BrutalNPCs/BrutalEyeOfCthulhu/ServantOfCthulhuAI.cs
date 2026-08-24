using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu
{
    /// <summary>
    /// 克苏鲁仆从编队接管：克眼生成时赋予编队参数，否则回退原版 AI<br/>
    /// ai[0]=模式(0原版 1编队就位 2出击 3环卫)　ai[1]=槽位+阵型*100　ai[2]=主眼索引　ai[3]=模式参数
    /// </summary>
    internal class ServantOfCthulhuAI : BrutalNPCOverride
    {
        public override int TargetID => NPCID.ServantofCthulhu;

        internal const int ModeVanilla = 0;
        internal const int ModeSeek = 1;
        internal const int ModeLaunched = 2;
        internal const int ModeOrbit = 3;

        /// <summary>阵型：0=枪列(主眼后方纵队)，1=血环(绕目标玩家)</summary>
        internal const int FormationLance = 0;
        internal const int FormationRing = 1;

        public override bool? CanBrutalOverride() {
            return null;
        }

        public override void SetProperty() {
            //出击残影需要位置缓存
            NPCID.Sets.TrailingMode[npc.type] = 3;
            NPCID.Sets.TrailCacheLength[npc.type] = 12;
        }

        private int Mode => (int)npc.ai[0];
        private int SlotIndex => (int)npc.ai[1] % 100;
        private int Formation => (int)npc.ai[1] / 100;

        private NPC Director {
            get {
                int idx = (int)npc.ai[2];
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC director = Main.npc[idx];
                if (!director.active || director.type != NPCID.EyeofCthulhu) {
                    return null;
                }
                return director;
            }
        }

        public override bool AI() {
            if (Mode == ModeVanilla) {
                return true;
            }

            NPC director = Director;
            //主眼失效→回归原版
            if (director == null && Mode != ModeLaunched) {
                RevertToVanilla();
                return true;
            }

            npc.noTileCollide = true;
            npc.timeLeft = Math.Max(npc.timeLeft, 60);

            switch (Mode) {
                case ModeSeek:
                    SeekFormationSlot(director);
                    break;
                case ModeLaunched:
                    UpdateLaunched();
                    break;
                case ModeOrbit:
                    UpdateOrbit(director);
                    break;
            }

            //周期同步（权威端）
            if (!VaultUtils.isClient && Main.GameUpdateCount % 30 == 0) {
                npc.netUpdate = true;
            }

            return false;
        }

        /// <summary>就位：飞向编队槽位，接触伤关闭（可读性阀门）</summary>
        private void SeekFormationSlot(NPC director) {
            npc.damage = 0;
            //各端本地一次性凝成演出（生成于权威端，远端靠此补上出场感）
            if (npc.localAI[3] == 0f) {
                npc.localAI[3] = 1f;
                if (!VaultUtils.isServer) {
                    EocMotion.BloodBurst(npc.Center, 0.5f, playSound: false);
                    SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.5f, Pitch = 0.25f }, npc.Center);
                }
            }
            Vector2 slotPos = ComputeSlotPosition(director);

            Vector2 toSlot = slotPos - npc.Center;
            float dist = toSlot.Length();
            float speed = MathHelper.Clamp(dist * 0.08f, 4f, 26f);
            Vector2 desired = toSlot.SafeNormalize(Vector2.Zero) * speed;
            npc.velocity = Vector2.Lerp(npc.velocity, desired, 0.16f);
            npc.rotation = npc.velocity.X * 0.06f;

            //就位后贴位微颤，蓄势读法
            if (dist < 26f) {
                npc.velocity *= 0.8f;
                if (!VaultUtils.isServer && Main.rand.NextBool(6)) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(npc.Center,
                        Main.rand.NextVector2Circular(1.4f, 1.4f), EocMotion.Arterial * 0.7f,
                        Main.rand.NextFloat(0.4f, 0.8f))?.Configure(14, 0.2f, 0.98f);
                }
            }

            Lighting.AddLight(npc.Center, EocMotion.Arterial.ToVector3() * 0.35f);
        }

        /// <summary>槽位坐标：枪列在主眼后方纵队；血环绕主眼目标玩家</summary>
        private Vector2 ComputeSlotPosition(NPC director) {
            if (Formation == FormationRing) {
                Player target = Main.player[director.target];
                if (!target.Alives()) {
                    return director.Center;
                }
                //环半径由主眼 ai[3] 低位驱动收拢（由合围状态控制），槽位均布
                float ringRadius = MathHelper.Clamp(director.ai[3], 300f, 700f);
                int totalSlots = Math.Max((int)npc.ai[3], 1);
                float angle = MathHelper.TwoPi * SlotIndex / totalSlots
                    + Main.GlobalTimeWrappedHourly * 0.13f;   //缓转防死板
                return target.Center + angle.ToRotationVector2() * ringRadius;
            }

            //枪列：主眼朝向反向排开
            Vector2 backDir = -(director.rotation + MathHelper.PiOver2).ToRotationVector2();
            return director.Center + backDir * (110f + SlotIndex * 92f);
        }

        /// <summary>出击：直线增压，接触伤开，一段时间后回归原版追猎</summary>
        private void UpdateLaunched() {
            npc.damage = npc.defDamage;
            //各端本地一次性出膛演出
            if (npc.localAI[2] == 0f) {
                npc.localAI[2] = 1f;
                if (!VaultUtils.isServer) {
                    Vector2 dir = npc.velocity.SafeNormalize(Vector2.UnitY);
                    EocMotion.BloodSpray(npc.Center - dir * 12f, -dir, 5, 8f, 0.5f);
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.45f, Pitch = 0.35f }, npc.Center);
                }
            }
            npc.ai[3]++;

            //复合增压
            if (npc.velocity.Length() < 30f) {
                npc.velocity *= 1.03f;
            }
            npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

            //出击拖尾
            if (!VaultUtils.isServer && EocMotion.OnScreen(npc.Center, 200f)) {
                if (Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(npc.Center + Main.rand.NextVector2Circular(8f, 8f),
                        -npc.velocity * 0.08f, EocMotion.Arterial * 0.8f, Main.rand.NextFloat(0.5f, 1f))?
                        .Configure(Main.rand.Next(12, 20), 0.26f, 0.98f);
                }
            }

            Lighting.AddLight(npc.Center, EocMotion.BrightBlood.ToVector3() * 0.4f);

            //出击窗口结束→回归原版
            if (npc.ai[3] > 95f) {
                RevertToVanilla();
            }
        }

        /// <summary>环卫：绕主眼旋转的血肉护盾</summary>
        private void UpdateOrbit(NPC director) {
            npc.damage = npc.defDamage;
            npc.ai[3] += 0.036f;
            float angle = npc.ai[3] + MathHelper.TwoPi * SlotIndex / 3f;
            Vector2 orbitPos = director.Center + angle.ToRotationVector2() * 205f;
            npc.velocity = (orbitPos - npc.Center) * 0.2f;
            npc.rotation = (angle + MathHelper.PiOver2) * 0.4f;
            Lighting.AddLight(npc.Center, EocMotion.Arterial.ToVector3() * 0.3f);
        }

        private void RevertToVanilla() {
            npc.ai[0] = ModeVanilla;
            npc.ai[1] = 0f;
            npc.ai[2] = 0f;
            npc.ai[3] = 0f;
            npc.noTileCollide = false;
            npc.netUpdate = true;
        }

        #region 静态编队工具（权威端由状态调用）
        /// <summary>生成一个编队仆从，返回索引；仅权威端</summary>
        internal static int SpawnFormationServant(NPC director, Vector2 pos, int mode, int formation, int slot, float param) {
            int idx = NPC.NewNPC(director.GetSource_FromAI(), (int)pos.X, (int)pos.Y, NPCID.ServantofCthulhu,
                0, mode, formation * 100 + slot, director.whoAmI, param);
            if (idx < Main.maxNPCs) {
                Main.npc[idx].netUpdate = true;
            }
            return idx;
        }

        /// <summary>把就位仆从点火出击；仅权威端</summary>
        internal static void LaunchServant(NPC servant, Vector2 velocity) {
            servant.ai[0] = ModeLaunched;
            servant.ai[3] = 0f;
            servant.velocity = velocity;
            servant.netUpdate = true;
        }
        #endregion

        #region 绘制
        /// <summary>编队态在原版绘制上叠加血光内衬</summary>
        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Mode == ModeVanilla) {
                return false;
            }
            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            int frameHeight = tex.Height / Main.npcFrameCount[npc.type];
            Rectangle rec = new(0, npc.frame.Y, tex.Width, frameHeight);
            Vector2 pos = npc.Center - screenPos;

            //出击态额外速度残影
            if (Mode == ModeLaunched) {
                for (int i = 2; i < npc.oldPos.Length; i += 2) {
                    if (npc.oldPos[i] == Vector2.Zero) {
                        break;
                    }
                    float t = 1f - i / (float)npc.oldPos.Length;
                    Vector2 ghostPos = npc.oldPos[i] + npc.Size / 2f - screenPos;
                    spriteBatch.Draw(tex, ghostPos, rec, new Color(150, 26, 34, 30) * (0.4f * t),
                        npc.rotation, rec.Size() / 2f, npc.scale, SpriteEffects.None, 0f);
                }
            }

            float glow = Mode == ModeLaunched ? 0.55f : 0.3f;
            spriteBatch.Draw(tex, pos, rec, (EocMotion.Arterial with { A = 0 }) * glow,
                npc.rotation, rec.Size() / 2f, npc.scale * 1.04f, SpriteEffects.None, 0f);
            return false;
        }
        #endregion
    }
}
