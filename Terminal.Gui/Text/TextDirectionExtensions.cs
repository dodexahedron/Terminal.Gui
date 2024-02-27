namespace Terminal.Gui;

public static class TextDirectionExtensions
{
    /// <summary>Check if it is a horizontal direction</summary>
    public static bool IsHorizontal (this TextDirection textDirection)
    {
        return textDirection switch
               {
                   TextDirection.LeftRight_TopBottom => true,
                   TextDirection.LeftRight_BottomTop => true,
                   TextDirection.RightLeft_TopBottom => true,
                   TextDirection.RightLeft_BottomTop => true,
                   _ => false
               };
    }

    /// <summary>Check if it is a vertical direction</summary>
    public static bool IsVertical (this TextDirection textDirection)
    {
        return textDirection switch
               {
                   TextDirection.TopBottom_LeftRight => true,
                   TextDirection.TopBottom_RightLeft => true,
                   TextDirection.BottomTop_LeftRight => true,
                   TextDirection.BottomTop_RightLeft => true,
                   _ => false
               };
    }

    /// <summary>Check if it is Left to Right direction</summary>
    public static bool IsLeftToRight (this TextDirection textDirection)
    {
        return textDirection switch
               {
                   TextDirection.LeftRight_TopBottom => true,
                   TextDirection.LeftRight_BottomTop => true,
                   _ => false
               };
    }

    /// <summary>Check if it is Top to Bottom direction</summary>
    public static bool IsTopToBottom (this TextDirection textDirection)
    {
        return textDirection switch
               {
                   TextDirection.TopBottom_LeftRight => true,
                   TextDirection.TopBottom_RightLeft => true,
                   _ => false
               };
    }
}
