namespace HoneyBeeRush.Gameplay.Core
{
    [System.Serializable]
    public struct BoardViewSettings
    {
        public bool rotationEnabled;
        public float dragDegreesPerScreenHeight;
        public float maxPitch;
        public float inertiaDamping;
        public float minInertiaSpeed;

        public static BoardViewSettings Default => new BoardViewSettings
        {
            rotationEnabled = true,
            dragDegreesPerScreenHeight = 260f,
            maxPitch = 75f,
            inertiaDamping = 5f,
            minInertiaSpeed = 4f
        };
    }
}
