using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MeleeOddities
{
    /// <summary>
    /// 【泰拉悲愿】材质：未成形的泰拉之芽，青绿刃光的剑之原胚。
    /// 签名「新芽爆发」：①按住化作稀疏大弧刃风，新芽生长（150 帧攒满）令刃影愈发密集
    /// ②松手按生长放出新月剑气
    /// </summary>
    internal class GsTerragrim : GodSmithScheme
    {
        public override int TargetItemID => ItemID.Terragrim;

        public override string GsFamily => "MeleeOddities";

        protected override string GsDescFallback =>
            "Reforged: hold to become a storm of blades while Growth builds;\nrelease to loose a blade wave scaled by Growth - at full power it takes the shape of the Terra Blade";
        public override bool? GsCanUseItem(Item item, Player player) {
            //手持乱舞在场即禁再触发（channel 驻场，held 自续，松手即收）
            if (HeldAlive<GsTerragrimHeld>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsTerragrimHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            }
            //全端返回 false 压掉原版乱舞；远端靠弹幕同步看到动作
            return false;
        }

        //底伤 +5%：乱舞本体保持原版 5 帧复击节奏，松手新月剑气（满蓄 2.2 倍武器伤、150 帧攒满）
        //的收益已计入包络，综合 DPS 约为原版 105%~118%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;
    }

    /// <summary>
    /// 泰拉悲愿手持乱舞：稀疏大弧刃风。新芽生长 0→1（150 帧），闪现间隔随生长 6 收到 4；
    /// 松手生长 ≥0.35 放出新月剑气，不足只响一记短刃音
    /// </summary>
    internal class GsTerragrimHeld : GsOdditiesFlurryHeldBase
    {
        protected override int SwordItemID => ItemID.Terragrim;

        /// <summary>生长攒满帧数</summary>
        private const int GrowthFullFrames = 150;
        /// <summary>放剑气的生长门槛</summary>
        private const float CrescentGate = 0.35f;

        /// <summary>新芽生长 0~1；随 held 生灭天然每场乱舞独立，各端按各自帧数推同一条曲线</summary>
        private float growth;

        /// <summary>刃影密度随生长提升：闪现间隔 6 收到 4</summary>
        protected override int FlashInterval => 6 - (int)MathF.Round(growth * 2f);
        protected override float SpreadArc => 0.85f;
        protected override float SwingPitch => 0.02f;

        protected override void FlurryAI()
            => growth = Math.Min(1f, timer / (float)GrowthFullFrames);

        /// <summary>松手结算：生长够放新月剑气（owner 生成），不足只响短刃音</summary>
        protected override void OnRelease() {
            if (growth >= CrescentGate) {
                if (Projectile.owner == Main.myPlayer) {
                    //伤害生成时算好传入：武器伤 ×(1+1.2×growth)
                    int dmg = Math.Max(1, (int)(Projectile.damage * (1f + (1.2f * growth))));
                    Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), Hand, AimUnit * 11f,
                        ModContent.ProjectileType<GsTerragrimCrescentProj>(),
                        dmg, Projectile.knockBack, Owner.whoAmI, growth);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item60 with { Volume = 0.55f, Pitch = -0.1f + (0.2f * growth) }, Owner.Center);
                }
            }
            else if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = 0.25f }, Owner.Center);
            }
        }
    }

    /// <summary>
    /// 新芽新月剑气：松手放出的青绿剑气，ai[0]=生长（随生成包过线，定尺度）。
    /// 飞行微减速不做匀速贴纸；贴图借原版泰拉刃剑气（130）默认绘制，朝向 velocity
    /// </summary>
    internal class GsTerragrimCrescentProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.TerraBeam;
        public override LocalizedText DisplayName => Language.GetText("ItemName.Terragrim");

        private const int Life = 40;

        private float Growth => MathHelper.Clamp(Projectile.ai[0], 0f, 1f);
        private float CrescentScale => 0.7f + (0.8f * Growth);

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.TerraBeam];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = Life;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; //一道剑气对同一目标只命中一次
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                //按生长撑判定箱与贴图尺度
                int size = (int)(44 * CrescentScale);
                Projectile.Resize(size, size);
                Projectile.scale = CrescentScale;
            }
            //原版剑气贴图斜 45°，长轴对齐飞行向
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            //飞行有生命周期：微减速收尾，不做匀速直飞
            Projectile.velocity *= 0.982f;

            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
        }
    }
}
