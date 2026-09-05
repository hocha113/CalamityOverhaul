using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【龙魂蓄啸】材质：铸入贝西龙魂的僦卒龙锋，每一斩都带龙吟。
    /// 签名：①每一斩放出龙吟音爆波（原版音爆波保留升级：出膛快后缓）
    /// ②连段命中积攒龙魂（上限 4），攒满后终结拍的音爆波升格为双龙缠旋波
    /// ③挥砍音保留 DD2_SonicBoomBladeSlash 身份
    /// </summary>
    internal class GsDD2SquireBetsySword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.DD2SquireBetsySword;

        protected override int HeldProjID => ModContent.ProjectileType<GsDD2SquireBetsySwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash looses a sonic dragon-roar wave; hits feed the Dragon Soul, and at 4 souls the finisher's wave ascends into twin coiling dragons";
        internal static readonly Color DragonBright = new(255, 232, 178); //鎏金刃缘
        internal static readonly Color DragonMain = new(255, 148, 64);    //龙焰橙体色
        internal static readonly Color DragonHot = new(255, 84, 36);      //龙怒赤红

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

        //龙锋大剑：触及与判定都比基准宽
        protected override float BaseReach => 128f;
        protected override float CollisionWidth => 46f;

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
            }
        }
    }

    /// <summary>
    /// 龙吟音爆波：用原版飞龙音爆波贴图，出膛快后缓（21 → 约 8.5）。
    /// 原版音爆波的宽波判定（波心垂直线段）与穿透 5 保留。
    /// ai[0]=0 单龙 / 1 双龙股（正弦缠旋交错前进）；ai[1]=股相位符号
    /// </summary>
    internal class GsDD2SquireBetsySwordWaveProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DD2SquireSonicBoom;

        private bool Twin => Projectile.ai[0] > 0.5f;
        private float StrandSign => Projectile.ai[1] >= 0f ? 1f : -1f;
        private ref float Life => ref Projectile.localAI[0];

        /// <summary>波面半展宽（原版 40*scale 的等价物，双龙股各自收窄）</summary>
        private float HalfSpan => Twin ? 30f : 44f;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.DD2SquireSonicBoom];
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
        }

        public override bool? CanDamage() => Life >= 1f ? null : false;

        /// <summary>原版同款宽波判定：波心两侧展开的垂直线段</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float cp = 0f;
            Vector2 span = Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(-MathHelper.PiOver2) * HalfSpan;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center - span, Projectile.Center + span, 16f, ref cp);
        }
    }
}
