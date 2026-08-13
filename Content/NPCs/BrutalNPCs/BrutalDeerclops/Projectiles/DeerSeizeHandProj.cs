using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using InnoVault.GameSystem;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Projectiles
{
    /// <summary>
    /// 攫取巨手(投技专用，与掠袭影手 DeerShadowHandProj 独立)。
    /// ai[0]=目标玩家 ai[1]=起飞延时。全程无伤害判定——命中裁决在
    /// DeerclopsSeizeHuntState 服务端完成；本弹幕负责可读预兆与携抓视觉：
    /// 胸前成形蜷曲→红芒掌心→直线掠夺→(命中)化作握爪随节拍演出→砸雪后散作影雪
    /// </summary>
    internal class DeerSeizeHandProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.InsanityShadowHostile;

        internal const float SweepSpeed = 19f;
        internal const float MaxSweep = 1500f;
        private const int FadeTime = 16;

        private int TargetIndex => (int)Projectile.ai[0];
        private int LaunchDelay => Math.Max((int)Projectile.ai[1], 6);

        private ref float Elapsed => ref Projectile.localAI[0];
        /// <summary>0成形 1掠夺 2握持(命中后) 3消散</summary>
        private ref float Phase => ref Projectile.localAI[1];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = 62;
            Projectile.height = 62;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 700;
            Projectile.alpha = 255;
            Projectile.scale = 1.35f;
        }

        public override void AI() {
            Elapsed += 1f;
            int t = (int)Elapsed;

            //消散期：无论外界如何，安静散完(先于一切判断，防止回流握持分支重放音画)
            if (Phase == 3f) {
                FadeAI();
                return;
            }

            Player target = TargetIndex >= 0 && TargetIndex < Main.maxPlayers ? Main.player[TargetIndex] : null;

            //命中后进入握持：以被抓boss状态为准(各端观察同一同步事实)
            if (DeerclopsEyeGrabState.TryFindGrabbingDeer(TargetIndex, out NPC grabDeer, out DeerclopsEyeGrabState grabState)) {
                GripAI(grabDeer, grabState, target);
                return;
            }
            if (Phase == 2f) {
                //携抓结束(或被打断)：散作影雪
                Phase = 3f;
                if (Projectile.timeLeft > FadeTime) {
                    Projectile.timeLeft = FadeTime;
                }
                FadeAI();
                return;
            }

            //孤儿检查：亲代boss不在攫取流程或目标已死→消散(各端判据一致)
            if (!AnyHuntingDeer() || !target.Alives()) {
                Phase = 3f;
                if (Projectile.timeLeft > FadeTime) {
                    Projectile.timeLeft = FadeTime;
                }
                FadeAI();
                return;
            }

            bool launched = Projectile.velocity.LengthSquared() > 16f;
            if (!launched) {
                FormAI(t, target);
                //服务端到点起飞：锁定航线(带轻预判)，速度包即为各端权威
                if (!VaultUtils.isClient && t >= LaunchDelay) {
                    Vector2 aim = target.Center + target.velocity * 14f;
                    Projectile.velocity = (aim - Projectile.Center).SafeNormalize(Vector2.UnitX) * SweepSpeed;
                    Projectile.netUpdate = true;
                }
                return;
            }

            SweepAI(t);
        }

        #region 成形(蜷在胸前，红芒渐盛)
        private void FormAI(int t, Player target) {
            Phase = 0f;
            float p = MathHelper.Clamp(t / (float)LaunchDelay, 0f, 1f);
            Projectile.alpha = (int)MathHelper.Lerp(255f, 55f, MathHelper.Clamp(p * 1.7f, 0f, 1f));

            //锚在亲代胸前，蜷曲蓄势(晚爆后吸)
            NPC deer = FindHuntingDeer();
            if (deer != null) {
                int dir = deer.spriteDirection != 0 ? deer.spriteDirection : 1;
                Vector2 chest = deer.Bottom + new Vector2(dir * 62f, -92f) * deer.scale;
                float coil = (float)Math.Pow(p, 6) * 26f;
                Projectile.Center = chest - new Vector2(dir * coil, 0f);
            }
            //朝向目标(各端本地表现，最终航线以服务端速度包为准)
            if (target != null) {
                Projectile.rotation = (target.Center - Projectile.Center).ToRotation();
            }

            if (t == 2 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.NPCDeath6 with { Volume = 0.5f, Pitch = -0.8f, MaxInstances = 3 }, Projectile.Center);
            }
            //暗影渗出+指向目标的航线预示尘(本端)
            if (!Main.dedServ) {
                if (Main.rand.NextBool(3)) {
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(24f, 24f),
                        DustID.Shadowflame, Vector2.Zero, 140, default, Main.rand.NextFloat(1f, 1.7f));
                    dust.noGravity = true;
                }
                if (target != null && t % 4 == 0 && p > 0.4f) {
                    Vector2 lane = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                    Dust dust = Dust.NewDustPerfect(Projectile.Center + lane * Main.rand.NextFloat(30f, 90f),
                        DustID.Shadowflame, lane * Main.rand.NextFloat(2f, 5f), 160, default, 1.1f);
                    dust.noGravity = true;
                }
            }
        }
        #endregion

        #region 掠夺(直线扑抓)
        private void SweepAI(int t) {
            if (Phase == 0f) {
                Phase = 1f;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DD2_GhastlyGlaiveImpactGhost with { Volume = 0.9f, Pitch = -0.4f }, Projectile.Center);
                    for (int i = 0; i < 10; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame,
                            -Projectile.velocity.SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(2f, 6f)
                            + Main.rand.NextVector2Circular(1.5f, 1.5f), 130, default, Main.rand.NextFloat(1.1f, 1.8f));
                        dust.noGravity = true;
                    }
                }
            }

            Projectile.alpha = 35;
            Projectile.rotation = Projectile.velocity.ToRotation();

            //行程尽头：扑空，散去
            float traveled = (t - LaunchDelay) * SweepSpeed;
            if (traveled > MaxSweep && Projectile.timeLeft > FadeTime) {
                Phase = 3f;
                Projectile.timeLeft = FadeTime;
            }

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f),
                    DustID.Shadowflame, -Projectile.velocity * 0.08f, 150, default, Main.rand.NextFloat(0.9f, 1.5f));
                dust.noGravity = true;
            }
        }
        #endregion

        #region 握持(命中后随携抓节拍走位)
        private void GripAI(NPC deer, DeerclopsEyeGrabState grabState, Player target) {
            if (Phase != 2f) {
                Phase = 2f;
                Projectile.velocity = Vector2.Zero;
                Projectile.alpha = 30;
                //合拢一瞬的影爆(本端)，顺便掩盖各端命中时序的微小对不齐
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.NPCHit54 with { Volume = 0.8f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
                    for (int i = 0; i < 14; i++) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Shadowflame,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 7f), 120, default, Main.rand.NextFloat(1.2f, 2f));
                        dust.noGravity = true;
                    }
                }
            }

            int timer = grabState.Timer;
            //拖拽段贴着受害者走(与其客户端钉身轨迹天然对齐)；此后咬住爪锚
            if (timer <= DeerclopsEyeGrabState.DragEnd && target.Alives()) {
                Projectile.Center = target.Center;
            }
            else {
                Projectile.Center = DeerclopsEyeGrabState.ClawAnchor(deer, timer);
            }
            //握爪竖持，微微搏动
            int dir = deer.spriteDirection != 0 ? deer.spriteDirection : 1;
            Projectile.rotation = MathHelper.PiOver2 + dir * 0.35f + (float)Math.Sin(timer * 0.2f) * 0.05f;
            Projectile.timeLeft = Math.Max(Projectile.timeLeft, 60);

            //砸进雪地后开始消散
            if (timer >= DeerclopsEyeGrabState.SlamHit + 4 && Projectile.timeLeft > FadeTime) {
                Projectile.timeLeft = FadeTime;
                Phase = 3f;
            }
        }
        #endregion

        private void FadeAI() {
            Projectile.velocity *= 0.9f;
            Projectile.alpha = (int)MathHelper.Lerp(255f, 35f, Projectile.timeLeft / (float)FadeTime);
        }

        #region 亲代查询
        /// <summary>正处于攫取(SeizeHunt)且目标一致的独眼巨鹿</summary>
        private NPC FindHuntingDeer() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.Deerclops) {
                    continue;
                }
                if ((int)npc.ai[2] != (int)DeerclopsStateIndex.SeizeHunt || (int)npc.ai[1] - 1 != TargetIndex) {
                    continue;
                }
                if (!npc.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)
                    || !overrides.TryGetValue(typeof(DeerclopsAI), out NPCOverride deerOverride)
                    || deerOverride is not DeerclopsAI) {
                    continue;
                }
                return npc;
            }
            return null;
        }

        private bool AnyHuntingDeer() => FindHuntingDeer() != null;
        #endregion

        #region 绘制
        public override Color? GetAlpha(Color lightColor) {
            //暗影体不吃环境光，自发光
            return new Color(255, 255, 255, 255) * Projectile.Opacity;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.InsanityShadowHostile);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.InsanityShadowHostile].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;

            float rot = Projectile.rotation;
            SpriteEffects fx = SpriteEffects.None;
            if (Math.Cos(rot) < 0 && Phase != 2f) {
                rot += MathHelper.Pi;
                fx = SpriteEffects.FlipHorizontally;
            }

            //掠夺期拖影
            if (Phase == 1f) {
                for (int i = 1; i <= 5; i++) {
                    Vector2 ghostPos = drawPos - Projectile.velocity * (i * 0.6f);
                    Color ghostColor = DeerclopsMotion.ShadowViolet with { A = 0 } * (0.38f * (1f - i / 6f)) * Projectile.Opacity;
                    Main.EntitySpriteDraw(tex, ghostPos, null, ghostColor, rot, origin, Projectile.scale * (1f - i * 0.04f), fx, 0);
                }
            }

            //暗紫辉边(比掠袭影手更厚重，压出"巨物"轮廓)
            Color aura = DeerclopsMotion.ShadowViolet with { A = 0 } * (0.65f * Projectile.Opacity);
            Main.EntitySpriteDraw(tex, drawPos, null, aura, rot, origin, Projectile.scale * 1.16f, fx, 0);

            //本体
            Main.EntitySpriteDraw(tex, drawPos, null, Projectile.GetAlpha(lightColor), rot, origin, Projectile.scale, fx, 0);

            //掌心红瞳：成形期随蓄势涨大脉动，掠夺期定亮——投技的专属识别色
            if (Phase <= 1f) {
                float p = Phase == 1f ? 1f : MathHelper.Clamp(Elapsed / LaunchDelay, 0f, 1f);
                float pulse = 0.62f + 0.38f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 13f + Projectile.identity);
                Texture2D glow = CWRAsset.SoftGlow.Value;
                Texture2D glint = CWRAsset.StarGlow01.Value;
                Color warn = DeerclopsMotion.GazeRed with { A = 0 } * (p * pulse);
                Main.EntitySpriteDraw(glow, drawPos, null, warn, 0f, glow.Size() / 2f, 0.5f * (0.5f + p * 0.6f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glint, drawPos, null, warn * 0.8f, 0f, glint.Size() / 2f, 0.42f * p * pulse, SpriteEffects.None, 0);
            }
            return false;
        }
        #endregion

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //散作影与雪
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center,
                    Main.rand.NextBool(3) ? DustID.Snow : DustID.Shadowflame,
                    Main.rand.NextVector2Circular(3.5f, 3.5f), 130, default, Main.rand.NextFloat(1f, 1.7f));
                dust.noGravity = true;
            }
        }

        /// <summary>服务端生成攫取手：胸前成形，launchDelay 帧后起飞；返回弹幕索引，失败-1</summary>
        internal static int SpawnSeizeHand(NPC npc, int targetIndex, int launchDelay) {
            if (VaultUtils.isClient || targetIndex < 0 || targetIndex >= Main.maxPlayers) {
                return -1;
            }
            int dir = npc.spriteDirection != 0 ? npc.spriteDirection : 1;
            Vector2 chest = npc.Bottom + new Vector2(dir * 62f, -92f) * npc.scale;
            return Projectile.NewProjectile(npc.GetSource_FromAI(), chest, Vector2.Zero,
                ModContent.ProjectileType<DeerSeizeHandProj>(), 0, 0f, Main.myPlayer,
                targetIndex, launchDelay);
        }
    }
}
