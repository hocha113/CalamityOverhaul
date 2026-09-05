using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【丛林活体草叶】材质：活体草叶锻成的丛林中剑，剑身有弹性会呼吸。签名：
    /// ①孢子弧：每拍斩切后沿挥弧洒落毒孢雾，驻留噬咬并挂毒 ②藤蔓缠斩：终结拍
    /// 触及拉长 1.35 倍，命中藤蔓缠定（击退清零 + 剧毒加深）
    /// </summary>
    internal class GsBladeofGrass : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BladeofGrass;

        protected override int HeldProjID => ModContent.ProjectileType<GsBladeofGrassHeld>();

        protected override string GsDescFallback =>
            "Reforged: a living jungle blade; every slash scatters biting spore mist along its arc, leaf-veins wake as you raise it, and the third strike lashes out with vines that bind the target";
        internal static readonly Color GrassBright = new(186, 232, 96); //黄绿刃缘
        internal static readonly Color GrassMain = new(96, 176, 64);    //丛林绿剑身
        internal static readonly Color GrassHot = new(222, 255, 128);   //亮叶光

        //预算：原版 18 伤/20 帧 = 0.9 伤帧，另有叶弹补射（接管后压掉，孢雾顶其位）。
        //周期 = 21+22+28 = 71f，直击 = 18×1.05×(1+1+1.3) ≈ 62.4，
        //孢雾每周期 7 团×20% 期望半数命中 ≈ +13 → 约 1.06 伤帧，
        //对原版含叶弹（~1.0 伤帧）约 106%，孢雾满命中理论上限 ~117%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 草刃剑手持：三拍连击走族标准弧（中剑语言，与太刀/大剑作对照），
    /// 活体剑身过冲更大回坐更弹。每拍收势沿挥弧洒 2~3 团孢雾；
    /// 终结拍触及 1.35 倍藤蔓缠斩。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBladeofGrassHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BladeofGrass;
        protected override Color EdgeBright => GsBladeofGrass.GrassBright;
        protected override Color BodyMain => GsBladeofGrass.GrassMain;
        protected override Color HotAccent => GsBladeofGrass.GrassHot;

        private bool sporeSpawned;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 2) {
                //藤蔓缠斩：长距终结
                return new GsBroadBeat {
                    Raise = 8, Hold = 3, Slash = 5, Recover = 12,
                    RaiseBack = 2.2f, Follow = 1.3f, ReachScale = 1.35f, LeanAmp = 0.09f,
                    DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 2.5f, SwingPitch = -0.15f,
                };
            }
            if (stage == 1) {
                return new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 4, Recover = 10,
                    RaiseBack = 2.05f, Follow = 1.05f, ReachScale = 1.06f, LeanAmp = 0.05f,
                    DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.1f,
                };
            }
            GsBroadBeat b = GsBroadBeat.Standard;
            b.SwingPitch = 0.02f;
            return b;
        }

        /// <summary>活体剑身：过冲更大、回坐更弹</summary>
        protected override float SwingCurve(float p) {
            const float burstEnd = 0.5f;
            const float overshoot = 1.07f;
            if (p < burstEnd) {
                return overshoot * SmoothStep01(p / burstEnd);
            }
            return MathHelper.Lerp(overshoot, 1f, SmoothStep01((p - burstEnd) / (1f - burstEnd)));
        }

        protected override void HandlePhaseEvents(int phase) {
            base.HandlePhaseEvents(phase);
            //孢子弧：收势首帧沿挥过的弧线洒落孢雾
            if (!sporeSpawned && phase == PhaseRecover) {
                sporeSpawned = true;
                int count = IsFinisher ? 3 : 2;
                for (int i = 0; i < count; i++) {
                    float t = (i + 0.5f) / count;
                    float ang = MathHelper.Lerp(ArcStart, ArcEnd, t);
                    Vector2 pos = Hand + (ang.ToRotationVector2() * (FullReach * 0.82f));
                    Vector2 drift = ((ang + (swingDir * MathHelper.PiOver2)).ToRotationVector2() * 0.5f)
                        + new Vector2(0f, 0.22f);
                    //孢雾一跳约 20% 当前伤（终结拍随 1.3 倍水涨，已记入预算）
                    SpawnOwnedProj(ModContent.ProjectileType<GsBladeofGrassSporeProj>(), pos, drift,
                        Math.Max(1, (int)(Projectile.damage * 0.2f)), 0f);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.6f, Pitch = -0.2f }, Hand);
                }
            }
        }

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = Beat.SwingPitch }, Owner.Center);
            //草叶剑每一挥都带叶擦声
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.35f, Pitch = 0.1f }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.4f }, Owner.Center);
            }
        }

        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            //藤蔓缠定：终结拍命中把目标缠在原地（击退清零）。
            //查证 NPC.cs：原版 Slow(32) buff 对 NPC 无任何处理，故减速改为缠定
            if (IsFinisher) {
                modifiers.Knockback *= 0.1f;
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, IsFinisher ? 360 : 180);
        }
    }

    /// <summary>
    /// 毒孢雾：斩切沿弧洒落的驻留孢团。生成时带微速缓漂，驻留 30 帧对碰到的目标
    /// 咬一口（约 20% 底伤）并挂毒；用原版孢子云贴图
    /// </summary>
    internal class GsBladeofGrassSporeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SporeCloud;

        private const int Life = 30;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.SporeCloud];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Life;//一生只咬一口
            Projectile.timeLeft = Life;
        }

        public override bool? CanDamage() => Projectile.timeLeft > 4 ? null : false;

        public override void AI() => Projectile.velocity *= 0.93f;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.Poisoned, 240);
    }
}
