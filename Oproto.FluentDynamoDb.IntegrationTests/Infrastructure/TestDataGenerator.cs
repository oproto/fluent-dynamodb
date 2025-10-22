namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Provides random data generation for integration tests.
/// Supports seeded random generation for reproducible tests.
/// </summary>
public class TestDataGenerator
{
    private readonly Random _random;
    
    /// <summary>
    /// Initializes a new instance of TestDataGenerator with a random seed.
    /// </summary>
    public TestDataGenerator()
    {
        _random = new Random();
    }
    
    /// <summary>
    /// Initializes a new instance of TestDataGenerator with a specific seed.
    /// Use this for reproducible test data generation.
    /// </summary>
    /// <param name="seed">The seed value for the random number generator.</param>
    public TestDataGenerator(int seed)
    {
        _random = new Random(seed);
    }
    
    /// <summary>
    /// Generates a random string of the specified length.
    /// </summary>
    /// <param name="length">The length of the string to generate.</param>
    /// <param name="includeNumbers">Whether to include numbers in the string.</param>
    /// <param name="includeSpecialChars">Whether to include special characters.</param>
    /// <returns>A random string.</returns>
    public string GenerateString(
        int length = 10,
        bool includeNumbers = true,
        bool includeSpecialChars = false)
    {
        const string letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string numbers = "0123456789";
        const string specialChars = "!@#$%^&*()-_=+[]{}|;:,.<>?";
        
        var chars = letters;
        if (includeNumbers) chars += numbers;
        if (includeSpecialChars) chars += specialChars;
        
        return new string(Enumerable.Range(0, length)
            .Select(_ => chars[_random.Next(chars.Length)])
            .ToArray());
    }
    
    /// <summary>
    /// Generates a random alphanumeric string (letters and numbers only).
    /// </summary>
    /// <param name="length">The length of the string to generate.</param>
    /// <returns>A random alphanumeric string.</returns>
    public string GenerateAlphanumeric(int length = 10)
    {
        return GenerateString(length, includeNumbers: true, includeSpecialChars: false);
    }
    
    /// <summary>
    /// Generates a random email address.
    /// </summary>
    /// <returns>A random email address.</returns>
    public string GenerateEmail()
    {
        var username = GenerateString(8, includeNumbers: true, includeSpecialChars: false).ToLower();
        var domain = GenerateString(6, includeNumbers: false, includeSpecialChars: false).ToLower();
        return $"{username}@{domain}.com";
    }
    
    /// <summary>
    /// Generates a random GUID string.
    /// </summary>
    /// <returns>A random GUID string.</returns>
    public string GenerateGuid()
    {
        return Guid.NewGuid().ToString();
    }
    
    /// <summary>
    /// Generates a random integer within the specified range.
    /// </summary>
    /// <param name="min">The minimum value (inclusive).</param>
    /// <param name="max">The maximum value (exclusive).</param>
    /// <returns>A random integer.</returns>
    public int GenerateInt(int min = 0, int max = 1000)
    {
        return _random.Next(min, max);
    }
    
    /// <summary>
    /// Generates a random long within the specified range.
    /// </summary>
    /// <param name="min">The minimum value (inclusive).</param>
    /// <param name="max">The maximum value (exclusive).</param>
    /// <returns>A random long.</returns>
    public long GenerateLong(long min = 0, long max = 1000000)
    {
        return _random.NextInt64(min, max);
    }
    
    /// <summary>
    /// Generates a random decimal within the specified range.
    /// </summary>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    /// <param name="decimalPlaces">The number of decimal places.</param>
    /// <returns>A random decimal.</returns>
    public decimal GenerateDecimal(decimal min = 0, decimal max = 1000, int decimalPlaces = 2)
    {
        var value = (decimal)_random.NextDouble() * (max - min) + min;
        return Math.Round(value, decimalPlaces);
    }
    
    /// <summary>
    /// Generates a random double within the specified range.
    /// </summary>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    /// <returns>A random double.</returns>
    public double GenerateDouble(double min = 0, double max = 1000)
    {
        return _random.NextDouble() * (max - min) + min;
    }
    
    /// <summary>
    /// Generates a random boolean value.
    /// </summary>
    /// <returns>A random boolean.</returns>
    public bool GenerateBool()
    {
        return _random.Next(2) == 1;
    }
    
    /// <summary>
    /// Generates a random DateTime within the specified range.
    /// </summary>
    /// <param name="minDate">The minimum date.</param>
    /// <param name="maxDate">The maximum date.</param>
    /// <returns>A random DateTime.</returns>
    public DateTime GenerateDateTime(DateTime? minDate = null, DateTime? maxDate = null)
    {
        var min = minDate ?? DateTime.UtcNow.AddYears(-1);
        var max = maxDate ?? DateTime.UtcNow.AddYears(1);
        
        var range = (max - min).TotalSeconds;
        var randomSeconds = _random.NextDouble() * range;
        
        return min.AddSeconds(randomSeconds);
    }
    
    /// <summary>
    /// Generates a random byte array of the specified length.
    /// </summary>
    /// <param name="length">The length of the byte array.</param>
    /// <returns>A random byte array.</returns>
    public byte[] GenerateBytes(int length = 16)
    {
        var bytes = new byte[length];
        _random.NextBytes(bytes);
        return bytes;
    }
    
    /// <summary>
    /// Generates a list of random strings.
    /// </summary>
    /// <param name="count">The number of strings to generate.</param>
    /// <param name="stringLength">The length of each string.</param>
    /// <returns>A list of random strings.</returns>
    public List<string> GenerateStringList(int count = 5, int stringLength = 10)
    {
        return Enumerable.Range(0, count)
            .Select(_ => GenerateString(stringLength))
            .ToList();
    }
    
    /// <summary>
    /// Generates a list of random integers.
    /// </summary>
    /// <param name="count">The number of integers to generate.</param>
    /// <param name="min">The minimum value (inclusive).</param>
    /// <param name="max">The maximum value (exclusive).</param>
    /// <returns>A list of random integers.</returns>
    public List<int> GenerateIntList(int count = 5, int min = 0, int max = 1000)
    {
        return Enumerable.Range(0, count)
            .Select(_ => GenerateInt(min, max))
            .ToList();
    }
    
    /// <summary>
    /// Generates a list of random decimals.
    /// </summary>
    /// <param name="count">The number of decimals to generate.</param>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    /// <param name="decimalPlaces">The number of decimal places.</param>
    /// <returns>A list of random decimals.</returns>
    public List<decimal> GenerateDecimalList(
        int count = 5,
        decimal min = 0,
        decimal max = 1000,
        int decimalPlaces = 2)
    {
        return Enumerable.Range(0, count)
            .Select(_ => GenerateDecimal(min, max, decimalPlaces))
            .ToList();
    }
    
    /// <summary>
    /// Generates a HashSet of random strings with unique values.
    /// </summary>
    /// <param name="count">The number of strings to generate.</param>
    /// <param name="stringLength">The length of each string.</param>
    /// <returns>A HashSet of random strings.</returns>
    public HashSet<string> GenerateStringSet(int count = 5, int stringLength = 10)
    {
        var set = new HashSet<string>();
        
        while (set.Count < count)
        {
            set.Add(GenerateString(stringLength));
        }
        
        return set;
    }
    
    /// <summary>
    /// Generates a HashSet of random integers with unique values.
    /// </summary>
    /// <param name="count">The number of integers to generate.</param>
    /// <param name="min">The minimum value (inclusive).</param>
    /// <param name="max">The maximum value (exclusive).</param>
    /// <returns>A HashSet of random integers.</returns>
    public HashSet<int> GenerateIntSet(int count = 5, int min = 0, int max = 1000)
    {
        if (max - min < count)
        {
            throw new ArgumentException(
                "Range is too small to generate the requested number of unique values");
        }
        
        var set = new HashSet<int>();
        
        while (set.Count < count)
        {
            set.Add(GenerateInt(min, max));
        }
        
        return set;
    }
    
    /// <summary>
    /// Generates a HashSet of random byte arrays.
    /// </summary>
    /// <param name="count">The number of byte arrays to generate.</param>
    /// <param name="length">The length of each byte array.</param>
    /// <returns>A HashSet of random byte arrays.</returns>
    public HashSet<byte[]> GenerateBytesSet(int count = 5, int length = 16)
    {
        var set = new HashSet<byte[]>(new ByteArrayEqualityComparer());
        
        while (set.Count < count)
        {
            set.Add(GenerateBytes(length));
        }
        
        return set;
    }
    
    /// <summary>
    /// Generates a dictionary with random string keys and string values.
    /// </summary>
    /// <param name="count">The number of key-value pairs to generate.</param>
    /// <param name="keyLength">The length of each key.</param>
    /// <param name="valueLength">The length of each value.</param>
    /// <returns>A dictionary with random string keys and values.</returns>
    public Dictionary<string, string> GenerateStringDictionary(
        int count = 5,
        int keyLength = 10,
        int valueLength = 20)
    {
        var dictionary = new Dictionary<string, string>();
        
        while (dictionary.Count < count)
        {
            var key = GenerateString(keyLength);
            if (!dictionary.ContainsKey(key))
            {
                dictionary[key] = GenerateString(valueLength);
            }
        }
        
        return dictionary;
    }
    
    /// <summary>
    /// Generates a dictionary with random string keys and integer values.
    /// </summary>
    /// <param name="count">The number of key-value pairs to generate.</param>
    /// <param name="keyLength">The length of each key.</param>
    /// <param name="min">The minimum integer value.</param>
    /// <param name="max">The maximum integer value.</param>
    /// <returns>A dictionary with random string keys and integer values.</returns>
    public Dictionary<string, int> GenerateStringIntDictionary(
        int count = 5,
        int keyLength = 10,
        int min = 0,
        int max = 1000)
    {
        var dictionary = new Dictionary<string, int>();
        
        while (dictionary.Count < count)
        {
            var key = GenerateString(keyLength);
            if (!dictionary.ContainsKey(key))
            {
                dictionary[key] = GenerateInt(min, max);
            }
        }
        
        return dictionary;
    }
    
    /// <summary>
    /// Picks a random element from the provided collection.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="items">The collection to pick from.</param>
    /// <returns>A random element from the collection.</returns>
    public T PickRandom<T>(IEnumerable<T> items)
    {
        var list = items.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("Cannot pick from an empty collection");
        }
        
        return list[_random.Next(list.Count)];
    }
    
    /// <summary>
    /// Picks multiple random elements from the provided collection.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="items">The collection to pick from.</param>
    /// <param name="count">The number of elements to pick.</param>
    /// <param name="allowDuplicates">Whether to allow duplicate selections.</param>
    /// <returns>A list of random elements from the collection.</returns>
    public List<T> PickRandomMultiple<T>(
        IEnumerable<T> items,
        int count,
        bool allowDuplicates = false)
    {
        var list = items.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("Cannot pick from an empty collection");
        }
        
        if (!allowDuplicates && count > list.Count)
        {
            throw new ArgumentException(
                "Cannot pick more unique items than available in the collection");
        }
        
        var result = new List<T>();
        
        if (allowDuplicates)
        {
            for (var i = 0; i < count; i++)
            {
                result.Add(list[_random.Next(list.Count)]);
            }
        }
        else
        {
            var available = new List<T>(list);
            for (var i = 0; i < count; i++)
            {
                var index = _random.Next(available.Count);
                result.Add(available[index]);
                available.RemoveAt(index);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Shuffles a collection randomly.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="items">The collection to shuffle.</param>
    /// <returns>A shuffled list.</returns>
    public List<T> Shuffle<T>(IEnumerable<T> items)
    {
        var list = items.ToList();
        var n = list.Count;
        
        while (n > 1)
        {
            n--;
            var k = _random.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
        
        return list;
    }
    
    /// <summary>
    /// Equality comparer for byte arrays used in HashSet operations.
    /// </summary>
    private class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.SequenceEqual(y);
        }
        
        public int GetHashCode(byte[] obj)
        {
            if (obj == null) return 0;
            
            unchecked
            {
                var hash = 17;
                foreach (var b in obj)
                {
                    hash = hash * 31 + b;
                }
                return hash;
            }
        }
    }
}
