using FCCCodeDesktop.Runtime;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class ProcessOutputPolicyTests
{
    [Fact]
    public void DefaultsAreFiniteAndInternallyConsistent()
    {
        var policy = ProcessOutputPolicy.Default;

        Assert.InRange(
            policy.MaximumRetainedEntries,
            1,
            ProcessOutputPolicy.MaximumSupportedRetainedEntries);
        Assert.InRange(
            policy.MaximumRetainedUtf8Bytes,
            1,
            ProcessOutputPolicy.MaximumSupportedRetainedUtf8Bytes);
        Assert.InRange(
            policy.MaximumPendingDeliveryEntries,
            1,
            ProcessOutputPolicy.MaximumSupportedPendingDeliveryEntries);
        Assert.True(policy.MaximumEntryCharacters <= policy.MaximumPartialLineCharacters);
        Assert.True(policy.MaximumEntryUtf8Bytes <= policy.MaximumRetainedUtf8Bytes);
        Assert.True(policy.ReadBufferCharacters <= ProcessOutputPolicy.MaximumSupportedReadBufferCharacters);
    }

    [Fact]
    public void CustomPolicyPreservesEveryExplicitBound()
    {
        var policy = new ProcessOutputPolicy(
            maximumRetainedEntries: 8,
            maximumRetainedUtf8Bytes: 1_024,
            maximumEntryCharacters: 64,
            maximumEntryUtf8Bytes: 256,
            maximumPartialLineCharacters: 128,
            maximumPendingDeliveryEntries: 4,
            readBufferCharacters: 16);

        Assert.Equal(8, policy.MaximumRetainedEntries);
        Assert.Equal(1_024, policy.MaximumRetainedUtf8Bytes);
        Assert.Equal(64, policy.MaximumEntryCharacters);
        Assert.Equal(256, policy.MaximumEntryUtf8Bytes);
        Assert.Equal(128, policy.MaximumPartialLineCharacters);
        Assert.Equal(4, policy.MaximumPendingDeliveryEntries);
        Assert.Equal(16, policy.ReadBufferCharacters);
    }

    [Fact]
    public void RejectsEveryInvalidOrContradictoryBound()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProcessOutputPolicy(maximumRetainedEntries: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProcessOutputPolicy(
                maximumRetainedEntries: ProcessOutputPolicy.MaximumSupportedRetainedEntries + 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProcessOutputPolicy(maximumRetainedUtf8Bytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProcessOutputPolicy(maximumEntryCharacters: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProcessOutputPolicy(maximumEntryUtf8Bytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProcessOutputPolicy(maximumPartialLineCharacters: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProcessOutputPolicy(maximumPendingDeliveryEntries: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ProcessOutputPolicy(readBufferCharacters: 0));
        Assert.Throws<ArgumentException>(
            () => new ProcessOutputPolicy(
                maximumRetainedUtf8Bytes: 8,
                maximumEntryUtf8Bytes: 9));
        Assert.Throws<ArgumentException>(
            () => new ProcessOutputPolicy(
                maximumEntryCharacters: 9,
                maximumPartialLineCharacters: 8));
    }

    [Fact]
    public void CorrelationRejectsEmptyDurableIdentities()
    {
        Assert.Throws<ArgumentException>(() => new ProcessOutputCorrelation(taskId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ProcessOutputCorrelation(agentRunId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ProcessOutputCorrelation(toolRunId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ProcessOutputCorrelation(processRunId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ProcessOutputCorrelation(operationId: Guid.Empty));
    }
}
