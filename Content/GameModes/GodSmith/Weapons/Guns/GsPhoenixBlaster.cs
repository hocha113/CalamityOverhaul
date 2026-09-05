using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.UI;
using InnoVault.GameContent.BaseEntity;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns
{
    /// <summary>
    /// 凤凰爆破枪「涅槃燃殿」：赤金凤羽·熔心手炮（手持接管，A 档）。<br/>
    /// ①命中积「涅槃火种」，击杀喂得更多；
    /// ②火种满 24：下一发化凤凰灾变弹（振翅加速俯冲，命中爆燃并裂出三只雏凤追猎）；
    /// ③火巢装填三拍。<br/>
    /// 账目：射击节拍对齐原版 10t，弹匣占空比 0.88、灾变弹均摊约 +12%，
    /// 伤害行 ×1.05 → 约 112%（待游戏内标定）
    /// </summary>
    internal class GsPhoenixBlaster : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.PhoenixBlaster;

        protected override string GsDescFallback =>
            "Reforged: every hit feeds the ember nest and the gun glows brighter; kills feed it faster.\nAt 24 sparks the next shot is reborn as a phoenix that dives, detonates, and splits into three seeking chicks.\nReloading breathes the stray sparks back into the chamber; a sweet-spot reload grants 8 sparks outright";
        public override int MagSize => 10;
        public override int ReloadTicks => 45;
        public override GsReloadStyle Style => GsReloadStyle.Ember;
        protected override int ReloadCueCount => 3;

        /// <summary>涅槃火种满值</summary>
        internal const int NirvanaMax = 24;

        /// <summary>涅槃就绪漂字</summary>
        internal static LocalizedText NirvanaText;

        public override void GsSetStaticDefaults() {
            NirvanaText = this.GetLocalization("Nirvana", () => "Nirvana!");
        }

        /// <summary>伤害行 ×1.05：灾变弹摊进预算后的余量，账目见类注释</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 1.05f;

        /// <summary>后坐由 held 自管（重踢 + 颠动），族默认 GsUseStyle 后坐关闭</summary>
        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) { }

        //==================== 使用流：手持接管 ====================

        public override bool? GsCanUseItem(Item item, Player player) {
            if (HeldAlive<GsPhoenixBlasterHeld>(player)) {
                return false;
            }
            if (!IsLocal(player)) {
                return null;
            }
            GsGunsEarlyPlayer mp = State(player);
            SyncHeld(mp);
            if (mp.reloadDuration > 0) {
                return false;
            }
            if (mp.magLeft <= 0) {
                StartReload(item, player, mp);
                return false;
            }
            Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                ModContent.ProjectileType<GsPhoenixBlasterHeld>(),
                player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            return false;
        }

        /// <summary>held 侧委托：起装填（myPlayer 路径由 held 保证）</summary>
        internal void HeldStartReload(Item item, Player player)
            => StartReload(item, player, State(player));

        /// <summary>不走 GsShoot 流，签名行为由 held 承载；此实现仅满足抽象面</summary>
        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => null;

        //==================== 火巢装填 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.4f, Pitch = 0.2f }, player.Center);
            }
        }

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (!VaultUtils.isServer) {
                //音调随节拍上行
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = -0.1f + 0.18f * index }, player.Center);
            }
        }

        /// <summary>命中喂火种：+1 每击，击杀 +4（owner 端权威）</summary>
        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.owner != Main.myPlayer) {
                return;
            }
            GsGunsEarlyPlayer mp = State(Main.player[proj.owner]);
            int before = mp.nirvanaStacks;
            mp.nirvanaStacks = Math.Min(NirvanaMax, mp.nirvanaStacks + (target.life <= 0 ? 4 : 1));
            if (before < NirvanaMax && mp.nirvanaStacks >= NirvanaMax && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.9f, Pitch = 0.4f }, Main.player[proj.owner].Center);
                CombatText.NewText(Main.player[proj.owner].getRect(), GameModeTheme.GodSmithEmber,
                    NirvanaText.Value);
            }
        }
    }

    /// <summary>
    /// 凤凰爆破枪手持弹幕：热手炮的重踢后坐、涅槃档位、灾变弹发射仪式自管
    /// （镜像 GsChainGun 射击循环）。火种数只在 owner 端有意义，远端只看姿态与弹幕
    /// </summary>
    internal class GsPhoenixBlasterHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override LocalizedText DisplayName => Language.GetText("ItemName.PhoenixBlaster");

        private const int IdleKillDelay = 40;

        private int fireTimer;
        private int idleTimer;
        private int dryTimer;
        private float recoilAnim;
        private int shotAge = 99;   //距上一发的帧数（颠动相位）
        private float glowLevel;    //涅槃档（0..1，owner 写 ai[1] 过线，各端平滑跟随；喂姿态微颤与枪声音高）

        private bool Dry => Projectile.ai[2] > 0f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.hide = true;
            Projectile.timeLeft = 60;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            if (!GameModeSystem.GodSmithActive || Item.type != ItemID.PhoenixBlaster
                || Owner.dead || !Owner.active || Owner.noItems) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 2;

            GsGunsEarlyPlayer mp = Owner.GetModPlayer<GsGunsEarlyPlayer>();
            if (Projectile.IsOwnedByLocalPlayer()) {
                if (mp.reloadDuration > 0) {
                    Projectile.Kill();
                    return;
                }
                //涅槃档随火种走，写 ai[1] 过线让远端同步
                float target = mp.nirvanaStacks / (float)GsPhoenixBlaster.NirvanaMax;
                if (MathF.Abs(target - Projectile.ai[1]) > 0.15f || target >= 1f != Projectile.ai[1] >= 1f) {
                    Projectile.ai[1] = target;
                    NetUpdate();
                }
            }
            glowLevel = MathHelper.Lerp(glowLevel, MathHelper.Clamp(Projectile.ai[1], 0f, 1f), 0.1f);

            UpdatePose();

            bool wantFire = DownLeft && !Owner.CCed && !Dry;
            if (wantFire) {
                idleTimer = 0;
                float atkSpeed = Owner.GetWeaponAttackSpeed(Item);
                if (atkSpeed <= 0f) {
                    atkSpeed = 1f;
                }
                int interval = Math.Max(1, (int)MathF.Round(10f / atkSpeed));
                if (++fireTimer >= interval) {
                    fireTimer = 0;
                    FireOnce(mp);
                }
            }
            else {
                idleTimer++;
                fireTimer = 99;
                if (Dry && Projectile.IsOwnedByLocalPlayer()) {
                    SetDry(false);
                }
                if (idleTimer > IdleKillDelay) {
                    Projectile.Kill();
                    return;
                }
            }

            if (Dry) {
                if (++dryTimer > 30 && Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.Kill();
                    return;
                }
            }
            else {
                dryTimer = 0;
            }

            recoilAnim = MathF.Max(0f, recoilAnim - 0.14f);
            shotAge++;
        }

        /// <summary>热手炮姿态：重踢上抬 + 出膛后的阻尼颠动 + 满火种低频呼吸微颤</summary>
        private void UpdatePose() {
            float breath = glowLevel > 0.95f
                ? MathF.Sin(Main.GameUpdateCount * 0.35f + Projectile.identity) * 0.01f
                : 0f;
            //重手枪的颠动：幅度大、停得慢
            float sway = GsGunRecoil.Wobble(shotAge, 1.9f, 0.86f, Projectile.identity) * 2.2f;
            GsGunPose.Update(this, 18f, -4f, recoilAnim * 0.09f + breath, recoilAnim * 3.2f, 0.34f, sway: sway);
        }

        /// <summary>一发：满火种时化凤凰灾变弹，否则原版弹药链子弹（各端播音，owner 出弹）</summary>
        private void FireOnce(GsGunsEarlyPlayer mp) {
            recoilAnim = 1f;
            shotAge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item41 with {
                    Volume = 0.5f,
                    Pitch = 0.1f + glowLevel * 0.15f,
                    MaxInstances = 3
                }, Projectile.Center);
            }

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            if (mp.magLeft <= 0) {
                RequestReload();
                Projectile.Kill();
                return;
            }
            if (!Owner.PickAmmo(Item, out int projToShoot, out float speed, out int damage,
                out float knockback, out _, false)) {
                SetDry(true);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item16 with { Volume = 0.4f, Pitch = -0.4f }, Projectile.Center);
                }
                return;
            }
            SetDry(false);
            mp.magLeft--;
            mp.lastShotTick = Main.GameUpdateCount;

            Vector2 muzzle = GsGunPose.MuzzlePos(Projectile, DirSign, 30f, -3f);
            Vector2 aim = ToMouseA.ToRotationVector2();

            if (mp.nirvanaStacks >= GsPhoenixBlaster.NirvanaMax) {
                //凤凰起翔：清空火种，灾变弹出膛
                mp.nirvanaStacks = 0;
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), muzzle, aim * 9f,
                    ModContent.ProjectileType<GsPhoenixBlasterNirvanaProj>(),
                    Math.Max(1, (int)(damage * 4.5f)), knockback * 2f, Owner.whoAmI);
                recoilAnim = 1.6f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.9f, Pitch = 0.1f }, muzzle);
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.7f, Pitch = 0.5f }, muzzle);
                }
            }
            else {
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), muzzle, aim * speed,
                    projToShoot, damage, knockback, Owner.whoAmI);
            }

            if (mp.magLeft <= 0) {
                RequestReload();
                Projectile.Kill();
            }
        }

        private void RequestReload() {
            if (GodSmithScheme.TryGetScheme(ItemID.PhoenixBlaster, out GodSmithScheme s) && s is GsPhoenixBlaster px) {
                px.HeldStartReload(Item, Owner);
            }
        }

        private void SetDry(bool value) {
            if (Dry != value) {
                Projectile.ai[2] = value ? 1f : 0f;
                NetUpdate();
            }
        }

        /// <summary>枪体本体一笔（原版物品贴图）</summary>
        public override bool PreDraw(ref Color lightColor) {
            GsGunPose.DrawGunBody(ItemID.PhoenixBlaster, Projectile.Center, Projectile.rotation, DirSign, lightColor);
            return false;
        }
    }

    /// <summary>
    /// 凤凰灾变弹：振翅加速、微幅寻的俯冲的火鸟。命中或触地爆燃（族内共享火团 + 震屏），
    /// 并裂出三只雏凤追猎。借原版流星贴图默认绘制
    /// </summary>
    internal class GsPhoenixBlasterNirvanaProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Meteor1;

        private float Seed => Projectile.identity * 0.6180f % 1f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 150;
        }

        public override void AI() {
            //振翅加速：9 px/f 拉到 22 px/f，带翼拍纵向摆
            float speed = Projectile.velocity.Length();
            if (speed < 22f) {
                Projectile.velocity *= 1.045f;
            }
            Vector2 side = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X).SafeNormalize(Vector2.Zero);
            Projectile.velocity += side * MathF.Sin(Main.GameUpdateCount * 0.5f + Seed * MathHelper.TwoPi) * 0.35f;

            //微幅寻的：只在飞行后半程俯冲咬向近敌（各端同算，owner 位置权威兜底）
            if (Projectile.timeLeft < 120) {
                NPC target = Projectile.FindTargetWithinRange(500f);
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * Projectile.velocity.Length();
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.045f);
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Detonate();

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Detonate();
            return true;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //爆燃：爆响 + 震屏（各端按同步位置演出）
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = 0.15f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
            if (CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center,
                    Main.rand.NextVector2Unit(), 4f, 7f, 12, 1000f, "GsPhoenixNirvana"));
            }
        }

        /// <summary>灾变：owner 权威生成火团滞留区与三只雏凤</summary>
        private void Detonate() {
            if (Projectile.owner != Main.myPlayer) {
                return;
            }
            //族内共享火团（径 110，点燃）
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<GsGunsEarlyBurstProj>(),
                Math.Max(1, Projectile.damage / 3), 4f, Projectile.owner, 110f, 0f);
            //浴火重生：三只雏凤扇形散出
            for (int i = -1; i <= 1; i++) {
                Vector2 vel = (-Projectile.velocity.SafeNormalize(Vector2.UnitY)).RotatedBy(i * 0.9f) * 6f
                    - Vector2.UnitY * 2f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    ModContent.ProjectileType<GsPhoenixBlasterChickProj>(),
                    Math.Max(1, (int)(Projectile.damage * 0.3f)), 1f, Projectile.owner);
            }
        }
    }

    /// <summary>
    /// 涅槃雏凤：灾变爆裂出的追猎小火鸟，寻的俯冲，命中点燃。借原版火球贴图默认绘制
    /// </summary>
    internal class GsPhoenixBlasterChickProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

        private float Seed => Projectile.identity * 0.377f % 1f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            NPC target = Projectile.FindTargetWithinRange(600f);
            if (target != null && Projectile.timeLeft < 80) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 13f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.08f);
            }
            else {
                //无标时扑翼盘旋，翼拍相位 identity 定相
                Projectile.velocity = Projectile.velocity.RotatedBy(MathF.Sin(Main.GameUpdateCount * 0.2f + Seed * 6f) * 0.05f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 180);

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.4f, Pitch = 0.5f }, Projectile.Center);
            }
        }
    }
}
