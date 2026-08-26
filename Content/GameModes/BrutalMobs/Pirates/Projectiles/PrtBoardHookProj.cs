using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Pirates.Projectiles
{
    /// <summary>
    /// 劫掠钩索：单实体三相位。ai[0]=投手NPC索引 ai[1]=Pack(风味,档位)(0甲板水手/1掠夺者) ai[2]=相位(0预告/1外飞/2收回，服务端翻转)。<br/>
    /// 预告期钩子悬在投手肩上盘旋、瞄线自出生锁向（预告即承诺）；外飞期钩链可见淡入，
    /// 淡入未满前无判定（判定窗=完全显形窗）；钩中挂缓速；触墙或飞满射程转收回，收回全程无判定
    /// </summary>
    internal class PrtBoardHookProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Hook;

        //==== 公平阀门 ====
        /// <summary>预告帧数（≥30 帧契约）</summary>
        internal const int TelegraphFrames = 32;
        /// <summary>外飞淡入帧数：此窗口内钩链半透明且无判定（判定循环真正读取见 <see cref="CanDamage"/>）</summary>
        internal const int FadeInFrames = 12;
        /// <summary>钩索最大射程（外飞行程封顶，瞄线预演画到哪钩就最多飞到哪）</summary>
        internal const float HookRange = 430f;

        /// <summary>钩速（风味 0 甲板水手 / 1 掠夺者）</summary>
        internal static readonly float[] HookSpeedByFlavor = [10.5f, 13f];
        /// <summary>命中缓速时长（档位 1/2/3）</summary>
        private static readonly int[] SlowTicksByTier = [90, 120, 150];
        /// <summary>收回提速倍率</summary>
        private const float RetractSpeedMult = 1.6f;
        /// <summary>收回到手判定距离</summary>
        private const float RetractArriveDist = 22f;
        /// <summary>收回兜底寿命（投手丢失时防止悬空钩滞留）</summary>
        private const int RetractMaxFrames = 90;

        private const byte PhaseTelegraph = 0;
        private const byte PhaseFlight = 1;
        private const byte PhaseRetract = 2;

        private static readonly Color RopeBrown = new Color(196, 150, 96);

        private int ShooterIndex => (int)Projectile.ai[0];
        private int Flavor => (int)Projectile.ai[1] % 2;
        private int Tier => Math.Clamp((int)Projectile.ai[1] / 2, 1, 3);
        private byte Phase => (byte)Projectile.ai[2];
        private int ExpectedShooterType => Flavor == 1 ? NPCID.PirateCorsair : NPCID.PirateDeckhand;

        internal static float Pack(int flavor, int tier) => flavor + tier * 2;

        private ref float Age => ref Projectile.localAI[0];
        /// <summary>相位内帧龄（各端本地推进：外飞期驱动淡入，收回期驱动兜底寿命）</summary>
        private ref float PhaseAge => ref Projectile.localAI[1];

        private byte prevPhase;
        private bool phaseInit;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 400;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => Phase == PhaseFlight;

        /// <summary>判定窗=完全显形窗：只有外飞期且淡入结束后有杀伤；预告与收回永不判定</summary>
        public override bool? CanDamage() => Phase == PhaseFlight && PhaseAge > FadeInFrames ? null : false;

        public override void AI() {
            Age++;

            //相位沿检测（各端本地）：跨相位时重置相位帧龄并补表现；
            //迟入端首帧即处于后段相位（未目击沿），跳过音效防错播
            if (!phaseInit) {
                phaseInit = true;
                prevPhase = Phase;
            }
            else if (prevPhase != Phase) {
                bool witnessed = Age > 1f;
                prevPhase = Phase;
                PhaseAge = 0f;
                if (witnessed && !Main.dedServ) {
                    if (Phase == PhaseFlight) {
                        SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = -0.4f, MaxInstances = 5 }, Projectile.Center);
                    }
                    else if (Phase == PhaseRetract) {
                        SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 5 }, Projectile.Center);
                    }
                }
            }
            PhaseAge++;

            bool shooterValid = ShooterIndex.TryGetNPC(out NPC shooter) && shooter.type == ExpectedShooterType;

            switch (Phase) {
                case PhaseTelegraph: {
                    if (!VaultUtils.isClient) {
                        if (!shooterValid) {
                            //投手没了：这一钩不会发生，预告消散
                            Projectile.Kill();
                            return;
                        }
                        if (Age >= TelegraphFrames) {
                            //出手：从手部沿承诺方向出膛（方向即出生 velocity，全程未重瞄）
                            Projectile.Center = HandPos(shooter);
                            Projectile.ai[2] = PhaseFlight;
                            Projectile.netUpdate = true;
                            return;
                        }
                    }
                    //钩子悬在投手肩上盘旋蓄势（锚定绘制走 HandPos，含 gfxOffY）
                    if (shooterValid) {
                        Projectile.Center = HandPos(shooter);
                    }
                    Projectile.rotation += 0.34f;
                    if (!Main.dedServ && Main.rand.NextBool(3)) {
                        Dust dust = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                            DustID.Iron, Vector2.Zero, 100, default, 0.8f);
                        dust.noGravity = true;
                        dust.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 0.8f;
                    }
                    break;
                }
                case PhaseFlight: {
                    Projectile.tileCollide = true;
                    Projectile.rotation = Projectile.velocity.ToRotation();
                    //射程封顶：飞满预演线长度即转收回（服务端权威翻转）
                    if (!VaultUtils.isClient
                        && PhaseAge * HookSpeedByFlavor[Flavor] >= HookRange) {
                        BeginRetract();
                    }
                    if (!Main.dedServ && Main.rand.NextBool(5)) {
                        Dust rope = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke,
                            -Projectile.velocity * 0.05f, 160, default, 0.6f);
                        rope.noGravity = true;
                    }
                    break;
                }
                default: {
                    //收回：各端朝投手手部本地追踪（表现），服务端权威收尾
                    Projectile.tileCollide = false;
                    Projectile.rotation += 0.2f;
                    if (shooterValid) {
                        Vector2 toHand = HandPos(shooter) - Projectile.Center;
                        float dist = toHand.Length();
                        if (!VaultUtils.isClient && (dist < RetractArriveDist || PhaseAge > RetractMaxFrames)) {
                            Projectile.Kill();
                            return;
                        }
                        Projectile.Center += toHand.SafeNormalize(Vector2.Zero)
                            * Math.Min(dist, HookSpeedByFlavor[Flavor] * RetractSpeedMult);
                    }
                    else if (!VaultUtils.isClient && PhaseAge > 18f) {
                        //投手已死：钩索失主，短暂滞空后消散
                        Projectile.Kill();
                        return;
                    }
                    break;
                }
            }

            Lighting.AddLight(Projectile.Center, RopeBrown.ToVector3() * 0.1f);
        }

        /// <summary>投手手部锚点（含 gfxOffY 上坡步进补偿）</summary>
        private static Vector2 HandPos(NPC shooter)
            => shooter.Center + new Vector2(shooter.direction * 12f, shooter.gfxOffY - 14f);

        private void BeginRetract() {
            Projectile.ai[2] = PhaseRetract;
            Projectile.velocity = Vector2.Zero;
            Projectile.netUpdate = true;
        }

        /// <summary>触墙不碎：转收回（一次性的、无判定的空钩收线）</summary>
        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Phase == PhaseFlight) {
                if (!VaultUtils.isClient) {
                    BeginRetract();
                }
                Projectile.velocity = Vector2.Zero;
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //命中方本机结算，减益原生同步
            target.AddBuff(BuffID.Slow, SlowTicksByTier[Tier - 1]);
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = -0.2f }, target.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D hookTex = TextureAssets.Projectile[Type].Value;
            Vector2 hookOrigin = hookTex.Size() * 0.5f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            bool shooterValid = ShooterIndex.TryGetNPC(out NPC shooter) && shooter.type == ExpectedShooterType;

            //淡入无判定期：可见度与判定门读同一时间轴（PhaseAge/FadeInFrames）
            float visibility = Phase switch {
                PhaseTelegraph => MathHelper.Clamp(Age / 8f, 0f, 1f) * 0.8f,
                PhaseFlight => MathHelper.Lerp(0.35f, 1f, MathHelper.Clamp(PhaseAge / FadeInFrames, 0f, 1f)),
                _ => 0.55f,
            };

            if (Phase == PhaseTelegraph) {
                //瞄线预演：自钩子沿承诺方向的细线（出生锁向，画的就是要飞的）
                Texture2D line = CWRAsset.MaskLaserLine.Value;
                float urgency = MathHelper.Clamp(Age / TelegraphFrames, 0f, 1f);
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity);
                Main.EntitySpriteDraw(line, drawPos, null,
                    RopeBrown with { A = 0 } * (visibility * (0.25f + 0.4f * urgency) * pulse),
                    Projectile.velocity.ToRotation(), new Vector2(0f, line.Height / 2f),
                    new Vector2(HookRange / line.Width, 10f / line.Height), SpriteEffects.None, 0);
            }
            else if (shooterValid) {
                //钩链：Chain22 分段铺设，透明度跟淡入窗同轴
                Texture2D chainTex = TextureAssets.Chain22?.Value;
                if (chainTex != null) {
                    Vector2 hand = HandPos(shooter);
                    Vector2 span = Projectile.Center - hand;
                    float length = span.Length();
                    if (length > 8f) {
                        Vector2 dir = span / length;
                        float chainRot = dir.ToRotation() + MathHelper.PiOver2;
                        int links = (int)(length / chainTex.Height) + 1;
                        for (int i = 0; i < links; i++) {
                            Vector2 linkPos = hand + dir * chainTex.Height * i;
                            Color linkColor = Lighting.GetColor((int)(linkPos.X / 16f), (int)(linkPos.Y / 16f));
                            Main.EntitySpriteDraw(chainTex, linkPos - Main.screenPosition, null,
                                linkColor * visibility, chainRot,
                                new Vector2(chainTex.Width / 2f, 0f), 1f, SpriteEffects.None, 0);
                        }
                    }
                }
            }

            //钩体：原版钩爪贴图（真 alpha 实体），淡入期半透明
            Main.EntitySpriteDraw(hookTex, drawPos, null, lightColor * visibility,
                Projectile.rotation, hookOrigin, 1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
