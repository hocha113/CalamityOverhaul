using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 信号枪「彩号信管」：救生信号枪·四色药罐。<br/>
    /// ①四色信管轮转（红/绿/蓝/白），钉进敌人身上持续燃烧；
    /// ②被钉信管的敌人受你的一切伤害 +12%（白色压轴信管 +16%），信管即战术信标；
    /// ③彩罐旋换装填两拍（退罐/上罐）。<br/>
    /// 后坐 1.5px + 信号枪扬口。钉不进敌人时落地烧完。<br/>
    /// 账目：本体伤害沿用原版信号弹（近零），价值全在 +12% 信标增伤（支援位），
    /// 无伤害行修饰（待游戏内标定）
    /// </summary>
    internal class GsFlareGun : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.FlareGun;

        protected override string GsDescFallback =>
            "Reforged: a four-color canister, red, green, blue, then white, each flare pinning into flesh and burning there.\nA pinned foe takes 12% more damage from you (16% for the white flare).\nSwap canisters in two beats; a sweet-spot swap fires the next pull as a double flare";
        public override int MagSize => 4;
        public override int ReloadTicks => 40;
        public override GsReloadStyle Style => GsReloadStyle.Canister;
        protected override int ReloadCueCount => 2;
        protected override float GetRecoil(bool lastRound) => 1.5f;

        /// <summary>本发罐位（0..3）。Fire* 时余弹已被共享层扣 1，故减一还原</summary>
        private int CanisterIndex(GsGunsEarlyPlayer mp) => Math.Clamp(MagSize - mp.magLeft - 1, 0, 3);

        protected override bool? FireNormalRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => LaunchFlare(player, mp, source, position, velocity, damage, knockback);

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => LaunchFlare(player, mp, source, position, velocity, damage, knockback);

        /// <summary>发射信管：压掉原版信号弹</summary>
        private bool? LaunchFlare(Player player, GsGunsEarlyPlayer mp, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int damage, float knockback) {
            int color = CanisterIndex(mp);
            Projectile.NewProjectile(source, position, velocity,
                ModContent.ProjectileType<GsFlareGunSignalProj>(),
                Math.Max(1, damage), knockback, player.whoAmI, color);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item11 with { Volume = 0.5f, Pitch = 0.55f }, position);
            }
            return false;
        }

        //==================== 彩罐旋换 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                //退罐
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.55f, Pitch = -0.2f }, player.Center);
            }
        }

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (!VaultUtils.isServer) {
                //上罐旋扣两拍
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.65f, Pitch = -0.15f + 0.25f * index }, player.Center);
            }
        }

        //==================== 后坐姿态：信号枪扬口（差分，见 GsGunRecoil） ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame)
            => GunKickStyle(player, 1.5f, 0.12f);
    }

    /// <summary>
    /// 信标增伤结算：目标身上钉着自己的信管即吃 +12%（白管 +16%）。
    /// 方案是共享单例，故这层挂在每玩家 ModPlayer 上；只读弹幕表，无跨端状态
    /// </summary>
    internal class GsFlareGunPlayer : ModPlayer
    {
        /// <summary>找目标身上自己钉的信管，返回最高档增伤系数</summary>
        private float MarkFactor(NPC target) {
            if (!GameModeSystem.GodSmithActive
                || Player.ownedProjectileCounts[ModContent.ProjectileType<GsFlareGunSignalProj>()] <= 0) {
                return 1f;
            }
            float factor = 1f;
            int signalType = ModContent.ProjectileType<GsFlareGunSignalProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == Player.whoAmI && proj.type == signalType
                    && proj.localAI[2] > 0f && (int)proj.ai[1] - 1 == target.whoAmI) {
                    factor = Math.Max(factor, proj.ai[0] >= 3f ? 1.16f : 1.12f);
                }
            }
            return factor;
        }

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers) {
            float factor = MarkFactor(target);
            if (factor > 1f) {
                modifiers.FinalDamage *= factor;
            }
        }

        public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers) {
            float factor = MarkFactor(target);
            if (factor > 1f) {
                modifiers.FinalDamage *= factor;
            }
        }
    }

    /// <summary>
    /// 彩号信管：ai[0]=罐色（0红/1绿/2蓝/3白），ai[1]=钉住的 NPC+1（0=未钉），localAI[2]=已钉旗标。<br/>
    /// 抛物线飞行，钉敌 8 秒持续点燃，钉不进就落地烧完；借原版信号弹贴图默认绘制
    /// </summary>
    internal class GsFlareGunSignalProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Flare;

        private bool Stuck => Projectile.localAI[2] > 0f;
        private int StuckNpc => (int)Projectile.ai[1] - 1;
        private float Seed => Projectile.identity * 0.6180f % 1f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 900;
        }

        /// <summary>钉上后不再判定</summary>
        public override bool? CanDamage() => Stuck ? false : null;

        public override void AI() {
            if (Stuck) {
                NPC host = StuckNpc >= 0 && StuckNpc < Main.maxNPCs ? Main.npc[StuckNpc] : null;
                if (host == null || !host.active) {
                    Projectile.Kill();
                    return;
                }
                //钉附随行：identity 定相的贴身偏移
                Vector2 offset = (Seed * MathHelper.TwoPi).ToRotationVector2()
                    * new Vector2(host.width, host.height) * 0.24f;
                Projectile.Center = host.Center + offset;
                Projectile.velocity = Vector2.Zero;
                host.AddBuff(BuffID.OnFire, 10);
            }
            else {
                //抛物线信号弹
                Projectile.velocity.Y += 0.12f;
                Projectile.velocity.X *= 0.998f;
                Projectile.rotation = Projectile.velocity.ToRotation();
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //钉入：转信标（owner 权威写 ai，随包过线）
            Projectile.ai[1] = target.whoAmI + 1;
            Projectile.localAI[2] = 1f;
            Projectile.timeLeft = 480;
            Projectile.netUpdate = true;
            target.AddBuff(BuffID.OnFire, 300);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.6f, Pitch = 0.2f }, target.Center);
            }
        }

        /// <summary>远端收到钉附同步后补旗标（ai 过线、localAI 不过线）</summary>
        public override void PostAI() {
            if (!Stuck && Projectile.ai[1] > 0f) {
                Projectile.localAI[2] = 1f;
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 480);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //钉不进敌人：落地当照明棒烧完
            Projectile.velocity = Vector2.Zero;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, 480);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.35f, Pitch = 0.3f }, Projectile.Center);
            }
            return false;
        }
    }
}
