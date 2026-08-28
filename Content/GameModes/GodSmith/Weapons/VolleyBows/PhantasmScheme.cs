using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows
{
    /// <summary>
    /// 幻影弓 / Phantasm（重铸 ~100%~110%）：星旋虚空织成的涡潮弓。
    /// 身份宣言：①按住不放持续引弓，射速随狂乱层攀升，弓身随层数裂出幻影残弓
    /// ②对同一目标连击六次唤出幻影射手（复用族内 GsPhantomArcherProj，悬于身后随你齐射魂箭）
    /// ③处决时幻影射手四连魂矢追钉该敌。原版隐形手持弹幕换成自绘 held：
    /// 拨弦臂姿、逐层残弓、狂乱星芒全程可见。
    /// 期望：满狂乱 3×0.82/7f + 侧翼 2×0.3 + 射手 0.25 ≈ 28.3/60f，对原版满疾速 ≈30/60f ≈ 94%；
    /// 处决魂矢 ≈+8% → 周期 ≈102%。弹药按「每轮齐射判定一次」，经济略优于原版，视作重铸 QoL
    /// </summary>
    internal class GsPhantasm : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.Phantasm;

        protected override string GsDescFallback =>
            "Reforged: hold to channel; fire rate climbs with frenzy, and the bow splits into phantom after-bows\nAt frenzy rank 2 every volley gains two flanking phantom arrows\nStriking the same foe 6 times in a row calls a phantom archer to hover at your back and loose soul bolts alongside you\nMain arrows stack soul brands; branding a foe thrice makes the archer nail it with 4 homing soul bolts";

        //==================== 家族参数（射击流全由 held 驱动，族充能不启用） ====================

        protected override int MarksPerVolleyHit => 1;
        protected override int PursuitEvery => 0;
        protected override Color TrailColor => GsPhantomArcherProj.PhantomTeal;

        /// <summary>唤出幻影射手所需的同目标连击数</summary>
        internal const int ComboToSummon = 6;

        //==================== 使用接管 ====================

        /// <summary>手持在场即引弓中；生成只在 owner 端，没箭不举弓；全端压掉原版隐形 held</summary>
        public override bool? GsCanUseItem(Item item, Player player) {
            if (!HeldAlive<GsPhantasmHeld>(player) && player.whoAmI == Main.myPlayer
                && player.GetShootState().HasAmmo) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsPhantasmHeld>(), 1, 0f, player.whoAmI);
            }
            return false;
        }

        //==================== 弹幕表现 ====================

        /// <summary>只有主箭叠魂标（侧翼与魂矢不回喂，防处决过频）</summary>
        protected override bool IsMarkingHit(Projectile proj, int role) => role == GsVolleyRole.VolleyMain;

        /// <summary>幻影箭矢自绘：星旋青重影 + 虚空蓝罩层（identity 定相）</summary>
        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role == GsVolleyRole.None) {
                return null;
            }
            DrawSpeedGhost(proj, GsPhantomArcherProj.PhantomTeal, 0.46f);
            Main.instance.LoadProjectile(proj.type);
            Texture2D tex = TextureAssets.Projectile[proj.type].Value;
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + proj.identity * 0.71f);
            Main.EntitySpriteDraw(tex, proj.Center - Main.screenPosition, null,
                (GsPhantomArcherProj.PhantomBlue with { A = 0 }) * (0.32f * pulse), proj.rotation,
                tex.Size() * 0.5f, proj.scale * 1.06f, SpriteEffects.None, 0);
            return null;
        }

        /// <summary>命中：魂连击计数（owner 端本地量），六连唤出/续期幻影射手</summary>
        protected override void OnMarkedProjHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            int role = (int)router.MarkData;
            if (role != GsVolleyRole.VolleyMain && role != GsVolleyRole.VolleySide) {
                return;
            }
            Player owner = Main.player[proj.owner];
            if (GsHuntMarkNPC.CanMark(target)) {
                GsHuntMarkNPC mark = target.GetGlobalNPC<GsHuntMarkNPC>();
                mark.SoulCombo++;
                mark.SoulComboTimer = 55;
                if (mark.SoulCombo >= ComboToSummon) {
                    mark.SoulCombo = 0;
                    SummonOrRefreshArcher(owner);
                }
            }
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Light>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(0f, -Main.rand.NextFloat(0.5f, 1.2f)),
                    Main.rand.NextBool() ? GsPhantomArcherProj.PhantomTeal : GsPhantomArcherProj.PhantomBlue,
                    0.09f)?.Configure(12, 0.8f);
            }
        }

        /// <summary>找该玩家名下的幻影射手</summary>
        internal static Projectile FindArcher(Player player) {
            int type = ModContent.ProjectileType<GsPhantomArcherProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type) {
                    return proj;
                }
            }
            return null;
        }

        /// <summary>唤出或续满幻影射手（owner 端；至多 1 名/玩家，族资产契约）</summary>
        private static void SummonOrRefreshArcher(Player owner) {
            Projectile archer = FindArcher(owner);
            if (archer != null) {
                archer.timeLeft = 180;
            }
            else {
                Projectile.NewProjectile(owner.GetSource_Misc("GsPhantasmArcher"),
                    owner.Center + new Vector2(-owner.direction * 34f, -50f), Vector2.Zero,
                    ModContent.ProjectileType<GsPhantomArcherProj>(), 0, 0f, owner.whoAmI);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = -0.2f }, owner.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(owner.Center + new Vector2(-owner.direction * 34f, -50f),
                    Vector2.Zero, GsPhantomArcherProj.PhantomTeal, 0.1f)?.Configure(0.1f, 0.55f, 14);
            }
        }

        /// <summary>处决「魂矢四连」：幻影射手（无则自你身侧）连射四支追魂矢钉向死标之敌</summary>
        protected override void OnExecute(Player player, NPC target, Projectile proj, int damageDone) {
            Projectile archer = FindArcher(player);
            Vector2 from = archer?.Center ?? player.Center + new Vector2(-player.direction * 24f, -36f);
            int dmg = (int)(proj.damage * 0.5f);
            for (int i = 0; i < 4; i++) {
                Vector2 vel = (target.Center - from).SafeNormalize(Vector2.UnitX)
                    .RotatedBy((i - 1.5f) * 0.22f) * 13f;
                Projectile.NewProjectile(player.GetSource_Misc("GsPhantasmEcho"),
                    from + vel.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * (i - 1.5f) * 8f,
                    vel, ModContent.ProjectileType<GsPhantasmEchoArrowProj>(), dmg, 1.5f,
                    player.whoAmI, target.whoAmI);
            }
            if (archer != null && archer.ModProjectile is GsPhantomArcherProj archerProj) {
                archerProj.Recoil = 6f;
            }
        }
    }

    /// <summary>
    /// 幻影弓共享手持弹幕：按住左键持续引弓的狂乱射击循环。<br/>
    /// 狂乱：持续引弓 160 帧内射击间隔 12f → 7f，阶位 0~3；升阶时弓身裂出更多幻影残弓。
    /// 每轮齐射三矢紧束（中箭叠魂标）；阶 2 起加两翼幻影箭；幻影射手在场则随你齐发魂箭。<br/>
    /// 网络：owner 权威推进，ai[0] = 狂乱阶位（变更即 netUpdate），ai[1] = 展示弹药；
    /// 远端按同步的 DownLeft/ToMouse 画拨弦姿态，音效走本端演出计时（近似同拍）；
    /// 箭矢只在 owner 端生成；断弹/切物品/死亡由 owner 端自杀
    /// </summary>
    internal class GsPhantasmHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //held 本身不参与命中，不注册新键，显示名指向原版键
        public override LocalizedText DisplayName => Language.GetText("ItemName.Phantasm");

        /// <summary>狂乱阶位 0~3（owner 写，各端读）</summary>
        private ref float Rank => ref Projectile.ai[0];
        /// <summary>展示用弹药物品 ID</summary>
        private ref float AmmoShowType => ref Projectile.ai[1];

        /// <summary>持续引弓帧数（owner 端权威依据）</summary>
        private int channelFrames;
        /// <summary>射击循环计时（owner 端权威）</summary>
        private int fireTimer = 6;
        /// <summary>本端演出计时（拨弦动画与远端音效，近似跟拍）</summary>
        private int pluckTimer;
        private int pluckPeriod = 12;
        /// <summary>后坐余帧</summary>
        private int recoilTimer;
        /// <summary>升阶星芒余帧</summary>
        private int starTimer;
        /// <summary>音画侦测：上次观察到的阶位</summary>
        private int seenRank;

        private int boundBowType;
        private GsPhantasm scheme;

        private Vector2 BowCenter => Owner.GetPlayerStabilityCenter() + ToMouseA.ToRotationVector2() * 13f;

        private float Frenzy => MathHelper.Clamp(channelFrames / 160f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 6;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        public override void Initialize() {
            TryBind();
        }

        private bool TryBind() {
            Item item = Item;
            if (item == null || item.IsAir) {
                return false;
            }
            if (!GodSmithScheme.TryGetScheme(item.type, out GodSmithScheme raw) || raw is not GsPhantasm ps) {
                return false;
            }
            scheme = ps;
            boundBowType = item.type;
            return true;
        }

        public override void AI() {
            Projectile.timeLeft = 6;//丢包/掉线兜底自清

            if (scheme == null && !TryBind()) {
                if (Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.Kill();
                }
                return;
            }
            if (Projectile.IsOwnedByLocalPlayer()) {
                if (Owner.dead || !Owner.active || Owner.CCed || Item.type != boundBowType || !DownLeft) {
                    Projectile.Kill();
                    return;
                }
            }

            SetHeld();
            UpdatePose();
            UpdateArms();
            UpdateFrenzy();
            if (Projectile.IsOwnedByLocalPlayer()) {
                UpdateAmmoPreview();
                if (--fireTimer <= 0) {
                    FireBurst();
                }
            }
            WatchRankCues();
            if (recoilTimer > 0) {
                recoilTimer--;
            }
        }

        //==================== 狂乱与开火（owner 权威） ====================

        /// <summary>各端按同步的 DownLeft 自走狂乱计数；阶位以 owner 写的 ai[0] 为准</summary>
        private void UpdateFrenzy() {
            channelFrames = Math.Min(channelFrames + 1, 400);
            pluckPeriod = Math.Max(4, (int)MathF.Round(MathHelper.Lerp(12f, 7f, Frenzy)
                / MathF.Max(0.1f, Owner.GetWeaponAttackSpeed(Item))));
            if (++pluckTimer >= pluckPeriod) {
                pluckTimer = 0;
                //远端演出音：跟本端拨弦拍近似同步（owner 的真枪声在 FireBurst 里）
                if (!VaultUtils.isServer && !Projectile.IsOwnedByLocalPlayer()) {
                    SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.6f, Pitch = -0.05f + 0.3f * Frenzy },
                        Projectile.Center);
                }
            }
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            int wantRank = Frenzy >= 1f ? 3 : Frenzy >= 0.66f ? 2 : Frenzy >= 0.33f ? 1 : 0;
            if (wantRank != (int)Rank) {
                Rank = wantRank;
                NetUpdate();
            }
        }

        /// <summary>owner 端低频更新展示弹药（远端画搭箭贴图用）</summary>
        private void UpdateAmmoPreview() {
            ShootState state = Owner.GetShootState();
            int show = state.HasAmmo ? state.UseAmmoItemType : 0;
            if ((int)AmmoShowType != show) {
                AmmoShowType = show;
                NetUpdate();
            }
        }

        /// <summary>
        /// 一轮齐射：恰好一次 PickAmmo（原版幻影弓 2/3 免耗照常生效），三矢紧束出膛；
        /// 阶 2 起加两翼幻影箭，幻影射手在场则随射一支魂箭
        /// </summary>
        private void FireBurst() {
            if (!Owner.PickAmmo(Item, out int shootType, out float speed, out int damage, out float knockback,
                out int usedAmmoItemId, false)) {
                Projectile.Kill();
                return;
            }
            fireTimer = pluckPeriod;
            recoilTimer = 5;
            EntitySource_ItemUse_WithAmmo source = new(Owner, Item, usedAmmoItemId, "GsPhantasm");
            Vector2 muzzle = BowCenter + UnitToMouseV * 8f;
            float velScale = 0.88f + 0.12f * Frenzy;
            int rank = (int)Rank;
            int dmg = Math.Max(1, (int)(damage * 0.82f));

            //三矢紧束：中箭主箭（叠魂标），两侧微散
            for (int k = -1; k <= 1; k++) {
                Vector2 vel = UnitToMouseV.RotatedBy(k * 0.028f) * speed * velScale;
                Vector2 pos = muzzle + UnitToMouseV.RotatedBy(MathHelper.PiOver2) * (k * 4f);
                scheme.SpawnTagged(Owner, source, pos, vel, shootType, dmg, knockback,
                    k == 0 ? GsVolleyRole.VolleyMain : GsVolleyRole.VolleySide, rank);
            }
            //两翼幻影箭（阶 2 起）：与实箭平行的魂箭侧列
            if (rank >= 2) {
                for (int s = -1; s <= 1; s += 2) {
                    Vector2 pos = muzzle + UnitToMouseV.RotatedBy(MathHelper.PiOver2) * (s * 13f);
                    Projectile.NewProjectile(Owner.GetSource_Misc("GsPhantasmEcho"), pos,
                        UnitToMouseV * speed * velScale * 0.92f,
                        ModContent.ProjectileType<GsPhantasmEchoArrowProj>(),
                        Math.Max(1, (int)(damage * 0.3f)), knockback * 0.5f, Owner.whoAmI, -1f);
                }
            }
            //幻影射手随射
            Projectile archer = GsPhantasm.FindArcher(Owner);
            if (archer != null) {
                Projectile.NewProjectile(Owner.GetSource_Misc("GsPhantasmEcho"),
                    archer.Center + UnitToMouseV * 10f, UnitToMouseV * speed * velScale * 0.9f,
                    ModContent.ProjectileType<GsPhantasmEchoArrowProj>(),
                    Math.Max(1, (int)(damage * 0.25f)), knockback * 0.4f, Owner.whoAmI, -1f);
                if (archer.ModProjectile is GsPhantomArcherProj archerProj) {
                    archerProj.Recoil = 4f;
                }
            }

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.75f, Pitch = -0.05f + 0.35f * Frenzy },
                    Projectile.Center);
                PRTLoader.NewParticle<PRT_Light>(muzzle, UnitToMouseV * 2f,
                    GsPhantomArcherProj.PhantomTeal, 0.1f + 0.03f * rank)?.Configure(8, 0.8f);
            }
        }

        //==================== 音画侦测（各端统一：观察 ai[0] 变化处发声） ====================

        private void WatchRankCues() {
            int rank = (int)Rank;
            if (rank > seenRank && !VaultUtils.isServer) {
                starTimer = 6;
                if (rank >= 3) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.8f, Pitch = -0.1f }, Projectile.Center);
                    if (Owner.whoAmI == Main.myPlayer) {
                        Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center,
                            UnitToMouseV, 1.6f, 5f, 8, 900f, "GsPhantasm"));
                    }
                }
                else {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.4f + 0.1f * rank, Pitch = 0.2f + 0.15f * rank },
                        Projectile.Center);
                }
            }
            seenRank = rank;
            if (starTimer > 0) {
                starTimer--;
            }
            //狂乱期弦上星尘：阶位越高越密（客户端）
            if (!VaultUtils.isServer && rank >= 1 && Main.rand.NextBool(rank >= 3 ? 2 : 5 - rank)) {
                Vector2 nock = GetNockWorldPos();
                PRTLoader.NewParticle<PRT_Light>(nock + Main.rand.NextVector2Circular(3f, 3f),
                    -UnitToMouseV * 0.4f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    Main.rand.NextBool() ? GsPhantomArcherProj.PhantomTeal : GsPhantomArcherProj.PhantomBlue,
                    0.07f)?.Configure(10, 0.75f);
                Lighting.AddLight(nock, GsPhantomArcherProj.PhantomTeal.ToVector3() * (0.1f * rank));
            }
        }

        //==================== 姿态 ====================

        private void UpdatePose() {
            Projectile.rotation = ToMouseA;
            Owner.ChangeDir(ToMouse.X >= 0 ? 1 : -1);
            Projectile.Center = BowCenter;
        }

        /// <summary>后手持弓瞄准，前手随拨弦循环快速抽放（狂乱越高抽放越急）</summary>
        private void UpdateArms() {
            float holdArmRot = Projectile.rotation - MathHelper.PiOver2 * SafeGravDir;
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, holdArmRot);

            float pluck = PluckProgress();
            Player.CompositeArmStretchAmount stretch = Player.CompositeArmStretchAmount.Full;
            if (pluck > 0.3f) {
                stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
            }
            if (pluck > 0.6f) {
                stretch = Player.CompositeArmStretchAmount.Quarter;
            }
            if (pluck > 0.85f) {
                stretch = Player.CompositeArmStretchAmount.None;
            }
            Owner.SetCompositeArmFront(true, stretch, holdArmRot);

            Owner.itemRotation = MathHelper.WrapAngle(Projectile.rotation * Owner.direction);
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
        }

        /// <summary>拨弦进度 0~1（每轮循环抽满一次）</summary>
        private float PluckProgress() => MathHelper.Clamp(pluckTimer / (float)Math.Max(1, pluckPeriod), 0f, 1f);

        /// <summary>搭箭点：弦被快速抽放，狂乱越高抽幅越大</summary>
        private Vector2 GetNockWorldPos()
            => BowCenter - ToMouseA.ToRotationVector2() * (3f + PluckProgress() * (6f + 2f * (int)Rank));

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            if (scheme == null || boundBowType <= 0 || boundBowType >= TextureAssets.Item.Length) {
                return false;
            }
            Main.instance.LoadItem(boundBowType);
            Texture2D bowTex = TextureAssets.Item[boundBowType].Value;
            int rank = (int)Rank;

            Vector2 drawCenter = Projectile.Center;
            //齐射后坐：弓身沿瞄准反向短促回顶
            if (recoilTimer > 0) {
                drawCenter -= ToMouseA.ToRotationVector2() * (recoilTimer * 0.6f);
            }
            //满狂乱高频震颤（identity 定相）
            if (rank >= 3) {
                drawCenter += new Vector2(
                    MathF.Sin(Main.GlobalTimeWrappedHourly * 55f + Projectile.identity),
                    MathF.Cos(Main.GlobalTimeWrappedHourly * 47f)) * 0.9f;
            }

            SpriteEffects effect = DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;
            Vector2 screenPos = drawCenter - Main.screenPosition;
            Color teal = GsPhantomArcherProj.PhantomTeal with { A = 0 };
            Color blue = GsPhantomArcherProj.PhantomBlue with { A = 0 };

            //狂乱辉光垫底
            if (rank >= 1) {
                Texture2D glowTex = CWRAsset.SoftGlow.Value;
                Main.EntitySpriteDraw(glowTex, screenPos, null, teal * (0.18f + 0.09f * rank), 0f,
                    glowTex.Size() / 2f, 0.5f + 0.09f * rank, SpriteEffects.None);
            }

            //幻影残弓：阶位数量的加色残弓在角度上滞后摆动（identity 定相）
            for (int g = 1; g <= rank; g++) {
                float lag = MathF.Sin(Main.GlobalTimeWrappedHourly * 9f + g * 2.1f + Projectile.identity * 0.5f)
                    * 0.07f + g * 0.045f;
                Color ghost = (g % 2 == 0 ? blue : teal) * (0.34f / g + 0.08f);
                Main.EntitySpriteDraw(bowTex, screenPos, null, ghost,
                    Projectile.rotation + lag * DirSign, bowTex.Size() / 2f, 1f, effect);
                Main.EntitySpriteDraw(bowTex, screenPos, null, ghost,
                    Projectile.rotation - lag * DirSign, bowTex.Size() / 2f, 1f, effect);
            }

            //弓弦：两端锚点连到搭箭点（随拨弦循环快速抽放）
            Vector2 perp = (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2();
            float halfString = bowTex.Height * 0.36f;
            Vector2 top = drawCenter + perp * halfString;
            Vector2 bottom = drawCenter - perp * halfString;
            Vector2 nock = GetNockWorldPos() + (drawCenter - Projectile.Center);
            Color stringColor = Color.Lerp(lightColor, Color.White, 0.3f) * 0.8f;
            DrawLine(top, nock, stringColor, 2f);
            DrawLine(nock, bottom, stringColor, 2f);
            DrawNockedArrow(nock, lightColor, rank);

            //弓体
            Main.EntitySpriteDraw(bowTex, screenPos, null, lightColor, Projectile.rotation,
                bowTex.Size() / 2f, 1f, effect);

            //升阶星芒
            if (starTimer > 0) {
                Texture2D star = CWRAsset.StarTexture_White.Value;
                float p = starTimer / 6f;
                Main.EntitySpriteDraw(star, screenPos, null, teal * (0.85f * p), Projectile.identity * 0.7f,
                    star.Size() / 2f, 0.15f + 0.1f * (1f - p), SpriteEffects.None);
            }
            return false;
        }

        private static void DrawLine(Vector2 start, Vector2 end, Color color, float thickness) {
            Vector2 toEnd = end - start;
            float length = toEnd.Length();
            if (length < 1f) {
                return;
            }
            Main.EntitySpriteDraw(VaultAsset.placeholder2.Value, start - Main.screenPosition, new Rectangle(0, 0, 1, 1),
                color, toEnd.ToRotation(), new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0);
        }

        /// <summary>搭在弦上的箭：按展示弹药画；阶 2 起画三箭小扇预告紧束齐射</summary>
        private void DrawNockedArrow(Vector2 nock, Color lightColor, int rank) {
            int ammoItemType = (int)AmmoShowType;
            if (ammoItemType <= ItemID.None || ammoItemType >= TextureAssets.Item.Length) {
                return;
            }
            Main.instance.LoadItem(ammoItemType);
            Texture2D arrowTex = TextureAssets.Item[ammoItemType].Value;
            Item ammoItem = new(ammoItemType);
            if (!ammoItem.consumable) {
                int showType = ItemID.WoodenArrow;
                if (VaultUtils.ProjectileToSafeAmmoMap.TryGetValue(ammoItem.shoot, out int mapped)) {
                    showType = mapped;
                }
                Main.instance.LoadItem(showType);
                arrowTex = TextureAssets.Item[showType].Value;
            }
            int fan = rank >= 2 ? 3 : 1;
            for (int k = 0; k < fan; k++) {
                float off = (k - (fan - 1) * 0.5f) * 0.1f;
                Color c = k == (fan - 1) / 2 ? lightColor
                    : (GsPhantomArcherProj.PhantomTeal with { A = 0 }) * 0.55f;
                Main.EntitySpriteDraw(arrowTex, nock - Main.screenPosition, null, c,
                    Projectile.rotation + MathHelper.PiOver2 + off,
                    new Vector2(arrowTex.Width / 2f, arrowTex.Height), 1f, SpriteEffects.FlipVertically);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Main.rand.NextBool() ? GsPhantomArcherProj.PhantomTeal : GsPhantomArcherProj.PhantomBlue,
                    0.08f)?.Configure(12, 0.8f);
            }
        }
    }

    /// <summary>
    /// 幻影魂矢：全加色自绘的星旋箭灵。ai[0] = 追踪目标 whoAmI（-1 为直射侧翼/射手箭）。
    /// 出膛 12 帧缓加速（0.88 → 1.2 倍），追魂型 5 帧后每帧 8° 咬向目标；穿墙（虚体），
    /// 亡时魂尘上飘（余痕相）。绘制 identity 定相，无随机
    /// </summary>
    internal class GsPhantasmEchoArrowProj : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.WoodenArrowFriendly}";

        private ref float TargetIndex => ref Projectile.ai[0];

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (Life <= 12f) {
                Projectile.velocity *= 1.03f;
            }
            int idx = (int)TargetIndex;
            if (idx >= 0 && Life > 5f) {
                NPC target = idx < Main.maxNPCs ? Main.npc[idx] : null;
                if (target != null && target.active && GsHuntMarkNPC.CanMark(target)) {
                    float current = Projectile.velocity.ToRotation();
                    float desired = (target.Center - Projectile.Center).ToRotation();
                    Projectile.velocity = current.AngleTowards(desired, MathHelper.ToRadians(8f))
                        .ToRotationVector2() * Projectile.velocity.Length();
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!VaultUtils.isServer && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center - Projectile.velocity * 0.5f,
                    -Projectile.velocity * 0.04f,
                    Main.rand.NextBool() ? GsPhantomArcherProj.PhantomTeal : GsPhantomArcherProj.PhantomBlue,
                    0.07f)?.Configure(9, 0.8f);
            }
            Lighting.AddLight(Projectile.Center, GsPhantomArcherProj.PhantomTeal.ToVector3() * 0.18f);
        }

        /// <summary>全加色魂体：三重后曳残影 + 青芯 + 箭簇白光，不吃环境光（identity 定相）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Projectile.type);
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            float fadeIn = MathHelper.Clamp(Life / 6f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
            float alpha = fadeIn * fadeOut;
            float pulse = 0.82f + 0.18f * MathF.Sin(Life * 0.3f + Projectile.identity * 0.77f);
            Color teal = GsPhantomArcherProj.PhantomTeal with { A = 0 };
            Color blue = GsPhantomArcherProj.PhantomBlue with { A = 0 };

            for (int i = 1; i <= 3; i++) {
                Main.EntitySpriteDraw(tex, Projectile.Center - Projectile.velocity * (0.55f * i) - Main.screenPosition,
                    null, blue * (0.4f * alpha * pulse / i), Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                teal * (0.85f * alpha), Projectile.rotation, origin, 1f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                (Color.White with { A = 0 }) * (0.35f * alpha * pulse), Projectile.rotation, origin,
                0.8f, SpriteEffects.None, 0);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                GsPhantomArcherProj.PhantomTeal, 0.12f)?.Configure(8, 0.85f);
        }

        /// <summary>魂散：亡处魂尘缓缓上飘，比矢体活得久（余痕相）</summary>
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.4f, 1f)),
                    Main.rand.NextBool() ? GsPhantomArcherProj.PhantomTeal : GsPhantomArcherProj.PhantomBlue,
                    0.09f)?.Configure(Main.rand.Next(16, 26), 0.75f);
            }
        }
    }
}
