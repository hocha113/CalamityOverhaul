using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills
{
    internal static class CloneFish
    {
        // 技能 ID（供 HalibutOverride 选择）
        public static int ID = 3;
        // 延迟帧数（1 秒 = 60 帧）
        public const int ReplayDelay = 60;

        // 右键调用：开启/关闭克隆
        public static void AltUse(Item item, Player player) {
            Activate(player);
        }

        public static void Activate(Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (hp.CloneFishActive)
                return;
            hp.CloneFishActive = true;
            if (Main.myPlayer == player.whoAmI) {
                SpawnCloneProjectile(player);
            }
        }

        public static void Deactivate(Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            hp.CloneFishActive = false;
            // 投射物会在自身 AI 中检测到并自杀
        }

        internal static void SpawnCloneProjectile(Player player) {
            var source = player.GetSource_Misc("CloneFishSkill");
            Projectile.NewProjectile(source, player.Center, Vector2.Zero, ModContent.ProjectileType<ClonePlayer>(), 0, 0, player.whoAmI);
        }
    }

    #region 数据结构
    internal struct PlayerSnapshot {
        public Vector2 Position;
        public Vector2 Velocity;
        public int Direction;
        public int SelectedItem;
        public int ItemAnimation;
        public int ItemTime;
        public float ItemRotation;
        public Rectangle BodyFrame;

        public PlayerSnapshot(Player p) {
            Position = p.position;
            Velocity = p.velocity;
            Direction = p.direction;
            SelectedItem = p.selectedItem;
            ItemAnimation = p.itemAnimation;
            ItemTime = p.itemTime;
            ItemRotation = p.itemRotation;
            BodyFrame = p.bodyFrame;
        }
    }

    internal struct CloneShootEvent {
        public int FrameIndex; // 事件原始发生帧
        public Vector2 Position;
        public Vector2 Velocity;
        public int Type;
        public int Damage;
        public float KnockBack;
        public int Owner;
        public int ItemType;
    }
    #endregion

    internal class ClonePlayer : ModProjectile
    {
        public override string Texture => "Terraria/Images/Extra_189"; // 占位，不实际绘制

        private static Player cloneRenderPlayer;

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 40;
            Projectile.timeLeft = 2; // 通过持续刷新保持存在
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active) {
                Projectile.Kill();
                return;
            }
            var hp = owner.GetOverride<HalibutPlayer>();
            if (hp == null || !hp.CloneFishActive) {
                Projectile.Kill();
                return;
            }
            // 保持持续
            Projectile.timeLeft = 2;

            // 取得延迟数据
            if (hp.CloneSnapshots.Count >= CloneFish.ReplayDelay) {
                int index = hp.CloneSnapshots.Count - CloneFish.ReplayDelay;
                var snap = hp.CloneSnapshots[index];
                Projectile.Center = snap.Position + owner.Size * 0.5f;
                Projectile.velocity = snap.Velocity;

                // 处理延迟射击事件
                int replayFrame = hp.CloneFrameCounter - CloneFish.ReplayDelay;
                if (hp.CloneShootEvents.Count > 0) {
                    for (int i = 0; i < hp.CloneShootEvents.Count; i++) {
                        var ev = hp.CloneShootEvents[i];
                        if (ev.FrameIndex == replayFrame) {
                            // 复制发射
                            if (Main.myPlayer == owner.whoAmI) {
                                int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), snap.Position + owner.Size * 0.5f, ev.Velocity, ev.Type, ev.Damage, ev.KnockBack, owner.whoAmI);
                                Main.projectile[proj].friendly = true;
                            }
                        }
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Player owner = Main.player[Projectile.owner];
            var hp = owner.GetOverride<HalibutPlayer>();

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap
                , null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);
            int index = hp.CloneSnapshots.Count - CloneFish.ReplayDelay;
            var snap = hp.CloneSnapshots[index];

            cloneRenderPlayer ??= new Player();

            // 基于原玩家拷贝关键外观状态
            var cp = cloneRenderPlayer;
            cp.ResetEffects();
            cp.CopyVisuals(owner);
            cp.position = snap.Position;
            cp.velocity = snap.Velocity;
            cp.direction = snap.Direction;
            cp.selectedItem = snap.SelectedItem;
            cp.itemAnimation = snap.ItemAnimation;
            cp.itemTime = snap.ItemTime;
            cp.itemRotation = snap.ItemRotation;
            cp.bodyFrame = snap.BodyFrame;
            // 让克隆不影响实际逻辑
            cp.whoAmI = owner.whoAmI;

            // 绘制
            try {
                Main.PlayerRenderer.DrawPlayer(Main.Camera, cp, cp.position, 0f, cp.fullRotationOrigin);
            }
            catch { }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap
                , null, Main.Rasterizer, null, Main.GameViewMatrix.ZoomMatrix);
            return false; // 我们已自绘
        }
    }

    internal static class PlayerCloneExtensions {
        // 拷贝外观相关（不复制装备全套逻辑，只复制可见内容）
        public static void CopyVisuals(this Player dst, Player src) {
            dst.skinVariant = src.skinVariant;
            dst.hair = src.hair;
            dst.hairColor = src.hairColor;
            dst.skinColor = src.skinColor;
            dst.eyeColor = src.eyeColor;
            dst.shirtColor = src.shirtColor;
            dst.underShirtColor = src.underShirtColor;
            dst.pantsColor = src.pantsColor;
            dst.shoeColor = src.shoeColor;
            for (int i = 0; i < dst.armor.Length && i < src.armor.Length; i++) {
                dst.armor[i] = src.armor[i].Clone();
            }
            for (int i = 0; i < dst.dye.Length && i < src.dye.Length; i++) {
                dst.dye[i] = src.dye[i].Clone();
            }
            dst.Male = src.Male;
            dst.bodyFrame = src.bodyFrame;
            dst.legFrame = src.legFrame;
            dst.headRotation = src.headRotation;
            dst.bodyRotation = src.bodyRotation;
            dst.legRotation = src.legRotation;
        }
    }
}
