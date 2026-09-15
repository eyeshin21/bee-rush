namespace HoneyBeeRush.Gameplay.Core
{
    public enum BlockGridBorderType
    {
        None = -1,
        Top = 0,
        Bottom = 1,
        Left = 2,
        Right = 3,
        TopLeft = 4,
        TopRight = 5,
        BottomLeft = 6,
        BottomRight = 7,
        LeftTopRight = 8,
        TopRightBottom = 9,
        RightBottomLeft = 10,
        BottomLeftTop = 11,
        ClosedBorder = 12,
        Wall = 13,
        TopBottom = 14,
        LeftRight = 15
    }

    public enum BlockGridCornerType
    {
        None = -1,
        TopLeftOut = 0,
        TopRightOut = 1,
        BottomLeftOut = 2,
        BottomRightOut = 3
    }
}
