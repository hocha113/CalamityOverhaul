using CalamityOverhaul.Content.Scenarios.Hadalworld.Gen.Passes;
using System.Collections.Generic;
using Terraria.WorldBuilding;

namespace CalamityOverhaul.Content.Scenarios.Hadalworld.Gen
{
    //深渊海沟生成管线(B路全权,冻结契约=BuildTasks签名):
    //P10模型演算(Terraria无关核心层,Gen\Core\)→P20浇筑直写→P30装饰→P80校验
    //设计蓝图:Doc\plans\Hadalworld\STRUCTURES.md(gitignore,按全路径读)
    //离线预览harness:%TEMP%\hadalprev(隔离csproj直接编入Gen\Core\*.cs,PIL渲染)
    internal static class HadalGenPipeline
    {
        internal static List<GenPass> BuildTasks() => [
            new HadalTimedPass(new HadalModelPass()),
            new HadalTimedPass(new HadalTilePass()),
            new HadalTimedPass(new HadalDecorPass()),
            new HadalValidatePass(),
        ];
    }
}
