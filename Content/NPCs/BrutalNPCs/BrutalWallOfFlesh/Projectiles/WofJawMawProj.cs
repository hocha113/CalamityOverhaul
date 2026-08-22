using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles
{
    /// <summary>
    /// 饥饿之颚：饥饿长城的单张巨口，锚在墙面车道上随墙推进。
    /// ai[0]=墙whoAmI ai[1]=车道序 ai[2]=咬合帧(-1=死颚永不咬合，耷拉滴涎的安全信号)。
    /// 成形→开口等拍→颤动嘶声预告→急咬伸出→扣死→缩回；
    /// 错拍(16f)≥伤害窗(14f)，相邻车道永不同时热
    /// </summary>
    internal class WofJawMawProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private ref float Timer => ref Projectile.localAI[0];
        private NPC Wall => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private int Lane => (int)Projectile.ai[1];
        /// <summary>咬合帧，-1=死颚</summary>
        private float SnapTick => Projectile.ai[2];
        private bool IsDeadJaw => SnapTick < 0f;

        /// <summary>咬合全程帧：伸出+扣死+缩回</summary>
        private static int BiteTotal => WofDirector.JawLungeFrames + WofDirector.JawClampFrames + WofDirector.JawRetractFrames;

        /// <summary>当前伸出距离</summary>
        private float reach;
        /// <summary>当前开口角(弧度)</summary>
        private float openAngle;
        /// <summary>整体淡出 0~1</summary>
        private float fade = 1f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 620;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = WofDirector.JawVolleyLife + 10;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>宿主有效：墙活着且仍处颚浪态</summary>
        private bool HostValid {
            get {
                NPC wall = Wall;
                return wall.Alives() && wall.type == NPCID.WallofFlesh
                    && WallOfFleshAI.GetStateIndex(wall) == WofStateIndex.JawRipple;
            }
        }

        public override void AI() {
            NPC wall = Wall;
            if (!HostValid) {
                Projectile.Kill();
                return;
            }
            Timer++;

            //锚定：根部埋进墙面18px，车道Y随墙域几何每帧重算(各端本地一致)
            float laneY = MathHelper.Lerp(WofWallField.Top, WofWallField.Bottom,
                (Lane + 0.5f) / WofDirector.JawLaneCount);
            Projectile.Center = new Vector2(WofWallField.WallFaceX(wall) - wall.direction * 18f, laneY);
            Projectile.direction = wall.direction;

            float grow = MathHelper.Clamp(Timer / WofDirector.JawGrowFrames, 0f, 1f);
            reach = 0f;
            openAngle = 0f;

            if (Timer == 2 && !VaultUtils.isServer && WofMotionFX.OnScreen(Projectile.Center, 100f)) {
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.2f, Volume = 0.5f, MaxInstances = 6 }, Projectile.Center);
            }

            if (IsDeadJaw) {
                UpdateDeadJaw(grow);
                return;
            }

            if (Timer <= WofDirector.JawGrowFrames) {
                //成形期：闭口鼓包
                openAngle = 0.1f * grow;
                return;
            }

            float sinceSnap = Timer - SnapTick;
            if (sinceSnap <= 0f) {
                UpdateGape();
                return;
            }
            UpdateBite(sinceSnap);
        }

        /// <summary>开口等拍：常态微颤，末12帧颤动加剧+嘶声，最后4帧冻结(静默拍)</summary>
        private void UpdateGape() {
            float untilSnap = SnapTick - Timer;
            float pre = MathHelper.Clamp(1f - untilSnap / WofDirector.JawPreSnapFrames, 0f, 1f);
            openAngle = MathHelper.Lerp(0.5f, 0.8f, pre);
            //颤动：预告期加剧，最后4帧死寂冻结
            if (untilSnap > 4f) {
                openAngle += (float)Math.Sin(Main.GlobalTimeWrappedHourly * 42f + Lane * 2.4f)
                    * (0.03f + 0.05f * pre);
            }
            if ((int)untilSnap == WofDirector.JawPreSnapFrames && !VaultUtils.isServer
                && WofMotionFX.OnScreen(Projectile.Center, 100f)) {
                //咬合前的湿嘶
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = 0.55f, Volume = 0.6f, MaxInstances = 6 }, Projectile.Center);
            }
            Lighting.AddLight(Projectile.Center, WofMotionFX.BloodHot.ToVector3() * (0.25f + 0.45f * pre));
        }

        /// <summary>咬合：急伸急扣，缩回甩涎</summary>
        private void UpdateBite(float sinceSnap) {
            int dir = Projectile.direction;
            if (sinceSnap <= WofDirector.JawLungeFrames) {
                //急咬：极锐缓出，口随伸出闭合
                float t = sinceSnap / WofDirector.JawLungeFrames;
                float ease = 1f - (float)Math.Pow(1f - t, 5);
                reach = WofDirector.JawReach * ease;
                openAngle = MathHelper.Lerp(0.8f, 0.06f, t * t);
                if ((int)sinceSnap == 1 && !VaultUtils.isServer && WofMotionFX.OnScreen(Projectile.Center, 200f)) {
                    SoundEngine.PlaySound(SoundID.NPCDeath10 with { Pitch = 0.35f, Volume = 0.85f, MaxInstances = 5 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.25f, Volume = 0.7f, MaxInstances = 5 }, Projectile.Center);
                }
                return;
            }
            if (sinceSnap <= WofDirector.JawLungeFrames + WofDirector.JawClampFrames) {
                //扣死保持
                reach = WofDirector.JawReach;
                openAngle = 0.06f;
                if ((int)sinceSnap == WofDirector.JawLungeFrames + 1 && !VaultUtils.isServer) {
                    Vector2 head = Projectile.Center + new Vector2(dir * reach, 0f);
                    if (WofMotionFX.OnScreen(head, 150f)) {
                        WofMotionFX.SpawnBloodBurst(head, 0.7f, new Vector2(dir, 0f));
                        WofMotionFX.CameraPunch(head, 3.2f, 8, "WofJawClamp", new Vector2(dir, 0f));
                    }
                }
                return;
            }
            if (sinceSnap <= BiteTotal) {
                //缩回：甩涎滴洒(余波层)
                float t = (sinceSnap - WofDirector.JawLungeFrames - WofDirector.JawClampFrames) / WofDirector.JawRetractFrames;
                reach = WofDirector.JawReach * (1f - t * t);
                openAngle = MathHelper.Lerp(0.06f, 0.3f, t);
                if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                    Vector2 head = Projectile.Center + new Vector2(dir * reach, 0f);
                    if (WofMotionFX.OnScreen(head, 100f)) {
                        PRTLoader.NewParticle<PRT_HeartcarverDroplet>(head + Main.rand.NextVector2Circular(16f, 16f),
                            new Vector2(dir * Main.rand.NextFloat(-1f, 2f), Main.rand.NextFloat(1f, 3f)),
                            WofMotionFX.BloodMid, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(16, 28), 0.32f);
                    }
                }
                return;
            }
            //咬完余韵：闭口渐隐
            reach = 0f;
            openAngle = 0.1f;
            fade = MathHelper.Clamp(fade - 0.09f, 0f, 1f);
        }

        /// <summary>死颚：成形后垂头滴涎，全程无害，缺口的可读身份</summary>
        private void UpdateDeadJaw(float grow) {
            openAngle = 0.06f * grow;
            //轮末与活颚同步渐隐
            if (Timer > WofDirector.JawVolleyLife - 20) {
                fade = MathHelper.Clamp(fade - 0.06f, 0f, 1f);
            }
            if (!VaultUtils.isServer && grow >= 1f && Main.rand.NextBool(9)
                && WofMotionFX.OnScreen(Projectile.Center, 100f)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    Projectile.Center + new Vector2(Projectile.direction * Main.rand.NextFloat(20f, 70f), 10f),
                    new Vector2(0f, Main.rand.NextFloat(1f, 2.5f)),
                    WofMotionFX.BloodDark, Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(20, 32), 0.3f);
            }
        }

        /// <summary>只在伸出与扣死期造成伤害(伤害窗=可见咬合)</summary>
        public override bool? CanDamage() {
            if (IsDeadJaw) {
                return false;
            }
            float sinceSnap = Timer - SnapTick;
            return sinceSnap > 0f && sinceSnap <= WofDirector.JawLungeFrames + WofDirector.JawClampFrames
                ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (reach < 12f) {
                return false;
            }
            Vector2 head = Projectile.Center + new Vector2(Projectile.direction * reach, 0f);
            float p = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, head, WofDirector.JawHitWidth * 0.8f, ref p);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Bleeding, 240);
        }

        public override void OnKill(int timeLeft) {
            //中途被打断(墙死/换态)的小血泡收场
            if (!VaultUtils.isServer && fade > 0.4f && WofMotionFX.OnScreen(Projectile.Center, 100f)) {
                WofMotionFX.SpawnBloodBurst(Projectile.Center, 0.35f, new Vector2(Projectile.direction, 0f));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            NPC wall = Wall;
            if (!wall.Alives() || fade <= 0.02f) {
                return false;
            }
            Texture2D spindle = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            int dir = Projectile.direction != 0 ? Projectile.direction
                : (wall.direction != 0 ? wall.direction : 1);
            float grow = MathHelper.Clamp(Timer / WofDirector.JawGrowFrames, 0f, 1f);
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float dirAngle = dir > 0 ? 0f : MathHelper.Pi;

            //死颚淡入死灰：血色褪成灰败肉，安全信号
            float deadMix = IsDeadJaw ? 0.55f : 0f;
            Color flesh = Color.Lerp(WofMotionFX.BloodDark, new Color(112, 104, 92), deadMix);
            Color lip = Color.Lerp(WofMotionFX.BloodMid, new Color(128, 118, 102), deadMix);
            Color bone = Color.Lerp(new Color(214, 176, 160), new Color(150, 146, 132), deadMix);

            //咬合期：颈体从墙面伸向头部
            Vector2 headWorld = Projectile.Center + new Vector2(dir * reach, 0f);
            Vector2 headScreen = headWorld - Main.screenPosition;
            if (reach > 8f) {
                //运动拖影：两帧残像(实体色降透明，非加色)
                float sinceSnap = Timer - SnapTick;
                if (sinceSnap > 0f && sinceSnap <= WofDirector.JawLungeFrames + 2) {
                    DrawJawHead(spindle, baseScreen + new Vector2(dir * reach * 0.55f, 0f), dirAngle,
                        flesh * (0.3f * fade), lip * (0.3f * fade), bone * 0f, openAngle + 0.15f, grow);
                    DrawJawHead(spindle, baseScreen + new Vector2(dir * reach * 0.8f, 0f), dirAngle,
                        flesh * (0.45f * fade), lip * (0.45f * fade), bone * 0f, openAngle + 0.08f, grow);
                }
                //颈体：拉长的暗肉柱，根粗头细
                float neckLen = reach + 26f;
                Vector2 neckMid = baseScreen + new Vector2(dir * neckLen * 0.5f, 0f);
                Main.EntitySpriteDraw(spindle, neckMid, null, flesh * fade, dirAngle + MathHelper.PiOver2,
                    spindle.Size() / 2f, new Vector2(WofDirector.JawHitWidth / spindle.Width * 1.5f,
                    neckLen / spindle.Height * 1.04f), SpriteEffects.None, 0);
                //颈芯亮肉
                Main.EntitySpriteDraw(spindle, neckMid, null, lip * (0.7f * fade), dirAngle + MathHelper.PiOver2,
                    spindle.Size() / 2f, new Vector2(WofDirector.JawHitWidth / spindle.Width * 0.8f,
                    neckLen / spindle.Height * 0.96f), SpriteEffects.None, 0);
            }

            //根部鼓包：埋在墙面里的肉丘
            Main.EntitySpriteDraw(spindle, baseScreen, null, flesh * (0.95f * fade), dirAngle + MathHelper.PiOver2,
                spindle.Size() / 2f, new Vector2(2.2f, 1.1f) * grow * 0.6f, SpriteEffects.None, 0);

            //颚头(未伸出时画在根部)
            DrawJawHead(spindle, reach > 8f ? headScreen : baseScreen, dirAngle,
                flesh * fade, lip * fade, bone * fade, openAngle, grow);

            //喉光：预告期加色暖芯(死颚无光，无光即无害)
            if (!IsDeadJaw && Timer > WofDirector.JawGrowFrames && reach < 8f) {
                float untilSnap = SnapTick - Timer;
                float pre = MathHelper.Clamp(1f - untilSnap / WofDirector.JawPreSnapFrames, 0f, 1f);
                float throatGlow = 0.25f + 0.6f * pre;
                Main.EntitySpriteDraw(glow, baseScreen + new Vector2(dir * 26f, 0f), null,
                    WofMotionFX.BloodHot with { A = 0 } * (throatGlow * fade), 0f, glow.Size() / 2f,
                    new Vector2(1.3f, 0.8f) * grow, SpriteEffects.None, 0);
            }
            return false;
        }

        /// <summary>画一张颚头：上下唇肉双层+骨牙列</summary>
        private void DrawJawHead(Texture2D spindle, Vector2 posScreen, float dirAngle,
            Color flesh, Color lip, Color bone, float open, float grow) {
            const float LipLen = 88f;
            const float LipWidth = 26f;
            //死颚垂头：整口向下耷拉
            float sag = IsDeadJaw ? 0.3f * grow : 0f;
            int dir = Projectile.direction;

            for (int side = -1; side <= 1; side += 2) {
                //side=-1上唇 +1下唇；开口绕根部铰开
                float lipAngle = dirAngle + dir * (side * open + sag);
                Vector2 lipDir = lipAngle.ToRotationVector2();
                Vector2 lipMid = posScreen + lipDir * (LipLen * 0.5f * grow);
                //唇鞘(暗)
                Main.EntitySpriteDraw(spindle, lipMid, null, flesh, lipAngle + MathHelper.PiOver2,
                    spindle.Size() / 2f, new Vector2(LipWidth / spindle.Width * 1.35f,
                    LipLen * grow / spindle.Height * 1.05f), SpriteEffects.None, 0);
                //唇肉(亮)
                Main.EntitySpriteDraw(spindle, lipMid, null, lip * 0.85f, lipAngle + MathHelper.PiOver2,
                    spindle.Size() / 2f, new Vector2(LipWidth / spindle.Width * 0.75f,
                    LipLen * grow / spindle.Height * 0.92f), SpriteEffects.None, 0);
                //牙列：3枚骨刺垂直唇面向口内
                if (bone.A > 0 || bone.R > 0) {
                    for (int i = 0; i < 3; i++) {
                        float along = 0.4f + 0.25f * i;
                        Vector2 toothPos = posScreen + lipDir * (LipLen * along * grow);
                        float toothAngle = lipAngle - dir * side * MathHelper.PiOver2;
                        Main.EntitySpriteDraw(spindle, toothPos, null, bone, toothAngle + MathHelper.PiOver2,
                            spindle.Size() / 2f, new Vector2(7f / spindle.Width,
                            (20f - i * 3f) * grow / spindle.Height), SpriteEffects.None, 0);
                    }
                }
            }
        }
    }
}
