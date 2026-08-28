using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
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
    /// 签名「新芽爆发」：①按住化作稀疏大弧刃风，新芽生长（150 帧攒满）令刃影愈发密集明亮
    /// ②松手按生长放出双层新月剑气，满蓄时具泰拉刃形出生白闪 ③命中草绿火星与叶屑
    /// </summary>
    internal class GsTerragrim : GodSmithScheme
    {
        public override int TargetItemID => ItemID.Terragrim;

        public override string GsFamily => "MeleeOddities";

        protected override string GsDescFallback =>
            "Reforged: hold to become a storm of blades while Growth builds;\n" +
            "release to loose a blade wave scaled by Growth - at full power it takes the shape of the Terra Blade";

        //新芽青绿色板
        internal static readonly Color TerraBright = new(178, 255, 150); //青绿刃光
        internal static readonly Color TerraGreen = new(95, 205, 105);   //泰拉绿身
        internal static readonly Color TerraDeep = new(24, 60, 36);      //深林暗绿
        internal static readonly Color BudWhite = new(235, 255, 225);    //芽白

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
    /// 泰拉悲愿手持乱舞：稀疏大弧刃风。新芽生长 0→1（150 帧），
    /// 闪现间隔随生长 6 收到 4，刃影亮度与光尘密度随生长渐升；
    /// 松手生长 ≥0.35 放出新月剑气，不足只出一道短刃光演出
    /// </summary>
    internal class GsTerragrimHeld : GsOdditiesFlurryHeldBase
    {
        protected override int SwordItemID => ItemID.Terragrim;
        protected override Color EdgeBright => GsTerragrim.TerraBright;
        protected override Color BodyMain => GsTerragrim.TerraGreen;
        protected override Color HotAccent => GsTerragrim.BudWhite;
        protected override Color DeepShadow => GsTerragrim.TerraDeep;

        /// <summary>生长攒满帧数</summary>
        private const int GrowthFullFrames = 150;
        /// <summary>放剑气的生长门槛</summary>
        private const float CrescentGate = 0.35f;

        /// <summary>新芽生长 0~1；随 held 生灭天然每场乱舞独立，各端按各自帧数推同一条曲线</summary>
        private float growth;

        /// <summary>刃影密度随生长提升：闪现间隔 6 收到 4</summary>
        protected override int FlashInterval => 6 - (int)MathF.Round(growth * 2f);
        protected override float SpreadArc => 0.85f;
        protected override float FlurryIntensity => 0.75f + (0.45f * growth);
        protected override float SwingPitch => 0.02f;

        protected override void FlurryAI()
            => growth = Math.Min(1f, timer / (float)GrowthFullFrames);

        /// <summary>材质分流：命中补草绿叶屑（火星走基类色板默认）</summary>
        protected override void OnFlurryHit(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.GrassBlades,
                    AimUnit.RotatedByRandom(1.1) * Main.rand.NextFloat(1.5f, 4f), 80, default,
                    Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>松手结算：生长够放新月剑气（owner 生成），不足只出短刃光演出</summary>
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
                    for (int i = 0; i < 4 + (int)(growth * 5f); i++) {
                        Vector2 vel = AimUnit.RotatedByRandom(0.5) * Main.rand.NextFloat(3f, 7f + (growth * 4f));
                        Color c = Main.rand.NextBool(3) ? GsTerragrim.BudWhite : GsTerragrim.TerraBright;
                        PRTLoader.NewParticle<PRT_Spark>(Hand + (AimUnit * 30f), vel, c,
                            Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(12, 20));
                    }
                }
            }
            else if (!VaultUtils.isServer) {
                //生长不足：一道短刃光演出，无弹幕
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = 0.25f }, Owner.Center);
                for (int i = 0; i < 3; i++) {
                    Vector2 vel = AimUnit.RotatedByRandom(0.35) * Main.rand.NextFloat(2.5f, 5f);
                    PRTLoader.NewParticle<PRT_Spark>(Hand + (AimUnit * 34f), vel, GsTerragrim.TerraBright,
                        Main.rand.NextFloat(0.3f, 0.45f))?.Configure(false, Main.rand.Next(8, 14));
                }
            }
        }

        /// <summary>生长可视：乱舞区青绿光晕垫底渐亮，攒满后手部芽白星闪提示「松手正当时」</summary>
        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow != null && growth > 0.05f) {
                Color halo = GsTerragrim.TerraGreen * (0.10f + (0.20f * growth));
                halo.A = 0;
                sb.Draw(glow, Projectile.Center - Main.screenPosition, null, halo, 0f,
                    glow.Size() / 2f, 0.9f + (0.5f * growth), SpriteEffects.None, 0f);
            }
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null && growth >= 1f) {
                //identity 播种错相呼吸，不掷绘制 rand
                float pulse = 0.7f + (0.3f * MathF.Sin((Main.GlobalTimeWrappedHourly * 10f) + (DrawRand01(7) * 6.28f)));
                Color cue = GsTerragrim.BudWhite * (0.55f * pulse);
                cue.A = 0;
                sb.Draw(star, Hand - Main.screenPosition, null, cue, timer * 0.05f,
                    star.Size() / 2f, 0.075f, SpriteEffects.None, 0f);
            }
            return base.PreDraw(ref lightColor);
        }
    }

    /// <summary>
    /// 新芽新月剑气：松手放出的青绿月牙，ai[0]=生长（随生成包过线，定尺度与满蓄白闪）。
    /// 飞行微减速不做匀速贴纸；自绘双层新月=SemiCircularSmear 外层青绿宽笔+内层白芯窄笔，
    /// 朝向 velocity，oldPos 残影链 3 节，加色批全 A=0；命中/消散草绿火星+叶屑
    /// </summary>
    internal class GsTerragrimCrescentProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.Terragrim");

        private const int Life = 40;

        private float Growth => MathHelper.Clamp(Projectile.ai[0], 0f, 1f);
        private float CrescentScale => 0.7f + (0.8f * Growth);
        private bool FullBloom => Growth >= 0.98f;
        private int Age => Life - Projectile.timeLeft;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
            ProjectileID.Sets.TrailingMode[Type] = 2;
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
                //按生长撑判定箱
                int size = (int)(44 * CrescentScale);
                Projectile.Resize(size, size);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            //飞行有生命周期：微减速收尾，不做匀速直飞
            Projectile.velocity *= 0.982f;

            Lighting.AddLight(Projectile.Center, GsTerragrim.TerraGreen.ToVector3() * (0.4f * CrescentScale));

            //叶屑拖尾（AI 内 Main.rand 允许）
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                    -Projectile.velocity * 0.1f, 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            //命中草绿火星+叶屑
            for (int i = 0; i < 4; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f);
                Color c = Main.rand.NextBool(3) ? GsTerragrim.BudWhite : GsTerragrim.TerraBright;
                PRTLoader.NewParticle<PRT_Spark>(target.Center, vel, c, Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(true, Main.rand.Next(10, 18));
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.GrassBlades,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f), 80, default,
                    Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f) * CrescentScale, 90, default,
                    Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = Main.rand.NextBool();
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, GsTerragrim.TerraBright,
                0.14f + (0.08f * CrescentScale))?.Configure(8, 0.7f);
        }

        /// <summary>绘制路径专用确定性伪随机（identity+salt 播种）</summary>
        private float DrawRand01(int salt) {
            uint h = (uint)((Projectile.identity * 374761393) + (salt * 668265263));
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (smear == null) {
                return false;
            }
            Vector2 origin = smear.Size() / 2f;
            float rot = Projectile.rotation;
            float sc = CrescentScale;
            float lifeFade = MathHelper.Clamp(Projectile.timeLeft / (Life * 0.35f), 0f, 1f);

            //oldPos 残影链 3 节，越旧越暗越小
            for (int i = 3; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + (Projectile.Size / 2f) - Main.screenPosition;
                float ghostFade = 1f - (i / 4f);
                Color gc = GsTerragrim.TerraGreen * (0.20f * ghostFade * lifeFade);
                gc.A = 0;
                Main.EntitySpriteDraw(smear, at, null, gc, Projectile.oldRot[i],
                    origin, new Vector2(0.46f, 0.26f) * sc * (0.82f + (0.06f * ghostFade)), SpriteEffects.None, 0);
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            //identity 播种错相呼吸，不掷绘制 rand
            float pulse = 0.88f + (0.12f * MathF.Sin((Main.GlobalTimeWrappedHourly * 9f) + (DrawRand01(1) * 6.28f)));

            //软光垫底
            if (glow != null) {
                Color under = GsTerragrim.TerraGreen * (0.30f * lifeFade * pulse);
                under.A = 0;
                Main.EntitySpriteDraw(glow, drawPos, null, under, 0f, glow.Size() / 2f, 0.7f * sc, SpriteEffects.None, 0);
            }
            //双层新月：外层青绿宽笔
            Color outer = Color.Lerp(GsTerragrim.TerraGreen, GsTerragrim.TerraBright, 0.45f) * (0.75f * lifeFade * pulse);
            outer.A = 0;
            Main.EntitySpriteDraw(smear, drawPos, null, outer, rot, origin,
                new Vector2(0.52f, 0.30f) * sc, SpriteEffects.None, 0);
            //内层白芯窄笔
            Color core = GsTerragrim.BudWhite * (0.85f * lifeFade * pulse);
            core.A = 0;
            Main.EntitySpriteDraw(smear, drawPos, null, core, rot, origin,
                new Vector2(0.42f, 0.14f) * sc, SpriteEffects.None, 0);

            //满蓄泰拉刃形：出生 1~2 帧白闪
            if (FullBloom && star != null && Age <= 2) {
                Color flash = GsTerragrim.BudWhite * (0.9f - (0.3f * Age));
                flash.A = 0;
                Main.EntitySpriteDraw(star, drawPos, null, flash, rot, star.Size() / 2f,
                    0.22f * sc, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
