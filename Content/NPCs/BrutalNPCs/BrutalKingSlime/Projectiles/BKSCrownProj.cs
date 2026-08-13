using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>
    /// 史莱姆王之冠，独立威胁单元。ai[0]=宿主whoAmI ai[1]=模式 ai[2]=锁定X<br/>
    /// 模式：0升空 1瞄准悬停 2天坠 3嵌地 4回归 5常驻悬浮(P2) 6审判指挥 7死亡坠地(纯演出)<br/>
    /// 模式切换服务端驱动；各端按模式+本地计时演出
    /// </summary>
    internal class BKSCrownProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int ModeLaunch = 0;
        internal const int ModeTelegraph = 1;
        internal const int ModeSlam = 2;
        internal const int ModeLanded = 3;
        internal const int ModeReturn = 4;
        internal const int ModeHover = 5;
        internal const int ModeDecree = 6;
        internal const int ModeDeathDrop = 7;

        private const int LaunchTime = 24;
        private const int TelegraphTime = 34;
        private const int LandedTime = 34;

        private NPC Host => (int)Projectile.ai[0] >= 0 && (int)Projectile.ai[0] < Main.maxNPCs
            ? Main.npc[(int)Projectile.ai[0]] : null;

        private int Mode => (int)Projectile.ai[1];

        private ref float ModeTimer => ref Projectile.localAI[0];
        private ref float PrevMode => ref Projectile.localAI[1];
        /// <summary>首帧入场表现已播(本地)</summary>
        private bool enterInit;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = 46;
            Projectile.height = 32;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        /// <summary>只有天坠与嵌地前几帧有伤害</summary>
        public override bool? CanDamage() {
            if (Mode == ModeSlam) {
                return null;
            }
            if (Mode == ModeLanded && ModeTimer < 8f) {
                return null;
            }
            return false;
        }

        public override void AI() {
            NPC host = Host;
            bool hostValid = host != null && host.active && host.type == NPCID.KingSlime;

            //宿主消失：演出坠地模式继续，其余直接消失
            if (!hostValid && Mode != ModeDeathDrop) {
                Projectile.Kill();
                return;
            }

            //模式切换检测：重置本地模式计时；首帧也要播入场表现
            if (!enterInit || PrevMode != Projectile.ai[1]) {
                enterInit = true;
                PrevMode = Projectile.ai[1];
                ModeTimer = 0f;
                OnModeEnter();
            }
            ModeTimer++;

            //常驻模式刷新寿命
            Projectile.timeLeft = Math.Max(Projectile.timeLeft, 60);

            switch (Mode) {
                case ModeLaunch: UpdateLaunch(host); break;
                case ModeTelegraph: UpdateTelegraph(host); break;
                case ModeSlam: UpdateSlam(); break;
                case ModeLanded: UpdateLanded(host); break;
                case ModeReturn: UpdateReturn(host); break;
                case ModeHover: UpdateHover(host); break;
                case ModeDecree: UpdateDecree(host); break;
                case ModeDeathDrop: UpdateDeathDrop(); break;
            }

            Lighting.AddLight(Projectile.Center, KingSlimeGelFX.CrownGold.ToVector3() * 0.5f);
        }

        /// <summary>模式进入时的一次性表现，各端本地播</summary>
        private void OnModeEnter() {
            switch (Mode) {
                case ModeLaunch:
                    KingSlimeGelFX.CrownChime(Projectile.Center, 0.3f, 0.9f);
                    KingSlimeGelFX.GoldGlint(Projectile.Center, 8, 5f);
                    break;
                case ModeSlam:
                    SoundEngine.PlaySound(SoundID.Item66 with { Pitch = -0.35f, Volume = 0.9f, MaxInstances = 3 }, Projectile.Center);
                    break;
                case ModeLanded:
                    //落地重响+金屑+震屏，冲击波由服务端生成
                    KingSlimeGelFX.ThudSound(Projectile.Center, 18f);
                    KingSlimeGelFX.CrownChime(Projectile.Center, -0.25f, 1f);
                    KingSlimeGelFX.GoldGlint(Projectile.Center, 22, 9f);
                    KingSlimeGelFX.CameraPunch(Projectile.Center, 7.5f, 16, "BKSCrownSlam", Vector2.UnitY);
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                            ModContent.ProjectileType<BKSShockwaveProj>(), 0, 0f, Main.myPlayer, 1f);
                    }
                    break;
                case ModeDecree:
                    KingSlimeGelFX.CrownChime(Projectile.Center, 0.45f, 1f);
                    break;
            }
        }

        private void UpdateLaunch(NPC host) {
            //从宿主头顶弹射升空，快出慢收
            float t = MathHelper.Clamp(ModeTimer / LaunchTime, 0f, 1f);
            float speed = MathHelper.Lerp(26f, 4f, VaultUtils.EaseOutCubic(t));
            Projectile.velocity = new Vector2(0f, -speed);
            Projectile.rotation += 0.2f * (1f - t);

            //服务端调度：天坠态转瞄准，其余(阶段转换脱冕)直接入常驻悬浮
            if (!VaultUtils.isClient && ModeTimer >= LaunchTime) {
                bool slamming = (int)host.ai[2] == (int)KingSlimeStateIndex.CrownSlam;
                SetMode(slamming ? ModeTelegraph : ModeHover);
            }
        }

        private void UpdateTelegraph(NPC host) {
            Player target = Main.player[host.target];
            if (!target.Alives()) {
                if (!VaultUtils.isClient) {
                    SetMode(ModeReturn);
                }
                return;
            }

            //横向缓追目标，保持高位
            float hoverY = target.Center.Y - 400f;
            Vector2 desired = new Vector2(target.Center.X + target.velocity.X * 14f, hoverY);
            Projectile.velocity = (desired - Projectile.Center) * 0.08f;
            if (Projectile.velocity.Length() > 22f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 22f;
            }
            //旋转回正
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.2f);

            //向心金屑，蓄势可读
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(70f, 70f);
                InnoVault.PRT.PRTLoader.NewParticle<PRT_BKSGoldSpark>(from,
                    (Projectile.Center - from) * 0.1f, KingSlimeGelFX.CrownGold, Main.rand.NextFloat(0.8f, 1.3f))
                    ?.Configure(14);
            }

            //服务端：锁定X后天坠
            if (!VaultUtils.isClient && ModeTimer >= TelegraphTime) {
                Projectile.ai[2] = target.Center.X + target.velocity.X * 10f;
                SetMode(ModeSlam);
            }
        }

        private void UpdateSlam() {
            //锁X直坠，复合加速
            Projectile.Center = new Vector2(MathHelper.Lerp(Projectile.Center.X, Projectile.ai[2], 0.2f), Projectile.Center.Y);
            float vy = Math.Max(Projectile.velocity.Y, 4f) * 1.11f;
            Projectile.velocity = new Vector2(0f, Math.Min(vy, 46f));
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.3f);

            //坠落金线拖尾
            if (!VaultUtils.isServer && Main.rand.NextBool()) {
                InnoVault.PRT.PRTLoader.NewParticle<PRT_BKSGoldSpark>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 6f),
                    new Vector2(0f, -Main.rand.NextFloat(1f, 3f)), KingSlimeGelFX.CrownGold, Main.rand.NextFloat(0.9f, 1.5f))
                    ?.Configure(Main.rand.Next(10, 18));
            }

            //触地判定(全端同规则，落点由锁定X+地形唯一确定)
            Vector2 ground = KingSlimeGelFX.FindGroundBelow(new Vector2(Projectile.ai[2], Projectile.Center.Y), 10);
            if (Projectile.Center.Y + 14f >= ground.Y || Collision.SolidCollision(Projectile.position, Projectile.width, Projectile.height)) {
                Projectile.Center = new Vector2(Projectile.ai[2], ground.Y - 12f);
                Projectile.velocity = Vector2.Zero;
                if (!VaultUtils.isClient) {
                    SetMode(ModeLanded);
                }
            }
        }

        private void UpdateLanded(NPC host) {
            Projectile.velocity = Vector2.Zero;
            //嵌地微颤
            if (ModeTimer < 10f) {
                Projectile.rotation = (float)Math.Sin(ModeTimer * 1.4f) * 0.12f * (1f - ModeTimer / 10f);
            }

            if (!VaultUtils.isClient && ModeTimer >= LandedTime) {
                SetMode(ModeReturn);
            }
        }

        private void UpdateReturn(NPC host) {
            //加速飞回宿主头顶
            Vector2 dest = host.Top + new Vector2(0f, -26f);
            float speed = MathHelper.Clamp(4f + ModeTimer * 0.9f, 4f, 34f);
            Projectile.velocity = (dest - Projectile.Center).SafeNormalize(Vector2.Zero) * speed;
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.25f);

            if (Projectile.Distance(dest) < 40f) {
                //P2王冠常驻悬浮，P1收回消失
                bool phase2 = host.GetOverride<KingSlimeAI>()?.ai[3] == 1f;
                if (phase2) {
                    if (!VaultUtils.isClient) {
                        SetMode(ModeHover);
                    }
                }
                else {
                    KingSlimeGelFX.GoldGlint(Projectile.Center, 10, 5f);
                    KingSlimeGelFX.CrownChime(Projectile.Center, 0.1f, 0.7f);
                    if (!VaultUtils.isClient) {
                        Projectile.Kill();
                    }
                }
            }
        }

        private void UpdateHover(NPC host) {
            //头顶悬浮呼吸
            float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.4f) * 10f;
            Vector2 dest = host.Top + new Vector2(0f, -44f + bob);
            Projectile.velocity = (dest - Projectile.Center) * 0.1f;
            Projectile.rotation = Projectile.rotation.AngleLerp((float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.7f) * 0.08f, 0.1f);

            if (!VaultUtils.isServer && Main.rand.NextBool(40)) {
                KingSlimeGelFX.GoldGlint(Projectile.Center + Main.rand.NextVector2Circular(16f, 8f), 1, 2f);
            }

            //服务端调度：宿主进入王冠天坠态则响应
            if (!VaultUtils.isClient) {
                int hostState = (int)host.ai[2];
                if (hostState == (int)KingSlimeStateIndex.CrownSlam) {
                    SetMode(ModeLaunch);
                }
                else if (hostState == (int)KingSlimeStateIndex.RoyalDecree) {
                    SetMode(ModeDecree);
                }
            }
        }

        private void UpdateDecree(NPC host) {
            //升至战场上空中央放光
            Vector2 dest = host.Center + new Vector2(0f, -430f);
            Projectile.velocity = (dest - Projectile.Center) * 0.06f;
            Projectile.rotation = Projectile.rotation.AngleLerp(0f, 0.15f);

            if (!VaultUtils.isServer) {
                if (Main.rand.NextBool(2)) {
                    Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(120f, 120f);
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_BKSGoldSpark>(from,
                        (Projectile.Center - from) * 0.08f, KingSlimeGelFX.CrownGold, Main.rand.NextFloat(1f, 1.6f))
                        ?.Configure(18);
                }
            }
            Lighting.AddLight(Projectile.Center, KingSlimeGelFX.CrownGold.ToVector3() * 1.2f);

            //服务端调度：宿主离开审判态则回归悬浮
            if (!VaultUtils.isClient && (int)host.ai[2] != (int)KingSlimeStateIndex.RoyalDecree) {
                SetMode(ModeHover);
            }
        }

        private void UpdateDeathDrop() {
            //纯演出坠地：重力+两次弹跳
            Projectile.velocity.Y += 0.5f;
            if (Projectile.velocity.Y > 18f) {
                Projectile.velocity.Y = 18f;
            }
            Projectile.rotation += Projectile.velocity.X * 0.04f;
            Projectile.velocity.X *= 0.99f;

            if (Collision.SolidCollision(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height)
                && Projectile.velocity.Y > 0f) {
                if (Projectile.velocity.Y > 4f && Projectile.localAI[2] < 2f) {
                    //弹跳
                    Projectile.localAI[2]++;
                    Projectile.velocity.Y *= -0.42f;
                    Projectile.velocity.X *= 0.6f;
                    KingSlimeGelFX.CrownChime(Projectile.Center, -0.1f - Projectile.localAI[2] * 0.15f, 0.8f);
                    KingSlimeGelFX.GoldGlint(Projectile.Bottom, 6, 4f);
                }
                else {
                    Projectile.velocity = Vector2.Zero;
                }
            }

            if (ModeTimer > 340f) {
                Projectile.alpha += 6;
                if (Projectile.alpha >= 255) {
                    Projectile.Kill();
                }
            }
        }

        /// <summary>服务端切换模式并同步</summary>
        private void SetMode(int mode) {
            Projectile.ai[1] = mode;
            Projectile.netUpdate = true;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadGore(GoreID.KingSlimeCrown);
            Texture2D crown = TextureAssets.Gore[GoreID.KingSlimeCrown].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = crown.Size() * 0.5f;
            float fade = 1f - Projectile.alpha / 255f;

            //瞄准/天坠期的金色指引与拖影
            if (Mode == ModeTelegraph || Mode == ModeSlam) {
                DrawGuideColumn(pos, fade);
            }
            if (Mode == ModeSlam) {
                for (int i = 1; i <= 4; i++) {
                    Vector2 ghost = pos - new Vector2(0f, Projectile.velocity.Y * i * 0.7f) * -1f;
                    Main.EntitySpriteDraw(crown, ghost, null,
                        KingSlimeGelFX.CrownGold with { A = 0 } * (0.3f - i * 0.06f) * fade,
                        Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
                }
            }

            //审判模式的辉光星冠
            if (Mode == ModeDecree) {
                Texture2D star = CWRAsset.StarTexture.Value;
                Texture2D glowTex = CWRAsset.DiffusionCircle.Value;
                float pulse = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 9f);
                Main.EntitySpriteDraw(glowTex, pos, null, KingSlimeGelFX.CrownGold with { A = 0 } * 0.7f, 0f,
                    glowTex.Size() * 0.5f, 1.5f * pulse, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(star, pos, null, KingSlimeGelFX.CrownGold with { A = 0 } * 0.9f,
                    Main.GlobalTimeWrappedHourly * 2.2f, star.Size() * 0.5f, 1.1f * pulse, SpriteEffects.None, 0);
            }

            //本体
            Color light = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            Main.EntitySpriteDraw(crown, pos, null, light * fade, Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            //金属泽光
            Main.EntitySpriteDraw(crown, pos, null, KingSlimeGelFX.CrownGold with { A = 0 } * (0.35f * fade),
                Projectile.rotation, origin, 1.03f, SpriteEffects.None, 0);
            return false;
        }

        /// <summary>瞄准指引：王冠到地面的细金柱，脉动</summary>
        private void DrawGuideColumn(Vector2 screenPos, float fade) {
            Texture2D pixel = InnoVault.VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            float lockX = Mode == ModeSlam ? Projectile.ai[2] : Projectile.Center.X;
            Vector2 ground = KingSlimeGelFX.FindGroundBelow(new Vector2(lockX, Projectile.Center.Y), 90);
            float height = ground.Y - Projectile.Center.Y;
            if (height < 40f) {
                return;
            }
            float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 14f);
            float alpha = (Mode == ModeSlam ? 0.5f : 0.22f + pulse * 0.14f) * fade;
            Vector2 top = new Vector2(lockX, Projectile.Center.Y) - Main.screenPosition;
            //细芯+宽晕两层
            Main.spriteBatch.Draw(pixel, top, null, KingSlimeGelFX.CrownGold with { A = 0 } * alpha, 0f,
                new Vector2(pixel.Width * 0.5f, 0f), new Vector2(3f / pixel.Width, height / pixel.Height), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(pixel, top, null, KingSlimeGelFX.CrownAmber with { A = 0 } * (alpha * 0.4f), 0f,
                new Vector2(pixel.Width * 0.5f, 0f), new Vector2(14f / pixel.Width, height / pixel.Height), SpriteEffects.None, 0f);
        }
    }
}
