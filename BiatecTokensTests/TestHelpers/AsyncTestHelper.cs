using System;
using System.Threading.Tasks;

namespace BiatecTokensTests.TestHelpers
{
    /// <summary>
    /// Helper utilities for writing deterministic async tests without hard-coded delays.
    /// 
    /// Business Value: Reduces test flakiness in CI environments by replacing fixed delays
    /// with condition-based waiting, improving test reliability and reducing false failures.
    /// 
    /// Usage Example:
    /// <code>
    /// // Instead of: await Task.Delay(200);
    /// // Use: await AsyncTestHelper.WaitForConditionAsync(() => deliveries.Count > 0, TimeSpan.FromSeconds(2));
    /// </code>
    /// </summary>
    public static class AsyncTestHelper
    {
        /// <summary>
        /// Polls a condition until it becomes true or timeout is reached.
        /// This is more deterministic than Task.Delay() because it checks as soon as the condition is met.
        /// </summary>
        /// <param name="condition">The condition to check</param>
        /// <param name="timeout">Maximum time to wait (default: 5 seconds)</param>
        /// <param name="pollInterval">How often to check the condition (default: 50ms)</param>
        /// <returns>True if condition became true within timeout, false otherwise</returns>
        public static async Task<bool> WaitForConditionAsync(
            Func<bool> condition,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null)
        {
            var maxWait = timeout ?? TimeSpan.FromSeconds(5);
            var interval = pollInterval ?? TimeSpan.FromMilliseconds(50);
            var endTime = DateTime.UtcNow.Add(maxWait);

            while (DateTime.UtcNow < endTime)
            {
                if (condition())
                {
                    return true;
                }

                await Task.Delay(interval);
            }

            return condition(); // One last check
        }

        /// <summary>
        /// Polls an async condition until it becomes true or timeout is reached.
        /// </summary>
        /// <param name="conditionAsync">The async condition to check</param>
        /// <param name="timeout">Maximum time to wait (default: 5 seconds)</param>
        /// <param name="pollInterval">How often to check the condition (default: 50ms)</param>
        /// <returns>True if condition became true within timeout, false otherwise</returns>
        public static async Task<bool> WaitForConditionAsync(
            Func<Task<bool>> conditionAsync,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null)
        {
            var maxWait = timeout ?? TimeSpan.FromSeconds(5);
            var interval = pollInterval ?? TimeSpan.FromMilliseconds(50);
            var endTime = DateTime.UtcNow.Add(maxWait);

            while (DateTime.UtcNow < endTime)
            {
                if (await conditionAsync())
                {
                    return true;
                }

                await Task.Delay(interval);
            }

            return await conditionAsync(); // One last check
        }

        /// <summary>
        /// Waits until a value retrieved by valueGetter meets the expected condition.
        /// This is useful for waiting on repository queries or service responses.
        /// </summary>
        /// <typeparam name="T">Type of value to check</typeparam>
        /// <param name="valueGetter">Function to get the current value</param>
        /// <param name="condition">Condition the value must meet</param>
        /// <param name="timeout">Maximum time to wait (default: 5 seconds)</param>
        /// <param name="pollInterval">How often to check (default: 50ms)</param>
        /// <returns>The value when condition is met, or the last retrieved value if timeout</returns>
        public static async Task<T?> WaitForValueAsync<T>(
            Func<Task<T>> valueGetter,
            Func<T, bool> condition,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null)
        {
            var maxWait = timeout ?? TimeSpan.FromSeconds(5);
            var interval = pollInterval ?? TimeSpan.FromMilliseconds(50);
            var endTime = DateTime.UtcNow.Add(maxWait);

            T? lastValue = default;

            while (DateTime.UtcNow < endTime)
            {
                lastValue = await valueGetter();
                if (condition(lastValue))
                {
                    return lastValue;
                }

                await Task.Delay(interval);
            }

            return lastValue; // Return last value even if condition not met
        }

        /// <summary>
        /// Waits for a count condition to be met (useful for waiting on collection sizes).
        /// </summary>
        /// <param name="countGetter">Function that returns current count</param>
        /// <param name="expectedCount">Expected count value</param>
        /// <param name="timeout">Maximum time to wait (default: 5 seconds)</param>
        /// <param name="pollInterval">How often to check (default: 50ms)</param>
        /// <returns>True if count reached expected value, false otherwise</returns>
        public static async Task<bool> WaitForCountAsync(
            Func<Task<int>> countGetter,
            int expectedCount,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null)
        {
            var maxWait = timeout ?? TimeSpan.FromSeconds(5);
            var interval = pollInterval ?? TimeSpan.FromMilliseconds(50);
            var endTime = DateTime.UtcNow.Add(maxWait);

            while (DateTime.UtcNow < endTime)
            {
                var currentCount = await countGetter();
                if (currentCount >= expectedCount)
                {
                    return true;
                }

                await Task.Delay(interval);
            }

            var finalCount = await countGetter();
            return finalCount >= expectedCount;
        }
    }
}
