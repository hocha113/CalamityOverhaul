using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode
{
    /// <summary>
    /// 【神圣套·圣辉审判 ★A】圣光所铸的裁决之甲：①命中积攒圣辉，满十层头顶升起审判圣剑
    /// ②圣剑悬空八秒，期间连续命中同一目标三次即完成锁定 ③锁定成时圣剑仰身蓄势、俯冲处决，
    /// 命中炸开圣辉光爆并留下一柱余辉。换击目标则锁定重计；受击崩两层圣辉。
    /// 原版套装奖励（神圣防护）保留，神赋叠加；普通件与远古件全组合互认
    /// </summary>
    internal class GsHallowedArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [
            ItemID.HallowedHelmet, ItemID.HallowedHeadgear, ItemID.HallowedMask, ItemID.HallowedHood,
            ItemID.AncientHallowedHelmet, ItemID.AncientHallowedHeadgear, ItemID.AncientHallowedMask, ItemID.AncientHallowedHood,
        ];

        public override int BodyID => ItemID.HallowedPlateMail;

        public override int LegsID => ItemID.HallowedGreaves;

        protected override string EndowLineFallback =>
            "Radiant Verdict: strikes build radiance; at 10 stacks a verdict blade rises overhead, and striking one foe three times sends it plunging in holy judgement";

        //圣辉色板
        internal static readonly Color HallowGold = new(255, 216, 130);
        internal static readonly Color HallowWhite = new(255, 250, 236);
        internal static readonly Color HallowDeep = new(168, 122, 50);
        internal static readonly Color HallowPink = new(255, 176, 222);

        protected override int FullCharge => 10;

        protected override Color ThemeMain => HallowGold;

        protected override Color ThemeBright => HallowWhite;

        /// <summary>处决锁定所需连击数</summary>
        private const int LockHits = 3;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsHallowedVerdictBladeProj>();

        private static Projectile FindBlade(Player player) {
            int type = ModContent.ProjectileType<GsHallowedVerdictBladeProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type && proj.ai[0] == 0f) {
                    return proj;
                }
            }
            return null;
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            if (sourceProj != null && IsOwnProc(sourceProj)) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            Projectile blade = FindBlade(player);
            if (blade == null) {
                base.OnEndowHitNPC(player, state, target, hit, damageDone, sourceProj);
                return;
            }

            //剑悬期：锁定计数（佩戴者端持剑权威）
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if ((int)blade.ai[1] != target.whoAmI) {
                //换目标重计
                blade.ai[1] = target.whoAmI;
                blade.ai[2] = 1f;
            }
            else {
                blade.ai[2]++;
            }
            if (!VaultUtils.isServer) {
                //锁定节拍音，随计数升调
                SoundEngine.PlaySound(SoundID.Item29 with {
                    Volume = 0.4f, Pitch = 0.1f + 0.25f * blade.ai[2], MaxInstances = 3
                }, target.Center);
            }
            if (blade.ai[2] >= LockHits) {
                //锁定成：号令俯冲，处决伤害以本击折算
                blade.ai[0] = 1f;
                blade.damage = Math.Clamp((int)(damageDone * 1.2f), 40, 320);
            }
            blade.netUpdate = true;
        }

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.85f, Pitch = -0.1f }, player.Center);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = 0.4f }, player.Center);
                //升剑演出：圣辉自地涌天
                for (int i = 0; i < 12; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(player.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(-10f, 30f)),
                        -Vector2.UnitY * Main.rand.NextFloat(1.5f, 3.5f),
                        i % 3 == 0 ? HallowPink : HallowGold, Main.rand.NextFloat(0.45f, 0.7f))
                        ?.Configure(HallowGold, Main.rand.Next(20, 32), 0.1f, 0.9f);
                }
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithHallowedEndow"),
                player.Center - new Vector2(0f, 90f), Vector2.Zero,
                ModContent.ProjectileType<GsHallowedVerdictBladeProj>(),
                40, 6f, player.whoAmI, 0f, -1f, 0f);
        }
    }

    /// <summary>远古胸甲组合（远古件与普通件互认，套装奖励同源）</summary>
    internal class GsHallowedArmorAncientBody : GsHallowedArmor
    {
        public override int BodyID => ItemID.AncientHallowedPlateMail;
    }

    /// <summary>远古护腿组合</summary>
    internal class GsHallowedArmorAncientLegs : GsHallowedArmor
    {
        public override int LegsID => ItemID.AncientHallowedGreaves;
    }

    /// <summary>远古胸甲 + 远古护腿组合</summary>
    internal class GsHallowedArmorAncientFull : GsHallowedArmorAncientBody
    {
        public override int LegsID => ItemID.AncientHallowedGreaves;
    }

    /// <summary>
    /// 审判圣剑：圣辉凝成的巨剑，剑尖向下悬于佩戴者头顶，剑侧圣环缓旋、
    /// 柄侧锁定星标随连击点亮；锁定成时仰身蓄势、俯冲处决，命中炸开圣辉光爆并留下余辉光柱
    /// </summary>
    internal class GsHallowedVerdictBladeProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "LightShot";

        /// <summary>0=悬空 1=蓄势俯冲 2=余辉光柱</summary>
        private ref float State => ref Projectile.ai[0];

        private ref float LockIndex => ref Projectile.ai[1];

        /// <summary>锁定星标数（0~3）</summary>
        private ref float LockPips => ref Projectile.ai[2];

        private ref float Life => ref Projectile.localAI[0];

        /// <summary>俯冲已进行帧数</summary>
        private ref float DiveTime => ref Projectile.localAI[1];

        private float Seed => Projectile.identity * 0.6089f % 2.93f;

        /// <summary>蓄势帧数（仰身聚光）</summary>
        private const int WindupFrames = 11;

        /// <summary>余辉光柱时长</summary>
        private const int PillarFrames = 30;

        private float VisualFade => MathHelper.Clamp(Life / 12f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 60;
        }

        /// <summary>只在俯冲段判定</summary>
        public override bool? CanDamage() => State == 1f && DiveTime > WindupFrames;

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (State == 0f) {
                //方案切走圣剑散光
                if (owner.GetModPlayer<GodSmithArmorPlayer>().ActiveScheme is not GsHallowedArmor) {
                    if (Projectile.owner == Main.myPlayer) {
                        Projectile.Kill();
                    }
                    return;
                }
                //悬空：头顶浮沉，剑尖向下，向锁定目标微倾
                Vector2 anchor = owner.Center + new Vector2(0f, -74f + MathF.Sin(Life * 0.05f + Seed) * 6f);
                Projectile.Center = Vector2.Lerp(Projectile.Center, anchor, 0.16f);
                Projectile.velocity = Vector2.Zero;
                float tilt = 0f;
                NPC locked = LockIndex >= 0 && LockIndex < Main.maxNPCs ? Main.npc[(int)LockIndex] : null;
                if (locked != null && locked.active && LockPips > 0f) {
                    tilt = MathHelper.Clamp((locked.Center.X - Projectile.Center.X) * 0.0008f, -0.3f, 0.3f);
                }
                //剑尖向下 = PiOver2
                Projectile.rotation = MathHelper.PiOver2 + tilt;
                Lighting.AddLight(Projectile.Center, GsHallowedArmor.HallowGold.ToVector3() * 0.32f);
                if (!Main.dedServ && Main.rand.NextBool(14)) {
                    PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(16f, 30f),
                        -Vector2.UnitY * 0.5f, GsHallowedArmor.HallowWhite, Main.rand.NextFloat(0.35f, 0.5f))
                        ?.Configure(GsHallowedArmor.HallowGold, 18, 0.06f, 0.7f);
                }
                return;
            }

            if (State == 1f) {
                DiveTime++;
                NPC target = LockIndex >= 0 && LockIndex < Main.maxNPCs ? Main.npc[(int)LockIndex] : null;
                if (DiveTime <= WindupFrames) {
                    //蓄势：仰身升高、聚光
                    Projectile.velocity = Vector2.Zero;
                    Projectile.Center += new Vector2(0f, -1.8f);
                    if (target != null && target.active) {
                        float aim = (target.Center - Projectile.Center).ToRotation();
                        //剑身先反向仰起再压向目标
                        Projectile.rotation = MathHelper.Lerp(Projectile.rotation, aim - 0.5f, 0.2f);
                    }
                    if (!Main.dedServ && DiveTime == WindupFrames) {
                        SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = 0.8f }, Projectile.Center);
                    }
                    return;
                }
                //俯冲：首帧佩戴者端定向同步
                if (DiveTime == WindupFrames + 1 && Projectile.owner == Main.myPlayer) {
                    Vector2 aimPos = target != null && target.active
                        ? target.Center + target.velocity * 5f
                        : Projectile.Center + new Vector2(0f, 300f);
                    Projectile.velocity = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitY) * 15f;
                    Projectile.netUpdate = true;
                }
                Projectile.velocity *= 1.09f;
                if (Projectile.velocity.Length() > 34f) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * 34f;
                }
                Projectile.rotation = Projectile.velocity.ToRotation();
                if (!Main.dedServ) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.5f,
                        Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                        Main.rand.NextBool() ? GsHallowedArmor.HallowWhite : GsHallowedArmor.HallowGold,
                        Main.rand.NextFloat(0.28f, 0.44f))?.Configure(false, Main.rand.Next(8, 14));
                }
                Lighting.AddLight(Projectile.Center, GsHallowedArmor.HallowWhite.ToVector3() * 0.5f);
                //俯冲超时未中：空掷谢幕
                if (DiveTime > WindupFrames + 40) {
                    EnterPillar();
                }
                return;
            }

            //余辉光柱：驻定燃尽
            Projectile.velocity = Vector2.Zero;
            if (Projectile.timeLeft > PillarFrames) {
                Projectile.timeLeft = PillarFrames;
            }
            if (!Main.dedServ && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + new Vector2(Main.rand.NextFloat(-22f, 22f), Main.rand.NextFloat(-10f, 10f)),
                    -Vector2.UnitY * Main.rand.NextFloat(1.5f, 4f),
                    Main.rand.NextBool(3) ? GsHallowedArmor.HallowPink : GsHallowedArmor.HallowGold,
                    Main.rand.NextFloat(0.35f, 0.55f))?.Configure(GsHallowedArmor.HallowGold, Main.rand.Next(16, 26), 0.08f, 0.7f);
            }
            Lighting.AddLight(Projectile.Center, GsHallowedArmor.HallowWhite.ToVector3() * (0.6f * Projectile.timeLeft / PillarFrames));
        }

        private void EnterPillar() {
            State = 2f;
            Projectile.velocity = Vector2.Zero;
            Projectile.timeLeft = PillarFrames;
            if (Projectile.owner == Main.myPlayer) {
                Projectile.netUpdate = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (State != 1f) {
                return;
            }
            //处决着弹：圣辉光爆 + 入余辉光柱
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.55f, Pitch = 0.5f }, target.Center);
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.8f, Pitch = -0.2f }, target.Center);
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                    GsHallowedArmor.HallowWhite, 0.3f)?.Configure(12, 0.9f);
                for (int i = 0; i < 14; i++) {
                    float ang = MathHelper.TwoPi * i / 14f;
                    PRTLoader.NewParticle<PRT_Sparkle>(target.Center,
                        ang.ToRotationVector2() * Main.rand.NextFloat(3f, 7f),
                        i % 3 == 0 ? GsHallowedArmor.HallowPink : GsHallowedArmor.HallowGold,
                        Main.rand.NextFloat(0.5f, 0.75f))?.Configure(GsHallowedArmor.HallowGold, Main.rand.Next(20, 34), 0.12f, 0.9f);
                }
            }
            EnterPillar();
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || State == 2f) {
                return;
            }
            //非处决谢幕：圣辉散光
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                    GsHallowedArmor.HallowGold, Main.rand.NextFloat(0.35f, 0.5f))
                    ?.Configure(GsHallowedArmor.HallowGold, Main.rand.Next(14, 22), 0.08f, 0.7f);
            }
        }

        //==================== 绘制：巨剑本体 + 圣环 + 锁定星标 + 俯冲拖迹 + 余辉光柱 ====================

        private void DrawGreatBlade(Vector2 pos, float rotation, float alpha, float lengthScale) {
            Texture2D shot = CWRAsset.LightShot?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (shot == null || star == null) {
                return;
            }
            Vector2 origin = shot.Size() * 0.5f;
            //焦金剑脊
            Main.EntitySpriteDraw(shot, pos, null,
                (GsHallowedArmor.HallowDeep with { A = 0 }) * (0.95f * alpha), rotation, origin,
                new Vector2(0.62f * lengthScale, 0.20f), SpriteEffects.None, 0);
            //圣金剑身
            Main.EntitySpriteDraw(shot, pos, null,
                (GsHallowedArmor.HallowGold with { A = 0 }) * alpha, rotation, origin,
                new Vector2(0.52f * lengthScale, 0.13f), SpriteEffects.None, 0);
            //纯白刃芯
            Main.EntitySpriteDraw(shot, pos, null,
                (GsHallowedArmor.HallowWhite with { A = 0 }) * (0.9f * alpha), rotation, origin,
                new Vector2(0.42f * lengthScale, 0.055f), SpriteEffects.None, 0);
            //十字护手（垂直于剑身的短横杠）
            Vector2 hiltPos = pos - rotation.ToRotationVector2() * 32f * lengthScale;
            Main.EntitySpriteDraw(shot, hiltPos, null,
                (GsHallowedArmor.HallowGold with { A = 0 }) * (0.9f * alpha), rotation + MathHelper.PiOver2, origin,
                new Vector2(0.14f, 0.09f), SpriteEffects.None, 0);
            //柄尾圣星
            Vector2 pommel = pos - rotation.ToRotationVector2() * 44f * lengthScale;
            Main.EntitySpriteDraw(star, pommel, null,
                (GsHallowedArmor.HallowWhite with { A = 0 }) * (0.85f * alpha), Life * 0.05f, star.Size() * 0.5f,
                0.3f, SpriteEffects.None, 0);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            //余辉光柱态：柱体 + 落点辉環
            if (State == 2f) {
                float pfade = Projectile.timeLeft / (float)PillarFrames;
                if (glow != null) {
                    //柱体三层（窄高拉伸的柔光）
                    Main.EntitySpriteDraw(glow, pos, null,
                        (GsHallowedArmor.HallowDeep with { A = 0 }) * (0.5f * pfade), 0f, glow.Size() * 0.5f,
                        new Vector2(1.3f, 9f) * pfade, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(glow, pos, null,
                        (GsHallowedArmor.HallowGold with { A = 0 }) * (0.7f * pfade), 0f, glow.Size() * 0.5f,
                        new Vector2(0.9f, 8f) * pfade, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(glow, pos, null,
                        (GsHallowedArmor.HallowWhite with { A = 0 }) * (0.8f * pfade), 0f, glow.Size() * 0.5f,
                        new Vector2(0.4f, 7f) * pfade, SpriteEffects.None, 0);
                }
                if (ring != null) {
                    Main.EntitySpriteDraw(ring, pos, null,
                        (GsHallowedArmor.HallowGold with { A = 0 }) * (0.6f * pfade), 0f, ring.Size() * 0.5f,
                        (1f - pfade) * 0.8f + 0.2f, SpriteEffects.None, 0);
                }
                return false;
            }

            float breathe = 1f + MathF.Sin(Life * 0.07f + Seed * 2f) * 0.04f;
            float lengthScale = State == 1f && DiveTime > WindupFrames
                ? 1f + MathHelper.Clamp(Projectile.velocity.Length() * 0.02f, 0f, 0.6f)
                : breathe;

            //俯冲残影
            if (State == 1f && DiveTime > WindupFrames) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.32f * fade;
                    DrawGreatBlade(Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                        Projectile.rotation, ghost, lengthScale * (1f - i * 0.04f));
                }
            }

            //悬空态背环：剑后圣环缓旋
            if (State == 0f && ring != null) {
                Main.EntitySpriteDraw(ring, pos, null,
                    (GsHallowedArmor.HallowGold with { A = 0 }) * (0.35f * fade), 0f, ring.Size() * 0.5f,
                    0.34f * breathe, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(ring, pos, null,
                    (GsHallowedArmor.HallowPink with { A = 0 }) * (0.2f * fade), 0f, ring.Size() * 0.5f,
                    0.28f / breathe, SpriteEffects.None, 0);
            }

            DrawGreatBlade(pos, Projectile.rotation, fade, lengthScale);

            //锁定星标：柄侧依次点亮（跨端由 ai[2] 同步）
            if (State == 0f && star != null) {
                for (int i = 0; i < 3; i++) {
                    bool lit = i < (int)LockPips;
                    float ang = Life * 0.08f + MathHelper.TwoPi * i / 3f + Seed;
                    Vector2 at = pos - Projectile.rotation.ToRotationVector2() * 44f * breathe
                        + ang.ToRotationVector2() * 13f;
                    Color pipColor = lit
                        ? (GsHallowedArmor.HallowPink with { A = 0 }) * 0.95f
                        : (GsHallowedArmor.HallowDeep with { A = 0 }) * 0.35f;
                    Main.EntitySpriteDraw(star, at, null,
                        pipColor * fade, 0f, star.Size() * 0.5f,
                        lit ? 0.2f : 0.12f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
