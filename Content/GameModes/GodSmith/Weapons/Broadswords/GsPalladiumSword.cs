using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【命脉汲取】材质：温血钯金重锻的再生金属剑。
    /// 签名：①命中在刃上积攒「活性」（上限 6）
    /// ②终结拍命中把活性抽成命脉光珠，自伤口飞回持剑人，每珠回复 1 点生命（每循环上限 6）
    /// ③拍表带脉搏滞（滞帧全族最长），终结汲取带治愈钟音
    /// </summary>
    internal class GsPalladiumSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.PalladiumSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsPalladiumSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: hits store vitality in the warm palladium edge; the finisher draws it out as life motes that fly back and mend you, up to six per combo";
        internal static readonly Color VeinBright = new(255, 214, 160); //暖金刃缘
        internal static readonly Color VeinMain = new(222, 128, 92);    //钯铜体色
        internal static readonly Color VeinLife = new(255, 122, 104);   //命脉橙红

        internal const int VitalityCap = 6;

        /// <summary>活性层数 0~6；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int Vitality;

        //预算账：拍均 (1+1+1.28)/3≈1.09 ×底伤 1.08≈1.18；命脉光珠零伤害（纯回复）；
        //连段总帧 (22+21+27)=70 对原版 66 (+6%) → 综合单体 DPS ≈ 1.18×0.94 ≈ 原版 111%；
        //回复包络：终结拍至多 6 珠 ×1 点 = 每循环上限 6 点生命（一循环约 1.2 秒，满转化 ~5 HP/s，量级克制）
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 命脉汲取手持：三拍脉搏剑（滞帧全族最长，举-滞如心跳蓄压）。
    /// 普通拍命中攒活性；终结拍首个命中抽层生成命脉光珠。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsPalladiumSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.PalladiumSword;
        protected override Color EdgeBright => GsPalladiumSword.VeinBright;
        protected override Color BodyMain => GsPalladiumSword.VeinMain;
        protected override Color HotAccent => GsPalladiumSword.VeinLife;

        private bool siphonFired;

        private GsPalladiumSword Scheme =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsPalladiumSword : null;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 沉横斩：长滞如屏息
            0 => new GsBroadBeat {
                Raise = 6, Hold = 4, Slash = 4, Recover = 8,
                RaiseBack = 1.9f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.16f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 6, Hold = 4, Slash = 4, Recover = 7,
                RaiseBack = 1.95f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.055f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.22f,
            },
            //拍2 汲取斩：滞谷最深，前压重收
            _ => new GsBroadBeat {
                Raise = 8, Hold = 5, Slash = 4, Recover = 10,
                RaiseBack = 2.2f, Follow = 1.25f, ReachScale = 1.14f, LeanAmp = 0.08f,
                DamageMult = 1.28f, Hitstop = 2, LungeSpeed = 2.8f, SwingPitch = -0.32f,
            },
        };

        /// <summary>命中记账：普通拍攒活性；终结拍首个命中抽层放命脉光珠</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsPalladiumSword scheme = Scheme;
            if (scheme == null) {
                return;
            }
            if (!IsFinisher) {
                int old = scheme.Vitality;
                scheme.Vitality = Math.Min(GsPalladiumSword.VitalityCap, scheme.Vitality + 1);
                if (old < GsPalladiumSword.VitalityCap && scheme.Vitality == GsPalladiumSword.VitalityCap
                    && !VaultUtils.isServer) {
                    //攒满：一记温软的提示音
                    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.35f, Pitch = 0.15f }, Owner.Center);
                }
                return;
            }
            if (siphonFired) {
                return;
            }
            siphonFired = true;
            int stacks = scheme.Vitality;
            if (stacks <= 0) {
                return;
            }
            scheme.Vitality = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.5f, Pitch = -0.05f }, target.Center);
            }
            //自伤口向四外抽出光珠，随后各自折返飞回持剑人（每珠回 1 点，上限即层上限）
            for (int i = 0; i < stacks; i++) {
                Vector2 vel = (mainAngle + MathHelper.Lerp(-1.1f, 1.1f, stacks <= 1 ? 0.5f : i / (float)(stacks - 1)))
                    .ToRotationVector2() * Main.rand.NextFloat(3.5f, 5f);
                SpawnOwnedProj(ModContent.ProjectileType<GsPalladiumSwordLifeMoteProj>(),
                    target.Center + Main.rand.NextVector2Circular(10f, 10f), vel, 0, 0f, i);
            }
        }
    }

    /// <summary>
    /// 命脉光珠：自伤口抽出的生命精粹，用原版吸血刀回血珠贴图。先外撇减速，再折返加速飞回持剑人，
    /// 贴身即消并回复 1 点生命（仅 owner 端结算，Heal 自带同步）。零伤害纯功能弹幕
    /// </summary>
    internal class GsPalladiumSwordLifeMoteProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.VampireHeal;
        public override LocalizedText DisplayName => Language.GetText("ItemName.PalladiumSword");

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 120;
        }

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (Life <= 9f) {
                //外撇段：出伤口减速漂散
                Projectile.velocity *= 0.9f;
            }
            else {
                //折返段：向持剑人加速，越近越快
                float speed = MathF.Min(4.5f + (Life - 9f) * 0.45f, 16f);
                Vector2 desired = (owner.MountedCenter - Projectile.Center).SafeNormalize(Vector2.UnitY) * speed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.14f);
                if (Projectile.Center.Distance(owner.MountedCenter) < 26f) {
                    //贴身：owner 端结算回复（Heal 自带治疗数字与同步包）
                    if (Projectile.owner == Main.myPlayer) {
                        owner.Heal(1);
                    }
                    Projectile.Kill();
                    return;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.22f, Pitch = 0.55f, MaxInstances = 4 }, Projectile.Center);
        }
    }
}
