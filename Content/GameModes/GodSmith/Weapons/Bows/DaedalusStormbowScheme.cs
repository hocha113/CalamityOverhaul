using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows
{
    /// <summary>
    /// 代达罗斯风暴弓（重铸 ~110%）：神圣淬银的机构弓。
    /// 身份宣言：①每一矢都自天穹落下②满充铺开三三天矩，
    /// 角、边、心按拍次第落矢③终结拍中心重矢落地绽圣爆。
    /// 天降改制：原版随机天雨换成「锁定点阵」——落点确定可学，地下自动压低落高。
    /// 期望：普通 3×0.95≈86% 基线；矩阵每 8 发（8×0.62+2.3+爆 0.9≈8.2）→ 周期 ≈106%；天罚处决 ≈+3%
    /// </summary>
    internal class GsDaedalusStormbow : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.DaedalusStormbow;

        protected override string GsDescFallback =>
            "Reforged: arrows now fall from the sky, and a silver seal marks each landing point before the bolt strikes\nShots build storm charge; at full charge the next shot lays a 3x3 sky matrix, corners then edges then center\nThe center bolt lands heaviest and bursts on impact.\nSky bolts stack hunt brands; branding a foe thrice calls a verdict bolt down upon it";
        protected override int VolleyCount => 9;
        protected override float ChargePerShot => 12.5f;
        protected override int MarksPerVolleyHit => 1;
        protected override int PursuitEvery => 0;

        //==================== 本弓角色 ====================

        /// <summary>普通天矢（0.95）</summary>
        internal const int RoleSky = GsVolleyRole.CustomBase;
        /// <summary>矩阵外圈矢（0.62）</summary>
        internal const int RoleMatrix = GsVolleyRole.CustomBase + 1;
        /// <summary>矩阵中心重矢（2.3，落地圣爆）</summary>
        internal const int RoleCore = GsVolleyRole.CustomBase + 2;
        /// <summary>天罚处决矢（追坠标记敌，MarkData2 = 目标 whoAmI）</summary>
        internal const int RoleVerdict = GsVolleyRole.CustomBase + 3;

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
        /// 投放一支天矢：立即种下落点锁定体，错帧后从落点上空放矢。
        /// verdictTarget ≥0 时为天罚矢（锁定体随敌、矢咬向敌）
        /// </summary>
        private void DropSkyBolt(Player player, Vector2 land, int projType, int damage, float knockback,
            float speed, int role, int cellIndex, int delay, int verdictTarget = -1) {
            GsVolleyPlayer vp = player.GetModPlayer<GsVolleyPlayer>();
            float height = FindDropHeight(land);
            //出手初速压至 0.75，飞行相逐帧提速到 1.3 倍，读作「坠而愈疾」
            Vector2 spawn = land + new Vector2((cellIndex % 3 - 1) * 6f, -height);
            Vector2 vel = (land - spawn).SafeNormalize(Vector2.UnitY) * speed * 0.75f;
            int lockDelay = delay + (int)(height / (speed * 1.05f)) + 2;

            //落点锁定体：中心矢锁定帧带音效
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

        /// <summary>矩阵收束：3×3 落点按 角→边→心 的节奏铺开，中心重矢终结</summary>
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
        }

        /// <summary>只有天矢与矩阵矢参与叠标（天罚矢是处决产物，不再回喂）</summary>
        protected override bool IsMarkingHit(Projectile proj, int role)
            => role == RoleSky || role == RoleMatrix;

        /// <summary>处决「天罚」：锁死标记之敌，一支重矢自天穹追坠而下</summary>
        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone) {
            Vector2 speedRef = proj.velocity;
            float speed = MathF.Max(13f, speedRef.Length() / 1.2f);
            DropSkyBolt(player, target.Center, proj.type, (int)(proj.damage * 1.3f), 3f,
                speed, RoleVerdict, 4, 6, target.whoAmI);
        }

        /// <summary>矩阵中心重矢消亡：圣爆（终结拍）</summary>
        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if ((int)router.MarkData == RoleCore && proj.IsOwnedByLocalPlayer()) {
                SpawnBurst(Main.player[proj.owner], proj.Center, (int)(proj.damage * 0.4f), 110f,
                    GsVolleyBurstProj.ThemeHoly);
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
    }

    /// <summary>
    /// 风暴弓落点锁定体：隐形编排弹幕，锁定帧到达时中心矢变体播放一记锁定音后自清。
    /// ai[0] = 锁定帧 + (目标whoAmI+1)×1000（天罚体随敌移动），ai[1] = 变体（天矢/矩阵/中心/天罚）。
    /// 零伤不绘制，作为真弹幕过线只承载各端一致的锁定拍
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
            //天罚体钉在标记敌身上随行
            if ((int)Variant == VariantVerdict) {
                int idx = TargetIndex;
                NPC target = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
                if (target != null && target.active) {
                    Projectile.Center = target.Center;
                }
            }
            //锁定帧：中心矢一记锁定音
            if ((int)Life == LockDelay && !VaultUtils.isServer && (int)Variant == VariantCore) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
            }
            if (Life > LockDelay + 14) {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
