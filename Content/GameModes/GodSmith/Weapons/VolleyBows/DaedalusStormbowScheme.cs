using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles;
using CalamityOverhaul.Content.GameModes.UI;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    /// <summary>
    /// 代达罗斯风暴弓（重铸 ~110%）：神圣淬银的机构弓。
    /// 身份宣言：①每一矢都自天穹落下，落点先亮起旋转银印做预告②满充铺开三三天矩，
    /// 角、边、心按拍次第落矢③终结拍中心重矢落地绽圣爆，八道银芒向心收束。
    /// 天降改制：原版随机天雨换成「银印锁定点阵」——落点确定可学，地下自动压低落高。
    /// 期望：普通 3×0.95≈86% 基线；矩阵每 8 发（8×0.62+2.3+爆 0.9≈8.2）→ 周期 ≈106%；天罚处决 ≈+3%
    /// </summary>
    internal class GsDaedalusStormbow : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.DaedalusStormbow;

        protected override string GsDescFallback =>
            "Reforged: arrows now fall from the sky, and a silver seal marks each landing point before the bolt strikes\nShots build storm charge; at full charge the next shot lays a 3x3 sky matrix, corners then edges then center\nThe center bolt lands heaviest and bursts on impact. Right click to release a partial matrix early\nSky bolts stack hunt brands; branding a foe thrice calls a verdict bolt down upon it";

        //==================== 家族参数 ====================

        protected override int VolleyCount => 9;
        protected override float ChargePerShot => 12.5f;
        protected override int MarksPerVolleyHit => 1;
        protected override int PursuitEvery => 0;
        protected override Color TrailColor => new(188, 222, 255);

        //==================== 本弓角色 ====================

        /// <summary>普通天矢（0.95）</summary>
        internal const int RoleSky = GsVolleyRole.CustomBase;
        /// <summary>矩阵外圈矢（0.62）</summary>
        internal const int RoleMatrix = GsVolleyRole.CustomBase + 1;
        /// <summary>矩阵中心重矢（2.3，落地圣爆）</summary>
        internal const int RoleCore = GsVolleyRole.CustomBase + 2;
        /// <summary>天罚处决矢（追坠标记敌，MarkData2 = 目标 whoAmI）</summary>
        internal const int RoleVerdict = GsVolleyRole.CustomBase + 3;

        private static readonly Color SilverBright = new(226, 240, 255);
        private static readonly Color SilverDeep = new(110, 140, 200);

        //==================== 天矢投放（owner 端） ====================

        /// <summary>
        /// 找落点上方的可用落高：优先满高，洞穴里逐级压低，保证地下也能用。
        /// 返回天窗高度 px（最低 96）
        /// </summary>
        private static float FindDropHeight(Vector2 land) {
            Span<float> tiers = [560f, 420f, 300f, 190f, 96f];
            foreach (float h in tiers) {
                if (Collision.CanHitLine(land + new Vector2(0f, -h), 1, 1, land, 1, 1)) {
                    return h;
                }
            }
            return 96f;
        }

        /// <summary>
        /// 投放一支天矢：立即种下银印，错帧后从落点上空放矢。
        /// verdictTarget ≥0 时为天罚矢（银印随敌、矢咬向敌）
        /// </summary>
        private void DropSkyBolt(Player player, Vector2 land, int projType, int damage, float knockback,
            float speed, int role, int cellIndex, int delay, int verdictTarget = -1) {
            GsVolleyPlayer vp = player.GetModPlayer<GsVolleyPlayer>();
            float height = FindDropHeight(land);
            //出手初速压至 0.75，飞行相逐帧提速到 1.3 倍，读作「坠而愈疾」
            Vector2 spawn = land + new Vector2((cellIndex % 3 - 1) * 6f, -height);
            Vector2 vel = (land - spawn).SafeNormalize(Vector2.UnitY) * speed * 0.75f;
            int lockDelay = delay + (int)(height / (speed * 1.05f)) + 2;

            //银印：预告 + 锁定 + 余辉，一体三相
            int variant = role == RoleCore ? GsDaedalusSigilProj.VariantCore
                : role == RoleVerdict ? GsDaedalusSigilProj.VariantVerdict
                : role == RoleMatrix ? GsDaedalusSigilProj.VariantMatrix : GsDaedalusSigilProj.VariantSky;
            float packedLock = lockDelay + (verdictTarget + 1) * 1000;
            Projectile.NewProjectile(player.GetSource_Misc("GsDaedalusSigil"), land, Vector2.Zero,
                ModContent.ProjectileType<GsDaedalusSigilProj>(), 0, 0f, player.whoAmI, packedLock, variant);

            vp.Enqueue(new GsPendingShot {
                Delay = Math.Max(1, delay),
                WeaponType = player.HeldItem.type,
                ProjType = projType,
                Velocity = vel,
                Damage = damage,
                Knockback = knockback,
                Role = role,
                Param = role == RoleVerdict ? verdictTarget : cellIndex,
                AbsolutePos = true,
                Pos = spawn,
            });
        }

        /// <summary>普通射击：三矢点阵替换口部出箭，落点绕准星横列、逐发交替镜像成风暴节奏</summary>
        protected override bool? OnNormalShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 aim = Main.MouseWorld;
            float speed = MathF.Max(13f, velocity.Length());
            int boltDamage = (int)(damage * 0.95f);
            //点阵横列 ±64px；奇偶发镜像错列，连射时读作左右交替的风暴拍
            float mirror = shotCounter % 2 == 0 ? 1f : -1f;
            for (int i = 0; i < 3; i++) {
                float offsetX = (i - 1) * 64f * mirror;
                Vector2 land = aim + new Vector2(offsetX, MathF.Abs(offsetX) * 0.1f);
                DropSkyBolt(player, land, type, boltDamage, knockback, speed, RoleSky, i, 1 + i * 4);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.55f, Pitch = 0.45f }, position);
            }
            return false;
        }

        /// <summary>矩阵收束：3×3 银印按 角→边→心 的节奏铺开，中心重矢终结</summary>
        protected override void FireVolley(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int count) {
            count = Math.Clamp(count, 4, 9);
            Vector2 center = Main.MouseWorld;
            float speed = MathF.Max(13f, velocity.Length());
            //九宫格铺开顺序：四角（0~3 拍）→ 四边（4~7 拍）→ 中心（终结拍）
            Span<int> order = [0, 2, 6, 8, 1, 3, 5, 7, 4];
            const float step = 74f;
            for (int n = 0; n < count; n++) {
                int cell = order[n];
                bool isCore = cell == 4 && count >= 9;
                Vector2 land = center + new Vector2(cell % 3 - 1, cell / 3 - 1) * step;
                int delay = n < 4 ? 1 + n * 4 : n < 8 ? 18 + (n - 4) * 4 : 42;
                int dmg = isCore ? (int)(damage * 2.3f) : (int)(damage * 0.62f);
                DropSkyBolt(player, land, type, dmg, isCore ? knockback * 1.6f : knockback * 0.6f,
                    speed, isCore ? RoleCore : RoleMatrix, cell, delay);
            }
        }

        //==================== 弹幕增强 ====================

        private class SkyBoltState
        {
            public int T;
            public float TopSpeed;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role < GsVolleyRole.CustomBase) {
                base.GsProjPostAI(proj, router);
                return;
            }
            SkyBoltState st = router.GetOrCreateState<SkyBoltState>();
            st.T++;
            //坠而愈疾：初速 0.75 → 1.3 倍封顶（确定性，各端同式）
            if (st.TopSpeed <= 0f) {
                st.TopSpeed = proj.velocity.Length() * 1.73f;
            }
            if (proj.velocity.Length() < st.TopSpeed) {
                proj.velocity *= 1.03f;
            }
            //天罚矢横向咬标：目标 whoAmI 随 MarkData2 过线，各端确定性一致
            if (role == RoleVerdict) {
                int idx = (int)router.MarkData2;
                NPC target = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
                if (target != null && target.active) {
                    float wantX = MathHelper.Clamp((target.Center.X - proj.Center.X) * 0.06f, -4f, 4f);
                    proj.velocity.X = MathHelper.Lerp(proj.velocity.X, wantX, 0.14f);
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            bool heavy = role == RoleCore || role == RoleVerdict;
            Lighting.AddLight(proj.Center, SilverBright.ToVector3() * (heavy ? 0.42f : 0.22f));
            if (st.T % (heavy ? 2 : 4) == 0) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.5f,
                    -proj.velocity * 0.04f, TrailColor, heavy ? 0.13f : 0.09f)?.Configure(9, 0.8f);
            }
            if (st.T % 9 == 0) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(proj.Center, -proj.velocity * 0.12f,
                    heavy ? GameModeTheme.GodSmithEmber : SilverBright, heavy ? 0.4f : 0.26f)
                    ?.Configure(false, 12);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role < GsVolleyRole.CustomBase) {
                return base.GsProjPreDraw(proj, ref lightColor, router);
            }
            bool heavy = role == RoleCore || role == RoleVerdict;
            DrawSpeedGhost(proj, heavy ? GameModeTheme.GodSmithEmber : TrailColor, heavy ? 0.5f : 0.36f);
            if (heavy) {
                //重矢白热芯：本体上叠一层放大的加色残影
                Main.instance.LoadProjectile(proj.type);
                Texture2D tex = TextureAssets.Projectile[proj.type].Value;
                float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + proj.identity * 0.61f);
                Main.EntitySpriteDraw(tex, proj.Center - Main.screenPosition, null,
                    (Color.White with { A = 0 }) * (0.4f * pulse), proj.rotation,
                    tex.Size() * 0.5f, 1.22f, SpriteEffects.None, 0);
            }
            return null;
        }

        /// <summary>只有天矢与矩阵矢参与叠标（天罚矢是处决产物，不再回喂）</summary>
        protected override bool IsMarkingHit(Projectile proj, int role)
            => role == RoleSky || role == RoleMatrix;

        /// <summary>命中反馈：银屑迸散；重矢命中附加圣音与星环</summary>
        protected override void OnMarkedProjHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (VaultUtils.isServer) {
                return;
            }
            int role = (int)router.MarkData;
            int count = role == RoleCore ? 7 : 3;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    (-proj.velocity.SafeNormalize(Vector2.UnitY)).RotatedByRandom(0.8) * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? SilverBright : SilverDeep,
                    Main.rand.NextFloat(0.26f, 0.44f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        /// <summary>处决「天罚」：银印锁死标记之敌，一支重矢自天穹追坠而下</summary>
        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone) {
            Vector2 speedRef = proj.velocity;
            float speed = MathF.Max(13f, speedRef.Length() / 1.2f);
            DropSkyBolt(player, target.Center, proj.type, (int)(proj.damage * 1.3f), 3f,
                speed, RoleVerdict, 4, 6, target.whoAmI);
        }

        /// <summary>矩阵中心重矢消亡：圣爆 + 八道银芒向心收束（终结拍）</summary>
        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role < GsVolleyRole.CustomBase) {
                return;
            }
            if (role == RoleCore && proj.IsOwnedByLocalPlayer()) {
                SpawnBurst(Main.player[proj.owner], proj.Center, (int)(proj.damage * 0.4f), 110f,
                    GsVolleyBurstProj.ThemeHoly);
            }
            if (VaultUtils.isServer) {
                return;
            }
            if (role == RoleCore) {
                //八道银芒自外圈格位向心收束，矩阵在此收笔
                for (int cell = 0; cell < 9; cell++) {
                    if (cell == 4) {
                        continue;
                    }
                    Vector2 from = proj.Center + new Vector2(cell % 3 - 1, cell / 3 - 1) * 74f;
                    PRTLoader.NewParticle<PRT_SkyBolt>(from, Vector2.Zero, SilverBright, 0.7f)
                        ?.Configure(from, proj.Center, 16);
                }
            }
            //落地银屑：向上迸散回落，比矢体活得久（余痕相）
            int sparkCount = role == RoleCore ? 6 : 2;
            for (int i = 0; i < sparkCount; i++) {
                PRTLoader.NewParticle<PRT_Spark>(proj.Center + Main.rand.NextVector2Circular(5f, 5f),
                    new Vector2(Main.rand.NextFloat(-1.4f, 1.4f), -Main.rand.NextFloat(0.8f, 2.4f)),
                    Main.rand.NextBool() ? SilverBright : TrailColor,
                    Main.rand.NextFloat(0.28f, 0.46f))?.Configure(true, Main.rand.Next(18, 30));
            }
        }

        //==================== 动画：天射后坐 ====================

        /// <summary>
        /// 天射后坐：出手瞬间弓身沿举天方向反坐 3px 并指数回坐（仅位移，确定性输入）。
        /// 充能满时使用中弓身细颤，读作风暴在弦上待发
        /// </summary>
        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float elapsed = 1f - player.itemAnimation / (float)player.itemAnimationMax;
            float kick = MathF.Exp(-4.6f * elapsed);
            //弓已被原版指向天穹，沿持弓方向反坐
            Vector2 aimDir = player.itemRotation.ToRotationVector2() * player.direction;
            player.itemLocation -= aimDir * (3.2f * kick);
            player.itemLocation.Y += 1.1f * kick * player.gravDir;
            if (player.whoAmI == Main.myPlayer
                && player.GetModPlayer<GsVolleyPlayer>().Charge >= 100f) {
                player.itemLocation.X += MathF.Sin(Main.GlobalTimeWrappedHourly * 42f) * 0.7f;
            }
        }

        /// <summary>充能满持弓待机：银尘自弓身升腾（本机个人读数）</summary>
        public override void GsHoldItem(Item item, Player player) {
            if (VaultUtils.isServer || player.whoAmI != Main.myPlayer) {
                return;
            }
            if (player.GetModPlayer<GsVolleyPlayer>().Charge < 100f || !Main.rand.NextBool(6)) {
                return;
            }
            Vector2 at = player.MountedCenter + new Vector2(player.direction * Main.rand.NextFloat(6f, 18f),
                -Main.rand.NextFloat(2f, 12f));
            PRTLoader.NewParticle<PRT_Light>(at, new Vector2(0f, -0.7f), SilverBright, 0.07f)?.Configure(14, 0.7f);
        }
    }

    /// <summary>
    /// 风暴弓银印：天矢落点的三相预告体（张开 → 锁定 → 余辉）。
    /// ai[0] = 锁定帧 + (目标whoAmI+1)×1000（天罚印随敌移动），ai[1] = 变体（天矢/矩阵/中心/天罚）。
    /// 零伤纯演出，但作为真弹幕过线，各端都看得到落点预告；绘制全走 identity 定相，无随机
    /// </summary>
    internal class GsDaedalusSigilProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int VariantSky = 0;
        internal const int VariantMatrix = 1;
        internal const int VariantCore = 2;
        internal const int VariantVerdict = 3;

        private ref float PackedLock => ref Projectile.ai[0];
        private ref float Variant => ref Projectile.ai[1];
        private ref float Life => ref Projectile.localAI[0];

        private int LockDelay => (int)PackedLock % 1000;
        private int TargetIndex => (int)PackedLock / 1000 - 1;

        private static readonly Color SigilSilver = new(206, 230, 255);

        private Color BaseColor => (int)Variant switch {
            VariantCore => Color.Lerp(GameModeTheme.GodSmithEmber, Color.White, 0.2f),
            VariantVerdict => GameModeTheme.GodSmithAccent,
            _ => SigilSilver,
        };

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            //天罚印钉在标记敌身上随行
            if ((int)Variant == VariantVerdict) {
                int idx = TargetIndex;
                NPC target = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
                if (target != null && target.active) {
                    Projectile.Center = target.Center;
                }
            }
            //锁定帧：星环脉冲一记
            if ((int)Life == LockDelay && !VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, BaseColor, 0.08f)
                    ?.Configure(0.08f, 0.5f, 12);
                if ((int)Variant == VariantCore) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
                }
            }
            if (Life > LockDelay + 14) {
                Projectile.Kill();
                return;
            }
            Lighting.AddLight(Projectile.Center, BaseColor.ToVector3() * 0.2f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            Texture2D line = VaultAsset.placeholder2?.Value;
            if (glow == null || star == null || line == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            int lockDelay = Math.Max(1, LockDelay);
            float grow = MathHelper.Clamp(Life / 9f, 0f, 1f);
            grow = 1f - (1f - grow) * (1f - grow);
            float fade = MathHelper.Clamp((lockDelay + 14 - Life) / 10f, 0f, 1f);
            float lockP = MathHelper.Clamp(Life / lockDelay, 0f, 1f);
            bool heavy = (int)Variant is VariantCore or VariantVerdict;
            float sizeK = heavy ? 1.35f : 1f;
            Color main = BaseColor with { A = 0 };
            float alpha = grow * fade;
            float spin = Life * 0.045f + Projectile.identity * 0.7f;

            //张开的旋印：正反双星 + 柔光芯
            Main.EntitySpriteDraw(glow, pos, null, main * (0.4f * alpha), 0f,
                glow.Size() * 0.5f, 0.34f * sizeK * grow, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, pos, null, main * (0.7f * alpha), spin,
                star.Size() * 0.5f, 0.13f * sizeK * grow, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, pos, null, (Color.White with { A = 0 }) * (0.45f * alpha), -spin * 0.7f,
                star.Size() * 0.5f, 0.08f * sizeK * grow, SpriteEffects.None, 0);

            //收束方框：四线旋转菱框随锁定进度向心收拢
            float radius = (30f - 13f * lockP) * sizeK;
            float frameRot = spin * 0.6f + MathHelper.PiOver4;
            for (int i = 0; i < 4; i++) {
                float a = frameRot + MathHelper.PiOver2 * i;
                Vector2 c1 = Projectile.Center + a.ToRotationVector2() * radius;
                Vector2 c2 = Projectile.Center + (a + MathHelper.PiOver2).ToRotationVector2() * radius;
                Vector2 seg = c2 - c1;
                Main.EntitySpriteDraw(line, c1 - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                    main * (0.55f * alpha), seg.ToRotation(), new Vector2(0f, 0.5f),
                    new Vector2(seg.Length(), 1.4f), SpriteEffects.None, 0);
            }

            //临锁引导束：落点向上 460px 的细银线，两端收口（底锚银印、顶端渐隐）
            float beamIn = MathHelper.Clamp((Life - (lockDelay - 16)) / 12f, 0f, 1f);
            if (beamIn > 0f && Life <= lockDelay + 4) {
                for (int seg = 0; seg < 3; seg++) {
                    float y0 = seg * 153f;
                    float segAlpha = 0.14f * beamIn * fade * (1f - seg * 0.32f);
                    Main.EntitySpriteDraw(line, pos + new Vector2(0f, -y0 - 153f), new Rectangle(0, 0, 1, 1),
                        main * segAlpha, MathHelper.PiOver2, new Vector2(0f, 0.5f),
                        new Vector2(153f, 2f - seg * 0.5f), SpriteEffects.None, 0);
                }
            }

            //锁定闪帧：整印提亮一拍
            if (Life >= lockDelay && Life <= lockDelay + 5) {
                float flash = 1f - (Life - lockDelay) / 5f;
                Main.EntitySpriteDraw(glow, pos, null, (Color.White with { A = 0 }) * (0.5f * flash), 0f,
                    glow.Size() * 0.5f, 0.4f * sizeK, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
