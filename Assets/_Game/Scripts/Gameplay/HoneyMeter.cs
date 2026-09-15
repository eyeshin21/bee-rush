using UnityEngine;

namespace HoneyBeeRush.Gameplay
{
    public sealed class HoneyMeter
    {
        public int Total { get; private set; }
        public int Collected { get; private set; }

        public float Percent01 => Total > 0 ? Collected / (float)Total : 0f;

        public int Percent
        {
            get
            {
                int value = Mathf.RoundToInt(Percent01 * 100f);
                if (value > 100) value = 100;
                if (value < 0) value = 0;
                if (value >= 100 && Collected < Total) value = 99;
                return value;
            }
        }

        public event System.Action Changed;

        public void Reset(int total)
        {
            Total = total < 0 ? 0 : total;
            Collected = 0;
            Changed?.Invoke();
        }

        public void AddLoad(int amount)
        {
            if (amount <= 0) return;

            int next = Collected + amount;
            if (next > Total) next = Total;
            if (next == Collected) return;

            Collected = next;
            Changed?.Invoke();
        }
    }
}
