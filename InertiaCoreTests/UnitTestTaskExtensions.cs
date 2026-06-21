using InertiaCore.Utils;

namespace InertiaCoreTests;

public partial class Tests
{
    [Test]
    [Description("Task<string> unwraps to correct string value.")]
    public async Task TestTaskOfStringUnwrapsToString()
    {
        Task task = Task.FromResult("hello");
        var result = await task.UnwrapResultAsync();

        Assert.That(result, Is.EqualTo("hello"));
    }

    [Test]
    [Description("Task<int> unwraps to correct int value (boxed).")]
    public async Task TestTaskOfIntUnwrapsToInt()
    {
        Task task = Task.FromResult(42);
        var result = await task.UnwrapResultAsync();

        Assert.That(result, Is.EqualTo(42));
        Assert.That(result, Is.TypeOf<int>());
    }

    [Test]
    [Description("Non-generic Task unwraps to null without throwing.")]
    public async Task TestNonGenericTaskUnwrapsToNull()
    {
        Task task = Task.CompletedTask;
        var result = await task.UnwrapResultAsync();

        Assert.That(result, Is.Null);
    }

    [Test]
    [Description("Completed task unwraps without blocking.")]
    public async Task TestCompletedTaskDoesNotBlock()
    {
        Task task = Task.FromResult("done");
        var result = await task.UnwrapResultAsync();

        Assert.That(result, Is.EqualTo("done"));
    }

    [Test]
    [Description("Task from async lambda unwraps correctly.")]
    public async Task TestAsyncLambdaTaskUnwraps()
    {
        Task task = AsyncReturnString();
        var result = await task.UnwrapResultAsync();

        Assert.That(result, Is.EqualTo("async value"));
    }

    private static async Task<string> AsyncReturnString()
    {
        await Task.Yield();
        return "async value";
    }
}
