using InnoVault.Actors;
using SubworldLibrary;
using System.Collections.Generic;
using Terraria;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.ADV.Scenarios.AcheronProtocols.Machines.DropPodScens
{
    /// <summary>空降仓演出子世界：极小的空世界</summary>
    internal class DropPodWorld : Subworld
    {
        public static DropPodWorld Instance { get; private set; }

        //极小的世界尺寸，仅用于演出
        public override int Width => 800;

        public override int Height => 600;

        public static bool Active => SubworldSystem.IsActive<DropPodWorld>();

        public override List<GenPass> Tasks => [new DropPodGen()];

        public static void Enter() {
            SubworldSystem.Enter<DropPodWorld>();
        }

        public static void Exit() {
            SubworldSystem.Exit();
        }

        public override void Load() {
            Instance = this;
        }

        public override void Unload() {
            Instance = null;
        }

        public override void OnEnter() {

        }

        public override void OnExit() {
            base.OnExit();
        }

        public override void OnLoad() {
            Main.dayTime = true;
            Main.time = Main.dayLength / 2;
            //将地表线和岩石层推到世界底部，完全隐藏地下背景
            Main.worldSurface = Main.maxTilesY - 2;
            Main.rockLayer = Main.maxTilesY - 1;
        }

        public override void Update() {
            //保持夜晚状态
            Main.dayTime = true;
            Main.time = Main.dayLength / 2;

            if (ActorLoader.GetActiveActors<DropPodActor>().Count == 0) {
                ActorLoader.NewActor<DropPodActor>(Main.LocalPlayer.Center, Vector2.Zero);
            }
        }

        public override float GetGravity(Entity entity) {
            return 0f;//无重力，玩家悬浮
        }
    }
}
