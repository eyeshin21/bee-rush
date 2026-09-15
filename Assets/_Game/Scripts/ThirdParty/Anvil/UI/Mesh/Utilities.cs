namespace Anvil
{
    public static class Utilities
    {
        public static bool TrySet<T>(ref T currentValue, T newValue) where T : class
        {
            if (currentValue == null)
            {
                if (newValue == null) return false;
            }
            else
            {
                if (currentValue.Equals(newValue)) return false;
            }

            currentValue = newValue;
            return true;
        }
    }
}