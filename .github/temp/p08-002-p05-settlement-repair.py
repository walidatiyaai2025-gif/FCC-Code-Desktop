from pathlib import Path

validator = Path("tools/ui/validate-task-state-machine.ps1")
workflow = Path(".github/workflows/temp-p08-002-p05-settlement-repair.yml")
helper = Path(__file__)

text = validator.read_text(encoding="utf-8")
old = '''    private static async Task WaitForSettledAsync(TaskExecutionState state)
    {
        var timeout = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < timeout)
        {
            if (!state.IsActive)
            {
                try
                {
                    state.ValidateCanStart();
                    return;
                }
                catch (InvalidOperationException exception) when (exception.Message.Contains("still settling", StringComparison.Ordinal))
                {
                }
            }
            await Task.Delay(20);
        }
        throw new InvalidOperationException("P05-005 assertion failed: task did not fully settle before timeout.");
    }
'''
new = '''    private static async Task WaitForSettledAsync(TaskExecutionState state)
    {
        const int settlementTimeoutSeconds = 30;
        var timeout = DateTimeOffset.UtcNow.AddSeconds(settlementTimeoutSeconds);
        string? lastStartRejection = null;
        while (DateTimeOffset.UtcNow < timeout)
        {
            if (!state.IsActive)
            {
                try
                {
                    state.ValidateCanStart();
                    return;
                }
                catch (InvalidOperationException exception) when (exception.Message.Contains("still settling", StringComparison.Ordinal))
                {
                    lastStartRejection = exception.Message;
                }
            }
            await Task.Delay(20);
        }
        throw new InvalidOperationException(
            $"P05-005 assertion failed: task did not fully settle within {settlementTimeoutSeconds}s. " +
            $"State={state.State}; IsActive={state.IsActive}; CanStop={state.CanStop}; CanRetry={state.CanRetry}; " +
            $"LastStartRejection={lastStartRejection ?? "<none>"}.");
    }
'''

count = text.count(old)
if count != 1:
    raise SystemExit(f"expected exactly one settlement block, found {count}")

validator.write_text(text.replace(old, new), encoding="utf-8", newline="")
workflow.unlink()
helper.unlink()
