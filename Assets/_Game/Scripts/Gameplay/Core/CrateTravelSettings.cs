using System;

namespace HoneyBeeRush.Gameplay.Core
{
    [Serializable]
    public struct CrateTravelSettings
    {
        public float hopStepDuration;
        public float flySpeed;
        public float jumpHeight;
        public float jumpHeightPerUnit;
        public float jumpMaxHeight;
        public float jumpMinDuration;
        public float jumpMaxDuration;
        public float jumpTilt;
        public float jumpDepthPop;
    }
}
