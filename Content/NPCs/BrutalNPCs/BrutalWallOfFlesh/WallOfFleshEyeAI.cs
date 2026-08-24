using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh
{
    /// <summary>
    /// 墙眼部件：贴墙滑轨定位(ai[0]=±1 上/下槽)，与墙共享血量。
    /// 平时按预告-点射节奏压制；扫描协议期锁定跟随光束；
    /// 接触伤害恒为0(墙面与舌头已经足够惩罚，眼不做廉价碰撞)
    /// </summary>
    internal class WallOfFleshEyeAI : BrutalNPCOverride
    {
        public override int TargetID => NPCID.WallofFleshEye;

        /// <summary>ai[1]=点射预告倒计时(服务端置位随同步下发，各端本地递减)</summary>
        private ref float TelegraphTimer => ref npc.ai[1];
        /// <summary>localAI[1]=服务端点射充能累计</summary>
        private ref float ChargeAccum => ref npc.localAI[1];

        public override bool? CanBrutalOverride() {
            return null;
        }

        public override bool AI() {
            if (!WallOfFleshAI.TryGetWall(out NPC wall)) {
                npc.active = false;
                return false;
            }

            //与墙共享血量(原版契约)
            npc.realLife = Main.wofNPCIndex;
            if (wall.life > 0) {
                npc.life = wall.life;
            }
            npc.damage = 0;
            npc.dontTakeDamage = wall.dontTakeDamage;

            //目标随墙
            if (npc.target < 0 || npc.target >= Main.maxPlayers || Main.player[npc.target].dead || !Main.player[npc.target].active) {
                npc.target = wall.target;
            }

            //贴墙滑轨(原版几何)
            npc.position.X = wall.position.X;
            npc.direction = wall.direction;
            npc.spriteDirection = npc.direction;
            UpdateSlotY();

            WofStateIndex wallState = WallOfFleshAI.GetStateIndex(wall);

            //扫描协议期：锁定跟随自己的光束
            Projectile beam = WofRetinaScanBeam.FindForEye(npc.whoAmI);
            if (beam != null) {
                Vector2 lookPoint = npc.Center + beam.rotation.ToRotationVector2() * 120f;
                UpdateRotation(lookPoint);
                TelegraphTimer = 0f;
                return false;
            }

            Player target = Main.player[npc.target];
            UpdateRotation(target.Center);

            //点射循环
            UpdatePotshot(wall, wallState, target);

            return false;
        }

        /// <summary>槽位Y：上眼在中线与上缘间，下眼在中线与下缘间，平滑逼近</summary>
        private void UpdateSlotY() {
            float middle = (Main.wofDrawAreaBottom + Main.wofDrawAreaTop) * 0.5f;
            float slotY = npc.ai[0] > 0f
                ? (middle + Main.wofDrawAreaTop) * 0.5f
                : (middle + Main.wofDrawAreaBottom) * 0.5f;
            slotY -= npc.height / 2;

            float diff = slotY - npc.position.Y;
            if (Math.Abs(diff) <= 1f) {
                npc.velocity.Y = 0f;
                npc.position.Y = slotY;
                return;
            }
            npc.velocity.Y = MathHelper.Clamp(Math.Abs(diff) * 0.04f, 1f, 6f) * Math.Sign(diff);
        }

        /// <summary>原版口径的注视旋转：目标在推进前方才转向</summary>
        private void UpdateRotation(Vector2 lookAt) {
            Vector2 toLook = lookAt - npc.Center;
            if (npc.direction > 0) {
                npc.rotation = lookAt.X > npc.Center.X
                    ? (float)Math.Atan2(-toLook.Y, -toLook.X) + MathHelper.Pi
                    : 0f;
            }
            else {
                npc.rotation = lookAt.X < npc.Center.X
                    ? (float)Math.Atan2(toLook.Y, toLook.X) + MathHelper.Pi
                    : 0f;
            }
        }

        /// <summary>能看到目标(在推进前方)</summary>
        private bool CanSeeTarget(Player target) {
            return npc.direction > 0 ? target.Center.X > npc.Center.X : target.Center.X < npc.Center.X;
        }

        /// <summary>
        /// 点射循环：充能累计(服务端)→预告置位ai[1]同步→归零帧发射。
        /// 各端读ai[1]绘制预警虹膜与瞄准线
        /// </summary>
        private void UpdatePotshot(NPC wall, WofStateIndex wallState, Player target) {
            //预告倒计时各端本地递减(视觉平滑)，发射决策只在服务端
            if (TelegraphTimer > 0f) {
                TelegraphTimer--;
                if (TelegraphTimer <= 0f && !VaultUtils.isClient) {
                    FireLaser(wall, target);
                }
                return;
            }

            if (VaultUtils.isClient) {
                return;
            }

            //只有战斗节奏态才点射
            bool combatState = wallState is WofStateIndex.Advance or WofStateIndex.LeechWave
                or WofStateIndex.FleshSpike or WofStateIndex.HungryNet or WofStateIndex.TongueLash
                or WofStateIndex.CrimsonExodus;
            if (!combatState || !target.Alives() || !CanSeeTarget(target)) {
                return;
            }

            float lifeRatio = MathHelper.Clamp(wall.life / (float)wall.lifeMax, 0f, 1f);
            float rate = 1f + (1f - lifeRatio) * 1.6f;
            if (wallState == WofStateIndex.CrimsonExodus) {
                rate *= 2.2f;
            }
            if ((int)wall.ai[1] >= 3) {
                rate *= 1.25f;
            }

            ChargeAccum += rate;
            //上下眼错拍：下眼相位提前半程
            float threshold = 300f + (npc.ai[0] > 0f ? 0f : -60f);
            if (ChargeAccum >= threshold) {
                ChargeAccum = 0f;
                TelegraphTimer = WofDirector.EyePotshotTelegraph;
                npc.netUpdate = true;
            }
        }

        /// <summary>发射：预告线锁定方向微带提前量，1~2连发(服务端)</summary>
        private void FireLaser(NPC wall, Player target) {
            if (!target.Alives() || !CanSeeTarget(target)) {
                return;
            }
            int shots = (int)wall.ai[1] >= 2 ? 2 : 1;
            int damage = WallOfFleshAI.ScaleDamage(wall, WofDirector.EyeLaserDamage);
            Vector2 aim = (target.Center + target.velocity * 9f - npc.Center).SafeNormalize(Vector2.UnitX * npc.direction);
            for (int i = 0; i < shots; i++) {
                Vector2 vel = aim.RotatedBy((i - (shots - 1) * 0.5f) * 0.07f) * 8.5f;
                //ai参数与原版眼激光一致(不带附加行为位)
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + vel.SafeNormalize(Vector2.Zero) * 30f,
                    vel, ProjectileID.EyeLaser, damage, 0f, Main.myPlayer);
            }
            npc.netUpdate = true;
        }

        #region 动画与绘制
        public override bool FindFrame(int frameHeight) {
            //原版咀嚼帧循环(接管走位后 ai[2] 不再冻结动画)
            npc.frameCounter += TelegraphTimer > 0f ? 2.0 : 1.0;
            if (npc.frameCounter >= 12.0) {
                npc.frameCounter = 0.0;
                npc.frame.Y += frameHeight;
                if (npc.frame.Y >= frameHeight * Main.npcFrameCount[npc.type]) {
                    npc.frame.Y = 0;
                }
            }
            return false;
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //充能强度：点射预告 / 扫描充能通道 取大
            float telegraphGlow = TelegraphTimer > 0f
                ? 1f - TelegraphTimer / WofDirector.EyePotshotTelegraph
                : 0f;
            float scanGlow = 0f;
            if (WallOfFleshAI.TryGetWall(out NPC wall)) {
                (_, float eyeCharge) = WofWallField.ReadVisual(wall.whoAmI);
                scanGlow = eyeCharge;
            }
            float glow = Math.Max(telegraphGlow, scanGlow);
            if (glow <= 0.02f) {
                return false;
            }

            //血目充血：本体贴图加色重影
            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            Vector2 pos = npc.Center - screenPos + new Vector2(0, npc.gfxOffY);
            Vector2 orig = npc.frame.Size() / 2f;
            SpriteEffects effects = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Color bloodshot = new Color(255, 40, 30, 0) * (0.75f * glow);
            spriteBatch.Draw(tex, pos, npc.frame, bloodshot, npc.rotation, orig, npc.scale * (1f + glow * 0.05f), effects, 0f);

            //预告瞄准线：正在点射预告时指向锁定方向(分段端部包络，根/尾无平切)
            if (telegraphGlow > 0.05f && Main.player[npc.target].Alives()) {
                Player target = Main.player[npc.target];
                Vector2 aim = (target.Center + target.velocity * 9f - npc.Center).SafeNormalize(Vector2.UnitX * npc.direction);
                Color lineColor = new Color(255, 60, 40, 0) * (0.5f * telegraphGlow);
                WofMotionFX.DrawAimLine(spriteBatch, npc.Center + new Vector2(0, npc.gfxOffY), aim, 900f, 7f, lineColor);
            }

            //扫描充能的汇聚光点
            if (scanGlow > 0.05f) {
                Texture2D soft = CWRAsset.SoftGlow.Value;
                spriteBatch.Draw(soft, pos, null, new Color(255, 70, 50, 0) * (0.9f * scanGlow), 0f,
                    soft.Size() / 2f, 0.8f + scanGlow * 1.4f, SpriteEffects.None, 0f);
            }
            return false;
        }
        #endregion

        public override bool CheckActive() => false;
    }
}
