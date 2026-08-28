using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【腐臭撕裂】材质：猩红肉瘤上拔下的腐骨拳爪，爪缝渗脓。
    /// 签名：①原版身份保留：极速贴身连抓，触及全族最短之一
    /// ②连击在目标身上叠撕裂爪印（自绘爪痕标记），叠满四印引发脓爆，
    /// 挂中毒与剧毒 ③四拍左右交替小弧，第四拍双爪撕裂；命中喷绿脓与腐臭雾
    /// </summary>
    internal class GsFetidBaghnakhs : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.FetidBaghnakhs;

        protected override int HeldProjID => ModContent.ProjectileType<GsFetidBaghnakhsHeld>();

        protected override int ComboBeats => 4;

        //贴身连抓：断手窗口收紧
        protected override int ComboResetFrames => 45;

        protected override string GsDescFallback =>
            "Reforged: a four-beat point-blank claw flurry; every hit rakes a festering mark " +
            "into the target, and the fourth mark bursts into pus, poisoning " +
            "and envenoming everything it splatters";

        //腐骨脓爪色板
        internal static readonly Color PusBright = new(214, 232, 140); //脓黄亮缘
        internal static readonly Color PusMain = new(132, 152, 62);    //腐骨橄榄
        internal static readonly Color PusHot = new(178, 255, 64);     //剧毒炽绿
        internal static readonly Color PusDeep = new(30, 34, 14);      //腐液暗沉

        //原版 8 帧/抓是全游戏顶级近战频率；四拍循环 ~34 帧对位 4 抓 ≈ 8.5 帧/抓，
        //拍表 1.0/1.0/1.0/1.2 均摊 ~1.05x；脓爆 0.45x 需同一目标连吃 4 记均摊 ~+11%，
        //中毒/剧毒挂尾 ~+3% → 综合单体 DPS 约为原版 108%~117%，脓爆溅射是范围收益；
        //底伤不动
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 腐臭撕裂手持：四拍贴身连抓。0~2 左右交替小弧快抓，3 双爪撕裂
    /// （残影上调 + 副爪痕涂抹 + 小前压）。命中在目标身上记撕裂印，
    /// 四印引爆脓爆。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsFetidBaghnakhsHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.FetidBaghnakhs;
        protected override int BeatCount => 4;
        protected override Color EdgeBright => GsFetidBaghnakhs.PusBright;
        protected override Color BodyMain => GsFetidBaghnakhs.PusMain;
        protected override Color HotAccent => GsFetidBaghnakhs.PusHot;
        protected override Color DeepShadow => GsFetidBaghnakhs.PusDeep;

        //拳爪贴身：触及极短、判定收窄
        protected override float BaseReach => 62f;
        protected override float CollisionWidth => 26f;
        protected override float PointBlankRadius => 42f;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 3) {
                //双爪撕裂：稍长的举拍并爪，撕开时带小前压
                return new GsBroadBeat {
                    Raise = 3, Hold = 1, Slash = 2, Recover = 4,
                    RaiseBack = 1.55f, Follow = 0.95f, ReachScale = 1.1f, LeanAmp = 0.04f,
                    DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 1.6f, SwingPitch = -0.05f,
                };
            }
            //极短抓挠拍：左右交替，节奏微错
            bool quick = stage % 2 == 0;
            return new GsBroadBeat {
                Raise = 2, Hold = 1, Slash = 2, Recover = 3,
                RaiseBack = 1.2f, Follow = 0.8f, ReachScale = 1f, LeanAmp = 0.02f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = quick ? 0.42f : 0.3f,
            };
        }

        //腐骨爪不反光
        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsFetidBaghnakhs.PusDeep, 0.2f);
        protected override int GhostCount => IsFinisher ? 4 : 1;
        protected override float GhostSpacing => 0.14f;

        /// <summary>命中记账（owner 端）：叠撕裂印，四印引爆脓爆</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer || target.life <= 0) {
                return;
            }
            int markType = ModContent.ProjectileType<GsFetidBaghnakhsMarkProj>();
            Projectile mark = null;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == Projectile.owner && proj.type == markType
                    && (int)proj.ai[0] == target.whoAmI) {
                    mark = proj;
                    break;
                }
            }
            if (mark == null) {
                SpawnOwnedProj(markType, target.Center, Vector2.Zero, 0, 0f, target.whoAmI, 1f);
                return;
            }
            mark.ai[1] += 1f;
            mark.localAI[0] = 0f; //owner 端衰减计时清零
            mark.netUpdate = true;
            if (mark.ai[1] >= 4f) {
                mark.Kill();
                int burstDamage = Math.Max(1, (int)(Projectile.damage * 0.45f));
                SpawnOwnedProj(ModContent.ProjectileType<GsFetidBaghnakhsBurstProj>(),
                    target.Center, Vector2.Zero, burstDamage, Projectile.knockBack * 0.5f);
            }
        }

        protected override void PlaySwingSound() {
            //抓挠比刀砍碎而湿
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.55f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.35f, Pitch = 0.25f }, Owner.Center);
            }
        }

        /// <summary>双爪撕裂：第四拍主弧两侧再画两道错角爪痕涂抹</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (!IsFinisher || slashProgress <= 0.02f || fanFade <= 0.02f) {
                return;
            }
            Texture2D wave = CWRAsset.SemiCircularSmear?.Value;
            if (wave == null) {
                return;
            }
            float alpha = fanFade * (0.2f + slashProgress * 0.26f);
            for (int i = -1; i <= 1; i += 2) {
                float ang = mainAngle + i * 0.36f;
                Vector2 at = Hand + (ang.ToRotationVector2() * mainReach * 0.5f) - Main.screenPosition;
                Color c = GsFetidBaghnakhs.PusHot * (alpha * 0.8f);
                c.A = 0;
                sb.Draw(wave, at, null, c, ang + (swingDir * 0.35f), wave.Size() / 2f,
                    new Vector2(0.3f, 0.11f) * (mainReach / 118f), SpriteEffects.None, 0f);
            }
        }

        protected override void HandleParticles(int phase) {
            //贴身快爪：小而密的脓黄碎火 + 偶发腐臭雾粒
            if (phase != PhaseSlash) {
                return;
            }
            Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f));
            PRTLoader.NewParticle<PRT_Spark>(at, sweepVel * Main.rand.NextFloat(2f, 4f),
                Main.rand.NextBool(3) ? GsFetidBaghnakhs.PusHot : GsFetidBaghnakhs.PusBright,
                Main.rand.NextFloat(0.2f, 0.34f))?.Configure(true, Main.rand.Next(6, 11));
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_Light>(at, sweepVel * 0.6f - Vector2.UnitY * 0.3f,
                    GsFetidBaghnakhs.PusMain, Main.rand.NextFloat(0.05f, 0.08f))?.Configure(11, 0.5f);
            }
        }

        /// <summary>血肉腥臭命中反馈：绿脓迸溅 + 腐臭雾粒上浮</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            int goo = IsFinisher ? 5 : 3;
            for (int i = 0; i < goo; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.GreenBlood,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 60, default,
                    Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = Main.rand.NextBool();
            }
            PRTLoader.NewParticle<PRT_Light>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f), GsFetidBaghnakhs.PusMain,
                Main.rand.NextFloat(0.06f, 0.1f))?.Configure(12, 0.55f);
        }
    }

    /// <summary>
    /// 撕裂爪印：钉在目标身上的标记弹幕（零伤）。ai[0]=目标 NPC 序号
    /// ai[1]=印数（1~4，owner 递增后 netUpdate 过线）；owner 端 4 秒未续印自灭。
    /// 每印画一组交叉爪痕，位置由 identity 播种钉在目标躯体上
    /// </summary>
    internal class GsFetidBaghnakhsMarkProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int DecayFrames = 240;
        private int TargetIndex => (int)Projectile.ai[0];
        private int Stacks => Math.Clamp((int)Projectile.ai[1], 0, 4);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3600;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (TargetIndex < 0 || TargetIndex >= Main.maxNPCs) {
                Projectile.Kill();
                return;
            }
            NPC npc = Main.npc[TargetIndex];
            if (!npc.active) {
                Projectile.Kill();
                return;
            }
            //钉在目标身上随行
            Projectile.Center = npc.Center;

            //印的衰减只由 owner 裁决（远端 localAI 不重置，不许自灭）
            if (Projectile.owner == Main.myPlayer && ++Projectile.localAI[0] > DecayFrames) {
                Projectile.Kill();
                return;
            }

            if (!VaultUtils.isServer && Main.rand.NextBool(14)) {
                //爪痕渗脓下滴
                Dust d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(npc.width * 0.3f, npc.height * 0.3f),
                    DustID.GreenBlood, new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)), 100, default,
                    Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = false;
            }
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (TargetIndex < 0 || TargetIndex >= Main.maxNPCs) {
                return false;
            }
            NPC npc = Main.npc[TargetIndex];
            if (!npc.active) {
                return false;
            }
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (star == null) {
                return false;
            }
            Vector2 center = npc.Center - Main.screenPosition;

            //每印一组交叉爪痕，钉在躯体确定性位置，痕越多目标越破
            for (int s = 0; s < Stacks; s++) {
                Vector2 at = center + new Vector2(
                    (SegRand(s * 7 + 1) - 0.5f) * npc.width * 0.66f,
                    (SegRand(s * 7 + 3) - 0.5f) * npc.height * 0.6f);
                float baseAng = SegRand(s * 7 + 5) * MathHelper.Pi;
                float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + s * 1.9f);
                float len = 0.26f + 0.06f * SegRand(s * 7 + 6);
                //三道平行爪线 + 亮缘
                for (int i = -1; i <= 1; i++) {
                    Vector2 offset = (baseAng + MathHelper.PiOver2).ToRotationVector2() * (i * 5f);
                    Color scar = GsFetidBaghnakhs.PusHot * (0.5f * pulse);
                    scar.A = 0;
                    Main.EntitySpriteDraw(star, at + offset, null, scar, baseAng,
                        star.Size() * 0.5f, new Vector2(len, 0.07f), SpriteEffects.None, 0);
                }
                Color rim = GsFetidBaghnakhs.PusBright * (0.35f * pulse);
                rim.A = 0;
                Main.EntitySpriteDraw(star, at, null, rim, baseAng,
                    star.Size() * 0.5f, new Vector2(len * 1.15f, 0.1f), SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 脓爆：四印引爆的小范围爆裂。8 帧过冲撑到满径，伤害只在扩张期结算一次，
    /// 命中挂中毒与剧毒；脓绿爆心 + 真 alpha 腐斑 + 脓滴外喷。绘制全走确定性相位
    /// </summary>
    internal class GsFetidBaghnakhsBurstProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 22;
        private const float MaxRadius = 88f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        /// <summary>当前扩张半径：8 帧过冲 8% 再回坐</summary>
        private float Radius {
            get {
                float p = MathHelper.Clamp(Life / 8f, 0f, 1f);
                float burst = p < 0.7f ? 1.08f * (p / 0.7f) : MathHelper.Lerp(1.08f, 1f, (p - 0.7f) / 0.3f);
                return MaxRadius * burst;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                //脓爆：湿裂声 + 绿脓四溅 + 腐雾上涌
                SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.85f, Pitch = -0.3f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.NPCDeath9 with { Volume = 0.4f, Pitch = 0.2f }, Projectile.Center);
                for (int i = 0; i < 12; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GreenBlood,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6.5f), 60, default,
                        Main.rand.NextFloat(1f, 1.6f));
                    d.noGravity = Main.rand.NextBool(3);
                }
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Light>(
                        Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.5f),
                        GsFetidBaghnakhs.PusMain, Main.rand.NextFloat(0.09f, 0.15f))?.Configure(15, 0.7f);
                }
            }
            Lighting.AddLight(Projectile.Center, GsFetidBaghnakhs.PusHot.ToVector3() * (0.5f * (1f - Life01)));
        }

        //伤害只在扩张期结算一次
        public override bool? CanDamage() => Life <= 8f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);

        /// <summary>脓液蚀身：中毒 + 剧毒（AddBuff 自动同步）</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 300);
            target.AddBuff(BuffID.Venom, 180);
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (blot == null || glow == null || star == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = 1f - Life01;
            float radius = Radius;

            //腐斑底：真 alpha 暗沉脓斑糊开
            for (int i = 0; i < 3; i++) {
                Vector2 at = center + (SegRand(i) * 6.28f).ToRotationVector2() * (radius * 0.3f * SegRand(i + 5));
                Color dark = GsFetidBaghnakhs.PusDeep * (0.55f * fade);
                Main.EntitySpriteDraw(blot, at, null, dark, SegRand(i + 8) * 6.28f,
                    blot.Size() * 0.5f, (0.36f + 0.14f * SegRand(i + 12)) * (radius / MaxRadius), SpriteEffects.None, 0);
            }

            //炽绿爆心
            Color core = GsFetidBaghnakhs.PusHot * (0.7f * fade * fade);
            core.A = 0;
            Main.EntitySpriteDraw(glow, center, null, core, 0f, glow.Size() * 0.5f,
                0.7f * (radius / MaxRadius), SpriteEffects.None, 0);

            //脓滴：一圈外喷的亮珠，速度参差
            int drops = 9;
            for (int i = 0; i < drops; i++) {
                float ang = MathHelper.TwoPi * i / drops + SegRand(i + 20) * 0.6f;
                float dist = radius * (0.7f + 0.35f * SegRand(i + 30));
                Vector2 at = center + ang.ToRotationVector2() * dist + new Vector2(0f, Life01 * Life01 * 14f);
                Color drop = GsFetidBaghnakhs.PusBright * (0.5f * fade);
                drop.A = 0;
                Main.EntitySpriteDraw(glow, at, null, drop, 0f, glow.Size() * 0.5f,
                    0.13f + 0.07f * SegRand(i + 40), SpriteEffects.None, 0);
            }

            //撕开的爪芒：爆心十字裂纹
            Color rip = GsFetidBaghnakhs.PusHot * (0.5f * fade * fade);
            rip.A = 0;
            Main.EntitySpriteDraw(star, center, null, rip, SegRand(50) * 6.28f + Life * 0.04f,
                star.Size() * 0.5f, new Vector2(0.5f, 0.24f) * (radius / MaxRadius), SpriteEffects.None, 0);
            return false;
        }
    }
}
