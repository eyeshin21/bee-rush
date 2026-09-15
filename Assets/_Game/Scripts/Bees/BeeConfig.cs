namespace HoneyBeeRush.Bees
{
    [System.Serializable]
    public sealed class BeeConfig
    {
        public float maxSpeedOut = 7.5f;
        public float maxSpeedHome = 8.5f;
        public float accel = 26f;
        public float turnDamping = 9f;

        public float separationRadius = 0.34f;
        public float separationWeight = 5.0f;

        public float wanderWeight = 1.1f;
        public float wanderFreq = 1.7f;

        public float cellArriveRadius = 0.16f;
        public float homeArriveRadius = 0.42f;

        public float diveMargin = 0.55f;
        public float drainTime = 0.34f;
        public float drainHoverHeight = 0.30f;
        public float drainBob = 0.05f;

        public float returnRise = 0.55f;
        public float stuckTimeout = 6f;

        public float popDuration = 0.18f;
        public float launchFlipTime = 0.12f;

        public float outboundBobAmp = 0.07f;
        public float waggleYawAmp = 12f;
        public float waggleFreq = 6f;
        public float bankRoll = 22f;

        public float wingFlapFreq = 22f;
        public float wingFlapFreqDrain = 12f;

        public float finaleLoopRadius = 1.6f;
        public float finaleLoopDuration = 2.2f;
        public float finaleLoopUpSpeed = 2.5f;
        public float finaleExitSpeed = 6f;

        public float spawnScatter = 0.22f;
        public float hiveApproachDist = 1.6f;
        public float speedJitter = 0.18f;

        public float hiveEntryStandoff = 1.15f;
        public float hiveEntryArriveRadius = 0.45f;
        public float homeEnterTimeout = 5f;

        public float headingDamping = 16f;
        public float headingMaxTurnRate = 480f;
        public float headingMinSpeed = 0.8f;
        public float diveHysteresis = 1.3f;
        public float arriveEase = 0.12f;
        public float drainApproachGain = 6f;
        public float drainDamping = 12f;

        public float hiveEnterDuration = 0.45f;
        public float hiveEnterDepth = 0.75f;
        public float hiveEnterDip = 0.06f;
        public float hiveEnterShrinkStart = 0.35f;

        public float boardClearance = 0.25f;
        public float orbitLeadAngle = 40f;
        public float laneExtra = 0.2f;
        public float backFacePenalty = 6f;
        public float farSidePenalty = 3f;
        public float maxFlightPitch = 70f;

        public float HiveEntryStandoff => hiveEntryStandoff > 0f ? hiveEntryStandoff : 1.15f;
        public float HiveEntryArriveRadius => hiveEntryArriveRadius > 0f ? hiveEntryArriveRadius : 0.45f;
        public float HomeArriveRadius => homeArriveRadius > 0f ? homeArriveRadius : 0.42f;
        public float HomeEnterTimeout => homeEnterTimeout > 0f ? homeEnterTimeout : 5f;
        public float HeadingDamping => headingDamping > 0f ? headingDamping : 16f;
        public float HeadingMaxTurnRate => headingMaxTurnRate > 0f ? headingMaxTurnRate : 480f;
        public float HeadingMinSpeed => headingMinSpeed > 0f ? headingMinSpeed : 0.8f;
        public float DiveHysteresis => diveHysteresis > 1f ? diveHysteresis : 1.3f;
        public float ArriveEase => arriveEase > 0f ? arriveEase : 0.12f;
        public float DrainApproachGain => drainApproachGain > 0f ? drainApproachGain : 6f;
        public float DrainDamping => drainDamping > 0f ? drainDamping : 12f;
        public float HiveEnterDuration => hiveEnterDuration > 0f ? hiveEnterDuration : 0.45f;
        public float HiveEnterDepth => hiveEnterDepth > 0f ? hiveEnterDepth : 0.75f;
        public float BoardClearance => boardClearance > 0f ? boardClearance : 0.25f;
        public float OrbitLeadAngle => orbitLeadAngle > 0f ? orbitLeadAngle : 40f;
        public float LaneExtra => laneExtra > 0f ? laneExtra : 0.2f;
        public float BackFacePenalty => backFacePenalty > 0f ? backFacePenalty : 6f;
        public float FarSidePenalty => farSidePenalty > 0f ? farSidePenalty : 3f;
        public float MaxFlightPitch => maxFlightPitch > 0f ? maxFlightPitch : 70f;
    }
}
