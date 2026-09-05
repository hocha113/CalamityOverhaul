using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【终结之钥】材质：黄铜鎏金的巨钥圣剑，齿刃衔锁。
    /// 签名：①原版失血增伤保留：目标血越少伤害越高，10% 血时翻倍，命中带原版钥匙粒子
    /// ②猎物低于三成血时落锁拍带锁舌预响，宣告处决窗口已开
    /// ③处决击杀（三成血以下斩杀）在尸位响开锁音
    /// </summary>
    internal class GsKeybrand : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Keybrand;

        protected override int HeldProjID => ModContent.ProjectileType<GsKeybrandHeld>();

        protected override string GsDescFallback =>
            "Reforged: strikes deal up to double damage as the target's health falls; below 30% health the blade lights golden key-runes, and executing such prey bursts into unlocking light";
        internal static readonly Color KeyBright = new(255, 244, 200); //鎏金亮缘
        internal static readonly Color KeyMain = new(216, 170, 84);    //黄铜体色
        internal static readonly Color KeyHot = new(255, 190, 64);     //处决灼金

        //原版低血增伤（至 +100%）在 ModifyHitExtra 等效保留，两侧同倍率不入预算；
        //拍表 1.0/1.0/1.3 均摊 ~1.1x，三拍循环 ~67 帧对原版 20 帧/斩 帧效率 ~0.97x，
        //底伤 +8% 兜底，处决开锁音零伤 → 综合 DPS 约为原版 104%~112%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 终结之钥手持：三拍连段。0 横斩 / 1 返斩 / 2 落锁重劈（前压终结）。
    /// 失血增伤走 ModifyHitExtra，处决击杀在命中记账里生成开锁音载体。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsKeybrandHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Keybrand;
        protected override Color EdgeBright => GsKeybrand.KeyBright;
        protected override Color BodyMain => GsKeybrand.KeyMain;
        protected override Color HotAccent => GsKeybrand.KeyHot;

        /// <summary>附近存在三成血以下的猎物（只驱动锁舌预响音效，非服务器端扫描）</summary>
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

        protected override void HandlePhaseEvents(int phase) {
            base.HandlePhaseEvents(phase);
            //处决窗口扫描：附近是否有三成血以下的可追猎目标（只喂音效，服务器不扫）
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

        /// <summary>命中记账：原版钥匙粒子广播 + 处决击杀生成开锁音载体</summary>
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
    }

    /// <summary>
    /// 开锁音载体：处决击杀生成的零伤隐形弹幕，随生成包全端可闻开锁音
    /// </summary>
    internal class GsKeybrandUnlockProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 34;
        private ref float Life => ref Projectile.localAI[0];

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
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
