using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【秘银共鸣】材质：奥术绿松秘银锻刃。
    /// 签名：①每一斩在目标身上烙一道共鸣印，印随命中递升音阶
    /// ②同一目标集齐三印即引发秘银震荡波（半径 150 的就地爆发，不限拍号）
    /// ③每一挥都叠一层水晶和音，命中反馈是清冷的秘银泛音
    /// </summary>
    internal class GsMythrilSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.MythrilSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsMythrilSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash brands its target with a mythril seal; " +
            "the third seal rings out as a resonant shockwave";

        //绿松秘银色板
        internal static readonly Color ResBright = new(178, 255, 226); //翠亮刃缘
        internal static readonly Color ResMain = new(66, 196, 156);    //秘银翠
        internal static readonly Color ResHot = new(110, 240, 255);    //共鸣青辉
        internal static readonly Color ResDeep = new(18, 52, 44);      //深翠垫影

        //预算账：拍均 (1+1+1.22)/3≈1.07；三印齐鸣 0.55x 每三次命中引爆一次（单体 ≈ +0.18/拍）；
        //连段总帧 (20+19+24)=63 对原版 60 (+5%) → 综合单体 DPS ≈ (1.07+0.18)×0.95 ≈ 原版 105%~119%
        //（震荡波半径 150 的多目标收益另计），底伤不再加成
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 秘银共鸣手持：三拍工整连击，每挥叠水晶和音（音阶随拍号上行）。
    /// owner 端按目标记共鸣印，三印即在目标处引爆震荡波（不限拍号）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsMythrilSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.MythrilSword;
        protected override Color EdgeBright => GsMythrilSword.ResBright;
        protected override Color BodyMain => GsMythrilSword.ResMain;
        protected override Color HotAccent => GsMythrilSword.ResHot;
        protected override Color DeepShadow => GsMythrilSword.ResDeep;

        /// <summary>共鸣印计数：whoAmI → (npc 类型, 印数)。命中判定只在 owner 端跑，
        /// 本表只被本地玩家的挥砍读写；类型不符视为槽位复用，重新记数</summary>
        private static readonly Dictionary<int, (int npcType, int brands)> brandMap = [];

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 正斩
            0 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.85f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.1f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.9f, Follow = 1.05f, ReachScale = 1.02f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.1f,
            },
            //拍2 阔斩：稍沉前压收束
            _ => new GsBroadBeat {
                Raise = 7, Hold = 3, Slash = 4, Recover = 10,
                RaiseBack = 2.1f, Follow = 1.2f, ReachScale = 1.1f, LeanAmp = 0.07f,
                DamageMult = 1.22f, Hitstop = 2, LungeSpeed = 2.4f, SwingPitch = -0.2f,
            },
        };

        /// <summary>每一挥叠一层水晶和音：底哨恒定，和音随拍号上行三度</summary>
        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = Beat.SwingPitch }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item29 with {
                Volume = 0.2f, Pitch = -0.1f + 0.25f * ComboStage, MaxInstances = 3
            }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.3f, Pitch = -0.3f }, Owner.Center);
            }
        }

        /// <summary>命中烙印：owner 端记账；三印引爆震荡波并清印</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            PruneBrands();
            int brands = brandMap.TryGetValue(target.whoAmI, out (int npcType, int brands) n) && n.npcType == target.type
                ? n.brands + 1 : 1;
            if (brands >= 3) {
                //三印齐鸣：就地引爆震荡波（伤害走本次挥砍伤害的 55%）
                brandMap.Remove(target.whoAmI);
                int waveDamage = Math.Max(1, (int)(Projectile.damage * 0.55f));
                SpawnOwnedProj(ModContent.ProjectileType<GsMythrilSwordWaveProj>(),
                    target.Center, Vector2.Zero, waveDamage, Projectile.knockBack * 0.6f);
                return;
            }
            brandMap[target.whoAmI] = (target.type, brands);
            //印记递升音阶 + 印数颗翠星贴在伤口上方
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with {
                    Volume = 0.3f, Pitch = 0.2f + 0.25f * brands, MaxInstances = 3
                }, target.Center);
                for (int i = 0; i < brands; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(
                        target.Center + new Vector2((i - (brands - 1) * 0.5f) * 14f, -target.height * 0.5f - 6f),
                        -Vector2.UnitY * 0.5f, Color.White, 0.75f)
                        ?.Configure(GsMythrilSword.ResHot, 20, 0.1f, 1.1f);
                }
            }
        }

        /// <summary>表过大时清掉已消亡/槽位复用的条目</summary>
        private static void PruneBrands() {
            if (brandMap.Count <= 64) {
                return;
            }
            List<int> dead = [];
            foreach (KeyValuePair<int, (int npcType, int brands)> kv in brandMap) {
                NPC npc = Main.npc[kv.Key];
                if (!npc.active || npc.type != kv.Value.npcType) {
                    dead.Add(kv.Key);
                }
            }
            foreach (int k in dead) {
                brandMap.Remove(k);
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //秘银泛音光点
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                GsMythrilSword.ResHot, IsFinisher ? 0.24f : 0.15f)?.Configure(10, 0.75f);
        }
    }

    /// <summary>
    /// 秘银震荡波：三印齐鸣的就地爆发，10 帧过冲撑到半径 150 后回坐，伤害只在扩张期结算一次。
    /// 自绘三件套：外圈虚线环（弧段沿切向排布）+ 内圈反相细环 + 三枚坍缩翠星沿半径旋入爆心，
    /// 佐镜头光斑与中心闪。绘制全走 identity 播种与确定相位，禁 Main.rand
    /// </summary>
    internal class GsMythrilSwordWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.MythrilSword");

        private const int TotalLife = 22;
        private const float MaxRadius = 150f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        /// <summary>当前扩张半径：10 帧过冲 5% 再回坐</summary>
        private float Radius {
            get {
                float p = MathHelper.Clamp(Life / 10f, 0f, 1f);
                float burst = p < 0.72f ? 1.05f * (p / 0.72f) : MathHelper.Lerp(1.05f, 1f, (p - 0.72f) / 0.28f);
                return MaxRadius * burst;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                //齐鸣：双层钟音 + 翠青火花外抛
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = -0.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item101 with { Volume = 0.3f, Pitch = 0.3f }, Projectile.Center);
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f),
                        Main.rand.NextBool(3) ? GsMythrilSword.ResHot : GsMythrilSword.ResBright,
                        Main.rand.NextFloat(0.32f, 0.55f))?.Configure(true, Main.rand.Next(12, 20));
                }
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Light>(
                        Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.6f),
                        GsMythrilSword.ResMain, Main.rand.NextFloat(0.07f, 0.13f))?.Configure(12, 0.7f);
                }
            }
            Lighting.AddLight(Projectile.Center, GsMythrilSword.ResMain.ToVector3() * (0.8f * (1f - Life01)));
        }

        //伤害只在扩张期结算（一目标一次）
        public override bool? CanDamage() => Life <= 10f ? null : false;

        /// <summary>圆形判定：目标碰到当前扩张半径即命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);//击退向外

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D dash = CWRAsset.SemiCircularSmear?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            Texture2D flare = CWRAsset.StarFlare01?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (dash == null || star == null || flare == null || glow == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = 1f - Life01;
            float radius = Radius;
            float spin = Main.GlobalTimeWrappedHourly * 1.6f + SegRand(2) * 6.28f;

            //中心闪：首帧最亮随后蚀散
            Color flash = GsMythrilSword.ResBright * (0.7f * fade * fade);
            flash.A = 0;
            Main.EntitySpriteDraw(glow, center, null, flash, 0f, glow.Size() * 0.5f,
                0.7f * (0.5f + 0.5f * Life01), SpriteEffects.None, 0);
            Color flareC = GsMythrilSword.ResHot * (0.45f * fade);
            flareC.A = 0;
            Main.EntitySpriteDraw(flare, center, null, flareC, Life * 0.06f, flare.Size() * 0.5f,
                0.42f, SpriteEffects.None, 0);

            //外圈虚线环：14 段弧刻沿切向排布，随波旋进
            const int dashes = 14;
            for (int i = 0; i < dashes; i++) {
                float ang = MathHelper.TwoPi * i / dashes + spin * 0.3f;
                Vector2 at = center + ang.ToRotationVector2() * radius;
                Color seg = GsMythrilSword.ResMain * (0.55f * fade);
                seg.A = 0;
                Main.EntitySpriteDraw(dash, at, null, seg, ang + MathHelper.PiOver2, dash.Size() * 0.5f,
                    new Vector2(0.1f, 0.05f), SpriteEffects.None, 0);
            }
            //内圈反相细环：10 段，逆旋
            const int inner = 10;
            for (int i = 0; i < inner; i++) {
                float ang = MathHelper.TwoPi * (i + 0.5f) / inner - spin * 0.4f;
                Vector2 at = center + ang.ToRotationVector2() * (radius * 0.76f);
                Color seg = GsMythrilSword.ResBright * (0.4f * fade);
                seg.A = 0;
                Main.EntitySpriteDraw(dash, at, null, seg, ang + MathHelper.PiOver2, dash.Size() * 0.5f,
                    new Vector2(0.07f, 0.035f), SpriteEffects.None, 0);
            }
            //三枚坍缩翠星：自外缘旋入爆心（印记归位的具象）
            for (int i = 0; i < 3; i++) {
                float ang = spin + MathHelper.TwoPi * i / 3f;
                float dist = radius * (1f - MathHelper.Clamp(Life / 12f, 0f, 1f)) * 0.85f;
                Vector2 at = center + ang.ToRotationVector2() * dist;
                Color sig = GsMythrilSword.ResHot * (0.65f * fade);
                sig.A = 0;
                Main.EntitySpriteDraw(star, at, null, sig, ang + Life * 0.12f, star.Size() * 0.5f,
                    0.2f + 0.06f * SegRand(i + 20), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
