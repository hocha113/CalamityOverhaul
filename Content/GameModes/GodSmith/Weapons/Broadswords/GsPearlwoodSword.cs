using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【虹彩珍珠】材质：镶珠贝的珍珠木伪圣剑。
    /// 签名：①每一斩沿挥弧外抛两枚珍珠光珠，命中或落地即碎
    /// ②第三拍呼来珍珠雨，五枚光珠自头顶加速砸落
    /// ③命中带珠贝脆响
    /// </summary>
    internal class GsPearlwoodSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.PearlwoodSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsPearlwoodSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: a playful pearlwood combo that flings bursting pearls with every slash; the third strike calls a short pearl rain from above";
        internal static readonly Color PearlBright = new(255, 246, 232); //珠光乳白
        internal static readonly Color PearlMain = new(230, 186, 168);   //珠木暖粉
        internal static readonly Color PearlHot = new(255, 176, 220);    //虹粉强调

        //弱势特许账（30 伤 15 帧的硬模式木剑公认垫底 → 上限放宽至 135%）：
        //拍均 (1+1+1.2)/3≈1.07；每斩 2 珠 ×0.30x 沿弧外抛（单体重叠约 1/4 → +0.15x/拍）；
        //终结珍珠雨 5×0.24x（单体实取 1~2 珠 → +0.36x/循环 → +0.12x/拍）；
        //连段总帧 51 对原版 45 (+13%) → 综合单体 DPS ≈ (1.07+0.27)×0.88 ≈ 原版 118%，
        //全珠命中的理论上界 ~135% 在弱势特许内（多目标溢出是覆盖收益），底伤不再加成
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 虹彩珍珠手持：三拍轻快连击，每拍小步跳进、音高全族最高。
    /// 每拍斩切爆发外抛珍珠，终结拍另呼珍珠雨。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsPearlwoodSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.PearlwoodSword;
        protected override Color EdgeBright => GsPearlwoodSword.PearlBright;
        protected override Color BodyMain => GsPearlwoodSword.PearlMain;
        protected override Color HotAccent => GsPearlwoodSword.PearlHot;

        private bool pearlsFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 轻横斩：小步跳进
            0 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.65f, Follow = 0.95f, ReachScale = 0.96f, LeanAmp = 0.035f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0.8f, SwingPitch = 0.3f,
            },
            //拍1 返斩：音高再上一阶
            1 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.7f, Follow = 1.0f, ReachScale = 0.98f, LeanAmp = 0.04f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0.8f, SwingPitch = 0.38f,
            },
            //拍2 呼雨上撩：稍沉半档换珍珠雨
            _ => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 9,
                RaiseBack = 2.0f, Follow = 1.2f, ReachScale = 1.1f, LeanAmp = 0.06f,
                DamageMult = 1.2f, Hitstop = 2, LungeSpeed = 1.6f, SwingPitch = 0.12f,
            },
        };

        /// <summary>轻剑高音快哨；终结拍垫一记呼雨铃音</summary>
        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.72f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.35f, Pitch = 0.45f }, Owner.Center);
            }
        }

        /// <summary>每拍外抛两枚珍珠；终结拍另撒五枚珍珠雨（上方遇实心块自动压低雨高）</summary>
        protected override void OnSlashBegin() {
            if (pearlsFired) {
                return;
            }
            pearlsFired = true;
            int pearlDamage = Math.Max(1, (int)(Projectile.damage * 0.30f));
            for (int i = 0; i < 2; i++) {
                float ang = MathHelper.Lerp(ArcStart, ArcEnd, 0.35f + 0.3f * i);
                Vector2 dir = ang.ToRotationVector2();
                Vector2 vel = dir * Main.rand.NextFloat(6.5f, 8f) + new Vector2(0f, -2.4f);
                SpawnOwnedProj(ModContent.ProjectileType<GsPearlwoodSwordPearlProj>(),
                    Hand + dir * (FullReach * 0.7f), vel, pearlDamage, Projectile.knockBack * 0.3f);
            }
            if (!IsFinisher) {
                return;
            }
            //珍珠雨：瞄准向前方上空撒五枚；向上探 11 格实心块，洞穴里雨线自动放矮
            int rainDamage = Math.Max(1, (int)(Projectile.damage * 0.24f));
            Vector2 anchor = Hand + baseAngle.ToRotationVector2() * 120f;
            float ceiling = 170f;
            Point tile = anchor.ToTileCoordinates();
            for (int j = 2; j <= 11; j++) {
                if (WorldGen.SolidTile(tile.X, tile.Y - j)) {
                    ceiling = MathF.Max(46f, (j - 1) * 16f - 10f);
                    break;
                }
            }
            for (int i = 0; i < 5; i++) {
                Vector2 at = new(anchor.X + ((i - 2) * 34f) + Main.rand.NextFloat(-10f, 10f),
                    anchor.Y - ceiling - Main.rand.NextFloat(0f, 24f));
                SpawnOwnedProj(ModContent.ProjectileType<GsPearlwoodSwordPearlProj>(),
                    at, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), 2.6f), rainDamage,
                    Projectile.knockBack * 0.3f, 0f, 1f);
            }
        }

        /// <summary>命中：珠贝脆响</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.25f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
            }
        }
    }

    /// <summary>
    /// 珍珠光珠：用原版水晶碎片贴图。抛珠走轻重力抛物线、雨珠加速直坠，命中/落地即碎。
    /// ai[1]=1 为雨珠
    /// </summary>
    internal class GsPearlwoodSwordPearlProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CrystalShard;
        public override LocalizedText DisplayName => Language.GetText("ItemName.PearlwoodSword");

        private bool Rain => Projectile.ai[1] > 0.5f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            if (Rain) {
                //雨珠：加速直坠，末速 15
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.5f, 15f);
                Projectile.velocity.X *= 0.99f;
            }
            else {
                //抛珠：轻重力抛物线，横速缓收
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.22f, 12f);
                Projectile.velocity.X *= 0.985f;
            }
            Projectile.rotation += 0.18f * (Projectile.velocity.X >= 0f ? 1f : -1f);
        }

        /// <summary>珠碎：珠贝脆响</summary>
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.35f, Pitch = 0.45f, MaxInstances = 3 }, Projectile.Center);
        }
    }
}
