namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    //教程高亮目标：暴露 UI 屏幕矩形，无需改目标 UI 内部
    internal interface ICybTutorialTarget
    {
        string Key { get; }
        //当前帧目标屏幕矩形，供高亮框/箭头
        Rectangle GetScreenRect();
        //目标当前是否可见可操作
        bool IsAvailable { get; }
    }
}
