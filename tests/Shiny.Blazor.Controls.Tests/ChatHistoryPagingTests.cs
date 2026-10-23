#pragma warning disable BL0006 // RenderTree types: a minimal interactive renderer is the only way to reach OnAfterRender
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Shiny.Blazor.Controls.Chat;
using Shouldly;
using Xunit;

namespace Shiny.Blazor.Controls.Tests;

/// <summary>
/// Switching conversations while an older-history page is still in flight let the late answer land
/// in the new conversation: the previous chat's messages were prepended to it, and its HasMore
/// overwrote the new one's, which could stop history loading for good.
/// </summary>
public class ChatHistoryPagingTests
{
    static T Field<T>(ChatView chat, string name) => (T)typeof(ChatView)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(chat)!;

    static IEnumerable<string> Ids(ChatView chat) => Field<List<ChatMessage>>(chat, "messages").Select(m => m.MessageId);


    [Fact]
    public async Task ALateHistoryPageFromThePreviousConversationIsDropped()
    {
        var a = new PagingSession("a", "a1", "a2");
        var b = new PagingSession("b", "b1", "b2");
        var provider = new Provider(a, b);
        var js = new ModuleJs();
        var renderer = new TestRenderer(Services(js));

        ChatView chat = null!;
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            chat = (ChatView)renderer.InstantiateComponent(typeof(ChatView));
            var id = renderer.AssignRootComponentId(chat);
            await renderer.RenderRootComponentAsync(id, Parameters(provider, "a"));
        });
        await js.Module.Initialised.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Ids(chat).ShouldBe(["a1", "a2"]);

        _ = renderer.Dispatcher.InvokeAsync(chat.OnScrolledToTop);
        await a.OlderRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await renderer.Dispatcher.InvokeAsync(() => chat.SetParametersAsync(Parameters(provider, "b")));
        Ids(chat).ShouldBe(["b1", "b2"]);

        a.ReleaseOlder(new MessagePage([PagingSession.Message("a0", 60)], false));
        await Task.Delay(50);

        await renderer.Dispatcher.InvokeAsync(() =>
        {
            Ids(chat).ShouldBe(["b1", "b2"], "a's history must not reach b");
            Field<bool>(chat, "hasMoreOlder").ShouldBeTrue("a's HasMore=false must not end b's paging");
            Field<bool>(chat, "loadingOlder").ShouldBeFalse();
        });

        // And b can page straight away rather than waiting on a's request.
        _ = renderer.Dispatcher.InvokeAsync(chat.OnScrolledToTop);
        await b.OlderRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }


    static ParameterView Parameters(IChatSessionProvider provider, string sessionId) => ParameterView.FromDictionary(new Dictionary<string, object?>
    {
        [nameof(ChatView.Provider)] = provider,
        [nameof(ChatView.SessionId)] = sessionId
    });


    static ServiceProvider Services(IJSRuntime js)
    {
        var services = new ServiceCollection()
            .AddSingleton(js)
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddShinyControls();
        return services.BuildServiceProvider();
    }


    sealed class TestRenderer(IServiceProvider services) : Renderer(services, NullLoggerFactory.Instance)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        public new IComponent InstantiateComponent(Type type) => base.InstantiateComponent(type);

        public new int AssignRootComponentId(IComponent component) => base.AssignRootComponentId(component);

        public new Task RenderRootComponentAsync(int id, ParameterView parameters)
            => base.RenderRootComponentAsync(id, parameters);

        protected override void HandleException(Exception exception) => throw exception;

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    }


    sealed class ModuleJs : IJSRuntime
    {
        public FakeModule Module { get; } = new();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => identifier == "import" ? new ValueTask<TValue>((TValue)(object)this.Module) : default;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => this.InvokeAsync<TValue>(identifier, args);
    }


    sealed class FakeModule : IJSObjectReference
    {
        public TaskCompletionSource Initialised { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "init")
                this.Initialised.TrySetResult();
            return default;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => this.InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync() => default;
    }


    /// <summary>
    /// Answers the first (cursor-less) page at once and holds every older-history page until the test
    /// releases it.
    /// </summary>
    sealed class PagingSession(string sessionId, params string[] initialIds) : IChatSession
    {
        readonly TaskCompletionSource<MessagePage> olderGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource OlderRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int OlderRequestCount;

        public void ReleaseOlder(MessagePage page) => this.olderGate.TrySetResult(page);

        public static ChatMessage Message(string id, int minutesAgo) => new(
            id, null, "them", id, null, MessageStatus.Sent, null,
            DateTimeOffset.UtcNow.AddMinutes(-minutesAgo), null, [], []);

        public ChatSessionInfo Info { get; } = new(
            sessionId, sessionId, [], null, MessageBodyPermissions.None, ChatSessionPermissions.None,
            DateTimeOffset.UtcNow, null, 0);

        public string CurrentUserId => "me";

        public Task<MessagePage> GetMessagesAsync(string? cursorMessageId, MessagePageDirection direction, int count, CancellationToken cancellationToken = default)
        {
            if (cursorMessageId is null)
                return Task.FromResult(new MessagePage(initialIds.Select((id, i) => Message(id, initialIds.Length - i)).ToList(), true));

            Interlocked.Increment(ref this.OlderRequestCount);
            this.OlderRequested.TrySetResult();

            // Deliberately ignores the token - a provider is not obliged to honour it, and the late
            // answer is exactly what the view has to survive.
            return this.olderGate.Task;
        }

        public Task<ChatMessage> SendMessageAsync(OutgoingMessage message, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ChatMessage> ResendMessageAsync(string clientMessageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EditMessageAsync(string messageId, string body, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteMessageAsync(string messageId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReactToMessageAsync(string messageId, string emoji, bool add, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkReadAsync(string[] messageIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ToggleTypingAsync(bool isTyping, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InviteUserAsync(string userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LeaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RenameAsync(string sessionName, CancellationToken cancellationToken = default) => Task.CompletedTask;

#pragma warning disable CS0067
        public event EventHandler<ChatMessage>? MessageReceived;
        public event EventHandler<MessageChanged>? MessageUpdated;
        public event EventHandler<string>? MessageDeleted;
        public event EventHandler<UserTypingEvent>? UserTyping;
        public event EventHandler<ChatSessionUserInfo>? UserJoined;
        public event EventHandler<ChatSessionUserInfo>? UserLeft;
        public event EventHandler<ChatSessionInfo>? SessionUpdated;
        public event EventHandler<ChatConnectionState>? ConnectionStateChanged;
#pragma warning restore CS0067

        public ValueTask DisposeAsync() => default;
    }


    sealed class Provider(params PagingSession[] sessions) : IChatSessionProvider
    {
        public Task<IChatSession> CreateSessionAsync(string[] userIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IChatSession> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult<IChatSession>(sessions.Single(s => s.Info.SessionId == sessionId));
    }
}
