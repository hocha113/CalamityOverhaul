using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【终结之钥】材质：黄铜鎏金的巨钥圣剑，齿刃衔锁。
    /// 签名：①原版失血增伤保留：目标血越少伤害越高，10% 血时翻倍，命中带钥匙粒子
    /// ②猎物低于三成血时刃脊亮起金色钥纹，宣告处决窗口已开
    /// ③处决击杀（三成血以下斩杀）在尸位炸开钥匙形金光与开锁音
    /// </summary>
    internal class GsKeybrand : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Keybrand;

        protected override int HeldProjID => ModContent.ProjectileType<GsKeybrandHeld>();

        protected override string GsDescFallback =>
            "Reforged: strikes deal up to double damage as the target's health falls; " +
            "below 30% health the blade lights golden key-runes, " +
            "and executing such prey bursts into unlocking light";

        //黄铜鎏金色板
        internal static readonly Color KeyBright = new(255, 244, 200); //鎏金亮缘
        internal static readonly Color KeyMain = new(216, 170, 84);    //黄铜体色
        internal static readonly Color KeyHot = new(255, 190, 64);     //处决灼金
        internal static readonly Color KeyDeep = new(46, 34, 14);      //暗铜垫影

        //原版低血增伤（至 +100%）在 ModifyHitExtra 等效保留，两侧同倍率不入预算；
        //拍表 1.0/1.0/1.3 均摊 ~1.1x，三拍循环 ~67 帧对原版 20 帧/斩 帧效率 ~0.97x，
        //底伤 +8% 兜底，处决爆光纯演出零伤 → 综合 DPS 约为原版 104%~112%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 终结之钥手持：三拍连段。0 横斩 / 1 返斩 / 2 落锁重劈（前压终结）。
    /// 失血增伤走 ModifyHitExtra，处决击杀在命中记账里生成开锁爆光。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsKeybrandHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Keybrand;
        protected override Color EdgeBright => GsKeybrand.KeyBright;
        protected override Color BodyMain => GsKeybrand.KeyMain;
        protected override Color HotAccent => GsKeybrand.KeyHot;
        protected override Color DeepShadow => GsKeybrand.KeyDeep;

        /// <summary>附近存在三成血以下的猎物（只驱动钥纹演出，非服务器端扫描）</summary>
        private bool executeReady;
        private int scanTimer;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 横斩
            0 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.85f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.06f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.9f, Follow = 1.05f, ReachScale = 1.02f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.14f,
            },
            //拍2 落锁：长举前压重劈，钥齿咬合
            _ => new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 12,
                RaiseBack = 2.2f, Follow = 1.25f, ReachScale = 1.15f, LeanAmp = 0.085f,
                DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 3.0f, SwingPitch = -0.28f,
            },
        };

        //处决窗口开启时刃身常亮灼金
        protected override bool GlowAlways => IsFinisher || executeReady;
        protected override Color GlowColor => executeReady ? GsKeybrand.KeyHot : GsKeybrand.KeyBright;

        protected override void HandlePhaseEvents(int phase) {
            base.HandlePhaseEvents(phase);
            //钥纹扫描：附近是否有三成血以下的可追猎目标（纯演出量，服务器不扫）
            if (!VaultUtils.isServer && ++scanTimer >= 6) {
                scanTimer = 0;
                executeReady = FindLowPrey();
            }
        }

        private bool FindLowPrey() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                if (npc.life < npc.lifeMax * 0.30f && npc.DistanceSQ(Owner.Center) < 560f * 560f) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>原版失血增伤等效保留：血量 100%→10% 线性升到 +100%</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            float lifeRatio = target.life / (float)target.lifeMax;
            float bonus = Utils.GetLerpValue(1f, 0.1f, lifeRatio, clamped: true);
            if (bonus > 0f) {
                modifiers.SourceDamage *= 1f + bonus;
            }
        }

        /// <summary>命中记账：原版钥匙粒子广播 + 处决击杀生成开锁爆光</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            //原版钥匙粒子：clientOnly false 走服务器转播，所有端可见
            ParticleOrchestraSettings settings = new() {
                PositionInWorld = target.Hitbox.ClosestPointInRect(mainTip),
            };
            ParticleOrchestrator.RequestParticleSpawn(clientOnly: false,
                ParticleOrchestraType.Keybrand, settings, Owner.whoAmI);

            //处决判定：命中前已低于三成血且这一击致死
            int preLife = target.life + damageDone;
            if (target.life <= 0 && preLife <= (int)(target.lifeMax * 0.30f)) {
                SpawnOwnedProj(ModContent.ProjectileType<GsKeybrandUnlockProj>(),
                    target.Center, Vector2.Zero, 0, 0f);
            }
        }

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.45f }, Owner.Center);
                if (executeReady) {
                    //处决窗口内的落锁拍：一声轻脆的锁舌预响
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.3f, Pitch = 0.45f }, Owner.Center);
                }
            }
        }

        /// <summary>处决窗口钥纹：沿刀脊排四粒金色钥齿刻光，明灭相位错开</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (!executeReady || fanFade <= 0.05f) {
                return;
            }
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (star == null) {
                return;
            }
            Vector2 hand = Hand;
            for (int i = 0; i < 4; i++) {
                Vector2 at = hand + mainAngle.ToRotationVector2() * (mainReach * (0.36f + 0.15f * i))
                    - Main.screenPosition;
                float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + i * 1.7f + DrawRand01(i) * 2f);
                Color c = GsKeybrand.KeyHot * (0.55f * fanFade * pulse);
                c.A = 0;
                sb.Draw(star, at, null, c, MathHelper.PiOver4, star.Size() * 0.5f,
                    0.13f + 0.03f * DrawRand01(i + 8), SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 开锁爆光：处决击杀的演出弹幕（零伤）。金色钥匙轮廓自尸位展开，
    /// 6 帧过冲撑满后缓转渐散，配开锁音与金屑迸溅。绘制全走确定性相位
    /// </summary>
    internal class GsKeybrandUnlockProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 34;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.85f, Pitch = 0.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.35f, Pitch = 0.3f }, Projectile.Center);
                //金屑迸溅 + 金尘上飘
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 7f),
                        Main.rand.NextBool(3) ? GsKeybrand.KeyHot : GsKeybrand.KeyBright,
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(14, 24));
                }
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Light>(
                        Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.8f),
                        GsKeybrand.KeyMain, Main.rand.NextFloat(0.07f, 0.13f))?.Configure(14, 0.7f);
                }
            }
            Lighting.AddLight(Projectile.Center, GsKeybrand.KeyMain.ToVector3() * (0.8f * (1f - Life01)));
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (glow == null || star == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = 1f - Life01;
            //6 帧过冲撑满再回坐，随后整体缓转
            float grow = Life <= 6f ? 1.1f * (Life / 6f)
                : MathHelper.Lerp(1.1f, 1f, MathHelper.Clamp((Life - 6f) / 5f, 0f, 1f));
            float axis = SegRand(5) * MathHelper.TwoPi + Life * 0.02f;
            Vector2 dir = axis.ToRotationVector2();
            Vector2 side = (axis + MathHelper.PiOver2).ToRotationVector2();

            //爆心星芒：首帧最亮随后蚀散
            Color flash = GsKeybrand.KeyBright * (0.75f * fade * fade);
            flash.A = 0;
            Main.EntitySpriteDraw(star, center, null, flash, axis, star.Size() * 0.5f, 0.42f * grow, SpriteEffects.None, 0);

            void Bead(Vector2 at, float scale, Color c) {
                c.A = 0;
                Main.EntitySpriteDraw(glow, at, null, c, 0f, glow.Size() * 0.5f, scale, SpriteEffects.None, 0);
            }

            //钥匙轮廓：环首（8 珠圆环）+ 钥杆（5 珠直列）+ 双钥齿
            Color gold = GsKeybrand.KeyMain * (0.6f * fade);
            Color bright = GsKeybrand.KeyBright * (0.7f * fade);
            Vector2 headAt = center - dir * (20f * grow);
            for (int i = 0; i < 8; i++) {
                float ang = MathHelper.TwoPi * i / 8f + Life * 0.03f;
                Bead(headAt + ang.ToRotationVector2() * (13f * grow), 0.18f, gold);
            }
            for (int i = 0; i < 5; i++) {
                float t = i / 4f;
                Bead(center + dir * MathHelper.Lerp(-6f, 24f, t) * grow, 0.16f, bright);
            }
            //钥齿：杆末两粒垂出
            Bead(center + (dir * 14f + side * 9f) * grow, 0.15f, bright);
            Bead(center + (dir * 22f + side * 9f) * grow, 0.15f, bright);
            return false;
        }
    }
}
