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

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard
{
    /// <summary>
    /// 乌兹重铸：泼洒与点杀两态 + 曳光火控。材质：镀镍冲锋枪与琥珀曳光弹。<br/>
    /// 签名行为：①每第 4 发（点射档为链末发）换装曳光弹，琥珀光带划破弹道
    /// ②曳光命中「点亮」目标 1.5 秒，乌兹子弹对其 +10% ③抛壳左右交替的双持演出。<br/>
    /// [双持乱射]：+30% 射速、附加散布；[高速点射]：3 连完全收束零散布，链间强制间歇。
    /// 点射走真实 use，每发照常耗弹
    /// </summary>
    internal class GsUzi : GsFireModeScheme
    {
        public override int TargetItemID => ItemID.Uzi;

        public override string GsFamily => "GunsHard";

        protected override string GsDescFallback =>
            "Reforged: right click to switch grip\n" +
            "Akimbo sprays 30% faster with a loose pattern; Burst fires tight three round strings dead on target\n" +
            "Every 4th round (the burst finisher) is an amber tracer: tracer hits light the target up, and your Uzi rounds dig 10% deeper into lit targets";

        /// <summary>曳光琥珀</summary>
        internal static readonly Color TracerAmber = new(255, 188, 92);

        /// <summary>抛壳左右交替记号；只在 owner 射击链读写</summary>
        private int casingSide = 1;

        /// <summary>乱射档曳光计数（每 4 发一枚）；只在 owner 射击链读写</summary>
        private int tracerCounter;

        /// <summary>曳光标记：目标编号/类型/截止帧。owner 本地量，收益只走攻击方端结算</summary>
        private int markNpc = -1;
        private int markNpcType;
        private uint markUntil;

        public override GsFireMode[] Modes { get; } = [
            new GsFireMode {
                Key = "ModeAkimbo", EnName = "Akimbo",
                UseSpeed = 1.30f, DamageMul = 0.87f,
                ExtraSpread = MathHelper.ToRadians(6f),
            },
            new GsFireMode {
                Key = "ModeBurst", EnName = "Burst",
                UseSpeed = 1.15f, DamageMul = 1.35f, Converge = 1f,
                BurstCount = 3, BurstRest = 18,
            },
        ];

        //冲锋枪后坐：轻快高频，点射档略沉
        protected override float RecoilShift => 2.4f;
        protected override float RecoilKick => 0.04f;
        protected override float RecoilScale(Item item, Player player, GsFireMode mode)
            => mode.BurstCount > 0 ? 1.35f : 0.9f;

        protected override void GsGunModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            //曳光换装：乱射档每第 4 发，点射档链末发
            bool tracer = mp.ModeIndex == 0
                ? ++tracerCounter % 4 == 0
                : mp.BurstShots == mode.BurstCount - 1;
            if (tracer) {
                type = ModContent.ProjectileType<GsUziTracerProj>();
            }
        }

        protected override bool? GsGunShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback,
            GsFireMode mode, GsGunsHardPlayer mp) {
            if (!VaultUtils.isServer) {
                //双持演出：弹壳左右交替甩出（乱射档），点射档规整右抛
                casingSide = mp.ModeIndex == 0 ? -casingSide : 1;
                Vector2 unit = velocity.SafeNormalize(Vector2.UnitX * player.direction);
                Vector2 side = unit.RotatedBy(MathHelper.PiOver2 * casingSide);
                PRTLoader.NewParticle<PRT_ProcChip>(position + unit * 8f,
                    side * Main.rand.NextFloat(1.5f, 2.5f) - Vector2.UnitY * Main.rand.NextFloat(1.5f, 3f),
                    new Color(206, 170, 96), Main.rand.NextFloat(0.4f, 0.55f))
                    ?.Configure(new Color(255, 226, 142), Main.rand.Next(18, 28));
                //曳光出膛：枪口琥珀闪
                if (type == ModContent.ProjectileType<GsUziTracerProj>()) {
                    PRTLoader.NewParticle<PRT_Light>(position + unit * 14f, unit * 2f,
                        TracerAmber, 0.12f)?.Configure(7, 0.8f);
                }
            }
            return null;
        }

        /// <summary>攻击方端结算：曳光命中点亮目标；被点亮目标吃乌兹弹 +10%</summary>
        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (proj.type != ModContent.ProjectileType<GsUziTracerProj>()
                || target.friendly || !target.active) {
                return;
            }
            markNpc = target.whoAmI;
            markNpcType = target.type;
            markUntil = Main.GameUpdateCount + 90;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item41 with { Volume = 0.4f, Pitch = 0.65f }, target.Center);
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, TracerAmber, 0.16f)
                    ?.Configure(9, 0.85f);
            }
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //标记是 owner 本地量，判定端即攻击方端，读到即权威
            if (target.whoAmI == markNpc && target.type == markNpcType
                && Main.GameUpdateCount < markUntil) {
                modifiers.FinalDamage *= 1.10f;
            }
        }

        protected override void GsGunHoldLocal(Item item, Player player, GsGunsHardPlayer mp) {
            //点亮读数：标记目标身上琥珀余光（归属者个人反馈）
            if (VaultUtils.isServer || markNpc < 0 || Main.GameUpdateCount >= markUntil
                || Main.GameUpdateCount % 6 != 0) {
                return;
            }
            NPC npc = Main.npc[markNpc];
            if (npc.active && npc.type == markNpcType) {
                PRTLoader.NewParticle<PRT_Spark>(npc.Center + Main.rand.NextVector2Circular(npc.width * 0.4f, npc.height * 0.4f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.4f, 1f), TracerAmber,
                    Main.rand.NextFloat(0.2f, 0.32f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        internal override void GsGunHeldReset(Player player) {
            casingSide = 1;
            tracerCounter = 0;
            markNpc = -1;
            markUntil = 0;
        }
    }

    /// <summary>
    /// 乌兹曳光弹：高速直飞的琥珀光带弹。命中点亮目标（收益在方案侧结算）。
    /// 自绘双层速度拉伸光带 + 白炽核，飞行相低频火星，命中琥珀迸溅
    /// </summary>
    internal class GsUziTracerProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithGunsHard";

        public override void SetDefaults() {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 200;
            Projectile.extraUpdates = 3;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, GsUzi.TracerAmber.ToVector3() * 0.24f);
            if (!VaultUtils.isServer && Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    -Projectile.velocity * 0.03f + Main.rand.NextVector2Circular(0.3f, 0.3f),
                    GsUzi.TracerAmber, Main.rand.NextFloat(0.16f, 0.26f))
                    ?.Configure(false, Main.rand.Next(6, 10));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //弹体 = 双层速度拉伸光带（黑底贴图 A=0 加色），核白尾琥珀
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.10f, 0.6f, 2.6f);
            Vector2 scaleOuter = new(0.30f * stretch, 0.075f);
            Vector2 scaleCore = new(0.20f * stretch, 0.045f);
            Color outer = GsUzi.TracerAmber * 0.85f;
            outer.A = 0;
            Color core = Color.White * 0.7f;
            core.A = 0;
            Main.EntitySpriteDraw(glow, pos, null, outer, Projectile.rotation,
                glow.Size() / 2f, scaleOuter, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, core, Projectile.rotation,
                glow.Size() / 2f, scaleCore, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    -Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.8) * Main.rand.NextFloat(1.5f, 4f),
                    i % 2 == 0 ? GsUzi.TracerAmber : Color.White,
                    Main.rand.NextFloat(0.2f, 0.34f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }
    }
}
