using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard.Specials;
using CalamityOverhaul.Content.GameModes.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsEarly
{
    /// <summary>
    /// 凤凰爆破枪「涅槃燃殿」：赤金凤羽·熔心手炮（手持接管，A 档）。<br/>
    /// ①命中积「涅槃火种」，枪身羽焰辉光三档升亮，击杀喂得更多；
    /// ②火种满 24：下一发化凤凰灾变弹（振翅加速俯冲，命中爆燃并裂出三只雏凤追猎）；
    /// ③火巢装填：飞散的火星被回吸入膛（倒放的抛壳），完美装填立得 8 层火种。<br/>
    /// 账目：射击节拍对齐原版 10t，弹匣占空比 0.88、灾变弹均摊约 +12%，
    /// 伤害行 ×1.05 → 约 112%（待游戏内标定）
    /// </summary>
    internal class GsPhoenixBlaster : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.PhoenixBlaster;

        protected override string GsDescFallback =>
            "Reforged: every hit feeds the ember nest and the gun glows brighter; kills feed it faster.\n" +
            "At 24 sparks the next shot is reborn as a phoenix that dives, detonates, and splits into three seeking chicks.\n" +
            "Reloading breathes the stray sparks back into the chamber; a sweet-spot reload grants 8 sparks outright";

        public override int MagSize => 10;
        public override int ReloadTicks => 45;
        public override GsReloadStyle Style => GsReloadStyle.Ember;
        protected override int ReloadCueCount => 3;
        protected override bool EjectsShell => true;
        protected override int ShellEvery => 2;

        /// <summary>涅槃火种满值</summary>
        internal const int NirvanaMax = 24;

        /// <summary>涅槃就绪漂字</summary>
        internal static LocalizedText NirvanaText;

        public override void GsSetStaticDefaults() {
            NirvanaText = this.GetLocalization("Nirvana", () => "Nirvana!");
        }

        /// <summary>伤害行 ×1.05：灾变弹摊进预算后的余量，账目见类注释</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 1.05f;

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
            if (player.altFunctionUse == 2) {
                return false;
            }
            if (mp.reloadDuration > 0) {
                return false;
            }
            if (mp.magLeft <= 0) {
                StartReload(item, player, mp, false);
                return false;
            }
            Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                ModContent.ProjectileType<GsPhoenixBlasterHeld>(),
                player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            return false;
        }

        /// <summary>held 侧委托：起装填（myPlayer 路径由 held 保证）</summary>
        internal void HeldStartReload(Item item, Player player, bool tactical)
            => StartReload(item, player, State(player), tactical);

        /// <summary>不走 GsShoot 流，签名行为由 held 承载；此实现仅满足抽象面</summary>
        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => null;

        /// <summary>完美奖励：立得 8 层火种（涅槃层死亡清零、换枪保留，语义随共享层）</summary>
        protected override void OnPerfectReload(Item item, Player player, GsGunsEarlyPlayer mp)
            => mp.nirvanaStacks = Math.Min(NirvanaMax, mp.nirvanaStacks + 8);

        //==================== 火巢回吸装填 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.4f, Pitch = 0.2f }, player.Center);
            }
        }

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (VaultUtils.isServer) {
                return;
            }
            //火星回吸：从四周飞回枪膛，音调随节拍上行
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.6f, Pitch = -0.1f + 0.18f * index }, player.Center);
            Vector2 chamber = player.Center + new Vector2(player.direction * 10f, -2f);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_ProcIntake>(
                    chamber + Main.rand.NextVector2CircularEdge(46f, 40f),
                    Vector2.Zero, GameModeTheme.GodSmithEmber, Main.rand.NextFloat(0.5f, 0.8f))
                    ?.Configure(chamber, Main.rand.Next(14, 22));
            }
        }

        //==================== 弹丸表现（普通弹带余温） ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer || proj.timeLeft % 4 != 0) {
                return;
            }
            //凤羽余温：稀疏烬点尾迹
            PRTLoader.NewParticle<PRT_DefEmber>(proj.Center - proj.velocity * 0.4f,
                -proj.velocity * 0.03f - Vector2.UnitY * 0.2f,
                new Color(255, 168, 80), Main.rand.NextFloat(0.25f, 0.4f))
                ?.Configure(Main.rand.Next(10, 16));
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
    /// 凤凰爆破枪手持弹幕：热手炮的重踢后坐、涅槃辉光档位、灾变弹发射仪式自管
    /// （镜像 GsChainGun 射击循环）。火种数只在 owner 端有意义，远端只看姿态与弹幕
    /// </summary>
    internal class GsPhoenixBlasterHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override LocalizedText DisplayName => Language.GetText("ItemName.PhoenixBlaster");

        private static readonly Color PhoenixGold = new(255, 196, 96);
        private static readonly Color PhoenixHot = new(255, 120, 48);

        private const int IdleKillDelay = 40;

        private int fireTimer;
        private int idleTimer;
        private int dryTimer;
        private float recoilAnim;
        private float glowLevel;    //涅槃辉光档（0..1，owner 写、各端画各自估计值）

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
                //辉光档随火种走，写 ai[1] 过线让远端同步枪身亮度
                float target = mp.nirvanaStacks / (float)GsPhoenixBlaster.NirvanaMax;
                if (MathF.Abs(target - Projectile.ai[1]) > 0.15f || target >= 1f != Projectile.ai[1] >= 1f) {
                    Projectile.ai[1] = target;
                    NetUpdate();
                }
                //右键战术换弹
                if (DownRight && mp.magLeft < 10 && mp.reloadDuration <= 0
                    && GodSmithScheme.TryGetScheme(ItemID.PhoenixBlaster, out GodSmithScheme s)
                    && s is GsPhoenixBlaster px) {
                    px.HeldStartReload(Item, Owner, tactical: mp.magLeft > 0);
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
            //满火种时枪口常燃凤羽火种
            if (!VaultUtils.isServer && glowLevel > 0.95f && Main.GameUpdateCount % 5 == 0) {
                Vector2 muzzle = GsGunPose.MuzzlePos(Projectile, DirSign, 28f, -3f);
                PRTLoader.NewParticle<PRT_DefEmber>(muzzle,
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 0.9f) + Main.rand.NextVector2Circular(0.3f, 0.2f),
                    PhoenixGold, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(14, 22));
            }
            Lighting.AddLight(GsGunPose.MuzzlePos(Projectile, DirSign, 26f, -3f),
                PhoenixHot.ToVector3() * (recoilAnim * 0.5f + glowLevel * 0.35f));
        }

        /// <summary>热手炮姿态：重踢上抬 + 满火种低频呼吸微颤</summary>
        private void UpdatePose() {
            float breath = glowLevel > 0.95f
                ? MathF.Sin(Main.GameUpdateCount * 0.35f + Projectile.identity) * 0.01f
                : 0f;
            GsGunPose.Update(this, 18f, -4f, recoilAnim * 0.09f + breath, recoilAnim * 3.2f, 0.34f);
        }

        /// <summary>一发：满火种时化凤凰灾变弹，否则原版弹药链子弹（各端播音，owner 出弹）</summary>
        private void FireOnce(GsGunsEarlyPlayer mp) {
            recoilAnim = 1f;
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
                    PRTLoader.NewParticle<PRT_Light>(muzzle, Vector2.Zero, PhoenixGold, 0.6f)?.Configure(10, 0.9f);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(muzzle, Vector2.Zero, PhoenixHot, 0f)
                        ?.Configure(0.05f, 0.5f, 12);
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
                px.HeldStartReload(Item, Owner, false);
            }
        }

        private void SetDry(bool value) {
            if (Dry != value) {
                Projectile.ai[2] = value ? 1f : 0f;
                NetUpdate();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //涅槃辉光三档：枪身金羽渐亮，满档白热
            float flicker = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity * 0.77f);
            Color glow = Color.Lerp(PhoenixGold, Color.White, glowLevel * 0.4f) * (glowLevel * 0.55f * flicker);
            GsGunPose.DrawGunBody(ItemID.PhoenixBlaster, Projectile.Center, Projectile.rotation, DirSign,
                lightColor, 1f, glowLevel > 0.03f ? glow : (Color?)null);

            //枪口焰羽
            if (recoilAnim > 0.35f) {
                Texture2D star = CWRAsset.StarTexture?.Value;
                if (star != null) {
                    Vector2 muzzle = GsGunPose.MuzzlePos(Projectile, DirSign, 34f, -3f) - Main.screenPosition;
                    Color c = PhoenixHot * (recoilAnim * 0.75f);
                    c.A = 0;
                    Main.EntitySpriteDraw(star, muzzle, null, c, Projectile.rotation,
                        star.Size() / 2f, new Vector2(0.16f, 0.07f) * recoilAnim, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 凤凰灾变弹：振翅加速、微幅寻的俯冲的火鸟。命中或触地爆燃（族内共享火团 + 震屏），
    /// 并裂出三只雏凤追猎。全程自绘（星羽双翼 + 残影拖尾），原版贴图零依赖
    /// </summary>
    internal class GsPhoenixBlasterNirvanaProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color PhoenixGold = new(255, 208, 110);
        private static readonly Color PhoenixHot = new(255, 122, 46);
        private static readonly Color PhoenixDeep = new(190, 54, 30);

        private float Seed => Projectile.identity * 0.6180f % 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

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

            Lighting.AddLight(Projectile.Center, PhoenixHot.ToVector3() * 0.8f);
            if (!VaultUtils.isServer) {
                //羽焰坠烬：火鸟身后洒落的燃羽
                if (Projectile.timeLeft % 2 == 0) {
                    PRTLoader.NewParticle<PRT_DefEmber>(
                        Projectile.Center - Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(6f, 6f),
                        -Projectile.velocity * 0.05f + Vector2.UnitY * 0.3f,
                        Main.rand.NextBool() ? PhoenixGold : PhoenixHot,
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(18, 30), 0.05f);
                }
                if (Projectile.timeLeft % 6 == 0) {
                    PRTLoader.NewParticle<PRT_HellFire>(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                        -Projectile.velocity * 0.1f, Color.White, Main.rand.NextFloat(0.4f, 0.7f));
                }
            }
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
            //爆燃：白芯闪 + 双环 + 火星扇（各端按同步位置演出）
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = 0.15f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, Color.White, 0.7f)?.Configure(8, 0.95f);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, PhoenixGold, 0f)
                ?.Configure(0.06f, 0.7f, 14);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, PhoenixHot, 0f)
                ?.Configure(0.04f, 0.5f, 18);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Circular(7f, 7f) - Vector2.UnitY * 2f,
                    Main.rand.NextBool() ? PhoenixGold : PhoenixHot,
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(true, Main.rand.Next(14, 24));
            }
            //余痕：地面灼痕 + 升腾余烬（火比鸟活得久）
            PRTLoader.NewParticle<PRT_DefScorch>(Projectile.Center, Vector2.Zero,
                new Color(70, 44, 32), Main.rand.NextFloat(0.6f, 0.9f))?.Configure(Main.rand.Next(60, 90));
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

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float wingBeat = MathF.Sin(Main.GlobalTimeWrappedHourly * 22f + Seed * 9f);

            //残影拖尾：旧位置的褪色鸟影
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color ghost = Color.Lerp(PhoenixDeep, PhoenixHot, t) * (0.28f * t);
                ghost.A = 0;
                Main.EntitySpriteDraw(star, ghostPos, null, ghost, Projectile.rotation,
                    star.Size() / 2f, new Vector2(0.16f, 0.07f) * t, SpriteEffects.None, 0);
            }

            //外焰晕
            Color halo = PhoenixHot * 0.5f;
            halo.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, halo, 0f, glow.Size() / 2f, 0.55f, SpriteEffects.None, 0);
            //鸟身：沿速度拉伸的星羽热芯
            Color body = Color.Lerp(PhoenixGold, Color.White, 0.35f);
            body.A = 0;
            Main.EntitySpriteDraw(star, drawPos, null, body, Projectile.rotation,
                star.Size() / 2f, new Vector2(0.24f, 0.09f), SpriteEffects.None, 0);
            //双翼：斜置星纹随翼拍开合
            Color wing = PhoenixHot * 0.9f;
            wing.A = 0;
            float spreadA = 0.5f + wingBeat * 0.22f;
            Main.EntitySpriteDraw(star, drawPos, null, wing, Projectile.rotation + spreadA,
                star.Size() / 2f, new Vector2(0.15f, 0.05f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, wing, Projectile.rotation - spreadA,
                star.Size() / 2f, new Vector2(0.15f, 0.05f), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 涅槃雏凤：灾变爆裂出的追猎小火鸟，寻的俯冲，命中小爆点燃。火痕自绘
    /// </summary>
    internal class GsPhoenixBlasterChickProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private static readonly Color ChickGold = new(255, 210, 120);
        private static readonly Color ChickHot = new(255, 138, 60);

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
            Lighting.AddLight(Projectile.Center, ChickHot.ToVector3() * 0.35f);
            if (!VaultUtils.isServer && Projectile.timeLeft % 3 == 0) {
                PRTLoader.NewParticle<PRT_DefEmber>(Projectile.Center - Projectile.velocity * 0.3f,
                    -Projectile.velocity * 0.06f, ChickGold, Main.rand.NextFloat(0.22f, 0.36f))
                    ?.Configure(Main.rand.Next(10, 18));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 180);

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item10 with { Volume = 0.4f, Pitch = 0.5f }, Projectile.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, ChickHot, 0f)
                ?.Configure(0.03f, 0.28f, 10);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Circular(3.5f, 3.5f), ChickGold,
                    Main.rand.NextFloat(0.25f, 0.4f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (star == null || glow == null) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float beat = MathF.Sin(Main.GlobalTimeWrappedHourly * 26f + Seed * 11f);
            Color halo = ChickHot * 0.45f;
            halo.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, halo, 0f, glow.Size() / 2f, 0.3f, SpriteEffects.None, 0);
            Color body = ChickGold;
            body.A = 0;
            Main.EntitySpriteDraw(star, drawPos, null, body, Projectile.rotation,
                star.Size() / 2f, new Vector2(0.12f, 0.05f), SpriteEffects.None, 0);
            Color wing = ChickHot * 0.85f;
            wing.A = 0;
            float spread = 0.55f + beat * 0.25f;
            Main.EntitySpriteDraw(star, drawPos, null, wing, Projectile.rotation + spread,
                star.Size() / 2f, new Vector2(0.08f, 0.035f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, drawPos, null, wing, Projectile.rotation - spread,
                star.Size() / 2f, new Vector2(0.08f, 0.035f), SpriteEffects.None, 0);
            return false;
        }
    }
}
