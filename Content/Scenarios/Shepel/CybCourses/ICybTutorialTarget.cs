namespace CalamityOverhaul.Content.Scenarios.Shepel.CybCourses
{
    //高亮目标屏幕矩形，不改目标UI
    internal interface ICybTutorialTarget
    {
        string Key { get; }
        Rectangle GetScreenRect();
        bool IsAvailable { get; }
    }
}
