using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
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
    /// 【龙魂蓄啸】材质：铸入贝西龙魂的僦卒龙锋，剑身常燃龙焰，每一斩都带龙吟。
    /// 签名：①每一斩放出龙吟音爆波（原版音爆波保留升级：龙弧双层波+音爆震纹，出膛快后缓）
    /// ②连段命中积攒龙魂（上限 4），攒满后终结拍的音爆波升格为双龙缠旋波
    /// ③挥砍音保留 DD2_SonicBoomBladeSlash 身份
    /// </summary>
    internal class GsDD2SquireBetsySword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.DD2SquireBetsySword;

        protected override int HeldProjID => ModContent.ProjectileType<GsDD2SquireBetsySwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash looses a sonic dragon-roar wave; " +
            "hits feed the Dragon Soul, and at 4 souls the finisher's wave " +
            "ascends into twin coiling dragons";

        //龙焰色板
        internal static readonly Color DragonBright = new(255, 232, 178); //鎏金刃缘
        internal static readonly Color DragonMain = new(255, 148, 64);    //龙焰橙体色
        internal static readonly Color DragonHot = new(255, 84, 36);      //龙怒赤红
        internal static readonly Color DragonDeep = new(42, 22, 16);      //焦鳞垫影

        /// <summary>龙魂满层数</summary>
        internal const int FullSouls = 4;

        /// <summary>龙魂层数（0~4）；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int DragonSoul;

        //底伤不加成（原版 180 已是骑士线顶格）：拍均 1.05/1.10/1.40 + 每斩 0.85x 音爆波随拍倍率，
        //三拍循环约 64 帧摊算：刀身 3.55x + 波 3.02x = 6.57x/64f，对上原版（刀+波）6.0x/60f 约 103%；
        //满魂时终结波换成 2x0.5x 双龙（+0.21x/循环，约 106%），穿透 5 的多目标是 AoE 上限（<=120%）
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 龙魂蓄啸手持：三拍骑士连段。0 横斩 / 1 返斩 / 2 龙啸重斩（长举蓄魂、前压、波更壮）。
    /// 每拍斩切爆发放出音爆波；满魂终结拍升格双龙。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsDD2SquireBetsySwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.DD2SquireBetsySword;
        protected override Color EdgeBright => GsDD2SquireBetsySword.DragonBright;
        protected override Color BodyMain => GsDD2SquireBetsySword.DragonMain;
        protected override Color HotAccent => GsDD2SquireBetsySword.DragonHot;
        protected override Color DeepShadow => GsDD2SquireBetsySword.DragonDeep;

        //龙锋大剑：触及与判定都比基准宽
        protected override float BaseReach => 128f;
        protected override float CollisionWidth => 46f;

        //龙焰常燃
        protected override bool GlowAlways => true;

        private bool waveFired;

        private GsDD2SquireBetsySword Scheme =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsDD2SquireBetsySword : null;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 横斩
            0 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 7,
                RaiseBack = 1.85f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1.05f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.05f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 7,
                RaiseBack = 1.9f, Follow = 1.05f, ReachScale = 1.03f, LeanAmp = 0.055f,
                DamageMult = 1.10f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.06f,
            },
            //拍2 龙啸：长举蓄魂、前压重斩
            _ => new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 11,
                RaiseBack = 2.2f, Follow = 1.25f, ReachScale = 1.15f, LeanAmp = 0.085f,
                DamageMult = 1.4f, Hitstop = 2, LungeSpeed = 3.0f, SwingPitch = -0.22f,
            },
        };

        /// <summary>挥砍音保留原版音爆龙吟身份</summary>
        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.DD2_SonicBoomBladeSlash with { Volume = 0.9f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.35f, Pitch = -0.35f }, Owner.Center);
            }
        }

        /// <summary>每拍斩切爆发放出音爆波；满魂终结拍消耗龙魂升格双龙缠旋波（方案层数守 owner）</summary>
        protected override void OnSlashBegin() {
            if (waveFired || Projectile.owner != Main.myPlayer) {
                return;
            }
            waveFired = true;
            Vector2 dir = baseAngle.ToRotationVector2();
            Vector2 from = Hand + dir * (FullReach * 0.62f);
            GsDD2SquireBetsySword scheme = Scheme;
            if (IsFinisher && scheme != null && scheme.DragonSoul >= GsDD2SquireBetsySword.FullSouls) {
                scheme.DragonSoul = 0;
                int twinDamage = Math.Max(1, (int)(Projectile.damage * 0.5f));
                for (int i = -1; i <= 1; i += 2) {
                    SpawnOwnedProj(ModContent.ProjectileType<GsDD2SquireBetsySwordWaveProj>(),
                        from, dir * 21f, twinDamage, Projectile.knockBack * 0.5f, 1f, i);
                }
            }
            else {
                int waveDamage = Math.Max(1, (int)(Projectile.damage * 0.85f));
                SpawnOwnedProj(ModContent.ProjectileType<GsDD2SquireBetsySwordWaveProj>(),
                    from, dir * 21f, waveDamage, Projectile.knockBack * 0.6f, 0f, swingDir);
            }
        }

        /// <summary>命中攒龙魂（所有拍都攒，终结拍攒的进下一轮）；攒满一声短龙吟提示</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsDD2SquireBetsySword scheme = Scheme;
            if (scheme == null) {
                return;
            }
            int old = scheme.DragonSoul;
            scheme.DragonSoul = Math.Min(GsDD2SquireBetsySword.FullSouls, scheme.DragonSoul + 1);
            if (old < GsDD2SquireBetsySword.FullSouls && scheme.DragonSoul == GsDD2SquireBetsySword.FullSouls) {
                SoundEngine.PlaySound(SoundID.DD2_BetsysWrathShot with { Volume = 0.55f, Pitch = 0.3f }, Owner.Center);
                SetFlash(6);
            }
        }

        /// <summary>龙啸拍蓄势：龙焰自四周汇入刀身；平时斩切照基类甩火星</summary>
        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (!IsFinisher || phase > PhaseHold) {
                return;
            }
            Vector2 hand = Hand;
            Vector2 at = hand + Main.rand.NextVector2Unit() * Main.rand.NextFloat(42f, 76f);
            PRTLoader.NewParticle<PRT_Light>(at, (Vector2.Lerp(hand, mainTip, 0.6f) - at) * 0.16f,
                GsDD2SquireBetsySword.DragonMain, Main.rand.NextFloat(0.06f, 0.11f))?.Configure(9, 0.6f);
        }

        /// <summary>龙魂刻焰：沿刀脊排出已攒层数（只画给 owner，层数不跨端共享）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            Texture2D glow = CWRAsset.StarGlow01?.Value;
            if (glow == null) {
                return;
            }
            GsDD2SquireBetsySword scheme = Scheme;
            int souls = scheme?.DragonSoul ?? 0;
            if (souls <= 0 || fanFade <= 0.05f) {
                return;
            }
            bool full = souls >= GsDD2SquireBetsySword.FullSouls;
            Vector2 hand = Hand;
            for (int i = 0; i < souls; i++) {
                Vector2 at = hand + mainAngle.ToRotationVector2() * (mainReach * (0.30f + 0.14f * i)) - Main.screenPosition;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6.5f + i * 1.4f);
                Color c = (full ? GsDD2SquireBetsySword.DragonHot : GsDD2SquireBetsySword.DragonMain) * (0.55f * fanFade * pulse);
                c.A = 0;
                sb.Draw(glow, at, null, c, 0f, glow.Size() * 0.5f, 0.16f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 龙吟音爆波：龙弧双层波前行，出膛快后缓（21 → 约 8.5），身后拖音爆震纹。
    /// 原版音爆波的宽波判定（波心垂直线段）与穿透 5 保留。
    /// ai[0]=0 单龙 / 1 双龙股（正弦缠旋交错前进）；ai[1]=股相位符号（单龙时为月牙弯向）
    /// </summary>
    internal class GsDD2SquireBetsySwordWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private bool Twin => Projectile.ai[0] > 0.5f;
        private float StrandSign => Projectile.ai[1] >= 0f ? 1f : -1f;
        private ref float Life => ref Projectile.localAI[0];

        /// <summary>波面半展宽（原版 40*scale 的等价物，双龙股各自收窄）</summary>
        private float HalfSpan => Twin ? 30f : 44f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 5; //原版音爆波穿透数
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 60;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && Twin && !VaultUtils.isServer) {
                //双龙出膛：龙炎啸一记（波实体已同步，各端都放）
                SoundEngine.PlaySound(SoundID.DD2_BetsysWrathShot with { Volume = 0.6f, Pitch = -0.1f }, Projectile.Center);
            }
            //出膛快后缓：非匀速前行
            if (Projectile.velocity.Length() > 8.5f) {
                Projectile.velocity *= 0.945f;
            }
            //双龙股：垂直向正弦缠旋，逐帧增量确定性推进，两股相位差半周交错
            if (Twin) {
                Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                float phase = StrandSign > 0f ? 0f : MathHelper.Pi;
                float now = MathF.Sin(Life * 0.30f + phase) * 26f;
                float prev = MathF.Sin((Life - 1f) * 0.30f + phase) * 26f;
                Projectile.position += perp * (now - prev);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, GsDD2SquireBetsySword.DragonMain.ToVector3() * 0.4f);

            if (!VaultUtils.isServer && Main.rand.NextBool(Twin ? 2 : 3)) {
                //龙焰余烬自波身洒落
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Main.rand.NextBool(3) ? GsDD2SquireBetsySword.DragonHot : GsDD2SquireBetsySword.DragonMain,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(10, 16));
            }
        }

        public override bool? CanDamage() => Life >= 1f ? null : false;

        /// <summary>原版同款宽波判定：波心两侧展开的垂直线段</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float cp = 0f;
            Vector2 span = Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(-MathHelper.PiOver2) * HalfSpan;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - span, Projectile.Center + span, 16f, ref cp);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f),
                    Main.rand.NextBool() ? GsDD2SquireBetsySword.DragonBright : GsDD2SquireBetsySword.DragonHot,
                    Main.rand.NextFloat(0.32f, 0.55f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //波散：龙焰光尘缓浮
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f),
                    GsDD2SquireBetsySword.DragonMain, Main.rand.NextFloat(0.05f, 0.09f))?.Configure(11, 0.6f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (smear == null || glow == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation + StrandSign * 0.22f;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f) * MathHelper.Clamp(Life / 2f, 0f, 1f);
            float speed01 = MathHelper.Clamp(Projectile.velocity.Length() / 21f, 0f, 1f);
            //速度拉伸：快时沿行进向拉长，慢时回缩堆厚
            Vector2 stretch = new(0.30f + 0.26f * speed01, 0.52f - 0.10f * speed01);
            float sizeMul = Twin ? 0.78f : 1.05f;

            //音爆震纹：身后旧位置的扩张残环，越远越薄越透
            for (int i = 1; i <= 3; i++) {
                int idx = i * 3;
                if (idx >= Projectile.oldPos.Length || Projectile.oldPos[idx] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[idx] + Projectile.Size * 0.5f - Main.screenPosition;
                Color ring = GsDD2SquireBetsySword.DragonMain * (0.16f * (1f - i / 4f) * fade);
                ring.A = 0;
                Main.EntitySpriteDraw(smear, at, null, ring, rot, smear.Size() * 0.5f,
                    stretch * sizeMul * (1f + i * 0.22f), SpriteEffects.None, 0);
            }

            //双龙缠旋轨迹：近段旧位置串珠光
            if (Twin) {
                for (int i = 1; i < Projectile.oldPos.Length; i++) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    Vector2 at = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                    float k = 1f - i / (float)Projectile.oldPos.Length;
                    Color bead = GsDD2SquireBetsySword.DragonHot * (0.22f * k * fade);
                    bead.A = 0;
                    Main.EntitySpriteDraw(glow, at, null, bead, 0f, glow.Size() * 0.5f, 0.22f * k + 0.06f, SpriteEffects.None, 0);
                }
            }

            //龙弧双层波：橙体宽弧 + 鎏金刃缘前压细弧 + 龙怒芯线
            Color body = GsDD2SquireBetsySword.DragonMain * (0.5f * fade);
            body.A = 0;
            Main.EntitySpriteDraw(smear, center, null, body, rot, smear.Size() * 0.5f, stretch * sizeMul, SpriteEffects.None, 0);
            Vector2 ahead = Projectile.velocity.SafeNormalize(Vector2.Zero) * 6f;
            Color edge = GsDD2SquireBetsySword.DragonBright * (0.72f * fade);
            edge.A = 0;
            Main.EntitySpriteDraw(smear, center + ahead, null, edge, rot, smear.Size() * 0.5f,
                new Vector2(stretch.X * 0.8f, stretch.Y * 0.55f) * sizeMul, SpriteEffects.None, 0);
            Color core = GsDD2SquireBetsySword.DragonHot * (0.35f * fade);
            core.A = 0;
            Main.EntitySpriteDraw(smear, center - ahead * 0.5f, null, core, rot, smear.Size() * 0.5f,
                new Vector2(stretch.X * 0.6f, stretch.Y * 0.3f) * sizeMul, SpriteEffects.None, 0);

            //波角亮点：波面两端的龙目光斑
            Vector2 side = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();
            for (int i = -1; i <= 1; i += 2) {
                Color horn = GsDD2SquireBetsySword.DragonBright * (0.4f * fade);
                horn.A = 0;
                Main.EntitySpriteDraw(glow, center + side * (i * (HalfSpan - 8f)) - ahead * 0.6f,
                    null, horn, 0f, glow.Size() * 0.5f, 0.22f * sizeMul + 0.04f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
