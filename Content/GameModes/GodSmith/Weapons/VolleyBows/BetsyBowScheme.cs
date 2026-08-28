using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.VolleyBows.Projectiles;
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
    /// 空袭 / Aerial Bane（重铸 ~110%，对空 ~135% 为本弓定位）：贝琪龙骨制的獠焰弓。
    /// 身份宣言：①龙焰矢先爬升、失速翻转、再俯冲轰炸准星区，三相弹道自带节奏
    /// ②着弹绽龙火爆圈，飞行之敌加倍受创③满充呼叫九矢地毯轰炸，沿航线依次砸落。
    /// 原版「六矢扇形直射」重制为俯冲轰炸编舞；洞穴内自动压平爬升角保底可用。
    /// 期望：普通 5×0.95+溅射≈5.75/6≈96%；地毯每 7 发（9×0.72+溅射≈7.6）→ 周期 ≈103%；重磅处决 ≈+4%
    /// </summary>
    internal class GsDD2BetsyBow : GsVolleyBowScheme
    {
        public override int TargetItemID => ItemID.DD2BetsyBow;

        protected override string GsDescFallback =>
            "Reforged: each draw looses 5 dragonfire bolts that climb, stall, then dive-bomb the cursor area\nImpacts burst into dragonfire rings; airborne foes take 35% more damage\nShots build airstrike charge; at full charge the next draw calls a carpet run of 9 bolts along the flight line\nBolt hits stack flame brands; branding a foe thrice drops one heavy bomb straight onto it";

        //==================== 家族参数（标记由轰炸矢自理，族打标关闭） ====================

        protected override int VolleyCount => 9;
        protected override float ChargePerShot => 100f / 7f;
        protected override int MarksPerVolleyHit => 0;
        protected override int PursuitEvery => 0;
        protected override Color TrailColor => EmberMain;

        internal static readonly Color EmberMain = new(255, 150, 70);
        internal static readonly Color EmberHot = new(255, 224, 150);
        internal static readonly Color EmberDeep = new(150, 62, 30);

        //==================== 射击流 ====================

        /// <summary>头顶净空不足时压平爬升角（洞穴保底可用）</summary>
        private static float LiftFactor(Vector2 muzzle)
            => Collision.CanHitLine(muzzle + new Vector2(0f, -200f), 1, 1, muzzle, 1, 1) ? 0.62f : 0.22f;

        /// <summary>放出一支龙焰轰炸矢：爬升初速与落点由生成端一次性定参</summary>
        private static void LaunchBomber(Player player, Vector2 muzzle, Vector2 aimDir, float speed,
            Vector2 target, int damage, float knockback, float lift, int stagger) {
            Vector2 launch = Vector2.Lerp(aimDir, -Vector2.UnitY, lift).SafeNormalize(-Vector2.UnitY)
                .RotatedBy((stagger - 2) * 0.14f);
            //初速逐矢递增：同帧齐射也会依次到达失速点，读作编队分批俯冲
            Vector2 vel = launch * speed * (0.58f + stagger * 0.05f);
            Projectile.NewProjectile(player.GetSource_Misc("GsBetsyBombard"), muzzle, vel,
                ModContent.ProjectileType<GsBetsyBombardProj>(), damage, knockback, player.whoAmI,
                target.X, target.Y);
        }

        /// <summary>普通射击：五矢爬升编队，俯冲轰炸准星横排落点</summary>
        protected override bool? OnNormalShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Vector2 aimDir = velocity.SafeNormalize(Vector2.UnitX);
            float speed = MathF.Max(10f, velocity.Length());
            float lift = LiftFactor(position);
            Vector2 aim = Main.MouseWorld;
            int dmg = (int)(damage * 0.95f);
            for (int i = 0; i < 5; i++) {
                Vector2 target = aim + new Vector2((i - 2) * 54f, 0f);
                LaunchBomber(player, position, aimDir, speed, target, dmg, knockback, lift, i);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item42 with { Volume = 0.5f, Pitch = -0.2f }, position);
            }
            return false;
        }

        /// <summary>地毯轰炸：九矢沿航线依次砸落，扫过准星所在的整条走廊</summary>
        protected override void FireVolley(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int count) {
            count = Math.Clamp(count, 5, VolleyCount);
            Vector2 aimDir = velocity.SafeNormalize(Vector2.UnitX);
            float speed = MathF.Max(10f, velocity.Length());
            float lift = LiftFactor(position);
            float sweep = aimDir.X >= 0f ? 1f : -1f;
            Vector2 aim = Main.MouseWorld;
            int dmg = (int)(damage * 0.72f);
            for (int i = 0; i < count; i++) {
                //航线：从近端扫向远端的一条轰炸走廊
                Vector2 target = aim + new Vector2((i - (count - 1) * 0.5f) * 62f * sweep, 0f);
                LaunchBomber(player, position, aimDir, speed, target, dmg, knockback * 0.7f, lift, i % 5);
            }
        }

        //==================== 动画：重炮后坐 ====================

        /// <summary>重炮后坐：5px 猛坐 + 慢回中带一次过冲，30 帧用时撑得起大后坐（仅位移）</summary>
        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            float elapsed = 1f - player.itemAnimation / (float)player.itemAnimationMax;
            float kick = MathF.Exp(-3.4f * elapsed);
            float overshoot = 0.7f * MathF.Sin(MathHelper.Clamp((elapsed - 0.35f) / 0.55f, 0f, 1f) * MathF.PI);
            Vector2 aimDir = player.itemRotation.ToRotationVector2() * player.direction;
            player.itemLocation -= aimDir * (5.2f * kick - overshoot);
            player.itemLocation.Y += 1.6f * kick * player.gravDir;
        }

        /// <summary>出手龙火：口部喷吐三簇獠焰（各端可见的出手相）</summary>
        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 muzzle = player.MountedCenter + new Vector2(player.direction * 20f, -4f);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_HellFire>(muzzle + Main.rand.NextVector2Circular(4f, 4f),
                    new Vector2(player.direction * Main.rand.NextFloat(1f, 2.4f), -Main.rand.NextFloat(0.6f, 1.6f)),
                    Color.White, Main.rand.NextFloat(0.5f, 0.8f));
            }
            Lighting.AddLight(muzzle, EmberMain.ToVector3() * 0.5f);
        }

        /// <summary>空袭充能满待机：弓身腾起余烬（本机个人读数）</summary>
        public override void GsHoldItem(Item item, Player player) {
            if (VaultUtils.isServer || player.whoAmI != Main.myPlayer) {
                return;
            }
            if (player.GetModPlayer<GsVolleyPlayer>().Charge < 100f || !Main.rand.NextBool(6)) {
                return;
            }
            Vector2 at = player.MountedCenter + new Vector2(player.direction * Main.rand.NextFloat(6f, 18f),
                Main.rand.NextFloat(-8f, 6f));
            PRTLoader.NewParticle<PRT_Spark>(at, new Vector2(0f, -Main.rand.NextFloat(0.6f, 1.4f)),
                Main.rand.NextBool() ? EmberMain : EmberHot, 0.24f)?.Configure(false, 14);
        }
    }

    /// <summary>
    /// 空袭龙焰轰炸矢：爬升（灰烟）→ 失速翻转（闪帧）→ 俯冲（焰尾加速）三相弹道。
    /// ai[0]/ai[1] = 落点坐标；ai[0] ≤ -1 时改读追踪目标 whoAmI = -(ai[0]+1)（处决重磅矢）。
    /// 着弹龙火爆圈由 owner 端生成（ThemeGold 参数爆），对空中之敌 +35%；
    /// 命中叠焰标、满三层触发重磅矢。绘制：原版贴图垫底 + 加色焰影分层，identity 定相
    /// </summary>
    internal class GsBetsyBombardProj : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.DD2BetsyArrow}";

        private const int PhaseClimb = 0;
        private const int PhaseStall = 1;
        private const int PhaseDive = 2;

        private ref float TargetX => ref Projectile.ai[0];
        private ref float TargetY => ref Projectile.ai[1];
        private ref float Phase => ref Projectile.localAI[0];
        private ref float Timer => ref Projectile.localAI[1];

        /// <summary>处决重磅矢（追踪敌身，爆圈更大）</summary>
        private bool Heavy => TargetX <= -1f;

        private int HomingIndex => -(int)(TargetX + 1f);

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>当前俯冲目标点（重磅矢实时读敌位置；目标失效由 owner 引爆）</summary>
        private Vector2 CurrentTarget() {
            if (!Heavy) {
                return new Vector2(TargetX, TargetY);
            }
            int idx = HomingIndex;
            NPC npc = idx >= 0 && idx < Main.maxNPCs ? Main.npc[idx] : null;
            if (npc != null && npc.active) {
                return npc.Center;
            }
            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.Kill();
            }
            return Projectile.Center + Projectile.velocity;
        }

        public override void AI() {
            Timer++;
            Vector2 target = CurrentTarget();
            switch ((int)Phase) {
                case PhaseClimb: {
                    //爬升：升力耗尽即失速；顶到实心物块也提前失速
                    Projectile.velocity.Y += 0.34f;
                    Projectile.velocity.X *= 0.985f;
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                    bool stalled = Projectile.velocity.Y >= -0.8f || Timer > 46f
                        || WorldGen.SolidTile(Projectile.Center.ToTileCoordinates());
                    if (stalled) {
                        Phase = PhaseStall;
                        Timer = 0f;
                        if (!VaultUtils.isServer) {
                            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                                GsDD2BetsyBow.EmberHot, 0.13f)?.Configure(8, 0.85f);
                        }
                    }
                    else if (!VaultUtils.isServer && Timer % 4 == 0) {
                        //爬升灰烟
                        PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.5f,
                            -Projectile.velocity * 0.05f, GsDD2BetsyBow.EmberDeep, 0.2f)?.Configure(false, 10);
                    }
                    break;
                }
                case PhaseStall: {
                    //失速翻转：滞空减速，机头压向落点
                    Projectile.velocity *= 0.86f;
                    float want = (target - Projectile.Center).ToRotation() + MathHelper.PiOver2;
                    Projectile.rotation = Projectile.rotation.AngleLerp(want, 0.3f);
                    if (Timer >= 6f) {
                        Phase = PhaseDive;
                        Timer = 0f;
                        Projectile.tileCollide = true;
                        if (!VaultUtils.isServer) {
                            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.35f, Pitch = 0.3f },
                                Projectile.Center);
                        }
                    }
                    break;
                }
                default: {
                    //俯冲：向落点持续加压，越冲越快
                    Vector2 dir = (target - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    Projectile.velocity = Projectile.velocity * 0.92f + dir * 2.2f;
                    if (Projectile.velocity.Length() > 21f) {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * 21f;
                    }
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                    //贴近落点空爆（owner 权威，专克空中目标）
                    if (Projectile.IsOwnedByLocalPlayer()
                        && Projectile.Center.DistanceSQ(target) < 30f * 30f) {
                        Projectile.Kill();
                        return;
                    }
                    if (!VaultUtils.isServer) {
                        Lighting.AddLight(Projectile.Center, GsDD2BetsyBow.EmberMain.ToVector3() * 0.35f);
                        if (Timer % 2 == 0) {
                            PRTLoader.NewParticle<PRT_HellFire>(
                                Projectile.Center - Projectile.velocity * 0.4f,
                                -Projectile.velocity * 0.06f, Color.White,
                                Main.rand.NextFloat(0.4f, 0.7f));
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>对空撕咬：飞行之敌 +35%（本弓定位，原版空袭同源）</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.noGravity || MathF.Abs(target.velocity.Y) > 0.4f) {
                modifiers.FinalDamage *= 1.35f;
            }
        }

        /// <summary>焰标（owner 端本地量）：叠满三层再中，呼叫重磅矢直坠该敌</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Heavy || !GsHuntMarkNPC.CanMark(target)) {
                return;
            }
            GsHuntMarkNPC mark = target.GetGlobalNPC<GsHuntMarkNPC>();
            mark.Cap = 3;
            if (mark.Stacks >= 3) {
                mark.Stacks = 0;
                mark.Timer = 0;
                Player owner = Main.player[Projectile.owner];
                //重磅矢从敌上方净空处入场
                float height = 380f;
                while (height > 90f && !Collision.CanHitLine(target.Center + new Vector2(0f, -height), 1, 1,
                    target.Center, 1, 1)) {
                    height -= 96f;
                }
                Projectile.NewProjectile(owner.GetSource_Misc("GsBetsyBombard"),
                    target.Center + new Vector2(0f, -height), new Vector2(0f, 6f),
                    ModContent.ProjectileType<GsBetsyBombardProj>(), (int)(Projectile.damage * 1.7f), 5f,
                    owner.whoAmI, -(target.whoAmI + 1f), 0f);
                //重磅矢直接入俯冲相
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.5f, Pitch = 0.15f }, target.Center);
                }
            }
            else {
                mark.Stacks++;
                mark.Timer = 240;
            }
        }

        /// <summary>着弹：龙火爆圈（owner 生成参数爆）+ 余烬迸散回落（余痕相，比矢体活得久）</summary>
        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                int burstDamage = (int)(Projectile.damage * (Heavy ? 0.6f : 0.4f));
                Projectile.NewProjectile(Main.player[Projectile.owner].GetSource_Misc("GsVolleyBurst"),
                    Projectile.Center, Vector2.Zero, ModContent.ProjectileType<GsVolleyBurstProj>(),
                    burstDamage, 3f, Projectile.owner, Heavy ? 110f : 62f, GsVolleyBurstProj.ThemeGold);
            }
            if (VaultUtils.isServer) {
                return;
            }
            int embers = Heavy ? 8 : 4;
            for (int i = 0; i < embers; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(1f, 3f)),
                    Main.rand.NextBool() ? GsDD2BetsyBow.EmberMain : GsDD2BetsyBow.EmberDeep,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(20, 34));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_HellFire>(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.4f, 1.2f)),
                    Color.White, Main.rand.NextFloat(0.6f, 0.9f));
            }
        }

        /// <summary>自绘：俯冲相三重焰影 + 白热芯 + 底部橙光；爬升相淡影（identity 定相，无随机）</summary>
        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Projectile.type);
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            bool diving = (int)Phase == PhaseDive;
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f + Projectile.identity * 0.53f);

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null && diving) {
                Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null,
                    (GsDD2BetsyBow.EmberMain with { A = 0 }) * (0.4f * pulse), 0f,
                    glow.Size() * 0.5f, Heavy ? 0.34f : 0.22f, SpriteEffects.None, 0);
            }
            int ghosts = diving ? 3 : 1;
            Color ghostColor = (diving ? GsDD2BetsyBow.EmberMain : GsDD2BetsyBow.EmberDeep) with { A = 0 };
            for (int i = 1; i <= ghosts; i++) {
                Main.EntitySpriteDraw(tex, Projectile.Center - Projectile.velocity * (0.5f * i) - Main.screenPosition,
                    null, ghostColor * ((diving ? 0.4f : 0.22f) * pulse / i), Projectile.rotation,
                    origin, Projectile.scale * (diving ? 1.05f : 1f), SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            if (diving) {
                Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                    (GsDD2BetsyBow.EmberHot with { A = 0 }) * (0.35f * pulse), Projectile.rotation,
                    origin, Projectile.scale * 1.08f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
