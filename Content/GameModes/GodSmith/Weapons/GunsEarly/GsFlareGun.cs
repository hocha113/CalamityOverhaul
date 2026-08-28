using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsEarly
{
    /// <summary>
    /// 信号枪「彩号信管」：救生信号枪·四色药罐。<br/>
    /// ①四色信管轮转（红/绿/蓝/白），钉进敌人身上持续燃照；
    /// ②被钉信管的敌人受你的一切伤害 +12%（白色压轴信管 +16%），信管即战术信标；
    /// ③彩罐旋换装填两拍（退罐/上罐）；完美装填下一发双信管齐射。<br/>
    /// 后坐 1.5px + 信号枪扬口。工具枪身份保留：钉不进敌人时照旧照明。<br/>
    /// 账目：本体伤害沿用原版信号弹（近零），价值全在 +12% 信标增伤（支援位），
    /// 无伤害行修饰（待游戏内标定）
    /// </summary>
    internal class GsFlareGun : GsMagazineScheme
    {
        public override int TargetItemID => ItemID.FlareGun;

        protected override string GsDescFallback =>
            "Reforged: a four-color canister, red, green, blue, then white, each flare pinning into flesh and burning there.\n" +
            "A pinned foe takes 12% more damage from you (16% for the white flare).\n" +
            "Swap canisters in two beats; a sweet-spot swap fires the next pull as a double flare";

        public override int MagSize => 4;
        public override int ReloadTicks => 40;
        public override GsReloadStyle Style => GsReloadStyle.Canister;
        protected override int ReloadCueCount => 2;
        protected override bool EjectsShell => false;
        protected override float GetRecoil(bool lastRound) => 1.5f;

        /// <summary>四色信管表（第四发白色压轴）</summary>
        internal static readonly Color[] FlarePalette = [
            new Color(240, 84, 70), new Color(96, 216, 120), new Color(90, 156, 240), new Color(245, 240, 225)];

        /// <summary>本发罐位（0..3）。Fire* 时余弹已被共享层扣 1，故减一还原</summary>
        private int CanisterIndex(GsGunsEarlyPlayer mp) => Math.Clamp(MagSize - mp.magLeft - 1, 0, 3);

        protected override bool? FireNormalRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => LaunchFlare(player, mp, source, position, velocity, damage, knockback);

        protected override bool? FireLastRound(Item item, Player player, GsGunsEarlyPlayer mp,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => LaunchFlare(player, mp, source, position, velocity, damage, knockback);

        /// <summary>发射信管：压掉原版信号弹；完美状态双管齐射</summary>
        private bool? LaunchFlare(Player player, GsGunsEarlyPlayer mp, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int damage, float knockback) {
            int color = CanisterIndex(mp);
            int volleys = mp.perfectNextShot ? 2 : 1;
            for (int i = 0; i < volleys; i++) {
                Vector2 vel = volleys > 1 ? velocity.RotatedBy((i == 0 ? -1 : 1) * 0.06f) : velocity;
                Projectile.NewProjectile(source, position, vel,
                    ModContent.ProjectileType<GsFlareGunSignalProj>(),
                    Math.Max(1, damage), knockback, player.whoAmI, color);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item11 with { Volume = 0.5f, Pitch = 0.55f }, position);
                Vector2 aim = velocity.SafeNormalize(Vector2.UnitX);
                PRTLoader.NewParticle<PRT_Smoke>(position + aim * 8f, aim * 1.4f,
                    new Color(180, 168, 150), Main.rand.NextFloat(0.04f, 0.06f))
                    ?.Configure(Main.rand.Next(14, 20), 0.35f, 0.02f);
            }
            return false;
        }

        //==================== 彩罐旋换 ====================

        protected override void OnReloadStart(Item item, Player player, GsGunsEarlyPlayer mp) {
            if (VaultUtils.isServer) {
                return;
            }
            //退罐：空药罐弹出
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.55f, Pitch = -0.2f }, player.Center);
            PRTLoader.NewParticle<PRT_ProcChip>(player.Center + new Vector2(player.direction * 6f, 0f),
                new Vector2(-player.direction * 1.2f, -2.2f),
                new Color(150, 60, 54), 0.8f)
                ?.Configure(new Color(240, 120, 100), 30, 0.6f);
        }

        protected override void OnReloadCue(Item item, Player player, GsGunsEarlyPlayer mp, int index, int total) {
            if (!VaultUtils.isServer) {
                //上罐旋扣两拍
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.65f, Pitch = -0.15f + 0.25f * index }, player.Center);
            }
        }

        //==================== 后坐姿态：信号枪扬口 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation -= new Vector2(player.direction, 0f) * (1.5f * progress);
            player.itemRotation -= player.direction * 0.12f * progress;
        }
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
    /// 抛物线飞行，钉敌燃照 8 秒（升烟柱 + 呼吸光晕 + 烬滴），钉不进就落地照明；
    /// 信管本体自绘（同色芯光 + 十字闪 + 烟），identity 定相
    /// </summary>
    internal class GsFlareGunSignalProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int ColorIndex => Math.Clamp((int)Projectile.ai[0], 0, 3);
        private Color FlareColor => GsFlareGun.FlarePalette[ColorIndex];
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

            //燃照：呼吸光 + 升烟 + 烬滴
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GameUpdateCount * 0.22f + Seed * MathHelper.TwoPi);
            Lighting.AddLight(Projectile.Center, FlareColor.ToVector3() * (0.9f * pulse));
            if (!VaultUtils.isServer) {
                if (Main.GameUpdateCount % 8 == Projectile.identity % 8) {
                    PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center,
                        -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.1f),
                        new Color(120, 112, 104), Main.rand.NextFloat(0.03f, 0.05f))
                        ?.Configure(Main.rand.Next(20, 32), 0.35f, 0.015f);
                }
                if (Stuck && Main.GameUpdateCount % 10 == Projectile.identity % 10) {
                    PRTLoader.NewParticle<PRT_DefEmber>(Projectile.Center,
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(0.4f, 1f)),
                        FlareColor, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(Main.rand.Next(14, 22), 0.08f);
                }
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
                PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero, FlareColor, 0f)
                    ?.Configure(0.035f, 0.32f, 10);
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

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || star == null) {
                return false;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + Seed * 10f);

            //信管光晕（钉附后更亮，作信标读数）
            Color halo = FlareColor * ((Stuck ? 0.75f : 0.55f) * pulse);
            halo.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, halo, 0f, glow.Size() / 2f,
                Stuck ? 0.4f : 0.28f, SpriteEffects.None, 0);
            //白热芯
            Color core = Color.Lerp(FlareColor, Color.White, 0.6f);
            core.A = 0;
            Main.EntitySpriteDraw(glow, drawPos, null, core, 0f, glow.Size() / 2f, 0.1f, SpriteEffects.None, 0);
            //十字信号闪
            Color cross = FlareColor * (0.9f * pulse);
            cross.A = 0;
            Main.EntitySpriteDraw(star, drawPos, null, cross, Seed * MathHelper.TwoPi,
                star.Size() / 2f, 0.2f * pulse, SpriteEffects.None, 0);
            return false;
        }
    }
}
