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
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsEarly
{
    /// <summary>
    /// 迷你鲨「噬饵狂潮」：镀铬鲨机·链动转膛（手持接管，A 档）。<br/>
    /// ①持续开火积「嗜血」：射速 8t 爬到 5t，散布随之发散、枪口逐渐上跳（失控曲线）；
    /// ②嗜血满入「狂潮」：每第 4 发化鲨齿撕咬弹（青光獠牙曳光，命中咬合爆 + 撕裂）；
    /// ③鼓匣整鼓拔插装填（50 发），完美装填本匣自带半量嗜血起手。<br/>
    /// 账目：射速均值约 6.2t 对原版 8t（×1.29），弹匣占空比 0.84、狂潮牙弹 +5%，
    /// 伤害行 ×0.9 → 约 108%（原版 33% 省弹由 PickAmmo 原样保留，待游戏内标定）
    /// </summary>
    internal class GsMinishark : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.Minishark;

        protected override string GsDescFallback =>
            "Reforged: hold the trigger and the shark tastes blood; fire rate climbs while the spread runs wild.\n" +
            "At full frenzy every fourth round becomes a shark-tooth bite that tears the wound open.\n" +
            "A 50-round drum swaps in one piece; nail the sweet spot to start the next drum half-frenzied";

        public override int MagSize => 50;
        public override int ReloadTicks => 55;
        public override GsReloadStyle Style => GsReloadStyle.Drum;
        protected override int ReloadCueCount => 2;
        protected override bool EjectsShell => true;
        protected override int ShellEvery => 3;

        /// <summary>狂潮漂字</summary>
        internal static LocalizedText FrenzyText;

        public override void GsSetStaticDefaults() {
            FrenzyText = this.GetLocalization("Frenzy", () => "Frenzy!");
        }

        /// <summary>伤害行 ×0.9：射速爬升与牙弹收益的对账回缩，账目见类注释</summary>
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= 0.9f;

        //==================== 使用流：手持接管 ====================

        public override bool? GsCanUseItem(Item item, Player player) {
            if (HeldAlive<GsMinisharkHeld>(player)) {
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
                return false;   //整鼓拔插不可打断
            }
            if (mp.magLeft <= 0) {
                StartReload(item, player, mp, false);
                return false;
            }
            Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                ModContent.ProjectileType<GsMinisharkHeld>(),
                player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            return false;
        }

        /// <summary>held 侧委托：起装填（myPlayer 路径由 held 保证）</summary>
        internal void HeldStartReload(Item item, Player player, bool tactical)
            => StartReload(item, player, State(player), tactical);

        /// <summary>held 侧委托：写打标档位后立即生成弹幕（owner 同帧消费）</summary>
        internal void SetPendingMark(float mark) => pendingMark = mark;

        /// <summary>不走 GsShoot 流，末发签名由 held 承载；此实现仅满足抽象面</summary>
        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => null;

        /// <summary>完美奖励改整匣：本匣自带半量嗜血起手（held 出生时消费一次）</summary>
        protected override void OnPerfectReload(Item item, Player player, GsGunsEarlyPlayer mp) => mp.perfectMag = true;

        //==================== 整鼓拔插音画 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (VaultUtils.isServer) {
                return;
            }
            //拔鼓：整鼓坠地 + 余壳散落
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.7f, Pitch = -0.45f }, player.Center);
            PRTLoader.NewParticle<PRT_ProcChip>(player.Center + new Vector2(player.direction * 6f, 2f),
                new Vector2(-player.direction * 0.8f, -1.6f),
                new Color(90, 96, 104), 1.1f)
                ?.Configure(new Color(170, 180, 190), 34, 0.8f);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_ProcChip>(player.Center + new Vector2(player.direction * 6f, -2f),
                    new Vector2(-player.direction * Main.rand.NextFloat(0.6f, 1.8f), -Main.rand.NextFloat(1.5f, 3f)),
                    new Color(190, 150, 70), Main.rand.NextFloat(0.45f, 0.6f))
                    ?.Configure(new Color(255, 224, 150), Main.rand.Next(20, 30), 0.6f);
            }
        }

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (!VaultUtils.isServer) {
                //两拍：新鼓上位闷响、插鼓到位脆响
                SoundEngine.PlaySound(index == 1
                    ? SoundID.Grab with { Volume = 0.7f, Pitch = -0.2f }
                    : SoundID.Unlock with { Volume = 0.8f, Pitch = 0.3f }, player.Center);
            }
        }

        //==================== 牙弹表现（MarkData=1） ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (router.MarkData < 1f || VaultUtils.isServer) {
                return;
            }
            //鲨齿曳光：青白牙尖 + 短涂抹
            Lighting.AddLight(proj.Center, 0.12f, 0.3f, 0.34f);
            if (proj.timeLeft % 2 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.35f,
                    -proj.velocity * 0.05f, new Color(185, 240, 250), Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(new Color(60, 170, 200), Main.rand.Next(8, 13), 0.15f, 0.7f);
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (router.MarkData < 1f) {
                return;
            }
            target.AddBuff(BuffID.Bleeding, 150);
            //咬合小爆：族内共享风味 1（owner 权威生成，径 44px）
            if (proj.owner == Main.myPlayer) {
                Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsGunsEarlyBurstProj>(),
                    Math.Max(1, proj.damage / 2), 0f, proj.owner, 44f, 1f);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.6f, Pitch = -0.3f }, target.Center);
            }
        }
    }

    /// <summary>
    /// 迷你鲨手持弹幕：射速爬升、散布发散、枪口上跳、鼓匣联动全部自管（镜像 GsChainGun 射击循环）。<br/>
    /// 嗜血由各端同步的 DownLeft 输入流确定性积分；弹匣余量只在 owner 端有意义，
    /// 干仓经 ai[2] 过线让远端同步收枪
    /// </summary>
    internal class GsMinisharkHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override LocalizedText DisplayName => Language.GetText("ItemName.Minishark");

        private static readonly Color SharkTeal = new(120, 220, 235);
        private static readonly Color MuzzleFire = new(255, 214, 150);

        /// <summary>嗜血满值（约 3 秒持续开火）</summary>
        private const float FrenzyMax = 180f;
        /// <summary>停火收枪延时</summary>
        private const int IdleKillDelay = 40;

        private float frenzy;
        private bool frenzyLatch;       //入狂潮的一次性音画闩
        private int fireTimer;
        private int idleTimer;
        private int shotCounter;
        private int dryTimer;
        private float recoilAnim;
        private float muzzleClimb;      //枪口上跳积角（弧度）
        private bool perfectConsumed;   //完美整匣起手只吃一次

        private bool Dry => Projectile.ai[2] > 0f;
        private float Frenzy01 => frenzy / FrenzyMax;
        private bool InFrenzy => frenzy >= FrenzyMax * 0.95f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
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
            if (!GameModeSystem.GodSmithActive || Item.type != ItemID.Minishark
                || Owner.dead || !Owner.active || Owner.noItems) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 2;

            GsGunsEarlyPlayer mp = Owner.GetModPlayer<GsGunsEarlyPlayer>();

            //完美整匣：出生首帧吃半量嗜血（owner 本地节拍层，远端无需一致）
            if (!perfectConsumed) {
                perfectConsumed = true;
                if (Projectile.IsOwnedByLocalPlayer() && mp.perfectMag) {
                    frenzy = FrenzyMax * 0.5f;
                }
            }

            //装填被触发（右键战术换鼓）即收枪
            if (Projectile.IsOwnedByLocalPlayer() && mp.reloadDuration > 0) {
                Projectile.Kill();
                return;
            }

            UpdatePose();
            HandleTacticalSwap(mp);

            bool wantFire = DownLeft && !Owner.CCed && !Dry;
            if (wantFire) {
                idleTimer = 0;
                frenzy = MathF.Min(frenzy + 1f, FrenzyMax);
                if (InFrenzy && !frenzyLatch) {
                    frenzyLatch = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f, Pitch = -0.2f }, Projectile.Center);
                        CombatText.NewText(Owner.getRect(), SharkTeal, GsMinishark.FrenzyText.Value);
                        PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, SharkTeal, 0f)
                            ?.Configure(0.04f, 0.4f, 12);
                    }
                }
                //狂潮负重：全速倾泻拖慢移动（owner 权威位置随原生同步）
                if (InFrenzy && Projectile.IsOwnedByLocalPlayer() && !Owner.mount.Active) {
                    Owner.velocity.X *= 0.95f;
                }

                float atkSpeed = Owner.GetWeaponAttackSpeed(Item);
                if (atkSpeed <= 0f) {
                    atkSpeed = 1f;
                }
                //失控曲线：8t 爬到 5t
                float baseInterval = MathHelper.Lerp(8f, 5f, Frenzy01);
                int interval = Math.Max(1, (int)MathF.Round(baseInterval / atkSpeed));
                if (++fireTimer >= interval) {
                    fireTimer = 0;
                    FireOnce(mp);
                }
            }
            else {
                idleTimer++;
                frenzy = MathF.Max(0f, frenzy - 2.5f);
                if (frenzy <= 0f) {
                    frenzyLatch = false;
                }
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

            recoilAnim = MathF.Max(0f, recoilAnim - 0.22f);
            muzzleClimb = MathF.Max(0f, muzzleClimb - 0.0035f);
            Lighting.AddLight(GsGunPose.MuzzlePos(Projectile, DirSign, 32f, -2f),
                MuzzleFire.ToVector3() * (recoilAnim * 0.5f + Frenzy01 * 0.2f));
        }

        /// <summary>右键战术换鼓：委托方案起装填，本体下一帧看到装填态自杀收枪</summary>
        private void HandleTacticalSwap(GsGunsEarlyPlayer mp) {
            if (!Projectile.IsOwnedByLocalPlayer() || !DownRight) {
                return;
            }
            if (mp.magLeft < 50 && mp.reloadDuration <= 0
                && GodSmithScheme.TryGetScheme(ItemID.Minishark, out GodSmithScheme scheme)
                && scheme is GsMinishark shark) {
                shark.HeldStartReload(Item, Owner, tactical: mp.magLeft > 0);
            }
        }

        /// <summary>枪身姿态：嗜血越高震颤越大 + 枪口上跳积角，identity 定相</summary>
        private void UpdatePose() {
            float shake = 0f;
            if (frenzy > 0f) {
                shake = MathF.Sin(Main.GameUpdateCount * 2.1f + Projectile.identity) * 0.016f * Frenzy01;
            }
            GsGunPose.Update(this, 20f, -4f,
                recoilAnim * 0.04f + muzzleClimb + shake, recoilAnim * 2.4f, 0.38f);
        }

        /// <summary>一发：owner 走原版弹药链（33% 省弹保留），各端播音画节拍</summary>
        private void FireOnce(GsGunsEarlyPlayer mp) {
            recoilAnim = 1f;
            muzzleClimb = MathF.Min(muzzleClimb + 0.006f * Frenzy01, 0.07f);
            shotCounter++;

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item11 with {
                    Volume = 0.3f,
                    Pitch = 0.15f + Frenzy01 * 0.2f + (Projectile.identity % 5) * 0.01f,
                    MaxInstances = 4
                }, Projectile.Center);
                if (shotCounter % 3 == 0) {
                    Vector2 ejectPos = Projectile.Center - Projectile.rotation.ToRotationVector2() * 4f;
                    PRTLoader.NewParticle<PRT_ProcChip>(ejectPos,
                        new Vector2(-DirSign * Main.rand.NextFloat(1.4f, 2.8f), -Main.rand.NextFloat(2f, 3.4f)),
                        new Color(190, 150, 70), Main.rand.NextFloat(0.5f, 0.7f))
                        ?.Configure(new Color(255, 224, 150), Main.rand.Next(20, 30));
                }
            }

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            if (mp.magLeft <= 0) {
                //鼓匣打空：起装填并收枪
                if (GodSmithScheme.TryGetScheme(ItemID.Minishark, out GodSmithScheme s) && s is GsMinishark shark) {
                    shark.HeldStartReload(Item, Owner, false);
                }
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

            //失控散布：0.5° 发散到 5°
            float spread = MathHelper.ToRadians(MathHelper.Lerp(0.5f, 5f, Frenzy01));
            //狂潮牙弹：每第 4 发（完美整匣期每第 3 发）
            bool tooth = InFrenzy && shotCounter % (mp.perfectMag ? 3 : 4) == 0;
            if (GodSmithScheme.TryGetScheme(ItemID.Minishark, out GodSmithScheme ms) && ms is GsMinishark markShark) {
                markShark.SetPendingMark(tooth ? 1f : 0f);
            }
            Vector2 muzzle = GsGunPose.MuzzlePos(Projectile, DirSign, 32f, -2f);
            Vector2 vel = (ToMouseA + Main.rand.NextFloat(-spread, spread)).ToRotationVector2() * speed;
            int dmg = tooth ? (int)(damage * 1.3f) : damage;
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), muzzle, vel,
                projToShoot, Math.Max(1, dmg), knockback, Owner.whoAmI);

            if (mp.magLeft <= 0) {
                if (GodSmithScheme.TryGetScheme(ItemID.Minishark, out GodSmithScheme s2) && s2 is GsMinishark shark2) {
                    shark2.HeldStartReload(Item, Owner, false);
                }
                Projectile.Kill();
            }
        }

        private void SetDry(bool value) {
            if (Dry != value) {
                Projectile.ai[2] = value ? 1f : 0f;
                NetUpdate();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //嗜血鲨腮辉光：青色随嗜血升亮，闪烁 identity 定相
            float flicker = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Projectile.identity * 0.67f);
            Color gill = Frenzy01 > 0.05f ? SharkTeal * (Frenzy01 * 0.5f * flicker) : Color.Transparent;
            GsGunPose.DrawGunBody(ItemID.Minishark, Projectile.Center, Projectile.rotation, DirSign,
                lightColor, 1f, gill);

            //枪口焰星，射后两三帧
            if (recoilAnim > 0.4f) {
                Texture2D star = CWRAsset.StarTexture?.Value;
                if (star != null) {
                    Vector2 muzzle = GsGunPose.MuzzlePos(Projectile, DirSign, 36f, -2f) - Main.screenPosition;
                    Color c = MuzzleFire * (recoilAnim * 0.8f);
                    c.A = 0;
                    float rot = Projectile.rotation + Projectile.identity * 1.7f;
                    Main.EntitySpriteDraw(star, muzzle, null, c, rot,
                        star.Size() / 2f, 0.1f * recoilAnim, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(star, muzzle, null, c * 0.6f, rot + MathHelper.PiOver4,
                        star.Size() / 2f, 0.06f * recoilAnim, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
